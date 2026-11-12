# Fase 6 — Aba PDF

Biblioteca e regras em [ADR-033](adr/033-pdfium-e-qpdf.md); telas nas pranchas 6A-1 a 6A-3 do Dossiê.

| Etapa | O que entra | Estado |
| --- | --- | --- |
| 6a-1 — Girar | QPDF no Worker; Girar todas as páginas (Direita, Ponta-cabeça, Esquerda); saída `nome - girado.pdf`; limpeza de conteúdo ativo (SEC-76, TST-22); menu abre na aba PDF (FR-03) | Em revisão |
| 6a-2 — Dividir | Cada página ou pela metade; várias saídas por conversão | Pendente |
| 6a-3 — Juntar | Vários PDFs no mesmo gesto, só para Juntar (emenda ao FR-30); ordem do arraste | Pendente |
| Senha | Campo de senha (FR-24), AES-256 na saída (SEC-70 a SEC-75) | Pendente; hoje, PDF protegido recebe `PASSWORD` |
| Imagens e Comprimir | PDFium | Pendente |
| Word | Motor decidido com a aba Docs | Pendente |

NFR-05 (Juntar, Dividir e Girar abaixo de 1 s até 100 páginas) é medido quando as três estiverem prontas.
