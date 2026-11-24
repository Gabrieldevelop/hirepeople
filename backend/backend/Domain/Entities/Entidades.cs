// Entidades del dominio generadas a partir del modelo de datos de la maqueta (Grupo 6).
// Campos marcados con [Cifrado] se protegen a nivel de columna (ver AppDbContext).
#nullable enable
namespace Rrhh.Domain.Entities;

public class Departamento
{
    public int IdDepartamento { get; set; }
    public string Nombre { get; set; } = default!;
    public int? IdResponsable { get; set; }
    public bool Activo { get; set; } = true;

    public Empleado? Responsable { get; set; }
    public ICollection<Cargo> Cargos { get; set; } = new List<Cargo>();
    public ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
}

public class Cargo
{
    public int IdCargo { get; set; }
    public string Nombre { get; set; } = default!;
    public int IdDepartamento { get; set; }
    /// [Cifrado]
    public decimal SalarioBase { get; set; }

    public Departamento Departamento { get; set; } = default!;
    public ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
}

public class Empleado
{
    public int IdEmpleado { get; set; }
    public string Codigo { get; set; } = default!;
    /// [Cifrado] Cédula almacenada cifrada (AES-256).
    public byte[] CedulaCifrada { get; set; } = default!;
    public string Nombres { get; set; } = default!;
    public string Apellidos { get; set; } = default!;
    public DateOnly FechaNacimiento { get; set; }
    public string Correo { get; set; } = default!;
    public int IdDepartamento { get; set; }
    public int IdCargo { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public string Estado { get; set; } = "Activo";

    public Departamento Departamento { get; set; } = default!;
    public Cargo Cargo { get; set; } = default!;
    public ICollection<Contrato> Contratos { get; set; } = new List<Contrato>();
    public ICollection<Evaluacion> EvaluacionesRecibidas { get; set; } = new List<Evaluacion>();
    public Usuario? Usuario { get; set; }
}

public class Contrato
{
    public int IdContrato { get; set; }
    public int IdEmpleado { get; set; }
    public string Tipo { get; set; } = default!;
    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    /// [Cifrado]
    public byte[] SalarioCifrado { get; set; } = default!;
    public bool Activo { get; set; } = true;

    public Empleado Empleado { get; set; } = default!;
}

public class Evaluacion
{
    public int IdEvaluacion { get; set; }
    public int IdEmpleado { get; set; }
    public int IdEvaluador { get; set; }
    public string Periodo { get; set; } = default!;
    public decimal Puntaje { get; set; }
    public string? Comentarios { get; set; }
    public DateOnly Fecha { get; set; }

    public Empleado Empleado { get; set; } = default!;
    public Empleado Evaluador { get; set; } = default!;
}

public class RegistroAsistencia
{
    public long IdRegistro { get; set; }
    public int IdEmpleado { get; set; }
    public DateOnly Fecha { get; set; }
    public TimeOnly? HoraEntrada { get; set; }
    public TimeOnly? HoraSalida { get; set; }
    public decimal? HorasTrabajadas { get; set; }
    public string Origen { get; set; } = "Web";
    public string Estado { get; set; } = default!;
    /// Campo añadido: el endpoint PUT /api/asistencia/{id} exige justificación al corregir una marca.
    public string? Justificacion { get; set; }

    public Empleado Empleado { get; set; } = default!;
}

public class TipoAusencia
{
    public int IdTipo { get; set; }
    public string Nombre { get; set; } = default!;
    public bool EsRemunerada { get; set; }
    public bool RequiereAprobacion { get; set; } = true;
    public int? DiasMaximos { get; set; }
}

public class SolicitudAusencia
{
    public int IdSolicitud { get; set; }
    public int IdEmpleado { get; set; }
    public int IdTipo { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public decimal DiasSolicitados { get; set; }
    public string Estado { get; set; } = "Pendiente";
    public int? IdAprobador { get; set; }
    public string? Motivo { get; set; }
    public DateTime FechaSolicitud { get; set; }

    public Empleado Empleado { get; set; } = default!;
    public TipoAusencia Tipo { get; set; } = default!;
}

public class SaldoVacaciones
{
    public int IdSaldo { get; set; }
    public int IdEmpleado { get; set; }
    public short Anio { get; set; }
    public decimal DiasAsignados { get; set; }
    public decimal DiasUsados { get; set; }

