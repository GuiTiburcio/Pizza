# Instruções para agentes — Pizza

Leia e siga [CLAUDE.md](CLAUDE.md) antes de alterar qualquer parte do projeto. As regras de segurança, testes e rastreabilidade continuam valendo em todos os diretórios.

## Bloco de Precedência de Regras e Skills

1. Aviso de segurança e ação irreversível vencem o `caveman`: escrever em frases completas.
2. Texto persistido fora do chat (commits, PRs, issues, `walkthrough.md`, `TAREFA.md`, docs) usa prosa normal, sem caveman.
3. O `caveman` comprime o estilo, não o idioma: respostas em português. Código, comandos, caminhos, números e erros ficam literais. Nada de modo ultra.
4. Perguntas ao mantenedor: frases completas, **uma por vez**, opção recomendada primeiro. Isso é um override local da `/grill-me`, que por padrão pergunta em rodadas.
5. `/plan` exige diálogo e leitura do código antes do artefato de aprovação. Nenhuma alteração antes da aprovação.
6. `/goal` só inicia com critérios verificáveis. Nunca encerra sem a auditoria de conclusão com evidências.
7. `SEC-*` e `PRIV-*` são entrada da reconnaissance da auditoria, não apenas lista de referência.
8. Alterações cirúrgicas, código mínimo, testes antes da implementação (`TST-*`), sem complexidade especulativa.

## Papéis dos Agentes

- **Claude Code (principal)**: Orquestra, revisa e aplica. Roda `/grill-me`, `/plan`, `/goal`; revisa diffs dos worktrees; faz merge na `main`; roda a auditoria de segurança. Não executa trabalho longo dentro dos worktrees do Antigravity.
- **Subagentes do Claude (exceção autorizada pelo mantenedor em 20/11/2026)**: Quando o Claude Code principal os dispara com um `TAREFA.md`, podem implementar na pasta de worktrees ao lado do repositório (`..\ramos\<faixa>\<tarefa>` (faixas `base`, `jumbo`, `projeto` e `revisao`; ramo `<faixa>/<tarefa>`)), vários em paralelo, um por worktree. Valem os mesmos limites do Antigravity: não fazem merge na `main`, não alteram invariantes `SEC-*`/`PRIV-*` e não editam fora do próprio worktree. Entregam commits no ramo e um `RESULTADO.md`. O contrato está na skill `tarefa-worktree`.
- **Antigravity**: Executa nos worktrees. Cria e mantém worktrees na pasta de worktrees ao lado do repositório (`..\ramos\<faixa>\<tarefa>` (faixas `base`, `jumbo`, `projeto` e `revisao`; ramo `<faixa>/<tarefa>`)); compila, testa e roda o app para validar; implementa a tarefa recebida. Não faz merge na `main`, não altera invariantes `SEC-*`/`PRIV-*`, não edita fora do próprio worktree.

## Comunicação

A pedido do mantenedor, use a skill [caveman](.agents/skills/caveman/SKILL.md) nas respostas sobre este projeto, respeitando estritamente o Bloco de Precedência acima. Responda em português, com concisão e clareza. Preserve números, unidades, comandos, erros, ressalvas e informações de segurança.

A skill muda a comunicação no chat. Ela não autoriza encurtar código, comentários, documentação, testes, mensagens da interface, commits ou PRs. Perguntas e opções devem ser frases completas. As instruções da plataforma e as regras de segurança prevalecem.

Continue o trabalho autorizado entre etapas independentes. Informe o resultado verificável, o teste disponível para o mantenedor e o próximo passo. Não declare validação manual, auditoria ou fase concluída sem evidência.

## Organização (5S)

- 5S: cada arquivo novo vai na pasta da cascata correspondente; nada gerado fica fora do .gitignore.
