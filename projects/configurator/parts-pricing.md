# Used parts and temporary pricing — 2026-09-15

Panels and installed finish parts now contribute to the header count and AUD estimate. Click the header or select **Parts & prices** from the menu for grouped frames, panel sizes, each veneer size, side/end caps and feet. The list shows quantities, unit prices, line totals and the combined estimate. The Piece Mode quote export uses the same snapshot. New and updated saved-piece cards include finish counts.

Temporary AUD prices are $25 per panel, $5 per veneer, $2 per side/end cap and $5 per foot. Existing frame estimates are retained. Edit **BuildStats → UI Build Stats** in the scene Inspector outside Play Mode and save the scene. Exact part-ID rows override category defaults. See [price editing instructions](../../Assets/Scripts/UI/PRICING.md).

The collector reads active installed objects, including actual Space merge results. It excludes placement previews, hidden replaced panels/frames and old finish objects awaiting destruction. Immediate finish synchronization keeps save/quote counts current. Space deletion now deactivates discarded instances immediately, and its summary remains clickable. Price changes do not regenerate geometry.

Validation passed in the isolated Unity 6000.5.0f1 project: **1,025 checks, zero application failures** — 581 pure checks, 54 history/summary, 15 restore, 43 UI wiring/layout, 28 physical finishing, 139 top/bottom channel exposure, 155 installed parts/pricing and 10 real Space merge/pricing checks. Coverage includes finish off/Undo/Redo, actual FBX counts, rotated panel-size grouping, ghosts, exact overrides, decimal rounding, invalid and zero prices, physical deduplication, replacement panel strips and same-frame deletion. Every C# source file matched the tested copy. UI validation includes actual header raycasts/click dispatch and modal contents/layout at 640 × 480; it is not a rendered usability review.

The known editor-only Unity SearchDatabase indexing exception was recorded separately. The non-development Windows build succeeded with zero errors and 676 warnings (existing obsolete API/shader warnings). Test results and build metadata are in [validation/parts-pricing](validation/parts-pricing).

Updated Windows package: `Builds/Windows-20260915-003913-630.zip` (60,694,492 bytes). SHA-256: `fd1db63d1541f259f79017f55c9cd268c71edb806ee4c45ef533ffdf1ecfb3b8`. The ZIP includes `configurator.exe` and excludes Unity DoNotShip debug folders. Extract the whole ZIP before launching. The compiled player loaded the configurator scene and initialized its systems in a headless startup smoke check, with no logged exceptions, errors or load failures; the owned smoke-test process was then stopped.

Prices remain placeholders; delivery, installation, unlisted accessories and tax adjustments are excluded. Legacy saved cards retain their old metadata until opened and updated. No checkout or live website deployment was performed.
