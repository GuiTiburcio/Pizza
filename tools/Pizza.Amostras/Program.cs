using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using NetVips;

// Amostras sintéticas da matriz manual de lançamento (docs/4-entrega/MATRIZ-LANCAMENTO.md).
// Todo conteúdo é inventado aqui: nenhum documento pessoal, nenhuma amostra de terceiros, nenhuma rede.
// Uso: dotnet run --project tools/Pizza.Amostras -- <pasta-de-saída>
if (args is not [var output])
{
    Console.Error.WriteLine("Uso: Pizza.Amostras <pasta-de-saída>");
    return 2;
}

Directory.CreateDirectory(output);
if (Directory.EnumerateFileSystemEntries(output).Any())
{
    Console.Error.WriteLine("A pasta de saída precisa estar vazia: as amostras de uma rodada não se misturam com outra.");
    return 3;
}

string Out(string name) => Path.Combine(output, name);

// ---------- Imagem ----------
var photo = Gradient(400, 200);
photo = photo.Mutate(m =>
{
    m.DrawRect([255, 255, 255], 0, 0, 400, 24, fill: true); // faixa branca = topo
    m.DrawRect([220, 0, 0], 0, 0, 40, 40, fill: true);      // quadrado vermelho = canto superior esquerdo
});
var jpeg = photo.JpegsaveBuffer(q: 90, keep: Enums.ForeignKeep.None);
File.WriteAllBytes(Out("foto-orientacao-6-com-gps.jpg"), WithExif(jpeg, PersonalExif(orientation: 6)));

var xyz = Image.Xyz(320, 160);
var alpha = (xyz[0] * (255.0 / 320)).Cast(Enums.BandFormat.Uchar);
var transparent = Gradient(320, 160).Bandjoin(alpha);
File.WriteAllBytes(Out("transparencia-com-autor.png"), WithPngText(transparent.PngsaveBuffer(keep: Enums.ForeignKeep.None), "Author", "Autor Ficticio da Amostra"));

var frames = Enumerable.Range(0, 3).Select(index => Image.Black(120, 80).NewFromImage([index * 100, 80, 255 - (index * 100)])
    .Mutate(m => m.DrawRect([255, 255, 255], 10 + (index * 35), 30, 20, 20, fill: true))).ToArray();
var strip = Image.Arrayjoin(frames, across: 1).Mutate(m =>
{
    m.Set(GValue.GIntType, "page-height", 80);
    m.Set(GValue.ArrayIntType, "delay", new[] { 500, 500, 500 });
    m.Set(GValue.GIntType, "loop", 0);
});
File.WriteAllBytes(Out("animado-3-quadros.gif"), strip.GifsaveBuffer(keep: Enums.ForeignKeep.None));
File.WriteAllBytes(Out("animado-3-quadros.webp"), strip.WebpsaveBuffer(lossless: true, keep: Enums.ForeignKeep.None));
File.WriteAllBytes(Out("scanner-300dpi.tiff"), Gradient(600, 400).Copy(xres: 300 / 25.4, yres: 300 / 25.4).TiffsaveBuffer(keep: Enums.ForeignKeep.None));

// Colisão de nome: converter colisao.png em JPG não pode sobrescrever o colisao.jpg que já existe.
File.WriteAllBytes(Out("colisao.png"), Gradient(64, 64).PngsaveBuffer(keep: Enums.ForeignKeep.None));
File.WriteAllBytes(Out("colisao.jpg"), Image.Black(64, 64).JpegsaveBuffer(keep: Enums.ForeignKeep.None));

// ---------- PDF ----------
File.WriteAllBytes(Out("paginas-1-a-5.pdf"), Pdf(
    [("Pagina 1 de 5", 595, 842, 0), ("Pagina 2 de 5 (ja girada 90)", 595, 842, 90), ("Pagina 3 de 5 (paisagem)", 842, 595, 0), ("Pagina 4 de 5", 595, 842, 0), ("Pagina 5 de 5", 595, 842, 0)],
    author: null, javascript: false));
File.WriteAllBytes(Out("paginas-A-e-B.pdf"), Pdf([("Pagina A", 595, 842, 0), ("Pagina B", 595, 842, 0)], author: null, javascript: false));
File.WriteAllBytes(Out("com-javascript-e-autor.pdf"), Pdf([("PDF com JavaScript ao abrir e autor", 595, 842, 0)], author: "Autor Ficticio da Amostra", javascript: true));

