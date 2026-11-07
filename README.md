# Pizza

Conversor de arquivos para Windows que funciona no gesto: **segure Ctrl, arraste o arquivo e solte no formato** num menu circular de vidro. Tudo offline, leve e rápido.

> **Status:** Fase 0 (ambiente). Ainda não há código; a especificação e o design estão aprovados.

## Como funciona

1. Segure **Ctrl** e comece a arrastar um arquivo (área de trabalho ou Explorer).
2. O menu radial abre no cursor, já na aba do tipo do arquivo. O scroll troca a aba.
3. Solte na fatia do formato desejado, escolha as opções e pronto: o arquivo novo aparece ao lado do original.

O original nunca é alterado e nada sai do computador.

## Documentação

- [Especificação completa](docs/PIZZA-PROJETO.md): produto, UX, arquitetura, conversores, fases.
- [Registro de decisões (ADRs)](docs/adr/).

## Ambiente de desenvolvimento

| Ferramenta | Para quê |
| --- | --- |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Compilar e testar |
| Visual Studio Build Tools com "Desenvolvimento para desktop com C++" | Linker exigido pelo NativeAOT |
| VS Code + extensão C# Dev Kit | Editor |
| Git + GitHub CLI (`gh`) | Versionamento e PRs |

As instruções de build entram aqui na etapa 0b, junto com a solução.

## Forma de trabalho

- Branch `main`; trabalho em `feat/…`, `fix/…`, `docs/…`, sempre por pull request.
- Commits em [Conventional Commits](https://www.conventionalcommits.org/pt-br/), em português: `feat(overlay): destaca fatia no hover`.
- Nomes no código em inglês; commits, documentação e interface em português.

## Licença

Código proprietário. Todos os direitos reservados. A licença final será decidida no v1.0 (ver [ADR-013](docs/adr/013-repositorio-privado.md)).
