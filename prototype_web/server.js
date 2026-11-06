import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

// fileURLToPath handles Windows drive letters and percent-encoded (e.g. Japanese) folder names.
const root = fileURLToPath(new URL('.', import.meta.url));
// /data/ serves the package's canonical data/*.json so the prototype never keeps its own copy.
const mounts = [
  ['/data/', fileURLToPath(new URL('../data/', import.meta.url))],
  // Same sprites as the Unity build (single source: unity_project Resources/Art).
  ['/art/', fileURLToPath(new URL('../unity_project/Assets/RhythmRunner/Resources/Art/', import.meta.url))],
  ['/', root],
];
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8', '.png': 'image/png' };
const port = Number(process.env.PORT) || 4173;

function resolvePath(urlPath) {
  const [prefix, base] = mounts.find(([p]) => urlPath.startsWith(p));
  const path = resolve(base, '.' + (urlPath === '/' ? '/index.html' : '/' + urlPath.slice(prefix.length)));
  if (!path.startsWith(base.endsWith(sep) ? base : base + sep)) throw new Error('outside root');
  return path;
}

createServer(async (req, res) => {
  try {
    const path = resolvePath(decodeURIComponent(new URL(req.url, 'http://localhost').pathname));
    const data = await readFile(path);
    res.writeHead(200, { 'content-type': types[extname(path)] ?? 'application/octet-stream', 'cache-control': 'no-store' });
    res.end(data);
  } catch {
    res.writeHead(404);
    res.end('Not found');
  }
}).listen(port, '127.0.0.1', () => console.log(`Prototype: http://127.0.0.1:${port}`));
