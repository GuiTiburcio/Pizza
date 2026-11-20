# ADR-040 — JPEG XL com a libjxl no Worker

- **Status:** Aceita pelo mantenedor em 20/11/2026, junto com a exceção ao PRIV-30 descrita abaixo. A implementação depende dos testes, da revisão do PR e da validação no app.
- **Data:** 2026-11-20

## Contexto

A aba Imagem não grava nem lê JPEG XL (`.jxl`). O libvips empacotado (NetVips.Native 8.18.7) não traz o carregador nem o gravador de JXL, e o Windows só lê o formato com uma extensão da Microsoft Store, que o Pizza não pode exigir (SEC-50, instalação por usuário, ADR-010). O JXL recomprime um JPEG sem perdas, com economia típica de 16% a 22%, e o arquivo volta byte a byte ao JPEG original. Para outras imagens ele comprime com perdas controladas pela distância visual.

## Decisão

- **libjxl 0.11.2** (BSD-3-Clause), release oficial `jxl-x64-windows.zip` (DLLs dinâmicas), SHA-256 `54886b2d…1be3ad7a`, fixado em `src/Pizza.Converters/Pizza.Converters.csproj` com o mesmo padrão da QPDF: o build baixa uma vez para `artifacts/deps/libjxl-0.11.2/`, confere o hash e, depois disso, compila offline. Vão para a pasta do Worker só `jxl.dll`, `jxl_cms.dll`, `jxl_threads.dll`, `brotlicommon.dll`, `brotlidec.dll` e `brotlienc.dll`. O runtime do Visual C++ já vem com a QPDF.
- **Só no Worker em AppContainer, sem rede (SEC-20).** O Worker lê a origem pelo handle (SEC-15) e entrega os bytes à libjxl em memória, fixados enquanto ela os usa (`fixed` e `GCHandle`). Nenhum caminho chega à biblioteca.
- **Limites iguais aos da aba Imagem:** origem até 2 GB (SEC-40; acima de `Array.MaxLength` o JXL recebe `LIMIT_EXCEEDED`) e 100 megapixels (SEC-41), conferidos nas informações básicas do arquivo antes de alocar a imagem decodificada. A libjxl não vem pelo carregador do libvips, então a lista de decodificadores da SEC-45 ganha o JXL, reconhecido pela assinatura do conteúdo e não pela extensão.
- **Opção "Qualidade" (distância):** Sem perda (0), Alta (1, padrão), Equilibrada (2), Compacta (3).
  - JPEG com distância 0: recompressão reversível (`JxlEncoderAddJPEGFrame` com os metadados de reconstrução). O JPEG original, com seu EXIF e XMP, fica dentro do JXL, porque é isso que o devolve byte a byte. Quem escolhe "Sem perda" num JPEG recebe um arquivo que guarda os metadados dele (exceção explícita ao PRIV-30, aceita pelo mantenedor em 20/11/2026; vale só para "Sem perda" com origem JPEG, que não é o padrão).
  - Qualquer outro caso: pixels sRGB de 8 bits (RGB ou RGBA), orientação aplicada, sem metadados (PRIV-30). Distância 0 é sem perdas nos pixels.
- **Entrada:** `.jxl` passa a ser aceito como origem de todas as saídas da aba (PNG, JPG, WEBP…). O primeiro quadro é decodificado para sRGB de 8 bits. A fatia JXL fica no fim da ordem, depois do GIF, para não mexer nas primeiras seis fatias.
- Log sem nome de arquivo nem conteúdo (PRIV-16): a libjxl não escreve log, e os erros viram `CORRUPTED`, `UNSUPPORTED` ou `LIMIT_EXCEEDED`.

## Consequências

- Mais uma DLL nativa para acompanhar CVEs (a libjxl tem fuzzing contínuo no OSS-Fuzz). Atualizar a versão e o hash juntos.
- JXL → JPG com reconstrução byte a byte existe no código (`JxlCodec.ReconstructJpeg`) e é conferido nos testes, mas a fatia JPG ainda decodifica os pixels. Ligar a reconstrução na saída JPG fica para uma etapa própria.
- A animação em JXL vira só o primeiro quadro. Imagens de 16 bits e HDR viram 8 bits sRGB.
- O build com a rede desligada falha até o primeiro download, como já acontece com a QPDF.

## Alternativas descartadas

- Extensão JXL da Microsoft Store: depende de instalação fora do controle do Pizza e não roda no Worker em AppContainer.
- libvips com JXL: o pacote NuGet atual não traz; trocar de binário do libvips muda uma dependência inteira por causa de um formato.
- Binding de terceiros (por exemplo, Magick.NET): superfície de ataque muito maior para um único formato.
- Versão 0.13.0: publicada em 11/11/2026, ainda sem tempo de uso; a 0.11.2 tem a mesma API usada aqui.
