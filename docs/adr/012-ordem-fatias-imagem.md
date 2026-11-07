# ADR-012 — Ordem das fatias da aba Imagem

- **Status:** Aceita
- **Data:** 2026-11-07

## Contexto

A ordem das fatias estava em aberto e o canvas não mostrava a fatia GIF.

## Decisão

Começando no topo e no sentido horário: JPG, PNG, WEBP, Imagem → PDF, TIFF, ICO, GIF, sem o formato de origem. Se sobrarem mais de 6, os últimos da ordem ficam de fora.

## Consequências

Regra fixa e fácil de testar. As outras abas decidem a ordem na fase delas.

## Alternativas descartadas

- Ordem do canvas, sem GIF.
