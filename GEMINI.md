# GEMINI.md — Pizza

Instruções para o agente Antigravity (Gemini).

Leia e siga estritamente [AGENTS.md](AGENTS.md) e [CLAUDE.md](CLAUDE.md).

## Papel do Antigravity no Pizza
- Executa tarefas nos worktrees na pasta de worktrees ao lado do repositório (`Pizza\2-ramos\<faixa>\<tarefa>` (faixas `base`, `jumbo`, `projeto` e `revisao`; ramo `<faixa>/<tarefa>`)).
- Lê e atende rigorosamente ao contrato `TAREFA.md` colocado na raiz do worktree.
- Compila e valida com `dotnet build -p:NuGetAudit=false` e `dotnet test -p:NuGetAudit=false`.
- Entrega commits no ramo de feature e relatório `RESULTADO.md`.
- **Limites de atuação**: Não faz merge na `main`, não altera invariantes `SEC-*`/`PRIV-*`, não edita fora do próprio worktree.
- Aplica o **Bloco de Precedência** definido em [AGENTS.md](AGENTS.md).
- **Worktree ocupado**: um worktree pode estar em uso por um subagente do Claude. Antes de começar, rode `git worktree list` e não use worktree marcado como `locked` nem worktree que já tenha um `TAREFA.md` de outro agente.
- Regras curtas em `.agents/rules/` e o contrato `TAREFA.md` → `RESULTADO.md` na skill `.agents/skills/tarefa-worktree/`.
