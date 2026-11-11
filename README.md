<div align="center">

![Pizza — conversão de arquivos no gesto](docs/assets/pizza-overview.svg)

[![CI](https://github.com/GuiTiburcio/Pizza/actions/workflows/ci.yml/badge.svg)](https://github.com/GuiTiburcio/Pizza/actions/workflows/ci.yml)
[![CodeQL](https://github.com/GuiTiburcio/Pizza/actions/workflows/codeql.yml/badge.svg)](https://github.com/GuiTiburcio/Pizza/actions/workflows/codeql.yml)
![Windows 10 e 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
[![GPL-3.0](https://img.shields.io/badge/licen%C3%A7a-GPL--3.0-blue)](LICENSE)

**Ctrl + arrastar. Solte onde estiver. Clique no formato.**

*Afinal, tudo acaba em pizza.*

Conversor de imagens para Windows com menu circular, processamento local e arquivo original preservado.

[Instalar](docs/INSTALACAO.md) · [Guia de TI](docs/TI-GPO.md) · [Como usar](docs/USO.md) · [Dossiê e design](docs/dossie/README.md) · [Documentação](docs/README.md) · [Próximas etapas](docs/ROADMAP.md)

</div>

## O que é o Pizza

O Pizza abre um menu de formatos quando você começa a arrastar um arquivo segurando Ctrl. Você pode soltar o mouse em qualquer posição e escolher com um clique, ou arrastar diretamente até uma fatia. A saída fica ao lado do original.

O projeto está em **prévia de desenvolvimento**. Imagens e Imagem → PDF estão implementados. Operações em PDF, documentos e áudio fazem parte do roteiro. Instaladores assinados e atualização automática ainda não estão disponíveis.

## Como funciona

<p align="center"><img src="docs/imagens/menu.png" alt="Menu do Pizza aberto sobre um arquivo JPG: abas Imagem, PDF, Documento e Áudio, e seis fatias de formato com o nome e a descrição acompanhando a curva; a fatia AVIF está em destaque" width="440"></p>

1. Abra o Pizza; ele fica na bandeja do Windows.
2. Segure **Ctrl** e comece a arrastar uma imagem no Explorer ou na área de trabalho.
3. Solte o mouse **onde estiver**. A Pizza continua aberta para você clicar no formato. Soltar diretamente numa fatia também funciona.
4. Use a roda para ver mais opções. Os pontos do miolo indicam as voltas quando a aba tem mais de seis formatos.
5. Escolha as opções, quando houver, e confirme. **Esc** cancela; depois da soltura, clicar em outro aplicativo também fecha o menu.

O cartão de resultado permite abrir a saída ou mostrá-la na pasta. O menu do ícone dá acesso a **Arquivos...**, com o histórico da sessão, e **Sair**.

## Formatos desta prévia

| Entrada | Saída |
| --- | --- |
| JPG/JPEG, PNG, WEBP, AVIF, TIFF/TIF e GIF | JPG, PNG, WEBP, AVIF, PDF, TIFF, ICO e GIF |

O formato de origem é omitido. GIFs são tratados como imagem estática nesta etapa. HEIC, BMP, vídeo, áudio e conversões de documentos ainda não estão disponíveis. Por enquanto, um arquivo por vez.

## Processamento local

- Sem conta, telemetria ou upload de arquivos.
- Original aberto somente para leitura; nomes existentes recebem um número, sem sobrescrever.
- Conversor em processo isolado do Windows, sem rede, recebendo os arquivos por handles.
- Metadados pessoais removidos por padrão; preservação opcional nos formatos compatíveis.
- Registros técnicos locais sem nomes ou conteúdo dos arquivos.

Requisitos e limites estão nas [SPECS](docs/SPECS.md). Recursos futuros descritos ali não representam funcionalidades já entregues; veja o [roteiro](docs/ROADMAP.md).

## Experimentar e desenvolver

Os builds de desenvolvimento são produzidos pelo [GitHub Actions](https://github.com/GuiTiburcio/Pizza/actions/workflows/ci.yml). Abra uma execução bem-sucedida e baixe o artefato **pizza-win-x64**. Consulte os [requisitos e o preparo do Worker](docs/USO.md).

Para desenvolver, use Windows x64, **.NET 10 SDK** e **Visual Studio Build Tools com Desenvolvimento para desktop com C++**, exigido pelo NativeAOT:

```powershell
dotnet restore Pizza.slnx --locked-mode
dotnet build Pizza.slnx -c Release --no-restore
dotnet test Pizza.slnx -c Release --no-build
```

Para gerar uma cópia local de teste:

```powershell
.\tools\publicar-dev.ps1
```

Builds locais são para desenvolvimento. A distribuição oficial deve vir do CI. Veja [como contribuir](CONTRIBUTING.md).

## Organização

| Pasta | Conteúdo |
| --- | --- |
| `src/Pizza.App` | Residente NativeAOT: bandeja, gesto, menu, painéis e notificações |
| `src/Pizza.Core` | Estados, geometria, catálogo, IPC e gravação segura |
| `src/Pizza.Converters` | Conversores executados pelo Worker |
| `src/Pizza.Worker` | Processo de conversão isolado |
| `tests` | Testes unitários, do drop, conversores e isolamento real |
| `docs` | Dossiê, especificações, uso, design e decisões |
| `tools` | Publicação de desenvolvimento, sons e verificação de logs |

## Inspiração

O Pizza nasceu depois que vi, por acaso, um vídeo no **TikTok**. Gostei da ideia de converter arquivos com um menu circular e decidi criar minha própria implementação para Windows. O vídeo inspirou o conceito; este repositório registra a construção do meu projeto. O nome vem do formato do menu, que lembra uma pizza fatiada, e do ditado "afinal, tudo acaba em pizza": aqui, todo arquivo arrastado acaba nela.

O link e o autor do vídeo ainda não foram identificados. Quando eu localizar a referência, adicionarei o crédito aqui. O planejamento e o design foram elaborados com o auxílio do Claude; o desenvolvimento conta com apoio do Claude e do Codex.

## Créditos e colaboradores

| Participação | Crédito |
| --- | --- |
| Idealização, decisões de produto e manutenção | [Guilherme Tiburcio](https://github.com/GuiTiburcio) |
| Apoio ao planejamento, design e desenvolvimento | [Claude / Claude Design](https://claude.ai/) |
| Apoio ao desenvolvimento, testes, revisão de código e organização do repositório | [Codex / OpenAI](https://github.com/openai/codex) |

Claude e Codex são as ferramentas de IA utilizadas na construção do projeto. As contribuições assistidas pelo Codex também recebem crédito de coautoria nos commits correspondentes.

## Dossiê e demonstração

O [**Pizza — Dossiê do Projeto**, criado no Claude Design](https://claude.ai/artifact/LMrbTPnaY6EbVK1UPjcVDs#page-dcb81aee8d83), reúne a referência visual do projeto. As [pranchas do dossiê](docs/dossie/README.md) também estão neste repositório, sem precisar de login no Claude. O que foi descartado no caminho, e por quê, está em [ideias descartadas](docs/ideias-descartadas/README.md).

Uma demonstração prática em vídeo está planejada e será adicionada quando estiver pronta.

## Contribuição e licença

Leia [CONTRIBUTING.md](CONTRIBUTING.md); vulnerabilidades devem seguir [SECURITY.md](SECURITY.md).

Código sob [GPL-3.0](LICENSE). Dependências e licenças estão descritas em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
