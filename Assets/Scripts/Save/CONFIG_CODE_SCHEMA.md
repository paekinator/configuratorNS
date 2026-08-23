# NEOSPACE Configuration Codes — v1 schema proposal

Status: **v1 implemented and wired into the live app** (`PartRegistry`,
`ConfigurationModel`, `PoseQuantizer`, `ConfigurationCodec`,
`ConfigurationCapture`, `ConfigurationRestorer`, `ConfigurationCode` facade,
`ConfigurationCodeUI` + `ConfigurationCodeBootstrap`). Self-tests:
Tools → Configurator → Run ConfigCode Selftest. Debug window:
Tools → Configurator → Configuration Code. The frozen v1 golden code lives
in `ConfigurationCodeSelfTest.GoldenCode` — if the byte layout ever changes,
that test fails and `SchemaVersion` must be bumped.

Live app: codes are copied contextually — the **ID** button per saved
piece in the My Pieces panel copies that piece's `NS1-…` code, and the
**Copy space code** button in the Space panel copies an `NSS1-…` space
code (see `Space/SpaceCodec.cs`) that embeds every distinct piece code
plus instance grid positions and quarter turns — fully self-contained.
Loading goes through the **Load code** dialog in the top bar (paste a code
into the text box); it detects the prefix and switches modes
automatically. Load semantics are **REPLACE, not merge** — the decoded
configuration becomes the whole build via the same wipe + replay path undo
uses, and the paste is one undo step (Ctrl/Cmd+Z brings the old build back).
Invalid or corrupt codes never touch the scene; the codec's error message is
shown in the status bar. Merging patterns into an existing build stays the
job of the selection clipboard (copy/stamp), not configuration codes.

Pieces v1 sits on top of the codec: a piece = name + configuration code +
metadata (part counts, bounds mm, price, optional PNG thumbnail), stored
one JSON per piece under `persistentDataPath/Pieces`
(`PieceLibrary.cs`, `PieceUI.cs` — the "Pieces" top-bar panel). Opening a
piece goes through the same validate → replace restore path.

Space Mode v1 (`Assets/Scripts/Space/`) composes rooms from saved pieces as
rigid instances: each distinct piece code is restored ONCE through the
normal pipeline (staged off-camera), frozen to pure renderers and cached;
placed instances are clones carrying only (piece id, code, price, pose).
Nested piece codes inside a space code (§2.5) remain future work — a saved
space would today store the list of piece codes + poses, which is exactly
the anticipated v2 shape.

Goal: a **deterministic, reversible configuration code** — a compact,
URL-safe string that recreates a build exactly (frames, panels, finish state)
without storing any assembled 3D files. The same build must always produce
the same code, and a code must always produce the same build.

---

## 1. Scene state that must be captured today

Everything a build IS today is already enumerated by `BuildHistory.ReadScene`
(the undo system) and restored by `BuildHistory.RestoreRoutine`. That pair is
the proof of reversibility: restore replays beams through
`BuildController.PlacePartsBatch(poses, seatVerticalsOnFloor:false,
validateOverlap:false)` and re-attaches panels by geometric slot lookup
(`StructureClipboard.FindSlotNear` → `PanelSlotManager.PlacePanel`).

| State | Source today | Notes |
| --- | --- | --- |
| **Beam part id** | root name minus `(Clone)` (`StructureClipboard.CleanPartId`) | `V{n}`, `H{n}`, `T{n}` (scene "T" = catalogue "HT"). Valid sizes in `CatalogueData.VSizes/HSizes/HtSizes`. |
| **Beam pose** | `root.position` (Vector3), `root.rotation` (Quaternion) | All placements come from the 88 mm hole/peg pipeline, so positions sit on the module lattice (plus fixed offsets like `GroundOffsetMm` = 30.5 mm) and rotations are axis-aligned yaws with baked prefab offsets. |
| **Panel** | `PanelInstance` (`slotId`, `side` ±1) + transform (`position` = slot center, `forward` = slot normal) | `slotId` is regenerated on every slot rescan — **not** stable across sessions. The stable address is the quantized slot center + normal axis + side, exactly what `BuildHistory.PanelState` stores. Panel size is derivable from the slot. |
| **Finish (veneer) state** | `VeneerManager.ApplyVeneers/ClearVeneers` toggling pre-baked `SelectableBeam.veneerStrips` | Today this is effectively **one global boolean** (strips shown or not). The generative per-face veneer code is commented out. Capture as a flag now; reserve room for per-part/per-face options later. |
| **Ghost exclusion** | `BuildController.ghostLayerMask` | Capture must skip ghosts, same as history/clipboard. |

Explicitly **not** captured (derivable or cosmetic): grid patch size,
dimension annotations, structure bounds/center (recomputable via
`StructureBounds.TryCompute`), camera pose, dark/light theme, control scheme
(user preference in PlayerPrefs), price/part counts (derived).

**Normalization**: codes must be position-independent. Before encoding,
translate everything so the structure's minimum module corner
(`StructureBounds.Info.MinModule`) is the origin. Two identical shelves built
in different corners of the grid then produce identical codes.

