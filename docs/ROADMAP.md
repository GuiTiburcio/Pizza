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
| Vídeo, em aba própria (Media Foundation e FFmpeg) | Planejado para v1.x ([ADR-026](adr/026-aba-video-e-motores.md)) |
| Lotes | Planejados para v1.x |
| Configurações completas e políticas de TI | Planejadas |
| Instaladores assinados, atualização verificada, SBOM e proveniência de release | Pendentes antes da distribuição estável |
| Auditoria completa e medições finais de desempenho | Pendentes para a v1.0 |
| Artefato original do Claude Design | [Referência disponível](dossie/README.md) |
| Vídeo de aplicação prática | Planejado pelo mantenedor |

Fases e critérios completos estão nas [SPECS](SPECS.md). Artefatos experimentais do CI são builds de desenvolvimento.

**Fase atual: 4 — Visual Pizza, ainda aberta.** O [acompanhamento por etapas](FASE-4.md) separa implementação, verificação automática e aceite visual, para manter Claude, Codex e mantenedor no mesmo ponto do plano. A Fase 5 só começa após o aceite da Fase 4.

## Ideias para depois da v1.x

Pedidos do mantenedor, anotados em 10/11/2026. Cada um passa por alinhamento e ADR antes do código.

| Ideia | O que é |
| --- | --- |
| Régua de porcentagem | No painel de opções, um controle deslizante (slider) no lugar dos botões fixos de 75% e 50% |
| Interação por voz | Uso guiado por voz, para incluir pessoas com deficiência visual |
| Dicas de uso | Um tutorial curto na primeira vez; as dicas continuam enquanto o usuário quiser, e uma opção desliga |

Decisões com fase marcada (faixa de níveis na 4b, barra de abas mais fina na 4d, temas do vidro e frase no Sobre na Fase 8) estão na SPECS §2.1.
