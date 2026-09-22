# Configurator task resolution

Source audit and implementation started 14 September 2026. Existing local edits were present in finish generation, history, capture/restore and Unity settings; those edits are retained. Status below distinguishes source changes from runtime verification and external product work.

Finishing follow-up later on 14 September: panel/veneer intersections and lost exposed channel lengths are fixed and verified with actual FBX meshes. See `finishing-fix.md` for the 553-check validation and newer Windows candidate `Builds/Windows-20260914-234533-433.zip`. The original delivery evidence below remains a record of the earlier task run.

15 September correction: the first finishing fix also hid top/bottom channels beside panels. That regression is corrected and now covered by actual H7/H23/T7 mesh checks and top/underside renders. The latest candidate is `Builds/Windows-20260915-000654-709.zip`; `finishing-fix.md` records the 833-check follow-up validation. Earlier build links below are historical.

## T-120 and T-107 — undo and header consistency

Reproduced on 14 September at https://neospace-xi.vercel.app/create/configurator?mode=blank in the Codex desktop browser: select Vertical frame, click the grid, choose V9 height; header shows 1 part / $35. Click Undo: frame disappears and a redo step is offered, but the header remains 1 part / $35, including after Escape and a later inspection. The deployed source revision is unknown. Current source watches attachment/panel version counters, but history completion does not consistently announce undo-to-empty/clear operations. A pending history capture can also miss very fast actions. Source changes add completion notifications, deferred summary refresh, rapid-action handling and finish state in snapshots. Actual-prefab Play Mode regression covers V9 placement, undo, redo, clear, delete and rapid undo. The exact old deployment cause cannot be conclusively attributed without its build revision.

## T-101 — source and PC access

Local source available: `https://github.com/paekinator/configuratorNS.git`, branch `main`. Unity is pinned by `ProjectSettings/ProjectVersion.txt` to **6000.5.0f1**. This Windows workspace provides source access and installed editor/build tooling. Existing interactive Unity sessions remain separate from the isolated validation copy under `tmp/task-validation`.

## T-102 — first development journey

Owner clarified: dimensions of available space → beginner template combinations → unique design → purchase, while retaining individual design and code sharing for a future design community. The first implemented slice adds space-size planning and live size feedback to existing guided tools, with save/reopen and a reviewable quote export. It does not automatically generate an optimised furniture design.

## T-108 and T-109 — save promise and recovery

Initial promise: explicit local piece saves, validated staged updates, retained previous copies, recovery notices, safe rejected loads, and portable configuration codes. Browser save success waits for IndexedDB acknowledgement. Save errors remain visible and users can preserve the code. Loading validates available parts before changing the scene and retains original objects until a complete replacement succeeds. Colours remain global preferences; the code currently stores the finish toggle, not individual palette choices. Cloud saves, account sync and continuous draft autosave are not implemented.

## T-110 — guided consumer journey

Existing guided frame/panel tools and individual Parts workflow remain. Space-size setup, fit feedback, build/save guidance and local quote review connect these into an initial consumer journey. The final purchase action depends on T-112. A template recommendation engine, constraint solver, curated furniture starter library and complete consumer checkout are further work, not represented by this slice.

## T-111 — presentation and material clarity

Existing named themes and swatches already provide colour previews. Source changes show selected panel and dressing names, translucent/opaque preview distinction and a request to confirm physical samples/material specifications. Estimates now include panels and installed finish parts using owner-requested temporary unit prices; unavailable frame or overridden prices remain explicit. Physical material fidelity and a broader visual redesign still require reference samples and real user evaluation.

## T-112 — authoritative commerce inputs

**Owner confirmed provider undecided; prepare quote export.** The repository has local AUD estimates, no connected commerce provider and no verifiable external authoritative price source. V11 has no listed price; twist beams inherit matching horizontal estimates. On 2026-09-15, the owner requested panels and used finish parts in the counts and pricing. Temporary AUD unit prices are panel $25, veneer $5, side/end cap $2, foot $5, editable on the scene's BuildStats component. Counts come from actual installed parts, including active Space merge results. See `Assets/Scripts/UI/PRICING.md`. These are placeholders, not an authoritative catalogue.

Required before checkout: provider and merchant account, approved catalogue source and revision, physical part/variant SKUs (including panels/caps/veneers), currency, tax/delivery/installation treatment, stock rules and server-side price confirmation.

## T-113 — purchase/quote handoff

Implemented first handoff: Piece Mode quote request with grouped frame, panel-size and installed finish quantities, AUD unit/line estimates and total, explicitly unpriced items/exclusions, colour notes and exact design code; users can copy/download a text file for their supplier. Clicking the count/price header or choosing Parts & prices opens the same inventory, also available for active merged Space parts. New/updated saved cards include finish counts. No messages are sent and no purchase is placed. Manufacturing approval and direct checkout remain outside this implementation.

