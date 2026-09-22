# ConfiguratorNS — README v2.2

Unity-based 3D configurator for assembling a modular **V/H beam** system with strict **hole + peg** snapping rules, dynamic **panel slot detection**, and a roadmap toward a **Part Library + Space Assembler** workflow (furnishing a room/apartment/office).

**UX goal:** smooth WYSIWYG workflow → hover ghost preview → clear valid/invalid feedback → click to place → selection + edit/delete → save/load → assemble multiple saved parts into a set space.

---

## 0) Project Status (Current)

### ✅ Implemented / Working
- Frame placement system (V/H) is functional (first placement + snapping rules)
- Ghost preview (valid/invalid feedback + click-to-place)
- Selection & erase with occupancy cleanup
- Panels — partially working
  - Wall slots detect and place panels
  - Floor/roof V+H rectangles detect reliably
  - Panels do not get destroyed if a slot disappears briefly during rescan (optional preserve)
  - Floor slot rotation is stable (no tilt/flip jitter)

### ⚠️ Still in progress / Known gaps
- H-only floor/roof rectangles are still problematic (top priority)
- “Any rectangle” generalization still needed (graph-based rectangle detection; interior beams must split rectangles)
- Persistence/save-load and undo/redo not implemented yet (or incomplete)
- **Finish (veneers, caps, feet)**: implemented on the Rhino NSFINISH rules — see §7

---

## 1) Core Concepts

