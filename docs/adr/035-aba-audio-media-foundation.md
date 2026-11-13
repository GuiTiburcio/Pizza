# ADR-035 — Aba Áudio com o Media Foundation do Windows

- **Status:** Aceita pelo mantenedor em 12/11/2026. Antecipa da v1.x a parte de áudio da linha "Áudio/Vídeo" da SPECS §2.1 e detalha o ADR-026 para o áudio. Adendo de 12/11/2026 (Estéreo, Volume e tamanho previsto), das pranchas AU-1 a AU-5, no fim.
- **Data:** 2026-11-12

## Contexto

A rota de 12/11/2026 pôs a aba Áudio entre as quatro que precisam converter antes da auditoria, usando só o Media Foundation (MF) do Windows, sem pacote novo. OGG/Opus e vídeo continuam na v1.x, com o FFmpeg (ADR-026). O mantenedor pediu opções completas, e não só os quatro valores de AAC da documentação do Windows.

Uma prova fora do repositório, feita em 12/11/2026 no Windows 11 (build 26300), rodou o MF dentro do AppContainer real (`SandboxedProcess`, sem capabilities, com Job Object), lendo e gravando só pelos handles recebidos:

- WAV, MP3, M4A e FLAC foram convertidos entre si nos dois sentidos, e cada saída foi lida de novo na conversão seguinte. 5 s de áudio levaram de 0,1 a 0,4 s, incluindo abrir o Worker.
- Codificadores do próprio Windows: MP3, AAC, FLAC e ALAC. O WAV é gravado sem codificador.
- Valores aceitos: MP3 estéreo de 112 a 320 kbps nos degraus do padrão (96 só testado em mono); MP3 mono 64 e 96. AAC 64, 96, 128, 160, 192, 256 e 320 kbps (112, 224 e 288 foram recusados). ALAC, FLAC de 16 e 24 bits, mono e outras taxas de amostragem funcionaram.
- Com a política de assinatura "só Microsoft" ligada no processo, tudo continuou funcionando, e só ficaram carregadas DLLs do System32, do .NET e do próprio Worker. Nesta máquina há um decodificador de terceiro registrado ("FFmpegAudioDecoder"), que a política impede de entrar no Worker.
- Lixo renomeado `.mp3` recebeu `MF_E_UNSUPPORTED_BYTESTREAM_TYPE`.

## Decisão

### Formatos

- Quatro fatias, numa volta: **MP3, M4A, FLAC, WAV**.
- Entradas aceitas: `.mp3`, `.m4a`, `.aac` (ADTS), `.flac` e `.wav`. Como nas imagens, a fatia do próprio formato não aparece.
- O M4A guarda AAC ou, na opção "Sem perda", ALAC.

### Opções

O painel abre no botão direito; a faixa de níveis (ADR-027) mostra só a opção principal.

| Formato | Opção principal (faixa) | Painel |
| --- | --- | --- |
| MP3 | Qualidade: Alta 320, **Equilibrada 192**, Compacta 128 kbps | Régua de Qualidade em degraus: 96, 112, 128, 160, 192, 224, 256, 320 kbps |
| M4A | Qualidade: Alta 256, **Equilibrada 160**, Compacta 96 kbps | Régua de Qualidade em degraus: 64, 96, 128, 160, 192, 256, 320 kbps e, no topo, Sem perda (ALAC) |
| FLAC | Profundidade: **Original**, 16 bits, 24 bits | Igual |
| WAV | Profundidade: **Original**, 16 bits, 24 bits | Igual |

Em todos os formatos, o painel também tem:

- **Canais:** Original (padrão) ou Mono.
- **Taxa de amostragem:** Original (padrão), 48 kHz ou 44,1 kHz.

Regras das opções:

- MP3 e AAC só aceitam 44,1 e 48 kHz. Em "Original", uma origem com outra taxa vai para 48 kHz.
- "Original" na profundidade guarda 16 ou 24 bits conforme a origem; acima de 24 bits, ou em ponto flutuante, fica 24.
- A régua em degraus é a régua do ADR-030 com valores fixos: o pino salta de um degrau ao outro e a caixa mostra o valor em kbps. O esquema só aceita os valores da lista, e o Worker valida com o mesmo catálogo (SEC-13). O protocolo não muda.
- 256 e 320 kbps em AAC foram vistos só no Windows 11. Antes do merge, o teste roda também no runner Windows da CI. Se ele recusar, a régua do M4A para em 192 e o ADR é corrigido.

