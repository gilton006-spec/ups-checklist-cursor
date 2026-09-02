using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace UpsChecklist.Core.Pdf;

internal static class PdfSyntax
{
    public static string Number(double value)
    {
        if (!double.IsFinite(value)) throw new InvalidDataException("PDF numbers must be finite.");
        return value.ToString("0.#####", CultureInfo.InvariantCulture);
    }
    public static string Numbers(params double[] values) => string.Join(" ", values.Select(Number));
    public static string Text(string value) => "<FEFF" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(value)) + ">";
    public static string Reference(int id) => Number(id) + " 0 R";
    public static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
}

// A deliberately bounded PDF 1.7 writer for this form: A4 pages, embedded TrueType,
// PNG/JPEG images, text fields and checkboxes. It never parses or edits arbitrary PDFs.
// All page coordinates and appearance coordinates use PDF's bottom-left origin.
internal sealed class PdfDocumentWriter(ChecklistFont font)
{
    private readonly List<byte[]?> _objects = [null];
    private readonly Dictionary<string, Field> _fields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _images = new(StringComparer.Ordinal);
    private readonly int _catalog = 1;
    private readonly int _pageTree = 2;
    private readonly int _font = 3;
    private readonly int _helvetica = 4;
    public List<PdfPageCanvas> Pages { get; } = [];

    private void Initialize()
    {
        if (_objects.Count != 1) return;
        for (var i = 0; i < 4; i++) Reserve();
    }
    private int Reserve() { _objects.Add(null); return _objects.Count - 1; }
    private void Set(int id, string value) => _objects[id] = PdfSyntax.Ascii(value);
    private int Add(string value) { var id = Reserve(); Set(id, value); return id; }
    private static string Ref(int id) => PdfSyntax.Reference(id);
    private string FontResources => $"<< /Font << /F0 {Ref(_font)} /Helv {Ref(_helvetica)} >> >>";

    private int Stream(string entries, byte[] bytes, bool compress = true)
    {
        if (compress)
        {
            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(bytes);
            bytes = compressed.ToArray();
            entries += " /Filter /FlateDecode";
        }
        using var result = new MemoryStream();
        result.Write(PdfSyntax.Ascii($"<< {entries} /Length {PdfSyntax.Number(bytes.Length)} >>\nstream\n"));
        result.Write(bytes);
        result.Write(PdfSyntax.Ascii("\nendstream"));
        var id = Reserve();
        _objects[id] = result.ToArray();
        return id;
    }

    public PdfPageCanvas AddPage(double width, double height)
    {
        Initialize();
        var page = new PdfPageCanvas(this, font, Reserve(), width, height);
        Pages.Add(page);
        return page;
    }

    internal int Image(PdfImage image)
    {
        var hash = Convert.ToHexString(SHA256.HashData(image.Pixels)) + ":" + image.Width + ":" + image.Height
            + ":" + image.IsJpeg + ":" + image.ColorSpace
            + (image.Alpha is null ? "" : Convert.ToHexString(SHA256.HashData(image.Alpha)));
        if (_images.TryGetValue(hash, out var existing)) return existing;
        var dimensions = $"/Type /XObject /Subtype /Image /Width {PdfSyntax.Number(image.Width)} /Height {PdfSyntax.Number(image.Height)} /BitsPerComponent 8";
        var mask = image.Alpha is null ? "" : $" /SMask {Ref(Stream(dimensions + " /ColorSpace /DeviceGray", image.Alpha))}";
        var filters = image.IsJpeg ? " /Filter /DCTDecode" : "";
        if (image.InvertCmyk) filters += " /Decode [1 0 1 0 1 0 1 0]";
        var id = Stream(dimensions + " /ColorSpace " + image.ColorSpace + mask + filters, image.Pixels, !image.IsJpeg);
        _images.Add(hash, id);
        return id;
    }

    public void TextField(PdfPageCanvas page, string name, string value, double x, double bottom,
        double width, double height, bool multiline, double size)
    {
        var field = GetField(name, value, multiline ? 4096 : 0, false, size);
        var appearance = PdfFormAppearance.Text(font, value, width, height, size, multiline);
        var ap = Appearance(appearance, width, height);
        Widget(page, field, x, bottom, width, height, $"/AP << /N {Ref(ap)} >>");
    }

