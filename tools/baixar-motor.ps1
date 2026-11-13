# Motor de documentos do Pizza Jumbo (ADR-034): baixa o MSI oficial do LibreOffice, confere o SHA-256 e extrai sem
# instalar nada. Usado pelo CI (testes e pacote). O Pizza nunca baixa o motor sozinho (S-05).
param(
    [Parameter(Mandatory)] [string] $Destino
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Versão e hash publicados pela The Document Foundation (ramo "still", o mais maduro). Trocar os dois juntos.
$versao = '26.2.7'
$hash = '480fecf759fc6393bd614fbf43753eb95a8198fc1a065d41b727e3b664cd1e59'
$nome = "LibreOffice_$($versao)_Win_x86-64.msi"
$url = "https://download.documentfoundation.org/libreoffice/stable/$versao/win/x86_64/$nome"

$temporaria = Join-Path ([IO.Path]::GetTempPath()) "pizza-motor-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory $temporaria | Out-Null
try {
    $msi = Join-Path $temporaria $nome
    Invoke-WebRequest $url -OutFile $msi -UserAgent 'pizza-ci' -MaximumRedirection 5
    $obtido = (Get-FileHash $msi -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($obtido -ne $hash) { throw "Motor com hash diferente do publicado: $obtido" }

    # Instalação administrativa: só copia os arquivos para a pasta, sem tocar no Windows.
    New-Item -ItemType Directory -Force $Destino | Out-Null
    $destinoCompleto = (Resolve-Path $Destino).Path
    $processo = Start-Process msiexec.exe -ArgumentList '/a', "`"$msi`"", '/qn', "TARGETDIR=`"$destinoCompleto`"" -Wait -PassThru
    if ($processo.ExitCode -ne 0) { throw "msiexec saiu com $($processo.ExitCode)" }

    # A instalação administrativa deixa uma cópia do MSI na pasta; ela não faz parte do motor.
    Get-ChildItem $destinoCompleto -Filter '*.msi' | Remove-Item
    if (-not (Test-Path (Join-Path $destinoCompleto 'program\mergedlo.dll'))) { throw 'Motor extraído sem program\mergedlo.dll.' }
    Write-Host "Motor $versao em $destinoCompleto"
}
finally {
    Remove-Item $temporaria -Recurse -Force -ErrorAction SilentlyContinue
}
