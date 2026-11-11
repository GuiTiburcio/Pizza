# ADR-028 — Avisos e Arquivos em vidro translúcido (Fase 4c)

- **Status:** Aceita pelo mantenedor em 11/11/2026, na prancha 4c-1 do Dossiê.
- **Data:** 2026-11-11

## Contexto

A Fase 4c leva o vidro do menu para os avisos do canto da tela e para a janela Arquivos. O vidro do menu desfoca uma cópia da tela tirada quando ele abre (ADR-023), mas o aviso de erro e Arquivos podem ficar muito tempo abertos, e a cópia ficaria desatualizada. O acrílico do Windows 11 foi testado e apareceu como cor sólida no PC do mantenedor (efeitos visuais personalizados), o que também é comum em PCs corporativos.

## Decisão

- Avisos e Arquivos usam **vidro translúcido com transparência por pixel** (`UpdateLayeredWindow`): um véu do tema, borda clara e brilho no alto, por onde o fundo real aparece ao vivo. Sem desfoque e sem cópia da tela.
- O véu é denso (87% no claro e 94% no escuro) para o texto manter o contraste do NFR-06 sobre qualquer fundo.
- Cantos de 20 px desenhados com suavização, como na prancha. Os dois temas seguem o Windows.
- A bolha de vidro B (ADR-027) corre na barra de progresso e marca a linha em foco em Arquivos.
- Textos, botões, alvos de 44 px, tempos, esmaecer, "Não perturbe", leitor de tela e ordem dos cartões não mudam (FR-40 a FR-44).

## Consequências

Funciona igual em qualquer Windows 10 e 11, sem depender de configuração do sistema, e nenhum pixel da tela fica na memória do Pizza por causa dessas janelas. O vidro dos avisos não desfoca: é mais discreto que o do menu. O desenho dos cartões e de Arquivos passa de GDI para GDI+.

## Alternativas descartadas

- **Cópia da tela, como o menu:** fundo desatualizado nos avisos que ficam abertos; o mantenedor preferiu a transparência ao vivo.
- **Acrílico do Windows 11:** apareceu sólido no PC do mantenedor e exige cantos de 8 px.
- **Cópia renovada a cada segundo:** exigiria esconder os avisos de capturas e compartilhamento de tela.
