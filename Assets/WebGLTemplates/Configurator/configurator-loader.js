(function (global) {
  "use strict";

  function start(settings) {
    var document = global.document;
    var canvas = document.getElementById("unity-canvas");
    var panel = document.getElementById("loading-panel");
    var title = document.getElementById("loading-title");
    var status = document.getElementById("loading-status");
    var progress = document.getElementById("loading-progress");
    var hint = document.getElementById("loading-hint");
    var retry = document.getElementById("retry-button");
    var details = document.getElementById("error-details");
    var errorMessage = document.getElementById("error-message");
    var notice = document.getElementById("connection-notice");
    var ready = false;
    var failed = false;
    var lastProgressAt = Date.now();
    var lastProgress = 0;
    var watchdog;

    function fail(heading, message, error) {
      if (failed) return;
      failed = true;
      global.clearInterval(watchdog);
      panel.hidden = false;
      notice.hidden = true;
      title.textContent = heading;
      status.textContent = message;
      hint.textContent = ready
        ? "Reloading may lose changes that were not saved. You can reopen a previously saved design after reloading."
        : "Check your connection and try again. If this continues, try another browser or contact the site owner.";
      progress.hidden = true;
      retry.hidden = false;
      if (error) {
        details.hidden = false;
        errorMessage.textContent = String(error && error.message ? error.message : error);
      }
      retry.focus();
    }

    retry.addEventListener("click", function () {
      // Reload the same origin/path. Never erase IndexedDB, localStorage or saved designs.
      global.location.reload();
    });

    function updateConnection() {
      if (failed) return;
      var offline = global.navigator.onLine === false;
      if (ready) {
        notice.hidden = !offline;
        notice.textContent = offline ? "You are offline. Keep this tab open and reconnect before using online features." : "";
      } else if (offline) {
        status.textContent = "You are offline. Reconnect to finish loading.";
        retry.hidden = false;
      } else {
        status.textContent = lastProgress >= 0.9 ? "Opening your workspace…" : "Loading the configurator…";
      }
    }
    global.addEventListener("offline", updateConnection);
    global.addEventListener("online", updateConnection);

    canvas.addEventListener("webglcontextlost", function (event) {
      event.preventDefault();
      fail("The 3D view was interrupted", "The browser lost its graphics connection. Reload to open the workspace again.");
    });

    if (global.location.protocol === "file:") {
      fail("Open the hosted configurator", "This 3D workspace needs a web address. Open it from the website instead of a downloaded HTML file.");
      return;
    }
    if (typeof global.WebAssembly !== "object") {
      fail("This browser cannot open the 3D workspace", "Use a browser with WebAssembly and WebGL 2 enabled.");
      return;
    }
    var probe;
    try {
      probe = document.createElement("canvas").getContext("webgl2");
    } catch (error) {
      probe = null;
    }
    if (!probe) {
      fail("The 3D view is unavailable", "Enable hardware acceleration in your browser, or try a different device with WebGL 2 support.");
      return;
    }
    var release = probe.getExtension("WEBGL_lose_context");
    if (release) release.loseContext();

    var config = {};
    Object.keys(settings).forEach(function (key) {
      if (key !== "loaderUrl") config[key] = settings[key];
    });
    config.showBanner = function (message, type) {
      if (type === "error") {
        fail("The configurator could not continue", "Reload the workspace to try again.", message);
      } else if (global.console) {
        global.console.warn(message);
      }
    };
    // Unity's fatal runtime errors are delivered separately from a rejected startup Promise.
    config.errorHandler = function (message, filename, line) {
      fail("The configurator stopped", "Reload the workspace to try again.", String(message) + (filename ? "\n" + filename + ":" + line : ""));
      return true;
    };

    // A slow connection gets an escape route without aborting a download that may still finish.
    watchdog = global.setInterval(function () {
      if (ready || failed || document.hidden) return;
      if (Date.now() - lastProgressAt >= 45000) {
        status.textContent = global.navigator.onLine === false
          ? "You are offline. Reconnect to finish loading."
          : "Loading is taking longer than usual. You can keep waiting or reload.";
        retry.hidden = false;
      }
    }, 5000);
    updateConnection();

    var script = document.createElement("script");
    script.src = settings.loaderUrl;
    script.onerror = function () {
      fail("The configurator could not load", "The download was interrupted or a required file is unavailable.", "Unable to load " + settings.loaderUrl);
    };
    script.onload = function () {
      if (failed) return;
      if (typeof global.createUnityInstance !== "function") {
        fail("The configurator could not load", "A required file did not load correctly. Reload to try again.");
        return;
      }
      try {
        global.createUnityInstance(canvas, config, function (value) {
          if (failed) return;
          if (value > lastProgress) {
            lastProgress = value;
            lastProgressAt = Date.now();
            retry.hidden = global.navigator.onLine !== false;
          }
          progress.value = Math.max(0, Math.min(1, value));
          updateConnection();
        }).then(function (instance) {
          if (failed) {
            if (instance && instance.Quit) instance.Quit();
            return;
          }
          ready = true;
          global.clearInterval(watchdog);
          global.unityInstance = instance;
          panel.hidden = true;
          updateConnection();
          canvas.focus();
        }).catch(function (error) {
          fail("The configurator could not open", "Some workspace files could not be loaded. Reload to try again.", error);
        });
      } catch (error) {
        fail("The configurator could not open", "Reload the workspace to try again.", error);
      }
    };
    document.body.appendChild(script);
  }

  global.NeoConfiguratorLoader = { start: start };
})(window);
