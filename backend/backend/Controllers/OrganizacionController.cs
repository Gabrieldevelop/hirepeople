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
[Route("api/departamentos")]
public class DepartamentosController(AppDbContext db, AuditService audit) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await db.Departamentos.Select(d => new { d.IdDepartamento, d.Nombre, d.IdResponsable, d.Activo }).ToListAsync());

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Crear([FromBody] DepartamentoRequest r)
    {
        if (r.IdResponsable is int resp && !await db.Empleados.AnyAsync(e => e.IdEmpleado == resp))
            return BadRequest(new { mensaje = "El responsable no existe." });
        var nuevo = new Departamento { Nombre = r.Nombre, IdResponsable = r.IdResponsable, Activo = r.Activo };
        db.Departamentos.Add(nuevo);
        await db.SaveChangesAsync();
        audit.Registrar("Departamento", nuevo.IdDepartamento.ToString(), "Crear", UsuarioId, Ip, null, new { r.Nombre });
        await db.SaveChangesAsync();
        return Created($"/api/departamentos/{nuevo.IdDepartamento}", new { nuevo.IdDepartamento });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] DepartamentoRequest r)
    {
        var dep = await db.Departamentos.FirstOrDefaultAsync(d => d.IdDepartamento == id);
        if (dep is null) return NotFound();
        if (r.IdResponsable is int resp && !await db.Empleados.AnyAsync(e => e.IdEmpleado == resp))
            return BadRequest(new { mensaje = "El responsable no existe." });
        var antes = new { dep.Nombre, dep.IdResponsable, dep.Activo };
        dep.Nombre = r.Nombre;
        dep.IdResponsable = r.IdResponsable;
        dep.Activo = r.Activo;
        audit.Registrar("Departamento", id.ToString(), "Actualizar", UsuarioId, Ip, antes,
            new { dep.Nombre, dep.IdResponsable, dep.Activo });
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/cargos")]
public class CargosController(AppDbContext db, AuditService audit) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await db.Cargos.Select(c => new { c.IdCargo, c.Nombre, c.IdDepartamento, Departamento = c.Departamento.Nombre })
            .ToListAsync());

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Crear([FromBody] CargoRequest r)
    {
        if (!await db.Departamentos.AnyAsync(d => d.IdDepartamento == r.IdDepartamento))
            return BadRequest(new { mensaje = "El departamento no existe." });
        var nuevo = new Cargo { Nombre = r.Nombre, IdDepartamento = r.IdDepartamento, SalarioBase = r.SalarioBase };
        db.Cargos.Add(nuevo);
        await db.SaveChangesAsync();
        audit.Registrar("Cargo", nuevo.IdCargo.ToString(), "Crear", UsuarioId, Ip, null, new { r.Nombre, r.IdDepartamento });
        await db.SaveChangesAsync();
        return Created($"/api/cargos/{nuevo.IdCargo}", new { nuevo.IdCargo });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] CargoRequest r)
    {
        var cargo = await db.Cargos.FirstOrDefaultAsync(c => c.IdCargo == id);
        if (cargo is null) return NotFound();
        var antes = new { cargo.Nombre, cargo.IdDepartamento, cargo.SalarioBase };
        cargo.Nombre = r.Nombre;
        cargo.IdDepartamento = r.IdDepartamento;
        cargo.SalarioBase = r.SalarioBase;
        audit.Registrar("Cargo", id.ToString(), "Actualizar", UsuarioId, Ip, antes,
            new { cargo.Nombre, cargo.IdDepartamento, cargo.SalarioBase });
        await db.SaveChangesAsync();
        return NoContent();
    }
}
