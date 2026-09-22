// Targeted recovery checks without needing Unity's generated WebAssembly payload.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const test = require('node:test');

const source = fs.readFileSync(path.join(__dirname, '../Assets/WebGLTemplates/Configurator/configurator-loader.js'), 'utf8');

function createPage(options = {}) {
  const elements = new Map();
  const listeners = {};
  const timers = new Map();
  let nextTimer = 0;
  let now = 0;
  let reloads = 0;
  let loader;
  let config;
  let progress;
  let resolve;
  let reject;
  let quitCalls = 0;
  function element(id) {
    if (!elements.has(id)) elements.set(id, {
      hidden: ['retry-button', 'error-details', 'connection-notice'].includes(id),
      textContent: '', value: 0, events: {},
      addEventListener(name, callback) { this.events[name] = callback; },
      focus() { this.focused = true; }
    });
    return elements.get(id);
  }
  const window = {
    document: {
      hidden: false,
      getElementById: element,
      createElement(tag) {
        if (tag === 'canvas') return { getContext: () => options.webgl === false ? null : { getExtension: () => null } };
        return {};
      },
      body: { appendChild(script) { loader = script; } }
    },
    WebAssembly: options.wasm === false ? undefined : {},
    navigator: { onLine: options.online !== false },
    location: { protocol: options.protocol || 'https:', reload() { reloads++; } },
    addEventListener(name, callback) { listeners[name] = callback; },
    setInterval(callback) { timers.set(++nextTimer, callback); return nextTimer; },
    clearInterval(id) { timers.delete(id); },
    console: { warn() {} },
    createUnityInstance(canvas, value, callback) {
      config = value;
      progress = callback;
      return new Promise((success, error) => { resolve = success; reject = error; });
    }
  };
  vm.runInNewContext(source, { window, Date: { now: () => now } });
  window.NeoConfiguratorLoader.start({ loaderUrl: 'Build/app.loader.js', dataUrl: 'Build/app.data' });
  return {
    window, element, listeners, timers,
    get loader() { return loader; },
    get config() { return config; },
    get reloads() { return reloads; },
    get quitCalls() { return quitCalls; },
    progress(value) { progress(value); },
    tick(milliseconds) { now += milliseconds; for (const callback of timers.values()) callback(); },
    async success() { resolve({ Quit() { quitCalls++; } }); await Promise.resolve(); await Promise.resolve(); },
    async failure(message) { reject(new Error(message)); await Promise.resolve(); await Promise.resolve(); }
  };
}

test('a successful load opens the workspace, clears the watchdog and focuses the canvas', async () => {
  const page = createPage();
  page.loader.onload();
  page.progress(0.5);
  assert.equal(page.element('loading-progress').value, 0.5);
  await page.success();
  assert.equal(page.element('loading-panel').hidden, true);
  assert.equal(page.element('unity-canvas').focused, true);
  assert.equal(page.timers.size, 0);
});

test('a missing loader offers reload with inert error text', () => {
  const page = createPage();
  page.loader.onerror();
  assert.equal(page.element('retry-button').hidden, false);
  assert.equal(page.element('error-details').hidden, false);
  assert.match(page.element('error-message').textContent, /app.loader.js/);
  page.element('retry-button').events.click();
  assert.equal(page.reloads, 1);
});

test('a rejected runtime startup reports the error and does not open the canvas', async () => {
  const page = createPage();
  page.loader.onload();
  await page.failure('<img src=x onerror=alert(1)>');
  assert.equal(page.element('loading-panel').hidden, false);
  assert.equal(page.element('retry-button').hidden, false);
  assert.equal(page.element('error-message').textContent, '<img src=x onerror=alert(1)>');
});

test('a long download can continue successfully after the reload option appears', async () => {
  const page = createPage();
  page.loader.onload();
  page.tick(45000);
  assert.equal(page.element('retry-button').hidden, false);
  page.progress(0.4);
  assert.equal(page.element('retry-button').hidden, true);
  await page.success();
  assert.equal(page.element('loading-panel').hidden, true);
});

test('offline and reconnect notices do not destroy an already running workspace', async () => {
  const page = createPage();
  page.loader.onload();
  await page.success();
  page.window.navigator.onLine = false;
  page.listeners.offline();
  assert.equal(page.element('connection-notice').hidden, false);
  assert.equal(page.element('loading-panel').hidden, true);
  page.window.navigator.onLine = true;
  page.listeners.online();
  assert.equal(page.element('connection-notice').hidden, true);
  assert.equal(page.reloads, 0);
});

test('context loss after startup explains unsaved-change risk and exposes reload', async () => {
  const page = createPage();
  page.loader.onload();
  await page.success();
  let prevented = false;
  page.element('unity-canvas').events.webglcontextlost({ preventDefault() { prevented = true; } });
  assert.equal(prevented, true);
  assert.equal(page.element('loading-panel').hidden, false);
  assert.match(page.element('loading-hint').textContent, /not saved/);
  assert.equal(page.element('retry-button').hidden, false);
});

test('a late successful startup cannot hide a fatal error', async () => {
  const page = createPage();
  page.loader.onload();
  page.config.showBanner('Fatal memory error', 'error');
  await page.success();
  assert.equal(page.element('loading-panel').hidden, false);
  assert.equal(page.quitCalls, 1);
  assert.equal(page.window.unityInstance, undefined);
});

test('unsupported browsers and file URLs get an explanation without downloading Unity', () => {
  for (const options of [{ wasm: false }, { webgl: false }, { protocol: 'file:' }]) {
    const page = createPage(options);
    assert.equal(page.loader, undefined);
    assert.equal(page.element('retry-button').hidden, false);
    assert.equal(page.element('loading-panel').hidden, false);
  }
});
