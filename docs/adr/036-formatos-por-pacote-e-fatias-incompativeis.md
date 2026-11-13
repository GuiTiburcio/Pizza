# ADR-036 — Formatos por pacote e fatias incompatíveis

- **Status:** Aceita pelo mantenedor em 13/11/2026. Complementa o ADR-034 (Brotinho e Jumbo) e substitui o pedido de 13/11/2026 de mostrar todas as fatias desabilitadas. Plano completo em [`propostas/plano-formatos-pacotes.md`](../propostas/plano-formatos-pacotes.md).
- **Data:** 2026-11-13

## Contexto

O mantenedor trouxe uma lista ampla de tipos de arquivo (imagem, PDF, documentos, planilhas, apresentações, e-books, áudio, vídeo, web e dados, legendas, fontes, compactados, 3D) e pediu para separar o que cabe em cada pacote e dividir o trabalho entre as sessões paralelas.

Dois problemas apareceram:

- Com os formatos novos, a aba Docs passaria de 15 fatias. Mostrando tudo desabilitado, um DOCX abriria um menu quase todo apagado.
- O pedido anterior (Áudio só no Jumbo) partia da ideia de que mais conversões deixam o Brotinho pesado. O Áudio usa o Media Foundation do Windows e não acrescenta nenhum byte ao pacote.

## Decisão

**Pacotes**

- **Brotinho:** tudo o que o Pizza faz com o próprio código, o libvips, o QPDF, o PDFium e o Media Foundation: Imagem, PDF (menos PDF → DOCX), Docs simples em .NET, Áudio e a aba Web (dados, legendas, Markdown/HTML, fontes, compressão).
- **Jumbo:** tudo do Brotinho mais o que precisa do motor de documentos (LibreOffice): formatos de escritório entre si e para PDF, PPTX → PNG, MD/EPUB → PDF e PDF → DOCX. O Vídeo (FFmpeg) entra só no Jumbo, na v1.x.
- O que é do Jumbo continua marcado no catálogo pelo `DocumentEngine` que já existe. No Brotinho essas fatias **não aparecem**. Não há campo novo de pacote: hoje o motor de documentos é a única diferença entre os dois, e o FFmpeg ganha a sua marca quando entrar.

**Menu**

- **Aba do arquivo:** numa aba com alguma conversão para o arquivo arrastado, aparecem só as fatias que servem a ele, em voltas de seis (ADR-024). A aba Docs continua única.
- **Outras abas:** continuam no scroll. Suas fatias aparecem apagadas. Escolher uma delas, clicando ou soltando, não converte: aparece um aviso curto sobre a fatia, "Não converte para JPG. Experimente a aba PDF.", que nomeia a primeira aba com conversão para o arquivo. O menu continua aberto para outra escolha.
- **Personalizar a pizza (Fase 8):** em Configurações, nos dois pacotes, ligar e desligar abas e fatias e mudar a ordem. Padrão: tudo ligado.

**Fora do Pizza:** compactados (não convertem conteúdo), 3D e CAD, PSD/AI/CDR, OCR, arquivos com DRM, PAGES/KEY/NUMBERS e raster → vetor. Motivos em [`ideias-descartadas`](../ideias-descartadas/README.md).

**Em estudo:** YAML → JSON (leitor de YAML próprio, aliases em cascata são vetor de bomba), minificar JS (minificador próprio é arriscado; biblioteca exige justificativa e SBOM) e JPEG XL (depende do libvips empacotado).

## Consequências

- O Brotinho ganha Áudio e a aba Web sem crescer de forma relevante.
- Cada leitor novo em .NET puro é código nosso diante de arquivo hostil: entra no Worker em AppContainer (SEC-20), com XML sem DTD, teto de bytes reais (SEC-40), TST-03 e TST-08.
- O aviso de fatia incompatível é texto novo na interface e passa pela validação do mantenedor no app.
- O trabalho segue em paralelo: Pizza — IV (Áudio), V (Docs), VI (PDF) e VII (Web, dados e legendas). Só esta etapa mexe no comportamento do menu; as demais acrescentam entradas no catálogo.

## Alternativas descartadas

- **Abas separadas para Texto, Planilha e Apresentação:** mais abas na barra, que cabe seis; o menu já abre na aba certa e filtra as fatias.
- **Sub-abas dentro de Docs:** mais um gesto e desenho novo no disco.
- **Mostrar no Brotinho as fatias do Jumbo apagadas, com "Disponível no Pizza Jumbo":** poluiria o menu do pacote leve.
- **Áudio só no Jumbo:** não economiza nada, só tira recurso do Brotinho.
- **Campo `Package` no catálogo:** duplicaria o `DocumentEngine` enquanto ele for a única diferença.
