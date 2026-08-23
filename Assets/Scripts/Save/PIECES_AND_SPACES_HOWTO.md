# Pieces & Spaces — user how-to

The short version of how to save, share and reuse designs. (Developer
internals: `CONFIG_CODE_SCHEMA.md` next to this file.)

## Save a design as a Piece

1. Build something on the grid (a chair, a table, a shelf).
2. Press **Pieces** in the top bar, type a name, press **Save as piece**.
3. It appears in *My Pieces* with a thumbnail, size, part count and price.

Per row: **Open** loads the piece back onto the grid (undoable),
**⟳** overwrites the piece with whatever is on the grid now, **✕** deletes
it. Both ask "Sure?" before acting. **ID** copies the piece's code.

## Share any design with a code

Every configuration has a code — one string of text that recreates it
exactly on any machine. No files, no accounts.

- **ID** on any row of *My Pieces* copies that piece's code.
- **Copy space code** (bottom of the Space panel) copies the whole room,
  pieces included — the receiver doesn't need your piece library.
- **Load code** (under the ⋯ menu in the top bar) opens a dialog: paste a
  code into the text box and press Load. It knows which kind it is: piece
  codes start with `NS1-`, space codes with `NSS1-`, and the app switches
  modes by itself. Loading replaces what's on screen — Ctrl+Z (Cmd+Z on
  Mac) brings your old work back.

If a code doesn't load, the dialog says why: it wasn't a NEOSPACE code,
it was damaged in transit (copy it again), or it was made with a newer
version of the configurator.

## Compose a room in Space Mode

1. Save at least one piece, then switch to **Space** in the top bar.
   Your current build is tucked away safely — switching back restores it.
2. Click a piece in the left panel; a ghost follows your cursor. Click the
   floor to place a copy (every click places another), **R** rotates,
   **Esc** puts the tool down.
3. Placed pieces move as one object: click to select, drag to move,
   Shift+click to select several. The little card next to a selection has
   **Duplicate / Rotate / Remove** — and **Edit** when every selected copy
   is the same piece.
4. Pieces can stand flush and even share their edge frames — place two
   shelves so their boundary posts coincide and the duplicates disappear:
   the row reads (and is priced) as one connected structure. Deeper
   overlaps are refused: a red ghost means the spot is taken, drags stop
   at the last valid spot, and rotating is refused when the turned piece
   wouldn't fit. **Duplicate** places the copy flush with the original,
   ready to extend the row.
5. **Edit** opens that piece back in the builder. When done, choose
   **Update all copies** (every copy in the room changes, library too) or
   **Make unique** (only the selected copies change).

The price pill totals every placed piece. Undo/redo works in both modes,
each with its own history.