// ---------- Documento ----------
var png = File.ReadAllBytes(Out("transparencia-com-autor.png"));
var foto = File.ReadAllBytes(Out("foto-orientacao-6-com-gps.jpg"));
File.WriteAllBytes(Out("documento-completo.docx"), Docx(png, foto));
var internet = Out("documento-da-internet.docx");
File.WriteAllBytes(internet, Docx(png, foto));
File.WriteAllText(internet + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n"); // Mark of the Web (SEC-65)
File.WriteAllBytes(Out("planilha-formulas.xlsx"), Xlsx());

// Jumbo: entradas que só o motor de documentos lê.
File.WriteAllText(Out("texto-acentuado.rtf"), @"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0 Arial;}}\f0\fs28 Convers\'e3o com acentua\'e7\'e3o: \'e1 \'e9 \'ed \'f3 \'fa \'e2 \'ea \'f4 \'e3 \'f5 \'e7.\par Segunda linha.\par}", Encoding.ASCII);
File.WriteAllBytes(Out("macro-ao-abrir.odt"), OdtWithOpenMacro(Out("macro-rodou.txt")));

// ---------- Áudio ----------
Wav(Out("tom-16bit-estereo-44k-silencio.wav"), 2, 44100, 16, 0, 3.0, (channel, t) =>
    t < 0.5 || t >= 2.5 ? 0 : 0.891 * Math.Sin(2 * Math.PI * (channel == 0 ? 440 : 880) * t)); // pico −1 dBFS, silêncio digital nas pontas
Wav(Out("tom-24bit-mono-48k.wav"), 1, 48000, 24, 0, 2.0, (_, t) => 0.5 * Math.Sin(2 * Math.PI * 1000 * t));
double[] surround = [300, 400, 500, 60, 600, 700];
Wav(Out("canais-5.1-16bit-48k.wav"), 6, 48000, 16, 0x3F, 2.0, (channel, t) => 0.4 * Math.Sin(2 * Math.PI * surround[channel] * t));
Wav(Out("curto-1000-quadros-16bit.wav"), 2, 44100, 16, 0, 1000 / 44100.0, (_, t) => 0.3 * Math.Sin(2 * Math.PI * 440 * t));

// ---------- Comum ----------
var readOnly = Out("somente-leitura.png");
File.Copy(Out("colisao.png"), readOnly);
File.SetAttributes(readOnly, FileAttributes.ReadOnly);

// TST-10: hash de cada original; tools/gerar-amostras.ps1 -Conferir compara depois dos testes.
var hashes = new StringBuilder();
foreach (var file in Directory.GetFiles(output).Order(StringComparer.Ordinal))
{
    using var stream = File.OpenRead(file);
    hashes.Append(CultureInvariant($"{Convert.ToHexString(SHA256.HashData(stream))}  {Path.GetFileName(file)}\n"));
}

File.WriteAllText(Out("hashes-originais.sha256"), hashes.ToString(), new UTF8Encoding(false));
Console.WriteLine($"{Directory.GetFiles(output).Length} arquivos gerados.");
return 0;

static string CultureInvariant(FormattableString text) => FormattableString.Invariant(text);

static Image Gradient(int width, int height)
{
    var xyz = Image.Xyz(width, height);
    var red = xyz[0] * (255.0 / width);
    var green = xyz[1] * (255.0 / height);
    return red.Bandjoin(green, Image.Black(width, height) + 128).Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
}

// EXIF big-endian com orientação, fabricante, autor e GPS (Cuiabá aproximada, inventada). Tudo menos a orientação
// deve sumir da saída (PRIV-30); a orientação deve ser aplicada ou preservada (PRIV-31).
static byte[] PersonalExif(ushort orientation)
{
    static byte[] Ascii(string text) => [.. Encoding.ASCII.GetBytes(text), 0];
    static byte[] Rationals(params uint[] values)
    {
        var bytes = new byte[values.Length * 4];
        for (var index = 0; index < values.Length; index++) BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(index * 4), values[index]);
        return bytes;
    }

    var orientationValue = new byte[2];
    BinaryPrimitives.WriteUInt16BigEndian(orientationValue, orientation);
    List<(ushort Tag, ushort Type, uint Count, byte[] Value)> ifd0 =
    [
        (0x010F, 2, 14, Ascii("Pizza Amostra")),
        (0x0112, 3, 1, orientationValue),
        (0x013B, 2, 26, Ascii("Autor Ficticio da Amostra")),
        (0x8825, 4, 1, new byte[4]), // ponteiro para o GPS, preenchido abaixo
    ];
    List<(ushort Tag, ushort Type, uint Count, byte[] Value)> gps =
    [
        (0x0000, 1, 4, [2, 3, 0, 0]),
        (0x0001, 2, 2, Ascii("S")),
        (0x0002, 5, 3, Rationals(15, 1, 35, 1, 0, 1)),
        (0x0003, 2, 2, Ascii("W")),
        (0x0004, 5, 3, Rationals(56, 1, 5, 1, 0, 1)),
    ];

    var gpsOffset = 8 + 2 + (ifd0.Count * 12) + 4;
    var dataOffset = gpsOffset + 2 + (gps.Count * 12) + 4;
    BinaryPrimitives.WriteUInt32BigEndian(ifd0[3].Value, (uint)gpsOffset);
    var data = new List<byte>();
    var tiff = new List<byte>([.. "MM"u8, 0, 42, 0, 0, 0, 8]);
    void Directory(List<(ushort Tag, ushort Type, uint Count, byte[] Value)> entries)
    {
        Span<byte> entry = stackalloc byte[12];
        tiff.AddRange([(byte)(entries.Count >> 8), (byte)entries.Count]);
        foreach (var (tag, type, count, value) in entries)
        {
            entry.Clear();
            BinaryPrimitives.WriteUInt16BigEndian(entry, tag);
            BinaryPrimitives.WriteUInt16BigEndian(entry[2..], type);
            BinaryPrimitives.WriteUInt32BigEndian(entry[4..], count);
            if (value.Length <= 4)
            {
                value.CopyTo(entry[8..]);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(entry[8..], (uint)(dataOffset + data.Count));
                data.AddRange(value);
                if ((data.Count & 1) != 0) data.Add(0);
            }

            tiff.AddRange(entry.ToArray());
        }

        tiff.AddRange([0, 0, 0, 0]);
    }

    Directory(ifd0);
    Directory(gps);
    return [.. tiff, .. data];
}

