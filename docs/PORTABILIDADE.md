# Pizza em macOS e Linux — roteiro futuro

Pedido do mantenedor em 14/11/2026. Estado: planejamento, sem versão funcional nem data prometida. A prioridade é concluir e validar o Brotinho para Windows. Este roteiro não autoriza substituir controles de segurança por uma compilação que apenas abra a interface.

## Base e limites atuais

O projeto usa C#/.NET. Parte do catálogo, opções e regras puras pode ser candidata ao compartilhamento, após revisão de dependências. O residente usa Win32/OLE, o isolamento usa AppContainer/Job Object e o áudio usa Media Foundation. Essas integrações precisam de implementação e validação próprias por plataforma; não são portadas apenas trocando o destino de compilação.

Não está decidida uma nova biblioteca de interface nem uma reescrita em Rust. Cada escolha depende de uma prova pequena, das licenças e dos recursos necessários. O formato de saída e a preservação do original continuam sendo os contratos de produto.

## Etapas

1. **Depois do Brotinho Windows validado:** inventariar dependências Windows e identificar regras puras reaproveitáveis. Registrar ADR com o que será compartilhado e o que será específico.
2. **Provas de plataforma:** demonstrar gesto/arraste ou alternativa acessível, menu, teclado, seleção segura de arquivos e saída ao lado do original. Avaliar macOS e Linux sem prometer equivalência antes das provas. Definir a primeira plataforma e, para Linux, distribuições e sessões gráficas suportadas com base em demanda e máquinas de teste disponíveis.
3. **Isolamento antes dos codecs:** provar processo sem rede, acesso mínimo à origem/destino, limites, cancelamento, privacidade e limpeza segura. Documentar diferenças em relação às garantias Windows; não chamar mecanismos distintos de equivalentes sem evidência.
4. **Uma aba por vez:** Imagem, PDF, Documento e Áudio, reutilizando amostras de referência e verificadores independentes. Validar motores e licenças em cada plataforma, incluindo metadados de origem e arquivos com nomes/caracteres locais.
5. **Distribuição:** pipeline por plataforma, identificação da arquitetura, assinatura/verificação aplicável, atualização por canal e desinstalação. Revisão de segurança e testes em máquinas limpas antes de Beta pública.

## Critérios de aceite

- Originais preservados e saídas reais, sem placeholders nem perda silenciosa.
- Isolamento e privacidade testados na plataforma, além de testes unitários compartilhados.
- Matriz pública de funcionalidades e diferenças; avisos para operações não suportadas.
- Estável/Beta separados por plataforma e compatibilidade de versão verificada.
- Custo de manutenção, hardware e assinatura discutido antes de assumir datas ou compromissos de suporte.

Este documento é uma intenção de evolução. A decisão de ordem entre macOS e Linux fica para o marco de portabilidade, após validar o Windows e ouvir usuários; não bloqueia o lançamento do Brotinho Windows.
