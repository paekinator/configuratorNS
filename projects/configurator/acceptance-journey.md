# Configurator acceptance journey

Updated 14 September 2026. This checklist replaces the unavailable earlier T-119 document for this checkout. It is a test plan, not evidence that a human usability session or browser certification has happened.

## Product direction

A beginner supplies the dimensions of the space they want to fill, combines guided frames and panels, reviews fit and finishes, saves or shares the design, and requests pricing. Keep individual part placement and portable design codes for experienced designers. The eventual product includes purchasing and a community of shared designs; neither an authoritative commerce catalogue nor a community service is configured in this repository.

## First delivery journey

1. Open the configurator. The loader must show useful progress, startup errors and a retry action. A graphics failure after startup must explain that a reload may lose unsaved changes.
2. Choose **Set space size**. Enter width, depth and height in millimetres. Reject empty, nonnumeric, nonpositive and unreasonable values without replacing a valid setup.
3. Start with **Build frames**, then **Add panels**. Combine the existing guided tools; experienced users can still choose individual **Parts**. The dimensions check is an overall bounding-size check, not a clearance, structural or manufacturing certification.
4. Add a V9. Expect one frame and the local estimate of AUD 35. Undo, redo, delete and clear it. Both the visible structure and header must agree after each operation. Test a same-frame placement followed immediately by undo.
5. Add panels and turn Finish on. Change panel and veneer/cap colour previews. The UI must disclose that panels, dressing and unpriced frames are not included in the known frame estimate. V11 must never appear to have a verified zero price.
6. Save as a named piece. Wait for a success or error result. In Web builds success must follow browser storage acknowledgement. Close and reopen the app/browser, open the saved piece, and compare frames, panels and finish toggle. Palette colours are currently global preferences; design codes do not carry them.
7. Copy the piece ID and load it. Invalid/corrupt codes or unavailable catalogue parts must not clear the current design. Force an unattached panel reference and confirm the original scene is returned. Undo a successful load and confirm the previous finish toggle returns too.
8. Open **Prepare quote** in Piece Mode. Check grouped frame quantities, missing prices, panel count, dressing/colour notes and the exact design code. Copy and download it. Confirm that nothing is submitted to a supplier and no order is placed by this action.
9. Switch to Space Mode, place saved pieces, move/rotate them, edit one and save it back. A failed save must keep editing open and permit code backup. Export an arrangement code. The first quote export is for a single Piece; arrangement manufacturing quantities require a separate merged-BOM implementation.

## Save/recovery fault cases

- Interrupt a staged update before its final rename: recover a valid committed copy.
- Corrupt the primary JSON: recover the validated backup and show a recovery notice.
- Interrupt a first save: a fully written, validated staging file can be recovered; a truncated one must not be loaded.
- Deny writes or exhaust browser storage: show a failed/unconfirmed result and offer a code backup, never unconditional success.
- Keep surviving files when no valid copy can be read. Do not silently overwrite them on load.
- Treat thumbnail failure separately from the design definition.
- Browser storage remains origin/device/profile specific. Clearing it can remove the library. This release promises explicit local saves and portable codes, not cloud sync or continuous autosave.

## Initial platform matrix

- Primary: Windows 11 with desktop Chrome and Edge; keyboard and mouse, 1366×768 and 1920×1080.
- Secondary validation before advertising support: macOS with Safari and Chrome; keyboard and trackpad.
- Separate mobile/touch validation is required before advertising phone/tablet support.
- Windows x64 downloadable build: open the configurator scene, size a space, build/finish, save/reopen, code export, quote export, space assembly.
- Browser tests must cover supported WebGL, disabled graphics, missing loader, startup rejection, stalled download, lost network and lost WebGL context. Unit simulations do not replace a real deployed WebGL test.

## First-time customer usability session (T-118)

Recruit someone who has not used the product and has no design training. Do not coach them during the first attempt. Give them a concrete brief: “Make a shelving arrangement for an alcove 1200 mm wide, 450 mm deep and 1800 mm high. Choose colours, keep a copy, and prepare it for pricing.”

Record consent and device/browser first. Observe whether they find size setup, understand the frame/panel tools, identify when dimensions do not fit, distinguish a price estimate from a purchasable total, save/reopen unaided, and recover from one intentional mistake. Record task completion, time, errors, assistance and their own words. Finish by asking what they expected to happen next and whether the saved design and quote felt trustworthy.

Do not mark T-118 complete from an agent smoke test. Store anonymised observations and resulting fixes here after a real participant session.

## Release gates

- Project scripts compile with the pinned Unity version; codec/core/persistence/quote tests and history/restore regressions pass.
- Real-browser deployment tests and local save across reload pass on the primary matrix.
- New panels and dialogs remain readable in light/dark themes and at the smallest supported desktop resolution.
- Package contains the configurator entry scene, required runtime files and a build manifest/checksum.
- Checkout stays unavailable until the provider, authoritative SKU/variant mapping, currency/tax/delivery rules, stock handling and final server-side pricing are supplied and validated.
- Community publishing stays outside this local release until identity, storage, ownership/licensing, privacy/moderation and publication controls are designed and implemented.
