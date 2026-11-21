---
trigger: always_on
---

# Build e testes do Pizza

Sem rede, o NuGetAudit falha com `NU1900`. Sempre passe `-p:NuGetAudit=false`.

Use saída curta para não gastar contexto com logs:

```powershell
dotnet build -p:NuGetAudit=false -v q -clp:ErrorsOnly
dotnet test  -p:NuGetAudit=false --nologo -v q --logger "console;verbosity=minimal"
```

Se algo falhar, rode de novo só o projeto ou o teste afetado (`--filter`) com mais detalhe. Não repita a solução inteira em modo detalhado.
