using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace UpsChecklist.Core.Pdf;

// The input contract is PNG/JPEG (the same formats accepted by EvidenceValidation).
// PNGs are decoded to RGB plus a PDF soft mask; JPEG streams are embedded directly.
// Ancillary PNG metadata is deliberately not copied into the report.
internal sealed record PdfImage(int Width, int Height, byte[] Pixels, byte[]? Alpha = null,
    bool IsJpeg = false, string ColorSpace = "/DeviceRGB", bool InvertCmyk = false)
{
    public static PdfImage Load(string path) => Read(File.ReadAllBytes(path));

    public (double Width, double Height) Fit(double maxWidth, double maxHeight)
    {
        var scale = Math.Min(0.75, Math.Min(maxWidth / Width, maxHeight / Height));
        return (Width * scale, Height * scale);
    }

    public static PdfImage Read(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return Png(bytes);
        if (bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8)
            return Jpeg(bytes);
        throw new InvalidDataException("Expected a PNG or JPEG image.");
    }

    private static void ValidateSize(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 16_000_000)
            throw new InvalidDataException("Invalid or excessively large image dimensions.");
    }

    private static PdfImage Jpeg(byte[] bytes)
    {
        if (bytes[^2] != 0xff || bytes[^1] != 0xd9)
            throw new InvalidDataException("Truncated JPEG image.");
        var offset = 2;
        var adobe = false;
        while (offset < bytes.Length)
        {
            if (bytes[offset++] != 0xff) break;
            while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
            if (offset >= bytes.Length) break;
            var marker = bytes[offset++];
            if (marker is 0xda or 0xd9) break;
            if (marker is 1 or >= 0xd0 and <= 0xd8) continue;
            if (offset + 2 > bytes.Length) break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
            if (length < 2 || offset + length > bytes.Length) break;
            if (marker == 0xee && length >= 7)
                adobe = bytes.AsSpan(offset + 2, 5).SequenceEqual("Adobe"u8);
            if (marker is 0xc0 or 0xc1 or 0xc2)
            {
                if (length < 8 || bytes[offset + 2] != 8) break;
                var height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 5, 2));
                var components = bytes[offset + 7];
                ValidateSize(width, height);
                var color = components switch
                {
                    1 => "/DeviceGray", 3 => "/DeviceRGB", 4 => "/DeviceCMYK",
                    _ => throw new InvalidDataException("Unsupported JPEG components."),
                };
                return new PdfImage(width, height, bytes, IsJpeg: true, ColorSpace: color,
                    InvertCmyk: components == 4 && adobe);
            }
            offset += length;
        }
        throw new InvalidDataException("Invalid JPEG image.");
    }

    private static PdfImage Png(byte[] bytes)
    {
        var offset = 8;
        var width = 0;
        var height = 0;
        var depth = 0;
        var color = 0;
        var interlace = 0;
        byte[] palette = [], transparency = [];
        using var compressed = new MemoryStream();
        var ended = false;
        while (offset <= bytes.Length - 12)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
            if (length > bytes.Length - offset - 12) throw new InvalidDataException("Truncated PNG chunk.");
            var type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            var data = bytes.AsSpan(offset + 8, length);
            if (type == "IHDR")
            {
                if (offset != 8 || length != 13) throw new InvalidDataException("Invalid PNG header.");
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data[4..]));
                depth = data[8]; color = data[9]; interlace = data[12];
                if (data[10] != 0 || data[11] != 0 || interlace > 1)
                    throw new InvalidDataException("Unsupported PNG compression/filter method.");
                ValidateSize(width, height);
            }
            else if (type == "PLTE") palette = data.ToArray();
            else if (type == "tRNS") transparency = data.ToArray();
            else if (type == "IDAT") compressed.Write(data);
            else if (type == "IEND") { ended = true; break; }
            offset += length + 12;
        }
        if (!ended || width == 0 || compressed.Length == 0) throw new InvalidDataException("Incomplete PNG image.");
        var channels = color switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
        var validDepth = color switch
        {
            0 => depth is 1 or 2 or 4 or 8 or 16,
            3 => depth is 1 or 2 or 4 or 8,
            2 or 4 or 6 => depth is 8 or 16,
            _ => false,
        };
        if (!validDepth) throw new InvalidDataException("Unsupported PNG bit depth/color type.");
        var rgb = new byte[width * height * 3];
        var alpha = new byte[width * height];
        var hasAlpha = false;
        compressed.Position = 0;
        using var inflated = new ZLibStream(compressed, CompressionMode.Decompress);
        (int X, int Y, int Dx, int Dy)[] passes = interlace == 0
            ? [(0, 0, 1, 1)]
            : [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4),
               (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)];
        foreach (var pass in passes)
        {
            var pw = Math.Max(0, (width - pass.X + pass.Dx - 1) / pass.Dx);
            var ph = Math.Max(0, (height - pass.Y + pass.Dy - 1) / pass.Dy);
            if (pw == 0 || ph == 0) continue;
            var rowSize = (pw * channels * depth + 7) / 8;
            var bpp = Math.Max(1, (channels * depth + 7) / 8);
            var previous = new byte[rowSize];
            var row = new byte[rowSize];
            for (var y = 0; y < ph; y++)
            {
                var filter = inflated.ReadByte();
                if (filter is < 0 or > 4) throw new InvalidDataException("Invalid PNG row filter.");
                inflated.ReadExactly(row);
                for (var i = 0; i < rowSize; i++)
                {
                    var a = i >= bpp ? row[i - bpp] : 0;
                    var b = previous[i];
                    var c = i >= bpp ? previous[i - bpp] : 0;
                    var prediction = filter switch { 1 => a, 2 => b, 3 => (a + b) / 2, 4 => Paeth(a, b, c), _ => 0 };
                    row[i] = unchecked((byte)(row[i] + prediction));
                }
                for (var x = 0; x < pw; x++)
                {
                    int Sample(int channel)
                    {
                        var sample = x * channels + channel;
                        if (depth == 16) return BinaryPrimitives.ReadUInt16BigEndian(row.AsSpan(sample * 2, 2));
                        if (depth == 8) return row[sample];
                        return (row[sample * depth / 8] >> (8 - depth - sample * depth % 8)) & ((1 << depth) - 1);
                    }
                    byte Scale(int sample) => (byte)(sample * 255 / ((1 << depth) - 1));
                    var destination = (pass.Y + y * pass.Dy) * width + pass.X + x * pass.Dx;
                    var a = (byte)255;
                    if (color == 3)
                    {
                        var index = Sample(0);
                        if (index * 3 + 2 >= palette.Length) throw new InvalidDataException("Invalid PNG palette index.");
                        palette.AsSpan(index * 3, 3).CopyTo(rgb.AsSpan(destination * 3, 3));
                        if (index < transparency.Length) a = transparency[index];
                    }
                    else
                    {
                        var r = Sample(0);
                        var g = color is 0 or 4 ? r : Sample(1);
                        var b = color is 0 or 4 ? r : Sample(2);
                        rgb[destination * 3] = Scale(r);
                        rgb[destination * 3 + 1] = Scale(g);
                        rgb[destination * 3 + 2] = Scale(b);
                        if (color is 4 or 6) a = Scale(Sample(channels - 1));
                        if (color == 0 && transparency.Length == 2 && r == BinaryPrimitives.ReadUInt16BigEndian(transparency)) a = 0;
                        if (color == 2 && transparency.Length == 6
                            && r == BinaryPrimitives.ReadUInt16BigEndian(transparency)
                            && g == BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(2))
                            && b == BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(4))) a = 0;
                    }
                    alpha[destination] = a;
                    hasAlpha |= a != 255;
                }
                (previous, row) = (row, previous);
            }
        }
        if (inflated.ReadByte() != -1) throw new InvalidDataException("Unexpected PNG pixel data.");
        return new PdfImage(width, height, rgb, hasAlpha ? alpha : null);
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a); var pb = Math.Abs(p - b); var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
