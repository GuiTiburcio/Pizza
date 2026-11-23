# Proposta de workspace, edições e trabalho paralelo

- Estado: proposta v2 conciliada com o Claude. Migração física pendente.
- Base observada: main 7ac527c e [PR #108 de organização documental](https://github.com/GuiTiburcio/Pizza/pull/108).
- Decisão preservada: Brotinho e Jumbo são edições da mesma base (ADR-034), não produtos com cópias independentes do código.

## 1. Pasta matriz local

```text
Pizza/
├── LEIA-ME.md
├── Pizza.code-workspace
├── 1-main/                         checkout de integração na main
├── 2-ramos/                        um worktree completo por tarefa
│   ├── base/<tarefa>/              funções compartilhadas pelas edições
│   ├── jumbo/<tarefa>/             funções exclusivas da edição Jumbo
│   ├── projeto/<tarefa>/           documentação, governança e ferramentas
│   └── revisao/<tarefa>/           revisão e validação independente
├── 3-validacao/                    local; fora do repositório
│   └── <sha-do-candidato>/
│       ├── brotinho/
│       │   ├── app/                pacote CI extraído
│       │   ├── amostras/           entradas sintéticas
│       │   └── evidencias/         resultados e hashes
│       └── jumbo/
│           ├── app/                inclui motor validado
│           ├── amostras/
│           └── evidencias/
├── 4-distribuicao/                 somente artefatos oficiais conferidos
│   └── <versao>/<canal>/<edicao>/
├── .claude/                        configuração local do orquestrador
├── .vscode/                        configuração local de navegação
├── .coordenacao/                   registro local de trabalho paralelo
└── GEMINI.md                       entrada de instruções da ferramenta
```

O diretório `1-main` contém o checkout de integração e o repositório Git. Use-o para atualizar a main e preparar validações e entregas integradas. Cada tarefa em `2-ramos/<faixa>/<tarefa>` tem seu índice e diretório de trabalho próprios. As faixas orientam a localização, e os novos ramos usam o padrão `<faixa>/<tarefa>`. Os ramos em andamento mantêm seus nomes durante a transição.

Os diretórios 3-validacao e 4-distribuicao não são outros repositórios nem outra fonte de código. Candidatos são identificados pelo SHA e pelo artefato real; ter uma pasta de edição não significa que a CI já produz esse pacote. Hoje o workflow de prévia observado não implementa toda a composição Jumbo descrita no ADR-034. A separação dos pacotes exige uma tarefa própria.

Não criar cópias permanentes do produto chamadas main, Brotinho e Jumbo. Não adotar branches permanentes por edição ou por agente. O mesmo commit pode originar ambos os pacotes, com conteúdo, SBOM, hashes e validação distintos.

## 2. Repositório de produto

```text
1-main/
├── src/                           implementação compartilhada
│   ├── Pizza.App/
│   ├── Pizza.Core/
│   ├── Pizza.Worker/
│   ├── Pizza.Converters/
│   └── Pizza.Docs/                 processo isolado do motor Jumbo
├── tests/                         testes existentes por responsabilidade
├── packaging/                     composição versionada, ainda a implementar
│   ├── README.md
│   ├── comum/
│   ├── brotinho/
│   └── jumbo/
├── tools/
├── docs/                          árvore documental definida no PR #108
├── .github/                       automação e templates
├── .agents/                       instruções e skills do projeto
├── .claude/                       integrações específicas do Claude
├── README.md
├── LICENSE
├── THIRD-PARTY-NOTICES.md
├── SECURITY.md
├── CONTRIBUTING.md
├── AGENTS.md
├── CLAUDE.md
├── GEMINI.md
└── Pizza.slnx
```

Manter src e os projetos atuais durante a reorganização documental. Não criar novos projetos .NET só para refletir a árvore. Packaging contém regras/manifestos/scripts de composição, não DLLs, instaladores nem o motor baixado. O motor é obtido e verificado no fluxo de build apropriado, e os binários gerados continuam fora do Git.

SECURITY e CONTRIBUTING podem ficar na raiz ou em localização reconhecida pelo GitHub. A preferência aqui é mantê-los visíveis na raiz, evitando migração sem benefício operacional. Se o ramo docs-estrutura os mover, conferir descoberta no GitHub, todos os links e referências de ferramentas antes de integrar; não manter duas políticas divergentes.

Os sete grupos documentais são navegação, não uma ordem rígida de execução. Segurança, operação, acessibilidade e capacidade de recuperação participam desde os requisitos e são verificadas em cada mudança.

## 3. Edições e dependências

| Elemento | Compartilhado | Brotinho | Jumbo |
| --- | --- | --- | --- |
| Residente, contratos e Worker | Sim | Incluídos | Incluídos |
| Conversores compatíveis com o pacote leve | Sim | Incluídos conforme catálogo | Incluídos conforme catálogo |
| Pizza.Docs e motor de documentos | Código no mesmo repositório | Não incluir no pacote leve | Incluir quando composição e isolamento forem validados |
| Documentação e requisitos comuns | Sim | Referenciados | Referenciados |
| Manifesto de conteúdo, SBOM, hashes e matriz de aceite | Regras comuns reutilizáveis | Evidência própria | Evidência própria |

O fluxo de execução App → IPC → Worker → conversores não é uma cadeia de referências de compilação. Hoje App referencia Core; Worker referencia Core e Converters; Converters e Docs referenciam Core. O residente inicia processos pelos contratos; Core não precisa referenciar o executável Worker. Reorganizar pastas não pode enfraquecer SEC-15/20/22/26/31 nem alterar silenciosamente o catálogo de edições.

## 4. Árvore de trabalho e coordenação

```text
main
├── docs/estrutura-360             organização documental existente
├── docs/validacao-autonoma-...    protocolo de validação
├── fix/alac-apple-101             correção em revisão no PR #109
├── fix/docs-worker-crash          frente existente, causa ainda a demonstrar
└── <faixa>/<tarefa>               padrão para novas tarefas
```

Esta árvore representa origem e integração do trabalho, não subdiretórios Git nem garantia de ancestralidade atual dos ramos. Cada tarefa parte de uma base registrada e volta por PR. Dependências entre tarefas são explícitas; uma tarefa dependente pode aguardar o PR anterior ou usar uma pilha curta de ramos com base identificada. Não manter um ramo develop ou integração permanente sem necessidade demonstrada.

Cada worktree possui TAREFA.md e RESULTADO.md locais, conforme a skill existente. O registro durável de escopo e evidências fica no PR/issue e na documentação versionada; retirar um worktree não pode apagar a única evidência da entrega. Uma frente tem um responsável por escrita. Revisão independente ocorre sem edição simultânea na mesma cópia. O workspace do VS Code deve nomear as pastas explicitamente: Produto/main, Claude/tarefa, Codex/validação, Antigravity/tarefa. Não adicionar automaticamente todas as pastas de worktree, caches e binários à busca do editor.

Worktrees isolam arquivos Git, não recursos do Windows. AppContainer, perfis, diário, mutexes, portas, letras temporárias e dados de teste também precisam de isolamento. Até isso estar comprovado, testes de integração que compartilham esses recursos são executados em série. Nunca executar duas cópias residentes sobre os mesmos arquivos sintéticos sem um cenário deliberado e registrado.

Worktree bloqueado por escopo adiado é preservado. Worktree ativo não é movido, rebaseado ou limpo por outro agente. Após merge, verificar alterações não commitadas, commits exclusivos, arquivos ignorados úteis, processos em uso e retenção das evidências antes da remoção pelo Git. Não usar exclusão recursiva para administrar worktrees.

## 5. Migração sem interromper o trabalho atual

1. Conciliar esta proposta com docs/estrutura-360; não refazer os movimentos já preparados. Manter um mapa único de caminho antigo → novo, cobrindo documentos, skills, workflows, templates, pranchas e scripts.
2. Conferir links e descoberta de instruções. O teste de links preparado nesse ramo deve examinar documentos de instruções e skills também. Corrigir referências relativas movidas e verificar clones limpos.
3. Preparar LEIA-ME da matriz e arquivos de workspace como mapas. Eles apontam para contratos versionados; não criam outro backlog nem outra SPECS fora do repositório.
4. Continuar o trabalho nos caminhos atuais enquanto houver sessões ativas. Neste levantamento, os ramos ALAC e Docs mudaram de HEAD durante a leitura: há trabalho em andamento.
5. Em uma janela coordenada, fechar processos dependentes e registrar caminhos, HEADs, locks e alterações locais. Mover worktrees vinculados com `git worktree move` quando elegíveis. Para renomear o checkout principal, reparar os vínculos com `git worktree repair` a partir do novo local e conferir cada destino registrado. Preservar worktrees bloqueados e preparar reversão antes de mover.
   Antes da migração, atualizar `tools/gerenciar-worktrees.ps1` e os consumidores para `2-ramos/<faixa>/<tarefa>`. A raiz deve ser a pasta matriz do checkout principal, inclusive quando o script for chamado dentro de um worktree vinculado.
6. Atualizar a solução padrão do VS Code, caminhos de ferramentas, permissões locais que dependem de caminho e instruções da raiz. Hoje .vscode/settings.json aponta para MenuConversorPizza/Pizza.slnx e GEMINI.md aponta para MenuConversorPizza e worktrees: ambos precisam de atualização conjunta.
7. Verificar git worktree list, status/HEAD de cada frente e abertura da solução. Depois executar verificações de build/testes, um teste real por edição disponível e comparação das origens. Não atribuir testes de uma versão antiga ao novo candidato.
8. Só retirar compatibilidade de caminhos depois de confirmar ausência de referências antigas. Não usar symlinks/junctions para esconder referências quebradas.

## 6. Conclusão e critérios de aceite

Esta entrega prepara a proposta; não move diretórios nem altera projetos, pacotes ou worktrees de outros agentes. A migração estará concluída quando o mapa da raiz refletir o disco, os vínculos Git e caminhos do editor funcionarem, não houver contratos ambíguos, os testes pertinentes passarem e a validação de cada edição disponível estiver registrada. O acompanhamento do Codex ocorre nas tarefas desta conversa e em frentes próprias; não implica um processo permanente observando o VS Code entre mensagens.

Referências: [Git worktree](https://git-scm.com/docs/git-worktree), [documentos comunitários do GitHub](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/creating-a-default-community-health-file), [ADR-034](../adr/034-brotinho-jumbo-motor-de-documentos.md).
