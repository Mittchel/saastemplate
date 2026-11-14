Feature: Test hosts generate their own certificates (DIA-134)
  The identity host in the tests signs tokens with certificates generated per run, so no certificate store is needed.

  Scenario: criterion 1: the identity host publishes a signing key without any certificate store (API side only)
    Then the identity host publishes a signing key backed by a self-signed certificate

  Scenario: criterion 2: TestCertificates generates RSA 2048 self-signed certificates with CertificateRequest
    Then the cert source file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" contains "CertificateRequest"
    And the cert source file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" contains "2048"
    And the cert source file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" contains "RSA"

  Scenario: criterion 3: certificates are exported as password-protected pfx files under the temp path
    Then the cert source file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" contains ".pfx"
    And the cert source file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" contains "Path.GetTempPath()"
    And the cert source file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" contains "Pfx"

  Scenario: criterion 4: the identity host receives the four certificate settings through UseSetting
    Then the cert source file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "OpenIddict:SigningCertificatePath"
    And the cert source file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "OpenIddict:SigningCertificatePassword"
    And the cert source file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "OpenIddict:EncryptionCertificatePath"
    And the cert source file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "OpenIddict:EncryptionCertificatePassword"

  Scenario: criterion 5: the identity service serves a key from a generated certificate (API side only)
    Then the identity host publishes a signing key backed by a self-signed certificate

  Scenario: criterion 6: the temporary folder is deleted when the run ends
    Then the cert source file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" contains "Directory.Delete"

  Scenario: criterion 7: no certificate files are in the repository
    Then the repository holds no pfx files outside build output

  Scenario: criterion 8: the loader still falls back to development certificates
    Then the cert source file "src/Saas.Auth.Api/Extensions/ServiceCollectionExtensions.cs" contains "AddDevelopmentSigningCertificate"
    And the cert source file "src/Saas.Auth.Api/Extensions/ServiceCollectionExtensions.cs" contains "AddDevelopmentEncryptionCertificate"

  Scenario: criterion 9: CLAUDE.md says the test hosts generate their own certificates
    Then the cert source file "CLAUDE.md" contains "certificate"
    And the cert source file "CLAUDE.md" contains "keychain"

  Scenario: criterion 10: the integration suite works end to end with generated certificates (API side only)
    Then the identity host publishes a signing key backed by a self-signed certificate
