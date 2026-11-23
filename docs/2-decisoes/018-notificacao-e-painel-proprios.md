# ADR-018 — Notificação e painel próprios

- **Status:** Aceita
- **Data:** 2026-11-08

## Contexto

As notificações nativas do Windows não aceitam o visual do Pizza, ficam no histórico do sistema e podem ser espelhadas no celular.

## Decisão

Notificação em janela própria no canto inferior direito, respeitando 'Não perturbe' e o leitor de tela, e esperando o desbloqueio da sessão. Sucesso some em 5 s, e o mouse em cima pausa; erro fica até fechar. No máximo 3 empilhadas e um cartão '+N'. Sons originais em WAV tocados pelo Windows, ao concluir e no erro. O painel 'Arquivos...', no clique direito do ícone na bandeja, lista só em memória as conversões desde que o Pizza abriu.

## Consequências

Dispensa os requisitos de espelhamento e histórico do toast (PRIV-18). Os sons enviados não entram no repositório, porque a origem não está confirmada.

## Alternativas descartadas

- Notificação nativa do Windows.
- Histórico do painel gravado em disco.
