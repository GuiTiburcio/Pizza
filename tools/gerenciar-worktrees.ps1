# Ferramenta de gerenciamento de worktrees do Pizza
# Convenção: pasta ..\ramos\<faixa>\<tarefa>, ao lado do repositório, e ramo <faixa>/<tarefa>.
# Faixas: base, jumbo, projeto, revisao.
# Compatibilidade: se ..\worktrees já existir e ..\ramos não, usa ..\worktrees\<tarefa>.

function New-PizzaWorktree {
    param(
        [Parameter(Mandatory = $true)][ValidateSet('base', 'jumbo', 'projeto', 'revisao')][string]$Faixa,
        [Parameter(Mandatory = $true)][string]$Tarefa
    )
    $repo = (& git rev-parse --show-toplevel).Trim()
    $parent = Split-Path $repo -Parent
    $legado = (Test-Path (Join-Path $parent 'worktrees')) -and -not (Test-Path (Join-Path $parent 'ramos'))
    $dest = if ($legado) { Join-Path $parent "worktrees\$Tarefa" } else { Join-Path $parent "ramos\$Faixa\$Tarefa" }
    $ramo = "$Faixa/$Tarefa"

    Write-Host "Atualizando origin main..." -ForegroundColor Cyan
    & git -C $repo fetch origin main

    Write-Host "Criando worktree em $dest no ramo $ramo..." -ForegroundColor Green
    & git -C $repo worktree add --no-track -b $ramo $dest origin/main
}
function Remove-PizzaWorktree {
    param(
        [Parameter(Mandatory = $true)][string]$Caminho
    )
    $repo = (& git rev-parse --show-toplevel).Trim()
    Write-Host "Removendo worktree $Caminho..." -ForegroundColor Yellow
    & git -C $repo worktree remove $Caminho
    & git -C $repo worktree prune
}

function Test-PizzaWorktrees {
    $repo = (& git rev-parse --show-toplevel).Trim()
    Write-Host "Estado dos worktrees:" -ForegroundColor Cyan
    & git -C $repo worktree list
}

Export-ModuleMember -Function New-PizzaWorktree, Remove-PizzaWorktree, Test-PizzaWorktrees -ErrorAction SilentlyContinue
