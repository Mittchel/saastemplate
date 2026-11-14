Feature: Test certificates
    The test hosts generate their own signing and encryption certificates, so tests need no certificate store or keychain.

    Scenario: Criterion 1 and 10: signing in works with the generated certificates
        When I sign in as the development user
        Then the sign in succeeds
        And the access token carries an organization

    Scenario: Criterion 2 and 3: the certificate folder is a fresh temporary folder holding password-protected pfx files
        Then the certificate folder exists
        And the certificate folder is under the system temporary path
        And the certificate folder is outside the test output directory
        And the certificate folder holds at least two pfx files
        And the signing certificate file exists

    Scenario: Criterion 4 and 5: the published signing key comes from the generated certificate
        Then the published signing key is a self-signed certificate named "CN=saas-test-signing"

    Scenario: Criterion 7: nothing is written to the repository during the run
        Then the certificate folder is outside the test output directory
        And the certificate folder is not under the current directory
