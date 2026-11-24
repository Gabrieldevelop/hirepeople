using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rrhh.Infrastructure;

namespace Rrhh.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reportes")]
public class ReportesController(AppDbContext db) : ApiControllerBase
{
    [HttpGet("headcount")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> Headcount() =>
        Ok(await db.Empleados.Where(e => e.Estado == "Activo")
            .GroupBy(e => e.Departamento.Nombre)
            .Select(g => new { Departamento = g.Key, Total = g.Count() })
            .ToListAsync());

    /// Solo formato JSON. xlsx y pdf quedan pendientes.
    [HttpGet("asistencia")]
    [Authorize(Roles = "RRHH,Admin,Jefe")]
    public async Task<IActionResult> Asistencia([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta,
        [FromQuery] string formato = "json")
    {
        if (formato != "json") return StatusCode(501, new { mensaje = $"Exportación a {formato} pendiente." });
        var visibles = EmpleadosVisibles(db).Select(e => e.IdEmpleado);
        var datos = await db.RegistrosAsistencia
            .Where(r => r.Fecha >= desde && r.Fecha <= hasta && visibles.Contains(r.IdEmpleado))
            .GroupBy(r => r.Estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToListAsync();
        return Ok(datos);
    }

    /// Solo formato JSON. xlsx y pdf quedan pendientes.
    [HttpGet("nomina")]
    [Authorize(Roles = "RRHH,Admin")]
    public async Task<IActionResult> Nomina([FromQuery] int periodoId, [FromQuery] string formato = "json")
    {
        if (formato != "json") return StatusCode(501, new { mensaje = $"Exportación a {formato} pendiente." });
        var resumen = await db.RecibosNomina.Where(r => r.IdPeriodo == periodoId)
            .GroupBy(r => r.IdPeriodo)
            .Select(g => new
            {
                PeriodoId = g.Key,
                Empleados = g.Count(),
                TotalBruto = g.Sum(r => r.SalarioBruto),
                TotalDescuentos = g.Sum(r => r.TotalDescuentos),
                TotalNeto = g.Sum(r => r.SalarioNeto)
            })
            .FirstOrDefaultAsync();
        return Ok(resumen ?? (object)new { PeriodoId = periodoId, Empleados = 0, TotalBruto = 0m, TotalDescuentos = 0m, TotalNeto = 0m });
    }
}
