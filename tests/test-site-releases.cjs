const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../docs/site.js'), 'utf8');
const releaseCode = source.slice(source.indexOf(' var repository ='), source.lastIndexOf('})();'));
const base = 'https://github.com/trfurkan/GoodbyeDPI-Tray/releases/';
function release(tag, prerelease = false) {
 return { tag_name: tag, html_url: base + 'tag/' + tag, published_at: '2026-11-09T12:00:00Z',
  prerelease, body: '- Example change\n- <script>unsafe()</script>',
  assets: [{ name: 'GoodbyeDPI-Installer.exe', browser_download_url: base + 'download/' + tag + '/GoodbyeDPI-Installer.exe' }] };
}
function node() {
 return { children: [], dataset: { latest: 'Latest', others: 'Other', notes: 'Notes', download: 'Download', prerelease: 'Preview' },
  appendChild(child) { this.children.push(child); }, replaceChildren(child) { this.children = child.children; },
  get childElementCount() { return this.children.length; } };
}
async function run() {
 const list = node(), badge = node(), download = node(), latest = node();
 const events = {}, storage = new Map();
 const cached = release('v1.9.0');
 storage.set('goodbyedpi-tray:releases:v1', JSON.stringify({ releases: [cached] }));
 const calls = [];
 let fail = false;
 const context = vm.createContext({
  root: { lang: 'tr' }, Date, AbortController, setTimeout, clearTimeout,
  setInterval() {},
  localStorage: { getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value) },
  document: { hidden: false, createElement: node, createDocumentFragment: node,
   querySelector: () => list,
   querySelectorAll: selector => selector === '[data-version]' ? [badge] : selector === 'a[data-installer]' ? [download] : [latest],
   addEventListener: (name, fn) => { events[name] = fn; } },
  window: { addEventListener: (name, fn) => { events[name] = fn; } },
  fetch: async url => {
   calls.push(url);
   if (fail) throw new Error('Offline');
   const second = url.endsWith('page=2');
   return { ok: true, headers: { get: () => second ? null : '<next>; rel="next"' },
    json: async () => second ? [cached] : [release('v1.11.0-beta', true), release('v1.10.0'), { ...release('v2.0.0'), draft: true }] };
  }
 });
 vm.runInContext(releaseCode, context);
 assert.equal(badge.textContent, 'v1.9.0', 'Cache renders before network response');
 await new Promise(resolve => setImmediate(resolve));
 assert.equal(calls.length, 2, 'Fetches all release pages');
 assert.equal(badge.textContent, 'v1.10.0', 'Stable release wins over preview');
 assert.match(download.href, /v1\.10\.0/);
 assert.equal(list.children.length, 2, 'Latest card and release history render');
 const before = list.children;
 fail = true;
 await context.refreshReleases(true);
 assert.equal(list.children, before, 'Network failure preserves working content');
 fail = false;
 events.online();
 await new Promise(resolve => setImmediate(resolve));
 assert.equal(calls.length, 5, 'Reconnection retries pagination');
 assert.equal(JSON.parse(storage.get('goodbyedpi-tray:releases:v1')).releases.length, 4);
 console.log('PASS: cache, pagination, stable selection, offline fallback and reconnection');
}
run().catch(error => { console.error(error); process.exitCode = 1; });
