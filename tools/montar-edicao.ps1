# Monta a pasta de uma edição do Pizza (ADR-034, etapa 4) a partir dos manifestos de packaging/.
# Falha se o manifesto citar um arquivo que não existe ou se aparecer um arquivo fora do manifesto.
# Roda offline e não baixa nada: o motor do Jumbo vem de tools/baixar-motor.ps1.
# Uso: ./tools/montar-edicao.ps1 -Edicao brotinho|jumbo [-Pizza pasta] [-Docs pasta] [-Motor pasta] [-Saida pasta]
param(
    [Parameter(Mandatory)] [ValidateSet('brotinho', 'jumbo')] [string] $Edicao,
    # Saída do publish do Pizza.App e do Pizza.Worker (mesma pasta).
    [string] $Pizza = 'artifacts/previa/Pizza',
    # Saída do publish do Pizza.Docs (só o Jumbo usa).
    [string] $Docs = 'artifacts/previa/Docs',
    # Motor extraído por tools/baixar-motor.ps1 (só o Jumbo usa).
    [string] $Motor = 'artifacts/previa/motor',
    [string] $Saida
)

$ErrorActionPreference = 'Stop'

$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestos = Join-Path $raiz 'packaging'

function Get-Absoluto([string] $caminho) {
    if ([IO.Path]::IsPathRooted($caminho)) { return [IO.Path]::GetFullPath($caminho) }
    return [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $caminho))
}

# Devolve os manifestos na ordem de aplicação: os herdados primeiro.
function Get-Cadeia([string] $nome, [string[]] $caminho) {
    if ($caminho -contains $nome) { throw "Herança circular nos manifestos: $($caminho -join ' > ') > $nome" }
    $arquivo = Join-Path $manifestos "$nome.json"
    if (-not (Test-Path $arquivo -PathType Leaf)) { throw "Manifesto não encontrado: packaging/$nome.json" }
    $manifesto = Get-Content $arquivo -Raw -Encoding utf8 | ConvertFrom-Json
    if ($manifesto.esquema -ne 1) { throw "packaging/$nome.json: esquema $($manifesto.esquema) não suportado." }
    if ($manifesto.edicao -ne $nome) { throw "packaging/$nome.json: o campo edicao é '$($manifesto.edicao)'." }
    foreach ($pai in @($manifesto.herda)) {
        if ($pai) { Get-Cadeia $pai ($caminho + $nome) }
    }
    $manifesto
}

function Get-Sha256([string] $arquivo) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($arquivo))) } finally { $sha.Dispose() }
}

function Get-Relativos([string] $pasta) {
    $base = (Get-Item $pasta).FullName.TrimEnd('\') + '\'
    Get-ChildItem $pasta -Recurse -File | ForEach-Object { $_.FullName.Substring($base.Length) -replace '\\', '/' }
}

# Cada manifesto aparece uma vez (o Jumbo herda o Brotinho, que herda o comum).
$cadeia = @(Get-Cadeia $Edicao @()) | Group-Object edicao | ForEach-Object { $_.Group[0] }
$proprio = $cadeia | Where-Object { $_.edicao -eq $Edicao }

$pastas = @{ pizza = (Get-Absoluto $Pizza); docs = (Get-Absoluto $Docs); motor = (Get-Absoluto $Motor); raiz = $raiz }
if (-not $Saida) { $Saida = "artifacts/previa/$((Get-Culture).TextInfo.ToTitleCase($Edicao))" }
$destinoRaiz = Get-Absoluto $Saida
if ((Test-Path $destinoRaiz) -and @(Get-ChildItem $destinoRaiz -Force).Count -gt 0) { throw "A pasta de saída já tem arquivos: $destinoRaiz" }

$erros = New-Object System.Collections.Generic.List[string]
$plano = New-Object System.Collections.Generic.List[object]   # origem completa -> destino relativo
$listados = @{}                                               # origem -> nomes citados no manifesto
$ignorar = @{}
$compartilhados = @{}

foreach ($manifesto in $cadeia) {
    if ($manifesto.origens) {
        foreach ($propriedade in $manifesto.origens.PSObject.Properties) {
            $ignorar[$propriedade.Name] = @($propriedade.Value.ignorar)
            if ($propriedade.Value.compartilhados) { $compartilhados[$propriedade.Name] = @($propriedade.Value.compartilhados) }
        }
    }
}

foreach ($manifesto in $cadeia) {
    foreach ($entrada in @($manifesto.entradas)) {
        if (-not $entrada) { continue }
        $origem = $entrada.origem
        if (-not $pastas.ContainsKey($origem)) { $erros.Add("packaging/$($manifesto.edicao).json: origem desconhecida '$origem'."); continue }
        $pasta = $pastas[$origem]
        if (-not (Test-Path $pasta -PathType Container)) { $erros.Add("Pasta da origem '$origem' não existe: $pasta"); continue }
        $destino = if ($entrada.destino) { $entrada.destino.TrimEnd('/') + '/' } else { '' }

        if ($entrada.arvore) {
            foreach ($exigido in @($entrada.exige)) {
                if (-not (Test-Path (Join-Path $pasta $exigido) -PathType Leaf)) { $erros.Add("Manifesto cita '$exigido', que não existe em $origem ($pasta).") }
            }
            $plano.Add([pscustomobject]@{ Origem = $pasta; Destino = $destino; Arvore = $true })
            continue
        }

        if (-not $listados.ContainsKey($origem)) { $listados[$origem] = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase) }
        foreach ($arquivo in @($entrada.arquivos)) {
            $listados[$origem].Add($arquivo) | Out-Null
            $completo = Join-Path $pasta $arquivo
            if (-not (Test-Path $completo -PathType Leaf)) { $erros.Add("Manifesto cita '$arquivo', que não existe em $origem ($pasta)."); continue }
            $plano.Add([pscustomobject]@{ Origem = $completo; Destino = "$destino$arquivo"; Arvore = $false })
        }
    }
}

