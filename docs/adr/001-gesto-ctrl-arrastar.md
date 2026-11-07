# ADR-001 — Gesto Ctrl + arrastar

- **Status:** Aceita
- **Data:** 2026-11-01

## Contexto

O gesto precisa funcionar igual na área de trabalho e no Explorer e não pode causar dano se o app falhar.

## Decisão

O menu abre quando o usuário segura **Ctrl** e começa a arrastar um arquivo.

## Consequências

No arraste nativo, Ctrl copia. Se o Pizza falhar, o pior caso é uma cópia do arquivo, nunca um arquivo movido.

## Alternativas descartadas

- Shift: move o arquivo.
- Alt e Ctrl+Shift: criam atalho.
