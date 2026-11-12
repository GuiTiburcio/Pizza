# Fase 5 — medir e endurecer

Referências: [SPECS §15](SPECS.md#15-rastreabilidade-requisito--fase--teste) (NFR-01 a NFR-05, PRIV-02 a PRIV-05, TST-13, TST-20, TST-28) e os itens V-01 a V-04 da [auditoria do projeto](security/AUDITORIA-SEGURANCA.md). As SPECS prevalecem.

**Fase aberta em 11/11/2026.** Ordem escolhida pelo mantenedor: medir, endurecer e auditar.

| Etapa | Entrega | Situação |
| --- | --- | --- |
| 5a — medições | NFR-01 a NFR-04 medidos e registrados | NFR-01, 02 e 03 dentro da meta; NFR-04 acima em PNG padrão, WEBP e AVIF (só registrado); fotos reais e memória retida pendentes |
| 5b — endurecimento | Memória retida, PRIV-05 e TST-20 (sem dump e fora do WER), V-03 (ACL), V-04 (SVG renomeado), SBOM pelas dependências, alto contraste | Memória retida corrigida (17,7 → 11,2 MB depois do uso); o resto pendente |
| TST-13 | Captura de tráfego por 24 h de uso | Pendente; roda no PC do mantenedor |
| 5c — auditoria | TST-28 com a skill `security-audit` (completa, perfil standard, verificação independente) e o Strix | Pendente |

V-01 (Word) e V-02 (senha) não se aplicam ainda: o Office só entra pelo LibreOffice isolado (S-01) e ainda não há campo de senha. NFR-05 (PDF) é medido na Fase 6, quando as operações de PDF existirem.

## 5a — medições de 11/11/2026

Cópia local do `main` em `d13c760` (prévia 0.1.0), Windows 11, notebook do mantenedor.

### NFR-01 e NFR-02 — residente em repouso

| Medida | Resultado | Meta |
| --- | --- | --- |
| Memória privada em repouso (60 s, após uso) | 9,3 MB | < 20 MB |
| CPU em 60 s parado | 0 ms | 0% |
| Conjunto de trabalho | 34,9 MB | — (inclui DLLs do Windows, ver NFR-01 na §2.1) |

Com uso real (menu, painel e conversão, amostragem a cada 100 ms por 3 minutos): pico de **19,5 MB**, dentro da meta mas perto dela. Depois do uso, parado, ficou em 17,7 MB, contra 7,9 MB ao abrir: cerca de 10 MB não voltam. A investigar no endurecimento (5b).

### Memória retida depois do uso (5b, 12/11/2026)

Reproduzida fora do app, com o mesmo desenho e o mesmo desfoque compilados em NativeAOT: cada gesto copia e desfoca a tela e desenha 60 quadros. De 7,8 MB ao abrir, chega a 18 a 21,5 MB e para de crescer entre 50 e 150 gestos: **não é vazamento**. São três partes:

| Parte | Quanto | O que é | Situação |
| --- | --- | --- | --- |
| Rascunhos do desfoque | 4 a 5 MB | A meia resolução era alocada no heap grande do .NET a cada gesto, com pixels da tela, e só sumia quando o coletor passava. Contrariava a cópia da tela "zerada ao fechar" (ADR-023) | **Corrigido:** rascunhos fora do heap gerenciado, zerados e soltos ao fim de cada desfoque |
| Geração 0 do coletor | cerca de 4 MB | O .NET mantém reservado o espaço da geração 0, enchido pelo lixo pequeno dos quadros | **Corrigido:** o ajuste do tamanho não é aceito embutido no NativeAOT; então, 10 s sem mensagens nas janelas do Pizza (o hook do mouse não conta), o residente faz uma coleta agressiva, uma vez, que devolve esse espaço |
| GDI+ | cerca de 4 MB | Fontes e caches criados no primeiro desenho | Fixo, não cresce |

Depois das duas correções, o medidor fica em 16,0 a 16,6 MB logo depois dos gestos e em 12,0 a 12,6 MB depois da coleta (antes, 18 a 21,5 MB). No app, com gestos e conversão de verdade, o mantenedor mediu **11,2 MB** parado depois do uso, contra 17,7 MB antes.

### NFR-04 — imagem de 12 MP

Foto sintética de 4000 × 3000 (gradientes com ruído), JPG de 1,6 MB, convertida pelo mesmo `ConversionRunner` do app, com o Worker isolado. Mediana de três rodadas, já com os cerca de 0,3 s de abertura do Worker.

| Conversão | Tempo | Meta < 1 s |
| --- | --- | --- |
| JPG → JPG | 0,32 s | ok |
| JPG → PDF | 0,37 s | ok |
| JPG → PNG (Rápida) | 0,74 s | ok |
| JPG → PNG (Equilibrada, padrão) | 1,23 s | acima |
| JPG → WEBP | 1,91 s | acima |
| JPG → AVIF | 8,40 s | acima |

Variações medidas direto no libvips, sem o Worker, para decidir depois. O mantenedor preferiu só registrar, sem mudar os codificadores por enquanto.

| Variação | Tempo | Tamanho |
| --- | --- | --- |
| PNG, compressão 2, filtro adaptativo (hoje) | 0,89 s | 29,1 MB |
| PNG, compressão 2, filtro "sub" | 0,47 s | 14,7 MB |
| WEBP q75, esforço 4 (hoje) | 1,66 s | 1,57 MB |
| WEBP q75, esforço 2 | 0,79 s | 1,53 MB |
| WEBP q75, esforço 1 | 0,51 s | 1,41 MB |
| AVIF q75, esforço 4 (hoje) | 14,0 s | 2,47 MB |
| AVIF q75, esforço 2 | 1,42 s | 2,52 MB |
| AVIF q75, esforço 0 | 0,57 s | 2,49 MB |

O esforço muda quanto o codificador procura a melhor compressão, não a qualidade escolhida. A imagem é sintética: a confirmação com fotos reais está pendente.

### NFR-03 — gesto até o menu

O evento `menu.opened` do log passou a levar o tempo do início do gesto até o menu na tela, em ms (só um número, PRIV-16; o verificador do TST-09 aceita só esse campo extra).

| | Antes | Depois da correção |
| --- | --- | --- |
| Mediana | 140 ms (16 gestos) | **87 ms** (9 gestos) |
| Faixa | 77 a 174 ms | 69 a 149 ms |
| Dentro de ≤ 100 ms | 2 de 16 | 8 de 9 |

**Causa**, medida por trecho numa versão temporária do app (10 gestos, escala de 100%): o Windows leva de 12 a 19 ms, ou de 83 a 91 ms, para entregar o arraste de arquivos; a cópia da tela com o desfoque do vidro levava de 40 a 54 ms; o desenho, de 9 a 12 ms.

**Correção:** o desfoque passou a ser feito em meia resolução, igual a olho e com cerca de um quarto do trabalho; e a cópia com o desfoque passou para o início do gesto, enquanto o Windows ainda não confirmou o arraste. Se não for arquivo, o fechamento zera a cópia (ADR-023).

O único gesto acima da meta foi o primeiro depois de abrir o Pizza (149 ms), quando o Windows ainda carrega fontes e o GDI+.
