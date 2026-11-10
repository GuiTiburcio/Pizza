# ADR-025 — Soltar em qualquer lugar e escolher com um clique

- **Status:** Aceita. Altera a regra "soltar fora do disco cancela" do WF-1.
- **Data:** 2026-11-10

## Contexto

Mirar numa fatia ainda durante o arraste exige precisão. O mantenedor quer soltar o arquivo onde estiver e escolher o formato depois, com um clique.

## Decisão

- Soltar direto numa fatia continua convertendo na hora, como atalho.
- Soltar em qualquer outro lugar, com o menu aberto e um arquivo só, deixa o menu na tela em modo de escolha: o hover segue o mouse, a roda troca a aba e um clique numa fatia converte.
- Esc ou clique fora fecham sem converter. Para o Esc chegar sem hook de teclado (SEC-01), a camada que cobre a tela recebe o foco nesse modo; o clique fora cai nela e não chega à janela de baixo. Se outra janela tomar o foco, a escolha termina.
- Esc apertado ainda durante o arraste continua cancelando, como antes.
- Soltar fora continua sem copiar nem mover nada (SEC-05): a camada sempre responde que não aceita o drop.

## Consequências

O gesto fica mais tolerante, sem custo de segurança. Enquanto a escolha está aberta, um clique em outra janela só fecha o menu.

## Alternativas descartadas

- Fechar também por tempo: o menu sumiria enquanto a pessoa ainda decide.
