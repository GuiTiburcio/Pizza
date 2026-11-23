# ADR-002 — Dois processos (residente + Worker)

- **Status:** Aceita
- **Data:** 2026-11-01

## Contexto

O app fica ligado o tempo todo e precisa ser leve em repouso. Bibliotecas de conversão são pesadas e podem falhar com arquivos ruins.

## Decisão

`Pizza.exe` (residente) cuida de hooks, menu e notificações. `Pizza.Worker.exe` abre sob demanda, converte e fecha depois de 30 a 60 s parado. Os dois falam por Named Pipe.

## Consequências

RAM baixa em repouso; uma falha na conversão não derruba o residente. Custo: contrato IPC para manter e testar.

## Alternativas descartadas

- Processo único.
