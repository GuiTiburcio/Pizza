# Dossiê do projeto

Pranchas do planejamento do Pizza, feitas em 08/11/2026 e 09/11/2026, antes do código. Elas mostram a intenção do projeto: público, visual, arquitetura, segurança, LGPD, implantação e wireframes.

> **Atenção:** este é o retrato do planejamento. Algumas decisões mudaram depois, durante a implementação. Quando uma prancha e as [SPECS](../SPECS.md) discordarem, valem as SPECS (em especial a §2.1, com as emendas) e os [ADRs](../adr/). Exemplos:
>
> - soltar fora do disco não cancela mais: o menu fica aberto para escolher com um clique ([ADR-025](../adr/025-soltar-em-qualquer-lugar.md));
> - a imagem vira PDF com um gerador próprio, sem dependência nova ([ADR-022](../adr/022-imagem-pdf-gerador-proprio.md));
> - as notificações são janelas próprias do Pizza, não os avisos nativos do Windows ([ADR-018](../adr/018-notificacao-e-painel-proprios.md));
> - o vidro é desenhado com GDI+ e um desfoque próprio ([ADR-023](../adr/023-vidro-gdiplus-desfoque-proprio.md)).

## Visão geral

![Visão geral](00-visao-geral.png)

## Produto e marketing

### MKT-1 · Posicionamento

![Posicionamento](mkt-1-posicionamento.png)

### MKT-2 · Confiança

![Confiança](mkt-2-confianca.png)

## Design

### DES-1 · Sistema visual

![Sistema visual](des-1-sistema-visual.png)

## Arquitetura

### ARQ-1 · Fronteiras de confiança

![Fronteiras de confiança](arq-1-fronteiras.png)

## Segurança e LGPD

### SEG-1 · Revisão de segurança do projeto

![Revisão de segurança](seg-1-auditoria.png)

### LGPD-1 · Para onde vão os dados

![LGPD](lgpd-1-dados.png)

## TI

### TI-1 · Implantação

![Implantação](ti-1-implantacao.png)

## Engenharia

### ENG-0 · Roteiro em fases

![Roteiro](eng-0-roteiro.png)

## Wireframes

### WF-1 · Anatomia do menu radial

![Anatomia do menu](wf-1-anatomia.png)

### WF-2 · Fatia em hover

![Fatia](wf-2-fatia.png)

### WF-3 · Painel de opções

![Painel](wf-3-painel.png)

### WF-4 · Notificações

![Notificações](wf-4-notificacoes.png)

### WF-5 · Ícone da bandeja

![Bandeja](wf-5-bandeja.png)
