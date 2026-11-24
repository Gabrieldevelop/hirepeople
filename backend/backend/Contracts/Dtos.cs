namespace Rrhh.Api.Contracts;

public record LoginRequest(string Correo, string Password);
public record RefreshRequest(string RefreshToken);
public record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiraUtc);

public record EmpleadoRequest(string Codigo, string? Cedula, string Nombres, string Apellidos,
    DateOnly FechaNacimiento, string Correo, int IdDepartamento, int IdCargo, DateOnly FechaIngreso);
public record EstadoEmpleadoRequest(string Estado);
public record ContratoRequest(string Tipo, DateOnly FechaInicio, DateOnly? FechaFin, decimal Salario);

public record DepartamentoRequest(string Nombre, int? IdResponsable, bool Activo = true);
public record CargoRequest(string Nombre, int IdDepartamento, decimal SalarioBase);

public record CorreccionAsistenciaRequest(TimeOnly? HoraEntrada, TimeOnly? HoraSalida, string Justificacion);

public record AusenciaRequest(int? IdEmpleado, int IdTipo, DateOnly FechaInicio, DateOnly FechaFin, string? Motivo);
public record AprobacionRequest(string Decision, string? Comentario);

public record PeriodoRequest(short Anio, byte Mes, string Tipo, DateOnly FechaPago);
public record ConceptoRequest(string Nombre, string Tipo, decimal Porcentaje, bool EsObligatorio);

public record VacanteRequest(int IdCargo, string Titulo, string Descripcion);
public record EstadoVacanteRequest(string Estado);
public record PostulacionRequest(string Nombres, string Apellidos, string Correo, string? Telefono, string? CvRuta);
public record EtapaRequest(string Etapa, decimal? Calificacion);

public record EvaluacionRequest(int IdEmpleado, string Periodo, decimal Puntaje, string? Comentarios, DateOnly Fecha);

public record UsuarioRequest(string Correo, string Password, int? IdEmpleado, List<string> Roles);
public record RolesRequest(List<string> Roles);
