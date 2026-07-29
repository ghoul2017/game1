mergeInto(LibraryManager.library, {
  P0ExposeWechatSdkUtf8RuntimeMethods: function () {
    // com.qq.weixin.minigame v0.1.31 的 TCPSocket 回调通过 Module 调用这两个 Unity 内部函数。
    Module.lengthBytesUTF8 = lengthBytesUTF8;
    Module.stringToUTF8 = stringToUTF8;
  }
});
