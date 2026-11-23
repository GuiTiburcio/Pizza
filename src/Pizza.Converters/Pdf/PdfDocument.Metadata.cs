using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Pizza.Converters.Pdf;

internal sealed unsafe partial class PdfDocument
{
    // Metadados não justificam descompactação sem limite. O payload de página não passa por este leitor.
    private const int MaxMetadataBytes = 1024 * 1024;

    private string? PreserveTechnicalTitle()
    {
        var trailer = Qpdf.Trailer(_qpdf);
        var info = Qpdf.Key(_qpdf, trailer, "/Info");
        if (Qpdf.IsDictionary(_qpdf, info) != 0
            && Qpdf.IsString(_qpdf, Qpdf.Key(_qpdf, info, "/Title")) != 0)
        {
            return null; // O título explícito de /Info prevalece em caso de conflito.
        }

        var metadata = Qpdf.Key(_qpdf, Qpdf.Root(_qpdf), "/Metadata");
        if (Qpdf.IsStream(_qpdf, metadata) == 0)
        {
            return null;
        }

        var dictionary = Qpdf.StreamDictionary(_qpdf, metadata);
        var length = Qpdf.Key(_qpdf, dictionary, "/Length");
        if (Qpdf.IsInteger(_qpdf, length) == 0 || Qpdf.IntValue(_qpdf, length) is < 0 or > MaxMetadataBytes)
        {
            return "LIMIT_EXCEEDED";
        }

        var compressed = false;
        if (Qpdf.HasKey(_qpdf, dictionary, "/Filter") != 0)
        {
            var filter = Qpdf.Key(_qpdf, dictionary, "/Filter");
            if (Qpdf.IsArray(_qpdf, filter) != 0 && Qpdf.ArrayCount(_qpdf, filter) == 1)
            {
                filter = Qpdf.ArrayItem(_qpdf, filter, 0);
            }

            if (Qpdf.IsName(_qpdf, filter, "/FlateDecode") == 0 || Qpdf.HasKey(_qpdf, dictionary, "/DecodeParms") != 0)
            {
                return "UNSUPPORTED"; // Não perder silenciosamente um título que não foi possível ler.
            }

            compressed = true;
        }

        byte* data = null;
        nuint size = 0;
        try
        {
            // Obter só os bytes comprimidos: a expansão ocorre no .NET, com teto de caracteres e sem DTD.
            if ((Qpdf.StreamData(_qpdf, metadata, 0, null, &data, &size) & Qpdf.Errors) != 0)
            {
                return "CORRUPTED";
            }

            if (size > MaxMetadataBytes)
            {
                return "LIMIT_EXCEEDED";
            }

            using var raw = new MemoryStream(new ReadOnlySpan<byte>(data, (int)size).ToArray());
            using Stream decoded = compressed ? new ZLibStream(raw, CompressionMode.Decompress) : raw;
            var xmlBytes = new byte[MaxMetadataBytes + 1];
            var count = decoded.ReadAtLeast(xmlBytes, xmlBytes.Length, throwOnEndOfStream: false);
            if (count > MaxMetadataBytes)
            {
                return "LIMIT_EXCEEDED";
            }

            using var content = new MemoryStream(xmlBytes, 0, count, writable: false);
            using var reader = XmlReader.Create(content, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxMetadataBytes,
            });
            var xml = XDocument.Load(reader);
            XNamespace dc = "http://purl.org/dc/elements/1.1/";
            XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
            var titleElement = xml.Descendants(dc + "title").FirstOrDefault();
            var titles = titleElement?.Descendants(rdf + "li").ToList() ?? [];
            var title = (titles.FirstOrDefault(item => (string?)item.Attribute(XNamespace.Xml + "lang") == "x-default")
                ?? titles.FirstOrDefault())?.Value ?? titleElement?.Value
                ?? xml.Descendants().Attributes(dc + "title").FirstOrDefault()?.Value;
            if (title is not null)
            {
                if (Qpdf.IsDictionary(_qpdf, info) == 0)
                {
                    info = Qpdf.NewDictionary(_qpdf);
                    Qpdf.ReplaceKey(_qpdf, trailer, "/Info", info);
                }

                Qpdf.ReplaceKey(_qpdf, info, "/Title", Qpdf.NewUnicodeString(_qpdf, title));
            }

            return null;
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException or IOException)
        {
            return "CORRUPTED";
        }
        finally
        {
            Qpdf.FreeBuffer(&data);
        }
    }

    private void ClearMetadataStream(uint stream)
    {
        if (Qpdf.IsStream(_qpdf, stream) == 0)
        {
            return;
        }

        // Uma segunda referência não pode ressuscitar os bytes privados depois de remover /Metadata.
        Qpdf.ReplaceStreamData(_qpdf, stream, null, 0, Qpdf.NewNull(_qpdf), Qpdf.NewNull(_qpdf));
        var dictionary = Qpdf.StreamDictionary(_qpdf, stream);
        foreach (var key in Keys(dictionary))
        {
            if (key != "/Length")
            {
                Qpdf.RemoveKey(_qpdf, dictionary, key);
            }
        }
    }
}
