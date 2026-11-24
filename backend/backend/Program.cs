using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Rrhh.Api.Services;
using Rrhh.Domain.Entities;
using Rrhh.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Base de datos TalentCoreDB: la cadena de conexión viene de la variable de entorno TalentCoreDB.
var cadena = builder.Configuration["TalentCoreDB"]
    ?? builder.Configuration.GetConnectionString("TalentCore")
    ?? throw new InvalidOperationException("Defina la variable de entorno TalentCoreDB (debe apuntar a la base TalentCoreDB).");

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cadena));
builder.Services.AddScoped<AuditService>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<CryptoService>();
builder.Services.AddSingleton<TokenService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Defina Jwt__Key.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    });
builder.Services.AddAuthorization();

// CORS: permite que el frontend React (Vite) llame a la API desde el navegador.
var origenes = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:5173", "http://localhost:5174"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Uso: dotnet run -- create-admin correo@empresa.com <contraseña>
// Crea el primer administrador (la contraseña debe tener al menos 10 caracteres).
if (args.Length >= 3 && args[0] == "create-admin")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var pwd = scope.ServiceProvider.GetRequiredService<PasswordService>();
    if (args[2].Length < 10) { Console.WriteLine("La contraseña debe tener al menos 10 caracteres."); return; }
    var rol = await db.Roles.FirstOrDefaultAsync(r => r.Nombre == "Admin");
    if (rol is null)
    {
        rol = new Rol { Nombre = "Admin", Descripcion = "Administrador del sistema" };
        db.Roles.Add(rol);
        await db.SaveChangesAsync();
    }
    var usuario = new Usuario { Correo = args[1], PasswordHash = pwd.Hash(args[2]), Activo = true };
    usuario.Roles.Add(new UsuarioRol { Rol = rol, FechaAsignacion = DateTime.UtcNow });
    db.Usuarios.Add(usuario);
    await db.SaveChangesAsync();
    Console.WriteLine($"Administrador creado: {args[1]}");
    return;
}

// Uso: dotnet run -- seed-demo
// Inserta datos ficticios (empleados, asistencia, ausencias, nómina, reclutamiento) para demostración.
if (args.Length >= 1 && args[0] == "seed-demo")
{
    using var scope = app.Services.CreateScope();
    var mensaje = await Rrhh.Infrastructure.DemoData.SembrarAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        scope.ServiceProvider.GetRequiredService<CryptoService>(),
        scope.ServiceProvider.GetRequiredService<PasswordService>());
    Console.WriteLine(mensaje);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
