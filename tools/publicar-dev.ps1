# Gera uma cópia local para testar o Pizza. Nunca distribua este resultado: só o CI publica (SPECS §11).
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$saida = Join-Path $raiz 'artifacts\publish\app'

dotnet publish (Join-Path $raiz 'src\Pizza.App') -c Release -o $saida --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish (Join-Path $raiz 'src\Pizza.Worker') -c Release -o $saida --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# O Worker roda em AppContainer e só consegue iniciar se puder ler a própria pasta (V-03).
# S-1-15-2-1 é "ALL APPLICATION PACKAGES"; a permissão é só de leitura e execução.
icacls $saida /grant '*S-1-15-2-1:(OI)(CI)(RX)' /T /Q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Pronto: $saida\Pizza.exe"
