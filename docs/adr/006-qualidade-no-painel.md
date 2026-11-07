# ADR-006 — Qualidade escolhida depois de soltar, num painel

- **Status:** Aceita
- **Data:** 2026-11-01

## Contexto

Mostrar níveis no hover disputa com o scroll (que troca a aba) e deixa o menu poluído.

## Decisão

Soltar na fatia abre um painel de opções montado a partir do `OptionSchema` do conversor. Conversões sem opções pulam o painel.

## Consequências

Menu mais limpo. A prancha de referência do canvas, que mostra níveis no hover, está desatualizada neste ponto (ver Anexo C.1 da spec).

## Alternativas descartadas

- Níveis no hover em cima do círculo.
