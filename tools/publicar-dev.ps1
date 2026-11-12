# Gera uma cópia local para testar o Pizza. Nunca distribua este resultado: só o CI publica (SPECS §11).
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$saida = Join-Path $raiz 'artifacts\publish\app'

dotnet publish (Join-Path $raiz 'src\Pizza.App') -c Release -o $saida --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish (Join-Path $raiz 'src\Pizza.Worker') -c Release -o $saida --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# V-03: o próprio Pizza dá ao Worker leitura e execução da pasta ao abrir, como no .zip da prévia.

Write-Host "Pronto: $saida\Pizza.exe"
