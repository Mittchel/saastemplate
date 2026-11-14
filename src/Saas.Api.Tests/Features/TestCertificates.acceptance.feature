Feature: Test hosts generate their own certificates (DIA-134)
  The identity host used by the tests signs with certificates the run generated, not with a certificate store.

  Scenario: criterion 1: signing in works without any certificate store
    When I sign in as the development user
    Then the sign in succeeds
    And the access token carries an organization

  Scenario: criterion 2: the signing certificate is a generated RSA 2048 self-signed certificate
    When I read the signing key published by the identity host
    Then the signing key is an RSA 2048 key
    And the signing certificate is not the OpenIddict development certificate

  Scenario: criterion 3: certificates are password-protected pfx files in a temporary folder
    Then the run has generated password-protected pfx files in a temporary folder

  Scenario: criterion 4: the identity host is configured with the generated files
    When I read the signing key published by the identity host
    Then the signing key is not held in the operating system's certificate store
    And the run has generated password-protected pfx files in a temporary folder

  Scenario: criterion 5: the development certificates are not reached
    When I read the signing key published by the identity host
    Then the signing key is not held in the operating system's certificate store
    And the signing certificate is not the OpenIddict development certificate

  Scenario: criterion 6: the temporary folder exists during the run and is removed at its end
    Then the run has generated password-protected pfx files in a temporary folder

  Scenario: criterion 7: nothing is written to the repository
    Then no certificate file is under the repository sources

  Scenario: criterion 8: certificate loading code and compose configuration are unchanged
    Then the identity service still falls back to the development certificates when no file is configured
    And the compose configuration does not refer to the test certificates

  Scenario: criterion 9: CLAUDE.md documents the generated certificates
    Then the Testing section of CLAUDE.md says the test hosts generate their own certificates

  Scenario: criterion 10: the integration scenarios pass in the CI job
    When I sign in as the development user
    Then the sign in succeeds
