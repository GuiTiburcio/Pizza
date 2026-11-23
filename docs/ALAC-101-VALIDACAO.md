# ALAC #101 — evidências e critérios de conclusão

## Escopo autorizado

Em 22/11/2026 o mantenedor escolheu o codificador de referência Apple, com ADR e SBOM. As conversões continuam locais e offline; tradução de idiomas e serviços externos estão fora do escopo. Nenhum original é alterado.

Este ramo preserva implementação e testes, inclusive testes vermelhos. Um checkpoint não significa correção concluída ou autorização de lançamento.

## Evidências de diagnóstico

- O diagnóstico anterior relatado pelo mantenedor atribuiu `MF_E_SINK_NO_SAMPLES_PROCESSED` ao codificador ALAC do Windows em trechos menores que 4096 quadros. As tentativas com padding/drenagem falharam na suíte concorrente. Esses números são evidência relatada, não uma reprodução realizada neste ramo.
- O novo caminho codifica pacotes sincronicamente pela referência Apple e grava a duração real, sem silêncio artificial.
- A revisão independente identificou a diferença entre ordem WAVE e ordem Apple, além do risco de compilar fontes extraídas antigas após verificar somente o ZIP. O build agora extrai novamente o arquivo verificado em staging exclusivo, com objetos novos por compilação.
- O fixture 5.1 tem um quadro com valores 1000L,2000R,3000C,4000LFE,5000BL,6000BR. A inspeção direta do pacote confirma a ordem Apple 3000,1000,2000,5000,6000,4000. O decoder do Windows devolve essa ordem em PCM, enquanto o gravador WAV a interpreta como WAVE.
- `MF_MT_USER_DATA` contém 48 bytes, incluindo o `chan` correto (tag 124). Portanto o layout não se perde no demuxer. Colocar `chan` dentro do atom de configuração ALAC foi recusado pelo Windows; o experimento foi revertido.
- A tentativa de inserir uma permutação por matriz em resamplers administrados pelo SourceReader não alterou a saída, mesmo com HRESULT de sucesso e tipos explícitos. Essa abordagem não é uma correção comprovada. A revisão recomenda decodificação no formato original, permutação em bytes e, quando necessário, um resampler Windows controlado diretamente antes do Volume.

## Execução de 22/11/2026, antes da revisão da cadeia PCM

- Core: 747 aprovados, 0 falhas, 0 ignorados.
- Conversores: 297 aprovados, 4 falhas, 0 ignorados, 301 no total. As quatro falhas são o teste semântico 5.1 e três roundtrips 6 canais. O teste concorrente de trechos curtos mono/estéreo passou dentro dessa execução.
- TRX locais: `artifacts/validation/alac-101/alac-core.trx` e `alac-converters.trx`.
- Resultados anteriores à mudança de canais não validam o código posterior.

## Portões de conclusão

1. Ordem de alto-falantes correta antes de downmix, reamostragem e normalização; preservar bits quando o formato não muda.
2. Provas independentes por canal, mono/estéreo de destino, 16/24 bits, 96 kHz, pacotes parciais, silêncio e segunda passada do Normalizar.
3. Suíte completa de conversores, integração em Worker real, build/publish, SBOM/licenças, revisão independente e CI no SHA final.
4. CI: verde.
5. Validação manual da interface e matriz manual: PENDENTE. Testes de biblioteca não são testes visuais nem ponta a ponta da interface.

O ALAC #101 só fecha quando a validação manual da interface (portão 5) for feita; até lá ela está PENDENTE.

## Revalidação após a revisão da cadeia PCM

A negociação implícita foi removida. O fluxo agora decodifica no formato original, permuta em bytes e usa um resampler Windows explícito somente se o destino exigir mudança. A configuração do decoder omite a máscara: pedir 4.0 ou 7.1 wide diretamente provocava mistura antecipada no SourceReader. A máscara real é aplicada depois da permutação e preservada no WAV.

- Conversores: 323 aprovados, 0 falhas, 0 ignorados na suíte completa, incluindo os sete casos acrescentados após a revisão.
- Residente: 54 aprovados, 0 falhas, 0 ignorados. Core: 747 aprovados, 0 falhas, 0 ignorados.
- Testes incluem comparação byte a byte de 5.1→mono/estéreo, 44.1/48 kHz, 16/24 bits, normalização com rewind; layouts 3–8 canais; 96 kHz; origem ALAC 32 bits construída por pacote escape independente; recusa de máscaras/cookies ambíguos; execução concorrente de 64 conversões curtas.
- ALAC→FLAC preserva PCM/máscara para 3/5/6 canais. Para 4/7/8 canais mantendo o layout, recusa explicitamente sem escrever; mono/estéreo continua testado.
- A revisão independente estática não encontrou novo bloqueio concreto após essas correções. Isso não substitui integração, CI ou validação manual.
- O PE da DLL local contém CFG, DEP/NX, ASLR, HighEntropyVA e CET. Build registra commit/hash da fonte, hash da DLL, versão do MSBuild e toolset.

- Integração de áudio em AppContainer: 9 aprovados. Inclui um arquivo de uma hora, ALAC 5.1 de 4095 quadros, reutilização do Worker após o probe e comparação com a saída WAV de referência.
- Integração de sandbox e conversão: 63 aprovados. Inclui cancelamento após progresso do ALAC 5.1, saída duplicada, falta de permissões, timeout e queda do Worker. O cancelamento não publicou saída nem deixou temporário.
- Build Release da solução e publicação local do Worker e do residente NativeAOT concluídos. O pacote local contém a DLL, a licença, o aviso e o manifesto ALAC. A geração de SBOM foi executada duas vezes e manteve uma única entrada Apple.

Ainda pendente: validação manual da interface (o CI está verde). As quatro falhas anteriores ficam preservadas como diagnóstico.
