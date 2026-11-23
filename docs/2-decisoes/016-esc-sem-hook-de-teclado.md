# ADR-016 — Esc sem hook de teclado

- **Status:** Aceita
- **Data:** 2026-11-08

## Contexto

Com o Ctrl pressionado, Esc vira Ctrl + Esc e abre o Menu Iniciar. Impedir isso exigiria hook de teclado, proibido pelo SEC-01. O atalho não pode ser reservado com RegisterHotKey (erro 1409, testado em 2026-11-08).

## Decisão

Nenhum hook de teclado, para todos os usuários. O cancelamento vem do próprio arraste OLE. O menu orienta: 'Solte o Ctrl e aperte Esc para cancelar'. O Ctrl só é lido no início do arraste.

## Consequências

Quem apertar Ctrl + Esc abre o Menu Iniciar. Em troca, a promessa de que o Pizza não enxerga o teclado continua verdadeira e verificável.

## Alternativas descartadas

- Hook de teclado só durante o gesto.
- RegisterHotKey de Ctrl + Esc (o Windows recusa).
