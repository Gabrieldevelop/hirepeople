using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Rrhh.Api.Contracts;

namespace Rrhh.Api.Services;

/// Emite access token (60 min) y refresh token (7 días). El refresh lleva el claim typ=refresh.
public class TokenService
{
    private readonly IConfiguration _cfg;
    private readonly byte[] _key;

    public TokenService(IConfiguration cfg)
    {
        _cfg = cfg;
        _key = Encoding.UTF8.GetBytes(cfg["Jwt:Key"] ?? throw new InvalidOperationException("Defina Jwt__Key."));
    }

    public TokenResponse Emitir(int idUsuario, int? idEmpleado, IEnumerable<string> roles)
    {
        var handler = new JwtSecurityTokenHandler();
        var creds = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, idUsuario.ToString()) };
        if (idEmpleado is int emp) claims.Add(new Claim("idEmpleado", emp.ToString()));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var expiraAcceso = DateTime.UtcNow.AddMinutes(60);
        var access = handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiraAcceso,
            SigningCredentials = creds,
            Issuer = _cfg["Jwt:Issuer"],
            Audience = _cfg["Jwt:Audience"]
        });

        var refresh = handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, idUsuario.ToString()),
                new Claim("typ", "refresh")
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = creds,
            Issuer = _cfg["Jwt:Issuer"],
            Audience = _cfg["Jwt:Audience"]
        });

        return new TokenResponse(handler.WriteToken(access), handler.WriteToken(refresh), expiraAcceso);
    }

    public ClaimsPrincipal? ValidarRefresh(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _cfg["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _cfg["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);
            return principal.FindFirst("typ")?.Value == "refresh" ? principal : null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
