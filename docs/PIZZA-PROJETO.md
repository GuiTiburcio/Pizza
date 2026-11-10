# Pizza — Especificação completa do projeto

> Conversor de arquivos para Windows que funciona no gesto: **segure Ctrl, arraste o arquivo e solte no formato** num menu circular de vidro. Tudo offline, leve e rápido.

| Campo | Valor |
| --- | --- |
| Nome do produto | Pizza |
| Repositório | [`github.com/GuiTiburcio/Pizza`](https://github.com/GuiTiburcio/Pizza) (privado até corrigir os desvios da auditoria, ver ADR-014) |
| Plataforma | Windows 10 (21H2+) e Windows 11, x64 |
| Linguagem | C# / .NET 10 (ver ADR-011) |
| Status | Especificação e design aprovados; Fase 0 (ambiente) em andamento |
| Última revisão | 07/11/2026 |

Este arquivo reúne tudo o que foi decidido até aqui. Ele está dividido em **partes independentes**, e cada parte tem dono, entregáveis e critério de pronto. A regra é: **não começar uma parte antes de a anterior cumprir o critério de pronto** (exceto onde a dependência diz o contrário).

---

## Sumário

- [Parte 0 — Como usar este documento](#parte-0--como-usar-este-documento)
- [Parte 1 — Produto](#parte-1--produto)
- [Parte 2 — Experiência do usuário (UX)](#parte-2--experiência-do-usuário-ux)
- [Parte 3 — Wireframes](#parte-3--wireframes)
- [Parte 4 — Design visual (UI)](#parte-4--design-visual-ui)
- [Parte 5 — Arquitetura](#parte-5--arquitetura)
- [Parte 6 — Máquina de estados](#parte-6--máquina-de-estados)
- [Parte 7 — Conversores e níveis de qualidade](#parte-7--conversores-e-níveis-de-qualidade)
- [Parte 8 — Desempenho e leveza](#parte-8--desempenho-e-leveza)
- [Parte 9 — Segurança, privacidade e confiabilidade](#parte-9--segurança-privacidade-e-confiabilidade)
- [Parte 10 — Instalação, distribuição e ícone](#parte-10--instalação-distribuição-e-ícone)
- [Parte 11 — Qualidade e testes](#parte-11--qualidade-e-testes)
- [Parte 12 — Repositório e forma de trabalho](#parte-12--repositório-e-forma-de-trabalho)
- [Parte 13 — Plano de execução (fases)](#parte-13--plano-de-execução-fases)
- [Parte 14 — Riscos e decisões em aberto](#parte-14--riscos-e-decisões-em-aberto)
- [Anexo A — Registro de decisões (ADRs)](#anexo-a--registro-de-decisões-adrs)
- [Anexo B — Glossário](#anexo-b--glossário)
- [Anexo C — Artefatos já produzidos](#anexo-c--artefatos-já-produzidos)

---

## Parte 0 — Como usar este documento

Pensamos o projeto como uma equipe pequena com papéis claros. Uma pessoa pode acumular papéis, mas cada decisão tem um dono.

| Papel | Responsável por | Partes |
| --- | --- | --- |
| Produto (PO) | O que entra e o que fica de fora, prioridade | 1, 13, 14 |
| UX / UI | Fluxo, wireframes, visual, acessibilidade | 2, 3, 4 |
| Arquitetura | Processos, módulos, contratos entre peças | 5, 6, Anexo A |
| Engenharia de conversão | Bibliotecas, qualidade, velocidade | 7, 8 |
| Plataforma Windows | Hooks, overlay, bandeja, instalador | 5, 10 |
| Qualidade (QA) | Testes, metas medidas, regressões | 11 |
| Segurança | Privacidade, assinatura, dependências | 9 |

**Regras de ouro**

1. Uma fase por vez; cada fase termina com algo que roda no PC.
2. Toda mudança de decisão vira um ADR no Anexo A (e um arquivo em `docs/adr/`), nunca só uma conversa.
3. Meta de desempenho sem medição não conta como cumprida.
4. O arquivo original do usuário **nunca** é alterado.

---

## Parte 1 — Produto

### 1.1 Problema

Converter arquivos hoje exige abrir um site (iLovePDF e similares) ou um programa, subir o arquivo, esperar e baixar. É lento, interrompe o trabalho e envia documentos sensíveis para servidores de terceiros.

### 1.2 Proposta

A conversão acontece no próprio gesto de arrastar, sem abrir nada, e o arquivo nunca sai do computador.

### 1.3 Público

- **Primário:** escritórios que lidam com documentos o dia todo, como cartórios, contabilidades, farmácias de manipulação e distribuidoras.
- **Secundário:** qualquer usuário de Windows que converte imagens e PDFs com frequência.

### 1.4 Diferenciais de venda

1. **100% offline:** nada é enviado para a internet, o que conta a favor na LGPD.
2. **Zero cliques extras:** Ctrl + arrastar + soltar.
3. **Leve:** parado, não pesa no PC.
4. **Rápido:** imagens e operações simples de PDF em menos de 1 segundo.

### 1.5 Escopo

| Versão | Inclui |
| --- | --- |
| **MVP (v0.1)** | Gesto Ctrl + arraste, menu radial, aba Imagem (JPG, PNG, WEBP, TIFF, ICO, GIF, Imagem→PDF), painel de opções, notificações, bandeja |
| **v0.2** | Aba PDF (Comprimir, Juntar, Dividir, Imagens, Word, Girar) |
| **v0.3** | Aba Documento (Office ↔ PDF) |
| **v1.0 (venda)** | Configurações, instalador assinado, licença, atualização automática |
| **v1.x** | Aba Áudio/Vídeo (FFmpeg), vários arquivos de uma vez com opções por lote |

**Fora do escopo** (por enquanto): edição de PDF (assinatura, OCR, anotações), nuvem e sincronização, macOS e Linux.

### 1.6 Métricas de sucesso

| Métrica | Meta |
| --- | --- |
| Tempo do gesto até o arquivo pronto (imagem comum) | < 1 s |
| RAM em repouso | < 20 MB |
| CPU em repouso | 0% |
| Conversões sem erro (amostra de testes) | ≥ 99% |
| Instalação limpa sem aviso do SmartScreen | Sim (com certificado) |

---

## Parte 2 — Experiência do usuário (UX)

### 2.1 Fluxo principal (exemplo: JPG → PNG)

1. O usuário clica no arquivo `fachada.jpg` (área de trabalho ou Explorer).
2. Segura **Ctrl** e começa a arrastar.
3. O **menu radial** abre centrado no cursor: o arquivo fica no miolo, com 6 fatias de formato em volta, já na aba certa para o tipo do arquivo.
4. Passa o mouse na fatia **PNG**: ela cresce, ganha a cor de destaque e um vidro mais forte. As outras ficam escuras e translúcidas.
5. Solta o arquivo na fatia PNG. Abre o **painel de opções** (resolução e compressão), já com o padrão marcado.
6. Clica em **Converter** (ou Enter). O painel fecha na hora.
7. O `fachada.png` aparece ao lado do original e uma **notificação** confirma, com os botões "Abrir" e "Mostrar na pasta".

### 2.2 Regras de interação

| Ação | Resultado |
| --- | --- |
| Ctrl + começar a arrastar arquivo | Abre o menu na aba do tipo do arquivo |
| Scroll durante o arraste | Troca a aba (Imagem → PDF → Documento → Áudio) |
| Passar o mouse numa fatia | A fatia cresce cerca de 6%, sai um pouco para fora e fica azul |
| Soltar numa fatia | Abre o painel de opções (ou converte direto, se o usuário marcou "usar sempre") |
| Soltar fora do menu | Cancela, sem fazer nada |
| Esc | Cancela em qualquer momento |
| Enter no painel | Converte |
| Formato de origem | Não aparece como fatia (um JPG não mostra "JPG") |

**Por que Ctrl e não Shift:** no arraste nativo do Windows, Shift **move** e Ctrl **copia**. Se o app falhar, o pior caso com Ctrl é uma cópia, nunca um arquivo movido sem querer. Alt e Ctrl+Shift criam atalhos, por isso foram descartados.

### 2.3 Nomes de arquivo de saída

- Mesmo nome e mesma pasta: `fachada.jpg` → `fachada.png`.
- Se já existir: `fachada (1).png`, `fachada (2).png`…
- Nunca sobrescreve e nunca altera o original.

### 2.4 Acessibilidade

- Tudo funciona pelo teclado no painel (Tab, setas, Enter, Esc).
- Contraste de texto ≥ 4,5:1, inclusive sobre o vidro.
- A cor nunca é o único sinal: a fatia ativa também cresce e muda o peso da fonte.
- Respeitar "Reduzir animações" do Windows (troca a animação por um fade simples).
- Leitor de tela: a notificação de conclusão é anunciada.

---

## Parte 3 — Wireframes

Wireframes em baixa fidelidade: só estrutura, sem vidro nem cor. São a referência para quem programa o layout. O visual final está na Parte 4.

> **Versão visual:** o canvas "Pizza — Wireframes e Engenharia" tem estes wireframes desenhados, com medidas e notas numeradas (WF-1 a WF-6). Também traz os diagramas de arquitetura, estados, módulos e sequência (ENG-1 a ENG-4) e o design aprovado como referência. Os esboços em texto abaixo servem de resumo. Onde o canvas e este documento divergem, **vale este documento** (ver Anexo C).

### W1 — Menu radial (estado 3)

```
                ┌──────────────────────────────────────┐
                │  [Imagem]   PDF   Documento   Áudio  │   ← barra de abas (aba ativa marcada)
                └──────────────────────────────────────┘

                          ╭──────────────────╮
                    ╭─────┤   FATIA 1 (topo)  ├─────╮
                    │ F6  ╰──────────────────╯ F2   │
                    │        ╭────────────╮         │
                    │        │   ──────   │         │   ← 4 traços = 4 abas
                    │  │     │  [ícone]   │      │  │     (traço da aba ativa em destaque)
                    │        │ fachada.jpg│         │
                    │        │   ──────   │         │
                    │ F5     ╰────────────╯     F3  │
                    ╰─────╮  ╭──────────────────╮ ╭─╯
                          ╰──┤   FATIA 4 (baixo) ├─╯
                             ╰──────────────────╯

                ┌──────────────────────────────────────┐
                │ Leve até uma fatia · Role p/ trocar a aba · Esc │
                └──────────────────────────────────────┘
```

Medidas: disco de 380 px; fatias da borda interna (r = 92) até a externa (r = 168), com 27,5° para cada lado do centro; cantos arredondados de 9 px; espaço entre fatias e entre fatia e borda do disco; miolo de r = 76.

### W2 — Fatia em hover

```
     antes (repouso)               depois (hover)
   ╭──────────────╮            ╭────────────────╮
   │     PNG      │    →       │      PNG       │  ← +6% de tamanho, 5 px para fora
   │  Sem perda   │            │   Sem perda    │    cor de destaque + brilho + sombra
   ╰──────────────╯            ╰────────────────╯
```

### W3 — Painel de opções (estado 4)

```
┌──────────────────────────────────────────────────────┐
│ [ícone jpg] → [PNG]   fachada.jpg → PNG              │
│                       Escolha como quer o arquivo     │
│                                                      │
│ Resolução                                            │
│ ( Original · padrão ) (   75%   ) (   50%   )        │
│                                                      │
│ Compressão (sem perda)                               │
│ (  Rápida  ) ( Equilibrada · padrão ) ( Máxima )     │
│ PNG não perde qualidade. Mais compressão = menor.    │
│                                                      │
│ [ ] Usar sempre estas opções para PNG                │
│                                                      │
│                       [ Cancelar Esc ] [ Converter Enter ] │
└──────────────────────────────────────────────────────┘
```

O conteúdo do painel muda por conversão (ver Parte 7). Conversões sem opções pulam o painel.

### W4 — Notificação de conclusão (estado 6)

```
┌──────────────────────────────────────┐
│ [ícone png]  Conversão concluída     │
│              fachada.png · original  │
│                                      │
│ [   Abrir   ]  [ Mostrar na pasta ]  │
└──────────────────────────────────────┘
```

### W5 — Notificação de erro (estado 7)

```
┌──────────────────────────────────────┐
│ (!)  Não foi possível converter      │
│      fachada.jpg → PNG               │
│      Motivo: arquivo corrompido      │
│                                      │
│ [ Tentar de novo ]  [ Ver detalhes ] │
└──────────────────────────────────────┘
```

### W6 — Menu da bandeja

```
 ┌───────────────────────────┐
 │ Pizza — ativo             │
 │ ───────────────────────── │
 │ Pausar por 1 hora         │
 │ Configurações…            │
 │ Abrir pasta de logs       │
 │ ───────────────────────── │
 │ Sobre                     │
 │ Sair                      │
 └───────────────────────────┘
```

### W7 — Configurações (v1.0)

```
┌─ Pizza · Configurações ───────────────────────────────┐
│ Geral        │ Tecla do gesto      [ Ctrl ▾ ]          │
│ Formatos     │ Iniciar com o Windows   [x]             │
│ Qualidade    │ Pasta de saída   (•) ao lado do original│
│ Desempenho   │                  ( ) pasta fixa [...]   │
│ Licença      │ Tema   (•) seguir Windows ( ) claro ( ) escuro │
│ Sobre        │ Cor de destaque  ● ● ● ●               │
└──────────────┴─────────────────────────────────────────┘
```

---

## Parte 4 — Design visual (UI)

### 4.1 Linguagem

"Liquid Glass" adaptado ao Windows: vidro desfocado (Acrylic) com borda clara, brilho no topo, sombra suave e animação curta.

### 4.2 Tokens

| Token | Valor |
| --- | --- |
| Cor de destaque (padrão) | `#2F6BFF` (o usuário pode trocar) |
| Texto principal | `#0F1320` |
| Texto secundário | `#1F2638` |
| Vidro (disco) | branco 16%, borda branca 55%, desfoque 22 px, saturação 180% |
| Fatia em repouso | azul-marinho `#1A2140`, de 20% a 8% de opacidade, borda branca 45% |
| Fatia em hover | cor de destaque de 62% a 30%, borda branca 95%, brilho no topo, sombra colorida |
| Miolo | branco 55%, borda branca 92% |
| Cantos das fatias | 9 px |
| Cantos de botões e painel | 12 px (botões) / 28 px (painel) |
| Fonte | Segoe UI Variable (fallback: Segoe UI) |
| Animação | 180 ms, ease |

### 4.3 Estados das fatias

| Elemento | Repouso | Hover |
| --- | --- | --- |
| Fatia | Vidro escuro translúcido | Cresce ~6%, sai 5 px, cor de destaque, brilho e sombra |
| Texto | 14 px, peso 600 | 16 px, peso 700 |
| Traço da aba | Cinza 28% | Cor de destaque, mais grosso |

### 4.4 Tema escuro

Mesmo desenho, com o vidro escurecido (preto de 30% a 40%) e textos claros. A cor de destaque se mantém.

### 4.5 Referências

O design interativo e as 6 telas do fluxo estão no canvas "Menu Radial de Vidro" (Anexo C).

---

## Parte 5 — Arquitetura

### 5.1 Visão geral

São **dois processos**, para o app ficar leve:

```
┌──────────────────────── Pizza.exe (residente) ────────────────────────┐
│  Bandeja   GestureHook   RadialOverlay   OptionsPanel   Notifier       │
│      │          │              │               │            ▲          │
│      └──────────┴──── Core (estado, config, logs, IPC) ─────┘          │
└──────────────────────────────────┬───────────────────────────────────────┘
                                   │ Named Pipe (JSON)
┌──────────────────────── Pizza.Worker.exe (sob demanda) ───────────────┐
│  JobQueue → ConverterRegistry → IConverter (Imagem | PDF | Office | Mídia) │
└────────────────────────────────────────────────────────────────────────┘
```

- **Residente:** sempre ligado, mínimo. Só hooks, desenho do menu e notificações.
- **Worker:** só existe durante a conversão. Carrega as bibliotecas pesadas e fecha depois de 30 a 60 s parado.

### 5.2 Stack

| Camada | Tecnologia | Licença |
| --- | --- | --- |
| Linguagem | C# / .NET 10, NativeAOT no residente (e no Worker, se as bibliotecas permitirem) | MIT |
| Janela e entrada | Win32 (SetWindowsHookEx WH_MOUSE_LL / WH_KEYBOARD_LL) | — |
| Desenho do menu | Direct2D via Vortice.Windows + DWM Acrylic | MIT |
| Drag & drop | OLE IDropTarget (COM gerado por source generator, compatível com NativeAOT) | — |
| Imagens | NetVips (libvips) | MIT / LGPL |
| PDF | PDFsharp (estrutura) + PDFium (renderização) | MIT / BSD |
| Office | Word do PC via COM (se houver) ou LibreOffice headless | — / MPL 2.0 |
| Áudio/vídeo (v1.x) | FFmpeg build LGPL | LGPL |
| Instalador | Inno Setup (depois MSIX/Store) | Gratuito |

**Proibido:** Ghostscript e iText, que são AGPL e obrigariam abrir o código do produto.

### 5.3 Módulos (solução .NET)

```
Pizza/
├─ src/
│  ├─ Pizza.App/            # residente: bandeja, hooks, overlay, painel, notificações
│  ├─ Pizza.Core/           # estados, configuração, logs, contratos IPC
│  ├─ Pizza.Worker/         # processo de conversão
│  └─ Pizza.Converters/     # IConverter + implementações por família
├─ tests/
│  ├─ Pizza.Core.Tests/
│  ├─ Pizza.Converters.Tests/
│  └─ samples/              # arquivos de amostra (pequenos, versionados)
├─ installer/               # script do Inno Setup
├─ docs/                    # este documento, ADRs, wireframes
└─ design/                  # exportações do mockup
```

### 5.4 Responsabilidades

| Módulo | Faz | Não faz |
| --- | --- | --- |
| `GestureHook` | Detecta Ctrl + início de arraste de arquivo, scroll e Esc | Não desenha nada |
| `RadialOverlay` | Desenha o menu, troca aba, destaca fatia, recebe o drop | Não converte |
| `OptionsPanel` | Mostra opções, lembra "usar sempre" | Não converte |
| `Core.StateMachine` | Única fonte da verdade do estado (Parte 6) | — |
| `Core.Settings` | Lê e grava `settings.json` | — |
| `Core.Ipc` | Fala com o Worker pelo Named Pipe | — |
| `Notifier` | Notificações do Windows (toast) | — |
| `Worker.JobQueue` | Fila, paralelismo, cancelamento | — |
| `Converters.*` | Converte um arquivo e devolve o resultado | Não fala com a UI |

### 5.5 Contrato `IConverter`

```csharp
public interface IConverter
{
    string Id { get; }                       // "image.png", "pdf.compress"
    IReadOnlyList<string> InputExtensions { get; }
    string OutputExtension { get; }
    OptionSchema Options { get; }            // define o que o painel mostra
    Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        IProgress<double> progress,
        CancellationToken ct);
}
```

Adicionar um formato novo = escrever uma classe e registrar no `ConverterRegistry`. Menu e painel se montam sozinhos a partir do registro.

### 5.6 Contrato IPC (Residente ↔ Worker)

Mensagens JSON, uma por linha, pelo Named Pipe `\\.\pipe\Pizza-<SID do usuário>`.

| Mensagem | Direção | Campos |
| --- | --- | --- |
| `warmup` | App → Worker | `category` (image, pdf, office), `source` (caminho) |
| `convert` | App → Worker | `jobId`, `converterId`, `source`, `options` |
| `progress` | Worker → App | `jobId`, `percent` |
| `done` | Worker → App | `jobId`, `output`, `elapsedMs` |
| `error` | Worker → App | `jobId`, `code`, `message` |
| `cancel` | App → Worker | `jobId` |

### 5.7 Configuração (`%LOCALAPPDATA%\Pizza\settings.json`)

```json
{
  "gestureKey": "Ctrl",
  "startWithWindows": true,
  "theme": "system",
  "accentColor": "#2F6BFF",
  "output": { "mode": "besideOriginal", "fixedFolder": null },
  "workerIdleSeconds": 45,
  "remembered": {
    "image.png": { "resolution": "original", "compression": "balanced" }
  }
}
```

---

## Parte 6 — Máquina de estados

O app tem 7 estados. Programar cada estado e cada transição abaixo cobre o fluxo inteiro.

```
            Esc / soltou fora
   ┌───────────────────────────────────────────────┐
   ▼                                               │
[1 Repouso] ──Ctrl+arraste──▶ [2 Arrastando] ──tipo detectado──▶ [3 Menu] ──soltou na fatia──▶ [4 Painel]
   ▲   ▲                                              │                         │
   │   │                                  opções salvas│                         │Converter
   │   │                                              ▼                         ▼
   │   └──notificação── [6 Concluído] ◀──sucesso── [5 Convertendo] ◀────────────┘
   │                                                  │
   └──────────aviso de erro────────── [7 Erro] ◀──falhou
```

| Estado | Quem roda | O que aparece | Saídas |
| --- | --- | --- | --- |
| 1 Repouso | Residente | Só o ícone na bandeja | Ctrl + arraste de arquivo → 2 |
| 2 Arrastando | Residente | Overlay invisível pronto | Tipo detectado → 3 |
| 3 Menu | Residente (Worker começa a aquecer) | Menu radial | Scroll troca a aba · hover destaca · soltar na fatia → 4 (ou 5 se há opções salvas) · Esc/soltar fora → 1 |
| 4 Painel | Residente | Painel de opções | Converter/Enter → 5 · Cancelar/Esc → 1 |
| 5 Convertendo | Worker | Nada na tela (progresso na notificação, se demorar) | Sucesso → 6 · falha → 7 |
| 6 Concluído | Residente | Arquivo novo + notificação | Notificação fecha → 1 |
| 7 Erro | Residente | Notificação com o motivo | Notificação fecha → 1 |

**Invariantes:** todo caminho termina em Repouso; o original nunca é alterado; o Worker morto não derruba o residente.

---

## Parte 7 — Conversores e níveis de qualidade

Cada conversão com perda oferece de 3 a 4 níveis com valores reais, e um deles já vem marcado como padrão. Conversões sem perda mostram apenas as opções que fazem sentido.

### 7.1 Aba Imagem

Ordem das fatias (ADR-012, alterado pelo ADR-024): começando no topo e seguindo no sentido horário, **JPG, PNG, WEBP, AVIF, Imagem → PDF, TIFF, ICO, GIF**, sem o formato de origem. Com mais de 6, as fatias se dividem em voltas, e a roda do mouse percorre as voltas antes de trocar de aba.

| Fatia | Opções no painel | Padrão |
| --- | --- | --- |
| JPG | Qualidade: Máxima 95% · Alta 85% · Equilibrada 75% · Compacta 60% | Equilibrada |
| PNG | Resolução: Original · 75% · 50%; Compressão sem perda: Rápida · Equilibrada · Máxima | Original · Equilibrada |
| WEBP | Igual ao JPG | Equilibrada |
| TIFF | Sem opções (sem perda) | — |
| ICO | Tamanho: Grande 256 px · Médio 128 px · Pequeno 48 px | Grande |
| GIF | Sem opções (256 cores) | — |
| Imagem → PDF | Resolução: Original · Escritório 150 dpi · Tela 96 dpi | Original |

### 7.2 Aba PDF

| Fatia | Opções | Padrão |
| --- | --- | --- |
| Comprimir | Impressão 300 dpi · Escritório 150 dpi · Tela 72 dpi | Escritório |
| Juntar | Sem opções (ordem do arraste) | — |
| Dividir | Cada página · Pela metade | Cada página |
| Imagens | Alta 300 dpi · Média 150 dpi · Leve 96 dpi | Média |
| Word | Sem opções (mantém o layout) | — |
| Girar | 90° · 180° · 270° | 90° |

### 7.3 Aba Documento (v0.3)

| Fatia | Opções | Padrão |
| --- | --- | --- |
| DOCX/XLSX/PPTX → PDF | Sem opções | — |
| PDF/A (arquivamento) | Sem opções | — |

### 7.4 Aba Áudio/Vídeo (v1.x)

| Fatia | Opções | Padrão |
| --- | --- | --- |
| MP3 | 320 · 192 · 128 kbps | 192 kbps |
| MP4 | Original · 1080p · 720p | 1080p |

Também entram (SPECS §2.1): WAV, FLAC, M4A/AAC, OGG/Opus, WebM, AV1, GIF para vídeo, extrair áudio de vídeo e MOV/MKV para MP4, com resolução, taxa de quadros e meta de tamanho. HEVC depende de análise de patentes; RAW e ProRes estão em estudo.

### 7.5 Aba Web (v1.x)

Minificar HTML, CSS e JS; TTF e OTF para WOFF2; gerar .gz e .br de um arquivo.

### 7.6 Regras gerais

- O app lembra a última opção usada em cada conversão.
- "Usar sempre" pula o painel nas próximas vezes; dá para desfazer nas Configurações.
- O formato de origem não aparece como fatia.

---

## Parte 8 — Desempenho e leveza

### 8.1 Orçamento

| Situação | RAM | CPU |
| --- | --- | --- |
| Repouso | < 20 MB | 0% (hooks só reagem a eventos) |
| Menu aberto | < 40 MB | Pico curto |
| Convertendo | Temporário (Worker) | Temporário |

### 8.2 Metas de tempo

| Conversão | Meta | Técnica |
| --- | --- | --- |
| Imagens | Fração de segundo | NetVips, um arquivo por núcleo |
| PDF: Juntar, Dividir, Girar | Quase instantâneo | Só reorganiza a estrutura |
| PDF: Comprimir, Imagens | Poucos segundos em arquivos grandes | Páginas em paralelo |
| Office → PDF | Poucos segundos | Word do PC ou LibreOffice aberto com antecedência |

### 8.3 Técnicas

1. **Worker aquecido:** começa a abrir no estado 3 (menu) e já lê o arquivo de origem enquanto o usuário escolhe.
2. **Worker vivo por pouco tempo:** fica 30 a 60 s esperando outra conversão e depois fecha.
3. **LibreOffice adiantado:** abre em segundo plano quando a aba Documento aparece.
4. **Sem espera na tela:** o painel fecha no clique, e a notificação avisa no fim.
5. **NativeAOT** no residente: abre rápido e dispensa instalar o .NET.

Todas as metas devem ser **medidas** na Fase 5 com o conjunto de amostras de `tests/samples`.

---

## Parte 9 — Segurança, privacidade e confiabilidade

- **Offline por padrão:** nenhuma chamada de rede, exceto a checagem de atualização (desligável).
- **Sem telemetria** no MVP. Se entrar depois, só com o usuário optando por ativar.
- **Original intacto:** gravar primeiro em arquivo temporário e renomear só no fim (escrita atômica).
- **Arquivos maliciosos:** o Worker roda com privilégio de usuário comum, com tempo limite por job e limites de memória. Uma falha nele nunca derruba o residente.
- **Pipe privado:** o nome do pipe inclui o SID do usuário e a ACL permite só o próprio usuário.
- **Assinatura de código** em todos os executáveis e no instalador.
- **Dependências:** inventário de licenças e checagem de vulnerabilidades no CI.
- **Logs locais** em `%LOCALAPPDATA%\Pizza\logs`, sem conteúdo dos arquivos, com rotação.
- **Antivírus:** hooks globais podem ser vistos como suspeitos; mitigar com assinatura e envio do executável para análise da Microsoft.
- **Engenharia reversa:** NativeAOT gera código nativo, bem mais difícil de ler que IL do .NET. A validação de licença (v1.0) não deve depender só do cliente.

---

## Parte 10 — Instalação, distribuição e ícone

### 10.1 Peças

| Peça | O que é |
| --- | --- |
| `Pizza-Setup.exe` | O que se vende/distribui (instalador) |
| `Pizza.exe` | Residente, roda em segundo plano com ícone na bandeja |
| `Pizza.Worker.exe` | Abre só durante a conversão |

### 10.2 Instalação

- Por usuário, em `%LOCALAPPDATA%\Programs\Pizza`, **sem pedir administrador**.
- Sem pré-requisitos (NativeAOT + bibliotecas embutidas).
- Inicia com o Windows pela chave `Run` do usuário.
- Desinstala limpo pelas Configurações do Windows.
- Pacotes opcionais (LibreOffice, FFmpeg) baixados só quando usados.
- Depois: winget e Microsoft Store.

### 10.3 Ícone

**Conceito:** o próprio menu, ou seja, um anel de fatias de pizza mordidas com uma fatia em destaque e uma folha de documento no centro. Combina com o nome "Pizza" e ensina o gesto.

| Versão | Uso | Estilo |
| --- | --- | --- |
| Grande (256 px) | Instalador, Store, atalho | Vidro, igual ao design |
| Bandeja (16–32 px) | Ícone ao lado do relógio | Chapado, com versão clara e escura |
| Estados da bandeja | Feedback | Fatia gira = convertendo · ponto verde = concluído · fatia vermelha = erro |

Arquivo `.ico` com 16, 20, 24, 32, 48, 64 e 256 px.

### 10.4 Comercial

- Versão gratuita limitada + versão paga (licença única ou assinatura: decisão em aberto).
- Piloto em um cliente (cartório ou contabilidade) antes da venda aberta.

---

## Parte 11 — Qualidade e testes

| Nível | O que cobre | Ferramenta |
| --- | --- | --- |
| Unitário | Máquina de estados, nomes de saída, configurações | xUnit |
| Conversores | Cada `IConverter` com arquivos de `tests/samples`; compara dimensões, páginas, tamanho | xUnit |
| Integração | Residente ↔ Worker pelo pipe, cancelamento, Worker caindo | xUnit + processo real |
| Desempenho | Tempo por conversão e RAM em repouso contra as metas da Parte 8 | BenchmarkDotNet + script |
| Manual (UX) | Roteiro do fluxo W1–W7 em Windows 10 e 11, tema claro/escuro, 100%/150% de escala | Checklist |

**Casos obrigatórios:** arquivo corrompido, arquivo bloqueado por outro programa, nome com acentos e espaços, caminho longo (> 260 caracteres), disco cheio, pasta sem permissão de escrita, arquivo de 0 byte, PDF com senha.

**Definição de pronto (para qualquer tarefa):** código revisado, testes passando no CI, sem aviso novo de análise estática, documentação atualizada se mudou comportamento.

---

## Parte 12 — Repositório e forma de trabalho

### 12.1 GitHub

- Nome: **Pizza** (`GuiTiburcio/Pizza`), gratuito e GPL-3.0 (ADR-014). Privado até corrigir os desvios da auditoria.
- Branch principal `main`; trabalho em `feat/…`, `fix/…`, `docs/…`. Regras de proteção de branch em repositório privado exigem plano pago do GitHub; até lá, a regra é seguida por disciplina.
- Pull request para tudo, mesmo trabalhando sozinho (histórico e revisão).
- Commits no padrão Conventional Commits, em português: `feat(overlay): destaca fatia no hover`.
- Idioma: nomes no código em inglês; commits, documentação e textos da interface em português.
- Versões com tags `v0.1.0`, `v0.2.0`…
- CI (GitHub Actions): build, testes, análise de licenças a cada PR; build do instalador a cada tag.
- Issues com rótulos por parte deste documento (`parte-5-arquitetura`, `parte-7-conversores`…).

### 12.2 Arquivos-base do repositório

```
README.md           # o que é, como instalar, como compilar
docs/PIZZA-PROJETO.md   # este documento
docs/adr/           # uma decisão por arquivo
LICENSE             # decisão em aberto (código fechado para venda)
THIRD-PARTY-NOTICES.md  # licenças das bibliotecas
.github/workflows/  # CI
```

### 12.3 Ambiente de desenvolvimento

- .NET 10 SDK.
- Visual Studio Build Tools com a carga "Desenvolvimento para desktop com C++" (linker exigido pelo NativeAOT).
- Editor: VS Code com a extensão C# Dev Kit.
- Git + GitHub CLI (`gh`).

### 12.4 Trabalho com o Claude

- O ideal é usar o **Claude Code no PC**, para editar, compilar e testar no próprio projeto.
- Uma parte deste documento por sessão, sempre fechando com o critério de pronto da fase.

---

## Parte 13 — Plano de execução (fases)

Cada fase só começa quando a anterior cumpriu o **critério de pronto**.

| Fase | Entrega | Pronto quando | Depende de |
| --- | --- | --- | --- |
| **0 — Ambiente** | .NET 10 SDK + Build Tools C++, repositório Pizza no GitHub, solução com os 4 projetos, CI rodando | A solução vazia compila no CI | — |
| **1 — O gesto** | Residente NativeAOT na bandeja; hooks de Ctrl + arraste e scroll; overlay que abre e fecha com Esc | Ctrl + arrastar abre um círculo simples e o scroll muda um texto | 0 |
| **2 — Primeira conversão** | Fatias como alvo de drop; JPG → PNG pelo Worker; nome de saída sem sobrescrever | Soltar um JPG na fatia PNG gera o PNG correto | 1 |
| **3 — Motor de conversores** | `IConverter`, registro, painel de opções, aba Imagem completa, notificações | 10 imagens mistas convertem sem travar a interface | 2 |
| **4 — Visual Pizza** | Vidro, fatias arredondadas, hover, animação, tema claro/escuro | O app fica igual ao canvas aprovado | 3 |
| **5 — Medir e endurecer** | Metas de RAM, CPU e tempo medidas; casos obrigatórios da Parte 11 | Todas as metas da Parte 8 cumpridas | 4 |
| **6 — Aba PDF** | Comprimir, Juntar, Dividir, Imagens, Word, Girar | Testes de conversores PDF verdes | 5 |
| **7 — Aba Documento** | Office → PDF (Word COM / LibreOffice) | DOCX → PDF em poucos segundos | 6 |
| **8 — Produto** | Configurações, instalador por usuário, assinatura, atualização automática, ícone | Instala limpo num PC de cliente sem aviso | 7 |
| **9 — Piloto e venda** | Piloto em cliente, licença, site/Store | Cliente piloto usando por 2 semanas sem bloqueio | 8 |

### 13.1 Etapas de validação

Cada fase é quebrada em etapas menores. Ao fim de cada etapa o dono do produto valida antes de seguir.

| Etapa | Entrega | Validação |
| --- | --- | --- |
| 0a | Repositório privado, `docs/` com a spec e os ADRs, README | Conferir no GitHub |
| 0b | Solução com os 4 projetos e os testes, CI no GitHub Actions | CI verde |
| 1a | Prova de conceito do gesto: Ctrl + arrastar do Explorer abre um círculo que recebe o drop e mostra o caminho; scroll troca um texto | Testar na área de trabalho e no Explorer |
| 1b | Bandeja, Esc, overlay limpo | Testar no PC |

A etapa 1a vem antes de tudo porque receber o arraste do Explorer numa janela própria (OLE `IDropTarget` com NativeAOT) é o ponto de maior incerteza técnica do projeto.

---

## Parte 14 — Riscos e decisões em aberto

### 14.1 Riscos

| Risco | Impacto | Mitigação |
| --- | --- | --- |
| Antivírus marcar o hook global | Cliente não instala | Assinatura + envio para análise; hooks mínimos |
| Ctrl + arraste conflitar com a cópia nativa | Cópia indesejada | O overlay só aceita drop nas fatias; fora delas cancela antes do Explorer |
| Receber o drop do Explorer com NativeAOT (COM/OLE) | Gesto não funciona | Prova de conceito na etapa 1a, antes de qualquer outra coisa |
| Vidro (Acrylic) pesar em PCs fracos | Lentidão | Desligar o desfoque automaticamente e usar vidro "sólido" |
| LibreOffice grande (~350 MB) | Instalador pesado | Download sob demanda |
| PDF → Word sem biblioteca AGPL | Qualidade fraca na v0.2 | Avaliar opções na Fase 6; pode sair do escopo da v0.2 |
| Hook não funcionar em janelas elevadas (admin) | Gesto falha em alguns apps | Documentar; considerar modo opcional elevado no futuro |
| Escopo crescer antes do MVP | Atraso | Respeitar a tabela de versões da Parte 1.5 |

### 14.2 Decisões em aberto

- [ ] Preço: licença única ou assinatura
- [ ] Vender pela empresa ou por conta própria
- [x] Licença: gratuito e GPL-3.0 (ADR-014)
- [x] Ordem das fatias da aba Imagem (ADR-012); as demais abas decidem na sua fase
- [ ] Nome final do produto (o nome de trabalho é "Pizza")

---

## Anexo A — Registro de decisões (ADRs)

Cada decisão também tem um arquivo próprio em [`docs/adr/`](adr/).

| # | Decisão | Motivo | Alternativas descartadas |
| --- | --- | --- | --- |
| 001 | Gesto Ctrl + arrastar | No pior caso só copia; funciona igual na área de trabalho e no Explorer | Shift (move arquivo), Alt e Ctrl+Shift (criam atalho) |
| 002 | Dois processos (residente + Worker) | Leveza em repouso e isolamento de falhas | Processo único |
| 003 | NativeAOT + Win32/Direct2D no residente | RAM baixa e sem pré-requisito de .NET | WPF residente (dezenas de MB) |
| 004 | NetVips para imagens | Mais rápido e com menos memória | Magick.NET |
| 005 | Sem AGPL | Permite vender software fechado | Ghostscript, iText |
| 006 | Qualidade escolhida **depois** de soltar, num painel | Não briga com o scroll e fica mais claro | Níveis no hover em cima do círculo |
| 007 | Fatias de pizza separadas e com cantos arredondados | Visual limpo, alvo de drop claro | Bolinhas, anel contínuo |
| 008 | Abas indicadas por barra no topo + 4 traços no miolo | Mostra onde o usuário está sem poluir | Contorno tracejado externo |
| 009 | Saída ao lado do original, sem sobrescrever | Previsível e seguro | Pasta fixa (fica como opção) |
| 010 | Instalação por usuário, sem admin | Instalação fácil em PCs de escritório | Instalação por máquina |
| 011 | .NET 10 em vez de .NET 8 | .NET 10 é o LTS atual; o suporte do .NET 8 termina em 10/11/2026 | .NET 8 |
| 012 | Ordem das fatias da aba Imagem: JPG, PNG, WEBP, Imagem → PDF, TIFF, ICO, GIF, sem a origem | Formatos mais usados primeiro; regra fixa e previsível | Ordem do canvas sem GIF |
| 013 | Repositório privado; decidir abrir junto com a licença no v1.0 | Produto será vendido; histórico público não pode ser desfeito | Público desde o início |
| 014 | Gratuito e GPL-3.0; substitui o ADR-013 | Transparência e confiança | Privado e venda |
| 015 | Animações: Sempre / Seguir o Windows / Nunca, padrão Sempre | Windows com animações desligadas escondia o deslize | Seguir sempre o Windows |
| 016 | Esc sem hook de teclado | Promessa do SEC-01 | Hook durante o gesto |
| 017 | Lote na v1.x, sem teto, fila de 2 por núcleo | MVP menor | Lote no MVP, teto de 500 |
| 018 | Notificação e painel próprios | Visual e privacidade | Notificação nativa |
| 019 | Simplificações S-01 a S-05 | Menos superfície de ataque | Word por COM, 15 políticas |
| 020 | Menu do ícone próprio de vidro, com Play/Pause/Stop da fila | Pedido do mantenedor; menu curto | Linhas de texto no menu do Windows |
| 021 | Pipe sem nome entre residente e Worker | Mais simples e sem como ser aberto por outro processo | Pipe com nome e ACL condicional |
| 022 | Imagem → PDF com gerador próprio, folha A4 e margem de 10 mm | PDFsharp virou AGPL na 6.5.0; sem dependência nova | PDFsharp 6.2.4 ou AGPL; página do tamanho da imagem |
| 025 | Soltar em qualquer lugar e escolher o formato com um clique; Esc ou clique fora fecham | Não exige mirar durante o arraste | Fechar também por tempo |
| 024 | AVIF depois do WEBP e voltas de fatias quando há mais de 6 | Nenhum formato fica de fora, sem tecla extra no arraste | Fatia "Mais", Shift + roda, AVIF no fim |
| 023 | GDI+ e desfoque próprio do vidro, substituindo Direct2D e Acrylic do ADR-003 | Acrylic e composição do Windows não funcionam com o disco inativo; sem dependência nova | Vortice, Acrylic, composição, API não documentada |

---

## Anexo B — Glossário

| Termo | Significado |
| --- | --- |
| Residente | Processo que fica sempre ligado (`Pizza.exe`) |
| Worker | Processo que faz a conversão e fecha depois (`Pizza.Worker.exe`) |
| Fatia | Cada botão do menu radial, um por formato ou ação |
| Aba | Categoria de fatias (Imagem, PDF, Documento, Áudio) |
| Miolo | Centro do menu, onde aparece o arquivo |
| Painel | Janela de opções que abre depois de soltar |
| Hook | Gancho do Windows que avisa sobre teclado e mouse |
| Overlay | Janela transparente por cima de tudo, onde o menu é desenhado |
| NativeAOT | Compilação do C# para executável nativo |

---

## Anexo C — Artefatos já produzidos

| Artefato | Conteúdo |
| --- | --- |
| Canvas "Pizza — Wireframes e Engenharia" | Pasta principal do projeto: design aprovado, mockup do fluxo, wireframes WF-1 a WF-6 e diagramas ENG-1 a ENG-4 |
| Canvas "Menu Radial de Vidro" | Design interativo (abas Imagem e PDF) e mockup do fluxo em 6 telas (JPG → PNG) |
| Doc "Conversor Ctrl+Arrastar — Visão, Arquitetura e Plano" | Versão anterior desta especificação, com diagramas de arquitetura e de estados |
| Este arquivo | Especificação consolidada (`docs/PIZZA-PROJETO.md`) |

### C.1 Divergências conhecidas entre o canvas e esta especificação

Onde houver diferença, **vale esta especificação**. O visual (vidro, cores, medidas) continua vindo do canvas.

| Onde no canvas | O que mostra | O que vale |
| --- | --- | --- |
| Referência · aba Imagem | Níveis de qualidade acima do disco, no hover | Painel de opções depois de soltar (ADR-006, WF-3) |
| Referência · aba Imagem | Arquivo `fachada.jpg` com a fatia JPG visível | O formato de origem não aparece como fatia |
| Referência · aba Imagem | PNG "sem níveis" | PNG com Resolução e Compressão (Parte 7.1) |
| Referência · aba Imagem | Sem a fatia GIF | Ordem do ADR-012, com GIF |
