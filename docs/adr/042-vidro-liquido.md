# ADR-042 — Vidro líquido no disco (opção de aparência)

- **Status:** Proposta, em 21/11/2026. Depende da aprovação do mantenedor na prancha [`propostas/vidro-liquido.svg`](../propostas/vidro-liquido.svg) (VL-1). **Nenhum código de `src/` é escrito antes dessa aprovação.** Estende os ADR-023 e ADR-027 sem substituí-los.
- **Data:** 2026-11-21

## Contexto

O ADR-027 deu ao disco a bolha de vidro B (sombra curta, fundo ampliado 1,06 a 1,10 vezes, véu, brilho e franjas coloridas). A ideia "Bolha com borda distorcida" do ROADMAP pede mais: o vidro como uma lente, que dobra o que está atrás nas bordas, com realce especular e uma leve variação quando o cursor se move. O mantenedor aprovou o fluxo prancha, aprovação, ADR e código.

O que o código tem hoje (`src/Pizza.App/DiscRenderer.Shapes.cs`, `DiscRenderer.cs`):

- `Glass` desenha a cópia desfocada da tela (`_backdrop`, ADR-023), um véu e uma borda. `Lens` desenha a bolha B.
- O véu é ajustado pela luminância média da cópia (`_glassAlpha`, mínimo 0,16 no claro e 0,35 no escuro). Um trecho muito claro ou muito escuro atrás de um rótulo pode ficar fora do contraste mínimo, porque a média não enxerga o trecho. Contas da prancha: texto escuro sobre véu 0,16 e fundo preto dá 1,3:1; texto claro sobre véu 0,35 e fundo branco dá 2,1:1.
- Sem transparência do Windows, o vidro vira sólido a 92%.
- "Reduzir animações" é lido por `Overlay.ResolveMotion` (SPI_GETCLIENTAREAANIMATION) e `MotionPolicy`.
- Não há detecção de alto contraste do Windows em `src/` (busca por HighContrast e SPI_GETHIGHCONTRAST sem resultado). Hoje o disco fica igual ao tema em alto contraste.

## Decisão proposta

Uma opção nova, **Configurações › Aparência › Vidro: Padrão | Líquido**. Padrão continua como é e segue sendo o valor de fábrica. A escolha vai para `%LOCALAPPDATA%\Pizza\settings.json`, vale na hora e não envia nada para fora (SEC-50, PRIV-03).

**O que o Líquido desenha**, tudo no `DiscRenderer` com GDI+, sem biblioteca nova:

1. **Lente nas bordas.** A mesma cópia da tela do ADR-023, ampliada em volta do centro do disco (1,16 vezes) e recortada numa faixa de 11 px junto à borda de cada fatia e do centro. O recorte usa regiões do GDI+ (caminho da fatia menos o caminho encolhido); o GDI+ não tem máscara suave, então a transição da prancha vira 2 degraus (zoom 1,16 e 1,08). O interior da fatia fica sem distorção.
2. **Realce especular.** Arco claro no alto à esquerda, arco menor embaixo à direita e um fio no raio interno, mais sombra interna e reflexo claro por dentro da borda. Com o cursor em movimento, o arco gira até 20° para o lado do cursor, suavizado pela mola de 240 ms do ADR-027.
3. **Placa de texto.** Cada rótulo ganha uma pílula com o véu do tema a 78%. O texto nunca depende do que está atrás.
4. **Hover e bolha.** O destaque da fatia em hover e a bolha de abas, níveis e painel usam a mesma lente. As franjas coloridas do ADR-027 ficam só no Padrão; no Líquido, a dispersão entra só se o mantenedor pedir na aprovação.

**Superfície sólida** (alto contraste do Windows, ou transparência do Windows desligada):

- Alto contraste: o disco usa as cores do sistema (COLOR_WINDOW, COLOR_WINDOWTEXT, COLOR_HIGHLIGHT, COLOR_HIGHLIGHTTEXT), com borda de 2 px, sem cópia da tela, sem lente, sem realce e sem sombra. A forma e a posição das fatias não mudam. A detecção usa SPI_GETHIGHCONTRAST e reage à troca de tema com o menu aberto.
- **Escopo a decidir na aprovação:** a mesma queda para sólido vale também para o Padrão, que hoje não trata alto contraste. É uma correção de acessibilidade pequena e independente do Líquido.
- Transparência desligada: sólido a 92% como hoje (SPECS, Desenho e vidro), sem lente, porque não há cópia da tela.

**Movimento reduzido** ("Reduzir animações" ligado): nada se anima. A bolha troca de lugar no quadro seguinte, sem mola e sem crescer; o brilho fica em posição fixa (225°) e não gira. A lente das bordas continua desenhada, mas parada. A regra é a que já existe no `MotionPolicy`.

## Custo de desempenho (NFR-03)

Valores abaixo são **estimativas de projeto, a medir antes de aceitar a implementação**. Só o ADR-027 trouxe número medido (franjas, cerca de 2 ms a 150%).