    public void Checkbox(PdfPageCanvas page, string name, bool isChecked, double x, double bottom, double width, double height)
    {
        var state = isChecked ? "/Yes" : "/Off";
        var field = GetField(name, state, 0, true, 0);
        var off = Appearance(PdfFormAppearance.Checkbox(width, height, false), width, height);
        var on = Appearance(PdfFormAppearance.Checkbox(width, height, true), width, height);
        Widget(page, field, x, bottom, width, height, $"/AS {state} /AP << /N << /Off {Ref(off)} /Yes {Ref(on)} >> >>");
    }

    private Field GetField(string name, string value, int flags, bool checkbox, double size)
    {
        if (_fields.TryGetValue(name, out var field))
        {
            if (field.Value != value || field.Flags != flags || field.Checkbox != checkbox)
                throw new InvalidOperationException("Conflicting values for repeated PDF field: " + name);
            return field;
        }
        field = new Field(Reserve(), name, value, flags, checkbox, size);
        _fields.Add(name, field);
        return field;
    }

    private int Appearance(string commands, double width, double height) =>
        Stream($"/Type /XObject /Subtype /Form /FormType 1 /BBox [0 0 {PdfSyntax.Numbers(width, height)}] /Resources {FontResources}", PdfSyntax.Ascii(commands));

    private void Widget(PdfPageCanvas page, Field field, double x, double bottom, double width, double height, string appearance)
    {
        // One canonical field may own several widgets, one per page. Never move the
        // same annotation between pages or create disconnected duplicate field names.
        var id = Add($"<< /Type /Annot /Subtype /Widget /F 4 /Parent {Ref(field.Id)} /P {Ref(page.Id)} "
            + $"/Rect [{PdfSyntax.Numbers(x, bottom, x + width, bottom + height)}] "
            + $"/BS << /W 0.8 /S /S >> /MK << /BC [0.72 0.74 0.76] /BG [1 1 1] >> {appearance} >>");
        field.Widgets.Add(id);
        page.Annotations.Add(id);
    }

