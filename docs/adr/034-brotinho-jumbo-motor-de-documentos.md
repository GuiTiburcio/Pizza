# ADR-034 — Pizza Brotinho e Pizza Jumbo: motor de documentos isolado

- **Status:** Aceita pelo mantenedor em 12/11/2026.
- **Data:** 2026-11-12

## Contexto

A aba Docs converte DOCX, XLSX e PPTX. O SPECS (S-01, ADR-019) exigia o LibreOffice instalado pela pessoa. O mantenedor quer que o Pizza converta pelo **formato**, sem depender de nenhum pacote Office (Word, WPS, Writer etc.).

Word e WPS não rodam em sandbox: abrir um documento neles interpretaria arquivo não confiável fora do AppContainer, com rede (invariante 4 do `CLAUDE.md`). Converter DOCX em PDF com fidelidade exige um motor de diagramação, e o único livre que dá para isolar é o do LibreOffice (MPL-2.0), com cerca de 300 MB.

Prova feita em 12/11/2026 com o LibreOffice 26.2.7 (MSI oficial, SHA-256 conferido, extraído sem instalar):

| Tentativa no AppContainer sem rede | Resultado |
| --- | --- |
| `soffice.bin --convert-to pdf` | Falha: cria o canal único `\\.\pipe\OSL_PIPE_…`, e o AppContainer só pode criar canais em `\\.\pipe\LOCAL\…` (acesso negado) |
| LibreOfficeKit (`libreofficekit_hook_2`) num processo próprio | Funciona, desde que o motor esteja na raiz de uma unidade |
| Motor em pasta comum | Trava: o LibreOffice confere cada pasta acima do motor (`C:\`, `C:\Users`…), que o AppContainer não pode listar, e mostra "User installation could not be completed" esperando um clique |
| Caminho `\\?\C:\…` | Trava do mesmo jeito |
| Motor na raiz de letra temporária (`DefineDosDevice`) | Converte DOCX em PDF em 8 a 11 s, sem rede, num processo só |

## Decisão

- **Dois pacotes**, publicados pelo CI (ADR-031):
  - **Pizza Brotinho:** o pacote leve de hoje. A aba Docs faz as conversões que não precisam de diagramação (por exemplo DOCX → TXT, HTML e Markdown, XLSX → CSV) no Worker de sempre, com o OpenXML SDK (MIT). As fatias que precisam do motor aparecem com "No Pizza Jumbo".
  - **Pizza Jumbo:** o mesmo Pizza mais a pasta `motor\`, com o LibreOffice extraído pelo CI do MSI oficial, versão e SHA-256 fixos. O Pizza nunca baixa o motor (S-05).
- **Processo `Pizza.Docs.exe`:** hospeda o motor pelo LibreOfficeKit, um arquivo por processo (S-02), em AppContainer sem capabilities (sem rede), dentro de um job de um processo, numa **área de trabalho oculta**. Assim nenhuma caixa do motor aparece na tela, e um aviso inesperado vira erro com prazo (SEC-43), não uma janela parada.
- **Letra temporária:** durante a conversão, a pasta do motor vira a raiz de uma letra livre (`DefineDosDevice` com `DDD_NO_BROADCAST_SYSTEM`, sem avisar o Explorer). Ela é removida no fim com `DDD_EXACT_MATCH_ON_REMOVE`, que só apaga a letra que aponta para o motor. Ao abrir, o Pizza remove as letras que tenham sobrado de uma queda. O sandbox continua só lendo e executando o motor.
- **Caminhos (SEC-15):** o residente abre o original só para leitura e copia o conteúdo para uma pasta temporária que só esse sandbox acessa. O motor lê e grava apenas ali, e o residente grava a saída ao lado do original pelo caminho seguro de hoje (SEC-29 a SEC-37). O sandbox nunca vê caminhos da pessoa.
- **Perfil novo e descartável a cada arquivo (SEC-62):** macros desligadas e segurança de macro "Muito alta", sem atualizar vínculos, DDE nem conteúdo remoto (SEC-63). Testes: TST-12 (macro `AutoOpen` não executa) e TST-16 (modelo remoto não gera conexão).
- Word e WPS continuam fora (ADR-019). O LibreOffice instalado pela pessoa deixa de ser requisito.

### Adendo de 12/11/2026: tempo de conversão

Medido no sandbox: um motor novo fica pronto em ~9–10 s; com o motor pronto, a conversão leva ~2,2 s; um motor pronto e parado ocupa ~200 MB. Decisão do mantenedor:

- **Aquecimento ao abrir o menu:** quando o menu abre sobre um documento, o Pizza inicia um motor novo enquanto a pessoa escolhe a fatia. Depois de cada conversão, um motor novo fica **de reserva por 60 s**. Cada documento continua usando um motor que nenhum outro arquivo tocou (S-02). A letra temporária e a área oculta existem enquanto há motor convertendo ou de reserva, e saem junto com ele.
- **Perfil-modelo:** criado uma vez por versão do motor, logo depois de iniciar e **antes de qualquer documento**, na pasta do AppContainer. Cada documento recebe uma cópia nova, com as travas de segurança regravadas. Ao abrir, o Pizza apaga as pastas de trabalho que sobrarem.
- **Opção futura (Fase 8):** "Converter documentos mais rápido (usa ~200 MB)", que deixa um motor sempre pronto (~2 s em toda conversão).
- **Enxugar o motor** (dicionários, 455 MB) fica para a etapa do pacote, mantendo a hifenização, que muda a quebra de linha, e com teste de fidelidade.

### Adendo de 13/11/2026: Brotinho sem dependência nova

Decisão do mantenedor na etapa 3: o Brotinho lê DOCX e XLSX só com o .NET (`ZipArchive` e `XmlReader`), sem o OpenXML SDK. Para extrair texto e células não é preciso o modelo de objetos inteiro, e cada dependência nova é superfície de ataque. Nada entra no SBOM.

- **Conversões desta etapa:** DOCX → TXT e XLSX → CSV. HTML e Markdown ficam para a etapa seguinte.
- **Leitura segura:** XML sem DTD (sem entidades externas nem expansão em cascata). Cada parte descompactada e a saída têm teto de 2 GB, contado nos bytes de verdade e não no tamanho declarado no zip (SEC-40, TST-08). Fora disso: `LIMIT_EXCEEDED`; zip ou XML fora do formato: `CORRUPTED`.
- **TXT:** só o corpo, um parágrafo por linha e as células de tabela separadas por tab; UTF-8 com BOM e CRLF.
- **CSV:** um arquivo por planilha (gráficos ficam de fora), com separador, decimal e datas das configurações regionais do Windows, como o Excel faz. Texto que começa com `=`, `+`, `-`, `@`, tab ou CR ganha um apóstrofo na frente, para não virar fórmula ao abrir (injeção de fórmula em CSV). Os nomes são do residente, numerados (`- planilha 1`); com uma planilha só, o nome é o do original. O nome da planilha nunca vira nome de arquivo, porque é texto do arquivo não confiável.

## Consequências

Ninguém precisa instalar Office para converter documentos: quem quer só imagens baixa o Brotinho (cerca de 10 MB); quem quer DOCX/XLSX/PPTX → PDF baixa o Jumbo (cerca de 300 MB a mais). O isolamento é o mesmo do Worker de imagens: AppContainer, job, sem rede.

Custos: uma letra de unidade existe por alguns segundos durante cada conversão de documento; a inicialização do motor leva de 6 a 9 s por arquivo (a reduzir com um perfil-modelo pronto); o CI passa a baixar o MSI oficial (com hash fixo) e a publicar dois zips. O motor entra no SBOM e no `THIRD-PARTY-NOTICES.md`.

## Alternativas descartadas

- **Usar o Word, o WPS ou outro pacote instalado:** não roda em sandbox (invariante 4).
- **Exigir o LibreOffice instalado (ADR-019):** contraria a decisão de converter pelo formato.
- **`soffice.bin` como processo:** o canal único é negado no AppContainer.
- **Letra de unidade enquanto o Pizza roda:** apareceria no Explorer o tempo todo.
- **Mapa de dispositivos privado do processo (`ProcessDeviceMap`):** API interna do Windows, restrita para processos em sandbox; frágil.
- **Dar ao AppContainer acesso às pastas acima do motor:** exigiria mudar permissões de `C:\` e `C:\Users`.
