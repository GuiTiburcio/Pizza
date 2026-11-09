# ADR-015 — Opção de animações, com Sempre como padrão

- **Status:** Aceita
- **Data:** 2026-11-08

## Contexto

O Windows pode estar com animações desligadas por desempenho, e não só por acessibilidade. Seguir esse ajuste por padrão escondia o deslize de quem o quer.

## Decisão

Opção Sempre / Seguir o Windows / Nunca, com Sempre como padrão. Em Seguir o Windows com animações desligadas, a transição só troca a opacidade, sem movimento.

## Consequências

Muda o NFR-06: quem precisa de menos movimento escolhe a opção nas Configurações.

## Alternativas descartadas

- Seguir sempre o Windows.
- Sem opção.
