# Plano: formatos por pacote e parcelas em paralelo

- **Status:** aprovado pelo mantenedor em 13/11/2026. Decisões registradas no [ADR-036](../adr/036-formatos-por-pacote-e-fatias-incompativeis.md) e na SPECS (§2.1 e §5.3).
- **Base:** ADR-026 (Vídeo), ADR-033 (PDFium e QPDF), ADR-034 (Brotinho e Jumbo), ADR-035 (Áudio) e a lista de formatos colada pelo mantenedor.

## 1. Decisões desta rodada

| Tema | Decisão |
| --- | --- |
| Áudio | Entra também no **Brotinho**: o Media Foundation é do Windows e não aumenta o pacote |
| Aba Docs | **Uma aba só, com voltas.** Dentro de Docs aparecem só as fatias que servem ao arquivo arrastado (DOCX não mostra CSV) |
| Outras abas | Continuam visíveis. Fatia incompatível fica apagada; ao clicar ou soltar nela, a descrição da fatia vira "Experimente a aba Docs". A aba citada é a que tem conversões para o arquivo. O menu continua aberto para nova escolha |
| Personalizar a pizza | Fase 8, **nos dois pacotes**: ligar e desligar abas e fatias e reordenar, em Configurações. Padrão: tudo ligado |
| Fora do Pizza | Compactados, 3D e CAD, PSD/AI/CDR, OCR, arquivos com DRM, PAGES/KEY/NUMBERS e raster → vetor. Vão para `docs/ideias-descartadas` com o motivo |
| Pizza — VII | Nova sessão no grupo Pizza: aba Web, dados, legendas e Markdown/HTML, em .NET puro, nos dois pacotes |

**A1 (aprovada):** no Brotinho, fatias que só existem no Jumbo **não aparecem** (menos é mais).

**A2 (aprovada):** legendas ficam na aba Web até existir a aba Vídeo (v1.x, Jumbo).

## 2. O que vai em cada pacote

Legenda: **B** = Brotinho e Jumbo. **J** = só Jumbo. ✔ = já existe. Motor entre parênteses.

### Imagem (libvips) — B

| Conversão | Pacote | Situação |
| --- | --- | --- |
| JPG, PNG, WEBP, AVIF, TIFF, ICO, GIF entre si; Imagem → PDF | B | ✔ |
| JPEG XL | B | Em estudo: depende de o libvips empacotado trazer o libjxl |
| SVG → PNG | — | Fica fora: o `svgload` é bloqueado de propósito (V-04) |
| HEIC, RAW | — | v1.x em estudo (codec com patente ou biblioteca nova) |

### PDF (QPDF e PDFium) — Pizza — VI

| Conversão | Pacote | Situação |
| --- | --- | --- |
| Juntar, Dividir, Girar | B | ✔ |
| Comprimir | B | A fazer |
| PDF → PNG/JPG por página | B | A fazer (PDFium) |
| PDF → TXT | B | A fazer (texto do PDFium) |
| PDF → DOCX | J | A fazer (LibreOffice), com aviso de que o layout pode mudar |
| PDF com senha | B | A fazer (SEC-70 a SEC-73) |

### Docs (aba única com voltas) — Pizza — V

| Conversão | Pacote | Motor |
| --- | --- | --- |
| DOCX → TXT, XLSX → CSV | B | .NET ✔ |
| DOCX → HTML, DOCX → MD | B | .NET (`ZipArchive` + `XmlReader`) |
| PPTX → TXT (texto dos slides) | B | .NET |
| EPUB → TXT, EPUB → HTML | B | .NET (zip + XHTML sem DTD) |
| CBZ → PDF | B | libvips + gerador próprio do ADR-022 |
| MD → HTML (Docs) e HTML → MD (Web) | B | .NET, feito no **Pizza — VII** e reaproveitado aqui |
| DOCX, DOC, ODT, RTF, XLSX, XLS, ODS, PPTX, PPT, ODP → PDF | J | LibreOffice (✔ para DOCX/XLSX/PPTX) |
| DOCX ↔ ODT ↔ RTF ↔ DOC | J | LibreOffice |
| XLSX ↔ ODS ↔ XLS; CSV → XLSX | J | LibreOffice |
| PPTX ↔ ODP ↔ PPT | J | LibreOffice |
| PPTX → PNG por slide | J | LibreOffice → PDF → PDFium |
| MD → PDF/DOCX, EPUB → PDF | J | HTML do .NET → LibreOffice |

