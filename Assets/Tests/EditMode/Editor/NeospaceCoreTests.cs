#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

public class NeospaceCoreTests
{
    [Test]
    public void CoreSelfTest_Passes()
    {
        int total = NeospaceSelfTest.RunAll(out List<string> failures);
        Assert.Greater(total, 0);
        Assert.IsEmpty(failures, failures.Count > 0 ? string.Join("\n", failures) : "");
    }

    [Test]
    public void ModuleConstant_Is88mm()
    {
        Assert.AreEqual(88f, CatalogueData.ModuleMm);
        // Prefabs are modelled at 1 unit = 100 mm, so one module is 0.88 world units.
        Assert.AreEqual(0.88f, NeospaceUnits.ModuleMeters, 1e-4f);
    }
}
#endif
