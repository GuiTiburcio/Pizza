# Guia de TI: implantar o Pizza por GPO ou Intune

Para a equipe de TI que quer distribuir o Pizza e controlar o que ele pode fazer. Para uso individual, veja [Instalar e configurar](INSTALACAO.md).

> **Estado atual:** o MSI por máquina, os modelos ADMX/ADML e a leitura das políticas chegam na **Fase 8** ([roteiro](ROADMAP.md)). Este guia descreve como vai funcionar, conforme a [SPECS](SPECS.md) (§5.5, §9 e §11). A prévia de hoje não lê nenhuma política e **não deve** ser implantada em produção.

## O que a TI precisa saber antes

| Ponto | Como o Pizza se comporta |
| --- | --- |
| Rede | Nenhum arquivo nem dado de uso sai do PC. A única conexão é a consulta de atualização, no máximo 1 vez por dia, por HTTPS; a política `UpdateCheck` desliga (POL-05). |
| Privilégios | Nunca pede elevação depois de instalado. Sem serviço e sem tarefa agendada com privilégio; abre com o Windows pela chave `Run`. |
| Teclado | Não instala hook de teclado. O gesto usa só o hook de mouse e o arrastar e soltar do Windows. |
| Arquivos | O original nunca é alterado, movido ou apagado. O arquivo convertido sai ao lado do original, ou numa pasta fixa por política (POL-04). |
| Isolamento | Arquivos são lidos por um processo separado em AppContainer, sem rede e sem acesso ao resto do disco. |
| Atualização no MSI | O MSI **nunca** se atualiza sozinho: só avisa que há versão nova. A TI publica a versão nova (SEC-57). |
| Registros | Só IDs e códigos, sem nome de arquivo nem conteúdo, por até 30 dias. Opcionalmente no Log de Eventos, para o Wazuh (POL-15). |
| Origem | Instaladores gerados só pelo GitHub Actions, a partir de tag assinada, com assinatura Authenticode e SHA-256 publicado. |

## 1. Baixar e conferir o MSI

1. Baixe `Pizza.msi` na página de releases do GitHub do projeto.
2. Confira o SHA-256 publicado na release:

   ```powershell
   Get-FileHash .\Pizza.msi -Algorithm SHA256
   ```

3. Confira a assinatura digital:

   ```powershell
   Get-AuthenticodeSignature .\Pizza.msi | Format-List Status, SignerCertificate
   ```

   O `Status` deve ser `Valid`. Não implante um MSI que falhe em qualquer uma das duas conferências.

## 2. Implantar por GPO

1. Copie o `Pizza.msi` para um compartilhamento de rede que as **contas de computador** possam ler, por exemplo `\\servidor\software$\Pizza\Pizza.msi`.
2. No **Gerenciamento de Política de Grupo**, crie ou edite uma GPO ligada à OU dos computadores.
3. Vá em **Configuração do Computador › Políticas › Configurações de Software › Instalação de Software** › Novo › Pacote, e aponte para o caminho de rede (UNC), não para uma unidade mapeada.
4. Escolha **Atribuído**. O Pizza é instalado em `%ProgramFiles%\Pizza` na próxima reinicialização.
5. Para atualizar, adicione o MSI novo na mesma GPO marcando-o como atualização do anterior.

Instalação silenciosa, para scripts ou outras ferramentas:

```powershell
msiexec /i Pizza.msi /qn /l*v "$env:TEMP\pizza-msi.log"
```

## 3. Implantar pelo Intune

1. Em **Aplicativos › Windows › Adicionar**, escolha **Aplicativo de linha de negócios** e envie o `Pizza.msi`.
2. Atribua como **Obrigatório** ao grupo de dispositivos.
3. As políticas (seção 4) vão por **Modelos administrativos importados (ADMX)** ou por **Catálogo de configurações** personalizado.

## 4. Configurar as políticas

O MSI traz os modelos `Pizza.admx` e `pt-BR\Pizza.adml`.

1. Copie o `Pizza.admx` para o Repositório Central (`\\dominio\SYSVOL\dominio\Policies\PolicyDefinitions`) e o `Pizza.adml` para a subpasta `pt-BR`.
2. Na GPO, as opções aparecem em **Modelos Administrativos › Pizza**, tanto em Configuração do Computador quanto em Configuração do Usuário.

As chaves ficam em `HKLM\SOFTWARE\Policies\Pizza` (computador) e `HKCU\SOFTWARE\Policies\Pizza` (usuário). A política do computador vence a do usuário, e as duas vencem a configuração que o usuário escolheu.

| ID | Valor | Tipo | Padrão | Efeito |
| --- | --- | --- | --- | --- |
| POL-02 | `DisabledConverters` | multi-string | vazio | Esconde fatias (ex.: `pdf.word`) |
| POL-03 | `ForceStripMetadata` | DWORD | 0 | 1 = sempre remove metadados (GPS, autor), sem opção de manter |
| POL-04 | `OutputMode` | string | `besideOriginal` | Ou `fixedFolder`, com a pasta em `OutputFolder` |
| POL-05 | `UpdateCheck` | DWORD | 1 | 0 = nunca consulta a internet |
| POL-06 | `LogRetentionDays` | DWORD | 30 | De 0 (sem log) a 30 |
| POL-12 | `AllowOptionalDownloads` | DWORD | 1 | 0 = nunca baixa pacotes opcionais; a TI instala |
| POL-15 | `EventLog` | DWORD | 0 | 1 = grava eventos (só IDs e códigos) no canal `Pizza/Operational`, para coleta pelo Wazuh |

Essas são as políticas previstas para a v1. As demais da SPECS §9 (POL-01, 07 a 11, 13 e 14) dependem de funções que entram depois.

Exemplo de configuração restrita para uma rede sem internet, com saída numa pasta fixa e eventos no Wazuh:

```text
UpdateCheck            = 0
AllowOptionalDownloads = 0
ForceStripMetadata     = 1
OutputMode             = fixedFolder
OutputFolder           = D:\Convertidos
EventLog               = 1
```

## 5. Conferir no computador

```powershell
gpupdate /force
gpresult /h "$env:TEMP\gpo.html"   # confira se a GPO do Pizza foi aplicada
reg query HKLM\SOFTWARE\Policies\Pizza
Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run' | Select-Object Pizza
```


## 6. Remover

Retire o pacote da GPO com **Desinstalar imediatamente**, ou rode:

```powershell
msiexec /x Pizza.msi /qn
```

A desinstalação remove binários, logs e temporários (FR-72).

## Dúvidas e segurança

Vulnerabilidades: siga o [SECURITY.md](../SECURITY.md). Para a validação interna, há o roteiro do piloto ([VALIDACAO-PILOTO.md](VALIDACAO-PILOTO.md)).
