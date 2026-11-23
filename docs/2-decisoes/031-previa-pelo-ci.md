# ADR-031 — Prévia 0.x publicada pelo CI

- **Status:** Aceita pelo mantenedor em 11/11/2026.
- **Data:** 2026-11-11

## Contexto

O mantenedor quer um .exe público o quanto antes. A §11 da SPECS descreve o lançamento estável: instalador assinado (SignPath), `SHA256SUMS` assinado com Ed25519, tag assinada e job de assinatura com aprovação manual. Isso depende das Fases 5 a 8 (auditoria, atualização verificada, instaladores). Esperar tudo isso atrasaria qualquer versão pública em semanas.

## Decisão

- Antes da v1.0, o Pizza pode ter **prévias** `0.x.y-previa`, publicadas no GitHub Releases como **pré-lançamento**.
- A prévia é um `.zip` portátil com `Pizza.exe`, o Worker e as bibliotecas, sem instalador. Basta extrair e abrir.
- Só o GitHub Actions gera a prévia, a partir de uma tag `v*-previa*` num commit que já está na `main` (invariante 8 do `CLAUDE.md`). Builds locais continuam proibidos de circular.
- O workflow roda os mesmos testes do CI antes de empacotar. Junto do `.zip` vão o `SHA256SUMS`, o SBOM CycloneDX e o atestado de proveniência (`actions/attest-build-provenance`).
- A prévia **não** tem assinatura Authenticode nem `SHA256SUMS` assinado com Ed25519: o Windows SmartScreen avisa ao abrir. As notas da versão dizem isso e explicam como conferir o hash e o atestado.
- A prévia não tem atualização automática (SEC-50 a SEC-55 ainda não existem): a pessoa baixa a próxima prévia quando quiser.

## Consequências

Uma versão pública já com a cadeia de origem verificável (proveniência e SBOM), sem esperar a assinatura. O aviso do SmartScreen é o custo, e fica explicado. Nada muda no lançamento estável: a v1.0 segue a §11 inteira.

## Alternativas descartadas

- **Esperar a v1.0:** atrasa o retorno de quem usa.
- **Instalador sem assinatura:** dá mais trabalho e tem o mesmo aviso do SmartScreen.
- **Zip de build local:** proibido pela invariante 8.
