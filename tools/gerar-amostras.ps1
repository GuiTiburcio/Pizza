# Amostras sintéticas para a matriz manual de lançamento (TST-10).
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Destino,
    [switch] $Conferir
)

$ErrorActionPreference = 'Stop'
$pasta = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destino)
$manifesto = Join-Path $pasta 'hashes-originais.sha256'

if (-not $Conferir) {
    $projeto = Join-Path $PSScriptRoot 'Pizza.Amostras/Pizza.Amostras.csproj'
    & dotnet run --project $projeto -c Release -- $pasta
    if ($LASTEXITCODE -ne 0) { throw "Gerador de amostras falhou ($LASTEXITCODE)." }
    return
}

$esperados = @{}
foreach ($linha in Get-Content -LiteralPath $manifesto) {
    if ($linha -cnotmatch '^([0-9A-F]{64})  ([A-Za-z0-9][A-Za-z0-9.-]*\.[A-Za-z0-9]+)$') {
        throw 'Linha inválida no manifesto de amostras.'
    }
    $nome = $Matches[2]
    if ($esperados.ContainsKey($nome)) { throw "Amostra duplicada no manifesto: $nome." }
    $esperados[$nome] = $Matches[1]
}

if ($esperados.Count -eq 0) { throw 'O manifesto não contém amostras.' }
foreach ($nome in $esperados.Keys | Sort-Object) {
    $entrada = Get-Item -LiteralPath (Join-Path $pasta $nome) -Force
    if ($entrada.PSIsContainer -or ($entrada.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Amostra não é um arquivo regular: $nome."
    }
    if ((Get-FileHash -LiteralPath $entrada.FullName -Algorithm SHA256).Hash -cne $esperados[$nome]) {
        throw "A origem foi alterada: $nome."
    }
}

$internet = Join-Path $pasta 'documento-da-internet.docx'
if ((Get-Content -LiteralPath $internet -Stream Zone.Identifier -Raw) -notmatch '(?m)^ZoneId=3\r?$') {
    throw 'A marca da Internet não foi preservada na origem.'
}
if (-not (Get-Item -LiteralPath (Join-Path $pasta 'somente-leitura.png')).IsReadOnly) {
    throw 'O atributo somente leitura não foi preservado na origem.'
}

Write-Output "$($esperados.Count) originais íntegros, marca da Internet e atributo somente leitura conferidos."
