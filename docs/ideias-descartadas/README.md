# Ideias descartadas

O que ficou pelo caminho, e por quê. Serve para não voltar a discutir o que já foi decidido e para entender por que o Pizza é como é. O registro completo de cada decisão continua no ADR indicado; os ADRs não saem de [`docs/adr`](../adr/), para que os links antigos sigam funcionando.

Para reabrir uma ideia, crie um ADR novo que substitui o anterior (veja [`docs/adr/README.md`](../adr/README.md)).

## Produto

| Ideia | Por que saiu | Onde |
| --- | --- | --- |
| Repositório privado até o v1.0 | O Pizza passou a ser gratuito e de código aberto | [ADR-013](../adr/013-repositorio-privado.md) → [ADR-014](../adr/014-gratuito-gpl.md) |
| Versão paga, com chave e plano | Gratuito sob GPL-3.0; a aba "Licença" saiu da Configuração | [ADR-014](../adr/014-gratuito-gpl.md) |
| 15 políticas de TI no v1 | Ficam 7, as essenciais | [ADR-019](../adr/019-simplificacoes-do-v1.md) |
| Opções "Mostrar dica" e "Cor de destaque" | Extras de interface que ninguém pediu | [ADR-019](../adr/019-simplificacoes-do-v1.md) |
| "Pausar por 1 hora" e "Sobre" no menu do ícone | Pausar o gesto reverteria uma simplificação; Sobre vai para a Configuração | [ADR-020](../adr/020-menu-do-icone.md) |

## Gesto e menu

| Ideia | Por que saiu | Onde |
| --- | --- | --- |
| Hook de teclado para o Esc | Nenhuma captura de teclado (SEC-01); o Esc chega pelo próprio arraste ou pela janela do menu | [ADR-016](../adr/016-esc-sem-hook-de-teclado.md) |
| Soltar fora do disco cancela | Soltar em qualquer lugar mantém o menu para escolher com um clique, sem precisar mirar | [ADR-025](../adr/025-escolha-por-clique-apos-o-gesto.md) |
| Fechar a escolha em prazo fixo, ou só depois de 30 s | Fecharia enquanto a pessoa navega no touchpad, ou ocuparia demais uma tela pequena: o prazo é de 10 s sem interação | [ADR-025](../adr/025-escolha-por-clique-apos-o-gesto.md) |
| Texto das fatias na horizontal com arco leve (versão A) | A descrição longa não cabia nas fatias laterais sem quebrar a linha; ficou a curva completa (B) | [Dossiê](../dossie/README.md#referência--texto-das-fatias) |
| Marca ✓ na fatia escolhida | A fatia só fica em destaque, com vidro fosco e escuro; o ✓ aparece apenas no aviso de concluído | Pedido do mantenedor em 10/11/2026 |

## Visual

| Ideia | Por que saiu | Onde |
| --- | --- | --- |
| Direct2D (Vortice) | Dependências novas, uma em beta | [ADR-023](../adr/023-vidro-gdiplus-desfoque-proprio.md) |
| Acrylic do Windows (DWM) | Só funciona em janela ativa | [ADR-023](../adr/023-vidro-gdiplus-desfoque-proprio.md) |
| Composição do Windows (`HostBackdropBrush`) | Desenhou preto na janela do disco | [ADR-023](../adr/023-vidro-gdiplus-desfoque-proprio.md) |
| Vidro sem desfoque | O fundo nítido compete com as fatias | [ADR-023](../adr/023-vidro-gdiplus-desfoque-proprio.md) |
| Avisos nativos do Windows (toasts) | Ficam no histórico e são espelhados no celular; viraram janelas próprias | [ADR-018](../adr/018-notificacao-e-painel-proprios.md) |

## Arquitetura e segurança

| Ideia | Por que saiu | Onde |
| --- | --- | --- |
| Converter documentos pelo Word (COM) | Arquivo da internet abriria fora do sandbox; no v1, só o LibreOffice isolado | [ADR-019](../adr/019-simplificacoes-do-v1.md) |
| Worker aquecido e reaproveitado | Um arquivo malicioso alcançaria os próximos; agora é um Worker por arquivo | [ADR-019](../adr/019-simplificacoes-do-v1.md) |
| Baixar o LibreOffice pelo Pizza | Nenhum download além da atualização | [ADR-019](../adr/019-simplificacoes-do-v1.md) |
| Pipe com nome, ACL e verificação de PID | Pipes sem nome, entregues só ao Worker, são mais simples e fecham o acesso entre usuários | [ADR-021](../adr/021-pipe-sem-nome.md) |
| Histórico do painel gravado em disco | Fica só em memória, durante a sessão | [ADR-018](../adr/018-notificacao-e-painel-proprios.md) |
| PDFsharp para Imagem → PDF | Passou a ser AGPL na versão 6.5.0; o Pizza usa um gerador próprio | [ADR-022](../adr/022-imagem-pdf-gerador-proprio.md) |
| SharpFuzz para os testes com arquivos corrompidos | Só observa código C#, e quem lê os arquivos é o libvips nativo; há um teste próprio | [SPECS §2.1](../SPECS.md) (TST-03) |
