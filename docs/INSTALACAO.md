# Instalar e configurar o Pizza

Passo a passo para quem vai usar o Pizza no próprio PC. Para implantar em vários computadores pela TI, veja o [guia de TI (GPO e Intune)](TI-GPO.md).

> **Estado atual:** o Pizza está em prévia de desenvolvimento. Hoje ele roda a partir de uma pasta, sem instalador. O instalador por usuário e o MSI por máquina chegam na Fase 8 ([roteiro](ROADMAP.md)).

## Requisitos

- Windows 10 21H2 ou mais novo, ou Windows 11, em 64 bits (x64).
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (só nesta prévia, para o Worker que converte os arquivos).
- Uma conta comum do Windows. O Pizza nunca pede administrador.

## Instalar a prévia

1. Abra o [GitHub Actions do Pizza](https://github.com/GuiTiburcio/Pizza/actions/workflows/ci.yml), entre num build com ✓ verde e baixe o artefato **pizza-win-x64**. O GitHub pode pedir login.
2. Crie uma pasta só para o Pizza, por exemplo `%LOCALAPPDATA%\Programs\Pizza`, e extraia **todo** o conteúdo nela. O `Pizza.exe` precisa ficar junto do Worker, das bibliotecas e dos sons.
3. Abra o PowerShell **nessa pasta** e rode o comando abaixo. Ele deixa o Worker, que roda isolado (AppContainer), ler os próprios arquivos. Só concede leitura e execução, e só nessa pasta. Não rode na pasta Documentos nem na raiz de um disco.

   ```powershell
   icacls . /grant '*S-1-15-2-1:(OI)(CI)(RX)' /T /Q
   ```

4. Abra o `Pizza.exe`. O ícone aparece na bandeja, perto do relógio (às vezes em "Mostrar ícones ocultos").
5. Teste: segure **Ctrl** e comece a arrastar uma imagem. Quando o menu aparecer, solte numa fatia ou num nível da faixa. O arquivo convertido aparece na mesma pasta do original.

O uso completo (abas, roda, faixa de níveis, botão direito, teclado) está em [Como usar](USO.md).

## Configurar

A tela de Configuração chega na Fase 8. Por enquanto:

| O quê | Como |
| --- | --- |
| Tema claro ou escuro | O Pizza segue o tema dos aplicativos do Windows (Configurações › Personalização › Cores). |
| Vidro transparente | Segue "Efeitos de transparência" do Windows. Desligado, o menu fica sólido. |
| Animações | Seguem "Efeitos de animação" do Windows (Configurações › Acessibilidade › Efeitos visuais). Para forçar, crie a variável de ambiente do usuário `PIZZA_ANIMATIONS` com `always`, `windows` (padrão) ou `never`, e reabra o Pizza. |
| Abrir com o Windows | Ainda não é automático. Crie um atalho para o `Pizza.exe` na pasta que abre com **Win + R** › `shell:startup`. |

Os dados do Pizza ficam em `%LOCALAPPDATA%\Pizza`: só registros técnicos em `logs`, sem nome nem conteúdo de arquivo, guardados por até 30 dias.

## Desinstalar a prévia

1. No ícone da bandeja, clique em **Sair**.
2. Apague a pasta onde o Pizza foi extraído e o atalho em `shell:startup`, se você criou.
3. Se quiser apagar também os registros, apague `%LOCALAPPDATA%\Pizza`.

## Na versão 1.0 (planejado)

Requisitos FR-70 a FR-73 da [SPECS](SPECS.md):

- **Instalador por usuário**, sem administrador, em `%LOCALAPPDATA%\Programs\Pizza`, com atualização assinada que só instala depois que você clica em "Atualizar".
- **MSI por máquina**, em `%ProgramFiles%\Pizza`, para a TI. Esse nunca se atualiza sozinho.
- Início com o Windows pela chave `Run`, sem serviço e sem tarefa com privilégio.
- Desinstalação que remove binários, logs e temporários; as configurações só saem se você marcar.
- Instaladores gerados só pelo CI, assinados, com SHA-256 publicado.

## Se algo não funcionar

Veja [Como usar › Se algo não funcionar](USO.md#se-algo-não-funcionar).
