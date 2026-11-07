# ADR-005 — Sem dependências AGPL

- **Status:** Aceita
- **Data:** 2026-11-01

## Contexto

O produto será vendido com código fechado.

## Decisão

Nenhuma dependência AGPL é permitida.

## Consequências

Ghostscript e iText ficam de fora. PDF usa PDFsharp + PDFium. O CI checa licenças.

## Alternativas descartadas

- Ghostscript, iText.
