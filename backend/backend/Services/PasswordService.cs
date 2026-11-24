using System.Security.Cryptography;

namespace Rrhh.Api.Services;

/// Hash PBKDF2-SHA256. Formato almacenado: iteraciones (4 bytes) + sal (16) + clave (32).
public class PasswordService
{
    private const int Iteraciones = 100_000;
    private const int TamSal = 16;
    private const int TamClave = 32;

    public byte[] Hash(string password)
    {
        var sal = RandomNumberGenerator.GetBytes(TamSal);
        var clave = Rfc2898DeriveBytes.Pbkdf2(password, sal, Iteraciones, HashAlgorithmName.SHA256, TamClave);
        var resultado = new byte[4 + TamSal + TamClave];
        BitConverter.GetBytes(Iteraciones).CopyTo(resultado, 0);
        sal.CopyTo(resultado, 4);
        clave.CopyTo(resultado, 4 + TamSal);
        return resultado;
    }

    public bool Verify(string password, byte[] almacenado)
    {
        if (almacenado.Length != 4 + TamSal + TamClave) return false;
        var iter = BitConverter.ToInt32(almacenado, 0);
        var sal = almacenado[4..(4 + TamSal)];
        var esperada = almacenado[(4 + TamSal)..];
        var calculada = Rfc2898DeriveBytes.Pbkdf2(password, sal, iter, HashAlgorithmName.SHA256, TamClave);
        return CryptographicOperations.FixedTimeEquals(calculada, esperada);
    }
}
