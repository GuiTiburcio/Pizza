# Pizza — previsão e critérios de entrega

**Foto de 23/11/2026 · America/Cuiaba.** Base integrada `289dd3b`. A meta de novembro considera seis dias úteis, contando 23 e 30 de novembro, e depende da disponibilidade de revisão e de uma sessão real no Windows. Os intervalos são estimativas de trabalho. Ainda não há histórico suficiente para calcular probabilidade ou uma data estável a partir da velocidade da equipe.

## O que pode terminar em novembro

| Entrega | Previsão | Condição para anunciar |
| --- | --- | --- |
| Demonstração do Brotinho | **27–30/11**, plausível | Escolher um pacote identificado, executar a sequência no app com amostras sintéticas e demonstrar apenas o que passou nesse pacote |
| Candidato do Brotinho para piloto | **30/11**, possível com pouco espaço para retrabalho | Resolver os bloqueios de áudio, integrar a matriz, publicar candidato verificável pelo CI e executar os casos essenciais no Windows. Se qualquer gate falhar, manter a demonstração e adiar o piloto |
| Piloto mais amplo do Brotinho | **Primeira quinzena de dezembro**, condicional | O candidato interno passou, as regressões foram tratadas e a matriz foi repetida no pacote entregue |
| Jumbo com pacote validado | **Dezembro, sem dia defensável** | Fechar isolamento de pastas e limites, reproduzir ou delimitar `WORKER_CRASHED`, aprovar os dois ZIPs no CI e testar a edição completa |
| Pizza v1.0 Estável com as duas edições | **Sem compromisso para novembro** | Auditoria TST-28 do candidato, matriz por edição, distribuição assinada e atualizador verificado. Definir nova data após comprovar os bloqueios e escolher o publicador |

Fase 6 completa, lote misto e polimento do movimento têm validação própria. Senha/AES-256, compressão e PDF → DOCX não entram como itens entregues numa demonstração de novembro. Web, vídeo e lote geral seguem o roteiro da v1.x. A data pretendida não reduz os critérios de privacidade.

## Estado verificável e próxima ação

| Frente | Estado em 23/11 | Próxima evidência necessária |
| --- | --- | --- |
| Quatro abas do Brotinho | Imagem, PDF, Documento e Áudio disponíveis na base integrada. PDF já inclui Imagens e Texto. DOCX para HTML/Markdown e XLSX para CSV estão implementados | Executar o fluxo no candidato, incluindo erros, cancelamento, Arquivos, Desfazer e offline |
| ALAC #101 | [#109](https://github.com/GuiTiburcio/Pizza/pull/109) aberto, com build, testes e CodeQL aprovados em `d2b79b5`. [#119](https://github.com/GuiTiburcio/Pizza/pull/119) aberto para 20 bits, com PCM verificado localmente | Revisão e CI no conjunto integrado. Resolver ou registrar a limitação multicanal sem `chan` sem atribuir posições de alto-falante por suposição |
| Privacidade de PDF/DOCX | [#111](https://github.com/GuiTiburcio/Pizza/pull/111) integrado | Repetir os casos de privacidade no mesmo pacote que será usado no piloto |
| Matriz | [#118](https://github.com/GuiTiburcio/Pizza/pull/118) em rascunho. Geração e conferência de 20 originais verificadas e CI verde em `3d964fa`. A íntegra manual ainda não foi executada | Revisar e integrar o roteiro. Registrar edição, SHA, ambiente e resultado para cada cenário |
| Jumbo | [#113](https://github.com/GuiTiburcio/Pizza/pull/113) aberto com falha de CI no SHA publicado verificado antes da retomada. Na revisão local do WIP, o descarte passou e o motor aquecido oscilou acima de 5 s | Confirmar saída do processo e limpeza antes do próximo job, revisar o patch e validar o SHA publicado |
| Dois pacotes | [#112](https://github.com/GuiTiburcio/Pizza/pull/112) em rascunho com falha no job de build/testes | Provar a composição separada de Brotinho/Jumbo e repetir a integração do motor. Verificar inventário, hash e alcance das dependências |
| Auditoria e lançamento | Há evidência de auditoria anterior. A aprovação de segurança e a validação manual do candidato final continuam abertas | Consolidar correções e evidências. Executar o gate vigente de [lançamento do Brotinho](BROTINHO-LANCAMENTO.md) na revisão a ser entregue |
| Dossiê e workspace | Cadastro documental em cascata já integrado. O workspace local tem checkout, ramos, validação e distribuição por finalidade | Atualizar [fundamentos e planta](../3-design/dossie/FUNDAMENTOS-DO-PIZZA.md) quando mudar escopo, arquitetura ou estado verificável |

## Ordem de trabalho até 30/11

| Janela | Resultado esperado | Se houver bloqueio |
| --- | --- | --- |
| 23–25/11 | Revisar ALAC e matriz, consolidar evidências, escolher o SHA de demonstração | Fixar uma demonstração na base verificável e identificar os casos que dependem do candidato novo |
| 26–27/11 | Gerar o candidato pelo CI. Rodar o roteiro manual com amostras e confirmar integridade dos originais | Corrigir apenas o que bloqueia uso, privacidade ou entendimento. Repassar os casos afetados no SHA seguinte |
| 28–30/11 | Registrar resultado e decidir se o Brotinho entra em piloto. Preservar tempo para uma segunda rodada | Apresentar a prévia validada e registrar por escrito o impedimento do piloto. Acompanhar Jumbo separadamente |

O caminho que determina o prazo é **correção integrada → pacote identificado → teste real → correção dos bloqueios → nova verificação → aceite**. CI, assinatura e evidências precisam corresponder à mesma revisão. Use o [dossiê](../3-design/dossie/README.md) para apresentar decisões e imagens, e as [SPECS](../1-produto/SPECS.md) para checar os critérios. Revise a previsão quando um PR mudar de estado, surgir uma falha de lançamento ou for concluída a sessão manual.
