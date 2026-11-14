Feature: Test hosts generate their own certificates (DIA-134)
  The integration test hosts need no certificate store or keychain.

  Scenario: criterion 1: signing in works with generated certificates
    When I sign in as the development user
    Then the sign in succeeds
    And the access token carries an organization

  Scenario: criterion 2: self-signed RSA 2048 certificates are generated in TestCertificates
    Then the certificate source file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" exists
    And the infra file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" contains "CertificateRequest"
    And the infra file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" contains "2048"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "TestCertificates"

  Scenario: criterion 3: certificates are exported as password-protected pfx files under the temp path
    Then the certificate sources together contain "Path.GetTempPath()"
    And the certificate sources together contain ".pfx"
    And the certificate sources together contain "Password"

  Scenario: criterion 4: the identity host receives the certificate settings through UseSetting
    Then the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "OpenIddict:SigningCertificatePath"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "OpenIddict:SigningCertificatePassword"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "OpenIddict:EncryptionCertificatePath"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "OpenIddict:EncryptionCertificatePassword"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "UseSetting"

  Scenario: criterion 5: tokens are issued by the identity service using the generated files
    When I sign in as the development user
    Then the sign in succeeds
    And the access token carries the role "Owner"

  Scenario: criterion 6: the temporary folder is deleted when the run ends
    Then the certificate sources together contain "Directory.Delete"

  Scenario: criterion 7: nothing is written to the repository
    Then the infra file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" does not contain "AppContext.BaseDirectory"
    And the infra file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" does not contain "Directory.GetCurrentDirectory"
    And the infra file "src/Saas.Api.Tests/Drivers/TestCertificates.cs" does not contain "Saas.slnx"

  Scenario: criterion 8: the identity service's certificate loading is unchanged
    Then the infra file "src/Saas.Auth.Api/Extensions/ServiceCollectionExtensions.cs" contains "AddDevelopmentSigningCertificate"
    And the infra file "src/Saas.Auth.Api/Extensions/ServiceCollectionExtensions.cs" contains "AddDevelopmentEncryptionCertificate"
    And the infra file "src/Saas.Auth.Api/Extensions/ServiceCollectionExtensions.cs" contains "SigningCertificatePath"

  Scenario: criterion 9: CLAUDE.md states the hosts generate their own certificates
    Then the infra file "CLAUDE.md" contains "certificates"
    And the infra file "CLAUDE.md" contains "keychain"

  Scenario: criterion 10: the integration scenarios still pass in CI
    When I sign in with email "dev@saas.local" and password "WrongPassword123"
    Then the sign in fails
