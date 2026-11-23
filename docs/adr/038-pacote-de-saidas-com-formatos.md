# ADR-038 — Pacote de saídas com um formato por parte

- **Status:** Aceita pelo mantenedor em 13/11/2026.
- **Data:** 2026-11-13

## Contexto

O DOCX → Markdown (ADR-034, Pizza Brotinho) segue o jeito do Obsidian: o texto num `.md` e cada imagem num arquivo ao lado, citado no texto por `![[relatorio - imagem 1.png]]`. São saídas de formatos diferentes que saem da mesma conversão.

O leitor DOCX compartilhado com HTML filtra os metadados no Worker antes de montar a saída, sem decodificar ou recomprimir a imagem. Em PNG, remove os chunks de texto, data e proveniência `caBX`. Em JPEG, descarta comentários, XMP e segmentos de aplicação sem papel técnico conhecido. Em GIF, conserva quadros, transparência, texto visível, repetição e perfil ICC, descartando comentários e outras extensões de aplicação. Em WebP, conserva pixels, animação e perfil ICC, removendo XMP e chunks desconhecidos. Os perfis ICC e os dados comprimidos permanecem intactos.

Para EXIF em PNG, JPEG e WebP, a saída recebe um IFD reconstruído apenas com orientação, resoluções e unidade. Os demais campos e diretórios EXIF não são copiados. Um contêiner truncado ou campos técnicos incoerentes retornam `CORRUPTED`. Estes filtros cobrem metadados do contêiner. O conteúdo visível da imagem e as informações internas de perfis de cor são preservados.

O pacote de partes de hoje (ADR-033, `PZB1`) leva saídas de um formato só, conferidas pelos bytes iniciais do formato da extensão (`%PDF-` no Dividir, BOM do UTF-8 no CSV). Os nomes são sempre do residente (SEC-15, SEC-30). O Worker não sabe o nome do arquivo e, por isso, não tem como escrever no `.md` o nome final das imagens.

## Decisão

- **Pacote `PZB2`:** "PZB2", a quantidade de partes (int32) e, para cada parte, um byte de formato, o tamanho (int64) e os bytes. O `PZB1` continua sendo lido igual, e Dividir, Juntar e CSV não mudam.
- **Lista fixa do residente (SEC-13):** a extensão da conversão, que vem do catálogo, diz quais formatos o pacote pode ter. Para `md`: exatamente uma parte Markdown, a primeira, e depois só PNG, JPEG, GIF ou WEBP. Formato fora da lista, ordem errada ou contagem errada: nada é gravado (`WORKER_CRASHED`).
- **Conferência sem interpretar:** cada imagem precisa começar pela assinatura do seu formato. O Markdown precisa ser UTF-8 válido.
- **Marcador trocado pelo residente:** onde vai o nome da imagem N, o Worker escreve o marcador `U+E000`, o número N e `U+E000` (caractere de uso privado; o Worker o tira do texto do documento). O residente:
  1. grava as imagens com o nome que ele mesmo escolhe: o nome do original sem `[ ] | # ^`, que o Obsidian não aceita em links, mais ` - imagem N`;
  2. troca só os marcadores pelos nomes finais;
  3. grava o `.md` por último.

  Marcador malformado ou com número fora das imagens recusa o pacote inteiro.
- **Ou saem todas, ou nenhuma:** se um nome falhar, as partes já gravadas são apagadas, como no Dividir.

## Consequências

### Correção de limites e controle da finalização — 15/11/2026

A validação do tamanho bruto do PZB2 não basta: o nome final de uma imagem pode ser maior que seu marcador e aparecer várias vezes. O residente deve limitar a escrita do Markdown já transformado ao saldo do teto agregado de 2 GiB, descontando os bytes das imagens. O fluxo continua recusando excesso, sem truncar conteúdo nem reduzir qualidade, com rollback das imagens já nomeadas.

A validação UTF-8 e a substituição de marcadores verificam cancelamento, pausa e prazo ativo por blocos, reutilizando CheckFinalization. As verificações também cercam o descarregamento final do buffer; não basta conferir apenas antes e depois de percorrer o arquivo inteiro. O formato do PZB2 e a compatibilidade do PZB1 não mudam.

Regressões cobrem expansão com nomes Unicode, limite agregado exato e um byte acima, ausência de parciais após uma imagem nomeada, cancelamento durante texto sem marcadores e com referências repetidas, prazo antes do flush e controle durante validação UTF-8. O teto reduzido interno dos testes nunca pode ultrapassar o teto de produção.

O PDF → PNG/JPG por página (Pizza — VI) não precisa do `PZB2`. Ele continua no `PZB1`, só com mais assinaturas na lista (PNG e JPEG).

O residente passa a ler o `.md` em fluxo para trocar os marcadores. É a única parte de conteúdo que ele toca, e só para trocar o marcador, que tem tamanho fixo de formato.

## Alternativas descartadas

- **Mandar o nome do arquivo ao Worker:** o nome é dado da pessoa, e o Worker não precisa dele (SEC-15).
- **Pasta de imagens com links relativos:** o residente passaria a criar pastas, o que mexe mais no SEC-30.
- **Gravar o `.md` e as imagens em duas conversões:** perde o "ou saem todas, ou nenhuma".
