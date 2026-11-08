# Pizza

Conversor de arquivos para Windows que funciona no gesto: **segure Ctrl, arraste o arquivo e solte no formato** num menu circular de vidro. Tudo offline, leve e rápido.

> **Status:** Fase 1, etapa 1a: prova de conceito do gesto. Ctrl + arrastar abre um disco simples que recebe o arquivo; ainda não converte nada. Enquanto não há bandeja, **Ctrl + Alt + F12** encerra o programa.

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

### Compilar e testar

```bash
dotnet build Pizza.slnx
```

```bash
dotnet test Pizza.slnx
```

### Gerar o `Pizza.exe` nativo (NativeAOT)

```bash
dotnet publish src/Pizza.App -c Release -o artifacts/publish/app
```

Se o publish falhar com `'vswhere.exe' não é reconhecido`, falta a pasta do instalador do Visual Studio no PATH. Adicione `C:\Program Files (x86)\Microsoft Visual Studio\Installer` ao PATH do usuário e abra um terminal novo.

### Estrutura

| Projeto | Papel |
| --- | --- |
| `src/Pizza.App` | Residente `Pizza.exe` (NativeAOT): bandeja, hooks, overlay, painel, notificações |
| `src/Pizza.Core` | Estados, configuração, logs, contratos IPC |
| `src/Pizza.Worker` | `Pizza.Worker.exe`: conversão sob demanda |
| `src/Pizza.Converters` | `IConverter` e implementações por família |
| `tests/*` | Testes xUnit; `tests/samples` guarda arquivos de amostra pequenos |

O CI (GitHub Actions, Windows) compila, testa e publica o `Pizza.exe` nativo a cada PR e a cada push na `main`.

## Forma de trabalho

- Branch `main`; trabalho em `feat/…`, `fix/…`, `docs/…`, sempre por pull request.
- Commits em [Conventional Commits](https://www.conventionalcommits.org/pt-br/), em português: `feat(overlay): destaca fatia no hover`.
- Nomes no código em inglês; commits, documentação e interface em português.

## Licença

Código proprietário. Todos os direitos reservados. A licença final será decidida no v1.0 (ver [ADR-013](docs/adr/013-repositorio-privado.md)).
