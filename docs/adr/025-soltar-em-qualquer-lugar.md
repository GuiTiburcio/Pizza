# ADR-025 — Soltar em qualquer lugar e escolher com um clique

- **Status:** Aceita. Altera a regra "soltar fora do disco cancela" do WF-1.
- **Data:** 2026-11-10

## Contexto

Mirar numa fatia ainda durante o arraste exige precisão. O mantenedor quer soltar o arquivo onde estiver e escolher o formato depois, com um clique.

## Decisão

- Soltar direto numa fatia continua convertendo na hora, como atalho.
- Soltar em qualquer outro lugar, com o menu aberto e um arquivo só, deixa o menu na tela em modo de escolha: o hover segue o mouse, a roda troca a aba e um clique numa fatia converte.
- O botão direito numa fatia também escolhe: abre o painel de opções do formato e, em formato sem opções, converte direto. No disco, fora das fatias, o botão direito não faz nada. Fora do disco, ele fecha o menu e segue para o Windows como um clique comum: o hook de mouse, que roda na mesma thread do menu, esconde a camada antes de o Windows escolher a janela que recebe o clique. O hook continua só de mouse e não registra a posição (SEC-01, SEC-04).
- Esc ou clique fora fecham sem converter. Para o Esc chegar sem hook de teclado (SEC-01), a camada que cobre a tela recebe o foco nesse modo, e o disco volta para cima dela. O clique fora cai na camada e só fecha quando o botão sobe, para nem o apertar nem o soltar chegarem à janela de baixo. Se outra janela tomar o foco, a escolha termina.
- Depois de 10 segundos sem nenhuma interação (mouse, roda ou teclado), o menu fecha; cada interação reinicia a contagem. Assim o menu não ocupa a tela de quem só tem o notebook, a cópia da tela usada no vidro (ADR-023) não fica na memória por tempo indeterminado e a camada não fica presa se o Windows negar o foco.
- Esc apertado ainda durante o arraste continua cancelando, como antes.
- Soltar fora continua sem copiar nem mover nada (SEC-05): a camada sempre responde que não aceita o drop.

## Consequências

O gesto fica mais tolerante, sem custo de segurança. Enquanto a escolha está aberta, um clique em outra janela só fecha o menu.

## Alternativas descartadas

- Prazo fixo, contado desde que o menu ficou parado: fecharia enquanto a pessoa ainda navega pelas abas no touchpad. Por isso o prazo conta só o tempo sem interação.
- 30 segundos: ocupa demais uma tela pequena de notebook.
