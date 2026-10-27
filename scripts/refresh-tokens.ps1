# Copies the web's design tokens into design/tokens/web-tokens.css so TokenDriftTests can compare Tokens.axaml with them.
# Run from anywhere; expects the web repo next to this one (or pass -WebRepo).
param([string]$WebRepo = (Join-Path $PSScriptRoot '..\..\CommunitiesForever-Web'))

$ErrorActionPreference = 'Stop'
$source = Join-Path $WebRepo 'src\styles\tokens.css'
$target = Join-Path $PSScriptRoot '..\design\tokens\web-tokens.css'
$commit = git -C $WebRepo log -1 --format=%H -- src/styles/tokens.css
$header = "/* Copy of CommunitiesForever-Web src/styles/tokens.css at $commit. Refresh with scripts/refresh-tokens.ps1; do not edit. */"
$content = Get-Content $source -Raw -Encoding UTF8
[IO.File]::WriteAllText($target, "$header`n$content".Replace("`r`n", "`n"))
Write-Host "Copied tokens.css at $commit"
