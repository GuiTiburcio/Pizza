# ADR-023 — Vidro com GDI+ e desfoque próprio

- **Status:** Aceita. Substitui, no ADR-003, o desenho com Direct2D (Vortice) e o DWM Acrylic.
- **Data:** 2026-11-10

## Contexto

O visual do Dossiê pede bordas suaves, transparência por pixel, degradês, sombras e vidro desfocado. O GDI não faz nada disso. A Vortice traria três pacotes ao residente, um deles em beta. Duas provas técnicas mostraram que o desfoque oficial do Windows não serve ao disco: o do DWM vira um quadrado cinza e o da API de composição vira um círculo preto. O disco nunca pode ser a janela ativa, porque o Explorer precisa manter o arraste.

## Decisão

- O desenho usa o GDI+ do próprio Windows (`gdiplus.dll`, chamadas diretas), sem dependência nova, em janela com transparência por pixel (`UpdateLayeredWindow`).
- O vidro é desfocado pelo próprio Pizza. Ao abrir o menu, ele copia só o trecho da tela atrás do disco, desfoca e satura na memória e descarta a cópia ao fechar, zerando o buffer. A cópia nunca é gravada nem registrada (PRIV-16).
- Com "Efeitos de transparência" desligado no Windows, o vidro vira branco sólido 92%, como o Dossiê prevê. O desfoque próprio funciona também no Windows 10.

## Consequências

Mesmo visual no Windows 10 e 11, sem API não documentada nem pacote novo. O fundo do vidro fica parado enquanto o menu está aberto. Desenhar na CPU basta para o disco de 380 px, e a medição fica para a Fase 5.

## Alternativas descartadas

- Direct2D via Vortice: dependências novas, uma em beta.
- DWM Acrylic (`DWMWA_SYSTEMBACKDROP_TYPE`): só funciona em janela ativa.
- Composição do Windows (`HostBackdropBrush`): desenhou preto na janela do disco.
- `SetWindowCompositionAttribute`: API não documentada.
- Vidro sem desfoque: o fundo nítido compete com as fatias.
