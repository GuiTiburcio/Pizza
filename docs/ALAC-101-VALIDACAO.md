# ALAC #101 — evidências e critérios de conclusão

## Escopo autorizado

Em 22/11/2026 o mantenedor escolheu o codificador de referência Apple, com ADR e SBOM. As conversões continuam locais e offline; tradução de idiomas e serviços externos estão fora do escopo. Nenhum original é alterado.

Este ramo preserva implementação e testes, inclusive testes vermelhos. Um checkpoint não significa correção concluída ou autorização de lançamento.

## Evidências de diagnóstico

- O diagnóstico anterior relatado pelo mantenedor atribuiu `MF_E_SINK_NO_SAMPLES_PROCESSED` ao codificador ALAC do Windows em trechos menores que4096 quadros. As tentativas com padding/drenagem falharam na suíte concorrente. Esses números são evidência relatada, não uma reprodução realizada neste ramo.
- O novo caminho codifica pacotes sincronicamente pela referência Apple e grava a duração real, sem silêncio artificial.
- A revisão independente identificou a diferença entre ordem WAVE e ordem Apple, além do risco de compilar fontes extraídas antigas após verificar somente o ZIP. O build agora extrai novamente o arquivo verificado em staging exclusivo, com objetos novos por compilação.
- O fixture 5.1 tem um quadro com valores1000L,2000R,3000C,4000LFE,5000BL,6000BR. A inspeção direta do pacote confirma a ordem Apple3000,1000,2000,5000,6000,4000. O decoder do Windows devolve essa ordem em PCM, enquanto o gravador WAV a interpreta como WAVE.
- `MF_MT_USER_DATA` contém48 bytes, incluindo o `chan` correto (tag124). Portanto o layout não se perde no demuxer. Colocar `chan` dentro do atom de configuração ALAC foi recusado pelo Windows; o experimento foi revertido.
- A tentativa de inserir uma permutação por matriz em resamplers administrados pelo SourceReader não alterou a saída, mesmo com HRESULT de sucesso e tipos explícitos. Essa abordagem não é uma correção comprovada. A revisão recomenda decodificação no formato original, permutação em bytes e, quando necessário, um resampler Windows controlado diretamente antes do Volume.

## Execução de 22/11/2026, antes da revisão da cadeia PCM

- Core:747 aprovados,0 falhas,0 ignorados.
- Conversores:297 aprovados,4 falhas,0 ignorados,301 no total. As quatro falhas são o teste semântico5.1 e três roundtrips6 canais. O teste concorrente de trechos curtos mono/estéreo passou dentro dessa execução.
- TRX locais: `artifacts/validation/alac-101/alac-core.trx` e `alac-converters.trx`.
- Resultados anteriores à mudança de canais não validam o código posterior.

## Gates ainda abertos

1. Ordem de alto-falantes correta antes de downmix, reamostragem e normalização; preservar bits quando o formato não muda.
2. Provas independentes por canal, mono/estéreo de destino,16/24bits,96kHz, pacotes parciais, silêncio e segunda passada do Normalizar.
3. Suíte completa de conversores, integração em Worker real, build/publish, SBOM/licenças, revisão independente e CI no SHA final.
4. A matriz manual e a validação de interface continuam pendentes. Testes de biblioteca não são testes visuais ou ponta a ponta da interface.

O ALAC #101 permanece aberto até esses critérios serem satisfeitos. Não integrar este checkpoint à main.
