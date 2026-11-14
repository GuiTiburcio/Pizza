# ADR-037 — Menus menores, móveis e redimensionáveis

- **Status:** Aceita pelo mantenedor em 13/11/2026, na prancha MOV-1 do Dossiê. Substitui o mínimo de 44 px dos alvos do ADR-026; o restante do ADR-026 continua valendo.
- **Data:** 2026-11-13

## Contexto

O mantenedor pediu, em 13/11/2026, que o menu radial e os demais menus fiquem menores, possam ser movidos pela tela e ajustados com o mouse, de forma responsiva à tela, ocupando no máximo cerca de 1/15 dela. Hoje o disco abre numa janela de 400 × 562 px a 96 dpi, perto de 1/9 da área útil de um monitor 1080p a 100%, e fica parado onde nasceu. O painel de opções nasce centrado no disco, e os avisos e o painel Arquivos ficam no canto inferior direito.

## Decisão

- **Tamanho:** o padrão e o teto são 1/15 da área útil do monitor onde a janela abre, medida em pixels físicos. Assim a proporção na tela é a mesma em qualquer resolução e escala do Windows (100% a 200%, NFR-08). Em 1080p, o conjunto fica com cerca de 77% do tamanho de hoje.
- **Escala uniforme:** tudo encolhe junto, inclusive a barra de abas, os níveis e as linhas do painel. Os alvos podem ficar abaixo dos 44 px do ADR-026.
- **Mover:** segurar o botão esquerdo em qualquer espaço que não seja botão ou alvo interativo e arrastar move a janela. Vale para o disco na escolha por clique (depois de soltar fora das fatias), para o painel de opções, para o painel Arquivos e para os avisos. Durante o Ctrl + arrastar de um arquivo, o disco continua nascendo no cursor e não se move: o botão do mouse está ocupado com o arquivo, e o drop nunca vira MOVE (SEC-05).
- **Redimensionar:** puxar a borda do círculo do disco ou qualquer borda dos retângulos dos menus muda o tamanho; o conteúdo se adapta. A borda só se destaca com o cursor sobre ela, sem fundo tracejado (comentário do mantenedor na prancha MOV-1, 14/11/2026). Ctrl + roda sobre o disco, na escolha por clique, também muda o tamanho. Um único fator vale para todos os menus, de 50% a 100% do padrão. A roda sem Ctrl continua trocando a aba (FR-04). O Ctrl é lido no próprio evento da roda, sem hook de teclado (SEC-01).
- **Vidro ao mover:** durante o movimento, o vidro não recopia a tela a cada quadro; a janela fica só translúcida, deixando o fundo real aparecer pela transparência da própria janela. Ao soltar, o trecho novo é copiado e desfocado uma vez, só em memória, como no ADR-023.
- **Lembrar:** o fator de tamanho e a posição dos painéis ficam num arquivo pequeno em `%LOCALAPPDATA%\Pizza`, sem nome de arquivo nem conteúdo (PRIV-15). A posição é gravada como fração da área útil do monitor, para caber em outra resolução. O disco não guarda posição: nasce no cursor.

## Consequências

Menus menores ocupam menos da tela, mas, com a escala uniforme, os alvos podem ficar abaixo de 44 px e o texto menor; a acessibilidade da barra de abas deixa de ter piso fixo. Passa a existir um arquivo de preferências antes da tela de Configuração da Fase 8. Mover exige uma área de arrasto bem definida em cada janela, para que um clique num botão nunca vire movimento.

## Alternativas descartadas

- Uma das 15 células de uma grade 5 × 3: deixaria o conjunto com cerca de 64% do tamanho atual.
- Manter os alvos em 44 px enquanto o resto encolhe: preservaria o ADR-026, mas o mantenedor preferiu a escala uniforme.
- Recopiar e desfocar a tela a cada quadro do movimento: custo de CPU e risco de passar do orçamento de quadro.
- Abrir o disco sempre no último lugar: tiraria o menu de perto do arquivo arrastado.
