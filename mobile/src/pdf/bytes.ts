export function base64(bytes: Uint8Array): string {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
  const parts: string[] = [];
  let part = '';
  for (let i = 0; i < bytes.length; i += 3) {
    const a = bytes[i],
      b = bytes[i + 1] || 0,
      c = bytes[i + 2] || 0;
    part +=
      alphabet[a >> 2] +
      alphabet[((a & 3) << 4) | (b >> 4)] +
      (i + 1 < bytes.length ? alphabet[((b & 15) << 2) | (c >> 6)] : '=') +
      (i + 2 < bytes.length ? alphabet[c & 63] : '=');
    if (part.length >= 8192) {
      parts.push(part);
      part = '';
    }
  }
  parts.push(part);
  return parts.join('');
}
export const physicalPage = (value: string | undefined) => {
  const page = Number(value);
  return Number.isSafeInteger(page) && page >= 1 ? page : 1;
};
