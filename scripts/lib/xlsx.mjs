// A minimal .xlsx reader for the seed (no dependency): the zip's central directory → each entry's bytes (stored or deflate),
// then one sheet's cells as { value, formula } by reference ("C6"). Shared and inline strings are resolved; numbers stay text
// as Excel wrote them (a date is its serial: 1899-12-30 + n).
import { readFileSync } from 'node:fs';
import { inflateRawSync } from 'node:zlib';

function entries(buffer) {
  // End of central directory: signature 0x06054b50 in the last 64 KiB.
  let eocd = -1;
  for (let i = buffer.length - 22; i >= Math.max(0, buffer.length - 65558); i--) {
    if (buffer.readUInt32LE(i) === 0x06054b50) { eocd = i; break; }
  }
  if (eocd < 0) throw new Error('not a zip file');
  const count = buffer.readUInt16LE(eocd + 10);
  let p = buffer.readUInt32LE(eocd + 16);
  const files = new Map();
  for (let n = 0; n < count; n++) {
    if (buffer.readUInt32LE(p) !== 0x02014b50) throw new Error('bad central directory');
    const method = buffer.readUInt16LE(p + 10);
    const compressed = buffer.readUInt32LE(p + 20);
    const nameLength = buffer.readUInt16LE(p + 28);
    const extraLength = buffer.readUInt16LE(p + 30);
    const commentLength = buffer.readUInt16LE(p + 32);
    const local = buffer.readUInt32LE(p + 42);
    const name = buffer.toString('utf8', p + 46, p + 46 + nameLength);
    files.set(name, { method, compressed, local });
    p += 46 + nameLength + extraLength + commentLength;
  }
  return (name) => {
    const f = files.get(name);
    if (!f) return null;
    const start = f.local + 30 + buffer.readUInt16LE(f.local + 26) + buffer.readUInt16LE(f.local + 28);
    const data = buffer.subarray(start, start + f.compressed);
    if (f.method === 0) return data.toString('utf8');
    if (f.method === 8) return inflateRawSync(data).toString('utf8');
    throw new Error(`unsupported zip method ${f.method} for ${name}`);
  };
}

const unescape = (s) => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'")
  .replace(/&#(\d+);/g, (_, d) => String.fromCodePoint(+d)).replace(/&amp;/g, '&');
const attr = (tag, name) => tag.match(new RegExp(`\\s${name}="([^"]*)"`))?.[1];
const texts = (xml) => [...xml.matchAll(/<t(?:\s[^>]*)?>([\s\S]*?)<\/t>/g)].map((m) => unescape(m[1])).join('');

/** The cells of sheet `sheetName` of the workbook at `path`: Map("C6" → { value, formula }). */
export function readSheet(path, sheetName) {
  const read = entries(readFileSync(path));
  const shared = [...(read('xl/sharedStrings.xml') ?? '').matchAll(/<si>([\s\S]*?)<\/si>/g)].map((m) => texts(m[1]));
  const workbook = read('xl/workbook.xml');
  const sheet = [...workbook.matchAll(/<sheet\s[^>]*>/g)].map((m) => m[0]).find((t) => unescape(attr(t, 'name') ?? '') === sheetName);
  if (!sheet) throw new Error(`sheet ${sheetName} not found`);
  const rid = attr(sheet, 'r:id');
  const rels = read('xl/_rels/workbook.xml.rels');
  const rel = [...rels.matchAll(/<Relationship\s[^>]*>/g)].map((m) => m[0]).find((t) => attr(t, 'Id') === rid);
  const target = attr(rel, 'Target').replace(/^\/?(xl\/)?/, '');
  const xml = read(`xl/${target}`);
  const cells = new Map();
  for (const m of xml.matchAll(/<c\s([^>]*?)(?:\/>|>([\s\S]*?)<\/c>)/g)) {
    const tag = ` ${m[1]}`;
    const ref = attr(tag, 'r');
    const type = attr(tag, 't');
    const body = m[2] ?? '';
    const v = body.match(/<v>([\s\S]*?)<\/v>/)?.[1];
    const formula = body.match(/<f(?:\s[^>]*)?>([\s\S]*?)<\/f>/)?.[1];
    let value = v === undefined ? null : unescape(v);
    if (type === 's' && value !== null) value = shared[+value];
    if (type === 'inlineStr') value = texts(body);
    cells.set(ref, { value, formula: formula === undefined ? null : unescape(formula) });
  }
  return cells;
}
