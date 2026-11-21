# Validação, governança e continuidade do Pizza

## Mandato e limites

Em 21/11/2026, o mantenedor autorizou o Codex a selecionar autonomamente as skills adequadas ao sistema e ao planejamento e a adiantar trabalhos que o Claude/Antigravity deixaram inconclusivos. O objetivo é uma validação minuciosa e crítica, colaborativa na estética e exigente na segurança, qualidade, rastreabilidade e governança de TI.

Essa autorização dispensa perguntas sobre qual skill usar. Não substitui evidências, aceite visual do mantenedor, decisões de produto ainda abertas ou aprovações exigidas para enfraquecer controles. Não autoriza gastos, novas contas, uso de credenciais descobertas, divulgação de vulnerabilidades ou envio de arquivos pessoais. As invariantes do CLAUDE.md continuam obrigatórias.

## Seleção de skills e práticas

Selecionar pela finalidade e pelo resultado esperado, não pelo nome de uma escola de gestão. Ler a versão disponível da skill e respeitar seu escopo. Não simular comandos que não existem no ambiente nem instalar ferramentas só para preencher esta tabela.

| Situação | Skill ou prática apropriada | Resultado verificável |
| --- | --- | --- |
| Implementar ou corrigir comportamento comum | `karpathy-guidelines` | Causa demonstrada, alteração mínima e regressão pertinente |
| Dividir ou retomar uma tarefa em worktree | `tarefa-worktree` | TAREFA.md antes da execução e RESULTADO.md com evidências e limites |
| Auditar uma revisão completa do repositório | `codex-security:security-scan` | Scan ligado ao alvo exato, cobertura real, revisão independente e relatório finalizado |
| Auditar mudanças de PR, commit ou patch | `codex-security:security-diff-scan` | Fonte e caminhos afetados examinados na revisão identificada |
| Avaliar achado importado | `codex-security:triage-finding` | Aplicabilidade e controles reais documentados |
| Corrigir vulnerabilidade validada e autorizada | `codex-security:fix-finding` | Correção delimitada e testes do controle afetado |
| Verificar explicitamente uma correção de segurança | `codex-security:verify-fix` | Evidência de remediação e incertezas restantes |
| Examinar alerta de dependência | `codex-security:resolve-dependency-alert` | Origem transitiva, versão, alcance e alternativa de atualização |
| Propor mudança estrutural de segurança | `codex-security:propose-security-hardening` | Alternativas, impactos, custo e plano de implantação |
| Validar interface e operação real | `computer-use:computer-use` | Cenário observado no aplicativo; falhas de ferramenta permanecem como não testadas |
| Decidir aparência | Dossiê, desenho de produção e avaliação do mantenedor | Proposta distinguida de implementação e de aceite |

Não usar Deep Scan como sinônimo de auditoria completa: a seleção depende do pedido e do fluxo vigente. Não reaproveitar um scan de outra cópia/revisão como validação da versão atual. Skills disponíveis em uma sessão podem mudar; a tabela orienta a seleção, não promete disponibilidade permanente.

## Um fluxo, responsabilidades explícitas

Cada item percorre necessidade, requisito, risco, responsável, execução, evidência, revisão e aceite. Uma tarefa tem um responsável por vez. Revisores independentes não implementam simultaneamente nos mesmos arquivos. O quadro distingue: a esclarecer, pronto para executar, em execução, em revisão, aguardando mantenedor, bloqueado e concluído.

Antes de assumir uma pendência:

1. Conferir estado atual, revisão, worktrees, bloqueios, contrato e resultados existentes.
2. Verificar se a pendência já foi integrada, adiada ou substituída por decisão posterior.
3. Criar frente exclusiva e contrato com objetivo, requisitos SEC-/PRIV-/TST-, arquivos, exclusões e critério de pronto.
4. Reproduzir o problema ou registrar a evidência disponível. Separar fatos, relatos e hipóteses.
5. Implementar o mínimo, verificar e entregar resultado com o que passou, falhou e não foi testado.

Dividir por fronteiras independentes: segurança, fidelidade de conversão, interface/acessibilidade, distribuição e documentação. Arquivos compartilhados e recursos globais exigem coordenação prévia. Enquanto não houver isolamento comprovado, executar somente uma suíte de integração por vez no PC quando ela usar os mesmos perfis AppContainer ou letras de unidade.

O limite de trabalho simultâneo deve refletir a capacidade de revisar e validar, não apenas a capacidade de gerar código. Tarefas bloqueadas continuam visíveis; não contam como concluídas.

## Segurança e decisões de liberação

