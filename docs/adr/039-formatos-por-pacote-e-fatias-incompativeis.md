# ADR-039 — Formatos por pacote e fatias incompatíveis

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
- **Outras abas:** continuam no scroll. Suas fatias aparecem apagadas. Escolher uma delas, clicando ou soltando, não converte: a descrição da fatia vira "Experimente a aba PDF", com a primeira aba que converte o arquivo, em tinta legível sobre a fatia apagada. O menu continua aberto para outra escolha.
- **Personalizar a pizza (Fase 8):** em Configurações, nos dois pacotes, ligar e desligar abas e fatias e mudar a ordem. Padrão: tudo ligado.

**Fora do Pizza:** compactados (não convertem conteúdo), 3D e CAD, PSD/AI/CDR, OCR, arquivos com DRM, PAGES/KEY/NUMBERS e raster → vetor. Motivos em [`ideias-descartadas`](../ideias-descartadas/README.md).

**Em estudo:** YAML → JSON (leitor de YAML próprio, aliases em cascata são vetor de bomba), minificar JS (minificador próprio é arriscado; biblioteca exige justificativa e SBOM) e JPEG XL (depende do libvips empacotado).

## Consequências

### Adendo de 14/11/2026 — orientação por aba incompatível

O mantenedor mostrou um JPG selecionado na aba PDF com o aviso "Em breve" e escolheu explicitamente manter as quatro abas com orientação sobre o arquivo esperado. Este adendo substitui somente o comportamento das outras abas descrito em **Menu**, acima; as decisões de pacotes e formatos permanecem.

- Mostrar fatias somente quando servem ao arquivo selecionado, mantendo a página da aba mesmo quando não há opções.
- Em Imagem, PDF, Documento e Áudio sem conversão para a entrada, mostrar respectivamente "Use uma imagem", "Use um arquivo PDF", "Use um documento" e "Use um arquivo de áudio".
- Não mostrar fatias incompatíveis apagadas nem exigir um clique nelas para explicar a incompatibilidade. A orientação aparece ao entrar na aba.
- Reservar "Em breve" a uma aba sem conversores no catálogo. O estado do recurso é separado da compatibilidade com o arquivo.
- Mouse, teclado e drop mantêm o menu aberto sem iniciar conversão na página vazia. O tamanho da fonte da orientação permanece 16 px de desenho; o espaço do texto foi ampliado para acomodar a frase de Áudio.

Regressões: JPG em PDF/Documento/Áudio, PDF e DOCX em Imagem; página de cada aba preservada, mensagem correta e nenhuma seleção/drop indevido. A integração com menus móveis usa a mesma geometria visível das fatias para selecionar ou mover o menu. Validação visual no candidato integrado permanece necessária.

- O Brotinho ganha Áudio e a aba Web sem crescer de forma relevante.
- Cada leitor novo em .NET puro é código nosso diante de arquivo hostil: entra no Worker em AppContainer (SEC-20), com XML sem DTD, teto de bytes reais (SEC-40), TST-03 e TST-08.
- O aviso de fatia incompatível é texto novo na interface e passa pela validação do mantenedor no app.
- O trabalho segue em paralelo: Pizza — IV (Áudio), V (Docs), VI (PDF) e VII (Web, dados e legendas). Só esta etapa mexe no comportamento do menu; as demais acrescentam entradas no catálogo.

## Alternativas descartadas

### Revisão do mantenedor em 14/11/2026 — descoberta das funções

Após testar o candidato c1a492c, o mantenedor confirmou que a orientação funciona e pediu manter também as fatias das outras abas, para mostrar o que o Pizza oferece. Esta revisão substitui o adendo anterior somente quanto a esconder essas fatias:

- Na aba sem conversão para o arquivo, exibir as funções realmente instaladas, esmaecidas e sem seleção/conversão.
- Manter nomes e descrições das funções. A explicação sobre a entrada esperada aparece imediatamente no rodapé, sem se sobrepor às fatias nem exigir um clique.
- Na aba compatível, manter a filtragem por arquivo e as páginas de seis fatias. A abertura continua na primeira aba que realmente converte o arquivo, não na primeira aba ilustrada.
- As funções do motor ausente não passam a ser anunciadas como disponíveis. Nenhum conversor novo é habilitado por esta mudança.

O mantenedor também escolheu confirmação antes da conversão, com volta à escolha, e lote misto agrupado por tipo com revisão final. Essas duas mudanças ainda não são implementação entregue; o contrato e a sequência de trabalho estão em [FLUXO-UNIFICADO.md](../FLUXO-UNIFICADO.md).

- **Abas separadas para Texto, Planilha e Apresentação:** mais abas na barra, que cabe seis; o menu já abre na aba certa e filtra as fatias.
- **Sub-abas dentro de Docs:** mais um gesto e desenho novo no disco.
- **Mostrar no Brotinho as fatias do Jumbo apagadas, com "Disponível no Pizza Jumbo":** poluiria o menu do pacote leve.
- **Áudio só no Jumbo:** não economiza nada, só tira recurso do Brotinho.
- **Campo `Package` no catálogo:** duplicaria o `DocumentEngine` enquanto ele for a única diferença.
