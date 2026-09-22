# Configurator build and platform runbook

Use the editor version recorded in `ProjectSettings/ProjectVersion.txt` (currently **6000.5.0f1**). The source remote is `https://github.com/paekinator/configuratorNS.git`; the audit was performed on local branch `main`. The Unity project is available locally. The user supplied the live site at [neospace-xi.vercel.app](https://neospace-xi.vercel.app). Its React wrapper uses `react-unity-webgl`; the separate website source repository and hosting access have not been identified.

## Build commands

Run from the repository root in PowerShell. Close this project's Unity editor first, or run against an isolated copy of the project; a second editor cannot safely share its Library.

```powershell
./Tools/Build-Configurator.ps1 -Target Windows -CheckOnly
./Tools/Build-Configurator.ps1 -Target Windows
./Tools/Build-Configurator.ps1 -Target WebGL -CheckOnly
./Tools/Build-Configurator.ps1 -Target WebGL
```

The wrapper finds the pinned Unity editor in Unity Hub's normal Windows installation directory; `-UnityEditor 'C:/path/to/Unity.exe'` supports another installation. `-Development` creates a development build. Every invocation writes to a new timestamped directory in `Builds`, logs to `Logs`, and verifies the build report. Windows builds also produce a ZIP and SHA-256 file. Extract the entire ZIP and run `configurator.exe` with its adjacent data and runtime files; copying only the EXE is insufficient.

The equivalent Unity `-executeMethod` entrypoints are `ConfiguratorBuild.BuildWindows` with `-buildTarget Win64`, and `ConfiguratorBuild.BuildWebGL` with `-buildTarget WebGL`. An optional `-configuratorOutput` specifies a subdirectory of the project's `Builds` directory. The editor menus are under **Tools → Configurator → Build**.

Both entrypoints explicitly build `Assets/Scenes/ConfiguratorScene.unity`, keeping the unrelated vendor demo out of release outputs without editing the project's scene list. The Web entrypoint temporarily selects `PROJECT:Configurator` and restores the prior template afterward. Other player settings and scenes are preserved. These scripts produce build candidates; they do not publish, sign an installer, configure commerce, or certify platform compatibility.

## Current module availability

The inspected PC has Unity **6000.5.0f1** and **6000.5.10f1**. Both have Windows Standalone support, including x64 Mono player files. Neither has WebGL support. The Windows preflight succeeds. The Web preflight exits with an explicit missing-module error. Add **Web Build Support** to the pinned editor through Unity Hub before making a full browser build; do not upgrade the project to work around the missing module.

On 2026-09-14, the official Unity CLI bundled with Hub **3.21.0** successfully completed an installation dry-run for `6000.5.0f1` and module `webgl`. The pinned editor's `modules.json` provides the official installer for revision `88b47c5e7076`: **1,105,649,928 bytes** to download and **5,933,445,556 bytes** installed. Approximately **555 GB** was free. The module metadata lists no additional EULA; no automatic EULA-acceptance flag was used.

The actual installation required sandbox approval to write into Program Files. That approval request was interrupted and produced no installation output. A subsequent check confirmed the WebGL module was still absent and no module installer, elevation helper or bundled Unity CLI process was running. No editor version was changed, no existing Unity/Hub process was stopped, and the install was not retried after the interruption.

The verified dry-run command is:

```powershell
& 'C:/Program Files/Unity Hub/resources/cli/unity.exe' install-modules --editor-version 6000.5.0f1 --module webgl --dry-run --non-interactive --format json --no-banner
```

After installation is authorized, the same official command without `--dry-run` installs only the selected module. See the [Unity CLI reference](https://docs.unity.com/en-us/unity-cli/unity-cli-reference). Full generated WebGL compilation remains unverified until the module is installed.

## Web loader and hosting checks

The `Assets/WebGLTemplates/Configurator` template supplies real download progress, slow-load guidance, a reload action, unsupported-browser guidance, offline/reconnect status, rejected-load errors and graphics-context-loss handling. A stalled load can still complete. Reload uses the same URL and does not clear local saves. After a runtime interruption the page explains that changes without a confirmed save may be lost.

This is the standalone Unity HTML template. The current live React wrapper does not consume that template. Its website source must integrate equivalent progress, error and recovery behavior, or explicitly embed the generated standalone page. Updating Unity assets alone does not deploy the loading-screen improvements to the existing React wrapper. No public deployment was performed in this task.

The template's recovery logic and download/save JavaScript bridges can be checked independently of Unity:

```powershell
node --test Tools/test-web-loader.cjs Tools/test-web-bridges.cjs
```

Those tests exercise a simulated browser API and generated-runtime promise. They do not replace testing a generated WebAssembly build in real browsers.

Deploy the complete Web build directory to the eventual approved host. Serve the page over HTTP(S), not `file://`. Preserve the existing public origin and application path when possible because browser save storage is scoped to them. Check that generated loader, framework, data and WASM URLs resolve, that MIME/compression headers match the actual output, and that deployment updates replace the HTML and its referenced assets together. The project currently enables decompression fallback; the build script preserves that choice. Test a normal load, interrupted connection, missing build asset, reload, save/reopen and a graphics-context interruption on the actual host before release.

Unity reference: [template variables](https://docs.unity3d.com/6000.0/Documentation/Manual/web-templates-variables.html), [runtime instantiation](https://docs.unity3d.com/6000.0/Documentation/Manual/web-templates-structure.html), [custom error handling](https://docs.unity3d.com/6000.0/Documentation/Manual/web-interacting-browser-error-handling.html), and [BuildPipeline.BuildPlayer](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/BuildPipeline.BuildPlayer.html).

## Initial validation scope

The first release candidate targets **Windows x64 with mouse and keyboard**, using the existing guided Tools and Parts modes, named local pieces, code backups, space assembly, and quote-request export. Installer signing and automatic updates require separate release configuration.

Browser acceptance targets for this slice are **Chrome and Edge on Windows**, at 1920 × 1080 and 1366 × 768, plus a narrower viewport to check whether essential controls remain reachable. The installed versions observed during the audit were Chrome **152.0.7977.83** and Edge **153.0.4234.32**. Run the build/save/reopen/undo/quote journey in both and record the browser version with the result. Availability is not a passing result.

Safari on macOS and Firefox are subsequent desktop compatibility checks. Touch-only phones/tablets and a native macOS build need their own workflow validation before being advertised. A real first-time customer usability session still requires a participant; a scripted smoke test is engineering evidence, not a substitute for that session.
