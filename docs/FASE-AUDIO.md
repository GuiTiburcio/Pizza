# Aba Áudio — retomada por etapas

Estado conferido em 12/11/2026: ADR-035 aceito; não há conversores de áudio na `main` b2a061a. A sessão Pizza — IV do Claude parou no desenho, após o pedido de ajustes das pranchas AU-1/AU-2. Este roteiro organiza a continuação de uma aba por vez; não declara as propostas abaixo implementadas ou aprovadas visualmente.

## Pedidos a preservar no desenho

[Abrir complemento proposto dos quatro painéis](propostas/audio-paineis-complemento.svg). É um estudo estático para discussão, com valores exemplificativos; não é captura do app e não substitui AU-1/AU-2. A apresentação recolhida de Volume e dinâmica é a proposta recomendada, ainda sujeita à preferência do mantenedor.

- Preservar a AU-1 anterior e complementá-la, sem apagar a referência anterior.
- Mostrar menu radial e faixa, com a fatia do próprio formato ausente.
- Painéis completos de MP3, M4A, FLAC e WAV, nos dois temas.
- Réguas sem pontos ou números ao longo da trilha; valor na caixa.
- Incluir canais Original, Estéreo e Mono; a opção Estéreo exige emenda ao ADR-035 e teste real de downmix 5.1.
- Mostrar tamanho estimado, com incerteza explícita quando não houver previsão confiável.
- Incluir os quatro controles de Volume e dinâmica: Amplificar, Comprimir, Limitar e Normalizar. Isso não implica reproduzir todo o editor Audacity.

## Sequência de entregas

| Etapa | Resultado verificável | Aceite |
| --- | --- | --- |
| A1 — pranchas e contrato | AU-1 preservada; complemento com quatro painéis, volume e estimativa; decisões de parâmetros registradas | Mantenedor confere os dois temas antes da tela |
| A2 — conversão básica | WAV/MP3/M4A/FLAC, sem efeitos, pelo Worker isolado | TST-02, leitura de volta, arquivo original intacto; cenários de erro do ADR-035 |
| A3 — canais, taxa e profundidade | Opções do catálogo aplicadas e validadas pelo Worker | Mono, estéreo e 5.1; identidade de canais e duração; formatos/valores recusados |
| A4 — Amplificar | Ganho fixo em dB, desligado por padrão | Sinal conhecido, ganho zero, silêncio, pico e valores inválidos |
| A5 — Comprimir e Limitar | Processamento contínuo, canais vinculados, sem cortes entre blocos | Envelope, ataque/liberação, teto, transientes e continuidade em tamanhos diferentes de bloco |
| A6 — Normalizar | Pico primeiro; LUFS só com medição e referência verificadas | Duas passagens limitadas em recursos; silêncio, balanço estéreo e alvo alcançado |
| A7 — integração e validação | Painéis e faixa ligados ao catálogo; cancelamento e progresso durante efeitos | Teclado, escala, dois temas, pacote do CI e teste do mantenedor |

A4–A6 detalham uma implementação incremental do conjunto completo solicitado, sem remover os quatro controles da proposta final. Não expor controles inoperantes na prévia. Uma etapa só entra na próxima prévia após os testes correspondentes.

## Contrato proposto para Volume e dinâmica

Todos os efeitos começam **desligados**. Os detalhes numéricos abaixo são campos a desenhar e validar, não presets aprovados:

| Controle | Campos | Regra |
| --- | --- | --- |
| Amplificar | Ganho em dB; pico previsto | Ganho zero é identidade; não cortar amostras intermediárias silenciosamente |
| Comprimir | Limiar em dBFS, razão, ataque e liberação em ms; compensação de ganho explícita | Compressão de dinâmica não é a compressão do arquivo; manter o balanço entre canais |
| Limitar | Teto em dBFS; antecipação/liberação a definir | Não anunciar proteção de pico verdadeiro sem medição de true peak; saturação abrupta não substitui um limitador transparente |
| Normalizar | Alvo de pico em dBFS; remoção de DC opcional | Um ganho comum para o conjunto de canais; não confundir pico com volume percebido em LUFS |

Ordem proposta, ainda a validar: decodificar → canais/taxa → ganho → compressor → normalização → limitador → quantização/codificação. Não ligar automaticamente um efeito para compensar outro. Se a combinação não puder ser representada com segurança, explicar o problema antes de confirmar, sem alterar o original.

### Tamanho estimado

- WAV PCM: duração × taxa × canais × bytes por amostra, mais cabeçalhos; validar a profundidade efetiva de saída.
- MP3 com taxa constante: duração × kbps ÷ 8, mais estrutura/metadados do contêiner; rotular como estimativa.
- AAC, ALAC e FLAC: não prometer tamanho exato por uma porcentagem fixa. Usar faixa justificada ou “Disponível após converter”.
- Não confundir kbps, kHz e bits. Duração informada pelo arquivo é não confiável: validar limites e valores antes de calcular; nunca reservar memória pelo tamanho estimado.

## Portões de segurança e desempenho

Manter SEC-15/20/26/31: apenas handles, Worker sem rede, codecs permitidos e original somente leitura. PRIV-30: sem etiquetas ou capa na saída desta entrega. Limites de tempo e memória do ADR-035 também valem para análise e efeitos; normalização não pode carregar o áudio inteiro em RAM. Cancelamento deve encerrar análise e processamento e limpar a saída temporária.

A disponibilidade real dos codecs e o downmix 5.1 precisam ser comprovados nas versões do Windows suportadas, não inferidos de um único computador. A estimativa não é medição de desempenho. A auditoria completa continua com seu próprio escopo e evidências; os testes de engenharia não a substituem.

## Evidência e próxima ação

Usar áudio sintético gerado nos testes: tom, silêncio, transiente, canais com níveis distintos e entrada inválida. Conferir hash do original antes/depois em sucesso, erro e cancelamento. Registrar SHA e build CI de cada rodada.

Próxima ação: concluir o complemento das pranchas e o contrato numérico de A1; em paralelo, especificar as fixtures de A2. A disposição dos controles de volume está sendo escolhida pelo mantenedor no chat. Nenhuma alteração no artefato publicado do Claude foi feita nesta retomada.

Referências técnicas: [Volume & Compression — Audacity](https://www.audacityteam.org/manual/effects/volume-and-compression/), [Amplify and Normalize](https://manual.audacityteam.org/man/amplify_and_normalize.html), [Media Samples — Microsoft](https://learn.microsoft.com/en-us/windows/win32/medfound/media-samples). São referências de comportamento; não foi copiado código dessas implementações.
