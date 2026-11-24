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
public class ReclutamientoController(AppDbContext db, AuditService audit) : ApiControllerBase
{
    private static readonly string[] EstadosVacante = ["Borrador", "Publicada", "Cerrada"];
    private static readonly string[] Etapas = ["Postulado", "Entrevista", "Prueba técnica", "Contratado", "Descartado"];

    /// Público: solo muestra vacantes publicadas. RRHH ve todas y puede filtrar por estado.
    [HttpGet("vacantes")]
    [AllowAnonymous]
    public async Task<IActionResult> Vacantes([FromQuery] string? estado)
    {
        IQueryable<Vacante> q = db.Vacantes;
        var gestor = User.Identity?.IsAuthenticated == true && EsRRHH;
        if (!gestor)
        {
            q = q.Where(v => v.Estado == "Publicada");
        }
        else if (!string.IsNullOrWhiteSpace(estado))
        {
            q = q.Where(v => v.Estado == estado);
        }

        var items = await q.OrderByDescending(v => v.FechaPublicacion).Select(v => new
        {
            v.IdVacante,
            v.Titulo,
            Cargo = v.Cargo.Nombre,
            v.Descripcion,
            v.FechaPublicacion,
            v.Estado
        }).ToListAsync();
        return Ok(items);
    }

    [HttpPost("vacantes")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> CrearVacante([FromBody] VacanteRequest r)
    {
        if (!await db.Cargos.AnyAsync(c => c.IdCargo == r.IdCargo))
            return BadRequest(new { mensaje = "El cargo no existe." });
        var nueva = new Vacante { IdCargo = r.IdCargo, Titulo = r.Titulo, Descripcion = r.Descripcion, Estado = "Borrador" };
        db.Vacantes.Add(nueva);
        await db.SaveChangesAsync();
        return Created($"/api/vacantes/{nueva.IdVacante}", new { nueva.IdVacante });
    }

    [HttpPatch("vacantes/{id:int}/estado")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> CambiarEstadoVacante(int id, [FromBody] EstadoVacanteRequest r)
    {
        if (!EstadosVacante.Contains(r.Estado)) return BadRequest(new { mensaje = "Estado no válido." });
        var vacante = await db.Vacantes.FirstOrDefaultAsync(v => v.IdVacante == id);
        if (vacante is null) return NotFound();

        vacante.Estado = r.Estado;
        if (r.Estado == "Publicada") vacante.FechaPublicacion ??= DateOnly.FromDateTime(DateTime.Today);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// Público: postulación a una vacante publicada. El CV se recibe como ruta (la carga de archivos queda pendiente).
    [HttpPost("vacantes/{id:int}/postulaciones")]
    [AllowAnonymous]
    public async Task<IActionResult> Postular(int id, [FromBody] PostulacionRequest r)
    {
        var vacante = await db.Vacantes.FirstOrDefaultAsync(v => v.IdVacante == id && v.Estado == "Publicada");
        if (vacante is null) return NotFound(new { mensaje = "Vacante no disponible." });

        var candidato = await db.Candidatos.FirstOrDefaultAsync(c => c.Correo == r.Correo);
        if (candidato is null)
        {
            candidato = new Candidato { Nombres = r.Nombres, Apellidos = r.Apellidos, Correo = r.Correo, Telefono = r.Telefono, CvRuta = r.CvRuta };
            db.Candidatos.Add(candidato);
            await db.SaveChangesAsync();
        }

        if (await db.Postulaciones.AnyAsync(p => p.IdVacante == id && p.IdCandidato == candidato.IdCandidato))
            return Conflict(new { mensaje = "Ya existe una postulación de este correo a la vacante." });

        var postulacion = new Postulacion
        {
            IdVacante = id,
            IdCandidato = candidato.IdCandidato,
            FechaPostulacion = DateTime.UtcNow,
            Etapa = "Postulado"
        };
        db.Postulaciones.Add(postulacion);
        await db.SaveChangesAsync();
        return Created($"/api/postulaciones/{postulacion.IdPostulacion}", new { postulacion.IdPostulacion });
    }

    [HttpGet("vacantes/{id:int}/postulaciones")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> PostulacionesDeVacante(int id)
    {
        var lista = await db.Postulaciones.Where(p => p.IdVacante == id).Select(p => new
        {
            p.IdPostulacion,
            Candidato = p.Candidato.Nombres + " " + p.Candidato.Apellidos,
            p.Candidato.Correo,
            p.Etapa,
            p.Calificacion,
            p.FechaPostulacion
        }).ToListAsync();

        // Agrupadas por etapa para alimentar el tablero.
        return Ok(lista.GroupBy(x => x.Etapa).Select(g => new { Etapa = g.Key, Items = g.ToList() }));
    }

    [HttpPatch("postulaciones/{id:int}/etapa")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> CambiarEtapa(int id, [FromBody] EtapaRequest r)
    {
        if (!Etapas.Contains(r.Etapa)) return BadRequest(new { mensaje = "Etapa no válida.", validas = Etapas });
        var postulacion = await db.Postulaciones.FirstOrDefaultAsync(p => p.IdPostulacion == id);
        if (postulacion is null) return NotFound();

        var anterior = postulacion.Etapa;
        postulacion.Etapa = r.Etapa;
        if (r.Calificacion is decimal nota) postulacion.Calificacion = nota;

        audit.Registrar("Postulacion", id.ToString(), "CambiarEtapa", UsuarioId, Ip,
            new { Etapa = anterior }, new { Etapa = r.Etapa });
        await db.SaveChangesAsync();
        return NoContent();
    }
}
