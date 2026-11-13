# Estado da prévia e próximas etapas

Esta entrega fecha uma prévia do gesto e da conversão de imagens. Não equivale à conclusão de todos os requisitos da v1.0.

| Área | Estado |
| --- | --- |
| Ctrl + arrastar e menu radial | Implementado |
| Soltar em qualquer posição e escolher por clique | Implementado nesta entrega |
| Atalho de soltar diretamente no formato | Mantido |
| Imagens e Imagem → PDF | Implementados, incluindo AVIF e voltas de formatos |
| Opções, metadados e histórico da sessão | Implementados |
| Worker isolado e proteção do original | Implementados; testes de integração |
| Vidro, temas e movimento reduzido | Implementados no disco; validação manual ampla ainda necessária |
| PDF → outros formatos e operações em PDF | Planejados |
| Documento | Em andamento: Pizza Brotinho (conversões simples) e Pizza Jumbo (motor de documentos embutido e isolado), [ADR-034](adr/034-brotinho-jumbo-motor-de-documentos.md) |
| Áudio | Antecipado da v1.x: só com o Media Foundation do Windows (MP3, WAV, M4A/AAC, FLAC); OGG/Opus e vídeo seguem na v1.x. Decidido no [ADR-035](adr/035-aba-audio-media-foundation.md); vai também no Pizza Brotinho ([ADR-036](adr/036-formatos-por-pacote-e-fatias-incompativeis.md)) |
| Web | Antecipada da v1.x: dados (JSON, XML, CSV), legendas, Markdown/HTML, fontes e compressão, em .NET puro e nos dois pacotes ([ADR-036](adr/036-formatos-por-pacote-e-fatias-incompativeis.md)) |
| Vídeo, em aba própria (Media Foundation e FFmpeg) | Planejado para v1.x ([ADR-026](adr/026-aba-video-e-motores.md)); [estudo da barra compacta](propostas/barra-compacta.svg) para a Fase 4d |
| Lotes | Planejados para v1.x |
| Configurações completas e políticas de TI | Planejadas |
| Prévias 0.x ([ADR-031](adr/031-previa-pelo-ci.md)) | `.zip` do CI com `SHA256SUMS`, SBOM e proveniência, como pré-lançamento |
| Instaladores assinados, atualização verificada, SBOM e proveniência de release | Pendentes antes da distribuição estável |
| Auditoria completa e medições finais de desempenho | Pendentes para a v1.0 |
| Artefato original do Claude Design | [Referência disponível](dossie/README.md) |
| Vídeo de aplicação prática | Planejado pelo mantenedor |

Fases e critérios completos estão nas [SPECS](SPECS.md). Artefatos experimentais do CI são builds de desenvolvimento.

**Fase 4 — Visual Pizza: concluída em 11/11/2026** ([acompanhamento](FASE-4.md)). Prévias 0.1.0 e 0.1.1 publicadas ([ADR-031](adr/031-previa-pelo-ci.md)). **Fase 5 — medir e endurecer** ([acompanhamento](FASE-5.md)): 5a e boa parte da 5b feitas.

**Rota decidida em 12/11/2026:** primeiro as quatro abas convertendo, na ordem Docs → PDF → Áudio; depois a auditoria completa (5c, cobrindo as abas novas) e, por fim, a parte estética (alto contraste e temas).

## Proteção intelectual

Pedido do mantenedor em 12/11/2026. Orientação geral, a confirmar com um agente da propriedade industrial:

| Item | O que é | Prazo |
| --- | --- | --- |
| Marca | Registro no INPI de "Pizza", "Pizza Brotinho" e "Pizza Jumbo" | Quanto antes |
| Registro de programa de computador | Registro no INPI, que comprova autoria e data | Quanto antes |
| Patente | No Brasil, programa de computador em si não é patenteável (Lei 9.279/96, art. 10, V); só invenção com efeito técnico. O código é público desde 09/11/2026, e o período de graça no Brasil e nos EUA é de 12 meses. A GPL-3.0 (§11) já concede licença de patente a quem recebe o Pizza | Avaliar antes de 09/11/2027 |

## Ideias para depois da v1.x

Pedidos do mantenedor, anotados em 10/11/2026. Cada um passa por alinhamento e ADR antes do código.

| Ideia | O que é |
| --- | --- |
| Régua de porcentagem | No painel de opções, um controle deslizante (slider) no lugar dos botões fixos de 75% e 50%. Antecipada para a Fase 4d em 11/11/2026 ([ADR-030](adr/030-regua-de-resolucao.md)) |
| Interação por voz | Uso guiado por voz, para incluir pessoas com deficiência visual |
| Dicas de uso | Um tutorial curto na primeira vez; as dicas continuam enquanto o usuário quiser, e uma opção desliga |
| Site no GitHub Pages | Layout "cartoonizado", com uma animação simples mostrando como o Pizza funciona. Segue o "efeito halo": primeira impressão premium em poucos segundos, mais visual que texto, poucos cards, caminho claro de por onde começar. Consentimento de cookies (LGPD) só se o site usar cookie ou rastreio não essencial; se a pessoa negar, nada é gravado. Pedido em 11/11/2026 |
| Melhorar imagens | Uma opção que melhora uma imagem borrada ou de baixa qualidade: nitidez, menos ruído e mais resolução. Tudo no próprio PC, dentro do Worker isolado e sem rede (SEC-20, SEC-50); exige estudo de motor e de licença antes. Pedido em 11/11/2026 |
| Bolha com borda distorcida | Nas extremidades, a bolha de vidro (ADR-027) distorce levemente o que está atrás, como uma lente de verdade. Pedido em 11/11/2026 |
| Paletas e cores do sistema | Opção "Usar cores do sistema" (segue as paletas personalizadas do Windows) e paletas claras Papel Sépia, Pêssego e Bordô, Chá Verde e Oliva, Areia Solar; prancha 5B-3; código com as Configurações (Fase 8). Pedido em 12/11/2026 |

Decisões com fase marcada (faixa de níveis na 4b, barra de abas mais fina na 4d, temas do vidro e frase no Sobre na Fase 8) estão na SPECS §2.1.
