# Fase 6 — Aba PDF

Biblioteca e regras em [ADR-033](adr/033-pdfium-e-qpdf.md); telas nas pranchas 6A-1 a 6A-3 do Dossiê.

| Etapa | O que entra | Estado |
| --- | --- | --- |
| 6a-1 — Girar | QPDF no Worker; Girar todas as páginas (Direita, Ponta-cabeça, Esquerda); saída `nome - girado.pdf`; limpeza de conteúdo ativo (SEC-76, TST-22); menu abre na aba PDF (FR-03) | Concluída no PR #68 |
| 6a-2 — Dividir | Cada página (`nome - pág 01.pdf`…) ou pela metade (`nome - parte 1.pdf`, `parte 2`); o Worker entrega um pacote de partes e o residente confere e grava cada uma, todas ou nenhuma | Concluída no PR #72 |
| 6a-3 — Juntar | Vários PDFs no mesmo gesto, só para Juntar (emenda ao FR-30); as outras fatias esmaecem e dizem por quê; a faixa mostra a ordem antes de soltar; saída `primeiro - juntado.pdf`; os outros arquivos chegam ao Worker só como handles (`moreSrcHandles`) | Concluída no PR #73 |
| Senha | Campo de senha (FR-24), AES-256 na saída (SEC-70 a SEC-75) | Pendente; hoje, PDF protegido recebe `PASSWORD` |
| Imagens e Comprimir | PDFium | Pendente |
| Word | Motor decidido com a aba Docs | Pendente |

## NFR-05: Juntar, Dividir e Girar abaixo de 1 s até 100 páginas

Medido em 12/11/2026 de ponta a ponta, do pedido até os arquivos prontos, com o residente e o Worker em AppContainer. Cada valor é a mediana de cinco rodadas, depois de uma de aquecimento. Foram usados dois PDFs de 100 páginas: um de **texto** (331 KB) e um **digitalizado** (61 MB, uma foto JPEG A4 por página).

| Operação | Antes | Depois | Meta |
| --- | --- | --- | --- |
| Girar · texto | 324 ms | 324 ms | ok |
| Girar · digitalizado | 982 ms | 982 ms | ok, no limite |
| Dividir cada página · texto | 3.567 ms | 717 ms | ok |
| Dividir cada página · digitalizado | 2.643 ms | 1.200 ms | acima |
| Dividir pela metade · digitalizado | 1.301 ms | 888 ms | ok |
| Juntar 2 × 50 · digitalizado | 1.092 ms | 834 ms | ok |
| Juntar 100 × 1 · texto | 548 ms | 363 ms | ok |

O "antes" do Girar (texto e digitalizado) é a segunda rodada da medição. A primeira deu 948 e 2.004 ms porque foi feita logo depois de gerar 61 MB de amostras, com o disco e o antivírus ocupados.

**Causa encontrada e corrigida:** ao gravar as partes do Dividir, o diário de temporários (SEC-37) era regravado duas vezes por parte, e cada gravação passa pelo DPAPI. Agora as partes entram e saem do diário numa gravação só (`PendingOutput.CreateMany` e `CommitAll`), e a zona de origem é lida uma vez. Gravar 100 partes caiu de 1,3–1,7 s para ~450 ms. A limpeza depois de uma queda continua valendo; o teste está em `OutputFileTests`.

**O que sobra:**
- O Worker isolado leva ~300 ms só para abrir. Isso pesa em toda conversão.
- No Dividir de um PDF digitalizado pesado, o Worker processa 61 MB (~400 ms), e criar e renomear 100 arquivos, cada um conferido pelo antivírus, custa ~4 ms por arquivo.
- Testado e descartado: tirar o ID determinístico da QPDF não mudou o tempo.

### Worker de reserva (13/11/2026)

O Worker agora abre quando o menu abre (adendo da ADR-033), e a conversão começa com ele pronto. A medição repete a de cima, com o diário em lote já na main (PR #77), e simula ~0,5 s entre abrir o menu e escolher a fatia, fora do cronômetro. Foram três medições completas em cada modo. A tabela mostra a menor e a maior das três medianas.

| Operação | Sem reserva | Com reserva | Meta |
| --- | --- | --- | --- |
| Girar · texto | 247–446 ms | 156–162 ms | ok |
| Girar · digitalizado | 712–862 ms | 642–701 ms | ok |
| Dividir cada página · texto | 525–704 ms | 378–506 ms | ok |
| Dividir cada página · digitalizado | 867–1.747 ms | 645–2.167 ms | oscila |
| Dividir pela metade · digitalizado | 601–878 ms | 452–799 ms | ok |
| Juntar 2 × 50 · digitalizado | 607–878 ms | 437–669 ms | ok |
| Juntar 100 × 1 · texto | 289–455 ms | 206–324 ms | ok |

**Leitura:** a reserva tira o custo fixo de abrir o Worker, de forma estável (Girar texto caiu para ~160 ms). O Dividir de 61 MB em 100 partes fica abaixo de 1 s com o disco calmo (645 ms), mas passa de 1 s quando o disco e o antivírus estão ocupados com as gravações anteriores, com ou sem reserva. O que sobra nesse caso é gravar 61 MB em 100 arquivos novos, cada um conferido pelo antivírus.
