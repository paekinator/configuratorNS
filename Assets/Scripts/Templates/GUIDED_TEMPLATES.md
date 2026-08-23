# Guided Template Tools

Additive extension for less-experienced users. **Expert mode (default) is unchanged.**

## How to use

1. Enter Play Mode in `ConfiguratorScene`.
2. The left panel has two tabs at the top: **Tools** (guided tools, with a
   wrench icon) and **Parts** (the original piece-by-piece palette).
   Click **Tools**.
3. Choose a tool (each has its own icon on the button; "frame" is the
   NEOSPACE word for a vertical V part — the wording deliberately avoids
   "post"):
   - **Frames** — base → base → depth → height (click the same spot to skip a dimension). Bases snap to the 88 mm grid and spacings snap to catalogue H spans. Ghost frames stand at every planned corner while you pick (V1 during the base picks, then the ghosts grow live through the V sizes as you aim the height), so you always see exactly what will be placed.
   - **Panels** — anchors on **frame holes only**, like the Rhino NST3. Click a free hole on a frame, then one on a **second frame at the same height** (the base edge). The third click chooses the plane: go **up or down** one of the anchor frames for a **vertical wall panel** (2 H beams + panels between the frames), or **sideways to a third frame** for a **horizontal panel** (a ring of 4 H beams + panels on both sides — the fourth corner frame must already exist). Panel sizes are currently unrestricted (any width × height / width × depth the beams can span); every beam runs through the same hole/peg + overlap pipeline as Expert mode.
   - (The old **Beams** tool was removed — single beams are placed with the
     **Horizontal beam** card in the Parts tab, which does the same job with
     a live ghost and a length scale.)
4. Press **Esc** to drop the tool entirely — picks are cancelled AND the cursor
   returns to a plain pointer with nothing following it (the same works in the
   Parts tab: Esc disarms the current tool). Pick a tool button again to keep
   building.
5. Click the **Parts** tab for the category part tools (below).

## Parts tab: three category tools, sizes picked on a scale

Nobody browsing the palette should need to know what a "V13" is, so the old
one-button-per-size grid is gone. The Parts tab now has **four cards** —
**Vertical frame**, **Horizontal beam**, **Twist beam** and **Panel** (no
separate bottom button anymore) — and the size is chosen **while placing**,
on the same measure scale the guided tools use (`FreePartSession`):

- **Vertical frame** — click **open ground** (snaps to the 88 mm grid;
  frames stand anywhere, no bridging rule) **or an amber dot** (a free peg)
  to stack. A vertical scale appears with a tick at **every catalogue V
  height**; click when the label shows the size you want. On a peg, aiming
  **below** the peg hangs the frame instead (it can never sink through the
  floor — placement runs the same floor check as Expert mode did).
  While aiming at the ground, the **grid row and column under the cursor
  get a thicker, darker tone** (two long solid lines in the grid's own
  color, exactly on the real grid lines), so it's obvious where the frame
  will land without an extra overlay.
- **Horizontal beam / Twist beam** — click a **blue ring** (free hole on a
  frame); a horizontal scale locks to the aimed axis with a tick at **every
  catalogue length**. Click to place. The far end may land on another frame
  (it pairs up) or hang free.
- **Every tool shows a live ghost** of the exact part while you scale:
  vertical frames grow through the V sizes on the spot, and the beam tools
  run the real span pipeline in preview mode (`TryPreviewConnectorSpan`), so
  the ghost pose is identical to what a click commits. No pose = the line
  turns red and the click is rejected.
- There is no Frames/Beams/Twist tab row anymore — the three cards are the
  categories.
- Clicking the armed card again, or **Esc**, puts the tool away
  (Esc from the scale step first goes back to the anchor step).

All placements still run the strict Expert pipeline (hole/peg pairing,
face checks, overlap rejection, undo history) — the freedom is in the
interaction, not the rules.

## Selection: click acts, drag selects

There is no separate Select mode anymore. One rule works everywhere, in both
the Tools and Parts tabs:

- **Click** places a part / picks a tool point while a tool or part is armed
  (commits on release). With **nothing armed** (Esc, or no part chosen), a
  click **selects the single beam or panel under the cursor** and opens the
  same action card as a drag selection. Clicking empty space deselects
  everything.
- **Shift+click** adds the part under the cursor to the selection (or removes
  it if it was already selected). Shift+click on empty space keeps the
  selection.
- **Hold the left button and drag** to draw a selection rectangle over the
  scene. Beams **and panels** inside it highlight live.