### Segurança

- **Isolamento:** o MF roda só no Worker em AppContainer sem rede (SEC-20). Origem e destino chegam por handle (SEC-15, SEC-22) e viram um `IStream` sobre o próprio handle, entregue ao MF por `MFCreateMFByteStreamOnStream`. Nenhum caminho chega ao MF.
- **Inicialização:** `MFStartup` usa `MFSTARTUP_NOSOCKET`.
- **SEC-26:** antes de iniciar o MF, o Worker de áudio liga a política de assinatura do processo "só binários assinados pela Microsoft". Nenhum codec de terceiro instalado no PC entra no Worker. Como cada arquivo tem o seu Worker (ADR-019), a política vale só para conversões de áudio e não afeta o libvips nem a QPDF.
- **Lista de formatos pelo conteúdo (como a SEC-45):** o Worker confere a assinatura do arquivo (ID3 ou quadro MPEG, `RIFF…WAVE`, `fLaC`, `ftyp` do ISO BMFF, sincronia ADTS). Depois de abrir, confere também que o primeiro fluxo de áudio é MP3, PCM, ponto flutuante, AAC, ALAC ou FLAC, e que não há fluxo de vídeo. Um MP4 com vídeo renomeado `.m4a`, ou um WMA renomeado `.mp3`, recebe `UNSUPPORTED`. O vídeo é da aba Vídeo.
- **Limites:** valem os 2 GB da SEC-40, os 2 minutos ativos da SEC-43 (ADR-029) e o 1,5 GB da SEC-44. O MF lê em fluxo, então a memória não cresce com a duração.
- **Progresso (ADR-029):** é a posição do último trecho lido dividida pela duração informada pelo arquivo. Pausar continua congelando o processo.
- **Windows sem Media Foundation** (edições N sem o Media Feature Pack): o Worker devolve o código novo `MEDIA_UNAVAILABLE`, com a mensagem "Este Windows não tem os recursos de mídia. Instale o Media Feature Pack." Nada é gravado.
- **Metadados:** a saída sai sem etiquetas nem capa (PRIV-30), e a caixa "Manter metadados" não aparece no áudio nesta entrega.

### Testes

- O teste vem antes do código, pelo Worker real:
  - conversões entre os quatro formatos, com cada opção;
  - leitura de volta da saída pelo próprio MF;
  - disfarce (vídeo e lixo com extensão de áudio) recusado com `UNSUPPORTED`;
  - política de assinatura ligada, com a lista de módulos carregados.
- Os arquivos de áudio de teste são gerados pelo próprio teste (tom senoidal), sem MP3 de usuário no repositório.
- O TST-03 ganha os quatro formatos: arquivos válidos corrompidos de forma repetível, passados pelo Worker real.
- Antes do merge, medir uma hora de áudio contra o SEC-43.

## Consequências

- Nenhuma dependência nova, e o instalador não cresce.
- O Worker ganha interop direto com o MF (COM pelas tabelas de funções, sem pacote).
- Os decodificadores do Windows passam a ser mais uma superfície de ataque. Eles são atualizados pelo Windows Update e ficam presos no AppContainer, com a política de assinatura.
- A qualidade do MP3 é a do codificador do Windows, só com taxa constante; MP3 com taxa variável, Opus e OGG ficam para o FFmpeg na v1.x.
- O painel ganha a régua em degraus e duas linhas novas (Canais e Taxa), e isso pede uma prancha no Dossiê antes do código da tela.

## Alternativas descartadas

