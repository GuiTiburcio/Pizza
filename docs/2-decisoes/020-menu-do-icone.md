# ADR-020 — Menu do ícone

- **Status:** Aceita
- **Data:** 2026-11-09

## Contexto

O FR-50 listava vários itens no menu do ícone, e o mantenedor definiu quais devem ficar nele, incluindo controles de Play, Pause e Stop. O menu padrão do Windows só tem linhas de texto.

## Decisão

O menu final, de cima para baixo, é: Pizza — ativo, Arquivos..., Configuração, Play/Pause/Stop, Abrir pasta de logs e Sair.

Play, Pause e Stop controlam a fila de conversões: Pause suspende, Play continua e Stop cancela o que falta. Ficam apagados sem nada em andamento e chegam na v1.x, com o lote.

O menu vira uma janela própria de vidro na Fase 4, com os controles como botões lado a lado e uso completo por teclado (NFR-06). Cada item só aparece na fase em que passa a funcionar. Exportar diagnóstico, Limpar dados do Pizza e Sobre ficam dentro da tela Configuração.

## Consequências

O menu do ícone fica curto. O menu próprio é mais código do que o menu do Windows, e o teclado passa a ser responsabilidade do Pizza.

## Alternativas descartadas

- Controlar o gesto com Pause, o que reverteria o corte do S-04 (ADR-019).
- Linhas de texto no menu do Windows.
- Mostrar itens apagados antes de funcionarem.
