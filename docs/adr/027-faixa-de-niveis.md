# ADR-027 — Faixa de níveis da etapa 4b

**Status:** regras da SPECS e retenção de formato aceitas; integração visual pendente.
**Data:** 11/11/2026.

## Contexto

A etapa 2 do Dossiê foi integrada no PR #43. Ref-Imagem e Ref-PDF mostram a faixa nos dois temas. A implementação atual ainda abre o painel no clique comum; a mudança de interação entra na integração da 4b, não apenas na criação do estado testável.

## Decisão

A faixa conserva o último formato apontado durante o trajeto até seus botões. Outra fatia troca o contexto; trocar de aba/volta ou encerrar o menu limpa o contexto. O mantenedor escolheu a retenção sem prazo em 11/11/2026. O prazo geral de inatividade do menu continua valendo.

Usar somente opções e padrões do catálogo do Worker:

| Formato | Opção principal | Padrão |
| --- | --- | --- |
| JPG, WEBP e AVIF | Qualidade: Máxima, Alta, Equilibrada, Compacta | Equilibrada (75) |
| PNG | Resolução: Original, 75%, 50% | Original |
| Imagem → PDF | Resolução: Original, Escritório, Tela | Original |
| ICO | Tamanho: Grande, Médio, Pequeno | Grande (256) |
| TIFF e GIF | Sem faixa | Conversão direta |

Trocar efetivamente de formato restaura o padrão; reapontar o mesmo conserva a seleção da faixa. Apontar uma fatia sem opções oculta a faixa. Um espaço vazio não é outra fatia e não limpa a faixa.

Conforme SPECS §2.1: clique/drop no nível confirma a opção principal; clique/drop na fatia usa os padrões; botão direito abre as opções completas quando existirem. Demais opções ficam no padrão, inclusive remoção de metadados. A roda sobre a faixa apenas muda o nível, limitada às extremidades; fora dela mantém a navegação por abas/voltas. Mudar o nível não converte por si só. Nenhuma escolha é persistida entre sessões.

Teclado escolhido pelo mantenedor em 11/11/2026: Tab entra no grupo de níveis; setas mudam a prévia; Enter confirma e Esc cancela. O grupo forma uma parada de foco, em vez de exigir Tab em cada nível. Preservar o retorno de foco e a navegação das fatias na integração.

## Integração e aceite pendentes

- Desenhar a faixa com a bolha de vidro das referências, nos dois temas e com transparência desligada; alvos ≥44 px e texto com contraste ≥4,5:1.
- Incluir a faixa no trajeto de drop e no cálculo dos limites da janela; não deixar a camada invisível presa.
- Integrar a navegação por teclado escolhida, preservando também os atalhos existentes aplicáveis, sem hook global. Confirmar que a ação é identificável antes da conversão.
- Testar 100% a 200%, monitores com DPI diferente, mudanças rápidas de formato e callbacks/timers atrasados.
- Conferir a faixa final com o mantenedor antes de declarar 4b concluída.

## Segurança e verificação

TST-01 cobre contexto, padrões, limites e opções aceitas pelo Worker. A lógica não abre arquivos, executa conversores ou grava configurações. A futura integração toca SEC-03/SEC-05/SEC-06: intenção de clique/drop validada, COPY/NONE e eventos locais. SEC-31 e PRIV-16 permanecem: original intacto e nenhum caminho/conteúdo novo em logs.

Alternativa descartada: esconder a faixa após breve intervalo ao deixar a fatia; exigiria velocidade de movimento e prejudicaria o trajeto até os controles.
