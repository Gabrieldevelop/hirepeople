using System.Text.Json;
using Rrhh.Domain.Entities;
using Rrhh.Infrastructure;

namespace Rrhh.Api.Services;

/// Agrega un registro a la tabla Auditoria. No guarda cambios: el controlador llama SaveChanges.
public class AuditService(AppDbContext db)
{
    public void Registrar(string entidad, string idRegistro, string accion, int? idUsuario, string? ip,
        object? antes, object? despues)
    {
        db.Auditoria.Add(new Auditoria
        {
            Entidad = entidad,
            IdRegistro = idRegistro,
            Accion = accion,
            IdUsuario = idUsuario,
            FechaHora = DateTime.UtcNow,
            Ip = ip,
            ValoresAntes = antes is null ? null : JsonSerializer.Serialize(antes),
            ValoresDespues = despues is null ? null : JsonSerializer.Serialize(despues)
        });
    }
}
