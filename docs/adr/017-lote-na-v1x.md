# ADR-017 — Lote na v1.x, sem teto de arquivos

- **Status:** Aceita
- **Data:** 2026-11-08

## Contexto

O FR-28 colocava o lote na Fase 3, e o SEC-46 sugeria limite de 500 arquivos.

## Decisão

O lote entra na v1.x. Não há teto de quantidade: os arquivos entram numa fila com no máximo 2 conversões por núcleo. Antes da v1.x, arrastar vários mostra a quantidade, recusa o drop e avisa 'Por enquanto, um arquivo por vez'.

## Consequências

O MVP fica menor. A fila protege a máquina sem limitar o usuário.

## Alternativas descartadas

- Lote no MVP.
- Teto de 500 arquivos.
