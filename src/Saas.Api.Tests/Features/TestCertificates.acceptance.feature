Feature: Test hosts generate their own certificates
  The integration test hosts sign and encrypt tokens with certificates generated for the run, so no certificate store or keychain is needed.

  Scenario: criterion 1 and 10: signing in works without any certificate store
    When I sign in as the development user
    Then the sign in succeeds
    And the access token carries an organization

  Scenario: criterion 2 and 3: the run generated a signing and an encryption pfx in a fresh temporary folder
    Then the generated certificate folder exists
    And the generated certificate folder is under the temporary path
    And the generated certificate folder holds the signing pfx and at least two pfx files

  Scenario: criterion 4 and 5: the published signing key is the generated self-signed certificate
    Then the published signing keys are the generated self-signed certificate

  Scenario: criterion 6: the certificate folder is removed when the run ends
    Then the generated certificate folder is checked for removal after the run

  Scenario: criterion 7: nothing is written to the repository
    Then the generated certificate folder is outside the repository
    And no pfx file exists in the repository tree

  Scenario: criterion 8: production and compose configuration carry no test certificates
    Then no compose or appsettings file mentions the test certificates

  Scenario: criterion 9: CLAUDE.md states the test hosts generate their own certificates
    Then the Testing section of CLAUDE.md says tests need no certificate store or keychain
