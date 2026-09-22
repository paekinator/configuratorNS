# Changelog

Every heading below is a git tag on `main`. The matching player build is attached to the GitHub
Release of the same name (https://github.com/paekinator/configuratorNS/releases), so any past version
can be downloaded and run. `Tools/Release-Version.ps1` turns a section here into that release.

## Unreleased

- UI redesign merged: utility rail, bottom dock with Build / Blocks / Checkout tabs, Pro | Lite
  mode switch, block and project libraries, typography sets. Set space size, Parts & prices, the
  build/save guide and Prepare quote moved from the retired top-bar menu to rows on the rail.
- Release tooling: `Tools/Release-Version.ps1`; each player is stamped with its tag as the
  product version.

## v1.0.0-beta.1 - 2026-09-22

- Finish follows the Rhino NSFINISH rules: Cap Side rings at every connection level, Cap Ends and
  Feet aligned with yawed posts, a beam plugging into another beam breaks that channel, and a 2 mm
  seam allowance absorbs the whole-millimetre code quantization (fixes bare bottom channels on
  restored designs and Space pieces).
- Finish diagnostics: Tools > Configurator > Run Finish Diagnostics builds reference structures,
  reports per-channel coverage and renders evidence; `FinishDiagnostics.RunCode -finishCode <code>`
  does the same for one saved design.
- Local save and recovery, quote export, part pricing, Space merge and finish regressions from the
  September sessions.
