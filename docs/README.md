# Documentação do Pizza

Este é o mapa da documentação. As pastas são numeradas na ordem em que o projeto é pensado: primeiro **o que** se constrói, depois **por que** cada escolha foi feita, **como** parece, **quando** entrega, **como se usa**, **como se protege** e **como melhora**.

```text
docs/
├── 1-produto/     o que o Pizza é e promete
├── 2-decisoes/    por que o projeto é assim
├── 3-design/      como ele parece
├── 4-entrega/     quando e como cada fase sai
├── 5-operacao/    como instalar, usar e implantar
├── 6-seguranca/   como ele se protege
└── 7-melhoria/    como o projeto aprende e melhora
```

## A cascata

```text
produto → decisões → design → entrega → operação → segurança → melhoria
```

Os números são a ordem de navegação, não fases obrigatórias: ninguém precisa terminar uma pasta para começar a outra. Segurança, privacidade, operação e acessibilidade valem desde os requisitos (pasta 1); as pastas 5 e 6 só reúnem a prova de que eles foram cumpridos.

Cada pasta nasce da anterior. O produto define o que vale; as decisões registram por que; o design mostra como fica; a entrega divide em fases; a operação põe nas mãos de quem usa; a segurança confere o conjunto; a melhoria devolve o aprendizado ao começo da cascata. Arquivo novo vai na pasta que corresponde ao seu papel.

## Pasta por pasta

| Pasta | O que há nela |
| --- | --- |
| [1-produto](1-produto/) | [Visão do projeto](1-produto/PIZZA-PROJETO.md), [SPECS](1-produto/SPECS.md) (requisitos; as emendas da §2.1 prevalecem), [roteiro](1-produto/ROADMAP.md) e [macOS e Linux](1-produto/PORTABILIDADE.md) |
| [2-decisoes](2-decisoes/README.md) | ADRs numerados (`NNN-titulo.md`) com o contexto, a decisão e as consequências |
| [3-design/dossie](3-design/dossie/README.md) | Artefato do Claude Design: pranchas, fluxos e wireframes |
| [3-design/propostas](3-design/propostas/) | Propostas de design e planos ainda em discussão |
| [3-design/imagens](3-design/imagens/) | Imagens usadas pelo README e pela documentação |
| [4-entrega](4-entrega/) | [Fase 4](4-entrega/FASE-4.md), [Fase 5](4-entrega/FASE-5.md), [Fase 6](4-entrega/FASE-6.md), [lançamento do Brotinho](4-entrega/BROTINHO-LANCAMENTO.md) e [validação do piloto](4-entrega/VALIDACAO-PILOTO.md) |
| [5-operacao](5-operacao/) | [Como usar](5-operacao/USO.md), [instalar e configurar](5-operacao/INSTALACAO.md) e [guia de TI (GPO e Intune)](5-operacao/TI-GPO.md) |
| [6-seguranca](6-seguranca/) | [Auditoria de segurança](6-seguranca/AUDITORIA-SEGURANCA.md) |
| [7-melhoria](7-melhoria/TI-360.md) | [Processo TI 360](7-melhoria/TI-360.md), a [validação e governança](7-melhoria/VALIDACAO-E-GOVERNANCA.md) das frentes e as [ideias descartadas](7-melhoria/ideias-descartadas/README.md), com o motivo de cada uma |

## Fora da pasta `docs`

| Arquivo | Para quê |
| --- | --- |
| [Contribuir](../.github/CONTRIBUTING.md) | Ambiente, testes e pull requests |
| [Segurança](../.github/SECURITY.md) | Canal para vulnerabilidades |

O dossiê mantém o planejamento e decisões históricas. Consulte o roteiro e o README da raiz para saber o estado atual da implementação.
