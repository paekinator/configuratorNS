using System.Collections.Generic;

/// <summary>
/// Selftests for the Blocks logic — pure, no scene needed.
/// Run from Tools → Configurator → Run ModuleSolver Selftest.
///
/// Two things are covered, both of them rules rather than plumbing:
/// <see cref="ModuleSolver.Components"/>, which decides where one block ends
/// and the next begins, and <see cref="BlockCollections.NextUntitledName"/>,
/// which the owner specified exactly.
///
/// The rest of the solver is glue that reads Unity's own pairing records, and
/// a test of that would only assert that Unity stores what it stores. What it
/// needs instead is reconciliation against a real scene, which is what
/// ModuleSolver.Reconcile and the scene report do.
/// </summary>
public static class ModuleSolverSelfTest
{
    public static int RunAll(out List<string> failures)
    {
        var failureList = new List<string>();
        failures = failureList;
        int total = 0;

        void Check(bool ok, string label)
        {
            total++;
            if (!ok)
                failureList.Add(label);
        }

        string Shape(List<List<int>> groups)
        {
            var parts = new List<string>();
            foreach (List<int> g in groups)
                parts.Add(string.Join(",", g));
            return string.Join(" | ", parts);
        }

        // --- nothing, and one thing --------------------------------------
        Check(Shape(ModuleSolver.Components(0, null)) == string.Empty,
            "an empty scene has no modules");

        Check(Shape(ModuleSolver.Components(1, null)) == "0",
            "a single unconnected frame is a module of one");

        Check(Shape(ModuleSolver.Components(3, null)) == "0 | 1 | 2",
            "three loose frames are three modules, not one");

        // --- a chain is one module ---------------------------------------
        Check(Shape(ModuleSolver.Components(3, new[] { (0, 1), (1, 2) })) == "0,1,2",
            "a chain of three joins into one module");

        // --- two separate structures -------------------------------------
        Check(Shape(ModuleSolver.Components(4, new[] { (0, 1), (2, 3) })) == "0,1 | 2,3",
            "two disconnected pairs stay two modules");

        // --- order must not matter ---------------------------------------
        // Attachment points are walked in registry order, which changes as
        // parts are added and removed (the registry removes by swap). The same
        // structure must still yield the same modules.
        string forwards = Shape(ModuleSolver.Components(5, new[] { (0, 1), (1, 2), (3, 4) }));
        string backwards = Shape(ModuleSolver.Components(5, new[] { (3, 4), (1, 2), (0, 1) }));
        Check(forwards == backwards, "edge order does not change the result");
        Check(forwards == "0,1,2 | 3,4", "and that result is the expected one");

        // --- both halves of every pairing report it ----------------------
        // PlacementCommit writes pairedWith on BOTH points, so every edge is
        // seen twice. Union has to be idempotent or a module would split.
        Check(Shape(ModuleSolver.Components(3, new[] { (0, 1), (1, 0), (1, 2), (2, 1) })) == "0,1,2",
            "an edge reported from both ends is still one edge");

        // --- a cycle is not an infinite walk ------------------------------
        // Four posts joined in a ring by four beams is the commonest module
        // there is; a naive walk that re-visits would never finish.
        Check(Shape(ModuleSolver.Components(4, new[] { (0, 1), (1, 2), (2, 3), (3, 0) })) == "0,1,2,3",
            "a closed ring of frames is one module");

        // --- a part joined to itself ---------------------------------------
        // Cannot happen from a placement, but a corrupted pairing would look
        // like this and must not merge anything.
        Check(Shape(ModuleSolver.Components(2, new[] { (0, 0) })) == "0 | 1",
            "a self-pairing joins nothing");

        // --- out-of-range edges are ignored, not thrown -------------------
        // A pairing can point at a part that was filtered out of the node set
        // (a ghost, or a root with no catalogue id). That must not take the
        // whole solve down mid-build.
        Check(Shape(ModuleSolver.Components(2, new[] { (0, 5), (-1, 1) })) == "0 | 1",
            "a pairing to something outside the scene set is skipped");

        // --- a long chain still terminates ---------------------------------
        var chain = new List<(int, int)>();
        for (int i = 0; i < 999; i++)
            chain.Add((i, i + 1));
        List<List<int>> big = ModuleSolver.Components(1000, chain);
        Check(big.Count == 1 && big[0].Count == 1000,
            "a thousand frames in a chain are one module");

        // --- what a freshly captured block is called ----------------------
        // Stated precisely by the owner: "Untitled Block" by default, and a
        // suffix only once that name is taken. The suffix exists to tell two
        // apart, so the first one does not carry it.
        string Next(params string[] taken) => BlockCollections.NextUntitledName(taken);

        Check(Next() == "Untitled Block",
            "the first block in an empty collection is plain 'Untitled Block'");

        Check(Next("Untitled Block") == "Untitled Block 01",
            "the second takes the first suffix");

        Check(Next("Untitled Block", "Untitled Block 01") == "Untitled Block 02",
            "and the third the next");

        Check(Next("Low-shelf", "Wide shelf") == "Untitled Block",
            "named blocks do not push the numbering along");

        Check(Next("Untitled Block", "Untitled Block 02") == "Untitled Block 01",
            "a gap left by a deleted block is filled, not skipped");

        Check(Next("untitled block") == "Untitled Block 01",
            "the name already taken is matched whatever its case");

        Check(Next("  Untitled Block  ") == "Untitled Block 01",
            "and whatever whitespace surrounds it");

        Check(Next(null, "", "   ") == "Untitled Block",
            "blank names are not names");

        // --- a block is a thing, not a place ------------------------------
        // A captured module's positions are rewritten from its own corner, by
        // a whole number of 88 mm modules, so the same wall built anywhere
        // saves as the same block. The shift itself is what is checked here;
        // that it lands on the lattice is the whole point.
        int Floor(int mm) => ConfigurationCapture.ModuleFloor(mm);

        Check(Floor(0) == 0, "a module already at the origin does not move");
        Check(Floor(968) == 968, "a shift lands on the lattice (968 = 11 modules)");
        Check(Floor(2552) == 2552, "and so does one from further out (2552 = 29)");

        // The real numbers from the two walls in the test scene: different
        // places, same wall, so the same distance between the posts.
        Check(1848 - Floor(968) == 880 && 3608 - Floor(2552) == 1056,
            "each wall's far post keeps its own spacing");

        Check(Floor(-176) == -176,
            "a module left of the origin shifts by whole modules too (-176 = -2)");

        // Truncation rounds toward zero and would send this to -88, so the
        // same module built left of the origin would not match itself built
        // right of it.
        Check(Floor(-176) != -88, "the shift floors rather than truncates");

        Check(Floor(100) == 88 && Floor(87) == 0,
            "an off-lattice position floors to the module below, never above");

        return total;
    }
}
