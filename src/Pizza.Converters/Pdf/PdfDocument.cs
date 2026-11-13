using System.Runtime.InteropServices;

namespace Pizza.Converters.Pdf;

/// <summary>
/// Um PDF aberto pela QPDF, todo em memória (ADR-033). O arquivo de entrada não é confiável: só roda no
/// Worker em AppContainer (SEC-20), e a saída é sempre regravada por inteiro e sem conteúdo ativo (SEC-76).
/// </summary>
internal sealed unsafe class PdfDocument : IDisposable
{
    /// <summary>SEC-44: a origem e a saída ficam na memória do Worker, que tem 1,5 GB; acima disto, LIMIT_EXCEEDED.</summary>
    public const long MaxSourceBytes = 512L * 1024 * 1024;

    public const int MaxPages = 2000; // SEC-42

    // SEC-76: ações que executam código, abrem programas, enviam dados ou tocam mídia embutida.
    private static readonly string[] DangerousActions =
        ["/JavaScript", "/Launch", "/SubmitForm", "/ImportData", "/GoToE", "/RichMediaExecute", "/Rendition", "/Movie", "/Sound"];

    private static readonly string[] DangerousAnnotations =
        ["/RichMedia", "/FileAttachment", "/Screen", "/Movie", "/Sound", "/3D"];

    // Chaves que só carregam código, gatilhos ou arquivos embutidos. Sem elas, o resto do documento continua igual.
    private static readonly string[] ActiveKeys =
        ["/AA", "/OpenAction", "/JS", "/EF", "/XFA", "/JavaScript", "/EmbeddedFiles"];

    private static readonly string[] ActionPayload = ["/F", "/Win", "/Mac", "/Unix", "/Fields", "/Next"];

    // Mapas de recursos: as chaves são apelidos escolhidos pelo autor ("/AA 12 Tf"), não chaves de conteúdo ativo.
    // Eles não são limpos; os objetos para onde apontam, sim.
    private static readonly string[] ResourceMaps =
        ["/Font", "/XObject", "/ExtGState", "/ColorSpace", "/Pattern", "/Shading", "/Properties"];

    private readonly int _length;
    private nint _qpdf;
    private byte* _source;

    private PdfDocument(nint qpdf, byte* source, int length)
    {
        _qpdf = qpdf;
        _source = source;
        _length = length;
    }

    public int PageCount => Qpdf.PageCount(_qpdf);

    /// <summary>Abre o PDF ou devolve o código de erro da SPECS §10.4.</summary>
    public static string? TryOpen(Stream stream, out PdfDocument? document)
    {
        document = null;
        if (stream.Length > MaxSourceBytes)
        {
            return "LIMIT_EXCEEDED";
        }

        var length = (int)stream.Length;
        var buffer = (byte*)NativeMemory.Alloc((nuint)Math.Max(length, 1));
        stream.ReadExactly(new Span<byte>(buffer, length));

        var qpdf = Qpdf.Init();
        Qpdf.SilenceErrors(qpdf);
        Qpdf.SuppressWarnings(qpdf, 1);
        var opened = new PdfDocument(qpdf, buffer, length);

        // Senha vazia: abre o PDF que só tem senha de dono. Com senha de usuário, cai em PASSWORD (FR-24 vem depois).
        byte empty = 0;
        if ((Qpdf.ReadMemory(qpdf, "entrada", buffer, (ulong)length, &empty) & Qpdf.Errors) != 0)
        {
            var code = Qpdf.GetErrorCode(qpdf, Qpdf.GetError(qpdf));
            opened.Dispose();
            return code == Qpdf.ErrorPassword ? "PASSWORD" : "CORRUPTED";
        }

        // SEC-74 ainda sem o campo de senha: um PDF protegido não sai sem proteção nem com a proteção trocada.
        if (Qpdf.IsEncrypted(qpdf) != 0)
        {
            opened.Dispose();
            return "PASSWORD";
        }

        if (Qpdf.PageCount(qpdf) is <= 0 or > MaxPages)
        {
            var tooMany = Qpdf.PageCount(qpdf) > MaxPages;
            opened.Dispose();
            return tooMany ? "LIMIT_EXCEEDED" : "CORRUPTED";
        }

        document = opened;
        return null;
    }

    /// <summary>Gira todas as páginas no sentido horário, somando ao giro que cada página já tinha.</summary>
    public void Rotate(int degrees)
    {
        // A rotação pode vir herdada da árvore de páginas; trazida para cada página, a soma fica certa.
        Qpdf.PushInheritedAttributesToPage(_qpdf);
        for (var index = 0; index < PageCount; index++)
        {
            var page = Qpdf.Page(_qpdf, (nuint)index);
            var current = Qpdf.HasKey(_qpdf, page, "/Rotate") != 0 && Qpdf.IsInteger(_qpdf, Qpdf.Key(_qpdf, page, "/Rotate")) != 0
                ? Qpdf.IntValue(_qpdf, Qpdf.Key(_qpdf, page, "/Rotate"))
                : 0;
            var normalized = (((current / 90 * 90) + degrees) % 360 + 360) % 360;
            Qpdf.ReplaceKey(_qpdf, page, "/Rotate", Qpdf.NewInteger(_qpdf, normalized));
        }

        Qpdf.ReleaseAll(_qpdf);
    }

