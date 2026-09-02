const validSize = (width: number, height: number) => width > 0 && height > 0 && width <= 1600 && height <= 1600 && width * height <= 2_000_000;

export function validEvidencePhoto(value: string): boolean {
  if (value === "") return true;
  if (value.length > 800000) return false;
  const match = /^data:image\/(jpeg|png);base64,([A-Za-z0-9+/]+={0,2})$/.exec(value);
  if (!match) return false;
  try {
    const bytes = Uint8Array.from(atob(match[2]), c => c.charCodeAt(0));
    const view = new DataView(bytes.buffer);
    if (match[1] === "png") {
      const signature = [137, 80, 78, 71, 13, 10, 26, 10];
      if (bytes.length < 33 || !signature.every((byte, i) => bytes[i] === byte) || view.getUint32(8) !== 13 || view.getUint32(12) !== 0x49484452) return false;
      return validSize(view.getUint32(16), view.getUint32(20));
    }
    if (bytes[0] !== 255 || bytes[1] !== 216) return false;
    for (let offset = 2; offset < bytes.length;) {
      if (bytes[offset] !== 255) return false;
      while (bytes[offset] === 255) offset++;
      const marker = bytes[offset++];
      if (marker === 0xda || marker === 0xd9) return false;
      if (marker === 1 || (marker >= 0xd0 && marker <= 0xd8)) continue;
      const length = view.getUint16(offset);
      if (length < 2 || offset + length > bytes.length) return false;
      if ([0xc0, 0xc1, 0xc2].includes(marker)) {
        return length >= 8 && validSize(view.getUint16(offset + 5), view.getUint16(offset + 3));
      }
      offset += length;
    }
  } catch { return false; }
  return false;
}
