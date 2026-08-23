// Writes text to the browser clipboard. GUIUtility.systemCopyBuffer is a
// no-op in WebGL builds, so sharing codes needs the real clipboard API.
mergeInto(LibraryManager.library, {
  NeoCopyText: function (ptr) {
    var text = UTF8ToString(ptr);
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(function () {
        // Fall through to the legacy path below on rejection.
        legacyCopy();
      });
      return;
    }
    legacyCopy();

    function legacyCopy() {
      var area = document.createElement('textarea');
      area.value = text;
      area.setAttribute('readonly', '');
      area.style.position = 'absolute';
      area.style.left = '-9999px';
      document.body.appendChild(area);
      area.select();
      try { document.execCommand('copy'); } catch (e) {}
      document.body.removeChild(area);
    }
  }
});