## T-114 — device/browser matrix

Initial validation target is desktop Windows Chrome/Edge with keyboard/mouse at 1366×768 and 1920×1080, followed by macOS Safari/Chrome. See acceptance checklist. This is a proposed release matrix, not a claim of tested browser support. The supplied live URL was smoke-tested for T-107; the updated WebGL build and full browser matrix remain unverified.

## T-115 — WebGL loading and recovery

Custom Configurator template supplies progress and actionable recovery for loader/network/startup/graphics failures. A Node test harness exercises loader state transitions. The pinned editor installation lacks its WebGL build module, so browser player compilation and live end-to-end verification remain pending. The official module-installation dry-run passed, but the installation approval request was interrupted. A subsequent check confirmed the module is absent and no installer is running. Build preflight reports the missing module clearly. The live React wrapper also requires the website-source integration described in T-106.

## T-116 and T-117 — first professional desktop workflow/build

Windows x64 is the first build target: size a space, design a piece, arrange saved pieces, save/reopen, and export code/quote. A non-development build of version 0.1.0 succeeded with Unity 6000.5.0f1 and zero build errors. The downloadable candidate is `Builds/Windows-20260914-010742-710.zip` (60,687,566 bytes), with a sibling SHA-256 checksum. It excludes Unity's generated DoNotShip debugging directories. Extract the entire ZIP and run `configurator.exe`.

The compiled Windows player also started successfully in a temporary headless smoke run: it loaded the configurator scene, calibrated the template scale and initialised the grid/panel systems, with no logged startup exceptions or load failures. The temporary player was then stopped. This is startup evidence, not a rendered desktop usability check.

Build metadata reports 678 warnings; the log includes obsolete Unity API warnings and inference-package shader-variant warnings. These remain recorded in the build log; the package is a validation candidate, not a claim of a warning-free production release. CAD interchange, structural checks and a manufacturing-ready merged BOM are not implemented.

## T-118 — first customer session

Protocol and observation/release criteria are prepared in `acceptance-journey.md`. A real first-time participant is still required; an automated test cannot complete this task.

## T-119 — acceptance journey/release checklist

Earlier referenced document was not in this checkout. The local acceptance journey is now present, aligned to the owner's clarified product direction and explicit release gates.

## T-106 — separate website source

Owner supplied https://neospace-xi.vercel.app. The landing page and its Create a Space link successfully open /create/configurator?mode=blank. The page embeds a react-unity-webgl canvas, so the new standalone WebGL HTML template will not automatically replace that React wrapper's loading UI. Website source/repository and deployment access are still unavailable in this workspace; its wrapper needs corresponding integration before release.

## Verification record

Unity 6000.5.0f1 compiled the final application sources in the isolated validation project. The final test run passed **462 application checks**: 362 core/codec/persistence/quote/size checks, 54 real-prefab history/summary checks, 15 restore/rollback checks and 31 actual-scene UI wiring/rectangle checks at 640×480. The 12 JavaScript loader/storage/download tests also pass.

The test report separately retains a UnityEditor.Search.SearchDatabase startup exception with no project-script frames; it is an editor diagnostic, not silently discarded. Full logs are under `tmp/task-validation/Logs` and `tmp/validation-tests.log`. The UI checks verify runtime wiring, modal input isolation, quote content and rectangle containment; they do not certify rendered appearance, accessibility or the full desktop browser matrix.

Re-run JavaScript checks with `node --test Tools/test-web-loader.cjs Tools/test-web-bridges.cjs`. For Unity, use the pinned editor in batch mode with `-executeMethod ConfiguratorValidation.Run` and a dedicated `-logFile`; **omit `-quit`**, because the runner enters Play Mode and exits itself after completion. Run against a disposable project copy when an editor session is already open.

The deployed browser smoke test confirms the original T-107 defect; local changes have not been published. Build/package results and remaining release gates should be reviewed together with `acceptance-journey.md` and `Tools/BUILDING.md`.

Windows ZIP SHA-256: `83652706f90d30848a599166fb39d845828ab7e668b15af7e0ff7dbaa145076e`.

## 2026-09-15 — installed parts and pricing

The accepted finish geometry is retained. Panels, used veneers by size, caps and feet now contribute to the shared header/list/quote totals and new or updated saved cards. Temporary unit prices and editing instructions, the 1,025 passing checks, and the updated Windows package are recorded in [parts-pricing.md](parts-pricing.md). The latest package is Builds/Windows-20260915-003913-630.zip.

