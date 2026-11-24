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
[Route("api/empleados")]
public class EmpleadosController(AppDbContext db, CryptoService crypto, AuditService audit) : ApiControllerBase
{
    private static readonly string[] EstadosValidos = ["Activo", "Vacaciones", "Suspendido", "Baja"];
    private const int TamPagina = 10;

    private string Mascara(byte[] cifrada) => CryptoService.Mask(crypto.Decrypt(cifrada));

    [HttpGet]
    [Authorize(Roles = "RRHH,Admin,Jefe")]
    public async Task<IActionResult> Listar([FromQuery] string? departamento, [FromQuery] string? estado,
        [FromQuery] string? buscar, [FromQuery] int pagina = 1)
    {
        IQueryable<Empleado> q = EmpleadosVisibles(db).Include(e => e.Departamento).Include(e => e.Cargo);
        if (!string.IsNullOrWhiteSpace(departamento)) q = q.Where(e => e.Departamento.Nombre == departamento);
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(e => e.Estado == estado);
        if (!string.IsNullOrWhiteSpace(buscar))
            q = q.Where(e => e.Nombres.Contains(buscar) || e.Apellidos.Contains(buscar) || e.Codigo.Contains(buscar));

        pagina = Math.Max(1, pagina);
        var total = await q.CountAsync();
        var items = await q.OrderBy(e => e.Apellidos).ThenBy(e => e.Nombres)
            .Skip((pagina - 1) * TamPagina).Take(TamPagina).ToListAsync();

        return Ok(new
        {
            total,
            pagina,
            totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)TamPagina)),
            items = items.Select(e => new
            {
                e.IdEmpleado,
                e.Codigo,
                NombreCompleto = $"{e.Nombres} {e.Apellidos}",
                Cargo = e.Cargo.Nombre,
                Departamento = e.Departamento.Nombre,
                e.FechaIngreso,
                e.Estado,
                CedulaMascarada = Mascara(e.CedulaCifrada)
            })
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        if (!await PuedeVerAsync(db, id)) return Forbid();
        var e = await db.Empleados.Include(x => x.Departamento).Include(x => x.Cargo)
            .FirstOrDefaultAsync(x => x.IdEmpleado == id);
        if (e is null) return NotFound();
        return Ok(new
        {
            e.IdEmpleado, e.Codigo, e.Nombres, e.Apellidos, e.FechaNacimiento, e.Correo,
            e.IdDepartamento, Departamento = e.Departamento.Nombre,
            e.IdCargo, Cargo = e.Cargo.Nombre,
            e.FechaIngreso, e.Estado,
            CedulaMascarada = Mascara(e.CedulaCifrada)
        });
    }

    [HttpPost]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> Crear([FromBody] EmpleadoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Cedula))
            return BadRequest(new { mensaje = "La cédula es obligatoria." });
        if (await db.Empleados.AnyAsync(x => x.Codigo == r.Codigo))
            return Conflict(new { mensaje = "El código de empleado ya existe." });
        if (!await db.Departamentos.AnyAsync(d => d.IdDepartamento == r.IdDepartamento) ||
            !await db.Cargos.AnyAsync(c => c.IdCargo == r.IdCargo))
            return BadRequest(new { mensaje = "Departamento o cargo inexistente." });

        var nuevo = new Empleado
        {
            Codigo = r.Codigo,
            CedulaCifrada = crypto.Encrypt(r.Cedula),
            Nombres = r.Nombres,
            Apellidos = r.Apellidos,
            FechaNacimiento = r.FechaNacimiento,
            Correo = r.Correo,
            IdDepartamento = r.IdDepartamento,
            IdCargo = r.IdCargo,
            FechaIngreso = r.FechaIngreso,
            Estado = "Activo"
        };
        db.Empleados.Add(nuevo);
        await db.SaveChangesAsync();

        audit.Registrar("Empleado", nuevo.IdEmpleado.ToString(), "Crear", UsuarioId, Ip, null,
            new { nuevo.Codigo, nuevo.Nombres, nuevo.Apellidos, nuevo.IdDepartamento, nuevo.IdCargo });
        await db.SaveChangesAsync();
        return Created($"/api/empleados/{nuevo.IdEmpleado}", new { nuevo.IdEmpleado });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] EmpleadoRequest r)
    {
        var emp = await db.Empleados.FirstOrDefaultAsync(x => x.IdEmpleado == id);
        if (emp is null) return NotFound();

        var antes = new { emp.Nombres, emp.Apellidos, emp.Correo, emp.IdDepartamento, emp.IdCargo, emp.Estado };
        emp.Nombres = r.Nombres;
        emp.Apellidos = r.Apellidos;
        emp.FechaNacimiento = r.FechaNacimiento;
        emp.Correo = r.Correo;
        emp.IdDepartamento = r.IdDepartamento;
        emp.IdCargo = r.IdCargo;
        emp.FechaIngreso = r.FechaIngreso;
        if (!string.IsNullOrWhiteSpace(r.Cedula)) emp.CedulaCifrada = crypto.Encrypt(r.Cedula);

        audit.Registrar("Empleado", id.ToString(), "Actualizar", UsuarioId, Ip, antes,
            new { emp.Nombres, emp.Apellidos, emp.Correo, emp.IdDepartamento, emp.IdCargo, emp.Estado });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/estado")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] EstadoEmpleadoRequest r)
    {
        if (!EstadosValidos.Contains(r.Estado))
            return BadRequest(new { mensaje = "Estado no válido.", validos = EstadosValidos });
        var emp = await db.Empleados.FirstOrDefaultAsync(x => x.IdEmpleado == id);
        if (emp is null) return NotFound();

        var anterior = emp.Estado;
        emp.Estado = r.Estado;
        audit.Registrar("Empleado", id.ToString(), "CambiarEstado", UsuarioId, Ip,
            new { Estado = anterior }, new { Estado = r.Estado });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/datos-sensibles")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> DatosSensibles(int id)
    {
        if (!await db.Empleados.AnyAsync(e => e.IdEmpleado == id)) return NotFound();
        var contrato = await db.Contratos.Where(c => c.IdEmpleado == id && c.Activo)
            .OrderByDescending(c => c.FechaInicio).FirstOrDefaultAsync();
        decimal? salario = contrato is null ? null : crypto.DecryptDecimal(contrato.SalarioCifrado);

        // Toda lectura de datos sensibles queda registrada.
        audit.Registrar("Empleado", id.ToString(), "LecturaSensible", UsuarioId, Ip, null, null);
        await db.SaveChangesAsync();
        return Ok(new { IdEmpleado = id, SalarioBase = salario });
    }

    [HttpGet("{id:int}/contratos")]
    public async Task<IActionResult> Contratos(int id)
    {
        if (!await PuedeVerAsync(db, id)) return Forbid();
        var lista = await db.Contratos.Where(c => c.IdEmpleado == id)
            .OrderByDescending(c => c.FechaInicio)
            .Select(c => new { c.IdContrato, c.Tipo, c.FechaInicio, c.FechaFin, c.Activo })
            .ToListAsync();
        return Ok(lista);
    }

    [HttpPost("{id:int}/contratos")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> CrearContrato(int id, [FromBody] ContratoRequest r)
    {
        if (!await db.Empleados.AnyAsync(e => e.IdEmpleado == id)) return NotFound();

        // Un solo contrato activo por empleado: el anterior se cierra un día antes del nuevo.
        var activos = await db.Contratos.Where(c => c.IdEmpleado == id && c.Activo).ToListAsync();
        foreach (var previo in activos)
        {
            previo.Activo = false;
            previo.FechaFin ??= r.FechaInicio.AddDays(-1);
        }

        var nuevo = new Contrato
        {
            IdEmpleado = id,
            Tipo = r.Tipo,
            FechaInicio = r.FechaInicio,
            FechaFin = r.FechaFin,
            SalarioCifrado = crypto.EncryptDecimal(r.Salario),
            Activo = true
        };
        db.Contratos.Add(nuevo);

        // El salario no se escribe en la auditoría; queda cifrado en el contrato.
        audit.Registrar("Contrato", id.ToString(), "Crear", UsuarioId, Ip, null,
            new { r.Tipo, r.FechaInicio, r.FechaFin });
        await db.SaveChangesAsync();
        return Created($"/api/empleados/{id}/contratos", new { nuevo.IdContrato });
    }
}
