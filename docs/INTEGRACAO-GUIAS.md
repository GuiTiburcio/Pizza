# Candidato integrado das quatro abas

Esta branch reúne a base aprovada, a fundação de formatos do PR #82 e os menus móveis do PR #87. Aplica a decisão mais recente do mantenedor sobre incompatibilidade: manter as abas e orientar qual arquivo usar, sem chamar uma aba implementada de "Em breve".

## Por que a cópia aberta divergia

A inspeção de 14/11/2026 identificou o executável aberto numa cópia de desenvolvimento do PR #87, com cabeçalho de versão 0.1.1.0. A correção de abas estava no PR #82, separado. A versão numérica sozinha não identifica quais mudanças experimentais foram compiladas. O código atual do Claude em Docs já avançou para Markdown no PR #92; isso também não torna o executável do PR #87 uma compilação do #92.

Não foi possível ler uma janela controlável da conversa do Claude; a conferência usou checkouts, commits, PRs e o caminho do processo. Não foram alterados os arquivos de contexto nem o checkout de trabalho dele. O processo que o mantenedor usa não foi parado ou substituído.

## Contrato do candidato

- Imagem, PDF, Documento e Áudio permanecem visíveis, com um único catálogo e filtragem por entrada.
- Arquivo compatível abre a aba certa e mostra suas conversões. Conforme a revisão do mantenedor após o teste de c1a492c, numa aba incompatível as funções instaladas permanecem visíveis e esmaecidas para descoberta; o rodapé mostra imediatamente qual arquivo usar. Clique/drop/teclado não iniciam conversão incompatível.
- Numa aba vazia, o rodapé orienta a usar a roda para trocar de aba ou Esc para cancelar; não pede clique em fatias ou níveis inexistentes.
- A faixa de níveis não adota um conversor que não aceite os arquivos selecionados.
- Seleção usa a mesma geometria desenhada, inclusive hover, antes de interpretar a borda como movimento/redimensionamento.
- A base preserva os limites de saída/MotW, pausa, cancelamento e rollback aprovados no PR #90 e o roteiro do Brotinho do PR #91.

## Validação antes de pedir novo teste manual

Identificar o candidato pelo SHA e CI, não apenas por "0.1.1". Exigir build, testes, NativeAOT e CodeQL aprovados na branch integrada. A revisão estática encontrou e corrigiu conflito entre alvo em hover e zona de movimento; a apresentação de fatias incompatíveis foi substituída pela escolha explícita do mantenedor.

Depois desses checks, conferir visualmente JPG → PDF/Documento/Áudio, PDF → Imagem, DOCX → Áudio e MP3 → PDF, mantendo o arquivo original. Nas abas incompatíveis devem aparecer as funções esmaecidas e as orientações; na aba correspondente, as operações reais. Repetir com roda, teclado, redimensionamento e arraste do menu, nos dois temas. O mantenedor confirmou o funcionamento da orientação em c1a492c; a revisão posterior de descoberta das fatias ainda requer nova validação visual.

O contrato de confirmação com volta e lote misto agrupado por tipo está em [FLUXO-UNIFICADO.md](FLUXO-UNIFICADO.md). A confirmação com volta está implementada para revisão nesta branch e exige CI/aceite visual; o plano puro de lote ainda não está ligado à interface nem à execução.

Markdown (#92), novos formatos PDF/Web e as correções pendentes de ALAC/negociação de bitrate não são declarados concluídos por esta integração. O teste intermitente do motor Jumbo continua obrigatório e uma falha impede aprovar o CI; ele não será removido para publicar o candidato.
