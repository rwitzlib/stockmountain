// One-off helper: accepts base64 PNGs from the browser and writes them into public/.
// Usage: node scripts/asset-save-server.mjs  (POST {name, b64} to http://127.0.0.1:8787/save)
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const publicDir = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'public');
const allowed = new Set(['og-image.png', 'apple-touch-icon.png', 'icon-32.png', 'icon-16.png']);

const server = http.createServer((req, res) => {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Headers', 'content-type');
  if (req.method === 'OPTIONS') return res.end();
  if (req.method !== 'POST' || req.url !== '/save') {
    res.statusCode = 404;
    return res.end('not found');
  }
  let body = '';
  req.on('data', (c) => (body += c));
  req.on('end', () => {
    try {
      const { name, b64 } = JSON.parse(body);
      if (!allowed.has(name)) throw new Error(`name not allowed: ${name}`);
      fs.writeFileSync(path.join(publicDir, name), Buffer.from(b64, 'base64'));
      res.end(JSON.stringify({ ok: true, name }));
    } catch (e) {
      res.statusCode = 400;
      res.end(JSON.stringify({ ok: false, error: String(e) }));
    }
  });
});

server.listen(8787, '127.0.0.1', () => console.log('asset-save-server on 8787'));
