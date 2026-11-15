# Revisão do fluxo unificado do Pizza

Decisões do mantenedor em 14/11/2026, após validar a orientação do candidato c1a492c. Este documento registra o contrato solicitado e as lacunas encontradas por inspeção do código e dos PRs. Não declara implementação de lote, confirmação ou auditoria integral.

## Experiência solicitada

1. Selecionar um ou vários arquivos no Explorer e fazer um único Ctrl + arraste.
2. Manter as quatro abas. As funções incompatíveis continuam visíveis para descoberta, esmaecidas, sem iniciar conversão. A orientação sobre o arquivo esperado aparece imediatamente.
3. Escolher formato e opções e confirmar antes de iniciar. Voltar deve reabrir a escolha com os arquivos preservados, sem repetir o gesto. Conversões sem opções também precisam de confirmação.
4. Com arquivos misturados, agrupar por tipo, escolher uma saída por grupo e revisar todos os grupos antes de iniciar. Nenhum arquivo é descartado silenciosamente. Arquivos sem conversor devem aparecer como não suportados, sem promessa de conversão.
5. Executar somente o plano confirmado, preservando os originais. Voltar antes de iniciar não cria saída. Cancelar depois de iniciar é uma operação diferente e não implica apagar resultados já concluídos.

"Tradução" neste pedido significa conversão de formatos, não tradução de idioma. O agrupamento deve respeitar as entradas aceitas por cada conversor: compartilhar uma aba não garante que DOCX e XLSX aceitem a mesma saída. Não escolher automaticamente um destino que descarte conteúdo ou mude a qualidade.

## Onde o código e o trabalho do Claude estão

| Frente | Evidência consultada | Lacuna |
| --- | --- | --- |
| Menu integrado | PR #93, base #82 e #87, candidato c1a492c; mantenedor confirmou a orientação | Ajustar descoberta das fatias conforme decisão posterior. A confirmação e a volta ainda não existem nesse candidato. |
| Documento | Checkout do Claude em feat/docs-5-markdown, 5aae945; PR #92 aberto | DOCX → Markdown com imagens muda o protocolo de saída para PZB2. Revisar código, limites e rollback antes de integrar; CI não aprovado na consulta desta rodada. |
| Áudio | PR #84 em rascunho e roteiro do Brotinho | ALAC ainda acrescenta amostras; fallback de bitrate precisa de consentimento. São bloqueios de fidelidade, não ajustes visuais. |
| Segurança | PR #90 integrado | Limites de saída, ADS e CSV corrigidos e testados. O scan anterior teve cobertura parcial e não fecha a auditoria integral. |
| Distribuição | PR #91 integrado | Canais, assinatura e atualização verificada têm roteiro; não estão entregues como lançamento estável. |

A conferência do Claude usa checkout, commits e PRs, não acesso à conversa completa do aplicativo. Não alterar o checkout dele nem incorporar Markdown junto com uma mudança de menu sem revisão específica.

## Lacunas concretas do fluxo atual

- `Overlay.Drag.Select` encerra a sessão e pode encaminhar diretamente a `Notifications.Convert`. Clique esquerdo e seleção de nível não oferecem confirmação geral. O painel completo hoje é acessado pelo botão direito.
- `Overlay.OnPanelClosed(null)` limpa a seleção e esconde o menu. Trocar apenas o texto "Cancelar" por "Voltar" não resolve: é necessário preservar/restaurar arquivos, aba e opções.
- `OptionsPanel.Show` cria o estado com os padrões do catálogo. A confirmação deverá receber as opções já escolhidas na faixa, inclusive volume, em vez de reiniciá-las silenciosamente.
- `ConverterInfo.Accepts` aceita vários arquivos somente para Juntar PDFs. `Overlay.Usable` e a escolha inicial usam o primeiro arquivo; um lote não pode depender da ordem dos itens para abrir ou detectar um arquivo suportado.
- `Conversion.Start` usa tarefas com uma trava global. Disparar uma tarefa por arquivo não constitui uma fila de lote adequada: faltam planejamento, limite de tarefas pendentes, progresso e cancelamento do conjunto.
- O teste obrigatório do motor Jumbo `Macro_ao_abrir_nao_executa` apresentou `WORKER_CRASHED` e passou em repetição sem mudança de código. A causa permanece em investigação; não remover nem relaxar o teste.

## Ordem de implementação e aceite

1. **Descoberta das fatias:** manter as quatro abas, orientação imediata e funções incompatíveis bloqueadas. Verificar TST-01, navegação de páginas, clique/drop/teclado e validação visual nos dois temas.
2. **Confirmação e volta com um arquivo:** reaproveitar o painel, preservar as opções escolhidas e restaurar a sessão ao voltar. Testar que nenhum Worker de conversão/arquivo de saída é iniciado antes de confirmar; o probe de áudio continua isolado e deve ser descartado ao voltar. Testar também conversores sem opções, Enter, Esc e fechamento da janela. A decisão substitui FR-20/FR-21 de conversão imediata quando esta etapa for integrada.
3. **Planejamento de lote misto:** construir primeiro o plano testável de grupos e atribuições, com todos os arquivos contabilizados. Separar incompatibilidades dentro de uma mesma aba, não confundir Juntar PDFs com converter PDFs individualmente, conservar ordem e impedir duplicação de execução. Validar o plano antes de ligar a execução.
4. **Revisão e fila de execução:** apresentar destinos/opções por grupo e resumo final; permitir voltar sem perder os demais grupos. Implementar fila limitada, estados por arquivo, cancelamento e falhas parciais explícitas. Preservar SEC-05/06, SEC-15/20, SEC-31, SEC-40 e PRIV-16; manter os limites de saída por conversão. A política de lote deve ser registrada nas SPECS antes desta execução, substituindo a restrição temporária de FR-30.
5. **Fidelidade e lançamento:** fechar ALAC e consentimento de bitrate, revisar #92 separadamente, executar a matriz das quatro abas num único candidato e então validar distribuição. Não chamar um lote de pronto porque o menu aceitou vários arquivos.

Não foram alterados neste ajuste o protocolo IPC, o Worker, os codecs nem os limites de recursos. O próximo portão é a confirmação com volta em um arquivo; o lote depende desse mesmo estado de revisão para manter comportamento coerente nas quatro abas.
