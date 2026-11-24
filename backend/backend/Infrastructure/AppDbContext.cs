#nullable enable
using Microsoft.EntityFrameworkCore;
using Rrhh.Domain.Entities;

namespace Rrhh.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<Evaluacion> Evaluaciones => Set<Evaluacion>();
    public DbSet<RegistroAsistencia> RegistrosAsistencia => Set<RegistroAsistencia>();
    public DbSet<TipoAusencia> TiposAusencia => Set<TipoAusencia>();
    public DbSet<SolicitudAusencia> SolicitudesAusencia => Set<SolicitudAusencia>();
    public DbSet<SaldoVacaciones> SaldosVacaciones => Set<SaldoVacaciones>();
    public DbSet<PeriodoNomina> PeriodosNomina => Set<PeriodoNomina>();
    public DbSet<ConceptoNomina> ConceptosNomina => Set<ConceptoNomina>();
    public DbSet<ReciboNomina> RecibosNomina => Set<ReciboNomina>();
    public DbSet<DetalleRecibo> DetallesRecibo => Set<DetalleRecibo>();
    public DbSet<Vacante> Vacantes => Set<Vacante>();
    public DbSet<Candidato> Candidatos => Set<Candidato>();
    public DbSet<Postulacion> Postulaciones => Set<Postulacion>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<UsuarioRol> UsuariosRoles => Set<UsuarioRol>();
    public DbSet<Auditoria> Auditoria => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Departamento>(e =>
        {
            e.ToTable("Departamentos");
            e.HasKey(x => x.IdDepartamento);
            e.Property(x => x.Nombre).HasMaxLength(100);
            e.HasOne(x => x.Responsable).WithMany().HasForeignKey(x => x.IdResponsable).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Cargo>(e =>
        {
            e.ToTable("Cargos");
            e.HasKey(x => x.IdCargo);
            e.Property(x => x.Nombre).HasMaxLength(100);
            e.Property(x => x.SalarioBase).HasPrecision(12, 2);
            e.HasOne(x => x.Departamento).WithMany(d => d.Cargos).HasForeignKey(x => x.IdDepartamento);
        });

        b.Entity<Empleado>(e =>
        {
            e.ToTable("Empleados");
            e.HasKey(x => x.IdEmpleado);
            e.Property(x => x.Codigo).HasMaxLength(20);
            e.Property(x => x.CedulaCifrada).HasMaxLength(256);
            e.Property(x => x.Nombres).HasMaxLength(100);
            e.Property(x => x.Apellidos).HasMaxLength(100);
            e.Property(x => x.Correo).HasMaxLength(150);
            e.Property(x => x.Estado).HasMaxLength(20);
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasOne(x => x.Departamento).WithMany(d => d.Empleados).HasForeignKey(x => x.IdDepartamento);
            e.HasOne(x => x.Cargo).WithMany(c => c.Empleados).HasForeignKey(x => x.IdCargo);
        });

        b.Entity<Contrato>(e =>
        {
            e.ToTable("Contratos");
            e.HasKey(x => x.IdContrato);
            e.Property(x => x.SalarioCifrado).HasMaxLength(256);
            e.HasOne(x => x.Empleado).WithMany(em => em.Contratos).HasForeignKey(x => x.IdEmpleado);
        });

        b.Entity<Evaluacion>(e =>
        {
            e.ToTable("Evaluaciones");
            e.HasKey(x => x.IdEvaluacion);
            e.Property(x => x.Puntaje).HasPrecision(4, 2);
            e.HasOne(x => x.Empleado).WithMany(em => em.EvaluacionesRecibidas).HasForeignKey(x => x.IdEmpleado);
            e.HasOne(x => x.Evaluador).WithMany().HasForeignKey(x => x.IdEvaluador).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RegistroAsistencia>(e =>
        {
            e.ToTable("RegistroAsistencia");
            e.HasKey(x => x.IdRegistro);
            e.Property(x => x.HorasTrabajadas).HasPrecision(5, 2);
            e.Property(x => x.Origen).HasMaxLength(20);
            e.Property(x => x.Estado).HasMaxLength(20);
            e.HasIndex(x => new { x.IdEmpleado, x.Fecha });
            e.HasOne(x => x.Empleado).WithMany().HasForeignKey(x => x.IdEmpleado);
        });

        b.Entity<TipoAusencia>(e =>
        {
            e.ToTable("TiposAusencia");
            e.HasKey(x => x.IdTipo);
            e.Property(x => x.Nombre).HasMaxLength(60);
        });

        b.Entity<SolicitudAusencia>(e =>
        {
            e.ToTable("SolicitudesAusencia");
            e.HasKey(x => x.IdSolicitud);
            e.Property(x => x.DiasSolicitados).HasPrecision(5, 1);
            e.Property(x => x.Estado).HasMaxLength(20);
            e.Property(x => x.Motivo).HasMaxLength(500);
            e.HasOne(x => x.Empleado).WithMany().HasForeignKey(x => x.IdEmpleado);
            e.HasOne(x => x.Tipo).WithMany().HasForeignKey(x => x.IdTipo);
        });

        b.Entity<SaldoVacaciones>(e =>
        {
            e.ToTable("SaldoVacaciones");
            e.HasKey(x => x.IdSaldo);
            e.Property(x => x.DiasAsignados).HasPrecision(5, 1);
            e.Property(x => x.DiasUsados).HasPrecision(5, 1);
            e.HasIndex(x => new { x.IdEmpleado, x.Anio }).IsUnique();
            e.HasOne(x => x.Empleado).WithMany().HasForeignKey(x => x.IdEmpleado);
        });

        b.Entity<PeriodoNomina>(e =>
        {
            e.ToTable("PeriodosNomina");
            e.HasKey(x => x.IdPeriodo);
            e.HasIndex(x => new { x.Anio, x.Mes, x.Tipo }).IsUnique();
        });

        b.Entity<ConceptoNomina>(e =>
        {
            e.ToTable("ConceptosNomina");
            e.HasKey(x => x.IdConcepto);
            e.Property(x => x.Porcentaje).HasPrecision(5, 2);
        });

        b.Entity<ReciboNomina>(e =>
        {
            e.ToTable("RecibosNomina");
            e.HasKey(x => x.IdRecibo);
            e.Property(x => x.SalarioBruto).HasPrecision(12, 2);
            e.Property(x => x.TotalIngresos).HasPrecision(12, 2);
            e.Property(x => x.TotalDescuentos).HasPrecision(12, 2);
            e.Property(x => x.SalarioNeto).HasPrecision(12, 2);
            e.HasIndex(x => new { x.IdPeriodo, x.IdEmpleado }).IsUnique();
            e.HasOne(x => x.Periodo).WithMany().HasForeignKey(x => x.IdPeriodo);
            e.HasOne(x => x.Empleado).WithMany().HasForeignKey(x => x.IdEmpleado);
        });

        b.Entity<DetalleRecibo>(e =>
        {
            e.ToTable("DetalleRecibo");
            e.HasKey(x => x.IdDetalle);
            e.Property(x => x.Monto).HasPrecision(12, 2);
            e.HasOne(x => x.Recibo).WithMany(r => r.Detalles).HasForeignKey(x => x.IdRecibo);
            e.HasOne(x => x.Concepto).WithMany().HasForeignKey(x => x.IdConcepto);
        });

        b.Entity<Vacante>(e =>
        {
            e.ToTable("Vacantes");
            e.HasKey(x => x.IdVacante);
            e.Property(x => x.Titulo).HasMaxLength(150);
            e.HasOne(x => x.Cargo).WithMany().HasForeignKey(x => x.IdCargo);
        });

        b.Entity<Candidato>(e =>
        {
            e.ToTable("Candidatos");
            e.HasKey(x => x.IdCandidato);
            e.Property(x => x.Correo).HasMaxLength(150);
            e.Property(x => x.CvRuta).HasMaxLength(300);
        });

        b.Entity<Postulacion>(e =>
        {
            e.ToTable("Postulaciones");
            e.HasKey(x => x.IdPostulacion);
            e.Property(x => x.Etapa).HasMaxLength(30);
            e.Property(x => x.Calificacion).HasPrecision(4, 2);
            e.HasOne(x => x.Vacante).WithMany(v => v.Postulaciones).HasForeignKey(x => x.IdVacante);
            e.HasOne(x => x.Candidato).WithMany(c => c.Postulaciones).HasForeignKey(x => x.IdCandidato);
        });

        b.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.HasKey(x => x.IdUsuario);
            e.Property(x => x.Correo).HasMaxLength(150);
            e.HasIndex(x => x.Correo).IsUnique();
            e.HasOne(x => x.Empleado).WithOne(em => em.Usuario).HasForeignKey<Usuario>(x => x.IdEmpleado).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Rol>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.IdRol);
            e.Property(x => x.Nombre).HasMaxLength(30);
            e.HasIndex(x => x.Nombre).IsUnique();
        });

        b.Entity<UsuarioRol>(e =>
        {
            e.ToTable("UsuarioRoles");
            e.HasKey(x => new { x.IdUsuario, x.IdRol });
            e.HasOne(x => x.Usuario).WithMany(u => u.Roles).HasForeignKey(x => x.IdUsuario);
            e.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.IdRol);
        });

        b.Entity<Auditoria>(e =>
        {
            e.ToTable("Auditoria");
            e.HasKey(x => x.IdAuditoria);
            e.Property(x => x.Entidad).HasMaxLength(60);
            e.Property(x => x.IdRegistro).HasMaxLength(50);
            e.Property(x => x.Accion).HasMaxLength(20);
            e.Property(x => x.Ip).HasMaxLength(45);
            e.HasIndex(x => new { x.Entidad, x.IdRegistro });
        });

        // La base TalentCore usa columnas en snake_case (id_departamento, fecha_ingreso...),
        // mientras que las entidades usan PascalCase. Se mapean aquí automáticamente.
        foreach (var entidad in b.Model.GetEntityTypes())
            foreach (var propiedad in entidad.GetProperties())
                propiedad.SetColumnName(ASnakeCase(propiedad.Name));
    }

    private static string ASnakeCase(string nombre) =>
        System.Text.RegularExpressions.Regex.Replace(nombre, "(?<=[a-z0-9])(?=[A-Z])", "_").ToLowerInvariant();
}
