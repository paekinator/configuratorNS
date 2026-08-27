// Sets the browser's native CSS cursor on the game canvas, so hover states
// (pointer over buttons, text beam over inputs) look exactly like the rest
// of the web.
mergeInto(LibraryManager.library, {
  NeoSetCursor: function (ptr) {
    var style = UTF8ToString(ptr);
    var target = (typeof Module !== 'undefined' && Module.canvas)
      ? Module.canvas
      : document.querySelector('canvas');
    (target || document.body).style.cursor = style;
  }
});
