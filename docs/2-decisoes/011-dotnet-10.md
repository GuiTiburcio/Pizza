# ADR-011 — .NET 10 em vez de .NET 8

- **Status:** Aceita
- **Data:** 2026-11-07

## Contexto

A spec original previa .NET 8. O suporte do .NET 8 termina em 10/11/2026, e o .NET 10 é o LTS atual (já instalado no PC de desenvolvimento).

## Decisão

Todos os projetos usam `net10.0` (residente e Worker com `net10.0-windows`).

## Consequências

Suporte até 2028 e NativeAOT mais maduro. A spec foi atualizada nas Partes 5.2, 12.3 e 13.

## Alternativas descartadas

- .NET 8.
