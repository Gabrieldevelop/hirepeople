using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rrhh.Api.Contracts;
using Rrhh.Api.Services;
using Rrhh.Infrastructure;

namespace Rrhh.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, PasswordService pwd, TokenService tokens) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var usuario = await db.Usuarios.Include(x => x.Roles).ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(x => x.Correo == req.Correo && x.Activo);
        if (usuario is null || !pwd.Verify(req.Password, usuario.PasswordHash))
            return Unauthorized(new { mensaje = "Credenciales inválidas." });

        usuario.UltimoAcceso = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(tokens.Emitir(usuario.IdUsuario, usuario.IdEmpleado, usuario.Roles.Select(ur => ur.Rol.Nombre)));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var principal = tokens.ValidarRefresh(req.RefreshToken);
        if (principal is null) return Unauthorized();
        var id = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var usuario = await db.Usuarios.Include(x => x.Roles).ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(x => x.IdUsuario == id && x.Activo);
        if (usuario is null) return Unauthorized();
        return Ok(tokens.Emitir(usuario.IdUsuario, usuario.IdEmpleado, usuario.Roles.Select(ur => ur.Rol.Nombre)));
    }

    /// Los tokens son stateless: el logout indica al cliente que descarte sus tokens.
    /// Revocar un refresh token exige una tabla de revocación, que no está en el modelo.
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout() => NoContent();

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var usuario = await db.Usuarios.Include(x => x.Roles).ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(x => x.IdUsuario == UsuarioId);
        if (usuario is null) return NotFound();
        return Ok(new
        {
            usuario.IdUsuario,
            usuario.IdEmpleado,
            usuario.Correo,
            Roles = usuario.Roles.Select(ur => ur.Rol.Nombre)
        });
    }
}
