# Componentes de terceiros

Dependências diretas desta prévia. Este documento não substitui o SBOM nem os avisos de todos os componentes nativos transitivos, pendentes antes de uma distribuição estável.

| Componente | Uso | Licença |
| --- | --- | --- |
| .NET 10 | Runtime, bibliotecas e NativeAOT | MIT e avisos de terceiros do runtime |
| NetVips 3.2.0 | Binding do conversor | MIT |
| libvips / NetVips.Native.win-x64 8.18.7 | Conversão nativa no Worker | LGPL-2.1-or-later, além das licenças dos codecs incluídos |
| QPDF 12.4.2 (`qpdf30.dll`) | Operações de estrutura de PDF no Worker ([ADR-033](docs/adr/033-pdfium-e-qpdf.md)) | Apache-2.0 |
| Runtime do Visual C++ (`msvcp140.dll`, `vcruntime140.dll`, `vcruntime140_1.dll`) | Importado pela `qpdf30.dll`, distribuído junto com ela | Licença de redistribuição do Visual C++ |
As versões exatas dos pacotes transitivos estão nos `packages.lock.json`. A QPDF, o PDFium e a libjxl não vêm do NuGet: o build baixa as releases e confere o SHA-256 fixado em `src/Pizza.Converters/Pizza.Converters.csproj`; `build/native-deps.json` repete versão, licença e hash, e o SBOM da prévia os inclui. O ExifTool é apenas um verificador do CI e não acompanha o Pizza. Sons WAV são sintetizados por `tools/gerar-sons.ps1`. GDI+ e Win32/OLE pertencem ao Windows.
| xUnit.net | Testes | Apache-2.0 |
| Microsoft.NET.Test.Sdk | Executor de testes | MIT |
| Coverlet | Cobertura de testes | MIT |

As versões exatas dos pacotes transitivos estão nos `packages.lock.json`. A QPDF, o PDFium e a libjxl não vêm do NuGet: o build baixa as releases e confere o SHA-256 fixado em `src/Pizza.Converters/Pizza.Converters.csproj`; `build/native-deps.json` repete versão, licença e hash, e o SBOM da prévia os inclui. O ExifTool é apenas um verificador do CI e não acompanha o Pizza. Sons WAV são sintetizados por `tools/gerar-sons.ps1`. GDI+ e Win32/OLE pertencem ao Windows.
