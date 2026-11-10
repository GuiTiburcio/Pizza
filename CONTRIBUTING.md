# Contribuir com o Pizza

Use uma branch por mudança e envie pull request para `main`. Código em inglês; interface, documentação e commits em português. Leia [CLAUDE.md](CLAUDE.md) e [SPECS](docs/SPECS.md). Mudanças de decisão ganham um [ADR](docs/adr/README.md).

Ambiente: Windows x64, .NET 10 SDK e Build Tools com C++ para NativeAOT.

```powershell
dotnet restore Pizza.slnx --locked-mode
dotnet build Pizza.slnx -c Release --no-restore
dotnet test Pizza.slnx -c Release --no-build --logger trx --results-directory TestResults
```

Os testes de integração iniciam o Worker real em AppContainer. Os testes do drop compilam o overlay real e substituem apenas desenho, painéis e logs. O CI verifica metadados com ExifTool e executa o teste de arquivos corrompidos.

Para a verificação independente de metadados, defina `PIZZA_EXIFTOOL` com o caminho do ExifTool. Sem ela, essa verificação acontece apenas no CI. `PIZZA_FUZZ_CASES` e `PIZZA_FUZZ_SEED` ajustam o teste de corrompidos.

Preserve lockfiles, mantenha diffs pequenos e descreva comportamento, requisito e validação no PR. Use Conventional Commits, por exemplo `fix(overlay): mantém menu após a soltura`.

Quando houver participação do Codex, mantenha o autor humano do commit e acrescente o crédito ao final da mensagem, separado do corpo por uma linha em branco:

```text
Co-authored-by: Codex <noreply@openai.com>
```

Preserve esse crédito ao integrar por squash e mantenha os créditos de outros participantes quando aplicáveis. A seção de créditos do README registra também as ferramentas usadas; a lista automática de contribuidores depende das regras de associação de commits e contas do GitHub.

Não inclua documentos pessoais, logs reais, certificados, senhas ou resultados de build no Git. Builds locais são para desenvolvimento; só o CI gera os artefatos distribuídos. Vulnerabilidades seguem [SECURITY.md](SECURITY.md).
