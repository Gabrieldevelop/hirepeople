# Backend RR. HH. (ASP.NET Core 8)

API REST que implementa los 54 endpoints del contrato `openapi.json`.

## Requisitos
- .NET 8 SDK
- SQL Server con la base de datos **TalentCoreDB** (creada por el equipo de base de datos)

## Variables de entorno
| Variable | Descripción |
|---|---|
| `TalentCoreDB` | Cadena de conexión a **TalentCoreDB** (obligatoria). Ejemplo: `Server=localhost;Database=TalentCoreDB;Trusted_Connection=True;TrustServerCertificate=True;` |
| `Jwt__Key` | Clave de firma JWT, de al menos 32 caracteres. |
| `Crypto__Key` | 32 bytes en base64 para cifrar cédula y salarios. Generar con `openssl rand -base64 32`. |

Emisor y audiencia del JWT están en `appsettings.json` (`Jwt:Issuer`, `Jwt:Audience`).

## Puesta en marcha
```bash
dotnet restore
dotnet build
dotnet run                                              # http://localhost:5000 (Swagger en Development)
dotnet run -- create-admin admin@empresa.com "ClaveSegura!2026"   # primer administrador
```

## Notas importantes
- **No hay migraciones.** El esquema lo crea el equipo de base de datos. Los nombres de tabla y columna en `Infrastructure/AppDbContext.cs` deben coincidir con el esquema real. Si difieren, la forma más segura es regenerar las entidades con `dotnet ef dbcontext scaffold`.
- **Roles:** `Admin`, `RRHH`, `Jefe` y `Empleado`. Un usuario puede tener varios. `Jefe` es el responsable de un departamento (`Departamentos.IdResponsable`).
- **Cifrado:** `Empleados.CedulaCifrada` y `Contratos.SalarioCifrado` se cifran con AES-256-GCM. `Cargos.SalarioBase` se guarda en claro (ver pendientes).

## Pendientes conocidos
- Generación de PDF de recibos y exportación xlsx/pdf de reportes: responden `501`.
- Logout no revoca el refresh token (no hay tabla de revocación en el modelo).
- Cuenta bancaria: no existe en el modelo de datos.
- Feriados: los días hábiles no descuentan feriados.
- Carga de CV: se recibe la ruta del archivo, no el archivo.
- Cifrado de `Cargos.SalarioBase`: pendiente de decidir.
