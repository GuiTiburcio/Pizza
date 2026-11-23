# ADR-019 — Simplificações do v1 (S-01 a S-05)

- **Status:** Aceita
- **Data:** 2026-11-08

## Contexto

A auditoria apontou caminhos mais complexos do que o necessário, e complexidade é superfície de ataque.

## Decisão

S-01: só LibreOffice isolado. S-02: um Worker por arquivo, sempre. S-03: políticas POL-02, 03, 04, 05, 06, 12 e 15. S-04: sem pulso de 120 ms, pausa de 1 h, dica desligável e cor de destaque configurável. S-05: nenhum download além da atualização.

## Consequências

Somem as lacunas L-01 e L-03 e o item V-01. DOCX complexo pode sair um pouco diferente do Word. O usuário instala o LibreOffice por conta própria. A POL-12 fica sem efeito enquanto não houver downloads.

## Alternativas descartadas

- Word por COM.
- Worker aquecido para arquivos locais.
- 15 políticas de TI.