    /// <summary>
    /// SEC-76: percorre tudo o que vai para a saída, a partir do trailer, e tira JavaScript, ações automáticas,
    /// programas, envio de formulário, anexos e mídia. Devolve quantos itens foram removidos.
    /// </summary>
    public int RemoveActiveContent()
    {
        var removed = 0;
        // O papel entra na chave: um objeto usado ao mesmo tempo como mapa de recursos e como /Names é limpo mesmo assim.
        var visited = new HashSet<(int, int, bool)>();
        var pending = new Stack<(uint Handle, bool NameMap)>();
        pending.Push((Qpdf.Trailer(_qpdf), false));
        while (pending.TryPop(out var item))
        {
            var handle = item.Handle;
            var id = Qpdf.ObjectId(_qpdf, handle);
            if (id != 0 && !visited.Add((id, Qpdf.Generation(_qpdf, handle), item.NameMap)))
            {
                continue;
            }

            if (Qpdf.IsStream(_qpdf, handle) != 0)
            {
                handle = Qpdf.StreamDictionary(_qpdf, handle);
            }

            if (Qpdf.IsArray(_qpdf, handle) != 0)
            {
                for (var index = 0; index < Qpdf.ArrayCount(_qpdf, handle); index++)
                {
                    pending.Push((Qpdf.ArrayItem(_qpdf, handle, index), false));
                }
            }
            else if (Qpdf.IsDictionary(_qpdf, handle) != 0)
            {
                if (!item.NameMap)
                {
                    removed += Clean(handle);
                }

                foreach (var key in Keys(handle))
                {
                    pending.Push((Qpdf.Key(_qpdf, handle, key), !item.NameMap && ResourceMaps.Contains(key)));
                }
            }
        }

        Qpdf.ReleaseAll(_qpdf);
        return removed;
    }

    /// <summary>Regrava o documento inteiro (nunca incremental) e copia para o destino.</summary>
    public string? Save(Stream destination) => WriteAll(_qpdf, destination.Write);

    /// <summary>
    /// Dividir: cada intervalo de páginas vira um PDF novo, regravado por inteiro, dentro do pacote de saídas
    /// (<see cref="Core.Output.OutputBundle"/>). Chame <see cref="RemoveActiveContent"/> antes: as páginas copiadas
    /// levam junto o que apontam.
    /// </summary>
    public string? SaveParts(Stream destination, IReadOnlyList<Range> parts, IProgress<int>? progress)
    {
        // Rotação, recursos e tamanho herdados da árvore de páginas iriam se perder na cópia.
        Qpdf.PushInheritedAttributesToPage(_qpdf);
        var bundle = new Core.Output.OutputBundle.Writer(destination, parts.Count);
        for (var index = 0; index < parts.Count; index++)
        {
            var part = Qpdf.Init();
            try
            {
                Qpdf.SilenceErrors(part);
                Qpdf.SuppressWarnings(part, 1);
                if ((Qpdf.EmptyPdf(part) & Qpdf.Errors) != 0)
                {
                    return "CORRUPTED";
                }

                var (offset, count) = parts[index].GetOffsetAndLength(PageCount);
                for (var page = offset; page < offset + count; page++)
                {
                    if ((Qpdf.AddPage(part, _qpdf, Qpdf.Page(_qpdf, (nuint)page), 0) & Qpdf.Errors) != 0)
                    {
                        return "CORRUPTED";
                    }
                }

                if (WriteAll(part, bundle.Add) is { } error)
                {
                    return error;
                }
            }
            finally
            {
                Qpdf.Cleanup(ref part);
                Qpdf.ReleaseAll(_qpdf);
            }

            progress?.Report(30 + (69 * (index + 1) / parts.Count));
        }

        bundle.Finish();
        return null;
    }

