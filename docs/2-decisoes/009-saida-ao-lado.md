# ADR-009 — Saída ao lado do original, sem sobrescrever

- **Status:** Aceita
- **Data:** 2026-11-01

## Contexto

O resultado precisa ser previsível e o original nunca pode ser perdido.

## Decisão

Saída na mesma pasta e com o mesmo nome; se existir, `nome (1).ext`, `nome (2).ext`…

## Consequências

Seguro e previsível.

## Alternativas descartadas

- Pasta fixa (fica como opção nas Configurações).
