# Pacotes do Pizza

Manifestos declarativos das duas edições ([ADR-034](../docs/adr/034-brotinho-jumbo-motor-de-documentos.md), etapa 4; conteúdo de cada pacote no [ADR-039](../docs/adr/039-formatos-por-pacote-e-fatias-incompativeis.md)). Esta pasta é a **única** fonte das regras de "o que entra em cada zip". Não há binários aqui, só nomes de arquivo, e nenhuma outra pasta repete estas listas.

| Manifesto | Edição | Conteúdo |
| --- | --- | --- |
| `comum.json` | (base) | Residente, Worker, bibliotecas nativas do Worker, sons, `LICENSE` e `THIRD-PARTY-NOTICES.md` |
| `brotinho.json` | Pizza Brotinho | O comum. **Nunca** leva `Pizza.Docs*` nem a pasta `motor\` (campo `proibidos`) |
| `jumbo.json` | Pizza Jumbo | O Brotinho, mais `Pizza.Docs.*` e `motor\` (LibreOffice extraído pelo `tools/baixar-motor.ps1`, versão e SHA-256 fixos) |

## Formato

- `herda`: manifestos aplicados antes deste. O Jumbo herda o Brotinho, que herda o comum.
- `origens` (no `comum.json`): `ignorar` (padrões que podem existir na pasta de publicação sem entrar no pacote, como `*.pdb`) e `compartilhados` (arquivos que existem em duas pastas de publicação e precisam ser idênticos).
- `entradas`: `origem` (`pizza`, `docs`, `motor` ou `raiz`, a raiz do repositório), `arquivos` e, opcionalmente, `destino`. Uma entrada com `"arvore": true` copia a pasta inteira (o motor) e confere os arquivos de `exige`.
- `proibidos`: padrões que o destino desta edição não pode ter. Valem só para o manifesto que os declara; não são herdados.

## Montagem

`tools/montar-edicao.ps1 -Edicao brotinho|jumbo` lê os manifestos e monta a pasta do pacote. Ele **falha** se o manifesto citar um arquivo que não existe, se a pasta de publicação trouxer um arquivo que nenhum manifesto cita, ou se a edição tiver um arquivo proibido. O script não acessa a rede.

Pastas de entrada (padrões): `artifacts/previa/Pizza` (publish do `Pizza.App` e do `Pizza.Worker`), `artifacts/previa/Docs` (publish do `Pizza.Docs`) e `artifacts/previa/motor` (saída do `tools/baixar-motor.ps1`).

## Como o CI usa

O `previa.yml` publica os projetos, baixa o motor (com hash fixo), monta as duas pastas e gera, a partir do mesmo commit:

- `Pizza-Brotinho-<versão>.zip` e `Pizza-Jumbo-<versão>.zip`;
- um SBOM por edição (`Pizza-<Edição>-<versão>.sbom.cdx.json`): o do Brotinho vem do syft mais as dependências nativas do escopo `previa`; o do Jumbo parte do mesmo SBOM e acrescenta também o escopo `jumbo` (o motor), por `tools/sbom-nativos.ps1`;
- o `SHA256SUMS` com o hash de cada zip e o atestado de proveniência dos dois.

Nenhum instalador ou pacote sai de build local (CLAUDE.md, invariante 8).

## Pendente

- O Jumbo já leva o `Pizza.Docs` e o motor (o download com versão e SHA-256 fixos já existe no CI, então o job não ficou condicionado). **O enxugamento do motor** (dicionários, ~455 MB extraídos) ainda não foi feito; o zip do Jumbo sai grande. A entrada `motor` copia a árvore inteira até essa etapa.
- O syft não analisa a pasta do motor (seria lento e barulhento); o motor entra no SBOM do Jumbo só pelo `build/native-deps.json`.
