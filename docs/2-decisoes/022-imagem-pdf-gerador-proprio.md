# ADR-022 — Imagem → PDF com gerador próprio

- **Status:** Aceita
- **Data:** 2026-11-09

## Contexto

O ADR-005 proíbe dependências AGPL e previa o PDFsharp para PDF, contando com a licença MIT. O PDFsharp é MIT até a versão 6.2.4; a partir da 6.5.0 passou a ser AGPL-3.0 ou licença comercial. A SPECS define as opções de Imagem → PDF, mas não o tamanho da página.

## Decisão

Imagem → PDF usa um gerador próprio, sem dependência nova: uma página A4, em pé ou deitada conforme a imagem, com margem de 10 mm e a imagem centralizada. A imagem é decodificada pelo libvips e recodificada em JPEG, então os bytes de origem e os metadados não chegam ao PDF. Escritório e Tela limitam a resolução a 150 e 96 dpi na folha, sem aumentar os pixels.

## Consequências

Nada novo para atualizar nem para auditar além de cerca de 100 linhas. O gerador só serve para este caso. A biblioteca de Comprimir, Juntar e Dividir continua em aberto e será decidida na Fase 6; a menção ao PDFsharp no ADR-005 deixa de valer para versões atuais.

## Alternativas descartadas

- PDFsharp 6.2.4, a última versão MIT: não receberia mais correções nessa licença.
- PDFsharp atual sob AGPL: exigiria substituir o ADR-005.
- Página do tamanho da imagem: uma foto de celular viraria uma página enorme, ruim para imprimir.
