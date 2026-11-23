using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Pizza.Core.Tests;

/// <summary>
/// Poka-Yoke da documentação: nenhum link relativo de um .md rastreado pode apontar
/// para arquivo ou pasta que não existe. Mover um documento sem corrigir os links quebra o teste.
/// </summary>
public class DocsLinksTests
{
    // [texto](destino) ou [texto](destino "título"); o destino pode vir entre < >.
    private static readonly Regex Link = new(
        @"\[(?:[^\]\\]|\\.)*\]\(\s*(<[^>]+>|[^)\s]+)(?:\s+""[^""]*"")?\s*\)",
        RegexOptions.Compiled);

    [Fact]
    public void Links_relativos_dos_markdown_rastreados_apontam_para_arquivos_que_existem()
    {
        var raiz = AcharRaiz();
        var quebrados = new List<string>();

        foreach (var arquivo in ListarMarkdownRastreados(raiz))
        {
            var caminho = Path.Combine(raiz, arquivo);
            if (!File.Exists(caminho)) continue; // removido no índice, ainda não no disco
            // Skill de terceiros copiada como veio; os links dela apontam para o repositório de origem.
            if (arquivo.StartsWith(".agents/skills/caveman/", StringComparison.Ordinal)) continue;

            var pasta = Path.GetDirectoryName(caminho)!;
            var linhas = File.ReadAllLines(caminho);
            var emBloco = false;

            for (var i = 0; i < linhas.Length; i++)
            {
                var linha = linhas[i];
                if (linha.TrimStart().StartsWith("```", StringComparison.Ordinal) ||
                    linha.TrimStart().StartsWith("~~~", StringComparison.Ordinal))
                {
                    emBloco = !emBloco;
                    continue;
                }
                if (emBloco) continue;

                var semCodigo = Regex.Replace(linha, "`[^`]*`", "");
                foreach (Match m in Link.Matches(semCodigo))
                {
                    var alvo = m.Groups[1].Value.Trim('<', '>');
                    if (!EhLinkRelativo(alvo, out var arq)) continue;

                    var destino = arq.StartsWith('/')
                        ? Path.Combine(raiz, arq.TrimStart('/'))
                        : Path.Combine(pasta, arq);
                    destino = Path.GetFullPath(Uri.UnescapeDataString(destino));

                    if (!File.Exists(destino) && !Directory.Exists(destino))
                        quebrados.Add($"{arquivo}:{i + 1} -> {alvo}");
                }
            }
        }

        Assert.True(quebrados.Count == 0,
            "Links quebrados:" + Environment.NewLine + string.Join(Environment.NewLine, quebrados));
    }

    private static bool EhLinkRelativo(string alvo, out string arquivo)
    {
        arquivo = "";
        if (alvo.Length == 0 || alvo.StartsWith('#')) return false;
        if (alvo.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            alvo.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            alvo.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return false;

        var corte = alvo.IndexOfAny(['#', '?']);
        arquivo = corte >= 0 ? alvo[..corte] : alvo;
        return arquivo.Length > 0;
    }

    private static string AcharRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Pizza.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Pizza.slnx não encontrado acima de " + AppContext.BaseDirectory);
    }

    private static IEnumerable<string> ListarMarkdownRastreados(string raiz)
    {
        var info = new ProcessStartInfo("git", "-C \"" + raiz + "\" ls-files -z -- \"*.md\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(info)!;
        var saida = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        Assert.Equal(0, p.ExitCode);
        return saida.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
