# Pizza — Especificação técnica (SPECS)

| Campo | Valor |
| --- | --- |
| Produto | Pizza: conversor de arquivos por Ctrl + arrastar, com menu radial |
| Versão da especificação | 1.2 (emendas na §2.1, que prevalecem sobre o restante do texto) |
| Data | 08/11/2026 |
| Licença do código | GPL-3.0 |
| Modelo | Gratuito, sem licença, sem conta, sem telemetria |
| Plataforma | Windows 10 21H2+ e Windows 11, x64 |
| Documentos relacionados | `PIZZA-PROJETO.md` (visão geral) · canvas "Pizza — Wireframes e Engenharia" (WF-1 a WF-6, ENG-1 a ENG-4) |

> **Princípio que governa tudo:** o Pizza trata documentos de cartórios, contabilidades e farmácias. **Nenhum dado sai do computador**, o app **nunca vê nada além do que precisa** e **qualquer falha deixa o original intacto**. Um requisito que contrarie esse princípio está errado, mesmo que pareça mais prático.

Esta especificação **substitui** o que o `PIZZA-PROJETO.md` dizia sobre venda e licença (Partes 1.4 e 10.4). O Pizza agora é gratuito e de código aberto.

---

## Sumário

1. [Como ler esta especificação](#1-como-ler-esta-especificação)
2. [Decisões da entrevista](#2-decisões-da-entrevista)
3. [Inventário de dados (LGPD)](#3-inventário-de-dados-lgpd)
4. [Modelo de ameaças](#4-modelo-de-ameaças)
5. [Requisitos funcionais (FR)](#5-requisitos-funcionais-fr)
6. [Requisitos de segurança (SEC)](#6-requisitos-de-segurança-sec)
7. [Requisitos de privacidade e LGPD (PRIV)](#7-requisitos-de-privacidade-e-lgpd-priv)
8. [Requisitos não funcionais (NFR)](#8-requisitos-não-funcionais-nfr)
9. [Políticas para a TI (GPO / Intune)](#9-políticas-para-a-ti-gpo--intune)
10. [Formatos e contratos](#10-formatos-e-contratos)
11. [Cadeia de build e release](#11-cadeia-de-build-e-release)
12. [Plano de testes de segurança](#12-plano-de-testes-de-segurança)
13. [Gestão de vulnerabilidades e incidentes](#13-gestão-de-vulnerabilidades-e-incidentes)
14. [Conformidade: LGPD e CNJ](#14-conformidade-lgpd-e-cnj)
15. [Rastreabilidade: requisito → fase → teste](#15-rastreabilidade-requisito--fase--teste)
16. [Itens em aberto](#16-itens-em-aberto)
- [Apêndice A — Kit LGPD: nota técnica de privacidade](#apêndice-a--kit-lgpd-nota-técnica-de-privacidade)
- [Apêndice B — Kit LGPD: modelo de registro da operação](#apêndice-b--kit-lgpd-modelo-de-registro-da-operação)
- [Apêndice C — Kit LGPD: respostas para o encarregado (DPO)](#apêndice-c--kit-lgpd-respostas-para-o-encarregado-dpo)

---

## 1. Como ler esta especificação

**Palavras-chave**

| Termo | Significado |
| --- | --- |
| **DEVE** | Obrigatório. Sem isso a versão não é liberada. |
| **NÃO DEVE** | Proibido. |
| **DEVERIA** | Fortemente recomendado; desvio só com ADR justificando. |
| **PODE** | Opcional. |

**Identificadores:** `FR-` funcional, `SEC-` segurança, `PRIV-` privacidade/LGPD, `NFR-` não funcional, `POL-` política de TI, `T-` ameaça, `TST-` teste. Cada requisito tem **critério de aceite**: se não dá para testar, não é requisito.

**Fases:** os números de fase (F0–F9) seguem a Parte 13 do `PIZZA-PROJETO.md`.

---

## 2. Decisões da entrevista

**Emenda de 10/11/2026 — ADR-025 (prevalece sobre FR-07/20/21 e SEC-03 abaixo):** Ctrl + arrastar abre o menu. Soltar em qualquer posição prepara o arquivo e mantém o menu aberto para seleção por clique; soltar numa fatia continua selecionando diretamente. Depois da soltura, Esc recebido pela própria janela ou perder o foco cancela. Durante o arraste, Esc continua sendo tratado pelo OLE, sem hook de teclado. Nenhuma cópia física é criada ao preparar o arquivo. SEC-01/02/04/05/06/31 permanecem obrigatórios.

Decisões tomadas na entrevista (método *grilling*), em 5 rodadas. Viram ADRs em `docs/adr/`.

| # | Tema | Decisão | Requisitos |
| --- | --- | --- | --- |
| Q1 | Licença | Gratuito, sem chave, sem servidor de ativação | PRIV-01 |
| Q2 | Atualização | GitHub Releases por HTTPS; assinatura verificada; o usuário aprova | SEC-50 a SEC-55 |
| Q3 | Telemetria | Nenhuma; diagnóstico só sai se o usuário exportar | PRIV-02, PRIV-20 |
| Q4 | Implantação | Instalador por usuário + MSI por máquina com políticas | FR-70, POL-* |
| Q5 | Código | Aberto, GPL-3.0 | §11 |
| Q6/Q9 | Logs | ID fixo por arquivo; nomes num mapa criptografado (DPAPI); 30 dias; exportação só com IDs, nomes opcionais | PRIV-10 a PRIV-17 |
| Q7 | Temporários | Arquivo oculto na pasta de destino; gravação atômica; apagado em falha | SEC-30 a SEC-34 |
| Q8 | Metadados | Remover GPS, autor e empresa por padrão; opção de manter | PRIV-30 a PRIV-32 |
| Q10 | Worker | AppContainer sem capabilities + Job Object; acesso só por handles | SEC-20 a SEC-28 |
| Q11 | Teclado | Nenhum hook de teclado | SEC-01 a SEC-04 |
| Q12 | Build | Só o CI gera instalador; SBOM; varredura; SignPath | §11 |
| Q13 | Limites | 100 MP · 2 GB · 2.000 páginas · 2 min · 1,5 GB RAM, ajustáveis | SEC-40 a SEC-44 |
| Q14 | Office | Só leitura; macros e conteúdo ativo bloqueados à força | SEC-60 a SEC-64 |
| Q15 | PDF com senha | Pedida no painel; só em memória; nunca gravada nem logada | SEC-70 a SEC-73 |
| Q16 | Origem web | O convertido herda a marca "veio da internet" (MotW) | SEC-35 |
| Q17 | Proteção da saída | Mantém a senha do PDF; se o destino não suporta, avisa e pede confirmação | SEC-74, SEC-75 |
| Q18 | Kit LGPD | Incluído (Apêndices A, B e C) | §14 |
| Q19 | Vulnerabilidades | SECURITY.md, reporte privado, prazos | §13 |

### 2.1 Emendas da versão 1.2

Decisões das rodadas de alinhamento de 08/11/2026. **Onde houver conflito, esta tabela prevalece** sobre o restante do documento.

| Requisito | Emenda | ADR |
| --- | --- | --- |
| Licença e repositório | Gratuito e GPL-3.0. O repositório fica privado até corrigir os desvios da auditoria | 014 |
| NFR-06 | Opção Animações: Sempre (padrão) / Seguir o Windows / Nunca. Em Seguir o Windows com animações desligadas, a transição só troca a opacidade | 015 |
| FR-04 | Um entalhe inteiro = uma aba; frações de touchpad se acumulam. Ao trocar de aba, as fatias giram levemente em volta do miolo enquanto os formatos trocam, em 180 ms | 015 |
| SEC-01, SEC-02, SEC-03, FR-07 | Sem hook de teclado, para todos. O Ctrl é lido só no início do arraste. O menu orienta "Solte o Ctrl e aperte Esc para cancelar"; Ctrl + Esc abre o Menu Iniciar | 016 |
| FR-09 (novo) | Arquivo sem conversão disponível não abre o menu; o Ctrl + arrastar segue como cópia normal do Windows | — |
| FR-28, FR-01 | Lote passa para a v1.x. Antes disso (FR-30, novo), arrastar vários mostra a quantidade, recusa o drop e avisa "Por enquanto, um arquivo por vez", sem copiar nada | 017 |
| FR-31 (novo, v1.x) | Tipos misturados: o menu abre na aba do primeiro arquivo, só os compatíveis são convertidos e os demais aparecem como "ignorado", sem bloquear o lote | 017 |
| FR-32 (novo, v1.x) | O painel de opções abre uma vez e vale para todos os compatíveis; cada saída fica ao lado do seu original | 017 |
| SEC-46 | Sem teto de arquivos; fila com no máximo 2 conversões por núcleo | 017 |
| FR-23 | "Manter metadados" funciona em JPG, PNG e WEBP. Em PDF e ICO, a caixa aparece desativada, com o aviso de que o formato não guarda metadados | — |
| Imagem → PDF | Gerador próprio, sem PDFsharp (AGPL desde a 6.5.0). Folha A4 acompanhando a orientação da imagem, margem de 10 mm. A biblioteca das demais operações de PDF será decidida na Fase 6 | 022 |
| Aba Imagem e FR-04 | AVIF entra depois do WEBP. Com mais de 6 formatos, as fatias se dividem em voltas; a roda percorre as voltas da aba antes da próxima aba, e pontos no miolo mostram a volta. Aba sem conversão mostra "Em breve" com o que vem nela | 024 |
| Áudio/Vídeo (v1.x) | MP3, WAV, FLAC, M4A/AAC e OGG/Opus; MP4, WebM e AV1; GIF para vídeo; extrair áudio de vídeo; MOV e MKV para MP4; opções de resolução, taxa de quadros e meta de tamanho. HEVC só depois de análise de patentes e licença. Em estudo para fotógrafos e videomakers: fotos RAW (CR2, NEF, ARW, DNG), que exigem biblioteca nova, e ProRes para MP4 | — |
| Web (v1.x, aba nova) | Minificar HTML, CSS e JS; TTF e OTF para WOFF2; gerar .gz e .br de um arquivo. Ativar compressão em servidor fica de fora, por não ser conversão de arquivo; SVG a partir de imagem fica em estudo | — |
| Desenho e vidro | GDI+ do Windows, sem Direct2D nem Vortice. Vidro desfocado pelo próprio Pizza a partir de uma cópia do trecho da tela atrás do menu, só em memória e zerada ao fechar; branco sólido 92% com a transparência do Windows desligada | 023 |
| TST-03 | Sem SharpFuzz: ele só observa código C#, e quem interpreta os arquivos é o libvips nativo. Teste próprio corrompe arquivos válidos de cada formato de forma repetível e os passa pelo Worker real: cerca de 100 casos em cada PR e 5.000 por semana. Queda do Worker, travamento, temporário sobrando ou erro fora de `CORRUPTED`, `UNSUPPORTED` e `LIMIT_EXCEEDED` reprovam | — |
| SEC-45 | HEIC fica de fora da lista enquanto o libvips empacotado não trouxer o codec HEVC: o menu não abre para `.heic`. AVIF continua aceito | — |
| FR-22 | "Usar sempre estas opções" passa para a Fase 8, junto com a tela de Configuração que permite desfazer. Até lá, o painel sempre abre e Enter converte com o padrão | — |
| FR-40 a FR-42 | Notificação em janela própria no canto inferior direito, com o design do Pizza. Respeita "Não perturbe" e é anunciada ao leitor de tela. Sucesso some em 5 s, e o mouse em cima pausa; erro fica até fechar. No máximo 3 empilhadas e um cartão "+N" que abre o painel | 018 |
| FR-40 a FR-43 (detalhes) | O disco fecha ao soltar ou confirmar o painel. Com a sessão bloqueada ou em "Não perturbe", o aviso fica guardado e aparece depois, sem som. Em erro, "Tentar de novo" repete com as mesmas opções e "Ver detalhes" abre a pasta de logs. O cartão "+N" abre o painel "Arquivos..." | — |
| FR-44 (detalhes) | O painel fica no canto inferior direito, fecha ao perder o foco ou com Esc e guarda as 100 conversões mais recentes. Lista as em andamento, concluídas e com erro (estas com o motivo e "Tentar de novo"); as canceladas não entram. Arquivo movido ou apagado mostra "Arquivo não encontrado" na hora do clique | — |
| FR-43 (novo) | Som leve em WAV, tocado pelo Windows, só ao concluir e no erro; "Não perturbe" silencia. Os sons são originais do projeto | 018 |
| FR-44 (novo, Fase 3) | Painel "Arquivos...", no clique direito do ícone da bandeja: só em memória, desde que o Pizza abriu, com "Abrir" e "Mostrar na pasta". Na v1.x, também cancelar o lote ou um arquivo e pausar/continuar | 018 |
| PRIV-18 | Com a sessão bloqueada, a notificação espera o desbloqueio. Como a janela é própria, não há histórico nem espelhamento do Windows | 018 |
| FR-50, FR-51 | Menu do ícone: Pizza — ativo, Arquivos... (Fase 3), Configuração (Fase 8), Play/Pause/Stop da fila de conversões (v1.x), Abrir pasta de logs e Sair. Cada item só aparece quando funcionar. Vira janela própria de vidro na Fase 4, com uso completo por teclado. Exportar diagnóstico, Limpar dados e Sobre ficam em Configuração. Sai "Pausar por 1 hora" | 019, 020 |
| SEC-61, SEC-65, SEC-66, POL-08, `OFFICE_UNTRUSTED_SOURCE` | Removidos: no v1, Office só pelo LibreOffice isolado | 019 |
| SEC-25, SEC-29 | Um Worker por arquivo, sempre. A saída só é finalizada depois que ele termina | 019 |
| SEC-58, PRIV-03 | Nenhum download além da atualização. O Pizza detecta o LibreOffice instalado e, se não houver, mostra o link oficial | 019 |
| §9 | Políticas do v1: POL-02, 03, 04, 05, 06, 12 e 15. A POL-12 fica sem efeito enquanto não houver downloads | 019 |
| §10.1 | Saem `showHint` e `accentColor` | 019 |
| SEC-10, 11, 12, 14, 16, TST-06, TST-27 | Removidos: o canal com o Worker usa pipes sem nome, entregues só a ele. SEC-13 e SEC-15 continuam valendo | 021 |
| SEC-57 | Confirmado: o MSI só avisa que há versão nova | — |
| Escolha por clique (detalhes) | O botão direito numa fatia também escolhe: abre o painel de opções e, em formato sem opções, converte direto. No disco, fora das fatias, nenhum botão faz nada. Depois de 10 s sem interação (mouse, roda ou teclado), a escolha fecha; cada interação reinicia a contagem | 025 |
| FR-04 e FR-20 (Fase 4b) | Acima do disco, uma faixa mostra os níveis da fatia em hover, em todas as abas (exemplo: "Nível de compressão: Impressão, Escritório, Tela"). Soltar ou clicar usa o nível padrão, e o painel completo abre no botão direito. O desenho final, mesclando faixa e painel, sai num ADR da Fase 4b | — |
| Barra de abas (Fase 4d) | Fica mais fina: entre 55% e 70% da altura atual | — |
| Temas do vidro (Fase 8) | Na tela de Configuração, paletas opcionais em degradê pastel, no estilo do fundo das pranchas de referência do Dossiê | — |
| Sobre (Fase 8) | A seção Sobre da Configuração mostra a frase do projeto: "Afinal, tudo acaba em pizza." | — |
| NFR-01 | A meta de 20 MB vale para a **memória privada** do residente, a coluna Memória do Gerenciador de Tarefas. O conjunto de trabalho total também conta DLLs do Windows compartilhadas com o Explorer (shell32, windows.storage, combase), exigidas pelo arrastar e soltar em qualquer linguagem. Medição de 10/11/2026, em repouso: 7,8 MB privados e 24 MB de conjunto de trabalho. A Fase 5 mede também com o menu aberto | — |

---

## 3. Inventário de dados (LGPD)

O Pizza é uma **ferramenta** que roda no computador do cliente. Quem decide converter um documento é o usuário, ou a organização dele (cartório, escritório), que é o **controlador** dos dados pessoais desses documentos. O desenvolvedor do Pizza **não recebe nenhum dado** e por isso não atua como controlador nem operador desses tratamentos (LGPD art. 5º, VI e VII). A tabela abaixo é a base do registro de operações do cliente (Apêndice B).

| Dado | Onde fica | Quem acessa | Retenção | Sai do PC? |
| --- | --- | --- | --- | --- |
| Conteúdo do arquivo de origem | Onde o usuário guardou | Worker, via handle, só durante a conversão | Não é copiado | Não |
| Arquivo convertido | Ao lado do original (ou pasta fixa) | Usuário | Do usuário | Não |
| Arquivo temporário | Oculto, na pasta de destino | Residente e Worker | Até o fim da conversão; apagado em falha | Não |
| Metadados pessoais (GPS, autor, empresa) | Dentro do arquivo | Worker | Removidos da saída por padrão | Não |
| Senha de PDF | Memória RAM | Residente e Worker | Zerada após o uso | Não |
| Log de diagnóstico | `%LOCALAPPDATA%\Pizza\logs` | Só o usuário do Windows | 30 dias | Só se o usuário exportar |
| Mapa de IDs (ID → caminho completo) | `%LOCALAPPDATA%\Pizza\idmap.dat`, criptografado (DPAPI) | Só o usuário do Windows | 30 dias por entrada | Só se o usuário incluir na exportação |
| Configurações | `%LOCALAPPDATA%\Pizza\settings.json` | Usuário | Até desinstalar ou limpar | Não |
| Consulta de atualização | Requisição HTTPS ao GitHub | GitHub (vê o IP, como qualquer site) | Política do GitHub | Sim: só a versão, nenhum dado de arquivo |
| Download de pacote opcional | Requisição HTTPS à URL fixa do manifesto | Servidor oficial do pacote (vê o IP) | — | Sim: só se o usuário pedir; nenhum dado de arquivo |
| Dump de falha | Não é gerado (PRIV-05) | — | — | Não |

**O Pizza NÃO coleta:** e-mail, nome, CPF, ID da máquina, conteúdo de arquivos, histórico de uso, teclas digitadas, posição do mouse, área de transferência, títulos de janelas.

---

## 4. Modelo de ameaças

### 4.1 Ativos

| Ativo | Por que importa |
| --- | --- |
| Documentos do usuário | Dados pessoais e sigilosos (certidões, contratos, receitas) |
| Integridade do original | Perder ou alterar o original é inaceitável |
| Execução no PC | O app roda sempre, com hook de mouse; se for tomado, vira um ponto de entrada |
| Canal de atualização | Quem controla a atualização controla todos os PCs |
| Reputação do projeto | Um incidente mata a confiança de cartórios |

### 4.2 Fronteiras de confiança

```
 [Internet] ──HTTPS──▶ (Atualizador) ──verifica assinatura──▶ [Instalação]
                                                                 │
 [Explorer] ──OLE drag──▶ [Residente: médio, só mouse] ──pipe ACL──▶ [Worker: AppContainer, sem rede]
                                │                                        │
                                └──── handles duplicados (origem/destino) ┘
 [Arquivo não confiável] ─────────────────────────────────────▶ só o Worker interpreta
```

### 4.3 Ameaças (STRIDE) e mitigações

| ID | Categoria | Ameaça | Mitigação | Requisitos |
| --- | --- | --- | --- | --- |
| T-01 | Elevação | PDF/imagem malicioso explora falha em libvips, PDFium ou PDFsharp | Worker em AppContainer sem rede e sem acesso a arquivos; limites de recursos; dependências atualizadas | SEC-20–28, SEC-40–44, §11 |
| T-02 | Adulteração | Atualização falsa (MITM, conta do GitHub invadida) | HTTPS + assinatura Ed25519 do manifesto + Authenticode do publicador + anti-rollback | SEC-50–55 |
| T-03 | Adulteração | Instalador adulterado distribuído por terceiros | Só builds do CI, assinados; SHA-256 publicado; proveniência atestada | §11 |
| T-04 | Divulgação | Log ou mapa de IDs vaza nomes de clientes | IDs no log; mapa criptografado com DPAPI; ACL só do usuário; 30 dias | PRIV-10–17 |
| T-05 | Divulgação | Arquivo convertido carrega GPS, autor ou empresa | Remoção de metadados por padrão | PRIV-30–32 |
| T-06 | Divulgação | PDF protegido vira saída sem senha sem ninguém perceber | Mantém a proteção ou exige confirmação | SEC-74, SEC-75 |
| T-07 | Divulgação | Senha de PDF fica em log, disco ou memória | Só em buffer zerado após o uso; nunca serializada no log | SEC-70–73 |
| T-08 | Falsificação | Outro processo se conecta ao pipe e manda conversões falsas | Pipe com ACL, nome aleatório por sessão, instância única, PID do cliente verificado | SEC-10–15 |
| T-09 | Adulteração | Mensagem malformada no pipe derruba ou confunde o residente | JSON estrito, tamanho máximo, esquema validado, campos desconhecidos rejeitados | SEC-13, §10.3 |
| T-10 | Negação de serviço | Arquivo-bomba (descompressão, pixels, páginas) trava o PC | Limites de pixels, páginas, tamanho, tempo e RAM | SEC-40–44 |
| T-11 | Adulteração | Arraste com Ctrl + falha do app move ou apaga o original | O drop target nunca devolve MOVE; origem aberta só para leitura | SEC-05, SEC-31 |
| T-12 | Adulteração | Junção/symlink faz a saída sobrescrever outro arquivo | Criação com `CREATE_NEW`; validação do caminho final por handle; rename sem substituir | SEC-32, SEC-33 |
| T-13 | Divulgação | O app vira keylogger, ou é acusado de ser um | Sem hook de teclado; hook de mouse não registra nada | SEC-01–04 |
| T-14 | Elevação | Macro em DOCM/XLSM roda durante a conversão | Abertura só leitura; macros desativadas à força; perfil isolado | SEC-60–64 |
| T-15 | Elevação | DLL plantada na pasta do app (DLL hijacking) | Busca de DLL restrita; bibliotecas por caminho absoluto | SEC-80 |
| T-16 | Adulteração | Conversão "lava" um arquivo vindo da internet | Saída herda a marca MotW (ZoneId) | SEC-35 |
| T-17 | Repúdio | Ninguém sabe o que deu errado | Log local com IDs e códigos de erro | PRIV-10 |
| T-18 | Divulgação | DOCX da internet aberto no Word busca modelo remoto e vaza o hash NTLM | Arquivo de internet só no LibreOffice isolado, sem rede | SEC-65, SEC-66 |
| T-19 | Elevação | Instalador verificado é trocado antes do UAC na instalação por máquina | MSI não se autoatualiza | SEC-57 |
| T-20 | Adulteração / Divulgação | Worker tomado por um arquivo altera ou lê os próximos documentos | Worker de uso único para arquivos de internet; finalização só após o handle fechado | SEC-29 |
| T-21 | Divulgação | Dump de falha (WER) leva conteúdo do documento para fora do PC | Pizza excluído do WER; sem dumps | PRIV-05 |
| T-22 | Divulgação | Nome do arquivo na tela bloqueada, no histórico ou no celular | Toasts adiados com a sessão bloqueada, sem espelhamento, removidos do histórico | PRIV-18 |
| T-23 | Adulteração | PDF de saída repassa JavaScript ou anexos da origem | Reescrita completa sem conteúdo ativo | SEC-76 |
| T-24 | Negação de serviço | Manifesto antigo e válido "congela" o app numa versão vulnerável | Manifesto com validade e canal assinados | SEC-56 |

**Riscos residuais aceitos**

| Risco | Por que aceitamos |
| --- | --- |
| Malware que já roda como o usuário pode trocar os binários da instalação por usuário | Tem o mesmo privilégio do usuário de qualquer forma. A TI que quiser mais deve usar o MSI por máquina (Program Files) |
| Conversão do Office pelo Word roda fora do AppContainer | O Word não roda em AppContainer. Só arquivos locais vão para o Word (SEC-65), com SEC-60–66 |
| O GitHub vê o IP na consulta de atualização | É inerente a qualquer download. Pode ser desligado por política (POL-05) |

---

## 5. Requisitos funcionais (FR)

### 5.1 Gesto e menu

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| FR-01 | O menu DEVE abrir quando o usuário arrasta um ou mais arquivos do Explorer ou da área de trabalho segurando Ctrl | Ctrl + arrastar `a.jpg` abre o menu em ≤ 100 ms (NFR-03) |
| FR-02 | Arrastar sem Ctrl NÃO DEVE abrir o menu nem interferir no arraste nativo | Arraste comum move/copia como sempre |
| FR-03 | O menu DEVE abrir na aba do tipo do primeiro arquivo | JPG → aba Imagem; PDF → aba PDF; DOCX → aba Documento |
| FR-04 | O scroll durante o arraste DEVE trocar a aba | 1 entalhe de scroll = 1 aba; traço e barra atualizam |
| FR-05 | A fatia sob o cursor DEVE ficar em destaque (WF-2) | Detecção por ângulo e raio, não pela caixa |
| FR-06 | O formato de origem NÃO DEVE aparecer como fatia | Um JPG não mostra a fatia "JPG" |
| FR-07 | Soltar fora do disco, ou Esc durante o arraste, DEVE cancelar sem efeito | Nenhum arquivo criado; volta ao Repouso |
| FR-08 | O menu DEVE caber inteiro na tela | Perto da borda do monitor, o disco desloca para dentro |

### 5.2 Painel e conversão

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| FR-20 | Soltar numa fatia com opções DEVE abrir o painel (WF-3) já com o padrão marcado | Enter converte sem nenhuma outra escolha |
| FR-21 | Conversões sem opções DEVEM começar ao soltar | TIFF, GIF, Juntar e Word não abrem painel |
| FR-22 | "Usar sempre estas opções" DEVE pular o painel nas próximas vezes | Desfazível em Configurações › Qualidade |
| FR-23 | O painel DEVE ter a opção "Manter metadados", desmarcada por padrão | Ver PRIV-30 |
| FR-24 | PDF com senha DEVE mostrar um campo de senha no painel | Ver SEC-70 |
| FR-25 | Origem protegida + destino sem suporte a senha DEVE pedir confirmação explícita | Ver SEC-75 |
| FR-26 | A saída DEVE ir para a mesma pasta, com o mesmo nome e a nova extensão | `a.jpg` → `a.png` |
| FR-27 | Se o nome existir, DEVE usar `a (1).png`, `a (2).png`…; NUNCA sobrescrever | Teste com 3 conflitos seguidos |
| FR-28 | Vários arquivos arrastados juntos DEVEM ser convertidos em lote, com as mesmas opções | 10 JPG → 10 PNG, um toast só |
| FR-29 | Cancelar no toast de progresso DEVE parar o job e apagar o temporário | Nenhum resto na pasta |

### 5.3 Conversões do MVP e das versões seguintes

| Versão | Conversões |
| --- | --- |
| v0.1 | Imagem: JPG, PNG, WEBP, AVIF, TIFF, ICO, GIF, Imagem → PDF |
| v0.2 | PDF: Comprimir, Juntar, Dividir, Imagens, Word, Girar |
| v0.3 | Documento: DOCX/XLSX/PPTX → PDF |
| v1.x | Áudio e vídeo (FFmpeg LGPL) e aba Web |

Os níveis de qualidade de cada conversão estão na Parte 7 do `PIZZA-PROJETO.md`.

### 5.4 Notificações, bandeja e configurações

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| FR-40 | O toast de progresso DEVE aparecer só em conversões acima de 1 s | Imagem pequena não mostra progresso |
| FR-41 | O toast de concluído DEVE oferecer "Abrir" e "Mostrar na pasta" | WF-4 |
| FR-42 | O toast de erro DEVE dizer o motivo em português claro, sem expor caminho completo | Mapa da §10.4 |
| FR-50 | A bandeja DEVE oferecer Pausar (1 h), Configurações, Abrir pasta de logs, Exportar diagnóstico, Limpar dados do Pizza, Sobre e Sair | WF-5 |
| FR-51 | Pausar DEVE remover o hook de mouse enquanto durar | Com o app pausado, o hook não aparece instalado |
| FR-60 | Configurações DEVEM gravar em `settings.json` e respeitar políticas (§9) | Campo travado por política aparece desabilitado com cadeado |
| FR-61 | A tela "Privacidade" DEVE explicar, em linguagem simples, o que o Pizza faz e não faz com dados | Texto do Apêndice A |

### 5.5 Instalação

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| FR-70 | DEVE haver instalador por usuário (sem admin) em `%LOCALAPPDATA%\Programs\Pizza` | Instala numa conta comum |
| FR-71 | DEVE haver MSI por máquina em `%ProgramFiles%\Pizza`, para GPO e Intune | `msiexec /i Pizza.msi /qn` funciona |
| FR-72 | A desinstalação DEVE remover binários, logs, mapa de IDs e temporários; configurações só se o usuário marcar | Nada sobra em `%LOCALAPPDATA%\Pizza` além do que foi escolhido |
| FR-73 | O início com o Windows DEVE usar a chave `Run` do usuário (ou da máquina no MSI) | Sem serviço e sem tarefa com privilégio elevado |

---

## 6. Requisitos de segurança (SEC)

### 6.1 Entrada: mouse, sem teclado

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| SEC-01 | O app NÃO DEVE instalar hook de teclado (`WH_KEYBOARD_LL` ou similar) | Inspeção do código e teste com ferramenta de listagem de hooks |
| SEC-02 | O estado do Ctrl DEVE ser lido só no início do arraste (`GetAsyncKeyState(VK_CONTROL)`) | Nenhuma outra leitura de teclado no código |
| SEC-03 | O Esc DEVE ser tratado pelo próprio arraste OLE (cancelamento chega como `DragLeave`) | Sem captura global de teclado |
| SEC-04 | O callback do hook de mouse DEVE retornar em < 1 ms, sem I/O nem log de coordenadas | Medição em teste; revisão de código |
| SEC-05 | O `IDropTarget` do menu DEVE devolver só `DROPEFFECT_COPY` ou `DROPEFFECT_NONE`, NUNCA `DROPEFFECT_MOVE` | Teste automatizado: o original continua no lugar em 100% dos drops |
| SEC-06 | O menu DEVE aceitar só dados `CF_HDROP` (arquivos); texto, URL e outros formatos são ignorados | Arrastar texto não abre o menu |

### 6.2 Comunicação entre processos (IPC)

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| SEC-10 | O pipe DEVE ter nome aleatório por sessão (`\\.\pipe\Pizza-<SID>-<128 bits aleatórios>`), passado ao Worker na linha de comando | Nome muda a cada início |
| SEC-11 | O pipe DEVE ter ACL só para o SID do usuário e o SID do AppContainer do Worker | Outro usuário recebe "acesso negado" |
| SEC-12 | O pipe DEVE usar `FILE_FLAG_FIRST_PIPE_INSTANCE` e `PIPE_REJECT_REMOTE_CLIENTS` | Tentativa de conexão pela rede falha |
| SEC-13 | Cada mensagem DEVE ter no máximo 64 KB, ser JSON válido segundo o esquema da §10.3, e campos desconhecidos DEVEM ser rejeitados | Fuzzing do parser (TST-04) sem crash |
| SEC-14 | O residente DEVE verificar que o cliente do pipe é o PID do Worker que ele mesmo iniciou (`GetNamedPipeClientProcessId`) | Outro processo é desconectado |
| SEC-15 | Caminhos de arquivo NÃO DEVEM trafegar no pipe; o Worker recebe só handles duplicados e o `converterId` | Inspeção das mensagens |
| SEC-16 | O perfil AppContainer DEVE ser por usuário (`Pizza.Worker.<hash do SID>`), e a ACL do pipe DEVE usar ACE condicional que exige o SID do usuário **e** o do AppContainer ao mesmo tempo | Em servidor RDP, o Worker da sessão B não abre o pipe da sessão A (TST-27) |

### 6.3 Worker isolado (sandbox)

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| SEC-20 | O Worker de imagens e PDF DEVE rodar em **AppContainer sem capabilities** (sem rede, sem acesso a arquivos do usuário, registro ou outras janelas) | Teste: o Worker não consegue abrir `C:\Users\<u>\Documents` nem resolver DNS |
| SEC-21 | O Worker DEVE rodar dentro de um Job Object com: memória máxima de 1,5 GB, 1 processo ativo (sem filhos), `KILL_ON_JOB_CLOSE` | Tentativa de criar processo filho falha |
| SEC-22 | O residente DEVE abrir a origem (só leitura) e criar o temporário de destino, e passar os handles ao Worker via `DuplicateHandle` | O Worker nunca chama `CreateFile` com caminho de usuário |
| SEC-23 | O Worker DEVE ser encerrado se passar do tempo limite (SEC-43) ou se o residente fechar | Job Object fecha junto |
| SEC-24 | A falha do Worker NÃO DEVE derrubar o residente; o job vai para Erro | Teste matando o Worker no meio |
| SEC-25 | O Worker DEVE fechar após 30–60 s sem trabalho | Medição |
| SEC-26 | O Worker DEVE carregar só as bibliotecas da sua família de conversão | Lista de módulos carregados |
| SEC-27 | O Worker NÃO DEVE gravar log com conteúdo nem nome de arquivo; só `jobId`, código e tempo | Revisão de código |
| SEC-28 | Conversões do Office DEVEM rodar num processo separado (OfficeWorker), nunca no residente (§6.6) | — |
| SEC-29 | Arquivo com `ZoneId ≥ 3` DEVE rodar num Worker de uso único, encerrado ao fim do arquivo. Arquivos locais podem usar o Worker aquecido. O residente só finaliza (rename) depois que o Worker fechou o handle de destino; se não conseguir confirmar, encerra o Worker antes | Worker que escreve após `done` não altera a saída final (TST-19) |

### 6.4 Sistema de arquivos

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| SEC-30 | O temporário DEVE ser criado oculto, na pasta de destino, com nome `.~pizza-<aleatório>.tmp` | Visível só com "mostrar ocultos" |
| SEC-31 | A origem DEVE ser aberta com `GENERIC_READ` e compartilhamento só de leitura; o Pizza NUNCA escreve na origem | Hash da origem igual antes e depois (TST-10) |
| SEC-32 | O temporário DEVE ser criado com `CREATE_NEW`, sem seguir reparse points para fora da pasta | Junção apontando para outra pasta é recusada |
| SEC-33 | A finalização DEVE renomear por handle (`SetFileInformationByHandle`, `FileRenameInfo`) **sem** substituir destino existente | Corrida com arquivo criado no meio: Pizza escolhe o próximo nome |
| SEC-34 | Em falha, cancelamento ou queda, o temporário DEVE ser apagado; temporários órfãos DEVEM ser limpos no início do app conforme SEC-37 | Nenhum resto após teste de queda |
| SEC-35 | Se a origem tiver `Zone.Identifier` (MotW), a saída DEVE receber `ZoneId` igual; `HostUrl` e `ReferrerUrl` NÃO DEVEM ser copiados (privacidade) | Arquivo baixado → convertido também abre em Modo Protegido |
| SEC-36 | Caminhos de rede (UNC, unidades mapeadas) DEVEM funcionar com as mesmas regras; caminhos longos (> 260) DEVEM funcionar | Testes dedicados |
| SEC-37 | A limpeza de órfãos DEVE usar um diário local (volume + File ID, protegido por DPAPI) dos temporários criados pelo Pizza. Abre por File ID, sem seguir reparse point, confere que o dono é o usuário atual e só então apaga. NUNCA apaga por padrão de nome | Arquivo `.~pizza-x.tmp` de outra conta não é apagado (TST-26) |

### 6.5 Limites contra arquivos-bomba

Valores padrão, ajustáveis por política (POL-10).

| ID | Limite | Padrão |
| --- | --- | --- |
| SEC-40 | Tamanho máximo do arquivo de origem | 2 GB |
| SEC-41 | Imagem: pixels decodificados | 100 megapixels (libvips com `access=sequential` e limite de pixels) |
| SEC-42 | PDF: número de páginas | 2.000 |
| SEC-43 | Tempo por arquivo | 2 minutos |
| SEC-44 | RAM do Worker | 1,5 GB (Job Object) |

Arquivo acima do limite DEVE gerar o erro `LIMIT_EXCEEDED`, com a explicação no toast. Nada de travar e nada de converter pela metade.

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| SEC-45 | O libvips DEVE rodar com o bloqueio de operações não confiáveis ligado e com uma lista explícita de decodificadores: JPEG, PNG, WEBP, TIFF, GIF, HEIF/AVIF. O formato real, detectado pelo conteúdo, DEVE estar na lista e corresponder à aba | `.jpg` que é SVG é recusado com `UNSUPPORTED` (TST-25) |
| SEC-46 | DEVERIA limitar a 500 arquivos por arraste, com fila de no máximo 2 jobs por núcleo | Arraste de 600 arquivos mostra aviso |

### 6.6 Office (Word / LibreOffice)

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| SEC-60 | Documentos DEVEM ser abertos em modo só leitura | O Word não grava nada na origem |
| SEC-61 | Word: `AutomationSecurity = msoAutomationSecurityForceDisable` antes de abrir, links externos não atualizados, suplementos não carregados | Documento com macro `AutoOpen` não executa (TST-12) |
| SEC-62 | LibreOffice: perfil isolado e descartável (`-env:UserInstallation=`), segurança de macro "Muito alta", sem execução de macros | Mesmo teste do SEC-61 |
| SEC-63 | Conteúdo ativo (OLE embutido, DDE, campos que buscam dados externos) NÃO DEVE ser atualizado nem executado | Documento com DDE não dispara nada |
| SEC-64 | O processo do Word ou LibreOffice DEVE ser encerrado ao fim de cada lote | Sem `WINWORD.EXE` órfão |
| SEC-65 | Arquivo com `ZoneId ≥ 3` NUNCA DEVE ir para o Word. Converte só no LibreOffice isolado (AppContainer sem capability de rede, recursos remotos desligados). Sem LibreOffice, mostra o erro `OFFICE_UNTRUSTED_SOURCE` | DOCX com modelo remoto e `ZoneId=3` não gera conexão de saída (TST-16) |
| SEC-66 | Para arquivos locais no Word: modelos anexados e vínculos remotos DEVEM ser removidos numa cópia em memória antes de abrir; instância dedicada, `AddToRecentFiles=false` (ver V-01) | O documento não aparece em Recentes |

### 6.7 PDF protegido por senha

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| SEC-70 | A senha DEVE ser digitada num campo mascarado do painel, nunca lembrada nem sugerida | "Usar sempre" não guarda senha |
| SEC-71 | A senha DEVE ficar só em `byte[]` fixado na memória e ser zerada logo após o uso (residente e Worker) | Dump de memória após o job não contém a senha |
| SEC-72 | A senha NÃO DEVE aparecer em log, exceção, toast ou mensagem de erro | Busca nos logs |
| SEC-73 | Após 5 senhas erradas seguidas para o mesmo arquivo, o painel DEVE esperar 30 s | Evita automação de tentativa |
| SEC-74 | Comprimir, Girar, Dividir e Juntar DEVEM manter a proteção na saída (mesma senha, AES-256; nunca RC4) | A saída pede a mesma senha |
| SEC-75 | Se o destino não suporta senha (JPG, PNG, DOCX), o painel DEVE avisar "a saída ficará sem proteção" e exigir confirmação explícita | Sem confirmação, nada é convertido |
| SEC-76 | PDFs de saída DEVEM ser reescritos por inteiro (sem salvamento incremental) e sem JavaScript, `OpenAction`, `AA`, `Launch`, `SubmitForm`, `EmbeddedFiles` e `RichMedia`. Se a origem tinha algo disso, o painel avisa. "Manter conteúdo ativo" é opcional e bloqueável (POL-14) | PDF com JS e anexo gera saída sem esses objetos (TST-22) |

### 6.8 Atualização

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| SEC-50 | A consulta DEVE ser no máximo 1 vez por dia, por HTTPS (TLS 1.2+), validando o certificado pelo Windows | Teste com proxy MITM: falha e não instala |
| SEC-51 | A requisição NÃO DEVE conter identificador do usuário ou da máquina; User-Agent só `Pizza/<versão>` | Captura de tráfego |
| SEC-52 | O manifesto da versão DEVE ser assinado com Ed25519; a chave pública fica embutida no app | Manifesto alterado é rejeitado |
| SEC-53 | O pacote baixado DEVE ter SHA-256 igual ao do manifesto e assinatura Authenticode do publicador esperado | Pacote trocado é rejeitado |
| SEC-54 | NÃO DEVE aceitar versão menor ou igual à instalada (anti-rollback) | Manifesto antigo é ignorado |
| SEC-55 | A instalação só DEVE acontecer depois que o usuário clicar em "Atualizar"; política pode desligar a consulta (POL-05) | Nada instala sozinho |
| SEC-56 | O manifesto DEVE trazer `expires` (até 14 dias), `product`, `channel` e `platform`, cobertos pela assinatura. Manifesto vencido gera o aviso "não foi possível confirmar se o Pizza está atualizado". O app aceita a chave atual e a próxima; chaves revogadas vêm listadas no manifesto | Manifesto vencido avisa; de outro canal, é recusado (TST-23) |
| SEC-57 | A instalação por máquina (MSI) NUNCA DEVE se autoatualizar: só avisa e indica a TI. A autoatualização vale só para a instalação por usuário, que não pede elevação | No MSI, nada é baixado nem executado (TST-17) |
| SEC-58 | Pacotes opcionais (LibreOffice, FFmpeg) só DEVEM ser baixados pela mesma cadeia da atualização: URL fixa no manifesto assinado + SHA-256 + publicador Authenticode esperado. Sem as três verificações, nada é executado. Desligável por POL-12 | Pacote com 1 byte alterado é recusado (TST-18) |

### 6.9 Endurecimento do binário

| ID | Requisito |
| --- | --- |
| SEC-80 | `SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32 \| LOAD_LIBRARY_SEARCH_APPLICATION_DIR)` no início; bibliotecas nativas carregadas por caminho absoluto |
| SEC-81 | ASLR, DEP e CFG ativos em todos os executáveis e DLLs nativas próprias |
| SEC-82 | Nenhum segredo no código ou no repositório (varredura de segredos do GitHub ativa) |
| SEC-83 | O app NUNCA pede elevação (UAC) depois de instalado |
| SEC-84 | O buffer da senha DEVERIA usar `VirtualLock` para não ir para o arquivo de paginação |

---

## 7. Requisitos de privacidade e LGPD (PRIV)

### 7.1 Princípios aplicados

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| PRIV-01 | O app NÃO DEVE ter conta, licença, ativação nem cadastro | Nenhuma tela pede dado pessoal |
| PRIV-02 | O app NÃO DEVE ter telemetria, analytics, relatório de falhas automático nem SDK de terceiros que fale com a rede | Captura de tráfego por 24 h de uso: só a consulta de atualização |
| PRIV-03 | As únicas conexões de rede permitidas são a consulta de atualização (SEC-50) e o download de pacote opcional pedido pelo usuário (SEC-58), sem nenhum dado do usuário | Idem |
| PRIV-04 | A tela de Privacidade e o README DEVEM descrever exatamente os dados da §3 | Revisão a cada release |
| PRIV-05 | Os executáveis do Pizza DEVEM ser excluídos do Relatório de Erros do Windows (`WerAddExcludedApplication`), e o Worker NÃO DEVE gerar dump. Falha vira só o código `WORKER_CRASHED` no log local | Worker forçado a travar: nenhum `.dmp` nem relatório na fila (TST-20) |

### 7.2 Logs e mapa de IDs

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| PRIV-10 | O log DEVE registrar: data/hora, nível, evento, `jobId`, `fileId`, extensão, conversão, tempo, código de erro | Formato da §10.2 |
| PRIV-11 | `fileId` = os 12 primeiros caracteres hex de HMAC-SHA256(chave local, caminho normalizado). Mesmo arquivo = mesmo ID; impossível reverter sem a chave | Teste de estabilidade e de colisão |
| PRIV-12 | A chave do HMAC DEVE ter 32 bytes aleatórios, gerados na instalação e protegidos por DPAPI (`CurrentUser`) | Copiar o arquivo para outro usuário não abre |
| PRIV-13 | Os nomes reais DEVEM ficar só no `idmap.dat`, criptografado por DPAPI (`CurrentUser`), com `lastSeen` por entrada | Abrir o arquivo em editor mostra só bytes cifrados |
| PRIV-14 | Logs e mapa DEVEM apagar entradas com mais de 30 dias, no início do app e uma vez por dia | Teste com relógio adiantado |
| PRIV-15 | Pasta `%LOCALAPPDATA%\Pizza` com ACL só para o usuário e SYSTEM (sem herança de grupos) | Outro usuário não lê |
| PRIV-16 | O log NUNCA DEVE conter: conteúdo de arquivo, senha, texto de metadados, coordenadas do mouse, área de transferência, títulos de janela | Varredura automatizada dos logs nos testes |
| PRIV-17 | No toast e na tela, o erro mostra o nome do arquivo; o log mostra só o `fileId` | Comparação lado a lado |
| PRIV-18 | Com a sessão bloqueada, os toasts DEVEM esperar o desbloqueio. Todo toast usa `NotificationMirroring = Disabled`, expira em 1 h e é removido do histórico ao ser fechado. POL-13 pode trocar o nome por "1 arquivo convertido" | Conversão com a tela bloqueada não mostra toast até o desbloqueio (TST-21) |

### 7.3 Exportação de diagnóstico e direitos do titular

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| PRIV-20 | "Exportar diagnóstico" DEVE gerar um `.zip` com logs (só IDs), versões e configurações sem caminhos | Inspeção do zip |
| PRIV-21 | A caixa "Incluir nomes dos arquivos" DEVE vir desmarcada; marcada, inclui só os nomes dos IDs presentes nos logs exportados | Prévia mostra o que vai no zip antes de salvar |
| PRIV-22 | O zip é salvo onde o usuário escolher; o Pizza não envia nada | Sem rede |
| PRIV-23 | "Limpar dados do Pizza" DEVE apagar logs, mapa de IDs, chave HMAC (gera nova) e opções lembradas | Pasta volta ao estado de instalação nova |
| PRIV-24 | Exportação com nomes DEVERIA oferecer senha opcional no zip | Zip pede senha quando marcada |

### 7.4 Metadados

| ID | Requisito | Critério de aceite |
| --- | --- | --- |
| PRIV-30 | Por padrão, a saída NÃO DEVE conter: GPS e dados de câmera (EXIF), autor, empresa, última pessoa que salvou, comentários e revisões ocultas, XMP com dados pessoais | Ferramenta de leitura de metadados na saída não mostra esses campos |
| PRIV-31 | DEVEM ser preservados os metadados técnicos necessários: orientação, perfil de cor, DPI, título do documento | A imagem não gira nem muda de cor |
| PRIV-32 | "Manter metadados" no painel DEVE preservar tudo o que o formato de destino suporta | Teste comparando origem e saída |

---

## 8. Requisitos não funcionais (NFR)

| ID | Requisito | Meta |
| --- | --- | --- |
| NFR-01 | RAM do residente em repouso | < 20 MB |
| NFR-02 | CPU do residente em repouso | 0% (sem polling) |
| NFR-03 | Tempo do gesto até o menu aparecer | ≤ 100 ms |
| NFR-04 | Imagem comum (≤ 12 MP) convertida | < 1 s |
| NFR-05 | PDF Juntar/Dividir/Girar | < 1 s para até 100 páginas |
| NFR-06 | Acessibilidade | Painel e configurações 100% por teclado; contraste ≥ 4,5:1; "Reduzir animações" respeitado |
| NFR-07 | Idiomas | Português do Brasil no v1; textos em arquivo de recursos para tradução futura |
| NFR-08 | Compatibilidade | Escala de 100% a 200%; vários monitores; tema claro e escuro |

---

## 9. Políticas para a TI (GPO / Intune)

Chaves em `HKLM\SOFTWARE\Policies\Pizza` (máquina) ou `HKCU\SOFTWARE\Policies\Pizza` (usuário). A política da máquina vence a do usuário, e as duas vencem o `settings.json`. O MSI traz modelos ADMX/ADML.

| ID | Valor | Tipo | Padrão | Efeito |
| --- | --- | --- | --- | --- |
| POL-01 | `GestureKey` | string | `Ctrl` | Tecla do gesto |
| POL-02 | `DisabledConverters` | multi-string | vazio | Esconde fatias (ex.: `pdf.word`) |
| POL-03 | `ForceStripMetadata` | DWORD | 0 | 1 = sempre remove metadados, sem opção de manter |
| POL-04 | `OutputMode` | string | `besideOriginal` | Ou `fixedFolder` com `OutputFolder` |
| POL-05 | `UpdateCheck` | DWORD | 1 | 0 = nunca consulta a internet |
| POL-06 | `LogRetentionDays` | DWORD | 30 | De 0 (sem log) a 30; nunca maior |
| POL-07 | `AllowExportFileNames` | DWORD | 1 | 0 = exportação nunca inclui nomes |
| POL-08 | `OfficeEngine` | string | `auto` | `word`, `libreoffice` ou `disabled` |
| POL-09 | `AllowPasswordPdf` | DWORD | 1 | 0 = recusa PDF com senha |
| POL-10 | `Limits` | string JSON | padrões da §6.5 | Ajusta os limites |
| POL-11 | `AllowUnprotectedOutput` | DWORD | 1 | 0 = proíbe sair sem senha a partir de PDF protegido |
| POL-12 | `AllowOptionalDownloads` | DWORD | 1 | 0 = nunca baixa LibreOffice ou FFmpeg; a TI instala |
| POL-13 | `ToastShowFileName` | DWORD | 1 | 0 = toast mostra "1 arquivo convertido", sem o nome |
| POL-14 | `AllowKeepActiveContent` | DWORD | 1 | 0 = PDF de saída sempre sem conteúdo ativo |
| POL-15 | `EventLog` | DWORD | 0 | 1 = grava eventos (só IDs e códigos) no Log de Eventos do Windows, canal `Pizza/Operational`, para coleta pelo Wazuh |

---

## 10. Formatos e contratos

### 10.1 `settings.json`

```json
{
  "schema": 1,
  "gestureKey": "Ctrl",
  "startWithWindows": true,
  "theme": "system",
  "accentColor": "#2F6BFF",
  "showHint": true,
  "output": { "mode": "besideOriginal", "fixedFolder": null },
  "keepMetadataDefault": false,
  "workerIdleSeconds": 45,
  "remembered": {
    "image.png": { "resolution": "original", "compression": "balanced" }
  }
}
```

Regras: o arquivo é lido com esquema validado; um valor inválido volta ao padrão e é registrado no log; caminhos (`fixedFolder`) nunca vão para o log.

### 10.2 Linha de log (JSON Lines)

```json
{"ts":"2026-11-08T19:20:11.482-04:00","lvl":"info","evt":"job.done","job":"7f3a9c","file":"a91be03c44d2","ext":"jpg","conv":"image.png","ms":182}
{"ts":"2026-11-08T19:21:02.003-04:00","lvl":"warn","evt":"job.error","job":"7f3aa1","file":"0c5e81f2b7aa","ext":"pdf","conv":"pdf.compress","ms":2310,"err":"PASSWORD"}
```

### 10.3 Mensagens do pipe

Todas têm `v` (versão do protocolo), `type` e `jobId` (12 hex aleatórios). Tamanho máximo de 64 KB. Campos desconhecidos são rejeitados.

| type | Direção | Campos | Validação |
| --- | --- | --- | --- |
| `warmup` | App → Worker | `category` | `image` \| `pdf` |
| `convert` | App → Worker | `converterId`, `srcHandle`, `dstHandle`, `options`, `password?` | `converterId` na lista do registro; `options` validadas pelo `OptionSchema`; handles são números de handles já duplicados |
| `cancel` | App → Worker | — | `jobId` existente |
| `progress` | Worker → App | `percent` | 0–100 |
| `done` | Worker → App | `elapsedMs` | — |
| `error` | Worker → App | `code` | Um dos códigos da §10.4 |

### 10.4 Códigos de erro

| Código | Mensagem ao usuário |
| --- | --- |
| `CORRUPTED` | O arquivo está corrompido ou não é do tipo esperado |
| `LOCKED` | O arquivo está aberto em outro programa |
| `NO_SPACE` | Não há espaço em disco suficiente |
| `NO_PERMISSION` | Sem permissão para gravar nesta pasta |
| `PASSWORD` | O PDF está protegido e a senha não foi informada ou está errada |
| `LIMIT_EXCEEDED` | O arquivo passa do limite permitido (tamanho, páginas ou pixels) |
| `TIMEOUT` | A conversão demorou mais que o limite |
| `UNSUPPORTED` | Este tipo de conversão não é suportado para este arquivo |
| `WORKER_CRASHED` | O conversor parou inesperadamente; o original não foi alterado |
| `OFFICE_UNAVAILABLE` | Word ou LibreOffice não encontrado |
| `OFFICE_UNTRUSTED_SOURCE` | Este documento veio da internet e só pode ser convertido pelo LibreOffice isolado, que não está instalado |

---

## 11. Cadeia de build e release

| Item | Regra |
| --- | --- |
| Origem do instalador oficial | **Só** o GitHub Actions, a partir de uma tag assinada na `main` |
| Builds locais | Só para desenvolvimento; nunca distribuídos |
| Actions | Fixadas por SHA de commit; `GITHUB_TOKEN` com permissão mínima |
| Proteção da `main` | Pull request obrigatório, CI verde, revisão, commits assinados, 2FA em todas as contas com escrita |
| Análise estática | CodeQL (C#) a cada PR |
| Dependências | Dependabot + OSV-Scanner; falha crítica conhecida bloqueia o release |
| SBOM | CycloneDX gerado a cada release e publicado junto |
| Proveniência | Atestado de build (`actions/attest-build-provenance`) |
| Assinatura | SignPath Foundation (Authenticode) no instalador, MSI e executáveis |
| Hashes | `SHA256SUMS` publicado e assinado (Ed25519, mesma chave do manifesto) |
| Chave Ed25519 do manifesto | Fora do CI comum: guardada offline ou em HSM/cofre; uso registrado |
| Licenças | GPL-3.0 no repositório; `THIRD-PARTY-NOTICES.md` gerado do SBOM |
| Dependências NuGet | `packages.lock.json` com `RestoreLockedMode=true`; Package Source Mapping só com nuget.org e prefixos esperados; `trustedSigners` para os pacotes nativos |
| Workflows de contribuição | Proibido `pull_request_target` com checkout do código do PR; PRs de fork rodam sem segredos e sem permissão de escrita |
| Job de assinatura | Só em tag, num *environment* protegido com aprovação manual, consumindo o artefato pelo **digest** produzido no build (nunca por nome) |
| Rotação da chave Ed25519 | Chave atual + próxima embutidas no app; troca anunciada num manifesto assinado pelas duas; chave comprometida entra na lista de revogadas |

---

## 12. Plano de testes de segurança

| ID | Teste | Ferramenta / método | Quando |
| --- | --- | --- | --- |
| TST-01 | Unitários da máquina de estados e das invariantes | xUnit | Cada PR |
| TST-02 | Conversores com amostras em `tests/samples` | xUnit | Cada PR |
| TST-03 | Fuzzing dos conversores (imagem, PDF) | SharpFuzz + corpus de arquivos malformados | Semanal e antes de release |
| TST-04 | Fuzzing do parser de mensagens do pipe | SharpFuzz | Semanal |
| TST-05 | Sandbox: o Worker não acessa rede, arquivos nem cria processos | Teste de integração com tentativas reais | Cada release |
| TST-06 | IPC: cliente estranho é recusado | Teste de integração | Cada release |
| TST-07 | Atualização: MITM, manifesto adulterado, rollback | Proxy de teste + manifestos falsos | Cada release |
| TST-08 | Arquivos-bomba (pixels, descompressão, páginas) | Corpus dedicado | Cada release |
| TST-09 | Varredura dos logs por dados proibidos (PRIV-16) | Script no CI | Cada PR |
| TST-10 | Integridade do original: hash antes e depois de cada teste | Em todos os testes de conversão | Cada PR |
| TST-11 | Junções, symlinks e caminhos longos | Teste de integração | Cada release |
| TST-12 | Office com macro `AutoOpen`, DDE e links externos | Documentos de teste | Cada release |
| TST-13 | Tráfego de rede por 24 h de uso | Captura de pacotes | Antes de cada versão maior |
| TST-14 | Metadados removidos na saída | ExifTool como verificador | Cada PR |
| TST-15 | Revisão externa de segurança | Voluntários da comunidade ou consultoria | Antes da v1.0 |
| TST-16 | DOCX com modelo remoto e `ZoneId=3`: nenhuma conexão de saída | Captura local | Cada release |
| TST-17 | MSI não baixa nem executa atualização | Teste de integração | Cada release |
| TST-18 | Pacote opcional adulterado é recusado | Fixture local | Cada release |
| TST-19 | Worker que escreve após `done` não altera a saída final | Worker de teste | Cada release |
| TST-20 | Worker forçado a travar: sem `.dmp` e sem relatório WER | Teste de integração | Cada release |
| TST-21 | Toast adiado com a sessão bloqueada | Teste manual guiado | Cada release |
| TST-22 | PDF de saída sem JS, ações e anexos | Verificador de estrutura PDF | Cada PR |
| TST-23 | Manifesto vencido avisa; de outro canal é recusado | Manifestos de teste | Cada release |
| TST-24 | PR que muda dependência sem atualizar o lockfile falha | CI | Cada PR |
| TST-25 | Decodificador fora da lista é recusado | Corpus de formatos | Cada PR |
| TST-26 | Órfão de outra conta não é apagado | Teste com duas contas | Cada release |
| TST-27 | Pipe inalcançável entre sessões RDP | Servidor de teste com 2 sessões | Antes de cada versão maior |
| TST-28 | **Auditoria completa** com a skill `security-audit` (modo completo, perfil `standard`, verificação independente) | Repositório do Pizza | Fase 5 e antes da v1.0 |

**Gate de release:** todos os testes acima verdes, nenhuma vulnerabilidade crítica ou alta aberta, SBOM e assinaturas publicados.

---

## 13. Gestão de vulnerabilidades e incidentes

| Item | Regra |
| --- | --- |
| Canal | Private Vulnerability Reporting do GitHub; `SECURITY.md` explica como reportar |
| Primeira resposta | Até 72 h |
| Correção: crítica | Até 7 dias |
| Correção: explorada ativamente | Até 72 h |
| Correção: alta | Até 30 dias |
| Correção: média ou baixa | Próxima versão |
| Divulgação | Aviso de segurança (GitHub Security Advisory + CVE quando couber) depois da correção publicada, com crédito a quem reportou |
| Versões suportadas | Só a última versão menor recebe correções |
| Incidente no cliente | O incidente com dados é do controlador (o cliente). O projeto publica o aviso técnico e orientações para ajudar o cliente a decidir sobre comunicar à ANPD e aos titulares (LGPD art. 48) |

Os prazos de correção ficam abaixo dos exigidos de cartórios pelo Provimento CNJ nº 213/2026: 30 dias para vulnerabilidade crítica e 72 h quando há exploração ativa.

---

## 14. Conformidade: LGPD e CNJ

### 14.1 LGPD (Lei nº 13.709/2018)

| Dispositivo | Como o Pizza atende |
| --- | --- |
| Art. 6º, III (necessidade) | Só o mínimo: nada de conta, telemetria ou coleta (PRIV-01–03) |
| Art. 6º, VI (transparência) | Tela de Privacidade, README e Apêndice A (PRIV-04, FR-61) |
| Art. 6º, VII e VIII (segurança e prevenção) | §6 inteira e modelo de ameaças da §4 |
| Art. 6º, X (responsabilização) | Specs, testes e SBOM públicos, porque o código é aberto |
| Art. 18, VI (eliminação) | "Limpar dados do Pizza" e retenção de 30 dias (PRIV-14, PRIV-23) |
| Art. 37 (registro das operações) | Modelo pronto para o cliente (Apêndice B) |
| Art. 46 (medidas de segurança, desde a concepção) | Privacidade por padrão: metadados removidos, IDs no log, sandbox |
| Art. 48 (comunicação de incidente) | Processo de aviso técnico para apoiar o cliente (§13) |

### 14.2 Cartórios: Provimento CNJ nº 213/2026 (padrões mínimos de TIC)

| Exigência do provimento | Como o Pizza ajuda |
| --- | --- |
| Software livre é admitido se cumprir as normas de segurança | Código GPL-3.0 auditável; esta especificação e o SBOM documentam os controles |
| Vulnerabilidades críticas tratadas em até 30 dias (72 h com exploração ativa) | Nossos prazos são menores (§13) |
| Atualização periódica com rotina documentada | Notas de versão e atualização assinada; a TI pode controlar pelo MSI e pelo POL-05 |
| Nada em fim de suporte | Política de versões suportadas (§13) |
| Logs e trilha de auditoria | O log do Pizza é de diagnóstico e não substitui a trilha de auditoria dos atos da serventia, que continua no sistema principal |

O Provimento nº 134/2022 (LGPD nas serventias) continua como base das obrigações de privacidade dos cartórios. O kit dos Apêndices facilita o registro do Pizza no inventário de tratamento da serventia.

---

## 15. Rastreabilidade: requisito → fase → teste

| Requisitos | Fase | Testes |
| --- | --- | --- |
| SEC-01–06, FR-01–08 | F1 · O gesto | TST-01, revisão de hooks |
| SEC-10–16, SEC-20–29, SEC-30–37, FR-26–27 | F2 · Primeira conversão | TST-04–06, TST-10, TST-11, TST-19, TST-26, TST-27 |
| FR-20–29, FR-40–42, PRIV-10–18, PRIV-30–32, SEC-40–46 | F3 · Motor de conversores | TST-02, TST-03, TST-08, TST-09, TST-14, TST-21, TST-25 |
| NFR-06, NFR-08 | F4 · Visual | Checklist manual |
| NFR-01–05, PRIV-02–05 | F5 · Medir e endurecer | Benchmarks, TST-13, TST-20, **TST-28** |
| SEC-70–76 | F6 · Aba PDF | TST-02, TST-03, TST-22 |
| SEC-28, SEC-58, SEC-60–66 | F7 · Aba Documento | TST-12, TST-16, TST-18 |
| FR-50–73, SEC-50–57, SEC-80–84, PRIV-20–24, POL-*, §11, §13 | F8 · Produto | TST-07, TST-15, TST-17, TST-23, TST-24, TST-28 |

---

## 16. Itens em aberto

| Item | Proposta | Decide |
| --- | --- | --- |
| Integração com Wazuh | Proposta como POL-15 (desligada por padrão); confirmar se entra no v1 | Gui |
| Mudanças de experiência vindas da auditoria | Arquivo da internet nunca abre no Word (SEC-65); MSI sem autoatualização (SEC-57); toasts esperam o desbloqueio (PRIV-18) | Gui |
| Itens a validar no código | V-01 a V-04 da `AUDITORIA-SEGURANCA.md` | Equipe, na Fase 5 |
| Propostas de simplicidade | S-01 a S-05 da `AUDITORIA-SEGURANCA.md` §7 (só LibreOffice, um Worker por arquivo, 6 políticas no v1, cortar extras de UI, sem downloads opcionais) | Gui |
| Doações | Página de patrocínio no GitHub para sustentar o projeto | Gui |
| Nome final | "Pizza" é o nome de trabalho | Gui |
| Revisão externa (TST-15) | Quem faz e quando | Gui |

---

## Apêndice A — Kit LGPD: nota técnica de privacidade

> **Pizza: nota técnica de privacidade e segurança** (para anexar ao inventário de dados do cliente)
>
> O Pizza é um utilitário para Windows que converte arquivos no próprio computador. Ele **não envia** arquivos, nomes de arquivos, dados pessoais nem estatísticas de uso para a internet. A única conexão é a consulta opcional de novas versões ao GitHub, que não carrega nenhum dado do usuário e pode ser desligada pela TI.
>
> **O que o Pizza trata:** o conteúdo dos arquivos que o usuário escolhe converter, somente durante a conversão, dentro de um processo isolado e sem acesso à rede.
>
> **O que o Pizza guarda:** um registro técnico local por até 30 dias, no qual os arquivos aparecem como códigos. Os nomes reais ficam num arquivo criptografado que só a conta do Windows do usuário consegue abrir. Esses dados podem ser apagados a qualquer momento em "Limpar dados do Pizza".
>
> **Privacidade por padrão:** dados de localização (GPS), autor e empresa são removidos dos arquivos convertidos, salvo escolha contrária.
>
> **Papéis na LGPD:** o cliente que usa o Pizza é o controlador dos dados dos documentos que converte. O projeto Pizza não recebe esses dados e, portanto, não é operador.
>
> **Transparência:** código aberto (GPL-3.0), especificação de segurança, lista de componentes (SBOM) e canal de reporte de vulnerabilidades públicos.

## Apêndice B — Kit LGPD: modelo de registro da operação

Para o controlador preencher no seu registro de operações (LGPD art. 37):

| Campo | Preenchimento sugerido |
| --- | --- |
| Operação | Conversão de formato de documentos e imagens |
| Ferramenta | Pizza, versão ___, instalada localmente |
| Finalidade | Adequar o formato de documentos aos procedimentos internos (ex.: PDF para arquivamento, compressão para envio) |
| Base legal | A mesma do tratamento original do documento (ex.: cumprimento de obrigação legal ou regulatória, art. 7º, II; execução de contrato, art. 7º, V) |
| Categorias de titulares | As dos documentos convertidos (clientes, partes, colaboradores) |
| Dados pessoais | Os contidos nos documentos; possivelmente dados sensíveis, conforme o documento |
| Compartilhamento | Nenhum; processamento local |
| Transferência internacional | Nenhuma |
| Retenção na ferramenta | Arquivo temporário até o fim da conversão; registro técnico por até 30 dias |
| Medidas de segurança | Processo isolado sem rede; remoção de metadados; registro com IDs criptografados; atualização assinada (ver SPECS §6 e §7) |
| Responsável interno | ___ |

## Apêndice C — Kit LGPD: respostas para o encarregado (DPO)

| Pergunta | Resposta |
| --- | --- |
| O Pizza envia algum dado para fora? | Não. Só consulta se há versão nova e, se o usuário pedir, baixa o LibreOffice verificado. Nenhum dado do usuário vai junto, e a TI pode desligar as duas coisas. Relatórios de falha do Windows ficam desligados para o Pizza. |
| Onde ficam cópias dos documentos? | Em nenhum lugar além do arquivo convertido. O temporário é apagado ao fim da conversão ou em falha. |
| Quem consegue ler o registro técnico? | Só a conta do Windows do usuário. Os nomes dos arquivos ficam criptografados. |
| Por quanto tempo o registro fica? | Até 30 dias, ou menos por política da TI; pode ser apagado a qualquer momento. |
| E se um documento tiver macro ou vírus? | O documento é aberto num processo isolado, sem rede e sem acesso a outros arquivos; macros são bloqueadas à força. |
| Como sabemos que o instalador é legítimo? | Ele é assinado digitalmente e gerado só pelo processo público de build; o hash é publicado. |
| Em caso de falha de segurança no Pizza? | O projeto publica um aviso e a correção nos prazos da §13, com orientação técnica para a avaliação do incidente pelo controlador. |
| Podemos auditar? | Sim. O código, esta especificação e a lista de componentes são públicos. |

---

### Fontes consultadas

- [Lei nº 13.709/2018 (LGPD)](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709.htm)
- [Provimento CNJ nº 213/2026: padrões mínimos de TIC para serventias](https://sintse.tse.jus.br/documentos/2026/Fev/23/diario-da-justica-eletronico-cnj/provimento-no-213-de-20-de-fevereiro-de-2026-dispoe-sobre-os-padroes-minimos-de-tecnologia-da)
- [Provimento CNJ nº 134/2022: LGPD nas serventias extrajudiciais (IRIB)](https://www.irib.org.br/noticias/detalhes/provimento-cn-cnj-n-134-de-24-de-agosto-de-2022)
