// cc-P95: D89, HEAD did not boot. ClusterFImbueKnowledge.Configure loads the imbuing table, the table's defaults read
// ImbueCatalogue.All, and the catalogue's skill rows read SkillInfo.Table, which pinned SkillsInfo.Configure fills
// (SkillsInfo.cs:133). Main.cs:667 runs every Configure through AssemblyHandler.Invoke, which sorts by CallPriority
// (AssemblyHandler.cs:143, default 50 in Attributes.cs:81) with List.Sort, which is not stable: two Configures at the
// same priority run in no promised order. The shard's start happened to run the imbuing one first.
//
// The test host fills SkillInfo.Table before any test runs, so no test can reproduce the start by calling the
// catalogue. These facts check the two things that make the start safe instead:
//   1. ClusterFImbueKnowledge.Configure has a strictly higher CallPriority than SkillsInfo.Configure, so it always
//      runs after the skill table is filled.
//   2. The catalogue is built on first use, not in a static initializer: with an empty skill table a build fails with
//      a plain InvalidOperationException (not a TypeInitializationException that poisons the type for the whole run),
//      and a build with the table filled matches the cached catalogue row for row.
// See shard-migration/notes/cc-P95-imbue-boot-fix.md.

using System;
using System.Linq;
using System.Reflection;
using Server;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ImbueStartOrderVerification
{
    private static int PriorityOf(MethodInfo mi) =>
        mi.GetCustomAttribute<CallPriorityAttribute>(true)?.Priority ?? 50;

    [Fact]
    public void ImbueKnowledgeConfigureRunsAfterSkillsAreConfigured()
    {
        var skills = typeof(SkillsInfo).GetMethod("Configure", BindingFlags.Public | BindingFlags.Static);
        var imbue = typeof(ClusterFImbueKnowledge).GetMethod("Configure", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(skills);
        Assert.NotNull(imbue);

        Assert.True(
            PriorityOf(imbue) > PriorityOf(skills),
            $"ClusterFImbueKnowledge.Configure is at CallPriority {PriorityOf(imbue)} and SkillsInfo.Configure at " +
            $"{PriorityOf(skills)}: the imbuing table can load before the skill table is filled (D89)."
        );
    }

    [Fact]
    public void CatalogueIsBuiltOnFirstUseNotInAStaticInitializer()
    {
        var saved = SkillInfo.Table;
        try
        {
            SkillInfo.Table = Array.Empty<SkillInfo>();
            Assert.Throws<InvalidOperationException>(() => ImbueCatalogue.Build());
        }
        finally
        {
            SkillInfo.Table = saved;
        }

        Assert.Equal(ImbueCatalogue.All.Select(d => d.Name), ImbueCatalogue.Build().Select(d => d.Name));
    }
}
