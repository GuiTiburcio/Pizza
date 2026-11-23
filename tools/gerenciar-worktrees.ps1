# Ferramenta de gerenciamento de worktrees do Pizza
# Convenção: Pizza\2-ramos\<faixa>\<tarefa>, ao lado de Pizza\1-main, e ramo <faixa>/<tarefa>.
# Faixas: base, jumbo, projeto, revisao.
# A pasta worktrees segue válida enquanto o checkout principal estiver em MenuConversorPizza.

function Get-PizzaMainCheckout {
    $common = & git rev-parse --path-format=absolute --git-common-dir
    if ($LASTEXITCODE -ne 0 -or -not $common) {
        throw 'Execute a partir de um checkout do Pizza.'
    }
    $gitDir = Get-Item -LiteralPath $common -Force -ErrorAction Stop
    if (-not $gitDir.PSIsContainer -or $gitDir.Name -ne '.git') {
        throw 'O script requer um checkout principal com um diretório .git.'
    }
    $gitDir.Parent.FullName
}

function New-PizzaWorktree {
    param(
        [Parameter(Mandatory = $true)][ValidateSet('base', 'jumbo', 'projeto', 'revisao')][string]$Faixa,
        [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$')][string]$Tarefa
    )
    $repo = Get-PizzaMainCheckout
    $parent = Split-Path $repo -Parent
    $legado = (Split-Path $repo -Leaf) -eq 'MenuConversorPizza' -and
        (Test-Path -LiteralPath (Join-Path $parent 'worktrees') -PathType Container)
    $dest = if ($legado) { Join-Path $parent "worktrees\$Tarefa" } else { Join-Path $parent "2-ramos\$Faixa\$Tarefa" }
    $ramo = "$Faixa/$Tarefa"

    Write-Host "Atualizando origin main..." -ForegroundColor Cyan
    & git -C $repo fetch origin main
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao atualizar origin/main.' }

    Write-Host "Criando worktree em $dest no ramo $ramo..." -ForegroundColor Green
    & git -C $repo worktree add --no-track -b $ramo $dest origin/main
    if ($LASTEXITCODE -ne 0) { throw "Falha ao criar o worktree $ramo." }
}
function Remove-PizzaWorktree {
    param(
        [Parameter(Mandatory = $true)][string]$Caminho
    )
    $repo = Get-PizzaMainCheckout
    $dest = (Resolve-Path -LiteralPath $Caminho -ErrorAction Stop).Path
    Write-Host "Removendo worktree $Caminho..." -ForegroundColor Yellow
    & git -C $repo worktree remove -- $dest
    if ($LASTEXITCODE -ne 0) { throw 'Git recusou remover o worktree.' }
}

function Test-PizzaWorktrees {
    $repo = Get-PizzaMainCheckout
    Write-Host "Estado dos worktrees:" -ForegroundColor Cyan
    & git -C $repo worktree list
}

if ($ExecutionContext.SessionState.Module) {
    Export-ModuleMember -Function New-PizzaWorktree, Remove-PizzaWorktree, Test-PizzaWorktrees
}
