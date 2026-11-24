using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Rrhh.Api.Services;

/// Cifrado AES-256-GCM para cédula, salarios y otros datos sensibles.
/// Formato del resultado: nonce (12 bytes) + tag (16 bytes) + texto cifrado.
/// La clave viene de la variable de entorno Crypto__Key (32 bytes en base64).
public class CryptoService
{
    private readonly byte[] _key;

    public CryptoService(IConfiguration config)
    {
        var b64 = config["Crypto:Key"] ?? throw new InvalidOperationException("Defina Crypto__Key (32 bytes en base64).");
        _key = Convert.FromBase64String(b64);
        if (_key.Length != 32) throw new InvalidOperationException("Crypto__Key debe representar 32 bytes.");
    }

    public byte[] Encrypt(string texto)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var datos = Encoding.UTF8.GetBytes(texto);
        var cifrado = new byte[datos.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, datos, cifrado, tag);

        var resultado = new byte[12 + 16 + cifrado.Length];
        nonce.CopyTo(resultado, 0);
        tag.CopyTo(resultado, 12);
        cifrado.CopyTo(resultado, 28);
        return resultado;
    }

    public string Decrypt(byte[] payload)
    {
        var nonce = payload[..12];
        var tag = payload[12..28];
        var cifrado = payload[28..];
        var plano = new byte[cifrado.Length];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, cifrado, tag, plano);
        return Encoding.UTF8.GetString(plano);
    }

    public byte[] EncryptDecimal(decimal valor) =>
        Encrypt(valor.ToString(CultureInfo.InvariantCulture));

    public decimal DecryptDecimal(byte[] payload) =>
        decimal.Parse(Decrypt(payload), CultureInfo.InvariantCulture);

    /// Muestra solo los últimos cuatro caracteres.
    public static string Mask(string texto) =>
        texto.Length <= 4 ? new string('*', texto.Length) : new string('*', texto.Length - 4) + texto[^4..];
}