    public byte[] Save(string title, string subject)
    {
        Initialize();
        var fontFile = Stream($"/Length1 {PdfSyntax.Number(font.Bytes.Length)}", font.Bytes);
        var descriptor = Add($"<< /Type /FontDescriptor /FontName /ChecklistSans /Flags 32 /FontBBox [{string.Join(" ", font.BoundingBox.Select(v => PdfSyntax.Number(v)))}] "
            + $"/ItalicAngle 0 /Ascent {PdfSyntax.Number(font.Ascent)} /Descent {PdfSyntax.Number(font.Descent)} /CapHeight {PdfSyntax.Number(font.Ascent)} /StemV 80 /FontFile2 {Ref(fontFile)} >>");
        var widths = string.Join(" ", font.Widths.Select(v => PdfSyntax.Number(v)));
        var descendant = Add($"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /ChecklistSans /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> "
            + $"/FontDescriptor {Ref(descriptor)} /CIDToGIDMap /Identity /DW 1000 /W [0 [{widths}]] >>");
        var unicode = Stream("", PdfSyntax.Ascii(font.UnicodeMap()));
        Set(_font, $"<< /Type /Font /Subtype /Type0 /BaseFont /ChecklistSans /Encoding /Identity-H /DescendantFonts [{Ref(descendant)}] /ToUnicode {Ref(unicode)} >>");
        // Standard 14 Helvetica is the portable editing fallback. Initial appearances
        // use the embedded checklist font; both font resources are explicitly defined.
        Set(_helvetica, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");

        foreach (var field in _fields.Values)
        {
            var value = field.Checkbox ? field.Value : PdfSyntax.Text(field.Value);
            var da = field.Checkbox ? "" : $" /DA (/Helv {PdfSyntax.Number(field.Size)} Tf 0.12 0.12 0.12 rg)";
            Set(field.Id, $"<< /FT {(field.Checkbox ? "/Btn" : "/Tx")} /T {PdfSyntax.Text(field.Name)} /TU {PdfSyntax.Text(field.Name)} "
                + $"/V {value} /DV {value} /Ff {PdfSyntax.Number(field.Flags)} /Kids [{string.Join(" ", field.Widgets.Select(Ref))}]{da} >>");
        }
        var form = Add($"<< /Fields [{string.Join(" ", _fields.Values.Select(f => Ref(f.Id)))}] /NeedAppearances false /DA (/Helv 10 Tf 0.12 0.12 0.12 rg) /DR {FontResources} >>");
        foreach (var page in Pages)
        {
            var content = Stream("", PdfSyntax.Ascii(page.Content.ToString()));
            var images = string.Join(" ", page.Images.Select(id => $"/Im{PdfSyntax.Number(id)} {Ref(id)}"));
            Set(page.Id, $"<< /Type /Page /Parent {Ref(_pageTree)} /MediaBox [0 0 {PdfSyntax.Numbers(page.Width, page.Height)}] "
                + $"/Resources << /Font << /F0 {Ref(_font)} >> /XObject << {images} >> >> /Contents {Ref(content)} "
                + $"/Annots [{string.Join(" ", page.Annotations.Select(Ref))}] /Tabs /R >>");
        }
        Set(_pageTree, $"<< /Type /Pages /Kids [{string.Join(" ", Pages.Select(p => Ref(p.Id)))}] /Count {PdfSyntax.Number(Pages.Count)} >>");
        Set(_catalog, $"<< /Type /Catalog /Pages {Ref(_pageTree)} /AcroForm {Ref(form)} >>");
        var info = Add($"<< /Title {PdfSyntax.Text(title)} /Subject {PdfSyntax.Text(subject)} /Creator (Sunrise checklist) /Producer (UPS checklist .NET exporter) >>");
        using var output = new MemoryStream();
        output.Write(PdfSyntax.Ascii("%PDF-1.7\n%"));
        output.Write(new byte[] { 0xe2, 0xe3, 0xcf, 0xd3, 10 });
        var offsets = new List<long> { 0 };
        for (var id = 1; id < _objects.Count; id++)
        {
            offsets.Add(output.Position);
            output.Write(PdfSyntax.Ascii($"{PdfSyntax.Number(id)} 0 obj\n"));
            output.Write(_objects[id] ?? throw new InvalidOperationException("Unresolved PDF object."));
            output.Write(PdfSyntax.Ascii("\nendobj\n"));
        }
        var xref = output.Position;
        output.Write(PdfSyntax.Ascii($"xref\n0 {PdfSyntax.Number(_objects.Count)}\n0000000000 65535 f \n"));
        foreach (var offset in offsets.Skip(1))
            output.Write(PdfSyntax.Ascii(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n"));
        output.Write(PdfSyntax.Ascii($"trailer\n<< /Size {PdfSyntax.Number(_objects.Count)} /Root {Ref(_catalog)} /Info {Ref(info)} >>\nstartxref\n{PdfSyntax.Number(xref)}\n%%EOF\n"));
        return output.ToArray();
    }

    private sealed record Field(int Id, string Name, string Value, int Flags, bool Checkbox, double Size)
    {
        public List<int> Widgets { get; } = [];
    }
}

internal sealed class PdfPageCanvas(PdfDocumentWriter document, ChecklistFont font, int id, double width, double height)
{
    public int Id { get; } = id;
    public double Width { get; } = width;
    public double Height { get; } = height;
    public StringBuilder Content { get; } = new();
    public List<int> Annotations { get; } = [];
    public HashSet<int> Images { get; } = [];

    public void Text(string text, double x, double baseline, double size, PdfColor color) =>
        Content.Append("q\n").Append(color.Fill).Append("\nBT\n/F0 ").Append(PdfSyntax.Number(size))
            .Append(" Tf\n1 0 0 1 ").Append(PdfSyntax.Numbers(x, baseline)).Append(" Tm\n")
            .Append(font.Encode(text)).Append(" Tj\nET\nQ\n");

    public void Rectangle(double x, double bottom, double w, double h, PdfColor color, bool fill) =>
        Content.Append("q\n").Append(fill ? color.Fill : color.Stroke).Append("\n0.8 w\n")
            .Append(PdfSyntax.Numbers(x, bottom, w, h)).Append(fill ? " re f\nQ\n" : " re S\nQ\n");

    public void Image(PdfImage image, double x, double bottom, double w, double h)
    {
        var imageId = document.Image(image);
        Images.Add(imageId);
        Content.Append("q\n").Append(PdfSyntax.Numbers(w, 0, 0, h, x, bottom))
            .Append(" cm\n/Im").Append(PdfSyntax.Number(imageId)).Append(" Do\nQ\n");
    }
}
