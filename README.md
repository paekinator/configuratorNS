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
- **Veneers**: rules defined; implementation in progress

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

## 7) Veneers (Automated Frame Finish)

### 7.1 Goal
Veneers are an **automated finish system** that covers every eligible exposed beam face area, **excluding occupied areas** (panel-facing exclusions, forbidden faces).

UI for now: one button  
✅ **“Apply Veneers”** → generate/update all veneers in the scene.

Users do not manually place veneer parts.

---

## 7.2 Veneer Coverage Domain (What gets veneered)
Veneers cover **all exposed beam faces**, with exceptions:

1) **Panel-facing surfaces**
- If a panel occupies a slot, the panel-facing inner faces of the framing beams receive **no veneers** on their panel-facing side (only that side/region).

2) **H beams**
- Top and bottom square faces (peg faces) → **no veneer**

3) **V beams**
- Top square face → **Top Cap**
- Bottom square face → **Foot**

---

## 7.3 Veneer Parts (Prefabs)
Prefabs provided:
- `H1 interior veneer` ... `H15 interior veneer`
- `H1 exterior veneer` ... `H15 exterior veneer`
- `top cap`
- `side cap`
- `foot`

Naming clarification:
- Veneer strips are named **H[number]** because **two strips equal the length of the corresponding H beam**.
- These strips apply to vertical beams as well (universal lengths), but veneer runs cannot cross beam-type boundaries.

---

## 7.4 Strip Length System (Sizing)
The lengths listed in the project spec are **BEAM lengths**, not veneer lengths.

Rule:
- `StripLength(Hk) = BeamLength(Hk) / 2`

Example:
- H3 beam length = 311 → H3 strip length = 155.5

If a veneer strip number is needed that does not exist as a beam, its strip length is defined by interpolation between neighbors.

---

## 7.5 Strip End Types + Connection Rules (Hard Rules)

### 7.5.1 Strip end types
Each strip has:
- **one Flat end**
- **one Functional (chamfered) end**

Interior strip:
- Functional end = **Accept**
- other end = **Flat**

Exterior strip:
- Functional end = **Cover/Tongue**
- other end = **Flat**

### 7.5.2 Strip-to-strip connections
✅ **Strips may connect to other strips ONLY via Flat ↔ Flat.**  
This is the only legal strip-to-strip connection.

❌ Not allowed:
- Interior Accept ↔ Interior Accept
- Interior Accept ↔ Exterior Cover
- Exterior Cover ↔ any strip end

So: **no cover/accept strip-to-strip joining**.

### 7.5.3 Passive Interior Endpoint (PIE) logic
A **Passive Interior Endpoint (PIE)** is an “interior endpoint surface” created by a **completed veneer set** (often involving caps).  
Exterior cover ends are allowed to terminate onto a PIE.

PIE is not “an interior strip by itself.” It is the endpoint behavior of a fully satisfied assembly.

### 7.5.4 Completion definition (No1 — Completion)
A veneer solution is valid only if:
- Every **Flat end** meets another **Flat end**
- Every functional end (Accept/Cover) terminates against a valid endpoint surface (caps / PIE)
- No strip end is left dangling
- No parts violate placement constraints (side caps only on holes)

No single strip can terminate alone.

---

## 7.6 Caps + Foot Rules

### Top Cap
- Interior type
- Covers the top face of the highest V endpoint
- Can act as an endpoint in completion logic

### Side Cap (easy wording)
- Interior-type cap used mainly as a **terminal endpoint for exterior veneers** (so exterior pieces have a valid place to end).
- Not exclusive to V beams — it’s commonly used at V beam endpoints, but the important idea is:
  - **side caps exist to provide a clean endpoint** so a veneer run doesn’t end “in the air”.
- Placement constraint: side caps can only be placed where there is a **hole attachment point**.
- Size: square side length **41**.

### Foot
- Neutral (no chamfer logic)
- Covers the bottom face of the lowest V endpoint

---

## 7.7 Run Segmentation Rule (Cannot cross beam types)
A veneer strip run cannot apply across different beam types.

Example: `H3 → V1 → H3`
- Veneer run cannot continue from the first H3 across the V1 to the second H3.

Priority behavior:
1) Solve/complete veneers on **V beam segments first** (vertical sections).
2) Then **H beams** treat the completed vertical veneer endpoints / PIE surfaces as valid termination surfaces.

This is the “puzzle” behavior: vertical completion creates valid endpoints for horizontal runs.

---

## 7.8 Optimization Rule (No2 — Minimize Part Count)
The veneer generator must always use the **minimum number of parts** while covering all eligible areas and satisfying No1 completion.

Implications:
- A run can be tiled with multiple strips, but always choose the combination with the fewest parts.
- Side caps only where genuinely required to satisfy endpoints.
- No redundant overlaps or double-coverage.

Example length decomposition:
- If a single face run length is **399**:
  - H7 strip = 663/2 = 331.5
  - H1 strip = 135/2 = 67.5
  - 331.5 + 67.5 = 399
  - Minimum parts = **2 strips** (H7 + H1), then endpoints must be resolved via caps/PIE.

---

## 7.9 Veneer Implementation Plan (Algorithm Shape)

### Inputs
- Placed beams (V/H) and their world transforms
- Exposed face regions per beam (minus exclusions)
- Panels and panel planes to exclude panel-facing sides
- Available veneer prefabs and their lengths
- Attachment point locations (for side cap eligibility)

### Outputs
- A set of instantiated veneer prefabs parented under `VeneersRoot`
- Deterministic placement so re-applying produces stable results

### Core steps (high level)
1) Collect geometry
   - Find all placed beams (exclude ghost layer)
   - Determine exposed faces via ray probes or visibility rules
   - Subtract panel-facing regions
2) Segment into “runs”
   - Each run is a contiguous exposed region along a single beam face
   - Runs must not cross beam-type boundaries
3) Solve each run
   - Determine run length in world units
   - Choose strip combo that sums to run length with minimum part count
   - Enforce chaining via **Flat↔Flat only**
4) Resolve endpoints
   - Place caps/foot/top cap where required and allowed
   - Allow exterior cover termination only onto PIE surfaces
5) Validate completion
   - No dangling flat ends
   - All required caps placed legally (side caps only at holes)
6) Instantiate
   - Spawn veneers with colliders disabled
   - Parent under VeneersRoot
   - Make re-apply idempotent (clear + rebuild, or diff update)

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

### Veneers (New)
- `VeneerPrefabLibrary.cs` (planned/new)
  - references to strip/cap prefabs
  - strip length measurement / mapping
- `VeneerManager.cs` (planned/new)
  - “Apply Veneers” pipeline
  - exposure + panel exclusion
  - run segmentation + minimum-part solving
  - PIE endpoint logic
  - instantiate + rebuild management

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

### Veneers (when implemented)
- A scene object `VeneerSystem` with:
  - `VeneerPrefabLibrary` assigned with all veneer prefabs
  - `VeneerManager` assigned referencing the library
- UI Button `Apply Veneers` calls `VeneerManager.ApplyVeneers()`
- Veneers must not interfere with build/selection:
  - colliders disabled (recommended)
  - not in selection raycast masks
  - not in overlap checks

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

### B) Veneers — Current Session Focus
- Implement `Apply Veneers` end-to-end
- Exposure detection + panel-facing exclusion
- Run segmentation (cannot cross beam types)
- Minimum-part solver for run length coverage
- Endpoint resolution:
  - top cap / side cap (hole-only) / foot
  - passive interior endpoint (PIE) logic
- Ensure veneers do not break Build/Select:
  - colliders off
  - not in raycast masks
  - not in overlap checks

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
