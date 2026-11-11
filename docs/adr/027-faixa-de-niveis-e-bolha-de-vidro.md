# ADR-027 — Faixa de níveis e bolha de vidro (Fase 4b)

- **Status:** Aceita pelo mantenedor em 11/11/2026, nas pranchas 4b-1, 4b-2 e 4b-3 do Dossiê. Altera o FR-20.
- **Data:** 2026-11-11

## Contexto

O mantenedor gostou da faixa "Nível de compressão" do design aprovado (Ref-PDF) e pediu que ela valesse para todas as abas, junto com o painel de opções. Pediu também um efeito de vidro em movimento (liquid glass), como uma bolha que passa de um item para o outro, em todo o projeto.

## Decisão

**Faixa de níveis**

- Acima do disco, uma faixa mostra os níveis da opção principal da fatia em hover: Qualidade em JPG, WEBP e AVIF; Resolução em PNG e PDF; Tamanho em ICO. Formato sem opções (TIFF, GIF) não mostra faixa.
- Soltar ou clicar num nível converte com esse nível. Com o cursor sobre a faixa, a roda troca o nível; no resto do menu, a roda continua trocando a aba.
- Soltar ou clicar na fatia converte direto com o nível padrão. Isso substitui o FR-20, em que soltar numa fatia com opções abria o painel.
- O botão direito na fatia abre o painel completo, com todas as opções do formato (como já previa o ADR-025).

**Bolha de vidro**

- O item ativo é marcado por uma bolha de vidro que desliza até o próximo na mola de 240 ms do giro de aba, crescendo levemente no meio do caminho. Vale para as abas, os níveis da faixa e as opções do painel. Nas fatias, o destaque de vidro da fatia em hover passa de uma fatia para a outra com o mesmo movimento.
- Versão escolhida: **B, com franjas coloridas na borda.** A bolha usa a cópia da tela que o vidro já tem (ADR-023), só em memória e zerada ao fechar.
- As franjas custam mais por quadro. A implementação mede o tempo de cada quadro; se passar do orçamento da animação, a bolha cai para a versão sem franjas, sem mudar a forma.
- No painel, a opção escolhida em cada grupo também é marcada pela bolha. O botão Converter continua na cor de destaque.

## Consequências

Converter fica a um gesto em quase todos os casos: arrastar, passar pelo nível e soltar. O painel deixa de interromper o fluxo e vira o caminho para quem quer ajustar tudo. A roda passa a ter dois papéis, conforme a posição do cursor; a dica do menu explica isso. Não há dependência nova nem captura de teclado (SEC-01): a roda continua chegando pelo hook de mouse que já existe.

## Alternativas descartadas

- Painel a cada soltura numa fatia com opções (FR-20 original): interrompe o gesto para quem só quer o padrão.
- Várias linhas de níveis, uma por opção: ocupa a tela do notebook; as demais opções ficam no painel completo.
- Roda sobre a fatia trocando o nível: a roda deixaria de trocar a aba sobre o disco.
- Bolha sem franjas (versão A): escolhida a versão com franjas; a sem franjas fica só como modo de economia.
- Opção escolhida do painel em azul sólido: substituída pela bolha, para o vidro ser o mesmo em todo o Pizza.
