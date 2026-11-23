---
trigger: always_on
---

# Invariantes de segurança do Pizza (nunca quebrar)

Versão curta. O texto completo e a fonte da verdade estão em [CLAUDE.md §5](../../CLAUDE.md) e em `docs/1-produto/SPECS.md`.

1. Nenhum dado de usuário sai do PC. A única rede permitida é a consulta de atualização (SEC-50).
2. O arquivo original nunca é escrito, movido ou apagado, e o drop nunca devolve `DROPEFFECT_MOVE` (SEC-05, SEC-31).
3. Sem hook de teclado (SEC-01).
4. Arquivo não confiável só é interpretado no Worker em AppContainer, sem rede (SEC-20).
5. Caminhos nunca trafegam no pipe; o Worker só recebe handles (SEC-15).
6. Log sem nome de arquivo, conteúdo, senha, coordenadas ou área de transferência (PRIV-16).
7. Senha só em buffer zerado após o uso; nunca em string, log ou exceção (SEC-70 a SEC-73).
8. Nenhum instalador sai de build local; só o CI publica (SPECS §11).

Mudança que enfraqueça um item acima precisa de ADR em `docs/2-decisoes/` e aprovação do mantenedor antes do código.