# Arquivo fora do manifesto: a pasta de publicação trouxe algo que nenhuma edição declarou.
foreach ($origem in $listados.Keys) {
    if ($origem -eq 'raiz') { continue }   # a raiz é o repositório inteiro; só os arquivos citados entram
    $pasta = $pastas[$origem]
    if (-not (Test-Path $pasta -PathType Container)) { continue }
    $partilhados = if ($compartilhados.ContainsKey($origem)) { $compartilhados[$origem] } else { @() }
    foreach ($relativo in Get-Relativos $pasta) {
        if ($listados[$origem].Contains($relativo)) { continue }
        if ($ignorar.ContainsKey($origem) -and ($ignorar[$origem] | Where-Object { $_ -and $relativo -like $_ })) { continue }
        if ($partilhados -contains $relativo) {
            # Arquivo repetido de propósito: tem de ser idêntico ao da pasta principal.
            $outro = Join-Path $pastas['pizza'] $relativo
            if (-not (Test-Path $outro -PathType Leaf) -or (Get-Sha256 $outro) -ne (Get-Sha256 (Join-Path $pasta $relativo))) {
                $erros.Add("'$relativo' em $origem difere do mesmo arquivo em pizza.")
            }
            continue
        }
        $erros.Add("Arquivo fora do manifesto em ${origem}: $relativo")
    }
}

# Destino repetido e arquivo proibido na edição (o Brotinho nunca leva Pizza.Docs nem o motor).
$vistos = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
foreach ($item in $plano) {
    if (-not $vistos.Add($item.Destino)) { $erros.Add("Destino repetido no manifesto: $($item.Destino)") }
    foreach ($proibido in @($proprio.proibidos)) {
        if ($proibido -and $item.Destino -like $proibido) { $erros.Add("A edição $Edicao não pode levar '$($item.Destino)' (proibido por '$proibido').") }
    }
}

if ($erros.Count -gt 0) {
    $erros | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    throw "Edição $Edicao não montada: $($erros.Count) problema(s) com o manifesto."
}

New-Item -ItemType Directory -Force $destinoRaiz | Out-Null
foreach ($item in $plano) {
    if ($item.Arvore) {
        $alvo = Join-Path $destinoRaiz $item.Destino
        New-Item -ItemType Directory -Force $alvo | Out-Null
        Copy-Item (Join-Path $item.Origem '*') $alvo -Recurse -Force
    }
    else {
        $alvo = Join-Path $destinoRaiz $item.Destino
        New-Item -ItemType Directory -Force (Split-Path $alvo -Parent) | Out-Null
        Copy-Item $item.Origem $alvo
    }
}

$total = @(Get-ChildItem $destinoRaiz -Recurse -File)
Write-Host ("Edição {0} montada em {1}: {2} arquivos, {3} MB." -f $Edicao, $destinoRaiz, $total.Count, [math]::Round(($total | Measure-Object Length -Sum).Sum / 1MB, 1))
