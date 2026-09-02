using System.Text.RegularExpressions;

namespace UpsChecklist.Core;

public static partial class EvidenceValidation
{
    private const int MaxLength = 800_000;

    [GeneratedRegex(@"^data:image/(jpeg|png);base64,([A-Za-z0-9+/]+={0,2})$")]
    private static partial Regex DataUrlPattern();

    public static bool ValidEvidencePhoto(string value)
    {
        if (string.IsNullOrEmpty(value))
            return true;
        if (value.Length > MaxLength)
            return false;

        var match = DataUrlPattern().Match(value);
        if (!match.Success)
            return false;

        try
        {
            var bytes = Convert.FromBase64String(match.Groups[2].Value);
            return match.Groups[1].Value == "png"
                ? ValidPng(bytes)
                : ValidJpeg(bytes);
        }
        catch
        {
            return false;
        }
    }

    private static bool ValidSize(int width, int height) =>
        width > 0 && height > 0 && width <= 1600 && height <= 1600 && width * height <= 2_000_000;

    private static bool ValidPng(byte[] bytes)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(signature))
            return false;

        if (ReadUInt32Be(bytes, 8) != 13)
            return false;
        if (ReadUInt32Be(bytes, 12) != 0x49484452)
            return false;

        var width = (int)ReadUInt32Be(bytes, 16);
        var height = (int)ReadUInt32Be(bytes, 20);
        return ValidSize(width, height);
    }

    private static uint ReadUInt32Be(byte[] bytes, int offset) =>
        ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

    private static bool ValidJpeg(byte[] bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            return false;

        var offset = 2;
        while (offset < bytes.Length)
        {
            if (bytes[offset] != 0xFF)
                return false;
            while (offset < bytes.Length && bytes[offset] == 0xFF)
                offset++;
            if (offset >= bytes.Length)
                return false;

            var marker = bytes[offset++];
            if (marker is 0xDA or 0xD9)
                return false;
            if (marker is 1 or >= 0xD0 and <= 0xD8)
                continue;

            if (offset + 1 >= bytes.Length)
                return false;
            var length = (bytes[offset] << 8) | bytes[offset + 1];
            if (length < 2 || offset + length > bytes.Length)
                return false;

            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                if (length < 8)
                    return false;
                var height = (bytes[offset + 3] << 8) | bytes[offset + 4];
                var width = (bytes[offset + 5] << 8) | bytes[offset + 6];
                return ValidSize(width, height);
            }

            offset += length;
        }

        return false;
    }
}
