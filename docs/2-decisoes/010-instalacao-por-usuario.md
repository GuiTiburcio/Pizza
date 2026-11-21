# ADR-010 — Instalação por usuário, sem admin

- **Status:** Aceita
- **Data:** 2026-11-01

## Contexto

PCs de escritório costumam não dar direito de administrador.

## Decisão

Instalar em `%LOCALAPPDATA%\Programs\Pizza` e iniciar pela chave `Run` do usuário.

## Consequências

Instala sem pedir senha de TI. O hook não alcança janelas elevadas (risco documentado).

## Alternativas descartadas

- Instalação por máquina.
