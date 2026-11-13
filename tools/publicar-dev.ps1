# Gera uma cópia local para testar o Pizza. Nunca distribua este resultado: só o CI publica (SPECS §11).
# -Motor <pasta>: cópia do Pizza Jumbo, com a pasta "motor" apontando (junção) para o LibreOffice já extraído
# por tools/baixar-motor.ps1 (ADR-034). Sem -Motor, é o Brotinho.
param([string] $Motor)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$saida = Join-Path $raiz 'artifacts\publish\app'

dotnet publish (Join-Path $raiz 'src\Pizza.App') -c Release -o $saida --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish (Join-Path $raiz 'src\Pizza.Worker') -c Release -o $saida --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish (Join-Path $raiz 'src\Pizza.Docs') -c Release -o $saida --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# V-03: o próprio Pizza dá ao Worker leitura e execução da pasta ao abrir, como no .zip da prévia.

$pastaMotor = Join-Path $saida 'motor'
if (Test-Path $pastaMotor) { (Get-Item $pastaMotor).Delete() }
if ($Motor) {
    New-Item -ItemType Junction -Path $pastaMotor -Target (Resolve-Path $Motor).Path | Out-Null
    Write-Host "Pizza Jumbo: motor em $Motor"
}

Write-Host "Pronto: $saida\Pizza.exe"
