# Confirmação e volta antes da conversão

Decisão do mantenedor em 14/11/2026: uma escolha errada deve poder ser corrigida antes de iniciar, sem repetir Ctrl + arraste. Esta implementação cobre um arquivo e a operação já existente Juntar PDFs; não habilita lote misto.

## Comportamento

- Soltar ou clicar numa fatia ou nível prepara a confirmação. Botão esquerdo e direito passam pelo mesmo painel, inclusive TIFF, GIF e Juntar, que não têm opções.
- O painel recebe o formato e os valores já marcados na faixa. Converter ou Enter confirma; nenhuma chamada de conversão é feita ao abrir o painel.
- Voltar e Esc fecham o painel e retomam a pizza com a seleção, a página e as opções completas preservadas. Ao revisitar uma fatia nesta sessão, suas opções anteriores são recuperadas, inclusive metadados e efeitos de volume. Um valor personalizado aparece na faixa mesmo quando não pertence aos atalhos usuais.
- Alt+F4 fecha a sessão e descarta a seleção. Cancelar uma conversão já iniciada continua sendo uma ação dos avisos de progresso, não uma volta no painel.
- Ao voltar, o probe de áudio é descartado. Um novo painel de áudio pode iniciar outro probe isolado, sem executar a conversão.

## Segurança e limites

SEC-05/06: a confirmação só retém arquivos de um drop validado; os efeitos do drop continuam COPY/NONE. SEC-31: não escreve no original. PRIV-16: o novo estado não acrescenta caminhos a logs. A imagem da tela é liberada quando o menu/painel fecha e uma nova cópia é feita ao voltar, após recomposição do desktop; o fundo anterior não é reaproveitado. Nenhuma alteração no IPC, codecs, limites do Worker ou rede.

As opções são resolvidas pelo catálogo antes de hidratar o painel/faixa. Preferências de revisão existem somente na memória desta sessão e são limpas ao cancelar, converter ou iniciar outro gesto. A confirmação não garante que o Windows aceite o bitrate escolhido; a negociação explícita de uma taxa menor continua uma pendência separada.

## Verificação

TST-01 cobre opções da faixa, valores personalizados, metadados/volume, conversores sem opções, retorno ao estado Choosing sem timers antigos, arquivos/página preservados, revisita a outra fatia e ausência de conversão antes da confirmação. Os testes de overlay substituem apenas as bordas de janela/notificação; a reconstrução visual e o foco real do Windows ainda exigem conferência no candidato CI, nos dois temas e por teclado/mouse. Nenhum teste-alvo foi executado localmente nesta etapa.

Aceite manual: selecionar um arquivo, escolher WEBP, alterar qualidade e metadados, voltar, visitar outra fatia e retornar ao WEBP. Confirmar os valores preservados; só então converter e conferir a saída/original. Repetir em Áudio com volume e TIFF/GIF sem opções, Juntar com dois PDFs, Esc e Alt+F4. Não declarar tela validada antes dessa conferência.
