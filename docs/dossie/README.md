# Dossiê do projeto

**[Abrir o artefato original no Claude Design](https://claude.ai/artifact/LMrbTPnaY6EbVK1UPjcVDs#page-dcb81aee8d83)** (exige login no Claude). As imagens abaixo são as mesmas pranchas, exportadas para quem não usa o Claude. O texto consolidado do planejamento está em [PIZZA-PROJETO.md](../PIZZA-PROJETO.md).

Pranchas do planejamento do Pizza, feitas antes do código, em 08/11/2026 e 09/11/2026. Elas mostram:

- público e posicionamento;
- sistema visual;
- arquitetura;
- segurança e LGPD;
- implantação;
- wireframes.

> **Padrão desde 11/11/2026:** pranchas de documentação em tema claro, 1600 × 1000, fundo único; telas do Pizza em claro e escuro lado a lado.
>
> **Revisão de 10/11/2026:** as pranchas foram atualizadas para bater com a SPECS v1.2. Se alguma ainda divergir, valem as [SPECS](../SPECS.md) (em especial a §2.1, com as emendas) e os [ADRs](../adr/).
>
> As principais mudanças desde o planejamento são estas:
>
> - **Gesto:** soltar fora de uma fatia deixa o menu aberto para escolher com um clique ([ADR-025](../adr/025-escolha-por-clique-apos-o-gesto.md)).
> - **Vidro:** desenhado com GDI+ e um desfoque próprio ([ADR-023](../adr/023-vidro-gdiplus-desfoque-proprio.md)).
> - **Notificações:** são janelas do próprio Pizza ([ADR-018](../adr/018-notificacao-e-painel-proprios.md)).
> - **Simplificações do v1** ([ADR-019](../adr/019-simplificacoes-do-v1.md)):
>   - um Worker por arquivo;
>   - documentos do Office só pelo LibreOffice isolado;
>   - nenhum download além da atualização.
> - **Canal com o Worker:** pipes sem nome ([ADR-021](../adr/021-pipe-sem-nome.md)).
> - **Imagem para PDF:** um gerador próprio, sem dependência nova ([ADR-022](../adr/022-imagem-pdf-gerador-proprio.md)).

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

### Referência · texto das fatias

Decidido em 10/11/2026: **B · Curva completa**, mostrada nos temas claro e escuro. A versão A, sem girar, foi descartada porque a descrição longa não cabia nas fatias laterais sem quebrar a linha.

![Texto das fatias](ref-texto-fatias.png)

### Referência · barra de abas para seis abas

Decidido em 11/11/2026: **B · ícone e nome, 44 px**, para a Fase 4d. A versão A (texto curto, 38 px) foi descartada porque deixa os alvos abaixo dos 44 px de acessibilidade (NFR-06).

![Barra de abas](ref-barra-abas.png)

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

### ENG-1 · Arquitetura em dois processos

![Arquitetura](eng-1-arquitetura.png)

### ENG-2 · Máquina de estados

![Estados](eng-2-estados.png)

### ENG-3 · Módulos e contratos

![Módulos](eng-3-modulos.png)

### ENG-4 · Sequência de uma conversão

![Sequência](eng-4-sequencia.png)

## Decisões recentes

### DEC-1 · O que mudou no caminho, e o que vem depois

![Decisões recentes e próximos passos](dec-1-decisoes.png)

## Telas de referência

Telas do design aprovado, nos temas claro e escuro. No Claude Design, a Ref-Imagem e a Ref-PDF são interativas: passar o mouse nas fatias e clicar num nível muda os dois temas juntos. Aqui, a imagem mostra o estado inicial.

### Design aprovado · aba Imagem

![Design aprovado da aba Imagem](ref-imagem.png)

### Design aprovado · aba PDF

![Design aprovado da aba PDF](ref-pdf.png)

### Fluxo do gesto, passo a passo

![Fluxo 1 · seleciona o arquivo](fluxo-1.png)

![Fluxo 2 · Ctrl e arrasta](fluxo-2.png)

![Fluxo 3 · passa o mouse no PNG](fluxo-3.png)

![Fluxo 4 · abrem as opções](fluxo-4.png)

![Fluxo 5 · escolhe e converte](fluxo-5.png)

![Fluxo 6 · pronto](fluxo-6.png)

## Fase 4b · para aprovar

Pranchas da próxima etapa do código, nos temas claro e escuro. Depois da aprovação do mantenedor, viram ADR e só então código.

### Faixa de níveis acima do disco

![Faixa de níveis](4b-1-faixa-niveis.png)

### Bolha de vidro (liquid glass), sem e com franjas

![Bolha de vidro](4b-2-bolha-vidro.png)

### Painel de opções em vidro

![Painel de opções em vidro](4b-3-painel-vidro.png)

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

### WF-6 · Configuração (v1.0)

![Configuração](wf-6-configuracoes.png)
