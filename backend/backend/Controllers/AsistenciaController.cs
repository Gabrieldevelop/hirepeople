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
[Route("api/asistencia")]
public class AsistenciaController(AppDbContext db, AuditService audit) : ApiControllerBase
{
    // Entrada posterior a esta hora se marca como Tardanza.
    private static readonly TimeOnly HoraLimite = new(8, 15);

    [HttpPost("marcas")]
    [Authorize(Roles = "Empleado")]
    public async Task<IActionResult> Marcar()
    {
        if (EmpleadoId is not int idEmp) return Forbid();
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var ahora = TimeOnly.FromDateTime(DateTime.Now);

        var reg = await db.RegistrosAsistencia.FirstOrDefaultAsync(r => r.IdEmpleado == idEmp && r.Fecha == hoy);
        if (reg is null)
        {
            reg = new RegistroAsistencia
            {
                IdEmpleado = idEmp,
                Fecha = hoy,
                HoraEntrada = ahora,
                Origen = "Web",
                Estado = ahora <= HoraLimite ? "Puntual" : "Tardanza"
            };
            db.RegistrosAsistencia.Add(reg);
            await db.SaveChangesAsync();
            return Created($"/api/asistencia/{reg.IdRegistro}", new { reg.IdRegistro, Tipo = "entrada", Hora = ahora });
        }

        if (reg.HoraSalida is not null)
            return Conflict(new { mensaje = "Ya registró entrada y salida hoy." });

        var entrada = reg.HoraEntrada ?? ahora;
        if ((ahora.ToTimeSpan() - entrada.ToTimeSpan()).TotalMinutes < 5)
            return Conflict(new { mensaje = "Marca duplicada: espere al menos 5 minutos." });

        reg.HoraSalida = ahora;
        reg.HorasTrabajadas = Math.Round((decimal)(ahora.ToTimeSpan() - entrada.ToTimeSpan()).TotalHours, 2);
        await db.SaveChangesAsync();
        return Ok(new { reg.IdRegistro, Tipo = "salida", Hora = ahora, reg.HorasTrabajadas });
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] DateOnly? fecha, [FromQuery] int? empleadoId)
    {
        var dia = fecha ?? DateOnly.FromDateTime(DateTime.Today);
        var visibles = EmpleadosVisibles(db).Select(e => e.IdEmpleado);
        var q = db.RegistrosAsistencia.Where(r => r.Fecha == dia && visibles.Contains(r.IdEmpleado));
        if (empleadoId is int eid) q = q.Where(r => r.IdEmpleado == eid);

        var items = await q.OrderBy(r => r.Empleado.Apellidos).Select(r => new
        {
            r.IdRegistro,
            r.IdEmpleado,
            Empleado = r.Empleado.Nombres + " " + r.Empleado.Apellidos,
            Departamento = r.Empleado.Departamento.Nombre,
            r.Fecha,
            r.HoraEntrada,
            r.HoraSalida,
            r.HorasTrabajadas,
            r.Estado,
            r.Justificacion
        }).ToListAsync();
        return Ok(items);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> Corregir(long id, [FromBody] CorreccionAsistenciaRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Justificacion))
            return BadRequest(new { mensaje = "La justificación es obligatoria." });

        var reg = await db.RegistrosAsistencia.FirstOrDefaultAsync(x => x.IdRegistro == id);
        if (reg is null) return NotFound();

        var antes = new { reg.HoraEntrada, reg.HoraSalida, reg.HorasTrabajadas, reg.Estado };
        reg.HoraEntrada = r.HoraEntrada ?? reg.HoraEntrada;
        reg.HoraSalida = r.HoraSalida ?? reg.HoraSalida;
        reg.Justificacion = r.Justificacion;

        if (reg.HoraEntrada is TimeOnly entrada)
        {
            reg.Estado = entrada <= HoraLimite ? "Puntual" : "Tardanza";
            if (reg.HoraSalida is TimeOnly salida)
                reg.HorasTrabajadas = Math.Round((decimal)(salida.ToTimeSpan() - entrada.ToTimeSpan()).TotalHours, 2);
        }

        audit.Registrar("RegistroAsistencia", id.ToString(), "Corregir", UsuarioId, Ip, antes,
            new { reg.HoraEntrada, reg.HoraSalida, reg.HorasTrabajadas, reg.Estado, r.Justificacion });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("resumen")]
    [Authorize(Roles = "RRHH,Admin,Jefe")]
    public async Task<IActionResult> Resumen([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta)
    {
        var visibles = EmpleadosVisibles(db).Select(e => e.IdEmpleado);
        var filas = await db.RegistrosAsistencia
            .Where(r => r.Fecha >= desde && r.Fecha <= hasta && visibles.Contains(r.IdEmpleado))
            .Select(r => new { r.IdEmpleado, r.Estado, r.HorasTrabajadas })
            .ToListAsync();

        // Agregación en memoria: el volumen por rango de fechas es acotado.
        var resumen = filas.GroupBy(f => f.IdEmpleado).Select(g => new
        {
            IdEmpleado = g.Key,
            HorasTrabajadas = g.Sum(x => x.HorasTrabajadas ?? 0),
            Tardanzas = g.Count(x => x.Estado == "Tardanza"),
            Ausencias = g.Count(x => x.Estado == "Ausente")
        });
        return Ok(resumen);
    }
}
