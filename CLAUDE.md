# CLAUDE.md — Pizza

Regras para qualquer pessoa ou agente que escreva código neste repositório. Elas juntam as regras da skill [karpathy-guidelines](https://github.com/multica-ai/andrej-karpathy-skills) (MIT) com as regras de segurança do Pizza.

**Fonte da verdade:** `docs/SPECS.md`. Se o código e as SPECS discordam, pare e pergunte. Não "resolva" sozinho.

**Troca assumida:** estas regras preferem cautela a velocidade. Para tarefas triviais, use bom senso.

---

## 1. Pensar antes de codar

**Não assuma. Não esconda dúvida. Mostre as trocas.**

- Diga quais premissas está usando. Se tiver dúvida, pergunte.
- Se o pedido tem mais de uma leitura, apresente as opções em vez de escolher em silêncio.
- Se existe um jeito mais simples, diga. Discorde quando fizer sentido.
- Se algo está confuso, pare, diga o que está confuso e pergunte.
- **Pizza:** antes de mexer em entrada (hook, drop), IPC, Worker, sistema de arquivos, atualização ou log, diga qual requisito `SEC-`/`PRIV-` da SPECS a mudança toca.

## 2. Simplicidade primeiro

**O mínimo de código que resolve o problema. Nada especulativo.**

- Nenhuma funcionalidade além do que foi pedido.
- Nenhuma abstração para código usado uma vez só.
- Nenhuma "flexibilidade" ou configuração que ninguém pediu. Política nova de TI só com requisito `POL-` na SPECS.
- Nenhum tratamento de erro para cenário impossível.
- Se 200 linhas podem virar 50, reescreva.
- **Pizza:** cada dependência nova é superfície de ataque. Só com justificativa no PR e entrada no SBOM.

Pergunta de controle: "um engenheiro sênior acharia isto complicado demais?" Se sim, simplifique.

## 3. Mudanças cirúrgicas

**Mexa só no necessário. Limpe só a sua própria bagunça.**

- Não "melhore" código, comentários ou formatação vizinhos.
- Não refatore o que não está quebrado.
- Siga o estilo existente, mesmo que você faria diferente.
- Viu código morto que não é seu? Comente no PR, não apague.
- Remova imports, variáveis e funções que a **sua** mudança deixou sem uso.

Teste: cada linha alterada precisa ter ligação direta com o pedido.

## 4. Execução guiada por objetivo

**Defina o critério de sucesso. Repita até verificar.**

- "Adicionar validação" → escreva testes com entradas inválidas e faça passarem.
- "Corrigir bug" → escreva um teste que reproduz o bug e faça passar.
- "Refatorar X" → os testes passam antes e depois.
- **Pizza:** toda tarefa começa pelo teste `TST-` ligado ao requisito (SPECS §12 e §15). Sem teste, a tarefa não está pronta.

Para tarefas com várias etapas, escreva um plano curto:

```
1. [etapa] → verificar: [teste ou checagem]
2. [etapa] → verificar: [teste ou checagem]
```

---

## 5. Invariantes de segurança do Pizza (nunca quebrar)

1. Nenhum dado de usuário sai do PC. A única rede permitida é a consulta de atualização (SEC-50).
2. O arquivo original nunca é escrito, movido ou apagado. O drop nunca devolve `DROPEFFECT_MOVE` (SEC-05, SEC-31).
3. Sem hook de teclado (SEC-01).
4. Arquivo não confiável só é interpretado dentro do Worker em AppContainer, sem rede (SEC-20).
5. Caminhos de arquivo nunca trafegam no pipe; o Worker só recebe handles (SEC-15).
6. Log nunca contém nome de arquivo, conteúdo, senha, coordenadas ou área de transferência (PRIV-16).
7. Senhas só em buffer zerado após o uso; nunca em string, log ou exceção (SEC-70 a SEC-73).
8. Nenhum instalador sai de build local: só o CI publica (SPECS §11).

Mudança que enfraqueça qualquer item acima precisa de ADR em `docs/adr/` e aprovação do mantenedor **antes** do código.

---

**Sinais de que estas regras estão funcionando:** diffs menores e sem mudanças desnecessárias, menos reescritas por excesso de complexidade, e perguntas feitas antes da implementação em vez de depois do erro.
