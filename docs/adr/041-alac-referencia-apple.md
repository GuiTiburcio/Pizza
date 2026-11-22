# ADR-041 — Codificação ALAC pela referência Apple

- Estado: decisão do mantenedor em 22/11/2026; implementação e validação em andamento.
- Origem: issue #101; substitui apenas a codificação ALAC do caminho Media Foundation do ADR-035.

## Problema e decisão

O diagnóstico de #101 identificou MF_E_SINK_NO_SAMPLES_PROCESSED em trechos menores que4096 quadros: o sink recebe PCM sem codificá-lo. Padding e esperas tiveram êxito isolado, mas falharam em concorrência. Esses resultados não são correção aceita.

O mantenedor escolheu o codificador de referência Apple, Apache-2.0, no commit imutável c38887c5c5e64a4b31108733bd79ca9b2496d987 de macosforge/alac. Arquivo fonte ZIP SHA-256: d555102b7d57b05f26b626bf4f92c1681928d883d621b41d80de63c6c3d3097c. O build baixa e confere esse arquivo; não baixa branch mutável nem DLL pronta desconhecida. Headers/licença dos arquivos compilados e avisos acompanham a distribuição. A dependência e a DLL resultante entram no inventário/SBOM.

## Integração e limites

Um wrapper C pequeno compila a referência numa DLL x64. Aceita somente PCM intercalado16/24bits,1–8 canais, até4096 quadros por pacote, e buffers com comprimento/capacidade explícitos. Não aceita caminhos, callbacks arbitrários, rede ou processos. Instância por conversão; buffers e estado não são compartilhados entre jobs.

O Worker mantém leitura e reamostragem pelo Media Foundation. M4A com opção Sem perda usa o encoder Apple e o muxer de uma trilha ALAC do projeto, com número real de quadros e cauda parcial. Não acrescenta silêncio nem corrige duração para esconder amostras ausentes. MP3/AAC/FLAC/WAV conservam seus caminhos existentes.

SEC-20/26: só a família Áudio carrega a DLL, por caminho absoluto sob os binários do Worker, antes de ativar MicrosoftSignedOnly. Isso permite a dependência explicitamente aprovada sem desativar a política posteriormente ou liberar outros codecs de terceiros. O probe pode preceder a conversão no mesmo processo; a preparação da dependência deve ocorrer antes da política também nesse caminho. O residente não carrega a DLL. Job, memória, orçamento de saída, timeout, handles e isolamento sem rede permanecem.

## Critérios de aceite

- Trechos1–4095 quadros,4096 e4097; PCM16/24bits, taxas suportadas, mono/estéreo/multicanal e silêncio real preservados byte a byte após releitura.
- Teste concorrente que reproduza o contexto da suíte, não somente rodadas isoladas.
- Duração, canais, ordem e cauda exatos; nenhuma finalização via codificador ALAC do Windows.
- Conversão no Worker, probe, cancelamento/limites e teste de família de DLLs.
- Build e publicação reproduzíveis, licença/avisos/SBOM presentes, revisão independente e CI aprovados.

Fidelidade e estabilidade são critérios ainda a executar. Este ADR não fecha #101 nem aprova lançamento.

Referências: https://macosforge.github.io/alac/ e https://github.com/macosforge/alac/tree/c38887c5c5e64a4b31108733bd79ca9b2496d987.
