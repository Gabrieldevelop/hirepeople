using System.Security.Claims;

namespace Rrhh.Api.Auth;

public static class CurrentUserExtensions
{
    public static int UsuarioId(this ClaimsPrincipal u) =>
        int.Parse(u.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    public static int? EmpleadoId(this ClaimsPrincipal u)
    {
        var v = u.FindFirst("idEmpleado")?.Value;
        return v is null ? null : int.Parse(v);
    }
}
