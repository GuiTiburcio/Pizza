# Pizza Brotinho — critérios de lançamento

Decisão do mantenedor em 14/11/2026: concluir e validar Imagem, PDF, Documento e Áudio antes da divulgação comercial do Brotinho; preparar distribuição verificada e, então, lançar. O portfólio pode registrar a construção desde agora, distinguindo implementação, evidência e planejamento. Este documento acompanha as SPECS, sem substituir seus portões nem declarar a versão pronta.

## Como registrar o estado

- **Planejado:** decisão ou desenho sem implementação entregue.
- **Implementado:** código disponível na main, com link do commit/PR.
- **Testado:** comando/cenário aprovado numa revisão e ambiente identificados.
- **Aceito para lançamento:** testes necessários e validação manual do mantenedor aprovados no candidato de lançamento.

Cada aprovação registra SHA, execução de CI, versão do Windows, data e resultado. Aprovação antiga não se transfere automaticamente a outro build. Não há lançamento enquanto houver defeito conhecido de perda de conteúdo, alteração do original ou redução de qualidade sem consentimento.

## Quatro abas

| Aba | Escopo implementado a validar no Brotinho | Evidência e pendências |
| --- | --- | --- |
| Imagem | JPG, PNG, WEBP, AVIF, TIFF, ICO, GIF e Imagem → PDF, conforme catálogo | O mantenedor já confirmou uso funcional. Repetir matriz no candidato final, incluindo qualidade, dimensões, orientação, metadados e transparência quando suportados. GIF é estático nesta etapa. |
| PDF | Juntar, dividir e girar | Conferir ordem, número de páginas, rotações, texto/imagens e remoção de conteúdo ativo. Senhas e demais conversões não são prometidas por esta matriz. |
| Documento | DOCX → TXT/HTML e XLSX → CSV | Conferir texto, acentos, listas, tabelas, links e imagens conforme cada destino. HTML teve correções de listas e quebras de código no PR #88, aprovado no CI e integrado. Neutralização de fórmulas CSV integrada no PR #90, com regressões aprovadas. Markdown é futuro; renderização Office → PDF pertence ao Jumbo. |
| Áudio | MP3, M4A/AAC ou ALAC, FLAC e WAV; opções de canais, taxa, profundidade e volume | Regressões ALAC e WAV mono de 24 bits corrigidas e verificadas no PR #84; confirmação de bitrate no PR #95, com prévias aprovadas pelo mantenedor. Repetir a matriz manual no candidato final, incluindo efeitos, reamostragem e destinos com perda. |

O escopo corresponde aos conversores realmente oferecidos. Formatos futuros não devem aparecer como funcionais. Não reduzir esta matriz nem esconder um formato com defeito para declarar a aba concluída sem decisão do mantenedor.

## Validação por aba

1. **Imagem:** arquivos sintéticos e amostras públicas em cada entrada/saída suportada; verificar dimensão, orientação, alfa, metadados e abertura independente. Compressão com perda é identificada; não prometer igualdade de pixels em JPG/WEBP/AVIF com perda.
2. **PDF:** PDFs sintéticos com páginas distintas, texto e imagem; conferir conteúdo e ordem após juntar/dividir, rotação e ausência dos objetos ativos proibidos (TST-22).
3. **Documento:** amostras sintéticas com acentos, tabelas e estrutura. CSV com separadores, aspas, tipos alternativos e prefixos de fórmula; HTML com escape, links recusados, imagens internas/externas e listas aninhadas. Comparar o conteúdo com o resultado esperado, não apenas a extensão.
4. **Áudio:** PCM conhecido de 16/24 bits, canais diferentes, picos e silêncio real no início/fim. Para sem perda, comparar quantidade e valor das amostras. Para destinos com perda, validar duração, canais, taxa e qualidade efetivamente aplicada. Testar recusa de bitrate sem conversão automática com qualidade menor.
5. **Comum:** hash da origem antes/depois (TST-10), nomes existentes, MotW, limites, cancelamento/pausa, teclado, DPI/temas e ausência de parciais. Usar cópias sintéticas; nunca exigir documentos pessoais para reportar falhas.