---

## 2. Schema fields + versioning

### 2.1 Permanent part IDs — `PartRegistry`

Small integer ids, assigned once, **never reused or renumbered** (the
V14/V22 removal is precedent that catalogue lists mutate — registry ids must
not). Retired parts keep their ids forever; the decoder reports "retired
part" rather than mis-mapping.

| Range | Family | Example |
| --- | --- | --- |
| 1–31 | V frames (`V1`…`V29`) | `V13` = 7 |
| 32–63 | H beams | `H7` = 35 |
| 64–95 | HT/T twist beams | `T5` = 66 |
| 96–127 | reserved (Load Bearing Bar, Cap, Foot) | — |
| 128+ | panels by pair `H{a}×H{b}`, veneer/finish types, future families | — |

The registry maps id ↔ canonical catalogue name (`Naming`/`CatalogueData`
vocabulary, so "T5" round-trips to catalogue "HT5" explicitly).

### 2.2 Logical schema (conceptual, before binary packing)

```
Configuration v1
├─ header
│  ├─ magic            "NSC"                      (3 bytes in binary, "NS1-" prefix in text)
│  ├─ schemaVersion    1                          (uint8)
│  ├─ flags            bit0 finishApplied; rest 0 (uint8, reserved)
│  ├─ beamCount        varint
│  └─ panelCount       varint
├─ beams[]   (sorted canonically, see 2.4)
│  ├─ partCode         varint  (PartRegistry id)
│  ├─ posX, posY, posZ zigzag varint, INTEGER MILLIMETRES from normalized origin
│  └─ rot              1 byte fast path: index into the 24 axis-aligned
│                      orientations (covers every pose the snap pipeline
│                      produces today); escape value 0xFF → extended form:
│                      3 × int16 euler in 0.1° steps (audit risk R2)
├─ panels[]  (sorted canonically)
│  ├─ centerX/Y/Z      zigzag varint, integer mm from normalized origin
│  ├─ axis             uint8: ±X, ±Z (standing) or ±Y (lying) slot normal
│  └─ side             1 bit (+normal / −normal), packed into the axis byte
├─ extensions          TLV list (type varint, length varint, payload) — v1
│                      writes none; decoders MUST skip unknown types.
│                      Reserved types: 0x10 per-part finish options,
│                      0x20 PIECE (see 2.5), 0x21 SPACE.
└─ checksum            CRC32 of all preceding bytes (uint32)
```

Positions in **integer millimetres** keep the codec float-free (deterministic
across platforms); the snap pipeline guarantees sub-mm values are noise.
Poses convert through `NeospaceUnits` on capture/restore.

### 2.3 Versioning strategy

- `schemaVersion` is a single byte bumped on **breaking layout changes**;
  additive evolution goes through the TLV extension section instead.
- Decoders accept every version ≤ their own and refuse newer ones with a
  clear "made in a newer version" message.
- The text prefix mirrors the version (`NS1-…`) so a human/support can
  identify vintage without decoding.
- Registry additions never require a version bump; registry ids are
  append-only.

### 2.4 Canonical serialization (determinism)

Same rules as `BuildHistory.BuildSignature`: after normalizing the origin,
sort beams by `(partCode, posX, posY, posZ, rot)` and panels by
`(centerX, centerY, centerZ, axis, side)`, ordinal integer comparisons only.
Encoding the same scene — regardless of build order, undo history, or world
position — yields byte-identical output, so the code doubles as a build
fingerprint (useful for dedupe and share-link caching).

### 2.5 Space codes — SHIPPED as a sibling format (`NSS1-…`)

Space nesting did not go through the reserved TLV route; it shipped as its
own top-level format in `Assets/Scripts/Space/SpaceCodec.cs` (the TLV types
0x20/0x21 stay reserved in case pieces ever need to nest *inside* an NS1
payload). Layout:

```
Space v1  =  "NSS1-" + Base64Url( "NSS" + version byte + Deflate(text) + CRC32 )
text      =  "S1\n"
             "P|{name}|{priceCents}|{full NS1 piece code}\n"   × distinct piece
             "I|{pieceIndex}|{xMm}|{zMm}|{quarterTurns}\n"     × instance
```

Determinism mirrors §2.4: distinct piece codes sorted ordinally, instance
positions normalized so the min corner is (0,0), instances sorted by
(piece, x, z, quarter). A space code is **self-contained** — the receiver
needs no piece library, every piece's full `NS1-…` code travels inside.
Decode validates prefix, version gate, CRC, then every embedded piece code
recursively. Self-tests: Tools → Configurator → Run SpaceCode Selftest.

---

## 3. Encoding / decoding pipeline

