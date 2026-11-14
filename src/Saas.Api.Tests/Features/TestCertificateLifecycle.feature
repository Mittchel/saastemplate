Feature: Test certificate lifecycle
    The generated test certificates are the only ones the identity service uses, and they leave nothing behind.

    Scenario: The identity service uses only the generated certificates, not the development ones
        Then the identity service signs only with the generated signing certificate
        And the identity service encrypts only with the generated encryption certificate

    Scenario: The certificates are kept out of the repository
        Then the certificate folder is not inside the repository

    Scenario: Disposing the certificates removes their folder
        Given a fresh set of test certificates
        When the test certificates are disposed
        Then their folder no longer exists

    Scenario: A failed host startup removes the certificate folder
        When the test hosts fail to start after generating their certificates
        Then the startup failure is reported
        And the certificate folder of that startup no longer exists

    Scenario: A failed certificate generation leaves no folder behind
        When generating test certificates fails
        Then no certificate folder is left behind
