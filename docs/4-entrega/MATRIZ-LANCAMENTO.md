# Matriz manual de lançamento

Use o mesmo candidato durante a rodada. Identifique edição, SHA, hash do pacote, CI, versão do Windows, escalas, monitores e data. Teste primeiro o Brotinho. Repita os casos comuns no Jumbo e execute os exclusivos com o motor identificado. Registre resultado como passou, falhou, bloqueado ou não executado. Use somente amostras sintéticas e anote o resultado observado.

Em uma pasta de QA vazia, fora do pacote publicado, gere as amostras em volume NTFS com PowerShell:

```powershell
./tools/gerar-amostras.ps1 -Destino 'C:\QA\Pizza\candidato\amostras'
./tools/gerar-amostras.ps1 -Destino 'C:\QA\Pizza\candidato\amostras' -Conferir
```

Guarde uma cópia do manifesto de hashes junto às evidências antes dos testes. Após os casos, repita `-Conferir` usando o manifesto original. A conferência ignora as saídas e verifica os originais, a marca da Internet e o atributo somente leitura.

| ID | Cenário | Aceite |
| --- | --- | --- |
| I01 | Converter `foto-orientacao-6-com-gps.jpg` para PNG e JPG | Faixa e quadrado aparecem na orientação esperada. GPS e autor ausentes na saída padrão. Conferir cores e dimensões |
| I02 | Converter `transparencia-com-autor.png` para PNG e WebP | Alfa do gradiente preservado conforme o destino, autor removido, imagem válida |
| I03 | Abrir `animado-3-quadros.gif` e `animado-3-quadros.webp` nas operações oferecidas | Conferir quadros e atraso segundo o contrato de cada destino. Se o destino for estático, comportamento explícito |
| I04 | Converter `scanner-300dpi.tiff` em PNG e PDF | Dimensões e densidade conforme as opções, sem corte ou reorientação indevida |
| P01 | Girar, Dividir e Juntar `paginas-1-a-5.pdf` e `paginas-A-e-B.pdf` | Páginas preservadas. Ordem do gesto, contagem, rotação e nomes conferidos |
| P02 | Girar `com-javascript-e-autor.pdf`, conferir a saída em leitor independente | Texto visível permanece. Autor e ações proibidas ausentes. Inspecionar sem executar ações do documento |
| P03 | PDF → Imagens e Texto com as páginas numeradas | Uma imagem por página e texto na ordem certa. Em PDF sem texto, aviso claro e nenhuma saída vazia |
| D01 | Converter `documento-completo.docx` em TXT, HTML e Markdown | Texto legível e com acentos. HTML embute imagens. Markdown grava referências e arquivos corretos. Conferir orientação visual e metadados removidos |
| D02 | Converter `planilha-formulas.xlsx` para CSV | Separador regional coerente, texto e células preservados, prefixos perigosos neutralizados |
| D03 | Converter `documento-da-internet.docx` | Política da edição aplicada. Marca da Internet preservada quando há saída |
| A01 | Converter `curto-1000-quadros-16bit.wav` para M4A sem perda e reler como WAV | 1000 quadros, dois canais e PCM idêntico |
| A02 | Converter os WAVs 16/24 bits e `canais-5.1-16bit-48k.wav` para os destinos compatíveis | Canais, duração, silêncio e profundidade conforme o destino. Comparar PCM quando o formato e as opções forem sem perda |
| A03 | Pedir bitrate incompatível, cancelar, repetir e aceitar | Cancelar não grava. Aceitar usa somente a taxa confirmada. Cartão e Arquivos exibem a mesma escolha |
| A04 | Abrir Volume, alterar valores, voltar com Esc e repetir com Aplicar | Voltar descarta a edição local, Aplicar retorna às opções com os valores. Conversão começa somente após confirmação |
| G01 | Converter `colisao.png` para JPG duas vezes | Novos nomes distintos. O JPG preexistente permanece intacto |
| G02 | Converter `somente-leitura.png`, repetir para destino sem escrita | Origem preservada. Destino sem escrita gera aviso e não deixa temporários |
| G03 | Cancelar durante o arraste, depois no painel e depois durante uma conversão | Sem arquivo indevido ou cópia pendente. Conversão seguinte funciona |
| G04 | Clicar fora dos alvos e em opção incompatível. Testar teclado, foco, painéis e Desfazer com saídas sintéticas | Nenhuma conversão em área vazia. Avisos, foco e recuperação coerentes. Desfazer afeta somente as saídas |
| G05 | Rodar sem internet e com tema claro/escuro, movimento reduzido, escalas 100% a 200% e monitor secundário | Conversões disponíveis offline. Menu, avisos e barra continuam legíveis e utilizáveis |
| G06 | Tentar arrastar tipos misturados em um gesto | Na versão atual, o lote geral é recusado sem copiar entradas. A exceção Juntar usa apenas PDFs |
| J01 | No Jumbo, converter `texto-acentuado.rtf` e `documento-completo.docx` para PDF | Layout e acentos legíveis, originais intactos. Funções exclusivas ausentes no Brotinho |
| J02 | Confirmar resultado da suíte de isolamento e limpeza do motor Jumbo no candidato | Rede negada, fontes intactas, sem cópias de jobs anteriores. Intermitências registradas com o SHA e log do CI |

## Checklist de aceite

- [ ] Confirmar que o pacote corresponde ao SHA e que CI, CodeQL e SBOM foram conferidos.
- [ ] Executar os casos aplicáveis e guardar severidade, evidência e destino de cada falha.
- [ ] Reexecutar casos afetados após correção, no novo candidato, com amostras limpas.
- [ ] Conferir hashes e atributos de origem. Verificar que cancelamento, timeout e falha deixam o próximo job utilizável.
- [ ] Registrar a revisão de privacidade e o gate TST-28 do candidato. A matriz não substitui a auditoria.
- [ ] Registrar aceite visual no app real e a decisão de liberação por edição. Conferir os [critérios do Brotinho](BROTINHO-LANCAMENTO.md).
