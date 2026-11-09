# ADR-021 — Pipe sem nome entre o residente e o Worker

- **Status:** Aceita
- **Data:** 2026-11-09

## Contexto

As SPECS (SEC-10, 11, 12, 14 e 16) previam um pipe com nome aleatório, ACL condicional, instância única e conferência do PID do cliente. Tudo isso existe para impedir que outro processo encontre e abra o canal. Com um Worker por arquivo (ADR-019), cada processo tem um único canal e um único dono.

## Decisão

O residente cria dois pipes sem nome e entrega ao Worker apenas as pontas dele, por herança restrita a esses dois handles (`PROC_THREAD_ATTRIBUTE_HANDLE_LIST`). Os números dos handles vão na linha de comando. Os requisitos SEC-13 (mensagens validadas) e SEC-15 (nenhum caminho no canal) continuam valendo.

## Consequências

Sem nome, nenhum outro processo tem como localizar ou abrir o canal, inclusive de outra sessão RDP. Saem os requisitos SEC-10, 11, 12, 14 e 16 e a lacuna L-12. Os testes TST-06 e TST-27 deixam de ter objeto. Há menos código de permissões para errar.

## Alternativas descartadas

- Pipe com nome, ACL condicional e verificação de PID.
