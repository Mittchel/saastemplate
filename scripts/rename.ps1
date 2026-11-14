<#
.SYNOPSIS
    Renames the template from "Saas" to a product name.

.DESCRIPTION
    Rewrites .NET namespaces and project names, renames the project folders and
    the solution, and updates the compose project name and database name.

    Run it once, immediately after cloning, before writing any product code.
    It rewrites files in place and has no undo: commit (or stash) first.

.EXAMPLE
    ./scripts/rename.ps1 -Name Acme

.EXAMPLE
    ./scripts/rename.ps1 -Name Acme -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    # The new product name, in PascalCase. Becomes the .NET root namespace.
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]{1,30}$')]
    [string]$Name
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$old = 'Saas'
$oldLower = 'saas'
$newLower = $Name.ToLowerInvariant()

if ($Name -eq $old) {
    throw "The project is already called $old."
}

Write-Host "Renaming $old -> $Name in $root" -ForegroundColor Cyan

# Directories that must never be touched: build output, dependencies, and the
# local git history.
$excludedDirectories = @(
    '\bin\', '\obj\', '\node_modules\', '\.next\', '\.git\',
    '\.venv\', '__pycache__', '\.ruff_cache\', '\.pytest_cache\',
    # Local editor/agent settings are not part of the product.
    '\.claude\'
)

# This script holds the old name in $old, so rewriting it would turn the rename
# into a no-op for anyone who runs it again.
$selfPath = $MyInvocation.MyCommand.Path

# Only text we own. Binary assets and lockfiles are left alone -- a lockfile
# references package names, not the product.
$includedExtensions = @(
    '.cs', '.csproj', '.slnx', '.sln', '.json', '.ts', '.tsx', '.js', '.jsx',
    '.py', '.yml', '.yaml', '.md', '.ps1', '.sh', '.sql', '.props', '.feature',
    '.editorconfig', '.example', '.toml', '.Dockerfile'
)

function Test-Excluded {
    param([string]$Path)

    foreach ($fragment in $excludedDirectories) {
        if ($Path -like "*$fragment*") { return $true }
    }
    return $false
}

# --- 1. File contents ------------------------------------------------------

# Extension-less files we still own. package-lock.json is excluded outright:
# it is generated, and the only "saas" in it is the package name npm rewrites
# on the next install.
$includedNames = @('Dockerfile', '.env.example', '.editorconfig', '.gitignore', '.dockerignore')

$files = Get-ChildItem -Path $root -Recurse -File |
    Where-Object {
        -not (Test-Excluded $_.FullName) -and
        $_.FullName -ne $selfPath -and
        $_.Name -ne 'package-lock.json' -and
        ($includedExtensions -contains $_.Extension -or $includedNames -contains $_.Name)
    }

$changed = 0
foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8

    if ($content -notmatch $old -and $content -cnotmatch $oldLower) { continue }

    # Case-sensitive, both casings: PascalCase for namespaces and project
    # names, lowercase for container, database and package names.
    $updated = $content -creplace $old, $Name -creplace $oldLower, $newLower

    if ($updated -ne $content) {
        if ($PSCmdlet.ShouldProcess($file.FullName, 'rewrite contents')) {
            # No BOM: a BOM in a .sh script breaks the shebang, and Postgres
            # init scripts are executed directly.
            [System.IO.File]::WriteAllText($file.FullName, $updated, [System.Text.UTF8Encoding]::new($false))
        }
        $changed++
    }
}
Write-Host "  rewrote $changed file(s)"

# --- 2. Paths --------------------------------------------------------------
# Deepest first, so renaming a parent cannot invalidate a queued child path.

$paths = Get-ChildItem -Path $root -Recurse |
    Where-Object { -not (Test-Excluded $_.FullName) -and $_.Name -match $old } |
    Sort-Object { $_.FullName.Length } -Descending

$renamed = 0
foreach ($item in $paths) {
    $newName = $item.Name -creplace $old, $Name -creplace $oldLower, $newLower
    if ($newName -eq $item.Name) { continue }

    if ($PSCmdlet.ShouldProcess($item.FullName, "rename to $newName")) {
        Rename-Item -LiteralPath $item.FullName -NewName $newName
    }
    $renamed++
}
Write-Host "  renamed $renamed path(s)"

Write-Host ""
Write-Host "Done. Next:" -ForegroundColor Green
Write-Host "  1. dotnet build $Name.slnx"
Write-Host "  2. docker compose build"
Write-Host "  3. Delete the old Postgres volume if the database name changed:"
Write-Host "       docker compose down -v"