```
ENCODE
scene ──▶ Capture: collect beams/panels (ReadScene rules, ghost mask),
          finish flag; convert to integer mm
      ──▶ Canonicalize: translate to min-module origin; sort (2.4)
      ──▶ Pack: binary writer (varint/zigzag), header + records + TLV
      ──▶ Compress: raw Deflate (System.IO.Compression), keep only if smaller
      ──▶ Checksum: CRC32 appended
      ──▶ Text: "NS1-" + Base64Url (RFC 4648 §5, no padding)

DECODE
text ──▶ prefix/version gate ──▶ Base64Url → bytes ──▶ CRC check (reject
     corrupt/typo'd codes early) ──▶ inflate ──▶ parse records; validate every
     partCode against PartRegistry + CatalogueData
     ──▶ Rebuild (the proven BuildHistory.RestoreRoutine path):
         1. wipe scene (like ClearRoutine), one frame for Destroy()
         2. PlacePartsBatch(poses, seatVerticalsOnFloor:false,
            validateOverlap:false) — exact poses, no re-validation
         3. rescan slots; re-place panels via FindSlotNear(center, normal)
         4. apply finish flag via VeneerManager
         5. BuildHistory.NotifyChanged() so the import is one undo step
     ──▶ Report: parts placed / skipped (retired ids, unresolved slots)
```

Size estimate: a beam ≈ 6–9 bytes, a panel ≈ 5–8 bytes pre-compression. A
100-part build ≈ 0.8 KB → ~600–900 Base64 chars after Deflate. Fine for
clipboard and QR; long for a URL path (see risk R6).

---

## 4. File / class names (matching repo conventions)

New domain folder `Assets/Scripts/Save/`, static helpers where no scene
state is needed, one bootstrap for future UI wiring — mirroring how
`History/`, `Selection/` and `Environment/` are organized:

| File | Kind | Responsibility |
| --- | --- | --- |
| `Save/CONFIG_CODE_SCHEMA.md` | doc | this document |
| `Save/PartRegistry.cs` | static class | permanent id ↔ catalogue-name table; retirement flags |
| `Save/ConfigurationModel.cs` | plain data | `BeamRecord`, `PanelRecord`, header struct — no Unity scene types, integer mm only |
| `Save/ConfigurationCapture.cs` | static class | scene → model (ReadScene rules + `StructureBounds` origin normalization) |
| `Save/ConfigurationCodec.cs` | static class | model ↔ bytes ↔ `NS1-…` string (varint, Deflate, CRC32, Base64Url) |
| `Save/ConfigurationRestorer.cs` | MonoBehaviour | model → scene coroutine (wipe → `PlacePartsBatch` → panel slots → finish), progress + skip report |
| `Save/SaveLoadBootstrap.cs` | static bootstrap | later: runtime UI injection (`RuntimeInitializeOnLoadMethod`, same pattern as `SelectionBootstrap`) |
| `NeospaceCore/SelfTest.cs` | existing | add a capture→encode→decode→re-capture round-trip assertion (codes must match byte-for-byte) |

`ConfigurationModel`/`Codec`/`PartRegistry` deliberately avoid UnityEngine
scene APIs so they can be unit-tested headless and reused server-side later.

---

## 5. Risks / open questions

- **R1 — Panel slot re-resolution.** Slots are geometric, regenerated per
  scan; `FindSlotNear` tolerance decides whether every stored panel finds its
  slot after replay. Undo already relies on this and works, but the importer
  must surface "N panels could not be re-attached" instead of failing
  silently.
- **R2 — Rotation audit.** Assumption: every snap-pipeline pose is one of 24
  axis-aligned orientations (after prefab-pivot offsets). Must be verified on
  real builds before freezing the 1-byte fast path; the 0xFF extended form is
  the safety valve, but if arbitrary rotations are common the fast path is
  pointless and v1 should just use quantized euler.
- **R3 — `T` vs `HT` naming.** Scene ids say `T5`, catalogue says `HT5`.
  The registry is the single place allowed to know this; nothing else should
  string-munge.
- **R4 — Panel sizes outside the catalogue.** The catalogue restriction on
  panel pairs is currently disabled, so scenes can hold panels
  `CatalogueData.PanelPairs` doesn't list. Codes store slot geometry (not a
  pair id), so they round-trip fine — but decide whether decode should warn,
  and whether production wants the restriction back before codes go public.
- **R5 — Finish granularity.** Today: one boolean. When real per-face veneer
  options land (`VeneerTypes`, `VeneerLengths` exist in the catalogue
  already), they go into TLV 0x10 — but the UX for choosing them doesn't
  exist yet, so the flag may live a long time. Acceptable?
- **R6 — Code length for sharing.** ~600–900 chars is fine for copy-paste
  and QR, unwieldy in a URL. If share-links matter, a backend short-link
  (code stored server-side, keyed by its content hash) is the likely v1.5.
  Decision needed on the primary sharing surface: clipboard string, `.nsc`
  file, QR, or URL.
- **R7 — Retired parts policy.** When a part id in a code is retired
  (V14-style), options are: refuse the whole code, place everything else and
  report, or substitute the nearest size. Proposal: place-and-report, never
  substitute silently.
- **R8 — Where does the world origin live for Piece/Space?** v1 normalizes
  to the structure's min corner. Spaces will need a real anchor convention
  (room origin? first piece?) — decide when Space lands, the TLV reserves the
  room.
