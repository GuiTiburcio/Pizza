**Prévia do Pizza.** Já dá para usar no dia a dia, mas ainda não é a v1.0.

Segure **Ctrl**, arraste um arquivo e solte numa fatia. Nada sai do seu computador.

### O que já funciona

- Imagens: PNG, JPG, WEBP, AVIF, TIFF, GIF, ICO e imagem para PDF, com qualidade, régua de resolução e remoção de metadados.
- Menu radial de vidro, avisos com progresso real, pausa e cancelamento, janela Arquivos e menu da bandeja.
- O arquivo original nunca é alterado; a conversão roda num processo isolado, sem rede.

### Como usar

1. Baixe o `.zip` abaixo e extraia numa pasta sua.
2. Abra o `Pizza.exe`. O ícone aparece perto do relógio.

### Antes de abrir

- **O Windows vai avisar** (SmartScreen), porque as prévias ainda não têm assinatura digital. Clique em "Mais informações" e em "Executar assim mesmo". A assinatura chega na v1.0.
- Sem atualização automática nas prévias: baixe a próxima quando quiser.
- Ainda faltam documentos, áudio, configurações e o instalador.

### Conferir o download

- Hash: compare o SHA-256 do `.zip` com o arquivo `SHA256SUMS` (`Get-FileHash .\arquivo.zip`).
- Origem: `gh attestation verify .\arquivo.zip --repo GuiTiburcio/Pizza` confirma que o pacote foi gerado por este repositório, no GitHub Actions.
- Componentes: a lista completa está em `sbom.cdx.json`.

Encontrou um problema? Abra uma issue. Falhas de segurança vão pelo canal privado descrito em `SECURITY.md`.