    public Empleado Empleado { get; set; } = default!;
}

public class PeriodoNomina
{
    public int IdPeriodo { get; set; }
    public short Anio { get; set; }
    public byte Mes { get; set; }
    public string Tipo { get; set; } = "Mensual"; // Mensual | Quincenal
    public DateOnly FechaPago { get; set; }
    public string Estado { get; set; } = "Abierto"; // Abierto | En cálculo | Cerrado
}

public class ConceptoNomina
{
    public int IdConcepto { get; set; }
    public string Nombre { get; set; } = default!;
    public string Tipo { get; set; } = default!; // Ingreso | Descuento
    public decimal Porcentaje { get; set; }
    public bool EsObligatorio { get; set; }
}

public class ReciboNomina
{
    public long IdRecibo { get; set; }
    public int IdPeriodo { get; set; }
    public int IdEmpleado { get; set; }
    public decimal SalarioBruto { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalDescuentos { get; set; }
    public decimal SalarioNeto { get; set; }
    public DateTime FechaCalculo { get; set; }

    public PeriodoNomina Periodo { get; set; } = default!;
    public Empleado Empleado { get; set; } = default!;
    public ICollection<DetalleRecibo> Detalles { get; set; } = new List<DetalleRecibo>();
}

public class DetalleRecibo
{
    public long IdDetalle { get; set; }
    public long IdRecibo { get; set; }
    public int IdConcepto { get; set; }
    public decimal Monto { get; set; }

    public ReciboNomina Recibo { get; set; } = default!;
    public ConceptoNomina Concepto { get; set; } = default!;
}

public class Vacante
{
    public int IdVacante { get; set; }
    public int IdCargo { get; set; }
    public string Titulo { get; set; } = default!;
    public string Descripcion { get; set; } = default!;
    public DateOnly? FechaPublicacion { get; set; }
    public string Estado { get; set; } = "Borrador"; // Borrador | Publicada | Cerrada

    public Cargo Cargo { get; set; } = default!;
    public ICollection<Postulacion> Postulaciones { get; set; } = new List<Postulacion>();
}

public class Candidato
{
    public int IdCandidato { get; set; }
    public string Nombres { get; set; } = default!;
    public string Apellidos { get; set; } = default!;
    public string Correo { get; set; } = default!;
    public string? Telefono { get; set; }
    public string? CvRuta { get; set; }

    public ICollection<Postulacion> Postulaciones { get; set; } = new List<Postulacion>();
}

public class Postulacion
{
    public int IdPostulacion { get; set; }
    public int IdVacante { get; set; }
    public int IdCandidato { get; set; }
    public DateTime FechaPostulacion { get; set; }
    public string Etapa { get; set; } = "Postulado";
    public decimal? Calificacion { get; set; }

    public Vacante Vacante { get; set; } = default!;
    public Candidato Candidato { get; set; } = default!;
}

public class Usuario
{
    public int IdUsuario { get; set; }
    public int? IdEmpleado { get; set; }
    public string Correo { get; set; } = default!;
    public byte[] PasswordHash { get; set; } = default!;
    public bool Activo { get; set; } = true;
    public DateTime? UltimoAcceso { get; set; }

    public Empleado? Empleado { get; set; }
    public ICollection<UsuarioRol> Roles { get; set; } = new List<UsuarioRol>();
}

public class Rol
{
    public int IdRol { get; set; }
    public string Nombre { get; set; } = default!; // Admin | RRHH | Jefe | Empleado
    public string? Descripcion { get; set; }
}

public class UsuarioRol
{
    public int IdUsuario { get; set; }
    public int IdRol { get; set; }
    public int? AsignadoPor { get; set; }
    public DateTime FechaAsignacion { get; set; }

    public Usuario Usuario { get; set; } = default!;
    public Rol Rol { get; set; } = default!;
}

public class Auditoria
{
    public long IdAuditoria { get; set; }
    public int? IdUsuario { get; set; }
    public string Entidad { get; set; } = default!;
    public string IdRegistro { get; set; } = default!;
    public string Accion { get; set; } = default!;
    public DateTime FechaHora { get; set; }
    public string? Ip { get; set; }
    public string? ValoresAntes { get; set; } // JSON
    public string? ValoresDespues { get; set; } // JSON
}
