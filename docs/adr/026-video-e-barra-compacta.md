# ADR-026 — Vídeo e barra compacta

**Status:** decisões de produto aceitas; desenho e implementação pendentes.
**Data:** 11/11/2026.

## Contexto

O mantenedor confirmou o alinhamento iniciado com o Claude: dois motores para vídeo, aba separada e barra compacta. Isso não antecipa conversores à Fase 4.

## Decisão

- Manter C# no residente e na coordenação do Worker. Rust só será reconsiderado com medições que mostrem um problema atribuível à linguagem.
- Na v1.x, usar Media Foundation e FFmpeg. A seleção depende do formato, dos codecs disponíveis e da capacidade validada no Worker isolado. Definir a política de fallback antes do código.
- Separar **Áudio** de **Vídeo**; extrair áudio fica em Vídeo. Mostrar opções relacionadas ao arquivo arrastado.
- Na Fase 4d, preparar uma barra com ícone e nome curto abaixo: Imagem, PDF, Docs, Áudio, Vídeo e futuramente Web. Conferir visualmente antes de implementar. Preservar nomes completos para acessibilidade, foco visível e alvos de pelo menos 44 px. Não ativar conversores antes de sua fase.
- Os nomes dos formatos devem acompanhar a curvatura da fatia, centralizados e legíveis sem girar a cabeça. Preparar prévia evitando texto invertido, palavras verticais ou rotação do nome inteiro nas laterais. Não diminuir a fonte para caber.

## Consequências

A Fase 4 continua aberta. Primeiro corrigir transbordamento e o arco decorativo do miolo; depois conferir a composição curva e a barra compacta com o mantenedor. O desenho da faixa/painel da 4b permanece pendente.

Antes do vídeo: verificar licença da configuração exata do FFmpeg e requisitos de distribuição de codecs/patentes; validar isolamento, aceleração, consumo e cancelamento. Não assumir que codecs instalados no Windows resolvem automaticamente os direitos de distribuição do Pizza. Medir tamanho do pacote e ganhos de GPU.

## Alternativas

Descartadas: aba única Áudio/Vídeo, ícones sem texto na proposta inicial e migração para Rust apenas pela expectativa de acelerar codecs nativos.
