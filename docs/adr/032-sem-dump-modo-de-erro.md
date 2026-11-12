# ADR-032 — Sem dump: modo de erro do processo e exclusão do WER

- **Status:** Aceita pelo mantenedor em 12/11/2026.
- **Data:** 2026-11-12

## Contexto

PRIV-05 previa só `WerAddExcludedApplication`. Medido no notebook do mantenedor, que tem a chave `LocalDumps` no HKLM (basta a chave existir para o Windows gravar dump de qualquer programa), com um executável NativeAOT igual ao `Pizza.exe`:

| Travamento | Sem proteção | Só a exclusão do WER | `SetErrorMode(SEM_NOGPFAULTERRORBOX)` |
| --- | --- | --- | --- |
| Acesso inválido à memória | dump e relatório | — | nada |
| Exceção não tratada | dump e relatório | — | nada |
| `FailFast` | dump e relatório | **dump** (só não envia) | nada |

A exclusão tira o relatório da fila de envio, mas o dump continua no disco, com o que estava na memória: nomes e caminhos de arquivos, a cópia da tela do vidro e o documento. O residente não roda em sandbox, então é o caso mais exposto. O Worker também não estava protegido: com o modo de erro padrão, o Worker sabotado gerou dump mesmo no AppContainer e no job. Nos testes isso não aparecia, porque ele herdava o modo do processo de teste.

## Decisão

- **Residente e Worker** chamam `SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX)` como primeira instrução. Com isso o Windows não chama o WER para o processo: sem dump e sem relatório.
- **O Worker herda esse modo do residente** já na criação, antes de rodar qualquer código (o residente não usa `CREATE_DEFAULT_ERROR_MODE`). A chamada no próprio Worker é a segunda barreira. O teste do sandbox confere o modo dentro do AppContainer.
- `JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION` foi testado e descartado: o modo do Worker continuou 0 e o dump foi gravado.
- **Segunda barreira:** a cada início, o residente chama `WerAddExcludedApplication` para `Pizza.exe` e `Pizza.Worker.exe`, no registro do usuário (`HKCU\Software\Microsoft\Windows\Windows Error Reporting\ExcludedApplications`), sem administrador. Falhar aqui não impede o Pizza de abrir.
- A falha continua virando só `WORKER_CRASHED` no log local.

## Consequências

Um travamento do Pizza não deixa rastro fora do log do próprio Pizza. Fica no log de eventos do Windows o registro do .NET (eventos 1025 e 1026, local, sem dump); ele não sai do PC por conta do Pizza.

O Pizza portátil deixa dois valores no HKCU depois de apagado. Quando o instalador existir, a desinstalação remove os dois com `WerRemoveExcludedApplication`.

## Alternativas descartadas

- **Só a exclusão do WER (texto anterior do PRIV-05):** não impede o dump quando o `LocalDumps` está ligado.
- **`LocalDumps\Pizza.exe` com `DumpCount` 0:** fica no HKLM e exige administrador; o Pizza portátil não tem.
- **Filtro de exceção próprio que encerra o processo:** não pega o `FailFast` nem o encerramento do CFG, que passam por cima dos filtros.
