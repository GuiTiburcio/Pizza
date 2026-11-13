# Fase 6 — Aba PDF

Biblioteca e regras em [ADR-033](adr/033-pdfium-e-qpdf.md); telas nas pranchas 6A-1 a 6A-3 do Dossiê.

| Etapa | O que entra | Estado |
| --- | --- | --- |
| 6a-1 — Girar | QPDF no Worker; Girar todas as páginas (Direita, Ponta-cabeça, Esquerda); saída `nome - girado.pdf`; limpeza de conteúdo ativo (SEC-76, TST-22); menu abre na aba PDF (FR-03) | Concluída no PR #68 |
| 6a-2 — Dividir | Cada página (`nome - pág 01.pdf`…) ou pela metade (`nome - parte 1.pdf`, `parte 2`); o Worker entrega um pacote de partes e o residente confere e grava cada uma, todas ou nenhuma | Concluída no PR #72 |
| 6a-3 — Juntar | Vários PDFs no mesmo gesto, só para Juntar (emenda ao FR-30); as outras fatias esmaecem e dizem por quê; a faixa mostra a ordem antes de soltar; saída `primeiro - juntado.pdf`; os outros arquivos chegam ao Worker só como handles (`moreSrcHandles`) | Em revisão |
| Senha | Campo de senha (FR-24), AES-256 na saída (SEC-70 a SEC-75) | Pendente; hoje, PDF protegido recebe `PASSWORD` |
| Imagens e Comprimir | PDFium | Pendente |
| Word | Motor decidido com a aba Docs | Pendente |

NFR-05 (Juntar, Dividir e Girar abaixo de 1 s até 100 páginas) é medido quando as três estiverem prontas.
