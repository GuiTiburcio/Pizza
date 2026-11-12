# Fase 5 — medir e endurecer

Referências: [SPECS §15](SPECS.md#15-rastreabilidade-requisito--fase--teste) (NFR-01 a NFR-05, PRIV-02 a PRIV-05, TST-13, TST-20, TST-28) e os itens V-01 a V-04 da [auditoria do projeto](security/AUDITORIA-SEGURANCA.md). As SPECS prevalecem.

**Fase aberta em 11/11/2026.** Ordem escolhida pelo mantenedor: medir, endurecer e auditar.

| Etapa | Entrega | Situação |
| --- | --- | --- |
| 5a — medições | NFR-01 a NFR-04 medidos e registrados | Em andamento |
| 5b — endurecimento | PRIV-05 e TST-20 (sem dump e fora do WER), V-03 (ACL), V-04 (SVG renomeado), SBOM pelas dependências, alto contraste | Pendente |
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

Com o menu aberto: medição em andamento.

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

Pendente: precisa de medição dentro do app, do início do arraste até o primeiro quadro do menu na tela.
