# Usar a prévia do Pizza

Requisitos: Windows 10 21H2+ ou Windows 11, x64, e .NET 10 Runtime para o Worker desta prévia. O residente usa NativeAOT. O instalador e o preparo automático da instalação ainda fazem parte do roteiro.

1. No [GitHub Actions](https://github.com/GuiTiburcio/Pizza/actions/workflows/ci.yml), abra um build bem-sucedido e baixe **pizza-win-x64**. O GitHub pode pedir login para baixar artefatos.
2. Extraia **todo** o conteúdo numa pasta própria. Mantenha o Worker, bibliotecas e sons junto do `Pizza.exe`.
3. Abra `Pizza.exe`. A inicialização concede leitura e execução da pasta dos binários somente ao perfil AppContainer específico do Worker. Não é necessário conceder acesso a todos os pacotes de aplicativos pelo PowerShell. O ícone aparece na bandeja, possivelmente em “Mostrar ícones ocultos”.
4. Segure Ctrl e comece a arrastar um arquivo suportado. Quando a Pizza aparecer, solte onde estiver e clique na operação, ou solte diretamente numa fatia.

A roda percorre as voltas da aba antes de passar à próxima. As abas ainda sem conversores mostram “Em breve”. Com o menu preparado para clique, Tab/setas percorrem formatos, Enter/Espaço selecionam e Esc cancela. Durante o arraste, solte Ctrl antes de usar Esc: Ctrl+Esc é um comando do Windows.

A saída fica na pasta da origem. Conflitos recebem ` (1)`, ` (2)` e assim por diante. O histórico em **Arquivos...** existe apenas enquanto o Pizza está aberto. Para encerrar, use **Sair** no menu do ícone.

## Se algo não funcionar

- **Nada acontece ao abrir:** verifique se o Pizza já está na bandeja; uma segunda instância não é aberta.
- **Worker parou inesperadamente:** confira a extração completa, o .NET 10 Runtime e a permissão de leitura/execução da pasta dos binários.
- **Menu não abre:** use um formato suportado e pressione Ctrl antes de o arraste ultrapassar o limiar do Windows. Arrastar sem Ctrl continua sendo uma operação normal do Explorer.
- **Vários arquivos:** Juntar PDFs aceita várias entradas; as demais conversões não oferecem um lote genérico nesta etapa.
- **Logs:** o menu do ícone abre os registros técnicos. Demonstre problemas com arquivos de exemplo, sem documentos pessoais.

Consulte as [limitações e próximas etapas](../1-produto/ROADMAP.md).
