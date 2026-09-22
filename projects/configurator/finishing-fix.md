# Panel-aware finishing fix — 14 September 2026

## Top/bottom face correction — 15 September 2026

The first fix was too broad for horizontal frame faces beside a panel. New regressions reproduced **24 failures** across H7, H23 and T7, with upper, lower and paired panels and rotated bays. The previous runtime fixture exercised a shelf crossing a vertical post; it did not check these horizontal-face combinations.

The supplied simplified veneer is about 42.259 mm wide over a 41 mm frame. At a normal panel boundary, its lip and the panel can overlap by about 0.49 mm sideways and 0.3 mm vertically. This small seam was incorrectly treated as an obstruction to the entire top or bottom channel.

The planner now distinguishes a panel **beside** an exposed face from a panel **covering** that face. For a coplanar face, a board wholly beyond the actual 41 mm frame footprint does not hide the channel. The inward panel-facing channel is still masked. Boards crossing posts or extending over the actual frame face still use obstruction checks. Existing panel positions and the imported veneer geometry are preserved; this is a channel-occupancy correction, not a claim that the simplified models contain manufacturing chamfers.

Exposed V extrusion caps follow the same nominal-footprint rule beside a coplanar board; a board actually covering the extrusion face still masks the cap. Measured cap thickness remains part of the obstruction test.

The follow-up passed **833 checks** in Unity 6000.5.0f1: 566 core/geometry checks and 267 runtime checks. This includes **139 checks against actual generated H7/H23/T7 FBX meshes**, with no panels, upper/lower/paired panels, 0°/37° rotations and panel deletion. Tests also verify that a board covering the real frame face still suppresses finish. The earlier transverse post/shelf regression continues to pass. The known editor-only search-index diagnostic remains recorded separately.

I inspected the actual [top render](validation/channel-exposure/h7-paired-panels-above.png) and [underside render](validation/channel-exposure/h7-paired-panels-below.png); both show continuous exposed veneer coverage. This is rendering and channel-occupancy evidence, not a manufacturing-clearance certification for the simplified lip geometry.

Latest Windows candidate: `Builds/Windows-20260915-000654-709.zip` (60,690,254 bytes). It compiled with zero errors, passed a headless startup smoke check, and the temporary player was stopped. Build metadata records 859 warnings, including deprecated Unity API and shader warnings. SHA-256: `53a09b07693085b81dc850eb5ce76fde19df0257b60373060533c6fa80e1c9db`. Extract the whole ZIP before running. Restart Play Mode when testing the source in an already-open Unity editor.

Follow-up evidence is stored under `validation/channel-exposure`. No live website deployment was performed. The original results below remain the record of the earlier fix.

The reported defect was reproduced in geometry regressions against the previous implementation: a horizontal shelf crossing a post did not mask the post's veneers. The old planner also discarded an entire long veneer when a panel covered only part of it, and rotated panels were measured using inflated world-aligned bounds.

## Implemented behavior

- Every veneer candidate is checked against the panel's actual occupied range and physical volume before it is selected. The planner chooses available shorter veneers for the remaining exposed lengths, including gaps between separate panels. It never stretches or cuts an imported veneer to make it fit.
- Physical overlap checks include transverse shelves, all channel faces, and the real widths/thicknesses of the imported veneer and cap meshes. The numerical contact tolerance is 0.001 mm; the previous 1 mm allowance could ignore an entire thin board.
- Panel bounds are measured from each renderer's local geometry in the panel's own coordinate system, preserving thin boards when a piece is rotated.
- Finish refresh uses the frame and panel geometry, including orientation, dimensions and active state. Old finish objects are hidden immediately when replaced.
- End caps remain exclusive to exposed V-frame extrusion ends; H/T joint ends do not receive them. Ground-contact feet keep their existing placement.

## Supplied assets and reference

The source reference was `Downloads/NEOSPACE's Configurator for Rhino/NEOSPACE's Configurator for Rhino/docs/system/04-finishing.md`, plus its linked geometry and NSFINISH documentation. Its per-range panel masking and exposed-channel rules informed this fix.

All **15** files in `Downloads/Simplified Veneers/Simplified Veneers` match `Assets/Resources/Finish/Veneer H1.fbx` through `Veneer H15.fbx` byte-for-byte. Their SHA-256 comparisons are recorded in `validation/finishing/veneer-assets.json`. They were already installed, so no binary replacement or asset GUID change was necessary.

These supplied simplified assets are plain rectangular plates: approximately **42.2592 mm wide and 1 mm thick**, with visible length **132.9143 + 88 × (H − 1) mm**. The visible plate is about 2.0857 mm shorter than its nominal contact length. Cap Side is approximately 42.3 mm square. Runtime collision planning now uses measured imported dimensions, rather than treating every plate as exactly the nominal 41 mm frame profile.

The updated H1–H15 asset set takes precedence over the older reference's sparse size list. The supplied files contain no Inner, Outer or In/Out chamfer variants or underside wings; this fix uses the supplied geometry and does not invent those manufacturing details. It does not port Rhino's selection/command workflow or change the configurator's finish toggle behavior.

## Verification

The first regression run against the previous planner failed **15** checks, covering transverse shelf penetration, lost exposed channel lengths and rotated-board measurements. The same checks passed after the fix; see `validation/finishing/before.txt` and `after-plan.txt`.

The final Unity 6000.5.0f1 run passed **553 checks**: 425 core/geometry/serialization checks, 54 history/summary checks, 15 restore checks, 31 UI checks, and 28 physical finishing/refresh checks. The finishing-specific portion totals 91 checks. The report retains the previously observed Unity editor search-index diagnostic separately; there were no application test failures.

The actual V9 and supplied veneer/cap FBXs were rendered with a physical H7 board at a locking-hole offset of +20.5 mm, with 1 mm thickness. Independent renderer-bounds checks measured **zero overlaps** above a 0.005 mm penetration threshold. Both inward channels retained veneers above and below the shelf; the outward channels retained their long veneers. Deleting the board restored the original four H7 veneers. Centered panel resize/rotation and frame yaw also triggered regeneration without placement notifications. Rendered evidence is in `validation/finishing/01-finish-off.png` through `04-after-panel-delete.png`.

Windows candidate: `Builds/Windows-20260914-234533-433.zip`. Extract the whole archive and run `configurator.exe`. The non-development build completed with **zero errors** and 672 warnings (recorded in the isolated build log; existing obsolete API and shader warnings remain). Its executable passed a headless startup smoke test and the temporary player was stopped. The ZIP contains the executable and runtime dependencies and excludes Unity's DoNotShip output.

SHA-256: `eb3b5e964d911770b5613068570bb3cd0033f37025487763fa50a17baaaa2d48`.

Tests and builds ran in the isolated `tmp/task-validation` project, preserving the user's open Unity project and existing local work. Source scripts, prefabs, finish assets and scene matched the validated copy. The live website has not been updated; it still requires the WebGL build and website integration described in the task-resolution report.
