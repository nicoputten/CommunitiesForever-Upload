# Copies the web's uploader API contract into docs/contract/UPLOADER-API.md. The Core tests take their sample
# responses from its "<!-- example: ... -->" blocks, so they follow the contract. The web repo owns it.
param([string]$WebRepo = (Join-Path $PSScriptRoot '..\..\CommunitiesForever-Web'), [string]$Ref = 'origin/develop')

$ErrorActionPreference = 'Stop'
# git writes UTF-8; Windows PowerShell would read it with the console code page
[Console]::OutputEncoding = New-Object Text.UTF8Encoding $false
git -C $WebRepo fetch -q origin
$commit = git -C $WebRepo log -1 --format=%H $Ref -- docs/UPLOADER-API.md
$content = (git -C $WebRepo show "${Ref}:docs/UPLOADER-API.md") -join "`n"
$header = "<!-- Copy of CommunitiesForever-Web docs/UPLOADER-API.md at $commit. Refresh with scripts/refresh-api-contract.ps1; do not edit. -->"
$target = Join-Path $PSScriptRoot '..\docs\contract\UPLOADER-API.md'
[IO.File]::WriteAllText($target, "$header`n$content`n", (New-Object Text.UTF8Encoding $false))
Write-Host "Copied UPLOADER-API.md at $commit"
