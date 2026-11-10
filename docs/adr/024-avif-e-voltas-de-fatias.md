# ADR-024 — AVIF e voltas de fatias

- **Status:** Aceita. Altera o ADR-012.
- **Data:** 2026-11-10

## Contexto

O ADR-012 limitava a aba Imagem a 6 fatias e descartava os últimos formatos da ordem. O mantenedor pediu AVIF, que o Pizza já lê e grava sem dependência nova, e quer que nenhum formato fique de fora: a pizza gira para mostrar os demais.

## Decisão

- Ordem da aba Imagem: JPG, PNG, WEBP, AVIF, Imagem → PDF, TIFF, ICO, GIF, sem o formato de origem.
- Com mais de 6 formatos, eles se dividem em voltas de até 6 fatias. A roda do mouse percorre as voltas da aba antes de passar à próxima aba; a última volta pode ficar incompleta. Pontos no miolo mostram em qual volta o menu está.
- Ao girar de volta, as fatias giram em volta do miolo, como na troca de aba (FR-04).

## Consequências

Nenhum formato fica de fora, sem gestos novos nem teclas durante o arraste. Trocar de aba passa a exigir mais giros da roda em abas grandes; os pontos no miolo deixam isso visível.

## Alternativas descartadas

- Fatia "Mais" que gira ao parar o cursor: perde uma fatia por volta.
- Shift + roda: exige segurar mais uma tecla durante o arraste.
- AVIF no fim da ordem: na prática, quase nunca apareceria.
