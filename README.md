# Pizza

Conversor de arquivos para Windows que funciona no gesto: **segure Ctrl, arraste o arquivo e solte no formato** num menu circular de vidro. Tudo offline, leve e rápido.

> **Status:** Fase 1, etapa 1b: o Pizza fica nos ícones ocultos da barra de tarefas. Ctrl + arrastar abre um disco simples que recebe o arquivo; ainda não converte nada. Para encerrar, clique com o botão direito no ícone e escolha **Sair**.

## Como funciona

1. Segure **Ctrl** e comece a arrastar um arquivo (área de trabalho ou Explorer).
2. O menu radial abre no cursor, já na aba do tipo do arquivo. O scroll troca a aba.
3. Solte na fatia do formato desejado, escolha as opções e pronto: o arquivo novo aparece ao lado do original.

O original nunca é alterado e nada sai do computador.

## Documentação

- [SPECS](docs/SPECS.md): fonte da verdade, com requisitos de segurança e privacidade.
- [Visão geral do projeto](docs/PIZZA-PROJETO.md): produto, UX, arquitetura, conversores, fases.
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

Gratuito e de código aberto, sob a [GPL-3.0](LICENSE) (ver [ADR-014](docs/adr/014-gratuito-gpl.md)). Para reportar falhas, veja [SECURITY.md](SECURITY.md).
