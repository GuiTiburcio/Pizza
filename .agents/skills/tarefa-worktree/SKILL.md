---
name: tarefa-worktree
description: >-
  Contrato de trabalho nos worktrees do Pizza (a pasta de worktrees ao lado do repositório, `..\2-paralelo\<nome>`). Use ao
  escrever um TAREFA.md, ao executar uma tarefa recebida num worktree ou ao
  entregar o RESULTADO.md.
---

# Contrato TAREFA.md → RESULTADO.md

Vale para o Antigravity e para os subagentes do Claude. As regras de [AGENTS.md](../../../AGENTS.md) e [CLAUDE.md](../../../CLAUDE.md) continuam valendo.

## Antes de começar

1. Rode `git worktree list`. Se o worktree estiver `locked` por outra sessão, pare e avise.
2. Leia o `TAREFA.md` na raiz do worktree. Sem `TAREFA.md`, não implemente nada.
3. Rode `git fetch origin` e `git rebase origin/main`. Se houver conflito, pare: rode `git rebase --abort` e relate os arquivos em conflito.

## Modelo do TAREFA.md

```markdown
# TAREFA — <nome do worktree>

- **Ramo:** <ramo>   **PR:** #<n ou "nenhum">   **ADR/SPECS:** <ADR-xxx, §y>
- **Objetivo:** <uma ou duas frases>

## O que falta
1. <etapa> → verificar: <teste TST-xxx ou checagem>

## Requisitos tocados
- SEC-/PRIV-: <lista ou "nenhum">

## Arquivos prováveis
- <caminhos>

## Fora de escopo
- <o que não mexer, inclusive os arquivos compartilhados: ConverterCatalog, RadialMenu, TabCarousel, QuickOptionsState, Overlay, ConversionRunner, OutputNaming, ConversionMessages>

## Critério de pronto
- Build e testes verdes com saída curta (`.agents/rules/build-offline.md`).
- <critério específico>
```

## Execução

1. Escreva primeiro o teste `TST-` ligado ao requisito e veja ele falhar.
2. Implemente o mínimo para o teste passar (CLAUDE.md §2 e §3).
3. Rode build e testes com saída curta.
4. Faça commit no ramo do worktree com o autor `Guilherme Tiburcio <54161354+GuiTiburcio@users.noreply.github.com>`. Subagentes do Claude acrescentam a linha `Co-Authored-By` da plataforma.
5. Rode `git push` do ramo. **Não** faça merge na `main` e **não** edite fora do worktree.
6. Tela nova não fica "pronta": fica "aguardando validação do mantenedor no app".
7. **Seiso** (limpeza): Antes do Seiso, guarde as evidências duráveis: o conteúdo do `RESULTADO.md` vai para a descrição do PR ou para as notas do mantenedor, porque `TAREFA.md` e `RESULTADO.md` são ignorados pelo Git e somem com o worktree. Só então rode o Seiso e verifique antes de remover:

   - commits que só existem neste ramo: `git log origin/main..HEAD` (tudo precisa estar no PR ou integrado);
   - alterações não commitadas: `git status`;
   - arquivos ignorados que ainda sejam úteis (`git status --ignored`);
   - processos abertos que usem a pasta (terminal, app, editor).

   Depois do merge, remova o worktree (`git worktree remove`) e o ramo local, e confira que `bin/`, `obj/` e saídas de build não ficaram fora do `.gitignore`. Proibido: `--force` e remover worktree ativo ou `locked`.

## Modelo do RESULTADO.md

```markdown
# RESULTADO — <nome do worktree>

- **Estado:** concluído | parcial | bloqueado
- **Commits:** <hash curto + assunto>
- **Verificado:** <comando → resultado, ex.: "dotnet test: 412 aprovados, 0 falhas">
- **Não verificado:** <o que ficou de fora e por quê>
- **Validação do mantenedor:** <o que o Gui precisa ver no app>
- **Riscos e dúvidas:** <lista curta>
```

`TAREFA.md` e `RESULTADO.md` não vão para commit (estão no `.gitignore`).
