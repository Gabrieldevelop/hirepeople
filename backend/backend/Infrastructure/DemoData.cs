#nullable enable
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rrhh.Api.Services;
using Rrhh.Domain.Entities;

namespace Rrhh.Infrastructure;

/// Datos ficticios para demostración. Uso: dotnet run -- seed-demo
/// Cédulas y salarios se cifran con la misma clave que usa la API (Crypto:Key).
public static class DemoData
{
    public const string ClaveDemo = "Demo#TalentCore2026";

    private record Emp(string Nombres, string Apellidos, string Depto, string Cargo, int AnioIngreso, string Estado = "Activo");

    public static async Task<string> SembrarAsync(AppDbContext db, CryptoService crypto, PasswordService pwd)
    {
        if (await db.Empleados.AnyAsync())
            return "La base ya tiene empleados. Para volver a sembrar, ejecute de nuevo database/TalentCore.sql.";

        var rnd = new Random(42);
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // ---------- Departamentos y cargos ----------
        var catalogo = new Dictionary<string, (string Cargo, decimal Salario)[]>
        {
            ["Tecnología"] = [("Gerente de TI", 150000), ("Desarrollador .NET Senior", 95000), ("Desarrollador Frontend", 75000), ("Analista QA", 60000)],
            ["Recursos Humanos"] = [("Gerente de RR. HH.", 120000), ("Analista de RR. HH.", 55000)],
            ["Finanzas"] = [("Contador", 70000), ("Analista Financiero", 65000)],
            ["Ventas"] = [("Gerente Comercial", 130000), ("Ejecutivo de Ventas", 50000)],
            ["Operaciones"] = [("Supervisor de Operaciones", 65000), ("Asistente Administrativo", 35000)],
        };
        var deptos = new Dictionary<string, Departamento>();
        var cargos = new Dictionary<string, Cargo>();
        foreach (var (nombre, lista) in catalogo)
        {
            var d = new Departamento { Nombre = nombre, Activo = true };
            deptos[nombre] = d;
            foreach (var (cargo, salario) in lista)
            {
                var c = new Cargo { Nombre = cargo, Departamento = d, SalarioBase = salario };
                cargos[cargo] = c;
                db.Cargos.Add(c);
            }
        }
        await db.SaveChangesAsync();

        // ---------- Empleados ----------
        var plantilla = new List<Emp>
        {
            new("Carlos", "Méndez", "Tecnología", "Gerente de TI", 2018),
            new("Laura", "Peña", "Tecnología", "Desarrollador .NET Senior", 2020),
            new("Miguel", "Santos", "Tecnología", "Desarrollador .NET Senior", 2021),
            new("Andrea", "Castillo", "Tecnología", "Desarrollador Frontend", 2022),
            new("Rafael", "Núñez", "Tecnología", "Analista QA", 2022),
            new("Ana", "Gómez", "Recursos Humanos", "Gerente de RR. HH.", 2017),
            new("Patricia", "Reyes", "Recursos Humanos", "Analista de RR. HH.", 2023),
            new("Luis", "Almonte", "Finanzas", "Contador", 2019),
            new("Daniela", "Herrera", "Finanzas", "Analista Financiero", 2023),
            new("Jorge", "Batista", "Ventas", "Gerente Comercial", 2018),
            new("Sofía", "Mejía", "Ventas", "Ejecutivo de Ventas", 2022),
            new("Pedro", "Valdez", "Ventas", "Ejecutivo de Ventas", 2021),
            new("Camila", "Ortiz", "Ventas", "Ejecutivo de Ventas", 2024),
            new("Ramón", "Féliz", "Operaciones", "Supervisor de Operaciones", 2019),
            new("Yesenia", "Cruz", "Operaciones", "Asistente Administrativo", 2023),
            new("Francisco", "Jiménez", "Operaciones", "Asistente Administrativo", 2024),
            new("Marisol", "Taveras", "Tecnología", "Desarrollador Frontend", 2024),
            new("Héctor", "Rosario", "Tecnología", "Analista QA", 2021, "Vacaciones"),
            new("Elena", "Guzmán", "Finanzas", "Analista Financiero", 2022, "Suspendido"),
            new("Víctor", "Polanco", "Ventas", "Ejecutivo de Ventas", 2020, "Baja"),
        };

        var empleados = new List<Empleado>();
        var contratos = new List<Contrato>();
        for (var i = 0; i < plantilla.Count; i++)
        {
            var p = plantilla[i];
            var ingreso = new DateOnly(p.AnioIngreso, rnd.Next(1, 13), rnd.Next(1, 28));
            var cedula = $"001-{rnd.Next(1000000, 9999999)}-{rnd.Next(0, 10)}";
            var e = new Empleado
            {
                Codigo = $"EMP-{i + 1:000}",
                CedulaCifrada = crypto.Encrypt(cedula),
                Nombres = p.Nombres,
                Apellidos = p.Apellidos,
                FechaNacimiento = new DateOnly(rnd.Next(1975, 2001), rnd.Next(1, 13), rnd.Next(1, 28)),
                Correo = $"{Sin(p.Nombres)}.{Sin(p.Apellidos)}@talentcore.com",
                Departamento = deptos[p.Depto],
                Cargo = cargos[p.Cargo],
                FechaIngreso = ingreso,
                Estado = p.Estado
            };
            empleados.Add(e);

            var salario = cargos[p.Cargo].SalarioBase + rnd.Next(0, 5) * 1000;
            var temporal = p.AnioIngreso >= 2024;
            contratos.Add(new Contrato
            {
                Empleado = e,
                Tipo = temporal ? "Temporal" : "Indefinido",
                FechaInicio = ingreso,
                FechaFin = temporal ? ingreso.AddYears(2) : null,
                SalarioCifrado = crypto.EncryptDecimal(salario),
                Activo = p.Estado != "Baja"
            });
        }
        db.Empleados.AddRange(empleados);
        db.Contratos.AddRange(contratos);
        await db.SaveChangesAsync();

        // Responsable (jefe) de cada departamento
        deptos["Tecnología"].IdResponsable = empleados[0].IdEmpleado;
        deptos["Recursos Humanos"].IdResponsable = empleados[5].IdEmpleado;
        deptos["Finanzas"].IdResponsable = empleados[7].IdEmpleado;
        deptos["Ventas"].IdResponsable = empleados[9].IdEmpleado;
        deptos["Operaciones"].IdResponsable = empleados[13].IdEmpleado;
        await db.SaveChangesAsync();

        // ---------- Usuarios y roles ----------
        async Task<Rol> RolAsync(string nombre, string desc) =>
            await db.Roles.FirstOrDefaultAsync(r => r.Nombre == nombre)
            ?? db.Roles.Add(new Rol { Nombre = nombre, Descripcion = desc }).Entity;

        var rolRrhh = await RolAsync("RRHH", "Recursos Humanos");
        var rolJefe = await RolAsync("Jefe", "Jefe de departamento");
        var rolEmp = await RolAsync("Empleado", "Empleado");
        await db.SaveChangesAsync();

        void Usuario(Empleado emp, params Rol[] roles)
        {
            var u = new Usuario { Correo = emp.Correo, PasswordHash = pwd.Hash(ClaveDemo), Activo = true, Empleado = emp };
            foreach (var r in roles) u.Roles.Add(new UsuarioRol { Rol = r, FechaAsignacion = DateTime.UtcNow });
            db.Usuarios.Add(u);
        }
        Usuario(empleados[5], rolRrhh, rolEmp);   // Ana Gómez: RR. HH.
        Usuario(empleados[0], rolJefe, rolEmp);   // Carlos Méndez: Jefe de Tecnología
        Usuario(empleados[1], rolEmp);            // Laura Peña: Empleado
        await db.SaveChangesAsync();

        // ---------- Ausencias ----------
        var tipos = new[]
        {
            new TipoAusencia { Nombre = "Vacaciones", EsRemunerada = true, RequiereAprobacion = true, DiasMaximos = 20 },
            new TipoAusencia { Nombre = "Permiso personal", EsRemunerada = true, RequiereAprobacion = true, DiasMaximos = 3 },
            new TipoAusencia { Nombre = "Licencia médica", EsRemunerada = true, RequiereAprobacion = true, DiasMaximos = 30 },
            new TipoAusencia { Nombre = "Licencia sin sueldo", EsRemunerada = false, RequiereAprobacion = true, DiasMaximos = 15 },
            new TipoAusencia { Nombre = "Duelo", EsRemunerada = true, RequiereAprobacion = false, DiasMaximos = 5 },
        };
        db.TiposAusencia.AddRange(tipos);
        await db.SaveChangesAsync();

        var activos = empleados.Where(e => e.Estado != "Baja").ToList();
        foreach (var e in activos)
            db.SaldosVacaciones.Add(new SaldoVacaciones { IdEmpleado = e.IdEmpleado, Anio = (short)hoy.Year, DiasAsignados = e.FechaIngreso.Year <= hoy.Year - 5 ? 20 : 14, DiasUsados = 0 });
        await db.SaveChangesAsync();

        var jefe = (Departamento d) => d.IdResponsable;
        void Solicitud(Empleado e, TipoAusencia t, int desdeHoy, int diasNaturales, string estado, string motivo, bool descuentaSaldo = false)
        {
            var ini = hoy.AddDays(desdeHoy);
            var fin = ini.AddDays(diasNaturales - 1);
            var habiles = DiasHabiles(ini, fin);
            if (habiles == 0) return;
            db.SolicitudesAusencia.Add(new SolicitudAusencia
            {
                IdEmpleado = e.IdEmpleado, IdTipo = t.IdTipo, FechaInicio = ini, FechaFin = fin,
                DiasSolicitados = habiles, Estado = estado, Motivo = motivo,
                IdAprobador = estado is "Aprobada" or "Rechazada" ? jefe(e.Departamento) : null,
                FechaSolicitud = DateTime.UtcNow.AddDays(Math.Min(desdeHoy, 0) - 3)
            });
            if (descuentaSaldo)
            {
                var s = db.SaldosVacaciones.Local.First(x => x.IdEmpleado == e.IdEmpleado);
                s.DiasUsados += habiles;
            }
        }
        var vac = tipos[0]; var perm = tipos[1]; var med = tipos[2];
        Solicitud(empleados[1], vac, 14, 7, "Pendiente", "Viaje familiar");
        Solicitud(empleados[2], perm, 3, 1, "Pendiente", "Trámite personal");
        Solicitud(empleados[12], vac, 21, 10, "Pendiente", "Vacaciones de fin de año");
        Solicitud(empleados[16], perm, 5, 2, "Pendiente", "Cita médica de un familiar");
        Solicitud(empleados[3], med, -12, 3, "Aprobada", "Reposo médico");
        Solicitud(empleados[11], vac, -30, 7, "Aprobada", "Descanso", descuentaSaldo: true);
        Solicitud(empleados[17], vac, -2, 10, "Aprobada", "Vacaciones", descuentaSaldo: true);
        Solicitud(empleados[14], perm, -8, 1, "Rechazada", "Asunto personal");
        await db.SaveChangesAsync();

        // ---------- Asistencia (últimos 10 días hábiles) ----------
        var dias = new List<DateOnly>();
        for (var d = hoy; dias.Count < 10; d = d.AddDays(-1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) dias.Add(d);

        foreach (var dia in dias)
            foreach (var e in empleados.Where(x => x.Estado == "Activo"))
            {
                var r = rnd.NextDouble();
                if (dia == hoy && r > 0.8) continue; // aún no marcan hoy
                if (r > 0.96 && dia != hoy)
                {
                    db.RegistrosAsistencia.Add(new RegistroAsistencia { IdEmpleado = e.IdEmpleado, Fecha = dia, Origen = "Manual", Estado = "Ausente" });
                    continue;
                }
                var tarde = r > 0.85;
                var entrada = tarde ? new TimeOnly(8, rnd.Next(20, 59)) : new TimeOnly(7, rnd.Next(40, 59));
                TimeOnly? salida = dia == hoy ? null : new TimeOnly(17, rnd.Next(0, 40));
                db.RegistrosAsistencia.Add(new RegistroAsistencia
                {
                    IdEmpleado = e.IdEmpleado, Fecha = dia, HoraEntrada = entrada, HoraSalida = salida,
                    HorasTrabajadas = salida is null ? null : Math.Round((decimal)(salida.Value - entrada).TotalHours - 1m, 2),
                    Origen = rnd.NextDouble() > 0.8 ? "Kiosco" : "Web",
                    Estado = tarde ? "Tardanza" : "Puntual"
                });
            }
        await db.SaveChangesAsync();

        // ---------- Evaluaciones (semestre anterior) ----------
        var comentarios = new[] { "Excelente desempeño y compromiso con el equipo.", "Cumple las metas con buena calidad.", "Buen trabajo; puede mejorar la comunicación.", "Supera las expectativas del puesto.", "Cumple lo esperado, con oportunidades de crecimiento." };
        foreach (var e in empleados.Where(x => x.Estado is "Activo" or "Vacaciones"))
        {
            var idEvaluador = e.Departamento.IdResponsable!.Value;
            if (idEvaluador == e.IdEmpleado) idEvaluador = empleados[5].IdEmpleado; // los jefes los evalúa RR. HH.
            if (idEvaluador == e.IdEmpleado) idEvaluador = empleados[0].IdEmpleado;
            db.Evaluaciones.Add(new Evaluacion
            {
                IdEmpleado = e.IdEmpleado, IdEvaluador = idEvaluador, Periodo = $"{hoy.Year}-S1",
                Puntaje = Math.Round((decimal)(3.2 + rnd.NextDouble() * 1.8), 2),
                Comentarios = comentarios[rnd.Next(comentarios.Length)], Fecha = new DateOnly(hoy.Year, 7, rnd.Next(5, 20))
            });
        }
        await db.SaveChangesAsync();

        // ---------- Nómina ----------
        var conceptos = new[]
        {
            new ConceptoNomina { Nombre = "Bono de transporte", Tipo = "Ingreso", Porcentaje = 2.00m, EsObligatorio = false },
            new ConceptoNomina { Nombre = "Seguro familiar de salud (SFS)", Tipo = "Descuento", Porcentaje = 3.04m, EsObligatorio = true },
            new ConceptoNomina { Nombre = "Fondo de pensiones (AFP)", Tipo = "Descuento", Porcentaje = 2.87m, EsObligatorio = true },
            new ConceptoNomina { Nombre = "Impuesto sobre la renta (ISR)", Tipo = "Descuento", Porcentaje = 5.00m, EsObligatorio = true },
        };
        db.ConceptosNomina.AddRange(conceptos);
        var mesAnt = hoy.AddMonths(-1);
        var cerrado = new PeriodoNomina { Anio = (short)mesAnt.Year, Mes = (byte)mesAnt.Month, Tipo = "Mensual", FechaPago = new DateOnly(mesAnt.Year, mesAnt.Month, DateTime.DaysInMonth(mesAnt.Year, mesAnt.Month)), Estado = "Cerrado" };
        var abierto = new PeriodoNomina { Anio = (short)hoy.Year, Mes = (byte)hoy.Month, Tipo = "Mensual", FechaPago = new DateOnly(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month)), Estado = "Abierto" };
        db.PeriodosNomina.AddRange(cerrado, abierto);
        await db.SaveChangesAsync();

        // El periodo anterior se deja calculado y cerrado; el actual queda abierto para probar "Calcular".
        foreach (var c in contratos.Where(c => c.Activo))
        {
            var bruto = crypto.DecryptDecimal(c.SalarioCifrado);
            var recibo = new ReciboNomina { IdPeriodo = cerrado.IdPeriodo, IdEmpleado = c.Empleado.IdEmpleado, SalarioBruto = bruto, FechaCalculo = DateTime.UtcNow.AddDays(-hoy.Day) };
            decimal ing = 0, desc = 0;
            foreach (var con in conceptos)
            {
                var monto = Math.Round(bruto * con.Porcentaje / 100, 2);
                recibo.Detalles.Add(new DetalleRecibo { IdConcepto = con.IdConcepto, Monto = monto });
                if (con.Tipo == "Ingreso") ing += monto; else desc += monto;
            }
            recibo.TotalIngresos = ing; recibo.TotalDescuentos = desc; recibo.SalarioNeto = bruto + ing - desc;
            db.RecibosNomina.Add(recibo);
        }
        await db.SaveChangesAsync();

        // ---------- Reclutamiento ----------
        var vNet = new Vacante { Cargo = cargos["Desarrollador .NET Senior"], Titulo = "Desarrollador .NET Senior", Descripcion = "Desarrollo de APIs con ASP.NET Core, EF Core y SQL Server. 5+ años de experiencia.", FechaPublicacion = hoy.AddDays(-20), Estado = "Publicada" };
        var vFront = new Vacante { Cargo = cargos["Desarrollador Frontend"], Titulo = "Desarrollador Frontend React", Descripcion = "Interfaces con React, TypeScript y TanStack Query.", FechaPublicacion = hoy.AddDays(-10), Estado = "Publicada" };
        var vVentas = new Vacante { Cargo = cargos["Ejecutivo de Ventas"], Titulo = "Ejecutivo de Ventas", Descripcion = "Gestión de cartera de clientes corporativos.", FechaPublicacion = hoy.AddDays(-45), Estado = "Cerrada" };
        var vCont = new Vacante { Cargo = cargos["Contador"], Titulo = "Contador General", Descripcion = "Cierre contable mensual y reportes fiscales.", Estado = "Borrador" };
        db.Vacantes.AddRange(vNet, vFront, vVentas, vCont);

        Candidato C(string n, string a, string tel) => new() { Nombres = n, Apellidos = a, Correo = $"{Sin(n)}.{Sin(a)}@correo.com", Telefono = tel, CvRuta = $"/cv/{Sin(n)}-{Sin(a)}.pdf" };
        var cands = new[]
        {
            C("Ana", "Torres", "809-555-0101"), C("Jorge", "Ruiz", "809-555-0102"), C("Marta", "Paz", "809-555-0103"),
            C("Luis", "Gil", "809-555-0104"), C("Sofía", "Mena", "809-555-0105"), C("Diego", "Ríos", "809-555-0106"),
            C("Ivana", "Solís", "809-555-0107"), C("Kevin", "Lora", "809-555-0108"), C("Paola", "Díaz", "809-555-0109"),
            C("Raúl", "Medina", "809-555-0110"), C("Natalia", "Pérez", "809-555-0111"),
        };
        db.Candidatos.AddRange(cands);
        await db.SaveChangesAsync();

        void Post(Vacante v, Candidato c, string etapa, decimal? cal, int hace) =>
            db.Postulaciones.Add(new Postulacion { IdVacante = v.IdVacante, IdCandidato = c.IdCandidato, Etapa = etapa, Calificacion = cal, FechaPostulacion = DateTime.UtcNow.AddDays(-hace) });
        Post(vNet, cands[0], "Postulado", 4.5m, 6); Post(vNet, cands[1], "Postulado", 3.8m, 5); Post(vNet, cands[2], "Postulado", null, 2);
        Post(vNet, cands[3], "Entrevista", 4.2m, 12); Post(vNet, cands[4], "Entrevista", 4.0m, 11);
        Post(vNet, cands[5], "Prueba técnica", 4.3m, 15); Post(vNet, cands[6], "Prueba técnica", null, 14);
        Post(vNet, cands[7], "Contratado", 4.8m, 20); Post(vNet, cands[8], "Descartado", 2.5m, 9);
        Post(vFront, cands[9], "Postulado", null, 3); Post(vFront, cands[10], "Entrevista", 4.1m, 7);
        Post(vVentas, cands[1], "Descartado", 3.0m, 40);
        await db.SaveChangesAsync();

        return $"Datos de demostración creados: {empleados.Count} empleados, {deptos.Count} departamentos, " +
               $"3 usuarios (clave {ClaveDemo}), asistencia de 10 días, ausencias, nómina de {mesAnt:MM/yyyy} y reclutamiento.";
    }

    private static int DiasHabiles(DateOnly ini, DateOnly fin)
    {
        var n = 0;
        for (var d = ini; d <= fin; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) n++;
        return n;
    }

    private static string Sin(string s)
    {
        var norm = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in norm)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().ToLowerInvariant().Replace(" ", "");
    }
}
