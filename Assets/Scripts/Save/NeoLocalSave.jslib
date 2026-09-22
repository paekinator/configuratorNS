mergeInto(LibraryManager.library, {
  NeoSyncLocalSaves: function (receiverPtr) {
    var receiver = UTF8ToString(receiverPtr);
    try {
      FS.syncfs(false, function (error) {
        SendMessage(receiver, 'OnStorageFlushed', error ? String(error) : '');
      });
    } catch (error) {
      SendMessage(receiver, 'OnStorageFlushed', String(error));
    }
  }
});
