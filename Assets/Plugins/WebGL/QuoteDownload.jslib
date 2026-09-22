mergeInto(LibraryManager.library, {
  NeoDownloadQuote: function (filename, content) {
    var blob = new Blob([UTF8ToString(content)], { type: 'text/plain;charset=utf-8' });
    var url = URL.createObjectURL(blob);
    var link = document.createElement('a');
    link.href = url;
    link.download = UTF8ToString(filename);
    try {
      document.body.appendChild(link);
      link.click();
    } finally {
      link.remove();
      // Give the browser time to consume the Blob; release it even if download was blocked.
      setTimeout(function () { URL.revokeObjectURL(url); }, 60000);
    }
  }
});
