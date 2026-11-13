using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Pizza.Converters.Pdf;

namespace Pizza.Converters.Tests;

/// <summary>Monta PDFs pequenos de teste, objeto por objeto, com a tabela xref certa.</summary>
internal static partial class TestPdf
{
    /// <summary>Os objetos começam em 1; o 1 é o catálogo.</summary>
    public static byte[] Build(params string[] objects)
    {
        var text = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.Latin1.GetByteCount(text.ToString()));
            text.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var start = Encoding.Latin1.GetByteCount(text.ToString());
        text.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            text.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        text.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(text.ToString());
    }

    /// <summary>Páginas simples, todas com o mesmo conteúdo.</summary>
    public static byte[] Pages(int count)
    {
        var kids = string.Join(" ", Enumerable.Range(0, count).Select(i => $"{i + 4} 0 R"));
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{kids}] /Count {count} >>",
            "<< /Length 0 >>\nstream\n\nendstream",
        };
        objects.AddRange(Enumerable.Range(0, count).Select(_ => "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 3 0 R >>"));
        return Build([.. objects]);
    }

    /// <summary>Protege um PDF com senha de usuário, em AES-256, usando a própria QPDF.</summary>
    public static unsafe byte[] Encrypt(byte[] pdf, string password)
    {
        // A QPDF lê do buffer até o fim, não só na abertura: ele fica fixado até o Cleanup.
        var pinned = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        var qpdf = Qpdf.Init();
        try
        {
            byte empty = 0;
            Assert.Equal(0, Qpdf.ReadMemory(qpdf, "teste", (byte*)pinned.AddrOfPinnedObject(), (ulong)pdf.Length, &empty) & Qpdf.Errors);

            Assert.Equal(0, Qpdf.InitWriteMemory(qpdf) & Qpdf.Errors);
            Native.SetR6(qpdf, password, password + "-dono", 1, 1, 1, 1, 1, 1, 0, 1);
            Assert.Equal(0, Qpdf.Write(qpdf) & Qpdf.Errors);
            return new ReadOnlySpan<byte>(Qpdf.Buffer(qpdf), (int)Qpdf.BufferLength(qpdf)).ToArray();
        }
        finally
        {
            Qpdf.Cleanup(ref qpdf);
            pinned.Free();
        }
    }

    internal static partial class Native
    {
        [LibraryImport("qpdf30", EntryPoint = "qpdf_set_r6_encryption_parameters2", StringMarshalling = StringMarshalling.Utf8)]
        public static partial void SetR6(nint qpdf, string user, string owner, int accessibility, int extract, int assemble, int annotate, int fill, int modify, int print, int metadata);

        [LibraryImport("qpdf30", EntryPoint = "qpdf_check_pdf")]
        public static partial int Check(nint qpdf);

        [LibraryImport("qpdf30", EntryPoint = "qpdf_oh_unparse_resolved")]
        public static partial nint UnparseResolved(nint qpdf, uint handle);
    }
}

/// <summary>
/// TST-22: verificador independente da saída. Reabre o PDF com a QPDF, confere a estrutura (como
/// <c>qpdf --check</c>) e procura conteúdo ativo em tudo o que é alcançável a partir do trailer.
/// </summary>
internal sealed unsafe class PdfInspection
{
    private static readonly string[] Forbidden =
        ["/JavaScript", "/JS", "/OpenAction", "/AA", "/Launch", "/SubmitForm", "/EmbeddedFiles", "/EF", "/RichMedia", "/FileAttachment", "/XFA"];

    public required IReadOnlyList<int> Rotations { get; init; }

    public required string Everything { get; init; }

    public required bool Valid { get; init; }

    public IEnumerable<string> ActiveContent => Forbidden.Where(name => System.Text.RegularExpressions.Regex.IsMatch(Everything, Regex(name)));

    public static PdfInspection Of(byte[] pdf)
    {
        // A QPDF lê do buffer até o fim, não só na abertura: ele fica fixado até o Cleanup.
        var pinned = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        var qpdf = Qpdf.Init();
        try
        {
            Qpdf.SilenceErrors(qpdf);
            byte empty = 0;
            Assert.Equal(0, Qpdf.ReadMemory(qpdf, "saida", (byte*)pinned.AddrOfPinnedObject(), (ulong)pdf.Length, &empty) & Qpdf.Errors);

            var valid = (TestPdf.Native.Check(qpdf) & Qpdf.Errors) == 0;
            Qpdf.PushInheritedAttributesToPage(qpdf);
            var rotations = Enumerable.Range(0, Qpdf.PageCount(qpdf)).Select(index =>
            {
                var page = Qpdf.Page(qpdf, (nuint)index);
                return Qpdf.HasKey(qpdf, page, "/Rotate") != 0 ? (int)Qpdf.IntValue(qpdf, Qpdf.Key(qpdf, page, "/Rotate")) : 0;
            }).ToList();

            // A QPDF regrava numerando tudo de 1 a /Size - 1, geração 0: cada objeto da saída, por extenso.
            var size = (int)Qpdf.IntValue(qpdf, Qpdf.Key(qpdf, Qpdf.Trailer(qpdf), "/Size"));
            var everything = string.Join("\n", Enumerable.Range(1, size - 1)
                .Select(id => Marshal.PtrToStringUTF8(TestPdf.Native.UnparseResolved(qpdf, Qpdf.ObjectById(qpdf, id, 0)))));
            return new PdfInspection { Rotations = rotations, Everything = everything, Valid = valid };
        }
        finally
        {
            Qpdf.Cleanup(ref qpdf);
            pinned.Free();
        }
    }

    // Um nome PDF termina onde começa um delimitador; "/JS" não pode casar com "/JSX".
    private static string Regex(string name) => System.Text.RegularExpressions.Regex.Escape(name) + @"(?![A-Za-z0-9#._-])";
}
