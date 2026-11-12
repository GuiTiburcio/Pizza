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
| Documento, áudio e web | Planejados |
| Vídeo, em aba própria (Media Foundation e FFmpeg) | Planejado para v1.x ([ADR-026](adr/026-aba-video-e-motores.md)); [estudo da barra compacta](propostas/barra-compacta.svg) para a Fase 4d |
| Lotes | Planejados para v1.x |
| Configurações completas e políticas de TI | Planejadas |
| Prévias 0.x ([ADR-031](adr/031-previa-pelo-ci.md)) | `.zip` do CI com `SHA256SUMS`, SBOM e proveniência, como pré-lançamento |
| Instaladores assinados, atualização verificada, SBOM e proveniência de release | Pendentes antes da distribuição estável |
| Auditoria completa e medições finais de desempenho | Pendentes para a v1.0 |
| Artefato original do Claude Design | [Referência disponível](dossie/README.md) |
| Vídeo de aplicação prática | Planejado pelo mantenedor |

Fases e critérios completos estão nas [SPECS](SPECS.md). Artefatos experimentais do CI são builds de desenvolvimento.

**Fase 4 — Visual Pizza: concluída em 11/11/2026** ([acompanhamento](FASE-4.md)). A seguir, a prévia 0.1.0 ([ADR-031](adr/031-previa-pelo-ci.md)) e a Fase 5.

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

Decisões com fase marcada (faixa de níveis na 4b, barra de abas mais fina na 4d, temas do vidro e frase no Sobre na Fase 8) estão na SPECS §2.1.
