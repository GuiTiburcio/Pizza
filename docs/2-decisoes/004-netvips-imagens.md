# ADR-004 — NetVips para imagens

- **Status:** Aceita
- **Data:** 2026-11-01

## Contexto

Conversão de imagem é o caso mais comum e precisa levar menos de 1 s.

## Decisão

Usar NetVips (libvips).

## Consequências

Rápido e com pouca memória. libvips é LGPL, usada como biblioteca dinâmica.

## Alternativas descartadas

- Magick.NET.