XLSM e DOCM convertidos perdem as macros. Isso é desejado (SEC-62) e o painel avisa.

### Áudio (Media Foundation) — B — Pizza — IV

Como no ADR-035: MP3, M4A (AAC/ALAC), FLAC e WAV entre si, com as opções do painel. OGG/Opus e vídeo seguem na v1.x com o FFmpeg, só no Jumbo (ADR-026).

### Web, dados e legendas (.NET puro) — B — Pizza — VII

| Conversão | Situação |
| --- | --- |
| SRT ↔ VTT ↔ ASS | A fazer. ASS → SRT perde estilos e posição; o painel avisa |
| JSON ↔ XML; JSON/XML → CSV (só estrutura plana); CSV → JSON; JSON → YAML | A fazer |
| YAML → JSON | Em estudo: exige um leitor de YAML próprio (aliases em cascata são vetor de bomba) |
| Gerar `.gz` e `.br` | A fazer (`GZipStream`, `BrotliEncoder`) |
| TTF/OTF → WOFF e WOFF2 | A fazer (WOFF2 com transformação nula das tabelas, permitida pela especificação) |
| Minificar HTML e CSS | A fazer, só o que é seguro (espaços e comentários) |
| Minificar JS | Em estudo: minificador próprio é arriscado; biblioteca exige justificativa e SBOM |
| HTML → MD | A fazer (junto com MD → HTML) |

### Vídeo — J — v1.x

Fica fora destas parcelas. Começa depois da análise de licença e patentes do FFmpeg (ADR-026).

## 3. Arquitetura

```
Residente (Pizza.App)                         Sandboxes (AppContainer, sem rede)
┌──────────────────────────────┐   handles    ┌──────────────────────────────┐
│ ConverterCatalog             │─────────────▶│ Pizza.Worker                 │
│  · Tab, Inputs, DocumentEngine│   pipe sem   │  libvips, QPDF, PDFium,      │
│  · filtro por arquivo        │   nome       │  Media Foundation, .NET puro │
│ RadialMenu / Overlay         │              └──────────────────────────────┘
│  · fatia apagada + aviso     │              ┌──────────────────────────────┐
│ ConversionRunner             │─────────────▶│ Pizza.Docs (só Jumbo)        │
│  · escolhe o sandbox         │              │  LibreOfficeKit              │
└──────────────────────────────┘              └──────────────────────────────┘
```

- **Sem campo novo no catálogo:** o Jumbo é marcado pelo `DocumentEngine` que já existe; sem o motor (Brotinho), essas fatias não aparecem. O FFmpeg ganha a sua marca quando o Vídeo entrar.
- **Filtro dentro de Docs:** a aba Docs lista só as fatias cujo `Inputs` contém a extensão do arquivo (conferida pelo conteúdo, como hoje).
- **Aviso de incompatível:** usa o `Inputs` do catálogo para achar a aba que serve ao arquivo. Texto curto, some sozinho; sem janela nova.
- **Conversores .NET puro** ficam em `Pizza.Converters`, rodam no Worker, e seguem as regras do adendo de 13/11 do ADR-034: XML sem DTD, teto de bytes reais, `CORRUPTED` e `LIMIT_EXCEEDED`.
- **Requisitos tocados:** SEC-15 (só handles), SEC-20 (todo parser novo no Worker), SEC-40 (limites), SEC-62/63 (LibreOffice), PRIV-16 (log sem nome nem conteúdo). Nenhuma invariante do `CLAUDE.md` muda.

### Trocas consideradas

| Escolha | Ganho | Custo |
| --- | --- | --- |
| Docs único com voltas | Menos abas; o menu já abre certo | Mais voltas quando o arquivo aceita muitas saídas (DOCX no Jumbo: ~8) |
| .NET puro em vez de bibliotecas | Sem dependência nova nem SBOM maior | Cada leitor novo é código nosso a testar contra arquivos hostis |
| MD/EPUB → PDF via HTML no LibreOffice | Reaproveita o motor que já existe | Só no Jumbo; o Brotinho fica sem PDF de MD/EPUB |

**A rever depois:** se o número de fatias por arquivo passar de 12 (duas voltas), avaliar sub-abas ou a personalização da Fase 8 com outro padrão.

## 4. Parcelas em paralelo

