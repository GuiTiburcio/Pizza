# Configuração, credenciais e limites de uso

## Pizza para Windows

O Brotinho e o Jumbo convertem arquivos no computador. O IPC é local e os arquivos não confiáveis passam pelo Worker ou pelo motor de documentos isolado. Esta edição opera sem cadastro, banco de contas ou API HTTP. Uma função que peça a senha de um PDF deve manter a senha em memória pelo tempo da operação, aplicar a proteção solicitada ao documento de saída e limpar os buffers após o uso. Senhas, conteúdo de arquivos e identificadores pessoais ficam fora dos logs. Valem SEC-15, SEC-20, SEC-70 a SEC-73 e PRIV-16 das [especificações](../1-produto/SPECS.md).

Para conversões de arquivos, controle o custo no ponto de admissão e na execução. Hoje, o residente serializa a execução, mas cria a tarefa antes de obter o acesso ao executor. A fila ainda precisa de um limite global de admissão, com recusa clara, cancelamento dos pendentes e verificação de novas conversões após a fila liberar espaço. Preserve cotas de tamanho de entrada e saída, de itens por lote e de tempo de processamento. Ao evoluir a fila local, valide capacidade finita, concorrência por tipo de trabalho e recuperação após cancelamento. PDFs, planilhas e arquivos web entram como dados. Preserve a origem e mantenha macros, links externos e outros recursos ativos sob as políticas de isolamento existentes.

## Configuração e segredos

- Documente o nome da variável, finalidade, escopo e comportamento quando ausente. Um modelo `.env.example` contém chaves e comentários, com valores vazios ou exemplos públicos. As variantes reais de `.env` e `.envrc` ficam fora do Git. A política de exclusão deve ser validada em pastas também.
- Para uma eventual hospedagem, injete segredos em tempo de execução pelo cofre do provedor, com credenciais distintas por ambiente, escopo mínimo e processo de rotação. Use identidade gerenciada quando disponível. Restrinja as variáveis sensíveis ao serviço que usa cada segredo. Preserve os valores fora de código, imagens de build, logs, artefatos e pacotes do frontend.
- Valide configuração obrigatória na inicialização e falhe com uma mensagem que cite a chave, sem expor o valor. Trate uma credencial já versionada como exposta mesmo após alterar o `.gitignore`: revogue, rotacione e apure o alcance.
- `PIZZA_ANIMATIONS` é uma preferência local sem segredo. `PIZZA_MOTOR` e `PIZZA_EXIFTOOL` apontam para ferramentas de teste. Segredos de assinatura e credenciais de hospedagem pertencem apenas aos processos autorizados que precisam deles.

## Requisitos para um eventual serviço com conta ou API

| Controle | Critério verificável antes de habilitar |
| --- | --- |
| Armazenamento de contas | Adotar Argon2id, com salt aleatório por senha, parâmetros versionados e custo validado no hardware de produção. Preferir a implementação do provedor de identidade ou uma biblioteca mantida. Scrypt é uma alternativa. PBKDF2 cabe quando houver requisito de conformidade e parâmetros aprovados. Transporte via TLS e exclusão de senhas de logs, traces e exportações. |
| Segundo fator obrigatório | Todas as contas humanas, inclusive administradores, passam por MFA. Preferir WebAuthn com verificação de usuário ou exigir senha e segundo fator. TOTP pode ser adotado com segredo cifrado em cofre e códigos de recuperação descartáveis. Recuperação e troca do fator mantêm o nível de verificação e revogam sessões afetadas. |
| Limites de uso | Cada endpoint tem limites inclusive para contas autenticadas, combinando identidade, IP e capacidade global. Separe login e recuperação das quotas de operações caras. Nas negativas de quota, devolva HTTP 429 com orientação de reintento. Em múltiplas réplicas, valide que os limites funcionam de forma consistente entre instâncias. |
| Fila e conversão | Limite bytes, duração, concorrência, memória e saída. Faça reserva de capacidade antes do trabalho caro. Use fila limitada e idempotência vinculada à identidade para que o reenvio não duplique a operação. Documente o comportamento durante indisponibilidade. |
| Banco de dados | Use consultas parametrizadas e credenciais com permissões mínimas. Restrinja alterações de esquema a uma identidade separada de migração. Teste autorização no serviço e restauração de backups. |
| Privacidade | Preserve a operação local da Pizza. Um serviço de contas eventual precisa de um desenho explícito de dados, acesso e retenção, com decisão e revisão antes de ser integrado ao desktop. |

Teste em homologação com dados sintéticos. Verifique campo vazio, credencial errada, MFA ausente, recuperação, sessão vencida, limites por identidade e globais, fila cheia, duplicidade, ausência de permissão e indisponibilidade. Observe arquivos originais e logs em cada cenário.

Referências: [OWASP Password Storage](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html), [OWASP Multifactor Authentication](https://cheatsheetseries.owasp.org/cheatsheets/Multifactor_Authentication_Cheat_Sheet.html) e [OWASP REST Security](https://cheatsheetseries.owasp.org/cheatsheets/REST_Security_Cheat_Sheet.html).
