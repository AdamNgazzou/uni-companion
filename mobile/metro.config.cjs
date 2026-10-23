const { getDefaultConfig } = require('expo/metro-config');
const http = require('node:http');
const config = getDefaultConfig(__dirname);
config.maxWorkers = 1;
config.resolver.assetExts.push('html');
const previous = config.server.enhanceMiddleware;
config.server.enhanceMiddleware = (middleware, server) => {
  const next = previous ? previous(middleware, server) : middleware;
  return (req, res, done) => {
    const path = req.url || '';
    if (!path.startsWith('/api/v1/')) return next(req, res, done);
    if (/%2f|%5c|\.\.|\\/i.test(path) || !['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].includes(req.method)) { res.writeHead(400); res.end(); return; }
    if (Number(req.headers['content-length'] || 0) > 1048576) { res.writeHead(413); res.end(); return; }
    // Fixed loopback target. Never proxy arbitrary hosts, mail, admin CLI or OpenAPI.
    const headers = { ...req.headers, host: 'localhost:5080' };
    for (const name of ['connection', 'transfer-encoding', 'forwarded', 'x-forwarded-for', 'x-forwarded-host', 'x-forwarded-proto', 'cookie']) delete headers[name];
    const sessionCookies = (req.headers.cookie || '').split(';').map(value => value.trim()).filter(value => /^uni\.session(?:C\d+)?=/.test(value));
    if (sessionCookies.length) headers.cookie = sessionCookies.join('; ');
    const upstream = http.request({ hostname: '127.0.0.1', port: 5080, path, method: req.method, headers }, response => {
      res.writeHead(response.statusCode || 502, response.headers);
      response.pipe(res);
    });
    upstream.setTimeout(65000, () => upstream.destroy());
    upstream.on('error', () => { if (!res.headersSent) res.writeHead(503, { 'Content-Type': 'application/problem+json' }); res.end(JSON.stringify({ title: 'Backend unavailable' })); });
    let bytes = 0;
    req.on('data', chunk => { bytes += chunk.length; if (bytes > 1048576) { upstream.destroy(); if (!res.headersSent) res.writeHead(413); res.end(); } });
    res.on('close', () => upstream.destroy());
    req.pipe(upstream);
  };
};
module.exports = config;
