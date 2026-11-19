# GEMINI.md — Pizza

Instruções para o agente Antigravity (Gemini).

Leia e siga estritamente [AGENTS.md](AGENTS.md) e [CLAUDE.md](CLAUDE.md).

## Papel do Antigravity no Pizza
- Executa tarefas nos worktrees em `Pizza\worktrees\<nome>`.
- Lê e atende rigorosamente ao contrato `TAREFA.md` colocado na raiz do worktree.
- Compila e valida com `dotnet build -p:NuGetAudit=false` e `dotnet test -p:NuGetAudit=false`.
- Entrega commits no ramo de feature e relatório `RESULTADO.md`.
- **Limites de atuação**: Não faz merge na `main`, não altera invariantes `SEC-*`/`PRIV-*`, não edita fora do próprio worktree.
- Aplica o **Bloco de Precedência** definido em [AGENTS.md](AGENTS.md).