Ao encontrar um possível problema de segurança, avisar o mantenedor com: evidência e revisão; controle SEC-/PRIV- afetado; atacante e pré-requisitos plausíveis; impacto; confiança; contenção possível; correção recomendada e teste de regressão. Não apresentar suspeita como vulnerabilidade confirmada. Não copiar segredos nem publicar achados em issue pública.

Perda ou alteração do original, execução indevida, acesso fora do isolamento e exposição de conteúdo bloqueiam a liberação do candidato afetado. Em caso de risco plausível ainda não delimitado, suspender os testes desse caminho e continuar frentes independentes. Alterações que enfraqueçam invariantes exigem ADR e decisão explícita antes do código.

Um teste de macro que termina em WORKER_CRASHED não comprova que a macro foi bloqueada. CI verde em uma revisão não aprova automaticamente outra. Aprovações antigas, testes isolados e reexecuções devem ser identificados como tais; não ocultar falhas intermitentes nem retirar casos para obter um resultado verde.

## Excelência operacional aplicada

| Referência | Aplicação no Pizza |
| --- | --- |
| Lean | Retirar duplicação e espera sem valor; reduzir tamanho de lote e retrabalho |
| Six Sigma | Usar dados e análise de causa para defeitos recorrentes; aplicar DMAIC quando houver problema e medição suficientes |
| ITIL | Separar incidente, problema, mudança, versão, configuração, suporte e recuperação |
| Kanban | Tornar estado, dependências, bloqueios e capacidade visíveis |
| PDCA/Kaizen | Testar uma melhoria delimitada, medir e incorporar ou rever o resultado |
| PMBOK | Explicitar escopo, responsáveis, riscos, dependências e critérios de aceite |
| 5S | Manter ambientes, ativos, documentação e evidências identificados e organizados |
| DevOps | Automatizar verificações e tornar build, distribuição, diagnóstico e recuperação reproduzíveis |

Medir inicialmente tempo de entrega, idade de bloqueios, retrabalho, recorrência de defeitos e critérios de aceite com evidência. Definir a unidade, denominador e janela de cada indicador antes de estabelecer metas. Não tratar quantidade de commits, testes ou agentes como medida isolada de qualidade. Não acrescentar telemetria ao produto para alimentar esse acompanhamento.

## Triagem inicial em 21/11/2026

Base local observada: `7ac527ca3b8b89f068e57e5397c1a041d3afb21f`. Esta é uma triagem documental e de estado, não uma auditoria completa nem validação manual do aplicativo.

| Frente | Evidência observada | Próxima verificação |
| --- | --- | --- |
| TST-28 | O scan anterior pertence à cópia/revisão de 16/11 e não foi concluído; revisores interrompidos por limite de uso | Auditar o candidato atual, com cobertura real e revisão independente disponível; preservar o scan histórico |
| Áudio/ALAC | RESULTADO.md de fix-docs-crash registra falhas ALAC no run 44775908564; existe ramo fix/alac-101-clean | Conferir commits, estado remoto e resultados atuais antes de assumir ou repetir trabalho; não presumir que o nome do ramo é número de PR |
| Docs/Jumbo | RESULTADO.md registra causa não encontrada e possível interferência entre perfis/letras de worktrees | Reproduzir isoladamente e melhorar diagnóstico sem registrar dados pessoais; não reduzir a proteção de macros |
| Desfazer | PR #97 integrado; descrição registra validação real da Lixeira pendente e operação de várias partes não transacional | Validar somente com arquivos sintéticos: preservação do original, saída alterada, várias partes, restauração e eventual aviso de exclusão permanente |
| Acessibilidade e estética | PR #105 é proposta de vidro líquido, sem implementação; contém relato de problemas de contraste | Verificar o contraste atual separadamente da aprovação estética; manter a proposta visual aguardando o mantenedor |
| Web | Worktree bloqueado e ADR-039 com adendo de 20/11 adiam a aba para v1.x | Preservar a frente; não reabrir como requisito do lançamento de quatro abas |
| Distribuição e inventário | Main inclui #106, que acrescenta DLLs nativas ao SBOM | Verificar o artefato e a cobertura efetiva das dependências; integração do PR não equivale à aprovação de assinatura/atualização |

## Evidência de conclusão

Cada entrega registra revisão/artefato, ambiente, cenário, esperado, observado, integridade das origens, resultado e limitações. Classificações: bloqueio, correção necessária, melhoria recomendada e preferência estética. A matriz manual só recebe aprovação para os cenários realmente executados. O aceite de lançamento continua nos critérios de [Brotinho](BROTINHO-LANCAMENTO.md) e nas [SPECS](SPECS.md).
