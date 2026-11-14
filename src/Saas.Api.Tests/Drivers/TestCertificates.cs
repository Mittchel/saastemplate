using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Saas.Api.Tests.Drivers;

/// <summary>
/// Self-signed signing and encryption certificates for the identity host, written
/// as password-protected <c>.pfx</c> files into a fresh temporary folder.
/// </summary>
/// <remarks>
/// Without these the identity service falls back to OpenIddict's development
/// certificates, which live in the operating system's certificate store (the
/// keychain on macOS). Files make the tests independent of any store.
/// </remarks>
public sealed class TestCertificates : IDisposable
{
    private TestCertificates(string folder, string password)
    {
        Folder = folder;
        Password = password;
        SigningCertificatePath = Path.Combine(folder, "signing.pfx");
        EncryptionCertificatePath = Path.Combine(folder, "encryption.pfx");
    }

    public string Folder { get; }
    public string Password { get; }
    public string SigningCertificatePath { get; }
    public string EncryptionCertificatePath { get; }

    /// <summary>Generates both certificates. The folder is removed again if anything fails.</summary>
    public static TestCertificates Create()
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var folder = Path.Combine(Path.GetTempPath(), "saas-test-certs-" + Guid.CreateVersion7().ToString("n"));
        Directory.CreateDirectory(folder);

        var certificates = new TestCertificates(folder, password);
        try
        {
            Export(certificates.SigningCertificatePath, "CN=saas-test-signing", X509KeyUsageFlags.DigitalSignature, password);
            Export(certificates.EncryptionCertificatePath, "CN=saas-test-encryption", X509KeyUsageFlags.KeyEncipherment, password);
            return certificates;
        }
        catch
        {
            certificates.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort: a leftover temp folder must not fail the run.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }

    private static void Export(string path, string subject, X509KeyUsageFlags usage, string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, critical: true));

        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
    }
}
