# ADR-003 — NativeAOT + Win32/Direct2D no residente

- **Status:** Aceita
- **Data:** 2026-11-01

## Contexto

O residente precisa abrir rápido, gastar menos de 20 MB e não exigir .NET instalado.

## Decisão

Residente compilado com NativeAOT, janelas em Win32 puro e desenho com Direct2D (Vortice.Windows) + DWM Acrylic.

## Consequências

Sem pré-requisitos e com pouca RAM. Exige linker C++ no build e cuidado com COM/reflection (sem trimming inseguro).

## Alternativas descartadas

- WPF no residente (dezenas de MB).