### 1.1 Parts
- **Vertical beams (V#):** primary posts placed on floor or snapped to pegs.
- **Horizontal beams (H#):** connect by snapping a peg into a hole.

### 1.2 Attachment Points (AP)
Each beam prefab contains child objects with an `AttachmentPoint` component:
- `role = Hole` or `role = Peg`
- `isOccupied + occupant` track whether the point is used by a committed snapped connection
- Pairing/scanning may use proximity (`pegToHoleMatchDistance`) for inference, but gameplay truth should increasingly rely on committed connections.

---

## 2) Geometry Rules (Hard Rules)

### 2.1 V Beams (Vertical)
- Square cross-section
- Holes on **all 4 side faces**, vertical lines, uniform spacing
- No attachment points on top/bottom
- V beams are primary posts and can be placed freely on the floor as roots.

### 2.2 H Beams (Horizontal)
- Box-shaped, placed horizontally
- Pegs exist on only **2 opposite side faces**
- Holes exist on only **2 opposite side faces**
- H is a “two-face connector,” not 4-sided like V
- No attachment points on top/bottom
- H beams can only be placed by snapping a peg to a free hole.

### 2.3 Connection Rules
- Only **hole + peg joins** are allowed
- No double occupancy:
  - a hole cannot take two pegs
  - a peg cannot go into two holes
- Beams must not intersect (no clipping / overlap)

### 2.4 Twist Beams (Horizontal)
Twist beams are a special type of horizontal beam with **two different joint ends**:

- **End A** can only connect to **V beams** (V-only end)
- **End B** can only connect to **H beams** (H-only end)

#### How it works in the configurator
The end you place first determines what the other end becomes:

- If you snap the twist beam onto a **hole on an H beam**, then:
  - the snapped end is the **H-only end**
  - the **other end becomes V-only**, meaning only **V beam holes** can connect there

- If you snap the twist beam onto a **hole on a V beam**, then:
  - the snapped end is the **V-only end**
  - the **other end becomes H-only**, meaning only **H beam holes** can connect there

**Goal:** Twist beams enforce a controlled H↔V transition, preventing incorrect connections.

---

## 3) Placement Logic (Beams)

### 3.1 First Placement (No beams exist yet)
Applies to both V and H ghosts:
1. Raycast to floor using `floorMask`
2. Snap X/Z to `gridStep`
3. Place ghost at hit point
4. Compute bounds `minY` and shift upward so lowest point sits on `BuildSurface` with `firstSurfaceClearance`
5. Validate no illegal overlaps (excluding floor)

**Goal:** first beam never sinks into the floor; sits flush on BuildSurface.

### 3.2 Subsequent V Placement (Snap-to-Peg)
- Raycast into scene
- Find nearest free **Peg** in range (`v3MaxSnapDistanceToPeg`)
- Try candidate lowest holes on ghost + yaw angles (`v3YawAngles`)
- Align chosen hole to target peg
- Validate overlap rules

### 3.3 Subsequent H Placement (Snap-to-Hole)
- Raycast into scene
- Find nearest free **Hole** in range (`h3MaxSnapDistance`)
- Determine host face index from hole name `AP_SideN_*`
- Compute outward direction and final beam direction
- Rotate ghost into target rotation
- Try each free peg on H ghost; align it to host hole
- Validate overlap rules

---

## 4) Occupancy Rules (Critical)

### 4.1 What “occupied” means
A hole/peg is occupied if it is used by a **committed snapped connection**.

### 4.2 On placement commit
When placing a real beam:
- Mark the target scene AP as occupied:
  - `isOccupied = true`
  - `occupant = newBeamInstance`
- Record occupied scene points on the placed beam via:
  - `BeamConnections.RegisterOccupiedScenePoint(...)`

### 4.3 On delete
When deleting a beam:
- Release all `RegisterOccupiedScenePoint` points:
  - `isOccupied = false`
  - `occupant = null`

### 4.4 Key invariant
Occupancy must remain consistent even after:
- slot scans
- selection delete
- scene reload (future save/load)

---

## 5) Panels (Goal + Constraints)

### 5.1 Fundamental Requirement
✅ Panels must be placeable in **ANY rectangular opening**, not just specific patterns.

A “rectangular opening” means:
- 4 edges form a closed loop
- corners are nodes; edges are beam spans
- opening lies on a plane:
  - Wall panel: vertical-ish
  - Floor/Roof panel: horizontal-ish
- rectangle may be formed by:
  - V + H + V + H
  - H-only cycles (H connected to H)
  - mixed corner nodes (V roots or H roots)

### 5.2 Two-Sided Panels per Slot
Each detected slot supports two panels:
- `panelPlus` (normal side)
- `panelMinus` (opposite normal side)

### 5.3 Block Frames When Panel Exists
If any panel exists in a slot, frame placement inside that slot must be blocked:
- Slot spawns a `PanelBlocker` collider aligned to rectangle plane
- BuildController treats PanelBlocker collisions as illegal overlap
- When panels removed and slot empty → blocker turns off

### 5.4 Panel Fit Rules
Panel is sized to inner opening:
- `innerW = slotWidth - 2*(frameThickness + panelInsetX)`
- `innerH = slotHeight - 2*(frameThickness + panelInsetY)`

Panel offset to avoid z-fighting:
- `offset = panelOutset + panelGap + panelThickness/2`

---

## 6) Panel Slot Detection (Current Approach + Implemented Fixes)

### 6.1 Current approach (high level)
- Rebuild peg↔hole pairing each scan using proximity (`pegToHoleMatchDistance`)
- Build spans from paired endpoints
- Infer rectangles for wall and floor/roof

### 6.2 Implemented stability fixes (current code)
Implemented in `PanelSlotManager.cs`:
- **Mixed V+H corner identity for floor slots**
  - If an H endpoint connects to a V post, snap join XZ to V pivot to avoid 4-face holes producing multiple corner nodes.
- **Floor corner keys ignore Y**
  - Floor separation uses Y-bucketing (`floorYBucketSize`), node identity uses XZ.
- **Collinear chain walking**
  - If extra connections split a perimeter side, detection can still find the rectangle via collinear shortcuts.
- **Deterministic floor rotation**
  - Floor/roof slots force:
    - `normal = Vector3.up`
    - `upAxis = Vector3.forward`
    - axis-aligned rectangle geometry using min/max XZ extents for stable scale/rotation.
- **Preserve panels if slots flicker**
  - If `preservePanelsOnSlotLoss = true`, panels are not destroyed during temporary slot loss; they auto-reattach when slot returns.

### 6.3 Required improvements (still)
Slot detection must generalize to “any rectangle”:
- Nodes can be V roots OR H roots
- Edges should represent actual committed connections (not just proximity)
- Use graph cycle detection to find planar 4-cycles (rectangles)
- Validate rectangle geometry:
  - adjacent edges perpendicular-ish
  - opposite edges parallel-ish
  - coplanar within tolerance (floor bucket; wall plane tolerance)
- Robust dedupe with stable slot IDs
- **Interior beams must split rectangles**:
  - if a beam splits a rectangle, detect two smaller rectangles, not one big one

---

## 7) Finish (Veneers, Caps, Feet)

The **Finish** toggle (Parts tab) dresses every placed frame automatically and
keeps the dressing in step with the build; Space Mode pieces are always
dressed. The rules are the NEOSPACE Rhino NSFINISH rules
(`docs/system/04-finishing.md` of the Rhino configurator), adapted to the
supplied simplified meshes.

### 7.1 Parts and models
- Models load from `Assets/Resources/Finish`: `Veneer H1` … `Veneer H15`,
  `Cap Side`, `Cap End`, `Foot`. Every produced veneer size is listed in
  `CatalogueData.VeneerLengths` (1–15); adding a size means adding the FBX
  and the number there.
- A veneer of size n covers **n + 1 modules** of one channel: contact length
  `(n + 1) × 88 − 41` mm. The simplified plates are 1 mm thick, 42.26 mm
  wide (a 0.63 mm lip past the 41 mm profile) and 2.09 mm shorter than the
  contact length, so a hairline reveal at each end is by design.
- Cap Side and Cap End are 42.3 mm squares; the Foot is 41 × 41 × 10 mm.

### 7.2 Rules (from the Rhino reference)
- **V posts**: Cap End on the top extrusion end (and on a floating bottom),
  Foot under a grounded bottom; the visible floor sinks by the foot height.
  Every channel is divided at the two end holes and at **every connection
  level of the post** — a Cap Side sits at each divider on every free face
  (never on the face a joint occupies) and veneers fill the sections between
  (`Finishing.CoverSegment`: fewest parts, then most balanced). A level pair
  one module apart drops the optional divider instead of leaving a bare
  strip (`FinishGenerator.SplitAllChannelsAtConnections`).
- **H / twist beams**: default coverage of the body on all four channels
  (one veneer, or two with a mid Cap Side); body ends are joints and never
  capped. The covering breaks where a body presses on a face: stacked
  posts, and connectors plugging into the beam's own side holes.
- **Panels**: a channel facing into a panelled bay is skipped over the
  length actually behind the board, never the whole channel; boards beside a
  face (edge within 2 mm of the profile, `SeamToleranceMm`, which absorbs the
  whole-millimetre code quantization) do not hide it, boards covering a
  face do. A single-sided bay shows the bare inward channels from its open
  side, exactly like the physical product.
- Parts dedupe by position, so re-applying never stacks duplicates.

### 7.3 Implementation
- `FinishGenerator` (pure planning from frame/panel records),
  `FinishController` (model measurement, placement, live refresh, floor
  drop), `FinishPanelMasking` (exact swept-box panel tests),
  `NeospaceCore/Finishing` (coverability maths ported from Rhino).
- Verification: **Tools → Configurator → Run Finish Diagnostics** builds a
  wall bay, single-sided bay, two-level shelf ring, V17 cabinet, twist
  branch, a yawed bay and a Space-Mode arrangement, then writes a per-channel
  coverage report and renders to `Logs/finish-diagnostics`. The self-tests
  and play-mode regressions run through `ConfiguratorValidation.Run`.
- `Assets/Scripts/Veneer/` (VeneerManager, VeneerPrefabLibrary) is the
  retired strip-based prototype and is not used.

---

## 8) Scripts & Responsibilities (High Level)

### Build / Placement
- `BuildController.cs`
  - ghost pose computation for V/H
  - placement commit
  - overlap checks including PanelBlocker
  - occupancy mark on involved APs
- `GhostController.cs`
  - ghost visuals and placement feedback
- `PartDatabase.cs`
  - partId → prefab mapping

### Occupancy
- `AttachmentPoint.cs`
- `BeamConnections.cs`
- `OccupancySanitizer.cs`

### Panels / Slots
- `PanelSlotManager.cs`
  - slot scanning lifecycle
  - SlotTrigger + PanelBlocker creation
  - two-sided panel placement
  - panel persistence across slot flicker (optional)
- `PanelSlotHandle.cs`
  - runtime slot data
- `PanelInstance.cs`
  - slotId + side metadata

### Selection / UI
- `SelectionManager.cs`
  - select / erase placed beams and panels
  - deletion routes through cleanup (occupancy + panel detach rules)
- UI:
  - `UIInteractionState.cs`
  - `UIPartsPalette.cs`
  - `UIStatusBar.cs`
  - `UIToolbarController.cs`
  - `PartSelectorUI.cs`

### Finish
- `Finish/FinishGenerator.cs` — pure planner (Rhino NSFINISH rules on frame/panel records)
- `Finish/FinishController.cs` — model measurement, placement, live refresh, floor drop
- `Finish/FinishPanelMasking.cs`, `NeospaceCore/Finishing.cs` — panel sweep tests, coverability maths
- `Finish/FinishDiagnosticsHost.cs` + `Editor/FinishDiagnostics.cs` — coverage report and renders
- `Veneer/VeneerManager.cs`, `Veneer/VeneerPrefabLibrary.cs` — retired prototype, unused

---

## 9) Unity / Inspector Setup Checklist

### Required Layers
- `Ghost`
- `PanelBlocker`
- `SlotTrigger` (slot selection/hover)
- `Panel`

### BuildController
- `cam` assigned
- `floorMask` set to BuildSurface
- `placementRayMask` excludes Ghost
- `ghostLayerMask` set to Ghost
- `panelBlockerMask` set to PanelBlocker

### PanelSlotManager
- `panelPrefab` assigned
- `slotTriggerLayer`, `panelLayer`, `panelBlockerLayer` assigned
- `pegToHoleMatchDistance` small (e.g., 0.01–0.03)
- `floorYBucketSize` tolerant (e.g., 0.02)
- `preservePanelsOnSlotLoss` ON (recommended)

### Finish
- No scene setup needed: `FinishController` bootstraps itself and loads models from `Resources/Finish`
- The Parts tab **Finish** card toggles it; Space Mode is always finished
- Finish parts are pure dressing: colliders disabled, no part identity, parented under `FinishRoot`

---

## 10) Roadmap: From Configurator to Platform (Parts → Space)

### Mode A — Part Builder
Users build a single reusable object (chair/table/shelf module) using beams + panels + veneer.

Outputs:
- Save as `PartDesign` (blueprint)
- Thumbnail preview
- Metadata: name, category, bounds (W/H/D)

### Mode B — Space Assembler (Set Space Furnishing)
Users create a set space (room/apartment/office) and place multiple saved parts.

Set Space definition:
- Floor footprint (rectangle W×D)
- Ceiling height
- Optional obstacles (columns, built-ins), wall segments

Assembly behaviors:
- Insert `PartInstances` from library
- Move/rotate/duplicate
- Snap to floor/walls/edges (later)
- Collision + clearance validation (walkways, door swings later)
- Save as `AssemblyDesign`

Edit workflow:
- Clicking an instance offers:
  - Move/Rotate/Duplicate
  - Edit Part:
    - update original (all instances) OR
    - fork “Save As New Part”

---

## 11) Current Task List (Live)

### A) Panels — Top Priority
- H-only floor/roof rectangles
- stable corner identity in pure-H cycles
- fix “gap on one side” in certain H-only rectangles
- “Any rectangle” graph cycle detection
  - planar 4-cycles
  - geometry validation + coplanarity
  - robust dedupe + stable IDs
- interior beams must split rectangles
- slot selection overlap edge cases

### B) Finish — Done
- Finish toggle dresses the build on the Rhino NSFINISH rules (§7); verify with **Tools → Configurator → Run Finish Diagnostics**

### C) Persistence — Save/Load (Near-Term)
- Save beam placement: partId, transform, connection info
- Save panels: slotId, side, dimensions, transforms
- Load restores structure, occupancy, panels/blockers
- Schema versioning

### D) Undo / Redo (High Impact UX)
- Command stack: place/delete beam, place/delete panel, future transforms
- Undo restores occupancy + panels correctly

### E) Part Library + Space Assembler (Platform Backbone)
- PartDesign save/load + thumbnail
- Part Library UI + spawn PartInstances
- Space creation UI + assembly save/load

### F) Premium UX (Later)
- Measurement overlay
- Camera presets
- Move/rotate beams (if needed)
- “Why placement failed” diagnostics

---

## 12) Known Gotchas / Notes
- Ghost layer must be excluded from:
  - placement raycasts
  - overlap checks
- Overlap checks should ignore:
  - floor
  - attachment points
  - self-root
  - ghost layer
- Slot detection should increasingly rely on committed connections instead of proximity-only pairing (reduce flicker)
- Veneers must be non-interactive:
  - no colliders (or on ignored layers)
  - do not participate in selection
  - do not participate in overlap checks

---

## 13) Guided Template Tools (Additive)
Faster NST-style templates for beginners. **Expert Build/Select is unchanged.**

- Switch with the **Templates** / **Parts** tabs at the top of the left panel
  (same scene; Parts is the original palette).
- Tools: Posts (T1), Connectors (T2), Panel (T3). Connectors and Panel anchor
  on **post holes only**: pick a hole on one post, then one on a second post at
  the same height; the Panel's third pick goes **up/down** a post (vertical wall
  panel, 2 beams + panels) or **sideways to a third post** (horizontal panel,
  4-beam ring + panels — the fourth corner post must already exist).
- World scale: prefabs are 1 unit = 100 mm; `NeospaceUnits` auto-calibrates
  from a V prefab at startup so guides and math match true frame size.
- Floor grid: **Tools → Configurator → Style Environment** regenerates the
  floor with one cell per 88 mm module, bold line every 8 modules (704 mm).
- Strict rules: connector picks snap to real free holes/pegs, spans must match
  catalogue sizes, and every connector commits through the same
  hole/peg + overlap pipeline as Expert placement (no freehand poses).
- Measure line: while picking, a thick axis-locked guide line shows tick marks at
  every catalogue length and labels the active snap (e.g. `H7 · 663 mm`). Post
  height is the last click — aim up and the line snaps through V sizes. Styled
  with the Evo UI kit (Inter font + accent colour) when present.
- Pure catalogue math: `Assets/Scripts/NeospaceCore/`
- Guided UX: `Assets/Scripts/Templates/` (see `GUIDED_TEMPLATES.md`)
- Batch place API: `BuildController.TemplateBatch.cs` (new partial only)
- Selftest: **Tools → Configurator → Run NeospaceCore Selftest**
- Branch: `feature/guided-templates`

---

## 14) Versions and releases

- The source of truth is git on GitHub (`origin`, https://github.com/paekinator/configuratorNS). `main` is the
  latest accepted state; features land through branches merged into it.
- Every version worth keeping is an **annotated tag on `main`** (`git tag -a v1.0.0-beta.1 -m "..."`;
  `v1.0.0-beta.1` is the 22 Sep 2026 configurator beta 1.0.0). Tag first, build from the tag: any tag can be
  rebuilt into the identical player later, so builds do not need to be archived for every commit.
- Builds are outputs, not sources: `Builds/` stays ignored. To publish a version run
  `Tools/Release-Version.ps1 -Tag v1.2.3 -Message "..."`: it tags, exports exactly that tree, builds it with the
  version stamped into the player, packages ZIP + `.sha256`, pushes the tag and creates the **GitHub Release**
  (with GitHub CLI signed in; otherwise it prints the upload page). Release notes come from the tag's section
  in `CHANGELOG.md`, so add that section first.
- Scenes, prefabs and assets are Unity YAML: `.gitattributes` routes them through Unity's SmartMerge. Each
  clone needs the driver once:
  `git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"`.
  When SmartMerge cannot settle a file it reports a conflict and leaves the local version; take the richer
  side and re-apply the small local change by hand (that is how the UI redesign scene was merged).
- Validation before tagging: `ConfiguratorValidation.Run` and `FinishDiagnostics.Run` in batch mode on an
  isolated copy of the project (`tmp/task-validation`), never on the project the editor has open.
