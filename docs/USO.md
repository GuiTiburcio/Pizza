# Usar a prévia do Pizza

Requisitos: Windows 10 21H2+ ou Windows 11, x64, e .NET 10 Runtime para o Worker desta prévia. O residente usa NativeAOT. O instalador e o preparo automático da instalação ainda fazem parte do roteiro.

1. No [GitHub Actions](https://github.com/GuiTiburcio/Pizza/actions/workflows/ci.yml), abra um build bem-sucedido e baixe **pizza-win-x64**. O GitHub pode pedir login para baixar artefatos.
2. Extraia **todo** o conteúdo numa pasta própria. Mantenha o Worker, bibliotecas e sons junto do `Pizza.exe`.
3. Para o Worker em AppContainer ler os próprios binários, abra o PowerShell nessa pasta e execute o comando abaixo. Ele concede apenas leitura e execução aos pacotes de aplicativos **nessa pasta**. Não use a pasta de documentos nem a raiz de um disco como destino.

```powershell
icacls . /grant '*S-1-15-2-1:(OI)(CI)(RX)' /T /Q
```

4. Abra `Pizza.exe`. O ícone aparece na bandeja, possivelmente em “Mostrar ícones ocultos”.
5. Segure Ctrl e comece a arrastar uma imagem. Quando a Pizza aparecer, solte onde estiver e clique no formato, ou solte diretamente numa fatia.

A roda percorre as voltas da aba antes de passar à próxima. As abas ainda sem conversores mostram “Em breve”. Com o menu preparado para clique, Tab/setas percorrem formatos, Enter/Espaço selecionam e Esc cancela. Durante o arraste, solte Ctrl antes de usar Esc: Ctrl+Esc é um comando do Windows.

A saída fica na pasta da origem. Conflitos recebem ` (1)`, ` (2)` e assim por diante. O histórico em **Arquivos...** existe apenas enquanto o Pizza está aberto. Para encerrar, use **Sair** no menu do ícone.

## Se algo não funcionar

- **Nada acontece ao abrir:** verifique se o Pizza já está na bandeja; uma segunda instância não é aberta.
- **Worker parou inesperadamente:** confira a extração completa, o .NET 10 Runtime e a permissão de leitura/execução da pasta dos binários.
- **Menu não abre:** use um formato suportado e pressione Ctrl antes de o arraste ultrapassar o limiar do Windows. Arrastar sem Ctrl continua sendo uma operação normal do Explorer.
- **Vários arquivos:** esta prévia aceita um por vez.
- **Logs:** o menu do ícone abre os registros técnicos. Demonstre problemas com arquivos de exemplo, sem documentos pessoais.

Consulte as [limitações e próximas etapas](ROADMAP.md).
