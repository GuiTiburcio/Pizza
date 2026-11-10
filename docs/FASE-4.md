# Fase 4 — acompanhamento do visual

Referências: [SPECS §15](SPECS.md#15-rastreabilidade-requisito--fase--teste), [dossiê, Partes 4 e 13](PIZZA-PROJETO.md), [prancha do painel](dossie/wf-3-painel.png). As SPECS prevalecem. Este acompanhamento detalha a fase existente; não muda sua ordem nem seu critério de pronto.

**Fase aberta.** O critério final continua sendo corresponder ao canvas aprovado. A Parte 13.1 prevê validação do mantenedor ao final de cada etapa, antes de seguir para a próxima.

| Etapa | Entrega | Situação |
| --- | --- | --- |
| 4a — disco | Vidro, fatias, hover, animação, temas e escolha por clique | Implementado nos PRs #26 e #30; conferência visual ampla pendente |
| 4r — reorganização | Separar `Overlay.cs` (720 linhas) e `DiscRenderer.cs` (601 linhas) por assunto, sem mudar comportamento | Pedido do mantenedor em 10/11/2026; vem antes de continuar a 4b |
| 4b — painel de opções | Temas, cantos, foco, legibilidade e vidro | Base de temas, cantos e alvos implementada; vidro e aceite visual pendentes |
| 4c — notificações e Arquivos | Aplicar linguagem visual e temas aos componentes da Fase 3 | Aguardando 4b |
| 4d — bandeja | Janela própria de vidro com teclado, conforme ADR-020 | Aguardando 4c |
| 4e — aceite da fase | Comparar o conjunto com o dossiê e conferir NFR-06/NFR-08 | Pendente; libera a Fase 5 |

## Etapa 4r — reorganização

A 4b (faixa de níveis e vidro no painel) mexe nesses dois arquivos. Separá-los antes deixa cada mudança menor e restrita à parte que ela toca.

- `Overlay.cs` vira partes de uma classe parcial, por assunto: janela e mensagens; arraste e soltura; escolha por clique; animação e hover. O estado continua o mesmo; só muda de arquivo.
- `DiscRenderer.cs` separa o desenho do vidro e das formas, o texto (reto e curvo) e a montagem do quadro.
- Nenhum comportamento muda. Prova: os testes passam antes e depois, e o verificador local desenha os mesmos quadros com resultado idêntico, pixel a pixel.

## Etapa 4b — base do painel

- O painel lê o tema dos aplicativos do Windows a cada abertura; se a leitura falhar, usa o tema claro.
- Cantos: 28 px no painel e 12 px nas opções e botões, escalados com o DPI do monitor.
- Alvos de clique de pelo menos 44 × 44 px lógicos. A caixa de metadados mantém o desenho compacto, com toda a linha clicável.
- Texto principal, secundário e sobre seleções com contraste mínimo de 4,5:1 nos fundos opacos usados. O preenchimento azul é `#2F6AFF`: o branco sobre `#2F6BFF` resulta em 4,4988:1, abaixo do NFR-06.
- Seleção continua indicada por marca e negrito, além da cor. Tab, Shift+Tab, setas, Espaço, Enter e Esc mantêm o comportamento existente.
- Em área útil menor que o painel, a altura fica limitada à tela sem reduzir fontes ou controles. A roda e os botões fixos ↑/↓ permitem percorrer o conteúdo; navegar pelo teclado revela automaticamente o alvo com foco. Testes cobrem o painel PNG com 640 px físicos de altura útil a 200%.
- **Ainda sem vidro no painel.** A superfície opaca desta entrega é a base para a próxima parte de 4b; não representa o aceite do visual final.

## Verificação e aceite

TST-01 cobre a máquina de estados e a geometria; testes adicionais verificam contraste e tamanho/alcance dos alvos. Os testes existentes cobrem não sobreposição, opções aceitas pelo Worker e navegação por teclado. Compilação, testes e NativeAOT devem passar no CI de cada PR.

Checklist manual da Fase 4 (NFR-06/NFR-08), ainda pendente:

- [ ] Comparar com a prancha WF-3 em temas claro e escuro.
- [ ] Reabrir após trocar o tema do Windows e conferir texto, seleção e foco.
- [ ] Percorrer todas as opções somente pelo teclado; cancelar e converter com um arquivo de teste.
- [ ] Conferir rótulos longos, aviso de metadados, botões e cantos em 100%, 125%, 150% e 200% de escala.
- [ ] Conferir posicionamento nos limites da tela e em monitores com DPI diferente.
- [ ] Conferir o vidro quando ele for implementado, inclusive transparência desligada e fundos de alto contraste.
- [ ] Registrar o aceite do mantenedor antes de avançar para 4c.

A revisão de segurança desta alteração é focada no código-fonte, conforme o modo de orientação da skill `security-audit`. A rolagem usa somente mensagens da própria janela. Não altera drop, hooks, IPC, Worker, acesso a arquivos ou logs; preserva SEC-01 e PRIV-16. Não foram executados testes locais de auditoria: este ambiente não demonstrou todos os controles de isolamento exigidos pela skill. O CI é validação de engenharia; a auditoria completa TST-28 permanece na Fase 5.
