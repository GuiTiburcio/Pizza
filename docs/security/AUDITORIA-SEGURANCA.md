# Pizza — Revisão de segurança do projeto (fase de especificação)

| Campo | Valor |
| --- | --- |
| Objeto revisado | `SPECS.md` v1.0, `PIZZA-PROJETO.md`, canvas "Pizza — Wireframes e Engenharia" (ENG-1 a ENG-4) |
| Método | Skill `security-audit` da Cloudflare ([cloudflare/security-audit-skill](https://github.com/cloudflare/security-audit-skill)), em **modo guia**, + lente de simplicidade da skill `karpathy-guidelines` ([multica-ai/andrej-karpathy-skills](https://github.com/multica-ai/andrej-karpathy-skills)) |
| Módulos aplicados | Desktop e IPC local · Cadeia de suprimentos e release · Dados e ciclo de vida · Exaustão de recursos · Segurança de memória e binários |
| Data | 08/11/2026 |
| Resultado | 12 lacunas de projeto (5 alta, 7 média) · 4 itens a validar no código · 5 endurecimentos · 7 pontos revisados sem lacuna |
| Efeito | Todas as lacunas foram corrigidas na `SPECS.md` v1.1 (novos requisitos indicados em cada item) |

---

## 1. Escopo e limites desta revisão

A skill da Cloudflare foi feita para auditar **código-fonte**: um achado só vira "confirmado" com evidência no código e um teste local reproduzindo o problema. O Pizza ainda **não tem código**. Por isso:

- Usamos a skill no **modo guia**, que ela própria prevê. Aplicamos os checklists de ataque de cada domínio às decisões de arquitetura e aos requisitos.
- **Nenhum item aqui é uma vulnerabilidade confirmada.** São **lacunas de projeto**: situações em que a especificação, se implementada ao pé da letra, permitiria atravessar uma fronteira de confiança.
- Seguindo a regra da skill de não dar severidade ao que não foi demonstrado, usamos **prioridade** (alta, média), não severidade.
- **Próxima auditoria obrigatória:** quando o repositório Pizza existir, rodar a skill no **modo auditoria completa** (perfil `standard`, com reconhecimento, ondas de caça, verificação independente e relatório validado por esquema). A Fase 5 do plano depende disso.

**Disciplina aplicada a cada item** (regra central da skill): quem é o atacante com menos privilégio · por onde ele entra · que controle deveria impedir · que fronteira é atravessada · qual o resultado concreto. Sugestão de boas práticas sem fronteira atravessada **não** conta como lacuna; vai para "Endurecimento".

---

## 2. Resumo

| # | Lacuna | Prioridade | Domínio | Correção (SPECS v1.1) |
| --- | --- | --- | --- | --- |
| L-01 | DOCX vindo da internet aberto pelo Word pode vazar a credencial NTLM e avisar o atacante | **Alta** | Desktop / Office | SEC-65, SEC-66 |
| L-02 | Atualização da instalação por máquina (MSI) vira elevação de privilégio | **Alta** | Supply chain / Desktop | SEC-57 |
| L-03 | Download do LibreOffice "sob demanda" é executável sem verificação | **Alta** | Supply chain | SEC-58 |
| L-04 | Worker reaproveitado: um arquivo malicioso contamina as conversões seguintes | **Alta** | Desktop / Dados | SEC-29 |
| L-05 | Dump de falha do Worker (WER) leva o conteúdo do documento para fora do PC | **Alta** | Dados / LGPD | PRIV-05 |
| L-06 | Nome do arquivo aparece na tela bloqueada, fica no histórico de notificações e é espelhado no celular | Média | Dados / LGPD | PRIV-18 |
| L-07 | "Comprimir" e "Juntar" preservam JavaScript, anexos e ações automáticas do PDF de origem | Média | Binário / Dados | SEC-76 |
| L-08 | Manifesto de atualização sem validade: o atacante pode "congelar" o app numa versão vulnerável | Média | Supply chain | SEC-56 |
| L-09 | Dependências NuGet sem lockfile e sem mapeamento de origem; assinatura sem ambiente protegido | Média | Supply chain | §11 (v1.1) |
| L-10 | libvips com todos os decodificadores ligados amplia a superfície de ataque | Média | Binário | SEC-45 |
| L-11 | Limpeza de temporários por padrão de nome pode apagar arquivos de outra pessoa numa pasta de rede | Média | Dados | SEC-37 |
| L-12 | Em servidor RDP, o Worker de um usuário pode alcançar o pipe de outro | Média | IPC local | SEC-16 |

---

## 3. Lacunas de projeto (detalhe)

### L-01 · DOCX da internet aberto pelo Word vaza NTLM — prioridade alta

| | |
| --- | --- |
| Atacante | Remoto: quem envia um DOCX por e-mail ou site |
| Entrada | DOCX com modelo anexado remoto (`attachedTemplate` apontando para `\\servidor-do-atacante\...`), imagem vinculada remota ou campo `INCLUDEPICTURE` |
| Controle esperado | SEC-63 bloqueia a atualização de campos e links, mas o **modelo remoto é buscado na abertura**. A automação COM do Word não usa o Modo Protegido |
| Fronteira | Rede local do cliente → servidor do atacante |
| Resultado | O Word, rodando como o usuário, conecta no servidor do atacante. O atacante captura o hash NTLMv2 do usuário (que pode ser quebrado ou reutilizado) e fica sabendo quando e por qual IP o documento foi aberto |
| Correção | **SEC-65:** arquivo com marca de internet (`ZoneId ≥ 3`) nunca vai para o Word. Converte só no LibreOffice isolado, sem rede (processo no AppContainer, sem capability de rede) e com recursos remotos desligados. Se o LibreOffice não estiver instalado, mostrar erro `OFFICE_UNTRUSTED_SOURCE`. **SEC-66:** para arquivos locais no Word, remover modelos e vínculos remotos antes de abrir, numa cópia em memória. |
| Teste | TST-16: DOCX com modelo `\\127.0.0.1\x` e `ZoneId=3`. Nenhuma conexão SMB ou HTTP de saída. |

### L-02 · Atualização da instalação por máquina vira elevação de privilégio — prioridade alta

| | |
| --- | --- |
| Atacante | Malware rodando como usuário comum (sem admin) |
| Entrada | O instalador baixado para uma pasta do usuário, entre a verificação da assinatura e o pedido de UAC |
| Controle esperado | SEC-52 e SEC-53 verificam o pacote, mas a especificação não diz **quem** verifica nem **onde** o arquivo fica até ser executado com privilégio |
| Fronteira | Usuário comum → administrador |
| Resultado | O malware troca o instalador já verificado. O usuário aprova o UAC achando que é o Pizza e roda o arquivo do atacante como admin |
| Correção | **SEC-57:** a instalação por máquina (MSI) **nunca** se autoatualiza. Ela só avisa que há versão nova, e a TI atualiza por Intune, GPO ou winget. A autoatualização fica restrita à instalação por usuário, que não pede elevação. |
| Teste | TST-17: no MSI, o atualizador não baixa nem executa nada. |

### L-03 · LibreOffice baixado "sob demanda" sem verificação — prioridade alta

| | |
| --- | --- |
| Atacante | Rede (MITM) ou quem comprometer o endereço de download |
| Entrada | Pacote opcional (LibreOffice ou FFmpeg) baixado na primeira conversão do Office, como previa o `PIZZA-PROJETO.md` |
| Controle esperado | PRIV-03 diz que a única conexão é a consulta de atualização, mas o projeto prevê um segundo download, e as SPECS não cobrem a verificação dele |
| Fronteira | Internet → execução no PC |
| Resultado | Executável adulterado roda como o usuário |
| Correção | **SEC-58:** pacotes opcionais só entram pela mesma cadeia da atualização: o manifesto Ed25519 do Pizza traz URL oficial fixa, SHA-256 do artefato e o publicador Authenticode esperado (The Document Foundation; FFmpeg em build próprio assinado pelo Pizza). Sem as três verificações, nada é executado. A TI pode desligar (POL-12) e instalar por conta própria. **PRIV-03** passa a citar os dois canais. |
| Teste | TST-18: pacote com 1 byte alterado é recusado. |

### L-04 · Worker reaproveitado contamina as próximas conversões — prioridade alta

| | |
| --- | --- |
| Atacante | Remoto: arquivo malicioso (PDF ou imagem) que explora uma falha no libvips ou no PDFium |
| Entrada | O primeiro arquivo de um lote, ou de uma sequência de conversões dentro dos 30–60 s em que o Worker fica vivo |
| Controle esperado | O AppContainer protege o **resto do PC**, mas não os **outros documentos** que passam pelo mesmo Worker depois |
| Fronteira | Documento A (não confiável) → documentos B, C… (de outros clientes) |
| Resultado | O Worker tomado recebe os handles de B e C. Ele pode alterar o conteúdo convertido deles, ou copiar dados de B para dentro da saída de A, que o usuário depois envia para fora. Além disso, um handle de escrita mantido aberto permite alterar a saída depois do "concluído" |
| Correção | **SEC-29:** (1) arquivo com marca de internet roda num **Worker de uso único**, encerrado ao fim do arquivo; (2) arquivos locais podem compartilhar o Worker aquecido; (3) o residente só renomeia a saída **depois** que o Worker fechou o handle de destino, e o confirma pela contagem de handles do objeto. Se não der para confirmar, encerra o Worker antes de finalizar. |
| Teste | TST-19: Worker de teste que tenta escrever depois do `done` não altera a saída final. |

### L-05 · Dump de falha leva o documento para fora do PC — prioridade alta

| | |
| --- | --- |
| Atacante | Nenhum ativo: é o próprio Windows. O risco é de vazamento e de promessa quebrada |
| Entrada | Arquivo que faz o Worker travar (justamente o caso malicioso) |
| Controle esperado | PRIV-02 promete que nada sai do PC, mas o Relatório de Erros do Windows (WER) pode gerar um minidump com trechos do documento em memória e enviá-lo à Microsoft, conforme a configuração do Windows. Se `LocalDumps` estiver configurado, ele também grava dumps em disco |
| Fronteira | PC do cliente → servidor da Microsoft (transferência internacional, LGPD art. 33) |
| Resultado | Conteúdo de documento pessoal sai do PC sem aviso, contrariando a nota técnica entregue ao DPO |
| Correção | **PRIV-05:** `WerAddExcludedApplication` para os executáveis do Pizza na instalação, `WerSetFlags(WER_FAULT_REPORTING_NO_UI)` no Worker, e o processo encerra sem dump. Falhas viram só o código `WORKER_CRASHED` no log local. |
| Teste | TST-20: Worker forçado a travar. Nenhum `.dmp` criado e nenhum relatório na fila do WER. |

### L-06 · Nome do arquivo na tela bloqueada, no histórico e no celular — prioridade média

| | |
| --- | --- |
| Atacante | Pessoa com acesso físico à tela (balcão de atendimento) ou ao celular vinculado |
| Entrada | Toast de conclusão com o nome do arquivo (PRIV-17) |
| Controle esperado | O log protege os nomes, mas o toast é uma cópia do dado com outro controle de acesso: aparece na tela bloqueada (conforme a configuração do Windows), fica no banco de notificações do Windows por tempo indefinido e pode ser espelhado no celular pelo Vincular ao Celular |
| Fronteira | Sessão do usuário → observador presencial ou celular |
| Resultado | Nome de cliente ou de documento exposto fora do controle e da retenção de 30 dias |
| Correção | **PRIV-18:** com a sessão bloqueada, os toasts ficam na fila e só aparecem depois do desbloqueio. Os toasts usam `NotificationMirroring = Disabled` e expiração de 1 h, e são removidos do histórico ao serem fechados. Por política (POL-13), o toast pode mostrar só "1 arquivo convertido", sem o nome. |
| Teste | TST-21: conversão com a tela bloqueada não gera toast até o desbloqueio. |

### L-07 · PDF de saída herda conteúdo ativo da origem — prioridade média

| | |
| --- | --- |
| Atacante | Remoto: PDF com JavaScript, ação de abertura (`OpenAction`), ação `Launch` ou anexo embutido |
| Entrada | Comprimir, Girar, Dividir ou Juntar copiam esses objetos para a saída |
| Controle esperado | O usuário (e quem recebe o arquivo) tende a confiar num arquivo "que já passou pelo Pizza". A marca de internet ajuda, mas não vale para arquivos que chegam por pen drive ou rede local |
| Fronteira | Remetente → quem recebe o PDF convertido |
| Resultado | O Pizza vira um "lavador" que repassa conteúdo ativo malicioso com cara de documento tratado |
| Correção | **SEC-76:** por padrão, a saída é reescrita por inteiro (sem salvamento incremental) e sem JavaScript, `OpenAction`, `AA`, `Launch`, `SubmitForm`, `EmbeddedFiles` e `RichMedia`. Se a origem tinha algo disso, o painel avisa. A opção "Manter conteúdo ativo" fica bloqueável por política (POL-14). |
| Teste | TST-22: PDF com JS e anexo. A saída não contém nenhum dos objetos listados. |

### L-08 · Manifesto de atualização sem validade (ataque de congelamento) — prioridade média

| | |
| --- | --- |
| Atacante | Rede ou espelho que entrega sempre um manifesto antigo, porém validamente assinado |
| Entrada | Consulta de atualização |
| Controle esperado | O anti-rollback (SEC-54) impede voltar de versão, mas não impede ficar parado: o app nunca fica sabendo da correção de segurança |
| Fronteira | Canal de atualização → integridade do app |
| Resultado | O cliente fica indefinidamente numa versão com falha conhecida |
| Correção | **SEC-56:** o manifesto traz `expires` (no máximo 14 dias), `product`, `channel` e `platform`, todos cobertos pela assinatura. Manifesto vencido gera um aviso visível ("não foi possível confirmar se o Pizza está atualizado"). As SPECS passam a ter um procedimento de **rotação e revogação** da chave Ed25519: o app aceita uma chave atual e uma próxima, e uma chave revogada vem listada no manifesto. |
| Teste | TST-23: manifesto vencido gera o aviso; manifesto de outro canal é recusado. |

### L-09 · Dependências e CI sem amarração — prioridade média

| | |
| --- | --- |
| Atacante | Quem publicar um pacote NuGet com nome parecido ou com versão maior num feed alternativo, ou um PR de fork |
| Entrada | Resolução de dependências e workflows do GitHub Actions |
| Controle esperado | §11 fixa actions por SHA, mas não exige lockfile do NuGet, nem mapeamento de origem dos pacotes, nem isolamento do job de assinatura |
| Fronteira | Contribuidor ou registro externo → binário assinado distribuído |
| Resultado | Código não revisado entra no instalador assinado pelo projeto |
| Correção | §11 v1.1: `packages.lock.json` com `RestoreLockedMode=true`; **Package Source Mapping** só com nuget.org e os prefixos esperados; `trustedSigners` para os pacotes nativos; **proibido** `pull_request_target` com checkout do código do PR; job de assinatura só em tag, num *environment* protegido com aprovação manual, consumindo o artefato pelo **digest** produzido no build. |
| Teste | TST-24: PR que altera dependência sem atualizar o lockfile falha no CI. |

### L-10 · libvips com decodificadores demais — prioridade média

| | |
| --- | --- |
| Atacante | Remoto: imagem num formato raro e pouco testado (SVG, matrizes científicas, formatos que passam por ImageMagick) |
| Entrada | Arquivo arrastado. A extensão mente, e o libvips detecta o formato pelo conteúdo |
| Controle esperado | O sandbox contém o dano, mas cada decodificador a mais é superfície de ataque sem nenhum ganho para o produto |
| Fronteira | Arquivo → processo do Worker (via L-04, alcança outros documentos) |
| Resultado | Mais chance de execução de código dentro do Worker |
| Correção | **SEC-45:** ligar o bloqueio de operações "não confiáveis" do libvips (`vips_block_untrusted` / equivalente no NetVips) e manter uma **lista explícita** dos decodificadores aceitos (JPEG, PNG, WEBP, TIFF, GIF, HEIF/AVIF). O formato real, detectado pelo conteúdo, precisa estar na lista **e** corresponder à aba escolhida. |
| Teste | TST-25: arquivo `.jpg` que na verdade é SVG ou outro formato fora da lista é recusado com `UNSUPPORTED`. |

### L-11 · Limpeza de temporários por nome em pasta compartilhada — prioridade média

| | |
| --- | --- |
| Atacante | Outro usuário com escrita na mesma pasta de rede, ou simplesmente outro colega usando o Pizza |
| Entrada | SEC-34 apaga `.~pizza-*` com mais de 1 h "nas pastas recentes" |
| Controle esperado | Apagar só o que o próprio Pizza criou |
| Fronteira | Usuário A → arquivos do usuário B na mesma pasta |
| Resultado | Apaga arquivos que não são seus: o temporário de um colega ou um arquivo plantado com nome parecido. Com link simbólico, o comportamento depende da API |
| Correção | **SEC-37:** o residente mantém um diário local dos temporários que criou (volume + File ID, protegido por DPAPI). A limpeza abre **por File ID**, sem seguir reparse point, confere que o dono é o usuário atual e só então apaga. Nunca apaga por padrão de nome. |
| Teste | TST-26: arquivo `.~pizza-x.tmp` criado por outra conta não é apagado. |

### L-12 · Pipe alcançável entre usuários em servidor RDP — prioridade média

| | |
| --- | --- |
| Atacante | Outro usuário do mesmo servidor RDP cujo Worker foi tomado por um arquivo malicioso |
| Entrada | Lista de pipes (qualquer usuário consegue listar `\\.\pipe\`) + ACE que libera o SID do AppContainer |
| Controle esperado | O SID de AppContainer vem do **nome do perfil** e é igual para todos os usuários do servidor. Se a ACL tiver uma ACE separada para esse SID, ela libera os Workers de **todos** os usuários. SEC-14 (PID verificado) evita o uso, mas sobra corrida e negação de serviço |
| Fronteira | Sessão do usuário B → pipe do usuário A |
| Resultado | Hoje mitigado pelo SEC-14; sem ele, haveria troca de mensagens entre sessões de usuários diferentes |
| Correção | **SEC-16:** nome do perfil AppContainer **por usuário** (`Pizza.Worker.<hash do SID>`) e ACE condicional que exige o SID do usuário **e** o do AppContainer ao mesmo tempo. SEC-14 permanece como segunda camada. |
| Teste | TST-27: em servidor com duas sessões, o Worker da sessão B não consegue abrir o pipe da sessão A. |

---

## 4. Itens a validar no código

Estes itens dependem de comportamento do Windows ou de bibliotecas que só se confirma testando. Não têm prioridade nem severidade, e viram tarefas da Fase 5.

| # | O que validar | Por quê | Como validar com segurança |
| --- | --- | --- | --- |
| V-01 | Se o Word por automação abre **instância própria** ou se anexa à do usuário | Se anexar, o `AutomationSecurity` muda a sessão do usuário e o documento aparece nos "Recentes" | Teste local com o Word aberto: comparar os PIDs e a lista de recentes (`AddToRecentFiles=false` é obrigatório nos dois casos) |
| V-02 | Se a senha digitada no campo do painel some da memória depois do uso | Controles de edição do Win32 guardam o texto num buffer próprio, e strings .NET não podem ser zeradas | Dump local do processo de teste com a senha fictícia "SENHA-TESTE-123" e busca no dump |
| V-03 | ACL da pasta de instalação por usuário permite o AppContainer **ler e executar** o Worker sem abrir escrita | Requisito para SEC-20 funcionar sem afrouxar a pasta | `icacls` após instalar: só leitura/execução para `ALL APPLICATION PACKAGES` ou para o SID específico |
| V-04 | Se a libvips no build do NetVips.Native escolhido respeita o bloqueio de decodificadores (SEC-45) | Depende da versão e dos módulos compilados | Teste com SVG renomeado para `.jpg` |

---

## 5. Endurecimentos (sem fronteira atravessada)

Boas práticas que não fecham uma brecha específica, mas reduzem o dano ou o esforço de suporte. Entraram nas SPECS como DEVERIA.

| # | Endurecimento | SPECS |
| --- | --- | --- |
| E-01 | Limite de 500 arquivos por arraste e fila com no máximo 2 jobs por núcleo | SEC-46 |
| E-02 | Buffer da senha com `VirtualLock` (não vai para o arquivo de paginação) | SEC-84 |
| E-03 | Recomendar à TI do cliente a GPO "Restringir NTLM: tráfego de saída" | Kit TI |
| E-04 | Zip do diagnóstico com nomes: oferecer senha opcional | PRIV-24 |
| E-05 | Política para enviar eventos ao Log do Windows (só IDs), para coleta pelo Wazuh | POL-15 |

---

## 6. Revisado sem lacuna

| Ponto | Por que está coberto |
| --- | --- |
| Arraste vindo de processo de integridade baixa (navegador em sandbox) | A UIPI do Windows bloqueia o arraste de processo de integridade menor para maior |
| Políticas em `HKCU\Software\Policies` | Por padrão, só administradores escrevem nessa chave |
| Ocupação antecipada do nome do pipe | `FILE_FLAG_FIRST_PIPE_INSTANCE` + PID verificado (SEC-12, SEC-14) |
| Saída redirecionada por junção ou link | `CREATE_NEW` sem seguir reparse + rename por handle (SEC-32, SEC-33) |
| "Lavagem" da marca de internet | SEC-35 copia o `ZoneId` |
| Hook de teclado como keylogger | Não existe (SEC-01) |
| Compatibilidade de licenças com GPL-3.0 | libvips (LGPL), PDFium (BSD), PDFsharp e NetVips (MIT) e FFmpeg (LGPL) são compatíveis |

---

## 7. Lente de simplicidade (skill `karpathy-guidelines`)

A skill [multica-ai/andrej-karpathy-skills](https://github.com/multica-ai/andrej-karpathy-skills) tem 4 regras: **pensar antes de codar** (explicitar premissas e trocas), **simplicidade primeiro** (nada especulativo), **mudanças cirúrgicas** e **execução guiada por objetivo** (critério verificável). Aplicada às specs, ela aponta onde o projeto ficou mais complexo do que precisa. Em segurança, complexidade é superfície de ataque, e cada caminho extra é um caminho a mais para errar.

Nada abaixo foi aplicado às SPECS. São **propostas que dependem da sua decisão**, porque mexem em escolhas já feitas.

| # | Onde está complexo | Proposta mais simples | O que se ganha | O que se perde |
| --- | --- | --- | --- | --- |
| S-01 | Dois motores de Office (Word por COM e LibreOffice), com regras diferentes por origem (SEC-61, SEC-65, SEC-66, V-01) | **Só LibreOffice isolado no v1**, para todos os arquivos | Some L-01 inteira, some V-01, um caminho só, roda no sandbox | DOCX complexo pode sair um pouco diferente do que sairia no Word |
| S-02 | Worker aquecido para arquivos locais + Worker de uso único para arquivos da internet (SEC-29) | **Um Worker por arquivo, sempre.** O próximo já nasce enquanto o menu está aberto, e a saída só é finalizada depois que o Worker termina | Uma regra só, sem verificar handles abertos, isolamento total entre documentos | Alguns milissegundos a mais por arquivo em lotes grandes (precisa medir) |
| S-03 | 15 políticas de TI (POL-01 a POL-15) | **v1 com as 6 essenciais:** POL-02, 03, 04, 05, 06 e 12. As outras entram quando um cliente pedir | Menos código, menos testes, menos combinações estranhas | A TI tem menos ajustes finos no começo |
| S-04 | Detalhes de interface que ninguém pediu: pulso de 120 ms, pausa de 1 h, dica desligável, cor de destaque configurável | **Cortar do v1** e manter só o que está no fluxo aprovado | Menos código de UI | Nenhum funcional |
| S-05 | Download de pacotes opcionais (SEC-58) | **Não baixar nada além da atualização:** o Pizza detecta o LibreOffice instalado e, se não houver, mostra o link oficial | Some um canal de rede inteiro e a L-03 | O usuário instala o LibreOffice uma vez por conta própria |

**Recomendação da equipe:** aceitar S-01, S-02, S-04 e S-05 para o v1, e S-03 parcialmente (manter também POL-15, que serve ao Wazuh). Juntas, essas mudanças eliminam 2 lacunas altas (L-01, L-03), 1 item a validar (V-01) e cerca de 10 requisitos, sem perder nenhuma proteção.

**Execução guiada por objetivo:** todos os requisitos das SPECS já têm critério de aceite e teste ligado (§15). O `CLAUDE.md` do repositório obriga cada tarefa a começar pelo teste que prova o requisito.

## 8. Próximos passos

1. Decidir as propostas de simplicidade S-01 a S-05 e revisar as 3 mudanças que afetam a experiência: arquivo da internet nunca abre no Word (L-01), MSI sem autoatualização (L-02) e toasts que esperam o desbloqueio (L-06).
2. Criar o repositório Pizza e incluir este relatório em `docs/security/`.
3. Na Fase 5, rodar a skill no **modo auditoria completa** sobre o código, com verificação independente de cada achado, e fechar os itens V-01 a V-04.