### Moving a selection: the arrow gizmo

While anything is selected (and no tool is armed), colored arrows stand at
the **centre of the selection**. Which arrows appear depends on what is
selected — one axis at a time, never diagonally:

- **Selection contains vertical frames** (a full shelf / configuration):
  **X (red) and Z (blue)** arrows — the whole structure slides around the
  grid in 88 mm steps and stays on the ground. On release the destination is
  validated with the normal placement rules; a spot that clips into another
  structure **reverts the move** with a status message.
- **Selection is only horizontal/twist beams** (and panels): a single
  **Y (green)** arrow — the beams slide up/down **along the frames they are
  plugged into**, snapping only to hole rows where **every peg lands on a
  free hole** (the same rule as the panel layer mover). No sideways movement
  for plugged-in beams.

On release the move commits: beam connections re-pair from the new
positions, panels re-seat into the bay at their destination (a panel with no
bay at the destination is removed — undo brings it back), and one undo step
is recorded. `Assets/Scripts/Selection/MoveGizmoController.cs`, shared slide
rules in `Assets/Scripts/Selection/BeamSlideRules.cs`.

Releasing the drag shows a small action card next to the selection:

- **Copy** — the whole selection (beams *and* panels) follows the cursor as a
  ghost, snapped to the 88 mm grid. **Every click stamps a copy** and the tool
  stays armed, so a row of shelves is copy + click + click + click. Beams that
  land exactly where an identical beam already stands are **skipped
  automatically** — butting a copy against the previous one shares the middle
  frames instead of doubling them (status bar reports e.g.
  `Stamped 10/12 beams (2 shared with existing), 2/2 panels`). Panels re-attach
  to the slots the stamped beams form. Esc finishes stamping.
- **Delete** — removes everything selected (also on the Delete/Backspace key).
- **✕ / Esc / click elsewhere** — deselects.

## Holes & pegs made visible

The NEOSPACE connection system has two connector types: **holes** (the
socket rows on frames and beams) and **pegs** (the pins that plug into
them). While a placement tool is armed, `AttachmentMarkerController`
(injected by `SelectionBootstrap`) lights up every FREE connector the armed
part can target:

- **Blue rings = free holes.** Shown while the Horizontal/Twist beam card or
  the guided Panels tool is armed — beams plug their pegs into these.
- **Amber dots = free pegs.** Shown while the Vertical frame card is armed —
  frames seat onto these.

Connectors sharing the same part and 88 mm grid cell merge into **one
marker per hole level** (a frame has four holes per level, but four rings
per layer was noise — any of the four accepts the connection, the pipeline
picks the right face). Markers fade with distance from the cursor, the
nearest candidate pulses so it's obvious what a click would connect to,
occupied connectors stay dark, and the status bar names the colors. Markers
sit on the Ignore Raycast layer and never block picking.

## Camera & fullscreen

Walkthrough controls: **W/A/S/D** move, **Q/E** up/down, **Shift** sprints,
right-drag looks around. (Shift+click is selection, so vertical movement lives
on Q/E, not Space/Shift.) A **fullscreen button** sits in the bottom-right
corner above the hint pill (`FullscreenBootstrap` injects it) — use it to
enter *and* leave fullscreen, because in a browser Esc always exits fullscreen
and Esc is also the cancel-tool key.

## Undo / redo / clear

The top bar has **Undo**, **Redo** and **Clear all** buttons
(`BuildHistoryBootstrap` wires baked buttons, or injects them into legacy
scenes). Shortcuts: **Ctrl/Cmd+Z** undo, **Ctrl/Cmd+Shift+Z** or
**Ctrl/Cmd+Y** redo.

The old top-bar **Apply veneer / Clear** actions are gone. Veneers now appear
to users as **"Finish"** — the outer skin that dresses the frames alongside
the panels — as a dimmed placeholder card at the bottom of the Parts list
(`UIPartsPalette.AddFinishButton`). It is not clickable yet; wire it up when
the finish feature lands. `VeneerManager.ApplyVeneers/ClearVeneers` still
exist for that future work.

## Configuration codes (copy / load)

Every configuration has a code — one string that recreates it exactly.
Getting a code is contextual (there is no global "Share code" button):

- a PIECE (one build) has an `NS1-…` code — the **ID** button on any row of
  My Pieces copies it.
- a SPACE (an arrangement of pieces) has an `NSS1-…` code, with every piece
  embedded so the receiver needs no library — the **Copy space code** button
  at the bottom of the Space panel copies it.