| Item | Estimativa |
| --- | --- |
| Abertura do menu (NFR-03, ≤ 100 ms) | Sem custo novo de captura ou desfoque: a cópia e o desfoque do ADR-023 já são pagos. O primeiro quadro ganha as regiões das bordas e 2 desenhos recortados da cópia, algo como +1 a 3 ms |
| Quadro em animação a 150% | Líquido a mais que a bolha B: +2 a 4 ms (duas ampliações recortadas em vez de uma, mais arcos). Orçamento mantido em 33 ms |
| Memória | Nenhum bitmap de tela cheia novo. Reusa `_backdrop`; zerado ao fechar como hoje (ADR-023) |
| Movendo a janela | Sem cópia ao vivo, o Líquido cai para a superfície translúcida do ADR-037 até soltar; ao soltar, recopia uma vez |

**Critérios de aceite medidos no app:** abertura ≤ 100 ms (NFR-03) em 1080p a 100% e em 1440p a 150%; quadro médio abaixo de 33 ms durante o giro de aba. A medição usa o cronômetro de quadro que o `DiscRenderer` já tem.

**Rede de proteção:** o mecanismo do ADR-027 vale de novo. Três quadros seguidos acima de 33 ms tiram a lente das bordas (a placa de texto e o realce ficam); se ainda estourar, o menu volta ao Padrão até abrir de novo. A opção salva não muda.

## Acessibilidade

- **Contraste (NFR-06, texto ≥ 4,5:1).** A placa de texto fixa o pior caso, calculado (WCAG 2.x): claro 10,9:1 (texto #0f1320 sobre placa branca 78% em fundo preto); escuro 8,9:1 (texto #eef1f8 sobre placa #0a0c14 78% em fundo branco). Alto contraste preto: 21:1 no texto e 14,3:1 no foco (#000 sobre #1aebff). Alto contraste branco: 21:1 e 15,1:1 (#fff sobre #37006e).
- **Controle da opção.** O segmento selecionado usa #2563eb com texto branco, 5,1:1. A cor de destaque atual (#2f6bff) com texto branco dá 4,499:1, um fio abaixo do mínimo; vale conferir onde o app a usa com texto branco miúdo.
- **Nada só por cor ou por brilho.** O foco continua marcado por forma (preenchimento da fatia e bolha) e a ordem do teclado não muda. O brilho e a lente são decoração; nenhuma informação depende deles.
- **Teclado e leitor de tela.** Nada muda no uso por teclado do menu e das Configurações. O controle Padrão | Líquido é um grupo de opções com nome acessível "Vidro" e valores "Padrão" e "Líquido".
- **Sensibilidade a movimento.** A lente e o brilho em movimento são opcionais por duas vias: a opção Padrão e "Reduzir animações".

## Consequências

- Mais código no `DiscRenderer.Shapes.cs` (lente em faixas, arcos especulares, placa de texto) e uma leitura nova de alto contraste. Cada parte tem teste: cálculo de contraste das placas e das cores do sistema, e escolha de superfície por modo (translúcido, transparência desligada, alto contraste).
- A SPECS precisa de uma linha nova (FR do vidro líquido, junto de "Temas do vidro", Fase 8) e de NFR-06 citando alto contraste. O Anexo A do `PIZZA-PROJETO.md` ganha o resumo. Isso entra no PR de código, não nesta proposta.
- Risco principal: custo por quadro em telas grandes e em máquinas sem GPU forte. Coberto pela rede de proteção e pela opção Padrão.
- Risco de acessibilidade: o vidro vivo pode surpreender quem tem sensibilidade a movimento ou a transparência; por isso é opcional, desligado de fábrica, e sólido em alto contraste.
- A lente em 2 degraus é uma aproximação da prancha (que usa transição suave). Se o degrau ficar visível no app, a validação do mantenedor decide entre aceitar, usar 3 degraus ou voltar ao zoom único da bolha B.

## Alternativas descartadas

- **Não fazer nada (bolha B do ADR-027).** Segue válida como Padrão; não entrega a lente pedida no ROADMAP.
- **Acrylic, Mica ou desfoque do DWM (APIs de composição do Windows).** Não dobram o que está atrás, não recortam por fatia e dependem de recursos que mudam entre versões do Windows. O ADR-023 já escolheu o desfoque próprio.
- **Direct2D, shaders ou biblioteca de efeitos.** Contrariam a decisão "GDI+, sem Direct2D nem Vortice" (SPECS) e abrem superfície de ataque nova (CLAUDE.md, dependência só com justificativa e SBOM).
- **Refração por pixel na CPU (mapa de deslocamento).** É o mais fiel, mas custa ordens de grandeza a mais por quadro; estouraria o orçamento de 33 ms.
- **Líquido como padrão de fábrica.** Muda o visual de todos e pesa mais; fica como escolha do usuário.
- **Texto direto sobre o vidro, sem placa.** Não garante 4,5:1 em fundos extremos.

## Próximos passos

1. O mantenedor aprova, pede ajustes ou recusa a prancha VL-1.
2. Aprovada, este ADR passa a "Aceita" e a SPECS é emendada.
3. Só então o código entra, em PR próprio, com os testes acima e a medição de desempenho descrita.