### Etapa 0 — fundação (esta sessão, um PR, antes das demais)

1. ADR-036 + emendas na SPECS §2.1 e §5.3 + itens em `ideias-descartadas` → verificar: revisão do mantenedor.
2. Fatias por arquivo na aba dele, fatias apagadas nas outras e aviso de incompatível → verificar: testes do `RadialMenu` e teste no app pelo mantenedor.

Só a Etapa 0 mexe em `RadialMenu`, `Overlay`, `TabCarousel` e no formato do `ConverterCatalog`. Depois dela, cada parcela só **acrescenta** entradas no bloco da sua aba.

### Pizza — IV (Áudio)

1. Concluir o PR #81 (etapa 1).
2. Marcar Áudio como B e conferir o zip do Brotinho.

### Pizza — V (Docs)

1. DOCX → HTML e DOCX → MD (B).
2. PPTX → TXT, EPUB → TXT/HTML, CBZ → PDF (B).
3. Entradas DOC, ODT, RTF, XLS, ODS, PPT, ODP → PDF (J).
4. Trocas entre formatos de escritório e CSV → XLSX (J).
5. PPTX → PNG por slide e MD/EPUB → PDF (J; depende do MD → HTML do VII).
6. Pacote: CI publica os dois zips; enxugar dicionários com teste de fidelidade.

### Pizza — VI (PDF)

1. Concluir o PR #79.
2. PDF → PNG/JPG por página e PDF → TXT (B).
3. Comprimir (B).
4. PDF com senha (B; SEC-70 a SEC-73).
5. PDF → DOCX (J).

### Pizza — VII (Web, dados e legendas) — sessão nova

1. Aba Web no menu + legendas SRT ↔ VTT ↔ ASS.
2. MD ↔ HTML (aviso ao Pizza — V quando mesclar).
3. JSON ↔ XML, → CSV, CSV → JSON, JSON → YAML.
4. `.gz` e `.br`; TTF/OTF → WOFF/WOFF2.
5. Minificar HTML e CSS.

### Regras de convivência

- `git fetch` e conferir `origin/main` antes de mesclar; atualizar ramo com `git merge origin/main` (force-push bloqueado).
- Avisar as outras sessões por mensagem antes de mexer em `ConverterCatalog` fora do próprio bloco, `ConversionRunner`, `OutputNaming`, `ConversionMessages` ou `Conversion.cs`.
- Uma etapa por PR, validada pelo mantenedor no app, com CI e CodeQL verdes.
- A seção do Dossiê de cada aba é da sessão dona da aba; o layout geral continua com o Pizza — II.

## 5. Estratégia de testes

| Camada | O quê | Onde vale |
| --- | --- | --- |
| Unidade | Arquivos-ouro pequenos por conversão (entrada → saída esperada, byte a byte ou por texto normalizado) | Todo conversor .NET puro |
| Contrato do catálogo | Todo `ConverterInfo` tem aba válida, entradas e ao menos um arquivo-ouro; sem o motor, nenhuma fatia J aparece | Etapa 0, roda em todo PR |
| Arquivo hostil (TST-03) | Corrompido, truncado e aleatório: só `CORRUPTED`, nunca queda nem travamento | Todo leitor novo (EPUB, PPTX, JSON, XML, YAML, SRT/ASS, TTF/OTF, CBZ) |
| Limites (TST-08) | Bomba de zip, XML com DTD, JSON aninhado demais, alias de YAML em cascata, legenda gigante | Idem |
| Injeção | CSV com `=`, `+`, `-`, `@` ganha apóstrofo; HTML gerado não leva script do original | CSV, HTML, MD |
| Integração no sandbox | Conversão real no Worker em AppContainer, com contraprova para teste que passa de primeira | Uma por fatia |
| Fidelidade (Jumbo) | Comparação de texto e de número de páginas contra referência | LibreOffice |
| Desempenho | NFR-04/05 por conversão nova; medir antes de dizer pronto | Todas |
| No app | Mantenedor arrasta pelo Explorer e valida cada etapa | Todas |

Lacunas de hoje: não há teste de contrato do catálogo nem teste que impeça fatia J no Brotinho; os dois entram na Etapa 0.

## 6. Pendências antes do código

- ~~Mantenedor aprova este plano e as premissas A1 e A2~~ (13/11/2026).
- ADR-036 mesclado.
- ~~Pizza — VII criado~~ (13/11/2026).
