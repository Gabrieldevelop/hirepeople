using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rrhh.Api.Auth;
using Rrhh.Domain.Entities;
using Rrhh.Infrastructure;

namespace Rrhh.Api.Controllers;

/// Reglas de alcance compartidas: RRHH/Admin ven todo; Jefe ve su departamento; Empleado solo lo propio.
public abstract class ApiControllerBase : ControllerBase
{
    protected int UsuarioId => User.UsuarioId();
    protected int? EmpleadoId => User.EmpleadoId();
    protected bool EsRRHH => User.IsInRole("RRHH") || User.IsInRole("Admin");
    protected bool EsJefe => User.IsInRole("Jefe");
    protected string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// El Jefe es el responsable de un departamento que contiene al empleado.
    protected async Task<bool> EsJefeDeAsync(AppDbContext db, int empleadoId)
    {
        var me = EmpleadoId;
        if (me is null) return false;
        return await db.Departamentos.AnyAsync(d => d.IdResponsable == me && d.Empleados.Any(e => e.IdEmpleado == empleadoId));
    }

    protected async Task<bool> PuedeVerAsync(AppDbContext db, int empleadoId) =>
        EsRRHH || EmpleadoId == empleadoId || (EsJefe && await EsJefeDeAsync(db, empleadoId));

    protected IQueryable<Empleado> EmpleadosVisibles(AppDbContext db)
    {
        if (EsRRHH) return db.Empleados;
        var jefe = EsJefe;
        if (EmpleadoId is int me)
            return db.Empleados.Where(e => e.IdEmpleado == me || (jefe && e.Departamento.IdResponsable == me));
        return db.Empleados.Where(e => false);
    }
}
