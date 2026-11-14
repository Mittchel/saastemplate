using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
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
    public static TestCertificates Create() => Create(Path.GetTempPath(), "CN=saas-test-signing");

    /// <summary>Lets a test choose the parent folder and break the subject, to prove a failed run cleans up.</summary>
    internal static TestCertificates Create(string parent, string signingSubject)
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var folder = Path.Combine(parent, "saas-test-certs-" + Guid.CreateVersion7().ToString("n"));
        Directory.CreateDirectory(folder);

        var certificates = new TestCertificates(folder, password);
        try
        {
            Export(certificates.SigningCertificatePath, signingSubject, X509KeyUsageFlags.DigitalSignature, password);
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temp folder must not fail the run, but it holds private keys, so say where it is.
            Console.Error.WriteLine($"Could not delete test certificate folder '{Folder}': {ex.Message}");
        }
    }

    private static void Export(string path, string subject, X509KeyUsageFlags usage, string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, critical: true));

        // CreateSelfSigned and Export(Pfx) attach the private key through a temporary keychain on macOS,
        // which a confined run cannot create. Signing with the key directly and building the PKCS#12
        // ourselves keeps the key in memory on every platform.
        using var certificate = request.Create(
            request.SubjectName,
            X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1),
            RandomNumberGenerator.GetBytes(16));

        var encryption = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000);
        var contents = new Pkcs12SafeContents();

        // The same localKeyId on both bags is how the loader pairs the key with the certificate.
        var keyId = RandomNumberGenerator.GetBytes(20);
        contents.AddCertificate(certificate).Attributes.Add(new Pkcs9LocalKeyId(keyId));
        contents.AddShroudedKey(rsa, password, encryption).Attributes.Add(new Pkcs9LocalKeyId(keyId));

        var builder = new Pkcs12Builder();
        builder.AddSafeContentsEncrypted(contents, password, encryption);
        builder.SealWithMac(password, HashAlgorithmName.SHA256, 100_000);
        File.WriteAllBytes(path, builder.Encode());
    }
}
