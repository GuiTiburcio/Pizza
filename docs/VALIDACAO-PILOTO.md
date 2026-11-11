# Preparação dos testes com usuários

Este roteiro prepara evidências enquanto o Claude trabalha nas referências visuais. Não declara a prévia pronta nem altera a ordem das fases das SPECS. O piloto formal continua na Fase 9, após os portões anteriores; uma antecipação precisa ser alinhada explicitamente e registrada.

## Uma versão por rodada

Registrar antes de testar: commit completo, link do build CI aprovado, data, edição/build do Windows, escala de cada monitor, tema e cenário. Usar o mesmo pacote durante a rodada. Uma nova correção inicia outra rodada; não transferir o resultado da versão anterior automaticamente.

Usar cópias de imagens de exemplo numa pasta de teste local. Não pedir documentos reais, captura de tela inteira ou logs brutos dos participantes. Relatos públicos não devem conter nomes/caminhos pessoais. Para suspeita de vulnerabilidade, seguir [SECURITY.md](../SECURITY.md).

## Entrada e pendências

| Portão | Evidência necessária | Estado deste roteiro |
| --- | --- | --- |
| Versão identificada | SHA, build CI, testes e CodeQL aprovados no mesmo commit | Preencher por rodada |
| Visual da Fase 4 | Aceite do mantenedor conforme o dossiê, teclado e escala de 100% a 200% | Pendente; acompanhar FASE-4.md |
| Medir e endurecer, Fase 5 | Metas aplicáveis, TST-10, TST-13, TST-20 e auditoria completa TST-28 | Não comprovados por este documento |
| Distribuição da Fase 8 | Pacote oficial do CI, assinatura e verificações previstas nas SPECS | Ainda não é instalador estável |
| Preparação da máquina | Extração, .NET Runtime e permissão de leitura do Worker conferidos | Procedimento atual em USO.md |

O comando de permissão de USO.md aplica-se somente à pasta dedicada dos binários. Não aplicá-lo à pasta que contém imagens/documentos. Não enviar ao participante um executável compilado localmente como se fosse distribuição oficial.

## Casos reproduzíveis

Executar com mouse e teclado nos temas claro/escuro. Cobrir 100%, 125%, 150% e 200%, área de trabalho e Explorer; repetir os casos de posicionamento com dois monitores de DPI diferente quando disponíveis. Marcar **não testado** onde faltar ambiente.

| Caso | Ação | Resultado esperado / requisito |
| --- | --- | --- |
| P01 | Ctrl + arrastar JPG, soltar fora das fatias e clicar PNG | Menu permanece para escolha; painel abre, sem converter antes da confirmação — ADR-025 |
| P02 | Soltar diretamente na fatia PNG | Caminho de drop continua disponível — FR-05 |
| P03 | Botão direito numa fatia e depois Cancelar | Painel disponível; nenhuma saída após cancelar — ADR-025 |
| P04 | Ficar 10 s sem interação; depois repetir clicando no miolo próximo do prazo | Primeiro fecha; interação reinicia os 10 s — ADR-025 |
| P05 | Roda/touchpad até AVIF e volta; trocar abas rapidamente | Formatos e indicadores acompanham a página, sem inverter o texto nem travar — FR-04 |
| P06 | Tab, Shift+Tab, setas, Enter, Espaço e Esc | Escolha/painel navegáveis; foco visível e cancelamento disponível — NFR-06 |
| P07 | PNG em tela pequena a 200%; navegar até Converter/Cancelar | Botões alcançáveis pela rolagem e pelo teclado — NFR-08 |
| P08 | Converter duas vezes para uma saída já existente | Saídas com sufixo; nenhuma sobrescrita — FR-26/SEC-30 |
| P09 | Arrastar vários arquivos ou arquivo sem conversão suportada | Lote recusado com aviso; arquivo sem conversão não abre o menu — FR-09/FR-30 |
| P10 | Rodada com 10 imagens mistas dos formatos suportados | UI continua responsiva, saídas abrem e origens permanecem idênticas — Fase 3/TST-10 |
| P11 | Concluir conversão; Abrir, Mostrar na pasta e Arquivos | Ações apontam para a saída correta; histórico apenas da sessão — FR-40/FR-44 |
| P12 | Fechar pela bandeja e abrir novamente | Não sobra camada que bloqueie o mouse; sessão anterior não reaparece |

Em **cada conversão**, comparar SHA-256 da origem antes/depois (TST-10), tamanho e abertura da saída. Não publicar os caminhos usados. Conversão malsucedida também deve preservar o original. Registrar falha, tempo observado e se foi possível recuperar sem encerrar pelo Gerenciador de Tarefas.

## Registro da rodada

Copiar este bloco para uma issue pelo formulário de teste, removendo informações pessoais:

```text
Commit e build CI:
Windows / escala / tema / monitores:
Caso Pxx e passos exatos:
Esperado:
Observado:
Repetições (falhas/tentativas):
Origem preservada (hash igual / diferente / não verificado):
Saída abre (sim / não / não houve saída):
Recuperação (Esc / cancelar / bandeja / encerramento forçado):
Resultado (passou / falhou / não testado):
```

Perda/alteração do original ou bloqueio persistente de entrada interrompe a rodada. Corrigir, repetir o cenário e verificar regressões antes de retomar. Registrar o aceite do mantenedor com a versão exata; deixar pendências visíveis, sem marcar uma fase inteira como concluída por um teste parcial.