static byte[] WithExif(byte[] jpeg, byte[] tiff)
{
    var payload = (byte[])[.. "Exif\0\0"u8, .. tiff];
    var segment = new byte[4 + payload.Length];
    segment[0] = 0xFF;
    segment[1] = 0xE1;
    BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(payload.Length + 2));
    payload.CopyTo(segment, 4);
    var afterJfif = jpeg.AsSpan(6, 5).SequenceEqual("JFIF\0"u8) ? 4 + BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(4)) : 2;
    return [.. jpeg[..afterJfif], .. segment, .. jpeg[afterJfif..]];
}

static byte[] WithPngText(byte[] png, string key, string value)
{
    var body = (byte[])[.. "tEXt"u8, .. Encoding.Latin1.GetBytes(key), 0, .. Encoding.Latin1.GetBytes(value)];
    var chunk = new byte[body.Length + 8];
    BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)(body.Length - 4));
    body.CopyTo(chunk, 4);
    BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(chunk.Length - 4), Crc32(body));
    return [.. png[..33], .. chunk, .. png[33..]]; // logo depois do IHDR
}

static uint Crc32(ReadOnlySpan<byte> bytes)
{
    var crc = uint.MaxValue;
    foreach (var value in bytes)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xEDB88320u);
    }

    return ~crc;
}

