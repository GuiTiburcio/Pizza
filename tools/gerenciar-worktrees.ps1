# Ferramenta de gerenciamento de worktrees do Pizza
# Convenção: pasta ..\2-paralelo\<nome>, ao lado do repositório.
# Compatibilidade: se ..\worktrees já existir e ..\2-paralelo não, continua usando ..\worktrees.

function New-PizzaWorktree {
    param(
        [Parameter(Mandatory = $true)][string]$Nome,
        [string]$Prefixo = 'feat'
    )
    $repo = (& git rev-parse --show-toplevel).Trim()
    $parent = Split-Path $repo -Parent
    $pasta = if ((Test-Path (Join-Path $parent 'worktrees')) -and -not (Test-Path (Join-Path $parent '2-paralelo'))) { 'worktrees' } else { '2-paralelo' }
    $dest = Join-Path $parent "$pasta\$Nome"
    
    Write-Host "Atualizando origin main..." -ForegroundColor Cyan
    & git -C $repo fetch origin main
    
    Write-Host "Criando worktree em $dest no ramo $Prefixo/$Nome..." -ForegroundColor Green
    & git -C $repo worktree add --no-track -b "$Prefixo/$Nome" $dest origin/main
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
