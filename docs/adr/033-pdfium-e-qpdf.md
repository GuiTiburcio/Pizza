# ADR-033 — PDF com QPDF e PDFium

- **Status:** Aceita (12/11/2026)
- **Data:** 2026-11-12

## Contexto

A Fase 6 traz a aba PDF: Girar, Juntar, Dividir, Imagens, Comprimir e Word (SPECS §5.3). O ADR-005 previa PDFsharp + PDFium, mas o PDFsharp é AGPL desde a 6.5.0 (ADR-022). As operações de estrutura também precisam manter a senha em AES-256 (SEC-74) e regravar o arquivo por inteiro, sem JavaScript, ações ou anexos (SEC-76).

## Decisão

- **QPDF** (Apache-2.0), pela API em C, faz as operações de estrutura: Girar, Juntar e Dividir, a senha AES-256 da saída e a limpeza de conteúdo ativo.
- **PDFium** (BSD-3 e Apache-2.0) desenha as páginas para PDF → Imagens e Comprimir. Entra quando essas operações chegarem.
- As duas rodam só no Worker em AppContainer, sem rede (SEC-20). O Worker lê o PDF pelo handle recebido e entrega os bytes à QPDF em memória, então nenhum caminho de arquivo chega à biblioteca (SEC-15). Os limites da SEC-41/42 valem antes da leitura.
- O PDFsharp sai de vez do projeto e não entra uma terceira biblioteca no Worker. Os testes conferem as saídas com o próprio QPDF (`--check` e `--json`): páginas, rotação, senha e ausência de JS e anexos (TST-22).
- Os binários vêm das releases oficiais, com versão e SHA-256 fixados no build e entrada no SBOM.
- A Fase 6 começa por Girar, Juntar e Dividir.
- **Emenda ao FR-30:** arrastar vários PDFs juntos é aceito só para Juntar. A aba PDF abre com Juntar ativa e as outras fatias esmaecidas, com a dica "um arquivo por vez". Com um PDF só, Juntar fica esmaecida. Os lotes das outras operações continuam na v1.x (FR-28).
- **Nomes das saídas:** a extensão não muda, então o FR-26 ganha um sufixo descritivo: `escritura - girado.pdf`, `escritura - pág 01.pdf` (Cada página), `escritura - parte 1.pdf` (Pela metade) e `escritura - juntado.pdf` (nome do primeiro arquivo). Se o nome já existir, vale o FR-27: `(1)`, `(2)`…

- **Primeira entrega (Girar):** QPDF 12.4.2 (`qpdf-12.4.2-msvc64.zip`, SHA-256 `db87077e…5de25d`, conferido com a lista `qpdf-12.4.2.sha256` da release). O build baixa e confere o hash; vão para a pasta do Worker `qpdf30.dll` e o runtime do Visual C++ que ela importa (`msvcp140.dll`, `vcruntime140.dll`, `vcruntime140_1.dll`).
- **Limite de memória:** a origem e a saída ficam na memória do Worker (SEC-44, 1,5 GB), então um PDF acima de 512 MB recebe `LIMIT_EXCEEDED`. É mais baixo que os 2 GB da SEC-40, que valem para as imagens.
- **PDF protegido:** enquanto o campo de senha (FR-24) não existe, um PDF criptografado recebe `PASSWORD` e nada é gravado. Assim nenhuma saída perde a proteção ou fica com proteção diferente (SEC-74).
- **Várias saídas (Dividir):** o residente não abre o PDF para contar páginas (SEC-20), então o Worker escreve no único destino um pacote: `PZB1`, a quantidade de partes e, para cada uma, o tamanho e os bytes. O residente confere o formato exato (até 2.000 partes, sem sobra nem corte, cada parte começando com `%PDF-`), copia cada parte para o seu próprio temporário e só então dá os nomes. Ou saem todas, ou nenhuma. Os nomes são sempre do residente.
- **Formulários no Dividir e no Juntar:** as páginas copiadas levam os campos (widgets) com a aparência deles, mas não o `/AcroForm` do documento. O campo continua visível e impresso igual, mas pode deixar de ser editável. A API em C da QPDF não expõe o `fixCopiedAnnotations`; editar campos depois de dividir ou juntar fica para uma etapa própria (decidido pelo mantenedor em 12/11/2026).
- **Conteúdo ativo (SEC-76):** a limpeza segue as referências a partir do trailer e remove `/OpenAction`, `/AA`, `/JS`, `/JavaScript`, `/EmbeddedFiles`, `/EF` e `/XFA`; as ações JavaScript, Launch, SubmitForm, ImportData, GoToE, RichMediaExecute, Rendition, Movie e Sound; e as anotações RichMedia, FileAttachment, Screen, Movie, Sound e 3D. O aviso no painel ("a origem tinha conteúdo ativo") e a opção "Manter conteúdo ativo" (POL-14) ficam para depois.

## Consequências

- Ficam duas DLLs nativas para acompanhar CVEs, ambas com histórico de fuzzing (OSS-Fuzz).
- A QPDF sozinha cobre a primeira entrega, e o PDFium só entra com Imagens.
- Juntar recebe vários arquivos, então o gesto e o painel precisam aceitar vários arquivos na aba PDF; isso pede uma prancha antes do código.
- PDF → Word continua sem motor definido. A decisão vem junto com a aba Docs (LibreOffice, S-01).

## Alternativas descartadas

- Só PDFium: não grava senha, e o SEC-74 ficaria sem solução.
- PdfPig na estrutura: também não grava senha, e a limpeza ficaria por nossa conta.
- PdfPig, PDFium e QPDF juntos no Worker, como redundância: um parser a mais é mais uma porta para um PDF malicioso, e a conferência dupla já existe nos testes.
- PDFsharp 6.2.4 (MIT): versão congelada, sem correções.
- iText, MuPDF e Ghostscript: AGPL (ADR-005).

## Adendo de 13/11/2026: Worker de reserva (NFR-05)

Abrir o Worker isolado custa ~150–300 ms em toda conversão. Agora o residente abre um Worker de reserva quando o menu abre sobre um arquivo que tenha alguma conversão feita pelo Worker (Imagem, Áudio, PDF e as do Brotinho), seguindo o motor de reserva da ADR-034. A conversão usa essa reserva se ela estiver viva; se não, abre um Worker na hora, como antes. Vale para todas as conversões do Worker, por decisão do mantenedor.

- **Isolamento igual (SEC-20, SEC-15, SEC-29):** a reserva é o mesmo `Pizza.Worker.exe`, no mesmo AppContainer sem rede e no mesmo Job. Ela não recebe nada até o pedido, e o pedido só traz handles. Cada Worker continua servindo um único arquivo e encerrando: a reserva usada não volta a ser reserva.
- **Prazo (SEC-43):** conta a partir do pedido, como antes. A espera da reserva não entra nele.
- **Espera:** sem pedido em 60 s, a reserva é encerrada. Ao sair do Pizza, também (o Job encerra o processo mesmo se o residente cair).
- **Custo:** um processo parado por até 60 s depois de cada menu aberto sobre esses arquivos.
- Código em `WorkerSpare` (Pizza.Core) e `Conversion.WarmUp`. Medição em `docs/FASE-6.md`.
