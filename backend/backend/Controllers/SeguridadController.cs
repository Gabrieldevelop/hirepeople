using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rrhh.Api.Contracts;
using Rrhh.Api.Services;
using Rrhh.Domain.Entities;
using Rrhh.Infrastructure;

namespace Rrhh.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api")]
public class UsuariosController(AppDbContext db, PasswordService pwd, AuditService audit) : ApiControllerBase
{
    [HttpGet("usuarios")]
    public async Task<IActionResult> Listar() =>
        Ok(await db.Usuarios.Include(u => u.Roles).ThenInclude(ur => ur.Rol)
            .Select(u => new
            {
                u.IdUsuario,
                u.Correo,
                u.IdEmpleado,
                u.Activo,
                u.UltimoAcceso,
                Roles = u.Roles.Select(ur => ur.Rol.Nombre)
            }).ToListAsync());

    [HttpPost("usuarios")]
    public async Task<IActionResult> Crear([FromBody] UsuarioRequest r)
    {
        if (r.Password.Length < 10) return BadRequest(new { mensaje = "La contraseña debe tener al menos 10 caracteres." });
        if (await db.Usuarios.AnyAsync(u => u.Correo == r.Correo)) return Conflict(new { mensaje = "El correo ya está registrado." });

        var roles = await db.Roles.Where(x => r.Roles.Contains(x.Nombre)).ToListAsync();
        if (roles.Count != r.Roles.Distinct().Count()) return BadRequest(new { mensaje = "Algún rol no existe." });

        var usuario = new Usuario
        {
            Correo = r.Correo,
            PasswordHash = pwd.Hash(r.Password),
            IdEmpleado = r.IdEmpleado,
            Activo = true
        };
        foreach (var rol in roles)
            usuario.Roles.Add(new UsuarioRol { Rol = rol, AsignadoPor = EmpleadoId, FechaAsignacion = DateTime.UtcNow });
        db.Usuarios.Add(usuario);

        await db.SaveChangesAsync();
        audit.Registrar("Usuario", usuario.IdUsuario.ToString(), "Crear", UsuarioId, Ip, null, new { r.Correo, r.Roles });
        await db.SaveChangesAsync();
        return Created($"/api/usuarios/{usuario.IdUsuario}", new { usuario.IdUsuario });
    }

    [HttpPatch("usuarios/{id:int}/roles")]
    public async Task<IActionResult> CambiarRoles(int id, [FromBody] RolesRequest r)
    {
        var usuario = await db.Usuarios.Include(u => u.Roles).ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.IdUsuario == id);
        if (usuario is null) return NotFound();

        var nuevos = await db.Roles.Where(x => r.Roles.Contains(x.Nombre)).ToListAsync();
        if (nuevos.Count != r.Roles.Distinct().Count()) return BadRequest(new { mensaje = "Algún rol no existe." });

        var antes = usuario.Roles.Select(ur => ur.Rol.Nombre).ToList();
        var idsNuevos = nuevos.Select(x => x.IdRol).ToHashSet();

        // Se quitan solo los roles que ya no aplican y se agregan los nuevos (evita reinsertar la misma llave).
        foreach (var quitar in usuario.Roles.Where(ur => !idsNuevos.Contains(ur.IdRol)).ToList())
            db.UsuariosRoles.Remove(quitar);
        foreach (var rol in nuevos.Where(candidato => usuario.Roles.All(ur => ur.IdRol != candidato.IdRol)))
            usuario.Roles.Add(new UsuarioRol { Rol = rol, AsignadoPor = EmpleadoId, FechaAsignacion = DateTime.UtcNow });

        audit.Registrar("Usuario", id.ToString(), "CambiarRoles", UsuarioId, Ip, new { Roles = antes }, new { Roles = r.Roles });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("roles")]
    public async Task<IActionResult> ListarRoles() => Ok(await db.Roles.ToListAsync());
}

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/auditoria")]
public class AuditoriaController(AppDbContext db) : ApiControllerBase
{
    private const int TamPagina = 50;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? entidad, [FromQuery] int? usuarioId,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int pagina = 1)
    {
        IQueryable<Auditoria> q = db.Auditoria;
        if (!string.IsNullOrWhiteSpace(entidad)) q = q.Where(a => a.Entidad == entidad);
        if (usuarioId is int uid) q = q.Where(a => a.IdUsuario == uid);
        if (desde is DateTime inicio) q = q.Where(a => a.FechaHora >= inicio);
        if (hasta is DateTime fin) q = q.Where(a => a.FechaHora <= fin);

        pagina = Math.Max(1, pagina);
        var items = await q.OrderByDescending(a => a.FechaHora)
            .Skip((pagina - 1) * TamPagina).Take(TamPagina)
            .Select(a => new { a.IdAuditoria, a.IdUsuario, a.Entidad, a.IdRegistro, a.Accion, a.FechaHora, a.Ip, a.ValoresAntes, a.ValoresDespues })
            .ToListAsync();
        return Ok(new { pagina, items });
    }
}
