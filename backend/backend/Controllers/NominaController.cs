using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rrhh.Api.Contracts;
using Rrhh.Api.Services;
using Rrhh.Domain.Entities;
using Rrhh.Infrastructure;

namespace Rrhh.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/nomina")]
public class NominaController(AppDbContext db, CryptoService crypto, AuditService audit) : ApiControllerBase
{
    private static readonly string[] TiposPeriodo = ["Mensual", "Quincenal"];
    private static readonly string[] TiposConcepto = ["Ingreso", "Descuento"];

    [HttpGet("periodos")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> Periodos() =>
        Ok(await db.PeriodosNomina.OrderByDescending(p => p.Anio).ThenByDescending(p => p.Mes).ToListAsync());

    [HttpPost("periodos")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> CrearPeriodo([FromBody] PeriodoRequest r)
    {
        if (r.Mes < 1 || r.Mes > 12) return BadRequest(new { mensaje = "Mes no válido." });
        if (!TiposPeriodo.Contains(r.Tipo)) return BadRequest(new { mensaje = "Tipo de periodo no válido." });
        if (await db.PeriodosNomina.AnyAsync(p => p.Anio == r.Anio && p.Mes == r.Mes && p.Tipo == r.Tipo))
            return Conflict(new { mensaje = "Ese periodo ya existe." });

        var nuevo = new PeriodoNomina { Anio = r.Anio, Mes = r.Mes, Tipo = r.Tipo, FechaPago = r.FechaPago, Estado = "Abierto" };
        db.PeriodosNomina.Add(nuevo);
        await db.SaveChangesAsync();
        return Created($"/api/nomina/periodos/{nuevo.IdPeriodo}", new { nuevo.IdPeriodo });
    }

    /// Genera un recibo por empleado con contrato activo. Recalcula si el periodo se calcula de nuevo.
    [HttpPost("periodos/{id:int}/calcular")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> Calcular(int id)
    {
        var periodo = await db.PeriodosNomina.FirstOrDefaultAsync(p => p.IdPeriodo == id);
        if (periodo is null) return NotFound();
        if (periodo.Estado == "Cerrado") return Conflict(new { mensaje = "El periodo ya está cerrado." });

        var conceptos = await db.ConceptosNomina.ToListAsync();
        var contratos = await db.Contratos.Where(c => c.Activo).ToListAsync();

        // Se eliminan los recibos previos antes de crear los nuevos (la unicidad por periodo y empleado lo exige).
        var previos = await db.RecibosNomina.Where(r => r.IdPeriodo == id).Include(r => r.Detalles).ToListAsync();
        if (previos.Count > 0)
        {
            db.DetallesRecibo.RemoveRange(previos.SelectMany(r => r.Detalles));
            db.RecibosNomina.RemoveRange(previos);
            await db.SaveChangesAsync();
        }

        var ahora = DateTime.UtcNow;
        foreach (var contrato in contratos)
        {
            var mensual = crypto.DecryptDecimal(contrato.SalarioCifrado);
            var bruto = periodo.Tipo == "Quincenal" ? Math.Round(mensual / 2, 2) : mensual;

            var recibo = new ReciboNomina
            {
                IdPeriodo = id,
                IdEmpleado = contrato.IdEmpleado,
                SalarioBruto = bruto,
                FechaCalculo = ahora
            };

            decimal ingresos = 0, descuentos = 0;
            foreach (var concepto in conceptos)
            {
                var monto = Math.Round(bruto * concepto.Porcentaje / 100, 2);
                recibo.Detalles.Add(new DetalleRecibo { IdConcepto = concepto.IdConcepto, Monto = monto });
                if (concepto.Tipo == "Ingreso") ingresos += monto;
                else descuentos += monto;
            }

            recibo.TotalIngresos = ingresos;
            recibo.TotalDescuentos = descuentos;
            recibo.SalarioNeto = bruto + ingresos - descuentos;
            db.RecibosNomina.Add(recibo);
        }

        periodo.Estado = "Calculado";
        audit.Registrar("PeriodoNomina", id.ToString(), "Calcular", UsuarioId, Ip, null, new { Recibos = contratos.Count });
        await db.SaveChangesAsync();
        return Ok(new { Recibos = contratos.Count });
    }

    [HttpPost("periodos/{id:int}/cerrar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Cerrar(int id)
    {
        var periodo = await db.PeriodosNomina.FirstOrDefaultAsync(p => p.IdPeriodo == id);
        if (periodo is null) return NotFound();
        if (periodo.Estado == "Cerrado") return Conflict(new { mensaje = "El periodo ya está cerrado." });

        periodo.Estado = "Cerrado";
        audit.Registrar("PeriodoNomina", id.ToString(), "Cerrar", UsuarioId, Ip, null, new { periodo.Estado });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("periodos/{id:int}/recibos")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> RecibosDePeriodo(int id) =>
        Ok(await db.RecibosNomina.Where(r => r.IdPeriodo == id)
            .OrderBy(r => r.Empleado.Apellidos)
            .Select(r => new
            {
                r.IdRecibo,
                r.IdEmpleado,
                Empleado = r.Empleado.Nombres + " " + r.Empleado.Apellidos,
                r.SalarioBruto,
                r.TotalIngresos,
                r.TotalDescuentos,
                r.SalarioNeto
            }).ToListAsync());

    /// Recibos del empleado en sesión (para el rol Empleado).
    [HttpGet("mis-recibos")]
    public async Task<IActionResult> MisRecibos()
    {
        if (EmpleadoId is not int yo) return Ok(Array.Empty<object>());
        return Ok(await db.RecibosNomina.Where(r => r.IdEmpleado == yo)
            .OrderByDescending(r => r.Periodo.Anio).ThenByDescending(r => r.Periodo.Mes)
            .Select(r => new
            {
                r.IdRecibo,
                r.IdPeriodo,
                Anio = r.Periodo.Anio,
                Mes = r.Periodo.Mes,
                r.SalarioBruto,
                r.TotalIngresos,
                r.TotalDescuentos,
                r.SalarioNeto
            }).ToListAsync());
    }

    [HttpGet("recibos/{id:long}")]
    public async Task<IActionResult> Recibo(long id)
    {
        var recibo = await db.RecibosNomina
            .Include(r => r.Periodo)
            .Include(r => r.Detalles).ThenInclude(d => d.Concepto)
            .FirstOrDefaultAsync(r => r.IdRecibo == id);
        if (recibo is null) return NotFound();
        if (!EsRRHH && EmpleadoId != recibo.IdEmpleado) return Forbid();

        return Ok(new
        {
            recibo.IdRecibo,
            recibo.IdEmpleado,
            Periodo = new { recibo.Periodo.Anio, recibo.Periodo.Mes, recibo.Periodo.Tipo },
            recibo.SalarioBruto,
            recibo.TotalIngresos,
            recibo.TotalDescuentos,
            recibo.SalarioNeto,
            Conceptos = recibo.Detalles.Select(d => new { d.Concepto.Nombre, d.Concepto.Tipo, d.Monto })
        });
    }

    [HttpGet("recibos/{id:long}/pdf")]
    public async Task<IActionResult> ReciboPdf(long id)
    {
        var recibo = await db.RecibosNomina.FirstOrDefaultAsync(r => r.IdRecibo == id);
        if (recibo is null) return NotFound();
        if (!EsRRHH && EmpleadoId != recibo.IdEmpleado) return Forbid();
        // Pendiente: la generación del PDF aún no está implementada.
        return StatusCode(501, new { mensaje = "La generación de PDF de recibos aún no está implementada." });
    }

    [HttpGet("conceptos")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> Conceptos() =>
        Ok(await db.ConceptosNomina.Select(c => new { c.IdConcepto, c.Nombre, c.Tipo, c.Porcentaje, c.EsObligatorio }).ToListAsync());

    [HttpPost("conceptos")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CrearConcepto([FromBody] ConceptoRequest r)
    {
        if (!TiposConcepto.Contains(r.Tipo)) return BadRequest(new { mensaje = "Tipo debe ser Ingreso o Descuento." });
        var nuevo = new ConceptoNomina { Nombre = r.Nombre, Tipo = r.Tipo, Porcentaje = r.Porcentaje, EsObligatorio = r.EsObligatorio };
        db.ConceptosNomina.Add(nuevo);
        await db.SaveChangesAsync();
        return Created($"/api/nomina/conceptos/{nuevo.IdConcepto}", new { nuevo.IdConcepto });
    }
}