    /// <summary>
    /// Juntar: as páginas de todos os documentos, na ordem dada, num PDF novo regravado por inteiro. Chame
    /// <see cref="RemoveActiveContent"/> em cada um antes.
    /// </summary>
    public static string? Merge(IReadOnlyList<PdfDocument> documents, Stream destination)
    {
        var merged = Qpdf.Init();
        try
        {
            Qpdf.SilenceErrors(merged);
            Qpdf.SuppressWarnings(merged, 1);
            if ((Qpdf.EmptyPdf(merged) & Qpdf.Errors) != 0)
            {
                return "CORRUPTED";
            }

            foreach (var document in documents)
            {
                // Rotação, recursos e tamanho herdados da árvore de páginas iriam se perder na cópia.
                Qpdf.PushInheritedAttributesToPage(document._qpdf);
                for (var page = 0; page < document.PageCount; page++)
                {
                    if ((Qpdf.AddPage(merged, document._qpdf, Qpdf.Page(document._qpdf, (nuint)page), 0) & Qpdf.Errors) != 0)
                    {
                        return "CORRUPTED";
                    }
                }

                Qpdf.ReleaseAll(document._qpdf);
            }

            return WriteAll(merged, destination.Write);
        }
        finally
        {
            Qpdf.Cleanup(ref merged);
        }
    }

    private delegate void Sink(ReadOnlySpan<byte> data);

    private static string? WriteAll(nint qpdf, Sink destination)
    {
        if ((Qpdf.InitWriteMemory(qpdf) & Qpdf.Errors) != 0)
        {
            return "CORRUPTED";
        }

        Qpdf.PreserveEncryption(qpdf, 0);
        Qpdf.SuppressOriginalObjectIds(qpdf, 1);
        Qpdf.DeterministicId(qpdf, 1);
        Qpdf.SetObjectStreamMode(qpdf, Qpdf.ObjectStreamsGenerate);
        if ((Qpdf.Write(qpdf) & Qpdf.Errors) != 0)
        {
            return "CORRUPTED";
        }

        // Uma parte cabe no limite de memória (MaxSourceBytes), bem abaixo de 2 GB.
        destination(new ReadOnlySpan<byte>(Qpdf.Buffer(qpdf), checked((int)Qpdf.BufferLength(qpdf))));
        return null;
    }

    public void Dispose()
    {
        if (_qpdf != 0)
        {
            Qpdf.Cleanup(ref _qpdf);
        }

        if (_source is not null)
        {
            // O PDF de origem não fica na memória depois do job.
            new Span<byte>(_source, _length).Clear();
            NativeMemory.Free(_source);
            _source = null;
        }
    }

    private int Clean(uint dictionary)
    {
        var removed = 0;
        foreach (var key in ActiveKeys)
        {
            removed += Remove(dictionary, key);
        }

        if (IsDangerousAction(dictionary))
        {
            foreach (var key in ActionPayload)
            {
                removed += Remove(dictionary, key);
            }
        }

        if (Qpdf.HasKey(_qpdf, dictionary, "/A") != 0 && IsDangerousAction(Qpdf.Key(_qpdf, dictionary, "/A")))
        {
            removed += Remove(dictionary, "/A");
        }

        if (Qpdf.HasKey(_qpdf, dictionary, "/Annots") != 0 && Qpdf.IsArray(_qpdf, Qpdf.Key(_qpdf, dictionary, "/Annots")) != 0)
        {
            removed += KeepSafeAnnotations(dictionary);
        }

        return removed;
    }

    private int KeepSafeAnnotations(uint page)
    {
        var annotations = Qpdf.Key(_qpdf, page, "/Annots");
        var kept = Qpdf.NewArray(_qpdf);
        var removed = 0;
        for (var index = 0; index < Qpdf.ArrayCount(_qpdf, annotations); index++)
        {
            var annotation = Qpdf.ArrayItem(_qpdf, annotations, index);
            var subtype = Qpdf.IsDictionary(_qpdf, annotation) != 0 ? Qpdf.Key(_qpdf, annotation, "/Subtype") : 0;
            if (subtype != 0 && DangerousAnnotations.Any(name => Qpdf.IsName(_qpdf, subtype, name) != 0))
            {
                removed++;
                continue;
            }

            Qpdf.Append(_qpdf, kept, annotation);
        }

        if (removed > 0)
        {
            Qpdf.ReplaceKey(_qpdf, page, "/Annots", kept);
        }

        return removed;
    }

    private bool IsDangerousAction(uint handle)
    {
        if (Qpdf.IsDictionary(_qpdf, handle) == 0 || Qpdf.HasKey(_qpdf, handle, "/S") == 0)
        {
            return false;
        }

        var type = Qpdf.Key(_qpdf, handle, "/S");
        return DangerousActions.Any(name => Qpdf.IsName(_qpdf, type, name) != 0);
    }

    private int Remove(uint dictionary, string key)
    {
        if (Qpdf.HasKey(_qpdf, dictionary, key) == 0)
        {
            return 0;
        }

        Qpdf.RemoveKey(_qpdf, dictionary, key);
        return 1;
    }

    private List<string> Keys(uint dictionary)
    {
        var keys = new List<string>();
        Qpdf.BeginKeys(_qpdf, dictionary);
        while (Qpdf.MoreKeys(_qpdf) != 0)
        {
            keys.Add(Marshal.PtrToStringUTF8(Qpdf.NextKey(_qpdf))!);
        }

        return keys;
    }
}
