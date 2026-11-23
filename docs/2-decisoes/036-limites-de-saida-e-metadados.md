# ADR-036 — Limites de saída e de metadados no residente

- **Status:** teto de saída aprovado pelo mantenedor; implementação sujeita aos testes e à revisão do PR.
- **Data:** 2026-11-14

## Contexto

A revisão estática identificou leitura integral de `Zone.Identifier` no residente e cópia de partes após o Worker sem orçamento absoluto de bytes nem cancelamento. Os limites do Job não protegem esse trabalho do residente. O mantenedor escolheu 2 GB totais por conversão, sem reduzir qualidade nem publicar saídas incompletas. A unidade adotada é a binária já usada nos conversores: 2 GiB, ou 2.147.483.648 bytes.

## Decisão

- Limitar a soma dos arquivos finais a 2 GiB, tanto em saída simples quanto em várias partes. O cabeçalho do pacote temporário tem orçamento separado e limitado pelo máximo de 2.000 partes.
- Limitar a escrita normal no Worker e conferir novamente o tamanho no residente antes de publicar ou copiar. Monitorar o comprimento durante execução e encerramento do Worker. O monitoramento periódico não é uma quota física de disco: há intervalo entre verificações.
- Contar o prazo ativo também durante a cópia no residente e conferir cancelamento entre blocos e antes da publicação. A pausa do Worker continua excluída do tempo ativo (ADR-029).
- Ler no máximo 64 KiB de bytes de `Zone.Identifier`, conferindo durante a leitura, sem depender do comprimento declarado. Manter detecção de BOM e propagar somente a zona, sem URLs.
- Excesso retorna `LIMIT_EXCEEDED`; metadados grandes não viram ausência de MotW. Nenhuma conversão troca qualidade automaticamente para caber.

## Verificação

TST-08 cobre saída simples e soma de partes, fronteira exata, excesso, arquivos de comprimento lógico grande e leitura de metadados em blocos curtos. TST-19 cobre Worker que anuncia conclusão antes de exceder o teto. TST-10 confere originais e ausência de saídas/temporários depois da recusa. Testes pequenos verificam cancelamento da cópia sem gravar gigabytes.

## Consequências

Conversões antes sem teto de saída passam a falhar explicitamente acima do orçamento. Durante a materialização de partes, o pacote e os arquivos temporários coexistem; portanto, o espaço temporário total pode ultrapassar 2 GiB. A leitura limitada de ADS controla memória, mas não estabelece um prazo rígido para I/O bloqueado em compartilhamentos de rede. Essas limitações não devem ser apresentadas como garantias resolvidas.

## Alternativas descartadas

- Confiar apenas no Worker: não protege o residente de uma saída produzida por código comprometido.
- Reduzir qualidade silenciosamente: contraria a escolha do mantenedor.
- Ignorar ADS acima do limite: removeria a marca MotW de modo silencioso.