static byte[] Pdf((string Text, int Width, int Height, int Rotate)[] pages, string? author, bool javascript)
{
    var objects = new List<string>();
    var kids = string.Join(" ", pages.Select((_, index) => $"{4 + (index * 2)} 0 R"));
    var info = author is null ? "" : $" /Info {4 + (pages.Length * 2)} 0 R";
    objects.Add("<< /Type /Catalog /Pages 2 0 R" + (javascript ? " /OpenAction << /S /JavaScript /JS (app.alert\\('amostra do Pizza'\\);) >>" : "") + " >>");
    objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pages.Length} >>");
    objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
    foreach (var (text, width, height, rotate) in pages)
    {
        var index = objects.Count + 1;
        var content = $"BT /F1 36 Tf 60 {height - 120} Td ({text}) Tj ET 0.8 0 0 rg 40 40 80 80 re f";
        objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Rotate {rotate} /Resources << /Font << /F1 3 0 R >> >> /Contents {index + 1} 0 R >>");
        objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
    }

    if (author is not null) objects.Add($"<< /Author ({author}) /Creator (Pizza.Amostras) >>");
    var pdf = new StringBuilder("%PDF-1.7\n");
    var offsets = new List<int>();
    for (var index = 0; index < objects.Count; index++)
    {
        offsets.Add(pdf.Length);
        pdf.Append(CultureInvariant($"{index + 1} 0 obj\n{objects[index]}\nendobj\n"));
    }

    var xref = pdf.Length;
    pdf.Append(CultureInvariant($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
    foreach (var offset in offsets) pdf.Append(CultureInvariant($"{offset:D10} 00000 n \n"));
    pdf.Append(CultureInvariant($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R{info} >>\nstartxref\n{xref}\n%%EOF\n"));
    return Encoding.Latin1.GetBytes(pdf.ToString());
}

static byte[] Zip(params (string Name, byte[] Content, bool Stored)[] entries)
{
    using var memory = new MemoryStream();
    using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
    {
        foreach (var (name, content, stored) in entries)
        {
            using var entry = zip.CreateEntry(name, stored ? CompressionLevel.NoCompression : CompressionLevel.Optimal).Open();
            entry.Write(content);
        }
    }

    return memory.ToArray();
}

static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

static byte[] Docx(byte[] png, byte[] jpeg)
{
    const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    const string Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    static string Picture(string attribute, string id, int number) => $"""
        <w:r><w:drawing><wp:inline><wp:extent cx="1905000" cy="952500"/><wp:docPr id="{number}" name="imagem {number}" descr="Imagem {number} da amostra"/>
        <a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic>
        <pic:nvPicPr><pic:cNvPr id="{number}" name="imagem {number}"/><pic:cNvPicPr/></pic:nvPicPr>
        <pic:blipFill><a:blip {attribute}="{id}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>
        <pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="1905000" cy="952500"/></a:xfrm><a:prstGeom prst="rect"/></pic:spPr>
        </pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>
        """;
    static string Item(int level, int numbering, string text) =>
        $"<w:p><w:pPr><w:numPr><w:ilvl w:val=\"{level}\"/><w:numId w:val=\"{numbering}\"/></w:numPr></w:pPr><w:r><w:t>{text}</w:t></w:r></w:p>";
    static string Cell(string text) => $"<w:tc><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:tc>";

    var body = string.Concat(
        "<w:p><w:pPr><w:pStyle w:val=\"Heading1\"/></w:pPr><w:r><w:t>Relatório de amostra do Pizza</w:t></w:r></w:p>",
        "<w:p><w:r><w:t xml:space=\"preserve\">Acentos: ação, coração, pão, açúcar, égua, ônibus, über. Símbolos: &lt;tag&gt; &amp; \"aspas\" 50%.</w:t></w:r></w:p>",
        "<w:p><w:r><w:rPr><w:b/></w:rPr><w:t xml:space=\"preserve\">Negrito, </w:t></w:r><w:r><w:rPr><w:i/></w:rPr><w:t>itálico</w:t></w:r></w:p>",
        Item(0, 1, "Item com marcador"), Item(1, 1, "Subitem aninhado"), Item(0, 1, "Segundo item"),
        Item(0, 2, "Primeiro numerado"), Item(0, 2, "Segundo numerado"),
        "<w:tbl><w:tblPr><w:tblBorders><w:top w:val=\"single\"/><w:bottom w:val=\"single\"/><w:insideH w:val=\"single\"/><w:insideV w:val=\"single\"/></w:tblBorders></w:tblPr>",
        "<w:tr>", Cell("Produto"), Cell("Quantidade"), Cell("Preço"), "</w:tr>",
        "<w:tr>", Cell("Pizza média"), Cell("2"), Cell("R$ 89,90"), "</w:tr>",
        "<w:tr>", Cell("Refrigerante"), Cell("3"), Cell("R$ 7,50"), "</w:tr></w:tbl>",
        "<w:p><w:r><w:t xml:space=\"preserve\">Link externo: </w:t></w:r><w:hyperlink r:id=\"rIdLink\"><w:r><w:t>exemplo.invalid</w:t></w:r></w:hyperlink></w:p>",
        "<w:p>", Picture("r:embed", "rIdPng", 1), "</w:p>",
        "<w:p>", Picture("r:embed", "rIdJpg", 2), "</w:p>",
        "<w:p>", Picture("r:link", "rIdRemota", 3), "</w:p>");

    return Zip(
        ("[Content_Types].xml", Utf8("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Default Extension="png" ContentType="image/png"/><Default Extension="jpg" ContentType="image/jpeg"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/><Override PartName="/word/numbering.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"/><Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/></Types>"""), false),
        ("_rels/.rels", Utf8($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{Rel}"><Relationship Id="rId1" Type="{R}/officeDocument" Target="word/document.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/></Relationships>"""), false),
        ("docProps/core.xml", Utf8("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>Relatório de amostra</dc:title><dc:creator>Autor Ficticio da Amostra</dc:creator><cp:lastModifiedBy>Autor Ficticio da Amostra</cp:lastModifiedBy></cp:coreProperties>"""), false),
        ("word/_rels/document.xml.rels", Utf8($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{Rel}"><Relationship Id="rIdStyles" Type="{R}/styles" Target="styles.xml"/><Relationship Id="rIdNumbering" Type="{R}/numbering" Target="numbering.xml"/><Relationship Id="rIdPng" Type="{R}/image" Target="media/imagem1.png"/><Relationship Id="rIdJpg" Type="{R}/image" Target="media/imagem2.jpg"/><Relationship Id="rIdRemota" Type="{R}/image" Target="http://exemplo.invalid/remota.png" TargetMode="External"/><Relationship Id="rIdLink" Type="{R}/hyperlink" Target="https://exemplo.invalid/pagina" TargetMode="External"/></Relationships>"""), false),
        ("word/styles.xml", Utf8($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:styles xmlns:w="{W}"><w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:pPr><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:b/><w:sz w:val="36"/></w:rPr></w:style></w:styles>"""), false),
        ("word/numbering.xml", Utf8($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:numbering xmlns:w="{W}"><w:abstractNum w:abstractNumId="0"><w:lvl w:ilvl="0"><w:numFmt w:val="bullet"/><w:lvlText w:val="•"/></w:lvl><w:lvl w:ilvl="1"><w:numFmt w:val="bullet"/><w:lvlText w:val="◦"/></w:lvl></w:abstractNum><w:abstractNum w:abstractNumId="1"><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%1."/></w:lvl></w:abstractNum><w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num><w:num w:numId="2"><w:abstractNumId w:val="1"/></w:num></w:numbering>"""), false),
        ("word/document.xml", Utf8($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="{W}" xmlns:r="{R}" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><w:body>{body}</w:body></w:document>"""), false),
        ("word/media/imagem1.png", png, true),
        ("word/media/imagem2.jpg", jpeg, true));
}

static byte[] Xlsx()
{
    const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static string Text(string cell, string text) => $"<c r=\"{cell}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{System.Security.SecurityElement.Escape(text)}</t></is></c>";
    var caixa = string.Concat(
        "<row r=\"1\">", Text("A1", "Item"), Text("B1", "Qtd"), Text("C1", "Preço"), Text("D1", "Total"), "</row>",
        "<row r=\"2\">", Text("A2", "Pizza; média, \"especial\""), "<c r=\"B2\"><v>2</v></c><c r=\"C2\"><v>89.9</v></c><c r=\"D2\"><f>B2*C2</f><v>179.8</v></c></row>",
        "<row r=\"3\">", Text("A3", "Linha\ncom quebra"), "<c r=\"B3\"><v>3</v></c><c r=\"C3\"><v>7.5</v></c><c r=\"D3\"><f>B3*C3</f><v>22.5</v></c></row>",
        "<row r=\"4\">", Text("A4", "=HYPERLINK(\"http://exemplo.invalid\",\"clique\")"), Text("B4", "+1"), Text("C4", "-2"), Text("D4", "@SUM(1)"), "</row>",
        "<row r=\"5\">", Text("A5", "Total geral"), "<c r=\"D5\"><f>SUM(D2:D3)</f><v>202.3</v></c></row>");
    var segunda = string.Concat("<row r=\"1\">", Text("A1", "Segunda aba: ação, coração"), "</row>");
    return Zip(
        ("[Content_Types].xml", Utf8("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/caixa.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/segunda.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>"""), false),
        ("_rels/.rels", Utf8("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>"""), false),
        ("xl/workbook.xml", Utf8($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="{Main}" xmlns:r="{R}"><sheets><sheet name="Caixa" sheetId="1" r:id="rId1"/><sheet name="Segunda" sheetId="2" r:id="rId2"/></sheets></workbook>"""), false),
        ("xl/_rels/workbook.xml.rels", Utf8($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{R}/worksheet" Target="worksheets/caixa.xml"/><Relationship Id="rId2" Type="{R}/worksheet" Target="worksheets/segunda.xml"/></Relationships>"""), false),
        ("xl/worksheets/caixa.xml", Utf8($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="{Main}"><sheetData>{caixa}</sheetData></worksheet>"""), false),
        ("xl/worksheets/segunda.xml", Utf8($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="{Main}"><sheetData>{segunda}</sheetData></worksheet>"""), false));
}

// TST-12: macro Basic no evento de abertura que gravaria o marcador. O marcador nunca pode aparecer.
static byte[] OdtWithOpenMacro(string marker) => Zip(
    ("mimetype", Utf8("application/vnd.oasis.opendocument.text"), true),
    ("META-INF/manifest.xml", Utf8("""<?xml version="1.0" encoding="UTF-8"?><manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.3"><manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.text"/><manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="Basic/script-lc.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="Basic/Standard/script-lb.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="Basic/Standard/Module1.xml" manifest:media-type="text/xml"/></manifest:manifest>"""), false),
    ("content.xml", Utf8("""<?xml version="1.0" encoding="UTF-8"?><office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:script="urn:oasis:names:tc:opendocument:xmlns:script:1.0" xmlns:xlink="http://www.w3.org/1999/xlink" office:version="1.3"><office:scripts><office:event-listeners><script:event-listener script:language="ooo:script" script:event-name="dom:load" xlink:href="vnd.sun.star.script:Standard.Module1.Main?language=Basic&amp;location=document" xlink:type="simple"/></office:event-listeners></office:scripts><office:body><office:text><text:p>Documento com macro ao abrir. O PDF deve sair; o arquivo macro-rodou.txt nunca pode aparecer.</text:p></office:text></office:body></office:document-content>"""), false),
    ("Basic/script-lc.xml", Utf8("""<?xml version="1.0" encoding="UTF-8"?><library:libraries xmlns:library="http://openoffice.org/2000/library" xmlns:xlink="http://www.w3.org/1999/xlink"><library:library library:name="Standard" xlink:href="Basic/Standard/script-lb.xml/" xlink:type="simple" library:link="false"/></library:libraries>"""), false),
    ("Basic/Standard/script-lb.xml", Utf8("""<?xml version="1.0" encoding="UTF-8"?><library:library xmlns:library="http://openoffice.org/2000/library" library:name="Standard" library:readonly="false" library:passwordprotected="false"><library:element library:name="Module1"/></library:library>"""), false),
    ("Basic/Standard/Module1.xml", Utf8($"""<?xml version="1.0" encoding="UTF-8"?><script:module xmlns:script="http://openoffice.org/2000/script" script:name="Module1" script:language="StarBasic">Sub Main&#10;Open "{System.Security.SecurityElement.Escape(marker)}" For Output As #1&#10;Print #1, "rodou"&#10;Close #1&#10;End Sub</script:module>"""), false));

// PCM inteiro; com máscara, grava WAVE_FORMAT_EXTENSIBLE (necessário para mais de 2 canais).
static void Wav(string path, int channels, int rate, int bits, uint mask, double seconds, Func<int, double, double> sample)
{
    var frames = (int)Math.Round(seconds * rate);
    var bytesPerSample = bits / 8;
    var data = new byte[frames * channels * bytesPerSample];
    var max = (1 << (bits - 1)) - 1;
    for (var frame = 0; frame < frames; frame++)
    {
        for (var channel = 0; channel < channels; channel++)
        {
            var value = (int)Math.Round(Math.Clamp(sample(channel, frame / (double)rate), -1, 1) * max);
            var offset = ((frame * channels) + channel) * bytesPerSample;
            for (var b = 0; b < bytesPerSample; b++) data[offset + b] = (byte)(value >> (8 * b));
        }
    }

    using var writer = new BinaryWriter(File.Create(path));
    var extensible = mask != 0;
    var formatSize = extensible ? 40 : 16;
    writer.Write("RIFF"u8);
    writer.Write(4 + (8 + formatSize) + (8 + data.Length));
    writer.Write("WAVEfmt "u8);
    writer.Write(formatSize);
    writer.Write((ushort)(extensible ? 0xFFFE : 1));
    writer.Write((ushort)channels);
    writer.Write(rate);
    writer.Write(rate * channels * bytesPerSample);
    writer.Write((ushort)(channels * bytesPerSample));
    writer.Write((ushort)bits);
    if (extensible)
    {
        writer.Write((ushort)22);
        writer.Write((ushort)bits);
        writer.Write(mask);
        writer.Write([0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71]); // KSDATAFORMAT_SUBTYPE_PCM
    }

    writer.Write("data"u8);
    writer.Write(data.Length);
    writer.Write(data);
}
