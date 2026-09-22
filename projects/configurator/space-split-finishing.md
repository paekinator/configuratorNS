# Space split finishing correction — 15 September 2026

This work is confined to `C:/Users/Nebula PC/Documents/configurator beta 1.0.0`, including its isolated `tmp/task-validation` project and build outputs. The earlier project folder was not changed.

## Reproduced faults and corrections

- Installed horizontal connector tips are approximately 20 mm from a post's axis, at its socket face. Post finishing previously required them to be within 6 mm of the axis. This missed real joints and allowed a veneer or Cap Side over an occupied socket. Joint detection now checks depth toward the post face separately from alignment with the hole. It retains the 6 mm alignment tolerance and rejects tips outside the connection depth.
- Freezing a Space piece or stripping a derived split frame removes its AttachmentPoint components. Its named Peg A/B transforms survive. Frame records now recover those transforms, preserving physical endpoints, center and signed orientation after splitting or rotation. Merge records and replacement placement use the same endpoint midpoint.
- Splitting one T beam at an inserted post previously produced two T segments. The new internal joint needs an ordinary H connection. The segment at the original outer Peg A retains the T family; remaining segments use H. The original attachment position is preserved. This correction covers single-beam splits; the separate policy for rebuilding a line of multiple T beams is unchanged.
- Space finish now refreshes immediately after a committed merge, including when the Build finish toggle is off or the placement preview remains armed. Cancelling a drag restores its starting positions and clears the busy state. A missed mouse-release edge completes the last accepted position without sampling a stale cursor. Rotation and duplication first cancel any temporary drag, so their new placement and undo/redo records agree.

Existing panel masking, exposed top/bottom channel rules, supplied veneer meshes and placeholder prices are retained. The complete validation includes their earlier regressions.

## Verification scope

The corrected baseline fixture uses upright V9 posts with independently checked 745 mm height and 41 mm footprint. With the previous production code, 28 of 60 joint checks, 46 of 237 actual-prefab split checks, and 7 of 16 lifecycle checks failed. The first exploratory run had a sideways V9 fixture; its penetration findings were discarded. The retained `before-*.txt` evidence comes from the corrected upright fixture.

The split tests use the actual H7/T7/H15/T15/V9 prefabs and imported finish FBXs. They cover frozen endpoints at 0°, 37°, 90°, 180° and 270°, plus actual H15 and T15 splits at all four cardinal rotations. Independent mesh bounds check exposed channel coverage, occupied sockets, physical post penetration, and the retained T attachment. Pure joint checks include V9/V25 posts, nominal and physical peg tips, tolerance boundaries and mirrored T connections. Lifecycle tests exercise immediate panel-split refresh and interrupted input.

The final Unity 6000.5.0f1 run passed **1,344 checks with zero application failures**: 641 pure checks and 703 runtime checks. These include 237 actual-prefab split checks and 22 lifecycle checks, with six additional rotation/duplication pose and history assertions. The existing history, restore, UI, finish, exposed top/bottom and installed-parts/pricing suites also passed. `suite-summary.txt` records each suite's count. Unity's known editor-only SearchDatabase startup diagnostic is retained separately in the report; application exceptions are treated as failures.

SHA-256 comparisons matched all 460 files in the scripts, prefabs, finish resources and entry-scene validation scope against this beta workspace. See `source-verification.json`. Validation ran in the isolated project; generated editor settings and font changes were confined there.

Evidence directory: `validation/space-split-finishing`. The rendered `h15-split-post-joint.png` and `t15-split-post-joint.png` images are close-ups of actual generated geometry. Automated scene tests and rendered inspection complement each other; they are not a recorded manual browser session.

## Windows candidate

`Builds/Windows-20260915-162222-836.zip` contains the fresh non-development Windows build (60,695,100 bytes). Extract the whole archive and run `configurator.exe`. For source testing, open this beta project and restart Play Mode so Unity recompiles the changed scripts and regenerates the finish.

The build completed with zero errors and 867 warnings, including Unity API and shader warnings. Build metadata is retained in `validation/space-split-finishing/build-report.json`. The player loaded the configurator scene and remained running through an eight-second headless startup check with no logged errors or exceptions; the temporary process was then stopped. The archive has 202 entries and excludes Unity's DoNotShip output. This startup check complements the earlier rendered Unity scene tests; it does not exercise an interactive standalone session.

SHA-256: `b0c676d07849f6c91c6997eb4e2fb6caffae1462ac7017e5c8d4c4ad45c111de`.

No live website deployment was performed in this correction.
