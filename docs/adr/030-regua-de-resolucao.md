# ADR-030 — Régua de resolução no painel (Fase 4d)

- **Status:** Aceita pelo mantenedor em 11/11/2026, na prancha 4d-1 do Dossiê, com o pino B (bolha de vidro).
- **Data:** 2026-11-11

## Contexto

O painel de opções do PNG oferecia só Original, 75% e 50%. O mantenedor pediu uma régua para escolher qualquer porcentagem, por exemplo 67%, sem degraus nem marcas. A ideia estava no roteiro para depois da v1.x e foi antecipada.

## Decisão

- No painel, a Resolução em porcentagem vira uma régua de 20% a 100%. O 100% é o original e fica à direita.
- Qualquer número inteiro vale. O valor aparece numa caixa ao lado do título, que também aceita digitar. Arrastar, clicar na régua, setas (1%), Shift + seta (10%), Home e End e a roda sobre a régua mudam o valor.
- O pino é a bolha de vidro B (ADR-027). O painel não cresce: a régua ocupa a altura da linha de botões que ela substitui.
- O esquema da opção ganha um mínimo: além de "original", aceita de "20" a "99", só dígitos. O Worker valida esse intervalo com o mesmo catálogo (SEC-13); o protocolo não muda.
- A faixa de níveis do menu (ADR-027) continua com os atalhos Original, 75% e 50%.

## Consequências

Mais liberdade sem nenhuma opção nova no protocolo. O painel passa a tratar arrastar e digitar, sempre só pelas mensagens da própria janela (SEC-01). Os 20% de mínimo podem mudar depois, se o uso pedir.

## Alternativas descartadas

- **Degraus de 5%:** o mantenedor quer o valor exato onde soltar.
- **Marcas e números embaixo da régua:** poluem; o valor fica só na caixa.
- **Pinos A (fatia de pizza) e C (mini disco):** a bolha mantém a mesma linguagem do resto do painel.
