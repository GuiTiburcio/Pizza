using System.IO.Compression;
using System.Xml;

namespace Pizza.Converters.Docs;

/// <summary>
/// Leitura de um pacote DOCX ou XLSX (OPC) só com o .NET, no Worker (ADR-034, Pizza Brotinho). O zip e o XML
/// vêm de arquivo não confiável: XML sem DTD (sem entidades externas nem expansão em cascata) e um teto para
/// os bytes descompactados de cada parte, contados de verdade e não pelo tamanho que o zip declara (SEC-40, TST-08).
/// </summary>
internal sealed class OfficePackage : IDisposable
{
    /// <summary>SEC-40: nenhuma parte descompactada, nem a saída, passa do maior arquivo de origem aceito.</summary>
    public const long MaxPartBytes = 2L * 1024 * 1024 * 1024;

    private static readonly XmlReaderSettings Xml = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        CloseInput = true,
    };

    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly long _maxPartBytes;

    private OfficePackage(Stream source, long maxPartBytes)
    {
        _zip = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        _maxPartBytes = maxPartBytes;
        foreach (var entry in _zip.Entries)
        {
            // Nomes de parte do OPC não diferenciam maiúsculas; o primeiro com o nome vale.
            _entries.TryAdd(entry.FullName.Replace('\\', '/'), entry);
        }
    }

    public void Dispose() => _zip.Dispose();

    /// <summary>
    /// Roda a conversão e traduz as falhas do arquivo para os códigos da SPECS §10.4: parte grande demais vira
    /// <c>LIMIT_EXCEEDED</c>; zip ou XML fora do formato, <c>CORRUPTED</c>.
    /// </summary>
    public static string? Convert(Stream source, long maxPartBytes, Func<OfficePackage, string?> convert)
    {
        try
        {
            using var package = new OfficePackage(source, maxPartBytes);
            return convert(package);
        }
        catch (PartTooLargeException)
        {
            return "LIMIT_EXCEEDED";
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or FormatException or OverflowException or NotSupportedException or EndOfStreamException)
        {
            return "CORRUPTED";
        }
    }

    /// <summary>A parte principal do pacote (documento ou pasta de trabalho), pelas relações da raiz.</summary>
    public string MainPart() =>
        Relationships("").Where(relation => relation.Type.EndsWith("/officeDocument", StringComparison.Ordinal)).Select(relation => relation.Target).FirstOrDefault()
        ?? throw new InvalidDataException("Pacote sem parte principal.");

    /// <summary>As relações internas de uma parte: Id, tipo e o nome da parte de destino já resolvido.</summary>
    public IReadOnlyList<(string Id, string Type, string Target)> Relationships(string part) =>
        AllRelationships(part).Where(relation => !relation.External).Select(relation => (relation.Id, relation.Type, relation.Target)).ToList();

    /// <summary>
    /// As relações externas de uma parte (links e imagens vinculadas), pelo Id, com o destino como está no
    /// arquivo. O Pizza nunca busca nada delas (SEC-50); quem decide o que vira link é o conversor.
    /// </summary>
    public IReadOnlyDictionary<string, string> ExternalTargets(string part) =>
        AllRelationships(part).Where(relation => relation.External).DistinctBy(relation => relation.Id).ToDictionary(relation => relation.Id, relation => relation.Target);

    public bool Contains(string part) => _entries.ContainsKey(part);

    /// <summary>Os bytes de uma parte (uma imagem, por exemplo), com o mesmo teto do XML.</summary>
    public byte[] ReadBytes(string part)
    {
        if (!_entries.TryGetValue(part, out var entry))
        {
            throw new InvalidDataException("Parte ausente.");
        }

        using var stream = new CappedStream(entry.Open(), _maxPartBytes);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private List<(string Id, string Type, string Target, bool External)> AllRelationships(string part)
    {
        var slash = part.LastIndexOf('/');
        var folder = slash < 0 ? "" : part[..(slash + 1)];
        var name = folder + "_rels/" + part[(slash + 1)..] + ".rels";
        var list = new List<(string, string, string, bool)>();
        if (!_entries.ContainsKey(name))
        {
            return list;
        }

        using var reader = Open(name);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship"
                && reader.GetAttribute("Id") is { } id && reader.GetAttribute("Type") is { } type && reader.GetAttribute("Target") is { } target)
            {
                if (reader.GetAttribute("TargetMode") == "External")
                {
                    list.Add((id, type, target, true));
                }
                else if (Resolve(folder, target) is { } resolved)
                {
                    list.Add((id, type, resolved, false));
                }
            }
        }

        return list;
    }

    /// <summary>Abre uma parte para leitura em fluxo, já posicionada no elemento raiz.</summary>
    public XmlReader Open(string part)
    {
        if (!_entries.TryGetValue(part, out var entry))
        {
            throw new InvalidDataException("Parte ausente.");
        }

        var reader = XmlReader.Create(new CappedStream(entry.Open(), _maxPartBytes), Xml);
        reader.MoveToContent();
        return reader;
    }

    /// <summary>Destino relativo à pasta da parte, ou absoluto a partir da raiz; nunca acima da raiz.</summary>
    private static string? Resolve(string folder, string target)
    {
        var segments = new List<string>();
        foreach (var segment in ((target.StartsWith('/') ? "" : folder) + target).Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
            }
            else if (segment is not ("" or "."))
            {
                segments.Add(segment);
            }
        }

        return string.Join('/', segments);
    }
}

/// <summary>A parte ou a saída passou do teto (SEC-40).</summary>
internal sealed class PartTooLargeException : Exception
{
}

/// <summary>Conta os bytes lidos ou escritos e para ao passar do teto.</summary>
internal sealed class CappedStream(Stream inner, long cap) : Stream
{
    private long _count;

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var read = inner.Read(buffer);
        Count(read);
        return read;
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Count(buffer.Length);
        inner.Write(buffer);
    }

    public override void Flush() => inner.Flush();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        // Na escrita o destino é do chamador; na leitura a parte do zip fecha junto.
        if (disposing && !inner.CanWrite)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Count(int bytes)
    {
        _count += bytes;
        if (_count > cap)
        {
            throw new PartTooLargeException();
        }
    }
}
