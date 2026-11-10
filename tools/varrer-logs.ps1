# TST-09 (PRIV-16): reprova qualquer linha de log fora do formato exato. Lista de permissão, não de proibição.
# Nunca imprime o conteúdo reprovado, só o arquivo e a linha: ele pode ser justamente o dado proibido.
param([Parameter(Mandatory)][string]$Pasta)

$eventos = 'app\.started|app\.stopped|menu\.opened|menu\.cancelled|menu\.notfile|menu\.dropped|conversion\.done|conversion\.failed|conversion\.cancelled'
$formato = '^\{"ts":"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}[+-]\d{2}:\d{2}","lvl":"info","evt":"(' + $eventos + ')"\}$'

$arquivos = @(Get-ChildItem -LiteralPath $Pasta -Filter 'pizza-*.log' -File -ErrorAction SilentlyContinue)
if ($arquivos.Count -eq 0) {
    Write-Output 'Nenhum log para varrer.'
    exit 2
}

$reprovadas = 0
$total = 0
foreach ($arquivo in $arquivos) {
    $numero = 0
    foreach ($linha in [IO.File]::ReadLines($arquivo.FullName)) {
        $numero++
        $total++
        if ($linha -cnotmatch $formato) {
            $reprovadas++
            Write-Output "Reprovada: $($arquivo.Name), linha $numero"
        }
    }
}

Write-Output "$total linhas em $($arquivos.Count) arquivos, $reprovadas reprovadas."
if ($reprovadas -gt 0) { exit 1 }
