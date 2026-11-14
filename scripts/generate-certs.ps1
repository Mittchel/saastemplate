<#
.SYNOPSIS
    Generates the OpenIddict signing and encryption certificates.

.DESCRIPTION
    Saas.Api and Saas-Ai both validate access tokens against the identity
    service's JWKS endpoint, so the signing key must be stable across restarts.
    OpenIddict's AddDevelopmentSigningCertificate() regenerates on every boot,
    which breaks both consumers -- hence real certificate files on a volume.

    Requires openssl. On Windows it ships with Git for Windows
    (C:\Program Files\Git\mingw64\bin\openssl.exe).

.EXAMPLE
    ./scripts/generate-certs.ps1 -Password 'my_cert_password'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Password,

    # Nested calls rather than Join-Path's multi-argument form, which only
    # exists in PowerShell 7+ and fails on Windows PowerShell 5.1.
    [string]$OutputDirectory = (Join-Path (Join-Path $PSScriptRoot '..') 'certs'),

    [int]$Days = 3650
)

$ErrorActionPreference = 'Stop'

$openssl = (Get-Command openssl -ErrorAction SilentlyContinue).Source
if (-not $openssl) {
    $gitOpenssl = 'C:\Program Files\Git\mingw64\bin\openssl.exe'
    if (Test-Path $gitOpenssl) {
        $openssl = $gitOpenssl
    } else {
        throw "openssl not found. Install Git for Windows or add openssl to PATH."
    }
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

# openssl reports progress on stderr. Windows PowerShell turns native stderr
# into ErrorRecords, which $ErrorActionPreference = 'Stop' then treats as fatal
# even when the command succeeded -- so success is judged by exit code instead.
function Invoke-OpenSsl {
    param([string[]]$Arguments, [string]$What)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & $openssl @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "$What failed (exit $LASTEXITCODE):`n$($output -join [Environment]::NewLine)"
        }
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

function New-Pfx {
    param([string]$Name, [string]$Subject)

    $key = Join-Path $OutputDirectory "$Name.key"
    $crt = Join-Path $OutputDirectory "$Name.crt"
    $pfx = Join-Path $OutputDirectory "$Name.pfx"

    Invoke-OpenSsl -What "openssl req ($Name)" -Arguments @(
        'req', '-x509', '-newkey', 'rsa:2048', '-sha256', '-days', "$Days", '-nodes',
        '-keyout', $key, '-out', $crt, '-subj', $Subject
    )

    # AES rather than the OpenSSL 1.x default of 3DES/SHA1: .NET rejects legacy
    # PKCS#12 algorithms when built against OpenSSL 3, as the containers are.
    Invoke-OpenSsl -What "openssl pkcs12 ($Name)" -Arguments @(
        'pkcs12', '-export', '-out', $pfx, '-inkey', $key, '-in', $crt,
        '-passout', "pass:$Password",
        '-keypbe', 'AES-256-CBC', '-certpbe', 'AES-256-CBC', '-macalg', 'sha256'
    )

    Remove-Item $key, $crt -Force
    Write-Host "  created $pfx"
}

Write-Host "Generating OpenIddict certificates into $OutputDirectory"
New-Pfx -Name 'signing'    -Subject '/CN=Saas Token Signing'
New-Pfx -Name 'encryption' -Subject '/CN=Saas Token Encryption'

Write-Host ""
Write-Host "Done. Set these in .env:" -ForegroundColor Green
Write-Host "  OIDC_SIGNING_CERT_PASSWORD=$Password"
Write-Host "  OIDC_ENCRYPTION_CERT_PASSWORD=$Password"