CI e CodeQL aprovados são necessários. A validação manual do aplicativo e os demais portões das SPECS também são necessários. O scan estático de 13–14/11/2026 teve cobertura parcial: não fecha TST-28 nem prova que todo o repositório está seguro.

**Marco verificado em 14/11/2026:** PR #90 integrado após CI e CodeQL aprovados; 903 testes, zero falhas e zero ignorados, incluindo excesso de saída, ADS, CSV, pausa, cancelamento e rollback. Não é aceite manual das quatro abas. O teste do motor Jumbo `Macro_ao_abrir_nao_executa` apresentou falha intermitente `WORKER_CRASHED` numa rodada documental e passou na base com as correções; sua causa permanece sem confirmação e o teste continua obrigatório.

**Áudio verificado em 15/11/2026 (America/Cuiaba):** código `c66efb001567c8be1d29f7a4faf323981092768a`, [CI #259](https://github.com/GuiTiburcio/Pizza/actions/runs/43786725684) e [CodeQL #260](https://github.com/GuiTiburcio/Pizza/actions/runs/43786725701) aprovados em Windows Server 2025 `10.0.26100`, imagem `windows-2025-vs2026` `20261103.298.1`. Foram 966 testes aprovados (Core 567, App 40, Converters 247, Integration 112), zero falhas e zero ignorados, com NativeAOT e Worker publicados. ALAC preservou cada amostra, comprimento e canais nos casos sintéticos de 16/24 bits, 44,1/48 kHz, limites de pacote e silêncio real. O Worker converteu uma hora de PCM estéreo em 94,77 s, dentro do prazo de 120 s; a releitura preservou o hash e o comprimento do PCM. O teste de macro passou nesta rodada, sem resolver a causa da intermitência anterior. A confirmação de bitrate foi validada na CI #255, e o mantenedor aprovou o cartão e o painel Arquivos desenhados pelo `DiscRenderer`.

## Distribuição e canais

Decisão do mantenedor: **Estável e Beta**, sem LTS nesta etapa. Brotinho/Jumbo são edições; Estável/Beta são canais de maturidade. macOS/Linux são plataformas futuras, não canais.

- Estável é o padrão no lançamento; Beta exige escolha explícita e notas claras sobre limitações.
- A prévia 0.x atual continua sendo prévia, sem assinatura nem atualizador automático (ADR-031). Não renomeá-la como estável para contornar validações.
- Implementar consulta no máximo diária por HTTPS, manifesto Ed25519 válido com produto/canal/plataforma/expiração e pacote com hash e publicador verificados (SEC-50–56). O canal é parte da informação assinada.
- Instalar somente após clicar em Atualizar. Nenhuma troca automática de Estável para Beta. Ao sair da Beta, não fazer downgrade automático: manter a instalação e oferecer a próxima Estável compatível com anti-rollback.
- MSI por máquina só informa disponibilidade e orienta a TI (SEC-57). Sem telemetria nem dados de arquivos na consulta.
- Publicar apenas pelo CI: candidato identificado por SHA, testes e CodeQL aprovados, artefato completo, licenças, SBOM, proveniência, hashes e assinaturas exigidas pelas SPECS. Chaves privadas fora do CI comum.
- Testar manifesto adulterado/vencido, canal/plataforma errados, pacote trocado, versão antiga, falha de rede e interrupção de instalação. Nenhum pacote não verificado pode ser executado.

O atualizador e a assinatura estável ainda são trabalho pendente. A configuração de assinatura exigirá escolher uma identidade de publicador e um serviço de assinatura disponível; não criar contas, gastos ou certificados sem essa definição com o mantenedor.

## Portfólio e próximo marco

Manter uma demonstração com arquivos de exemplo, uma tabela de capacidades e limitações, links dos PRs/testes e um relato das decisões de segurança e experiência. Informar o apoio de ferramentas de IA com transparência, sem exigir do mantenedor um cargo profissional ou prometer serviços ainda não validados.

Próximo marco: executar as quatro matrizes manuais no mesmo candidato, incluindo os casos de Áudio já cobertos pela CI. Depois, completar os portões de segurança e validar distribuição e atualização; só então abrir o lançamento Estável. O roteiro de portabilidade permanece [separado](PORTABILIDADE.md).