- **Só os quatro valores de AAC da documentação (96 a 192):** o Windows 11 aceita mais, e o mantenedor pediu opções completas.
- **FFmpeg ou LAME já agora:** dependência nova, com a análise de licença e de patentes do ADR-026 ainda pendente.
- **FDK-AAC:** licença incompatível com a GPL-3.0 (ADR-014).
- **Aceitar qualquer codec instalado no PC:** abriria o Worker a DLLs de terceiros (SEC-26).
- **Reconverter no mesmo formato para reduzir o tamanho (MP3 → MP3):** segue a regra das imagens; pode voltar com um sufixo de nome, como no ADR-033.
- **Régua contínua de kbps:** os codificadores só aceitam valores fixos e recusam o resto.

## Adendo de 12/11/2026: Estéreo, Volume e tamanho previsto

Aprovado nas pranchas AU-1 a AU-5 do Dossiê. O mantenedor pediu opções de som como as dos editores de áudio e o tamanho da saída antes de converter.

### Canais

- A opção passa a ser **Original, Estéreo ou Mono**.
- Estéreo leva qualquer origem a dois canais. Um 5.1 é mixado em dois, e um mono vira dois canais iguais.

### Volume

- O painel ganha a linha **Volume**: Original (padrão, nada é aplicado) ou **Ajustar…**, que abre o painel Volume.
- O painel Volume tem quatro efeitos, cada um com caixa para ligar e régua com o valor. São aplicados nesta ordem:

| Efeito | Valor | Padrão ao abrir |
| --- | --- | --- |
| Amplificar | −12 a +12 dB, de 1 em 1 | Desligado, +3 dB |
| Comprimir | Limiar de −40 a 0 dB, de 1 em 1; razão 2:1, 4:1 ou 8:1. Ataque de 10 ms e soltura de 100 ms, fixos | Desligado, −18 dB e 4:1 |
| Normalizar | Alvo de −23 a −9 LUFS, de 1 em 1, medido pela ITU-R BS.1770 (integrado, com portas) | Ligado, −14 LUFS |
| Limitar | Teto de −6 a 0 dB, de 1 em 1, no pico de amostra, com 5 ms de antecipação | Ligado, −1 dB |

- Os efeitos são código próprio em C#, sem biblioteca nova. Rodam no Worker sobre o áudio em ponto flutuante, entre a decodificação e o codificador.
- Normalizar precisa medir antes de aplicar, então o arquivo é lido duas vezes pelo mesmo handle: uma para medir e outra para gravar. O prazo da SEC-43 vale para as duas juntas.
- As opções novas entram no esquema do catálogo, e o Worker as valida (SEC-13). A faixa de níveis não muda: o Volume fica só no painel.

### Tamanho previsto

- O painel mostra o tamanho da saída e o atualiza quando as opções mudam.
- O residente não lê o arquivo, então pede ao Worker do próprio arquivo uma mensagem `probe`. O Worker responde `info` com:
  - duração em milissegundos;
  - taxa de amostragem;
  - canais;
  - bits por amostra.
- Nenhum nome nem caminho trafega (SEC-15). O mesmo Worker converte depois (ADR-019) e é encerrado se o painel for cancelado.
- A conta do tamanho:
  - MP3 e AAC: taxa × duração;
  - WAV: valor exato;
  - FLAC e ALAC: estimativa de 55% do WAV, mostrada com "≈".
- Se o `info` não chegar em 2 s, o painel mostra "—" e continua funcionando.

### Testes do adendo

O áudio de teste é gerado pelo próprio teste. Passa quem cumprir:

- Amplificar +6 dB dobra o pico.
- Comprimir 4:1 reduz a diferença entre trechos altos e baixos de um sinal em degraus.
- Normalizar em −14 LUFS mede −14 ± 0,5 LU na saída.
- Limitar em −1 dB não deixa nenhuma amostra acima do teto.
- Estéreo leva um 5.1 a dois canais.
- O `probe` acerta a duração com erro de até 50 ms.

## Alternativas descartadas no adendo

- **Só Normalizar:** o mantenedor escolheu o conjunto completo dos editores de áudio.
- **Efeitos do XAudio2 (XAPO) ou de uma biblioteca de DSP:** foram feitos para tocar, não para converter, ou seriam dependência nova; as contas são curtas e testáveis.
- **Estimar o tamanho só pelo tamanho da origem:** erra muito quando a origem já é comprimida.
