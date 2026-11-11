# ADR-029 — Progresso real, tempo e pausa no aviso (Fase 4c)

- **Status:** Aceita pelo mantenedor em 11/11/2026, na prancha 4c-2 do Dossiê. Antecipa parte do FR-51 (pausar), que estava na v1.x.
- **Data:** 2026-11-11

## Contexto

O aviso de conversão mostrava uma barra indeterminada, porque o Worker não informava o andamento. O mantenedor pediu uma linha verde com a porcentagem, o tempo decorrido e o estimado, e botões pequenos para continuar, pausar e parar.

## Decisão

- **Progresso:** o Worker liga o aviso de andamento do libvips na imagem de entrada; as etapas seguintes herdam esse aviso. A cada ponto percentual novo, ele envia a mensagem `progress` que o protocolo já previa (SPECS §10.2, só o número de 0 a 100). O residente valida a mensagem como todas as outras (SEC-13, SEC-24).
- **Tempo:** o residente mede o tempo ativo da conversão e estima o que falta pela taxa até agora, só a partir de 5% e de 1 s; antes disso, mostra "calculando o tempo…". Ao concluir, o aviso mostra quanto levou.
- **Pausar e continuar:** o residente congela o processo do Worker (`NtSuspendProcess`) e o retoma (`NtResumeProcess`). O Worker não sabe que foi pausado e nada novo passa pelo canal. O prazo de 2 min (SEC-43) conta só o tempo ativo.
- **Parar:** é o cancelar de hoje (FR-29): encerra o Worker e descarta o temporário.
- Botões com 32 px de desenho e alvo de 44 px (NFR-06), ao lado do título; o ativo ganha a bolha de vidro B.

## Consequências

O aviso deixa de ser uma espera cega. A pausa vale para uma conversão por vez; pausar a fila inteira continua na v1.x. Enquanto pausado, o Worker segura o arquivo de origem aberto só para leitura e o temporário, que continua no diário e é limpo se o Pizza fechar.

## Alternativas descartadas

- **Porcentagem estimada pelo tamanho do arquivo:** seria um número inventado.
- **Pausa cooperativa (o Worker para sozinho):** exige mensagem nova no canal e não funciona no meio de uma etapa do libvips.
- **Suspender thread por thread:** o libvips cria threads durante a conversão; suspender o processo inteiro é atômico.
