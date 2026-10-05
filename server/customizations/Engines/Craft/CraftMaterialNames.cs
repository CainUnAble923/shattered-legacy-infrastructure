// cc-P53 Part D (D62, Chase 2026-10-04): stock material names in the craft menus read in Title Case, like ours.
//
// Pinned names its stock sub-resources by cliloc, and those clilocs are upper case with the amount built in
// ("IRON (~1_AMT~)", 1044022; pinned DefBlacksmithy.cs:687-704, DefTinkering.cs:589-597, DefCarpentry.cs:601-607,
// DefBowFletching.cs:264-270, DefTailoring.cs:662-665, DefMasonry.cs:139-147). Ours are strings ("Platinum",
// ClusterFMiningExtension; the woods, ClusterFLumberjackingExtension), which the craft gump draws as
// "Platinum (12)" in the list and "Platinum (12 Available)" on the selected-material line (CraftGump.cs:155, :288).
// Chase chose to bring the stock names to ours, not the other way.
//
// After every Def*.Initialize (ServerStarted), each stock entry is replaced in place by the same sub-resource with a
// string name: same item type, required skill, generic name and message, same index, so a player's remembered choice
// (CraftContext.LastResourceIndex) and what the craft does with the resource are unchanged. CraftSubRes.Name has no
// setter, but CraftSubResCol is a List, so no pinned file is edited and no client cliloc either. The words are the
// cliloc's own, read from the client's Cliloc.enu, in Title Case. A cosmetic deviation (visible); register line in
// shard-migration notes cc-P53-two-hundred-cap.md, Part D.

using System;
using System.Collections.Generic;

namespace Server.Engines.Craft;

public static class CraftMaterialNames
{
    // cliloc -> its text, Title Case, without the "(~1_AMT~)" the gump now adds itself.
    public static readonly Dictionary<int, string> Names = new()
    {
        [1044022] = "Iron",
        [1044023] = "Dull Copper",
        [1044024] = "Shadow Iron",
        [1044025] = "Copper",
        [1044026] = "Bronze",
        [1044027] = "Gold",
        [1044028] = "Agapite",
        [1044029] = "Verite",
        [1044030] = "Valorite",
        [1060875] = "Red Scales",
        [1060876] = "Yellow Scales",
        [1060877] = "Black Scales",
        [1060878] = "Green Scales",
        [1060879] = "White Scales",
        [1060880] = "Blue Scales",
        [1072643] = "Wood",
        [1072644] = "Oak",
        [1072645] = "Ash",
        [1072646] = "Yew",
        [1072647] = "Heartwood",
        [1072648] = "Bloodwood",
        [1072649] = "Frostwood",
        [1044525] = "Normal",
        [1049150] = "Leather/Hides",
        [1049151] = "Spined Hides",
        [1049152] = "Horned Hides",
        [1049153] = "Barbed Hides"
    };

    public static void Configure()
    {
        EventSink.ServerStarted += () => Apply();
    }

    /// <summary>Every craft system that has sub-resources. Null entries (a system not initialized) are skipped.</summary>
    public static IEnumerable<CraftSystem> Systems()
    {
        yield return DefBlacksmithy.CraftSystem;
        yield return DefTinkering.CraftSystem;
        yield return DefCarpentry.CraftSystem;
        yield return DefBowFletching.CraftSystem;
        yield return DefTailoring.CraftSystem;
        yield return DefMasonry.CraftSystem;
        yield return DefAlchemy.CraftSystem;
        yield return DefInscription.CraftSystem;
        yield return DefCooking.CraftSystem;
        yield return DefCartography.CraftSystem;
        yield return DefGlassblowing.CraftSystem;
    }

    /// <summary>Renames every stock sub-resource. Idempotent; the test host calls it directly.</summary>
    public static int Apply()
    {
        var renamed = 0;

        foreach (var system in Systems())
        {
            if (system == null)
            {
                continue;
            }

            renamed += Rename(system.CraftSubRes);
            renamed += Rename(system.CraftSubRes2);
        }

        return renamed;
    }

    private static int Rename(CraftSubResCol col)
    {
        var renamed = 0;

        if (col.Name != null && col.Name.Number > 0 && Names.TryGetValue(col.Name.Number, out var title))
        {
            col.Name = title;
            renamed++;
        }

        for (var i = 0; i < col.Count; i++)
        {
            var res = col[i];

            if (res.Name == null || res.Name.Number <= 0)
            {
                continue;
            }

            if (!Names.TryGetValue(res.Name.Number, out var name))
            {
                Console.WriteLine($"[CraftMaterialNames] WARNING: no Title Case name for cliloc {res.Name.Number} ({res.ItemType.Name}).");
                continue;
            }

            col[i] = new CraftSubRes(res.ItemType, name, res.RequiredSkill, res.GenericNameNumber, res.Message);
            renamed++;
        }

        return renamed;
    }
}
