using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Pizza.Core.Ipc;
using Pizza.Core.Output;

namespace Pizza.Integration.Tests;

/// <summary>Motor de documentos do Pizza Jumbo (ADR-034): o LibreOffice isolado, sem rede, sem macros e sem janela.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class DocumentTests : IDisposable
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private const string Profile = "Pizza.Docs.Test";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pizza-docs-" + Guid.NewGuid().ToString("N"));
    private readonly string _docs;
    private readonly List<DocumentEngine> _engines = [];

    public DocumentTests()
    {
        Directory.CreateDirectory(_folder);
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(repository!.FullName, "Pizza.slnx")))
        {
            repository = repository.Parent;
        }

        // O Pizza.Docs vai para uma pasta própria, como no pacote: o motor dá acesso só ao que precisa.
        var binaries = Path.Combine(repository.FullName, "src", "Pizza.Docs", "bin", Configuration, "net10.0-windows", "win-x64");
        var copy = Path.Combine(_folder, "docs");
        Directory.CreateDirectory(copy);
        foreach (var file in Directory.EnumerateFiles(binaries))
        {
            File.Copy(file, Path.Combine(copy, Path.GetFileName(file)));
        }

        _docs = Path.Combine(copy, "Pizza.Docs.exe");
    }

    public void Dispose()
    {
        foreach (var engine in _engines)
        {
            engine.Dispose();
        }

        SandboxedProcess.DeleteProfile(Profile);
        Directory.Delete(_folder, recursive: true);
        File.Delete(_folder + ".journal");
    }

    [JumboFact]
    public void Docx_vira_pdf_isolado()
    {
        // TST-02 para documentos: o DOCX sai em PDF ao lado do original.
        var source = Write("contrato.docx", Docx("Pizza Jumbo converte DOCX em PDF."));

        var result = Engine().Run(source, "document.pdf", "pdf");

        Assert.Null(result.ErrorCode);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(_folder, "contrato.pdf")).AsSpan(0, 5)));
    }

    [JumboFact]
    public void Macro_ao_abrir_nao_executa()
    {
        // TST-12 e SEC-62: uma macro ligada à abertura do documento tentaria gravar este arquivo.
        // Guarda de regressão: em 12/11/2026 a mesma macro também não rodou fora do sandbox com macros permitidas
        // no perfil, porque o LibreOfficeKit abre sem executar macros. A prova com um documento cuja macro
        // comprovadamente roda está pendente (FASE-5/ADR-034).
        var marker = Path.Combine(DocumentEngine.WorkFolder(Profile), "macro-rodou.txt");
        File.Delete(marker);
        var source = Write("macro.odt", OdtWithOpenMacro(marker));

        var result = Engine().Run(source, "document.pdf", "pdf");

        Assert.Null(result.ErrorCode);
        Assert.False(File.Exists(marker));
    }

    [JumboFact]
    public void Imagem_remota_nao_gera_conexao()
    {
        // TST-16 e SEC-63: uma imagem vinculada a um endereço de rede não é buscada.
        // Guarda de regressão: em 12/11/2026 o motor também não buscou a imagem fora do sandbox. O bloqueio de rede
        // do AppContainer em si é provado pelos testes de sandbox (DNS e conexão negados, SEC-20).
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var source = Write("remoto.docx", Docx("Imagem remota abaixo.", $"http://127.0.0.1:{port}/pizza.png"));

        var result = Engine().Run(source, "document.pdf", "pdf");

        Assert.Null(result.ErrorCode);
        Assert.False(listener.Pending());
    }

    [JumboFact]
    public void Letra_temporaria_some_quando_o_motor_de_reserva_expira()
    {
        // ADR-034: a letra existe só enquanto há um motor convertendo ou de reserva.
        var source = Write("letra.docx", Docx("Letra temporária."));
        var engine = Engine(spareIdle: TimeSpan.FromSeconds(3));

        engine.Run(source, "document.pdf", "pdf");
        Assert.NotEmpty(DocumentEngine.DrivesFor(JumboFactAttribute.Engine!)); // a reserva para o próximo documento

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DocumentEngine.DrivesFor(JumboFactAttribute.Engine!).Count > 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(500);
        }

        Assert.Empty(DocumentEngine.DrivesFor(JumboFactAttribute.Engine!));
    }

    [JumboFact]
    public void Motor_aquecido_converte_em_poucos_segundos()
    {
        // ADR-034: o motor carrega enquanto a pessoa escolhe a fatia; o documento em si leva pouco.
        var source = Write("rapido.docx", Docx("Motor aquecido."));
        var engine = Engine();
        engine.WarmUp();
        Thread.Sleep(TimeSpan.FromSeconds(25));

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var result = engine.Run(source, "document.pdf", "pdf");

        Assert.Null(result.ErrorCode);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"levou {clock.Elapsed.TotalSeconds:F1} s");
    }

    [JumboFact]
    public void Limpeza_ao_abrir_apaga_sobras_e_mantem_o_modelo()
    {
        // Um motor de reserva encerrado à força deixa a pasta dele para trás.
        Engine().Run(Write("sobra.docx", Docx("Sobra.")), "document.pdf", "pdf");
        _engines[^1].Dispose();
        var work = DocumentEngine.WorkFolder(Profile);
        Directory.CreateDirectory(Path.Combine(work, "sobra-de-teste"));

        DocumentEngine.CleanWorkFolder(Profile);

        Assert.All(Directory.GetDirectories(work), folder => Assert.StartsWith("modelo-", Path.GetFileName(folder)));
        Assert.NotEmpty(Directory.GetDirectories(work, "modelo-*"));
    }

    [JumboFact]
    public void Perfil_modelo_e_criado_para_a_versao_do_motor()
    {
        // O modelo nasce logo depois de o motor iniciar, antes de qualquer documento.
        var source = Write("modelo.docx", Docx("Perfil-modelo."));

        Engine().Run(source, "document.pdf", "pdf");

        Assert.NotEmpty(Directory.GetDirectories(DocumentEngine.WorkFolder(Profile), "modelo-*"));
    }

    [JumboFact]
    public void Motor_atras_de_um_atalho_de_pasta_funciona()
    {
        // A permissão e a letra valem para a pasta real: dadas ao atalho, o sandbox não leria o motor.
        // Perfil novo: sem nenhuma permissão dada antes à pasta real por outro teste.
        var profile = "Pizza.Docs.Atalho." + Guid.NewGuid().ToString("N")[..8];
        var link = Path.Combine(_folder, "motor");
        Directory.CreateSymbolicLink(link, JumboFactAttribute.Engine!);
        var source = Write("atalho.docx", Docx("Motor por atalho."));
        try
        {
            var engine = Engine(link, profile);
            var result = engine.Run(source, "document.pdf", "pdf");
            engine.Dispose();

            Assert.Null(result.ErrorCode);
        }
        finally
        {
            RemoveAccess(JumboFactAttribute.Engine!, SandboxedProcess.EnsureProfile(profile));
            SandboxedProcess.DeleteProfile(profile);
        }
    }

    private static void RemoveAccess(string folder, string sid)
    {
        var directory = new DirectoryInfo(folder);
        var acl = directory.GetAccessControl();
        acl.PurgeAccessRules(new System.Security.Principal.SecurityIdentifier(sid));
        directory.SetAccessControl(acl);
    }

    [JumboFact]
    public void Conversao_fora_da_lista_e_recusada()
    {
        var source = Write("lista.docx", Docx("Fora da lista."));

        var result = Engine().Run(source, "document.docx", "docx");

        Assert.Equal("UNSUPPORTED", result.ErrorCode);
        Assert.False(File.Exists(Path.Combine(_folder, "lista (1).docx")));
    }

    private DocumentEngine Engine(string? engineRoot = null, string profile = Profile, TimeSpan? spareIdle = null)
    {
        var engine = new DocumentEngine(_docs, engineRoot ?? JumboFactAttribute.Engine!, new TempJournal(_folder + ".journal"), TimeSpan.FromMinutes(2), profile)
        {
            SpareIdle = spareIdle ?? TimeSpan.FromSeconds(60),
        };
        _engines.Add(engine);
        return engine;
    }

    private string Write(string name, byte[] content)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>DOCX mínimo; com <paramref name="remoteImage"/>, um parágrafo com imagem vinculada (TargetMode External).</summary>
    private static byte[] Docx(string text, string? remoteImage = null)
    {
        var image = remoteImage is null ? "" : """
            <w:p><w:r><w:drawing><wp:inline><wp:extent cx="952500" cy="952500"/><wp:docPr id="1" name="remota"/>
            <a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic>
            <pic:nvPicPr><pic:cNvPr id="1" name="remota"/><pic:cNvPicPr/></pic:nvPicPr>
            <pic:blipFill><a:blip r:link="rIdImg"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>
            <pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="952500" cy="952500"/></a:xfrm><a:prstGeom prst="rect"/></pic:spPr>
            </pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>
            """;
        var relationships = remoteImage is null ? "" : $"""<Relationship Id="rIdImg" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="{remoteImage}" TargetMode="External"/>""";
        return Zip(
            ("[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>""", false),
            ("_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""", false),
            ("word/_rels/document.xml.rels", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">{relationships}</Relationships>""", false),
            ("word/document.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><w:body><w:p><w:r><w:t>{text}</w:t></w:r></w:p>{image}</w:body></w:document>""", false));
    }

    /// <summary>ODT com uma macro Basic ligada ao evento de abertura, que grava <paramref name="marker"/>.</summary>
    private static byte[] OdtWithOpenMacro(string marker) =>
        Zip(
            ("mimetype", "application/vnd.oasis.opendocument.text", true),
            ("META-INF/manifest.xml", """<?xml version="1.0" encoding="UTF-8"?><manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.3"><manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.text"/><manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="Basic/script-lc.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="Basic/Standard/script-lb.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="Basic/Standard/Module1.xml" manifest:media-type="text/xml"/></manifest:manifest>""", false),
            ("content.xml", """<?xml version="1.0" encoding="UTF-8"?><office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:script="urn:oasis:names:tc:opendocument:xmlns:script:1.0" xmlns:xlink="http://www.w3.org/1999/xlink" office:version="1.3"><office:scripts><office:event-listeners><script:event-listener script:language="ooo:script" script:event-name="dom:load" xlink:href="vnd.sun.star.script:Standard.Module1.Main?language=Basic&amp;location=document" xlink:type="simple"/></office:event-listeners></office:scripts><office:body><office:text><text:p>Documento com macro ao abrir.</text:p></office:text></office:body></office:document-content>""", false),
            ("Basic/script-lc.xml", """<?xml version="1.0" encoding="UTF-8"?><library:libraries xmlns:library="http://openoffice.org/2000/library" xmlns:xlink="http://www.w3.org/1999/xlink"><library:library library:name="Standard" xlink:href="Basic/Standard/script-lb.xml/" xlink:type="simple" library:link="false"/></library:libraries>""", false),
            ("Basic/Standard/script-lb.xml", """<?xml version="1.0" encoding="UTF-8"?><library:library xmlns:library="http://openoffice.org/2000/library" library:name="Standard" library:readonly="false" library:passwordprotected="false"><library:element library:name="Module1"/></library:library>""", false),
            ("Basic/Standard/Module1.xml", $"""<?xml version="1.0" encoding="UTF-8"?><script:module xmlns:script="http://openoffice.org/2000/script" script:name="Module1" script:language="StarBasic">Sub Main&#10;Open "{marker}" For Output As #1&#10;Print #1, "rodou"&#10;Close #1&#10;End Sub</script:module>""", false));

    private static byte[] Zip(params (string Name, string Content, bool Stored)[] entries)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content, stored) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name, stored ? CompressionLevel.NoCompression : CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return memory.ToArray();
    }
}
