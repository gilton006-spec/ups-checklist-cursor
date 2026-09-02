using System.Buffers.Binary;
using System.Text;

namespace UpsChecklist.Core.Pdf;

// Read the bundled TrueType font's metrics/cmap and embed its complete bytes.
// No operating-system font lookup or platform-specific drawing API is needed.
internal sealed class ChecklistFont
{
    public byte[] Bytes { get; }
    public int UnitsPerEm { get; }
    public int Ascent { get; }
    public int Descent { get; }
    public int[] BoundingBox { get; }
    public int[] Widths { get; }
    private readonly int _cmap;
    private readonly int _format;
    private readonly Dictionary<ushort, string> _unicode = new();

    public ChecklistFont(string path)
    {
        Bytes = File.ReadAllBytes(path);
        var tables = new Dictionary<string, int>();
        for (var index = 0; index < U16(4); index++)
        {
            var offset = 12 + index * 16;
            tables.Add(Encoding.ASCII.GetString(Bytes, offset, 4), checked((int)U32(offset + 8)));
        }
        var head = tables["head"];
        UnitsPerEm = U16(head + 18);
        if (UnitsPerEm == 0) throw new InvalidDataException("Invalid font units.");
        BoundingBox = Enumerable.Range(0, 4).Select(i => Scale(I16(head + 36 + 2 * i))).ToArray();
        var hhea = tables["hhea"];
        Ascent = Scale(I16(hhea + 4));
        Descent = Scale(I16(hhea + 6));
        var metricsCount = U16(hhea + 34);
        var glyphCount = U16(tables["maxp"] + 4);
        Widths = Enumerable.Range(0, glyphCount)
            .Select(i => Scale(U16(tables["hmtx"] + Math.Min(i, metricsCount - 1) * 4))).ToArray();

        var cmap = tables["cmap"];
        var choices = new List<(int Format, int Offset)>();
        for (var i = 0; i < U16(cmap + 2); i++)
        {
            var record = cmap + 4 + i * 8;
            var platform = U16(record);
            var encoding = U16(record + 2);
            var offset = cmap + checked((int)U32(record + 4));
            var format = U16(offset);
            if ((platform == 0 || platform == 3 && encoding is 1 or 10) && format is 4 or 12)
                choices.Add((format, offset));
        }
        if (choices.Count == 0) throw new InvalidDataException("The font has no supported Unicode cmap.");
        (_format, _cmap) = choices.OrderByDescending(c => c.Format).First();
    }

    private int Scale(int value) => (int)Math.Round(value * 1000.0 / UnitsPerEm);
    private ushort U16(int offset) => BinaryPrimitives.ReadUInt16BigEndian(Bytes.AsSpan(offset, 2));
    private short I16(int offset) => BinaryPrimitives.ReadInt16BigEndian(Bytes.AsSpan(offset, 2));
    private uint U32(int offset) => BinaryPrimitives.ReadUInt32BigEndian(Bytes.AsSpan(offset, 4));

    private ushort Glyph(int codePoint)
    {
        if (_format == 12)
        {
            var count = checked((int)U32(_cmap + 12));
            for (var i = 0; i < count; i++)
            {
                var group = _cmap + 16 + i * 12;
                var start = U32(group);
                var end = U32(group + 4);
                if (codePoint >= start && codePoint <= end)
                    return checked((ushort)(U32(group + 8) + codePoint - start));
            }
        }
        else if (codePoint <= ushort.MaxValue)
        {
            var segments = U16(_cmap + 6) / 2;
            var ends = _cmap + 14;
            var starts = ends + segments * 2 + 2;
            var deltas = starts + segments * 2;
            var ranges = deltas + segments * 2;
            for (var i = 0; i < segments; i++)
            {
                if (codePoint < U16(starts + i * 2) || codePoint > U16(ends + i * 2)) continue;
                var delta = I16(deltas + i * 2);
                var range = U16(ranges + i * 2);
                if (range == 0) return unchecked((ushort)(codePoint + delta));
                var id = U16(ranges + i * 2 + range + (codePoint - U16(starts + i * 2)) * 2);
                return id == 0 ? (ushort)0 : unchecked((ushort)(id + delta));
            }
        }
        return 0;
    }

    private ushort RequireGlyph(Rune rune)
    {
        var glyph = Glyph(rune.Value);
        if (glyph == 0 || glyph >= Widths.Length)
            throw new InvalidOperationException($"The checklist font does not support U+{rune.Value:X4}. Use supported letters or draw your signature.");
        return glyph;
    }

    public double Measure(string value, double size) =>
        value.EnumerateRunes().Sum(rune => (double)Widths[RequireGlyph(rune)]) * size / 1000;

    public string Encode(string value)
    {
        var encoded = new StringBuilder("<");
        foreach (var rune in value.EnumerateRunes())
        {
            var glyph = RequireGlyph(rune);
            _unicode.TryAdd(glyph, Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(rune.ToString())));
            encoded.Append(glyph.ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        }
        return encoded.Append('>').ToString();
    }

    public string UnicodeMap()
    {
        var map = new StringBuilder("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n"
            + "/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n"
            + "/CMapName /ChecklistUnicode def\n/CMapType 2 def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
        foreach (var chunk in _unicode.OrderBy(p => p.Key).Chunk(100))
        {
            map.Append(PdfSyntax.Number(chunk.Length)).Append(" beginbfchar\n");
            foreach (var (glyph, unicode) in chunk)
                map.Append('<').Append(glyph.ToString("X4", System.Globalization.CultureInfo.InvariantCulture))
                    .Append("> <").Append(unicode).Append(">\n");
            map.Append("endbfchar\n");
        }
        return map.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n").ToString();
    }
}
