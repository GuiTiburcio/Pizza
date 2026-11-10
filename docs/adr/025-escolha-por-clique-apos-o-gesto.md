# ADR-025 — Escolher por clique depois do gesto

- **Status:** Aceita pelo mantenedor em 10/11/2026.
- **Altera:** FR-07/20/21 e SEC-03 quanto ao período posterior ao arraste.

Ctrl + arrastar continua abrindo o menu. O usuário pode soltar o mouse em qualquer posição da área de trabalho: o arquivo fica preparado, e a Pizza permanece aberta para escolher uma fatia com um clique. Não há zona obrigatória no centro. Soltar diretamente sobre uma fatia continua selecionando o formato como atalho.

Depois da soltura, Esc e perder o foco cancelam. As teclas chegam somente à própria janela; durante o arraste, Esc continua sendo tratado pelo OLE. A primeira soltura e eventos atrasados não podem cancelar a escolha nem selecionar duas vezes.

A camada de captura recebe apenas CF_HDROP válido durante o gesto e devolve apenas COPY/NONE, respeitando os efeitos oferecidos pela origem. Preparar o arquivo não cria uma cópia física e não altera o original. Finalizado o arraste, a camada de tela inteira é escondida.

Verificação: TST-01 para estados, dados inválidos e timers atrasados; TST-10 para integridade da origem; teste manual com Explorer, vários monitores e escala de 100% a 200%.
