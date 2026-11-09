using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using Pizza.Core.Output;

namespace Pizza.Integration.Tests;

/// <summary>SEC-30 a SEC-37 e FR-26/27, em disco de verdade. Inclui TST-10 e TST-11.</summary>
public sealed class OutputFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pizza-saida-" + Guid.NewGuid().ToString("N"));
    private readonly string _folder;
    private readonly string _source;
    private readonly TempJournal _journal;

    public OutputFileTests()
    {
        _folder = Path.Combine(_root, "pasta ação");
        Directory.CreateDirectory(_folder);
        _source = Path.Combine(_folder, "fachada.jpg");
        File.WriteAllText(_source, "origem");
        _journal = new TempJournal(Path.Combine(_root, "journal.dat"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Concluir_cria_o_arquivo_ao_lado_e_sem_oculto()
    {
        using var source = PendingOutput.OpenSource(_source);
        using var output = PendingOutput.Create(source, _folder, _journal);
        Write(output, "convertido");

        var name = output.Commit(OutputNaming.Candidates(_source, "png"));

        var path = Path.Combine(_folder, name);
        Assert.Equal("fachada.png", name);
        Assert.Equal("convertido", File.ReadAllText(path));
        Assert.False(File.GetAttributes(path).HasFlag(FileAttributes.Hidden));
        Assert.Empty(Temporaries());
    }

    [Fact]
    public void Temporario_e_oculto_na_pasta_de_destino()
    {
        // SEC-30.
        using var source = PendingOutput.OpenSource(_source);
        using var output = PendingOutput.Create(source, _folder, _journal);

        var temporary = Assert.Single(Temporaries());
        Assert.True(File.GetAttributes(temporary).HasFlag(FileAttributes.Hidden));
    }

    [Fact]
    public void Nunca_sobrescreve_arquivo_existente()
    {
        // FR-27 e SEC-33.
        File.WriteAllText(Path.Combine(_folder, "fachada.png"), "A");
        File.WriteAllText(Path.Combine(_folder, "fachada (1).png"), "B");

        using var source = PendingOutput.OpenSource(_source);
        using var output = PendingOutput.Create(source, _folder, _journal);
        Write(output, "novo");
        var name = output.Commit(OutputNaming.Candidates(_source, "png"));

        Assert.Equal("fachada (2).png", name);
        Assert.Equal("A", File.ReadAllText(Path.Combine(_folder, "fachada.png")));
        Assert.Equal("B", File.ReadAllText(Path.Combine(_folder, "fachada (1).png")));
    }

    [Fact]
    public void Sem_concluir_o_temporario_some()
    {
        // SEC-34: falha ou cancelamento não deixam resto.
        using (var source = PendingOutput.OpenSource(_source))
        using (var output = PendingOutput.Create(source, _folder, _journal))
        {
            Write(output, "parcial");
        }

        Assert.Empty(Temporaries());
        Assert.Equal(["fachada.jpg"], Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    [Fact]
    public void Original_fica_intacto_e_protegido_durante_a_conversao()
    {
        // SEC-31 e TST-10.
        var before = SHA256.HashData(File.ReadAllBytes(_source));

        using (var source = PendingOutput.OpenSource(_source))
        {
            Assert.Throws<IOException>(() => File.AppendAllText(_source, "alterado"));
            using var output = PendingOutput.Create(source, _folder, _journal);
            Write(output, "x");
            output.Commit(OutputNaming.Candidates(_source, "png"));
        }

        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(_source)));
    }

    [Fact]
    public void Pasta_de_destino_redirecionada_por_juncao_e_recusada()
    {
        // SEC-32 e TST-11.
        var other = Path.Combine(_root, "outra pasta");
        Directory.CreateDirectory(other);
        var junction = Path.Combine(_root, "juncao");
        CreateJunction(junction, other);

        using var source = PendingOutput.OpenSource(_source);

        Assert.Throws<UnauthorizedAccessException>(() => PendingOutput.Create(source, junction, _journal));
        Assert.Empty(Directory.GetFiles(other, "*", new EnumerationOptions { AttributesToSkip = 0 }));

        // Directory.Delete recursivo não remove junção sem privilégio de administrador.
        Directory.Delete(junction);
    }

    [Fact]
    public void Caminho_acima_de_260_caracteres_funciona()
    {
        // SEC-36.
        var deep = _root;
        while (deep.Length < 300)
        {
            deep = Path.Combine(deep, new string('p', 40));
        }

        Directory.CreateDirectory(deep);
        var longSource = Path.Combine(deep, "fachada.jpg");
        File.WriteAllText(longSource, "origem");

        using var source = PendingOutput.OpenSource(longSource);
        using var output = PendingOutput.Create(source, deep, _journal);
        Write(output, "ok");
        var name = output.Commit(OutputNaming.Candidates(longSource, "png"));

        Assert.Equal("ok", File.ReadAllText(Path.Combine(deep, name)));
    }

    [Fact]
    public void Marca_de_internet_e_copiada_sem_as_urls()
    {
        // SEC-35.
        File.WriteAllText(_source + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://site.example/a.jpg\r\n");

        using var source = PendingOutput.OpenSource(_source);
        using var output = PendingOutput.Create(source, _folder, _journal);
        output.CopyZoneFrom(_source);
        Write(output, "x");
        var name = output.Commit(OutputNaming.Candidates(_source, "png"));

        Assert.Equal("[ZoneTransfer]\r\nZoneId=3\r\n", File.ReadAllText(Path.Combine(_folder, name) + ":Zone.Identifier"));
    }

    [Fact]
    public void Origem_sem_marca_gera_saida_sem_marca()
    {
        using var source = PendingOutput.OpenSource(_source);
        using var output = PendingOutput.Create(source, _folder, _journal);
        output.CopyZoneFrom(_source);
        Write(output, "x");
        var name = output.Commit(OutputNaming.Candidates(_source, "png"));

        Assert.False(File.Exists(Path.Combine(_folder, name) + ":Zone.Identifier"));
    }

    [Fact]
    public void Limpeza_apaga_o_orfao_registrado_no_diario()
    {
        // SEC-37: simula a queda do residente, com o temporário registrado e nunca concluído.
        var orphan = CreateOrphan();
        Assert.True(File.Exists(orphan));

        new TempJournal(Path.Combine(_root, "journal.dat")).CleanOrphans();

        Assert.False(File.Exists(orphan));
    }

    [Fact]
    public void Limpeza_nunca_apaga_pelo_padrao_do_nome()
    {
        // SEC-37 e TST-26: arquivo com o mesmo padrão, que o Pizza não registrou.
        var stranger = Path.Combine(_folder, ".~pizza-de-outra-pessoa.tmp");
        File.WriteAllText(stranger, "de um colega");

        _journal.CleanOrphans();

        Assert.True(File.Exists(stranger));
    }

    [Fact]
    public void Limpeza_ignora_registro_de_arquivo_que_foi_trocado()
    {
        var orphan = CreateOrphan();
        File.Delete(orphan);
        File.WriteAllText(orphan, "outro arquivo, com o mesmo nome");

        new TempJournal(Path.Combine(_root, "journal.dat")).CleanOrphans();

        Assert.True(File.Exists(orphan));
    }

    [Fact]
    public void Falha_no_diario_nao_deixa_temporario_para_tras()
    {
        var unwritable = new TempJournal(Path.Combine(_root, "pasta inexistente", "journal.dat"));
        using var source = PendingOutput.OpenSource(_source);

        Assert.ThrowsAny<IOException>(() => PendingOutput.Create(source, _folder, unwritable));
        Assert.Empty(Temporaries());
    }

    [Fact]
    public void Orfao_em_uso_continua_no_diario_e_e_limpo_depois()
    {
        // Uma falha passageira não pode fazer o diário esquecer o arquivo.
        var orphan = CreateOrphan();
        var journal = new TempJournal(Path.Combine(_root, "journal.dat"));

        using (File.OpenHandle(orphan, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            journal.CleanOrphans();
            Assert.True(File.Exists(orphan));
        }

        new TempJournal(Path.Combine(_root, "journal.dat")).CleanOrphans();

        Assert.False(File.Exists(orphan));
    }

    [Fact]
    public void Registro_de_arquivo_que_nao_existe_mais_sai_do_diario()
    {
        var orphan = CreateOrphan();
        File.Delete(orphan);
        var journal = new TempJournal(Path.Combine(_root, "journal.dat"));

        journal.CleanOrphans();

        Assert.Equal(0, journal.Count);
    }

    [Fact]
    public void Volume_indisponivel_mantem_o_registro()
    {
        _journal.Add(new TempJournal.Entry(Path.Combine(_root, "volume desconectado\\"), 1, new byte[16]));

        _journal.CleanOrphans();

        Assert.Equal(1, _journal.Count);
    }

    [Fact]
    public void Diario_nao_guarda_o_conteudo_em_texto_claro()
    {
        // SEC-37: protegido por DPAPI.
        CreateOrphan();

        var content = File.ReadAllText(Path.Combine(_root, "journal.dat"));

        Assert.DoesNotContain(Path.GetPathRoot(_root)!, content, StringComparison.Ordinal);
    }

    private string CreateOrphan()
    {
        using var source = PendingOutput.OpenSource(_source);
        var output = PendingOutput.Create(source, _folder, _journal);
        var path = Assert.Single(Temporaries());

        // Fecha o handle sem Dispose, como faria o Windows numa queda.
        output.AbandonForTest();
        return path;
    }

    private string[] Temporaries() => Directory.GetFiles(_folder, ".~pizza-*.tmp", new EnumerationOptions { AttributesToSkip = 0 });

    private static void Write(PendingOutput output, string text)
    {
        using var stream = new FileStream(new SafeFileHandle(output.Handle.DangerousGetHandle(), ownsHandle: false), FileAccess.Write, 0, false);
        stream.Write(System.Text.Encoding.UTF8.GetBytes(text));
    }

    private static void CreateJunction(string link, string target)
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}
