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
[Route("api")]
public class AusenciasController(AppDbContext db, AuditService audit) : ApiControllerBase
{
    [HttpGet("tipos-ausencia")]
    public async Task<IActionResult> Tipos() => Ok(await db.TiposAusencia.ToListAsync());

    [HttpGet("empleados/{id:int}/saldo-vacaciones")]
    public async Task<IActionResult> Saldo(int id)
    {
        if (!await PuedeVerAsync(db, id)) return Forbid();
        var anio = (short)DateTime.Today.Year;
        var saldo = await db.SaldosVacaciones.FirstOrDefaultAsync(s => s.IdEmpleado == id && s.Anio == anio);
        var asignados = saldo?.DiasAsignados ?? 0;
        var usados = saldo?.DiasUsados ?? 0;
        return Ok(new { Anio = anio, DiasAsignados = asignados, DiasUsados = usados, DiasDisponibles = asignados - usados });
    }

    [HttpPost("ausencias")]
    public async Task<IActionResult> Solicitar([FromBody] AusenciaRequest r)
    {
        // RRHH puede registrar ausencias de otros empleados; el resto solo las propias.
        int? destino = EmpleadoId;
        if (EsRRHH && r.IdEmpleado is int otro) destino = otro;
        if (destino is not int idEmp) return Forbid();

        if (r.FechaFin < r.FechaInicio)
            return BadRequest(new { mensaje = "La fecha de fin es anterior a la de inicio." });

        var tipo = await db.TiposAusencia.FirstOrDefaultAsync(t => t.IdTipo == r.IdTipo);
        if (tipo is null) return BadRequest(new { mensaje = "Tipo de ausencia inexistente." });

        var dias = DiasHabiles(r.FechaInicio, r.FechaFin);
        if (tipo.DiasMaximos is int maximo && dias > maximo)
            return BadRequest(new { mensaje = $"El tipo permite máximo {maximo} días." });

        var solapa = await db.SolicitudesAusencia.AnyAsync(s =>
            s.IdEmpleado == idEmp && (s.Estado == "Pendiente" || s.Estado == "Aprobada") &&
            s.FechaInicio <= r.FechaFin && s.FechaFin >= r.FechaInicio);
        if (solapa) return Conflict(new { mensaje = "Ya existe una solicitud en esas fechas." });

        if (tipo.Nombre == "Vacaciones" && await SaldoDisponibleAsync(idEmp) < dias)
            return BadRequest(new { mensaje = "Saldo de vacaciones insuficiente." });

        var solicitud = new SolicitudAusencia
        {
            IdEmpleado = idEmp,
            IdTipo = r.IdTipo,
            FechaInicio = r.FechaInicio,
            FechaFin = r.FechaFin,
            DiasSolicitados = dias,
            Estado = "Pendiente",
            Motivo = r.Motivo,
            FechaSolicitud = DateTime.UtcNow
        };
        db.SolicitudesAusencia.Add(solicitud);
        await db.SaveChangesAsync();
        return Created($"/api/ausencias/{solicitud.IdSolicitud}",
            new { solicitud.IdSolicitud, solicitud.Estado, DiasSolicitados = dias });
    }

    [HttpGet("ausencias")]
    public async Task<IActionResult> Listar([FromQuery] string? estado, [FromQuery] int? empleadoId)
    {
        var visibles = EmpleadosVisibles(db).Select(e => e.IdEmpleado);
        var q = db.SolicitudesAusencia.Where(s => visibles.Contains(s.IdEmpleado));
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(s => s.Estado == estado);
        if (empleadoId is int eid) q = q.Where(s => s.IdEmpleado == eid);

        var items = await q.OrderByDescending(s => s.FechaSolicitud).Select(s => new
        {
            s.IdSolicitud,
            s.IdEmpleado,
            Empleado = s.Empleado.Nombres + " " + s.Empleado.Apellidos,
            Tipo = s.Tipo.Nombre,
            s.FechaInicio,
            s.FechaFin,
            s.DiasSolicitados,
            s.Estado,
            s.Motivo
        }).ToListAsync();
        return Ok(items);
    }

    [HttpPatch("ausencias/{id:int}/aprobacion")]
    [Authorize(Roles = "RRHH,Admin,Jefe")]
    public async Task<IActionResult> Resolver(int id, [FromBody] AprobacionRequest r)
    {
        var solicitud = await db.SolicitudesAusencia.Include(s => s.Tipo).FirstOrDefaultAsync(s => s.IdSolicitud == id);
        if (solicitud is null) return NotFound();

        // Nadie aprueba su propia solicitud; el Jefe solo decide sobre su equipo.
        var autorizado = EsRRHH || (EsJefe && await EsJefeDeAsync(db, solicitud.IdEmpleado));
        if (!autorizado || EmpleadoId == solicitud.IdEmpleado) return Forbid();
        if (solicitud.Estado != "Pendiente") return Conflict(new { mensaje = "La solicitud ya fue resuelta." });

        if (r.Decision == "Rechazar")
        {
            if (string.IsNullOrWhiteSpace(r.Comentario))
                return BadRequest(new { mensaje = "El comentario es obligatorio al rechazar." });
            solicitud.Estado = "Rechazada";
        }
        else if (r.Decision == "Aprobar")
        {
            if (solicitud.Tipo.Nombre == "Vacaciones")
            {
                var saldo = await db.SaldosVacaciones.FirstOrDefaultAsync(s =>
                    s.IdEmpleado == solicitud.IdEmpleado && s.Anio == (short)solicitud.FechaInicio.Year);
                if (saldo is null || saldo.DiasAsignados - saldo.DiasUsados < solicitud.DiasSolicitados)
                    return BadRequest(new { mensaje = "Saldo insuficiente para aprobar." });
                saldo.DiasUsados += solicitud.DiasSolicitados;
            }
            solicitud.Estado = "Aprobada";
        }
        else
        {
            return BadRequest(new { mensaje = "Decisión no válida: use Aprobar o Rechazar." });
        }

        solicitud.IdAprobador = EmpleadoId;
        audit.Registrar("SolicitudAusencia", id.ToString(), r.Decision, UsuarioId, Ip, null,
            new { solicitud.Estado, r.Comentario });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("ausencias/{id:int}")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var solicitud = await db.SolicitudesAusencia.FirstOrDefaultAsync(s => s.IdSolicitud == id);
        if (solicitud is null) return NotFound();
        if (solicitud.IdEmpleado != EmpleadoId) return Forbid();
        if (solicitud.Estado != "Pendiente")
            return Conflict(new { mensaje = "Solo se pueden cancelar solicitudes pendientes." });

        solicitud.Estado = "Cancelada";
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<decimal> SaldoDisponibleAsync(int idEmpleado)
    {
        var anio = (short)DateTime.Today.Year;
        var saldo = await db.SaldosVacaciones.FirstOrDefaultAsync(s => s.IdEmpleado == idEmpleado && s.Anio == anio);
        return saldo is null ? 0 : saldo.DiasAsignados - saldo.DiasUsados;
    }

    /// Cuenta días de lunes a viernes. Los feriados aún no están modelados.
    private static decimal DiasHabiles(DateOnly inicio, DateOnly fin)
    {
        decimal total = 0;
        for (var d = inicio; d <= fin; d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) total++;
        return total;
    }
}
