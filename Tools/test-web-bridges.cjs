const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const test = require('node:test');

function load(relativePath, globals) {
  const library = {};
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, '..', relativePath), 'utf8'), {
    LibraryManager: { library }, mergeInto: Object.assign, UTF8ToString: value => value, ...globals
  });
  return library;
}

test('local saves are acknowledged only after the browser filesystem callback', () => {
  const messages = [];
  let finish;
  const bridge = load('Assets/Scripts/Save/NeoLocalSave.jslib', {
    FS: { syncfs(populate, callback) { assert.equal(populate, false); finish = callback; } },
    SendMessage: (...args) => messages.push(args)
  });
  bridge.NeoSyncLocalSaves('LocalSavePersistence');
  assert.equal(messages.length, 0);
  finish(null);
  assert.deepEqual(messages, [['LocalSavePersistence', 'OnStorageFlushed', '']]);
});

test('both asynchronous quota errors and synchronous filesystem exceptions reach the save UI', () => {
  for (const synchronous of [false, true]) {
    const messages = [];
    const bridge = load('Assets/Scripts/Save/NeoLocalSave.jslib', {
      FS: { syncfs(populate, callback) {
        if (synchronous) throw new Error('Storage unavailable');
        callback(new Error('Quota exceeded'));
      } },
      SendMessage: (...args) => messages.push(args)
    });
    bridge.NeoSyncLocalSaves('LocalSavePersistence');
    assert.equal(messages.length, 1);
    assert.match(messages[0][2], synchronous ? /Storage unavailable/ : /Quota exceeded/);
  }
});

function quoteEnvironment(blockClick) {
  let blob;
  let attached = false;
  let clicked = false;
  let removed = false;
  let revoked;
  let release;
  let delay;
  const link = {
    click() { clicked = true; if (blockClick) throw new Error('Download blocked'); },
    remove() { removed = true; }
  };
  const bridge = load('Assets/Plugins/WebGL/QuoteDownload.jslib', {
    Blob,
    URL: {
      createObjectURL(value) { blob = value; return 'blob:local-quote'; },
      revokeObjectURL(value) { revoked = value; }
    },
    document: {
      createElement(tag) { assert.equal(tag, 'a'); return link; },
      body: { appendChild(value) { attached = value === link; } }
    },
    setTimeout(callback, milliseconds) { release = callback; delay = milliseconds; }
  });
  return {
    bridge, link, get blob() { return blob; }, get attached() { return attached; },
    get clicked() { return clicked; }, get removed() { return removed; },
    get revoked() { return revoked; }, get delay() { return delay; }, release() { release(); }
  };
}

test('quote download preserves Unicode plain text and releases its temporary object URL', async () => {
  const page = quoteEnvironment(false);
  const content = 'NEOSPACE — quote\nV3 × 2\nNS1-test';
  page.bridge.NeoDownloadQuote('neospace-quote.txt', content);
  assert.equal(page.link.download, 'neospace-quote.txt');
  assert.equal(page.blob.type, 'text/plain;charset=utf-8');
  assert.equal(await page.blob.text(), content);
  assert.equal(page.attached, true);
  assert.equal(page.clicked, true);
  assert.equal(page.removed, true);
  assert.equal(page.delay, 60000);
  assert.equal(page.revoked, undefined);
  page.release();
  assert.equal(page.revoked, 'blob:local-quote');
});

test('a blocked download also removes the anchor and schedules Blob cleanup', () => {
  const page = quoteEnvironment(true);
  assert.throws(() => page.bridge.NeoDownloadQuote('quote.txt', 'test'), /Download blocked/);
  assert.equal(page.removed, true);
  page.release();
  assert.equal(page.revoked, 'blob:local-quote');
});