**Load code** (under the top bar's ⋯ overflow menu) opens a dialog with a
text box: paste a code and press Load. Either kind works — a piece code
opens in Piece Mode, a space code in Space Mode (the app switches modes by
itself). Loading **replaces** what is there —
one undo step, Ctrl/Cmd+Z brings the old content back — and codes are
deterministic: the same configuration always produces the same code.
Invalid or corrupt codes show their error inside the dialog without
touching the scene. Format and internals:
`Assets/Scripts/Save/CONFIG_CODE_SCHEMA.md` and
`Assets/Scripts/Space/SpaceCodec.cs`.

## Pieces (named saved builds)

**Pieces** in the top bar opens the *My Pieces* panel. A piece is one named
furniture item — a chair, a table, a shelf — stored as its configuration
code plus display metadata (part count, W×D×H, price, thumbnail), never a
mesh dump. Thumbnails are rendered from a fixed three-quarter angle framing
the structure (`PieceLibrary.CaptureThumbnailFramed`), so every card looks
consistent regardless of where the camera pointed at save time; pieces
missing a thumbnail get one backfilled the next time they are opened. Type a name and press
**Save as piece** to add the current build to the library; each row offers
**Open** (replaces the current build — one undo step), **⟳ overwrite** (the
piece takes on the current build) and **✕ delete**. Overwrite and delete ask
for a second click ("Sure?") before acting. The library lives as one JSON +
PNG per piece under `persistentDataPath/Pieces`
(`Assets/Scripts/Save/PieceLibrary.cs`, UI in `PieceUI.cs`).

## Space Mode (arranging pieces into a room)

**Space mode** in the top bar switches between building one piece and
composing a room from saved pieces (`Assets/Scripts/Space/`). Entering
Space Mode hides the current build (kept intact — switching back restores
it exactly), sleeps every builder tool, and swaps the left panel for the
piece library.

Click a piece in the list and a translucent ghost of the whole piece
follows the cursor on the floor (88 mm grid snap, **R** rotates 90°); every
click stamps one copy, **Esc** or right-click puts the tool down. Placed
pieces are RIGID INSTANCES — frozen visual clones with one collider, no
holes or pegs (no mating between pieces in v1), invisible to all builder
systems. Click selects one (Shift+click adds/removes, empty floor clears),
dragging moves the whole selection on the grid, and a small card near the
selection offers **Duplicate / Rotate / Remove**. Delete/Backspace and
Ctrl/Cmd+D work too.

The price pill shows the sum of the placed instances, and Undo / Redo /
Clear all (buttons and Ctrl/Cmd+Z shortcuts) work on the space while the
mode is on — the piece-mode history is suspended, not lost. Instance
geometry is built once per piece via the configuration-code restore
pipeline (staged 200 m off-camera, then stripped to pure renderers) and
cached, so ghosts, stamps, duplicates and undo steps are instant clones.

### Editing a piece from the space

Select one or more copies of the same piece and press **Edit** on the
action card: the piece's definition opens in Piece Mode with all the normal
build tools, under a bar that offers the two ways to save
(`SpaceEditSession.cs`):

- **Update all N** — every copy in the space takes the edited design (the
  matching piece in the library is updated too, thumbnail included);
- **Make unique** — only the copies you had selected switch, as a new
  "… (unique)" definition; the other copies and the library keep the old
  design;
- **Cancel** (or the mode button) — back to the space, nothing changed.

Applying is one space-history step, so Ctrl/Cmd+Z rolls the edit back.
Codec + edit-semantics self-tests: Tools → Configurator → Run SpaceCode
Selftest (`SpaceCodeSelfTest.cs`) — round-trip, determinism, invalid codes,
version gate, update-all vs make-unique.

History is snapshot-based (`Assets/Scripts/History/BuildHistory.cs`): every
mutating tool calls `BuildHistory.NotifyChanged()` and the scene state (beams
and panels) is captured one frame later, so a whole template batch, clipboard
stamp or layer move is a single undo step. Restoring replays beams through the
normal batch-placement pipeline and re-attaches panels to the rebuilt slots.
**Clear all** wipes every beam and panel on the grid (and any veneers) — it is
itself undoable.

## Moving panel layers (panel mode)

With the Panels tool active, **click a placed panel to select it**, then **drag
up or down**: the panel and the beams that frame it (2 rails for a wall panel,
the 4-beam ring for a floor/shelf panel) move together along the frames. The
drag snaps only to hole rows where **every peg lands on a free hole**, a ghost
shows the beams at the target height, and the move commits on release — the
panel re-attaches to the slot at the new height automatically.

### The measure line

While picking, a thick guide line is drawn from your last pick:

- It **never runs diagonally** — it locks to the nearest world axis on the ground,
  or straight up for heights, exactly how beams are built.
- **Tick marks** show every catalogue length the line can snap to; the active one
  is highlighted and labelled with the part and its nominal length
  (e.g. `H7 · 663 mm`, `V13 · 1097 mm` — the same numbers as the price list).
- Blue (Evo accent) = the click will commit that size; red = not a valid target yet.

### Setting frame height

Frame height is the **last click** of the Frames sequence: after the base picks,
move the mouse **upwards** — the guide line grows vertically and snaps through
the V sizes (V1, V3, V5 … V29). Click when the label shows the size you want.
Aiming at floor level gives V1. The status bar mirrors the current snap at all
times. The Panels tool never sets frame heights — it only ever attaches to holes
on frames that already exist.

### Strict placement rules (why clicks get rejected)

- Beam picks that are not near a free hole/peg do nothing (status bar explains).
- Spans that don't match an 88 mm-module catalogue size are rejected, not fudged.
- A beam that would extend away from the picked gap is rejected — pick the
  connection point on the face that looks at the other beam.
- All committed parts go through `PlaceRealBeamFromGhost`, so occupancy pairing,
  overlap checks, and panel-slot rescans behave exactly like Expert placements.

### World scale

The prefabs are modelled at **1 unit = 100 mm** (one 88 mm module = 0.88 units).
All template math and preview visuals convert through `NeospaceUnits`, which
defaults to that scale and re-measures it at startup from a real V prefab's
hole spacing (`TemplateSession.CalibrateWorldScale`), so the guide lines and
ticks are always drawn at true frame size.

### The adaptive grid & structure dimensions

The floor no longer carries one huge baked grid. At runtime
(`EnvironmentBootstrap` → `Assets/Scripts/Environment/`):

- **Adaptive grid patch** (`AdaptiveGridController`) — the floor keeps only a
  faint reference grid (the bold 704 mm lines, barely darker than the tint) so
  the world never reads as a void, and the full 88 mm module grid lives on a
  transparent patch with a thin outline that sizes itself to the current build
  plus a margin (default 5 modules on every side, minimum 20×20 modules when
  empty). It grows and shrinks smoothly as the structure changes; its UVs are
  anchored to world coordinates, so the lines always sit exactly on the snap
  grid.
- **Dimension annotations** (`StructureDimensionsController`) — when the
  structure *changes*, thin CAD-style dimension lines appear just outside its
  bounding box: **width** and **depth** along the ground on the camera-facing
  sides, **height** up the shared corner, each with end ticks and a small
  millimetre label, plus a cross at the footprint centre. They hold for ~4 s,
  then fade out so the canvas stays clean.
- **Internal coordinate system** (`StructureBounds`, static) — the world is an
  88 mm module lattice centred on the origin. `WorldToModule` / `ModuleToWorld`
  convert between metres and integer cells, and `TryCompute` returns the live
  structure metrics (world bounds, centre, ground centre, W/D/H in mm, and the
  inclusive min/max module cells). **This is the data a saved configuration
  should serialize** — centre + module extents fully locate and size a build.

`GuidedBootstrap` auto-wires UI at runtime if the scene was not rebuilt with `Tools/Configurator/Rebuild UI`.

## Code map (for the other chat)

| Area | Path | Notes |
| --- | --- | --- |
| Pure catalogue math | `Assets/Scripts/NeospaceCore/` | No scene deps; safe to ignore |
| Guided UX / planners | `Assets/Scripts/Templates/` | New only |
| Batch place API | `Assets/Scripts/Build/BuildController.TemplateBatch.cs` | New partial only |
| Hotspots touched lightly | `UIInteractionState`, `UIToolbarController`, `UIStatusBar`, `UIPartsPalette`, `GhostController`, `PanelGhostController`, `ConfiguratorUIBuilder` | Prefer not to rewrite these while Guided is in flight |

### Parallel-work guidance

- Prefer editing outside the hotspot files above.
- Guided lives on branch `feature/guided-templates`.
- Do not remove `UIInteractionState.Experience` or the Guided early-outs in ghost controllers.

## Selftest

Unity menu: **Tools → Configurator → Run NeospaceCore Selftest**

EditMode NUnit (if Test Runner discovers Editor tests): `Assets/Tests/EditMode/Editor/NeospaceCoreTests.cs`
