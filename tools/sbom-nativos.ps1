# SBOM: acrescenta ao sbom.cdx.json do syft as dependências nativas que o build baixa (build/native-deps.json).
# O syft só enxerga os pacotes NuGet; sem isto, qpdf, pdfium e libjxl não aparecem para o acompanhamento de CVEs.
# Roda offline. Uso: ./tools/sbom-nativos.ps1 -Sbom artifacts/previa/sbom.cdx.json [-Escopo previa[,jumbo]]
param(
    [Parameter(Mandatory)] [string] $Sbom,
    [string] $Dependencias = (Join-Path $PSScriptRoot '..\build\native-deps.json'),
    [ValidateSet('previa', 'jumbo')] [string[]] $Escopo = 'previa'
)

$ErrorActionPreference = 'Stop'

$bom = Get-Content $Sbom -Raw | ConvertFrom-Json
$fonte = Get-Content $Dependencias -Raw | ConvertFrom-Json

# Rodar de novo não duplica: tira o que uma execução anterior acrescentou.
$existentes = @($bom.components | Where-Object { $_ -and $_.'bom-ref' -notlike 'pizza-nativo:*' })

$novos = foreach ($dep in $fonte.componentes | Where-Object { $_.escopo -in $Escopo }) {
    $licenca = if ($dep.licenca -like 'LicenseRef-*') { @{ license = @{ name = $dep.licenca } } }
    elseif ($dep.licenca -match '\s') { @{ expression = $dep.licenca } }
    else { @{ license = @{ id = $dep.licenca } } }
    [ordered]@{
        type = 'library'
        'bom-ref' = "pizza-nativo:$($dep.id)"
        name = $dep.nome
        version = $dep.versao
        purl = $dep.purl
        hashes = @(@{ alg = 'SHA-256'; content = $dep.sha256 })
        licenses = @($licenca)
        externalReferences = @(@{ type = 'distribution'; url = $dep.url })
        properties = @(
            @{ name = 'pizza:hash-de'; value = 'arquivo baixado pelo build' }
            foreach ($arquivo in $dep.arquivos) { @{ name = 'pizza:arquivo'; value = $arquivo } }
        )
    }
}

$bom.components = @($existentes) + @($novos)
$bom | ConvertTo-Json -Depth 20 | Set-Content $Sbom -Encoding utf8
Write-Host "SBOM: $(@($novos).Count) componentes nativos acrescentados ($($Escopo -join ', '))."
