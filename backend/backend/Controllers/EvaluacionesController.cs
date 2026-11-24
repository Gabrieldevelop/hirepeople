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
public class EvaluacionesController(AppDbContext db, AuditService audit) : ApiControllerBase
{
    [HttpGet("evaluaciones")]
    [Authorize(Roles = "RRHH,Admin,Jefe")]
    public async Task<IActionResult> Listar([FromQuery] int? empleadoId, [FromQuery] string? periodo)
    {
        var visibles = EmpleadosVisibles(db).Select(e => e.IdEmpleado);
        var q = db.Evaluaciones.Where(x => visibles.Contains(x.IdEmpleado));
        if (empleadoId is int eid) q = q.Where(x => x.IdEmpleado == eid);
        if (!string.IsNullOrWhiteSpace(periodo)) q = q.Where(x => x.Periodo == periodo);

        return Ok(await q.OrderByDescending(x => x.Fecha).Select(x => new
        {
            x.IdEvaluacion, x.IdEmpleado, x.IdEvaluador, x.Periodo, x.Puntaje, x.Comentarios, x.Fecha
        }).ToListAsync());
    }

    /// Solo el Jefe directo (responsable del departamento del empleado) puede evaluar.
    [HttpPost("evaluaciones")]
    [Authorize(Roles = "Jefe")]
    public async Task<IActionResult> Crear([FromBody] EvaluacionRequest r)
    {
        if (EmpleadoId is not int evaluador) return Forbid();
        if (!await EsJefeDeAsync(db, r.IdEmpleado)) return Forbid();
        if (r.Puntaje < 0 || r.Puntaje > 5) return BadRequest(new { mensaje = "El puntaje debe estar entre 0 y 5." });

        var evaluacion = new Evaluacion
        {
            IdEmpleado = r.IdEmpleado,
            IdEvaluador = evaluador,
            Periodo = r.Periodo,
            Puntaje = r.Puntaje,
            Comentarios = r.Comentarios,
            Fecha = r.Fecha
        };
        db.Evaluaciones.Add(evaluacion);
        await db.SaveChangesAsync();
        return Created($"/api/evaluaciones/{evaluacion.IdEvaluacion}", new { evaluacion.IdEvaluacion });
    }

    [HttpGet("empleados/{id:int}/evaluaciones")]
    public async Task<IActionResult> Historial(int id)
    {
        if (!await PuedeVerAsync(db, id)) return Forbid();
        return Ok(await db.Evaluaciones.Where(x => x.IdEmpleado == id).OrderByDescending(x => x.Fecha)
            .Select(x => new { x.IdEvaluacion, x.Periodo, x.Puntaje, x.Comentarios, x.Fecha, x.IdEvaluador })
            .ToListAsync());
    }
}
