# ADR-026 — Aba Vídeo separada e dois motores de vídeo

- **Status:** Aceita pelo mantenedor em 11/11/2026. Detalha a linha "Áudio/Vídeo (v1.x)" da SPECS §2.1. Consolida também o PR #40, preparado com o Codex.
- **Data:** 2026-11-11

## Contexto

A SPECS previa uma aba única de Áudio/Vídeo para a v1.x. O mantenedor pediu uma aba própria de vídeo, com as principais conversões, e quis saber se o C# continua servindo para algo pesado como vídeo.

## Decisão

- **Duas abas:** Áudio e Vídeo ficam separadas. O menu já abre na aba do tipo do arquivo arrastado, então quem arrasta um vídeo cai direto nos formatos de vídeo. Separadas, cada aba cabe numa volta de seis fatias, ou quase; juntas, passariam de onze formatos. "Extrair áudio" fica na aba Vídeo, porque é o vídeo que se arrasta.
- **Dois motores:** o Media Foundation do Windows cobre os casos comuns (MP4, H.264, extrair áudio, reduzir resolução), com aceleração por placa de vídeo e sem aumentar o instalador. O FFmpeg empacotado cobre o que o Windows não traz (WebM, AV1, MKV). Antes de entrar, o FFmpeg passa pela análise de licença e de patentes que a SPECS já exige para o HEVC, e roda como os demais interpretadores: só dentro do Worker isolado, sem rede (SEC-20). A regra de qual motor atende cada formato, e o que fazer quando um codec não estiver disponível, é definida antes do código. Ter o codec instalado no Windows não resolve, por si só, os direitos de distribuição do Pizza: isso entra na mesma análise.
- **Linguagem:** o C# continua. O trabalho pesado é decodificar e codificar, e isso acontece dentro dos codecs nativos; a linguagem do Pizza só orquestra.
- **Barra de abas:** com Vídeo e Web, serão seis abas. A barra mais fina da Fase 4d, pedida com 55–70% da altura atual, precisa caber nelas. A proposta é ícone com o nome curto embaixo (Imagem, PDF, Docs, Áudio, Vídeo, Web), com o nome completo para o leitor de tela, foco visível e alvos de pelo menos 44 px ([estudo](../propostas/barra-compacta.svg)). O mantenedor escolheu essa versão (B) em 11/11/2026, na prancha Ref-Barra-Abas: 44 px de altura, 81% da atual, porque os 70% pedidos deixariam os alvos abaixo do mínimo de 44 px. Nenhuma aba futura é ativada antes da sua fase.

## Consequências

O instalador cresce com o FFmpeg (estimativa de 80 a 100 MB); o Media Foundation não aumenta nada. Há mais um interpretador de arquivo não confiável a manter atualizado e coberto pelo teste de arquivos corrompidos (TST-03).

## Alternativas descartadas

- Uma aba só de Áudio e Vídeo: abriria com formatos que não servem para o arquivo arrastado e precisaria de duas ou três voltas.
- Só o FFmpeg: instalador maior e licença de H.264/HEVC a resolver até para o caso mais comum.
- Só o Media Foundation: WebM, AV1 e MKV dependeriam do que estivesse instalado na máquina.
- Reescrever em Rust para ganhar velocidade em vídeo: o tempo é gasto nos codecs nativos, iguais nas duas linguagens.
