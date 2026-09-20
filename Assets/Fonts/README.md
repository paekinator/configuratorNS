# NEOSPACE typography test library

The source PDF remains outside this Unity project. This folder contains only
font downloads, licensed source files, TextMesh Pro assets and reusable set
definitions.

## Folder layout

- `Downloads/` keeps the original downloaded ZIP archives unchanged.
- `Families/<Family>/Source/` contains the selected Regular, Medium and
  SemiBold source faces plus the supplied licence/readme files.
- `Families/<Family>/TMP/` contains Unity TextMesh Pro font assets.
- `Sets/` contains one `NeospaceTypographySet` asset per test combination.
- `Legacy/` contains earlier unused Lato, Poppins and Rubik TMP assets.

The existing website/configurator baseline is treated as its own set:
DM Sans is the primary UI face and Manrope is the display face. Because that
baseline has no mono family, technical content falls back to DM Sans.

## Testing in Unity

Open `NEOSPACE > Typography > Font Set Tester`, then choose **Apply to open
configurator** on any set. The operation is undoable and updates the open
scene only. It preserves three weight levels and applies mono only to genuine
technical content such as dimensions, prices, IDs, codes, counts and states.

The formal PDF shortlist is numbered 01-04. The three additional combinations
are included after it so they can be compared under the same UI conditions.
