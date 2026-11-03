// cc-P785: chocolatiering in Cooking. Pinned's DefCooking already makes cocoa butter and cocoa liquor from cocoa pulp
// (Ingredients) and the dark, milk and white chocolates (Chocolatiering), all ML. This appends, at ServerStarted (the
// CookingCraftRegistrations shape), the rows of Configuration/chocolatiering.json to the same Chocolatiering group
// (1080001):
//   - sweet cocoa butter (ServUO's TOL recipe, the step pinned and our tree lacked);
//   - the three chocolate nutcrackers (ServUO's TOL recipes; our ChocolateNutcracker since cc-P128);
//   - the chocolate figures, five shapes in three chocolates, from a mold (after Carlos_ar's Chocolatiering Adventure).
// Pinned's craft engine has no CraftItem.Data, so the nutcracker's and the figure's kind is looked up from the craft
// entry itself when the item is made (RecipeOf, from ICraftable.OnCraft).
//
// No separate bowl: everything is Cooking with a pinned cooking tool, the sweet cocoa butter at an oven. No pinned
// recipe ids: our recipe gating (F-43) puts each entry in the Cooking recipe book of its minimum skill's band, like
// every other dish. The molds, sugar, vanilla and foil are sold by every provisioner (SBChocolatier).
//
// [Chocolatiering status] for staff.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Commands;
using Server.Items;

namespace Server.Engines.Craft;

public static class ChocolatieringRecipes
{
    public const int Group = 1080001; // Chocolatiering

    private static readonly Dictionary<CraftItem, ChocoRecipe> _byCraft = new();
    private static bool _registered;

    public static IReadOnlyDictionary<CraftItem, ChocoRecipe> Registered => _byCraft;

    public static ChocoRecipe? RecipeOf(CraftItem? craftItem) =>
        craftItem != null && _byCraft.TryGetValue(craftItem, out var r) ? r : null;

    public static CraftItem? CraftItemFor(string key) =>
        _byCraft.FirstOrDefault(kv => kv.Value.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Key;

    public static void Configure()
    {
        foreach (var line in ChocolatieringTable.LoadOrWriteDefaults(ChocolatieringTable.FilePath))
        {
            Console.WriteLine($"[Chocolatiering] {line}");
        }

        CommandSystem.Register("Chocolatiering", AccessLevel.GameMaster, OnCommand);
        EventSink.ServerStarted += () => Register();
    }

    /// <summary>Idempotent; the test host calls it directly because ServerStarted never fires there.</summary>
    public static int Register()
    {
        if (_registered)
        {
            return 0;
        }

        var cooking = DefCooking.CraftSystem;
        if (cooking == null)
        {
            Console.WriteLine("[Chocolatiering] WARNING: DefCooking.CraftSystem is null; the chocolatiering recipes are not added.");
            return 0;
        }

        _registered = true;
        var table = ChocolatieringTable.Current;
        if (!table.Enabled || !Core.ML)
        {
            return 0;
        }

        var n = 0;
        foreach (var r in table.Recipes)
        {
            if (r.ItemType == null || r.Resources.Count == 0 || r.Resources.Any(res => res.ItemType == null))
            {
                continue;
            }

            var first = r.Resources[0];
            var index = cooking.AddCraft(r.ItemType, Group, NameOf(r), ArtOf(r), r.MinSkill, r.MaxSkill,
                first.ItemType, ResourceName(first.ItemType!), first.Amount, 1044253);

            foreach (var res in r.Resources.Skip(1))
            {
                cooking.AddRes(index, res.ItemType, ResourceName(res.ItemType!), res.Amount, 1044253);
            }

            if (r.ItemType == typeof(SweetCocoaButter))
            {
                cooking.SetItemHue(index, 0x457);
            }
            else if (r.ItemType == typeof(ChocolateFigure))
            {
                cooking.SetItemHue(index, (int)r.ChocolateKind);
            }

            // The nutcracker comes wrapped (unhued) and takes its chocolate's hue when unwrapped (ChocolateNutcracker).

            cooking.SetNeededExpansion(index, Expansion.ML);
            if (r.NeedOven)
            {
                cooking.SetNeedOven(index, true);
            }

            _byCraft[cooking.CraftItems[index]] = r;
            n++;
        }

        Console.WriteLine($"[Chocolatiering] {n} recipes added to Cooking.");
        return n;
    }

    public static TextDefinition NameOf(ChocoRecipe r)
    {
        if (r.ItemType == typeof(SweetCocoaButter))
        {
            return 1156401; // Sweet Cocoa butter
        }

        if (r.ItemType == typeof(ChocolateNutcracker))
        {
            return r.ChocolateKind switch
            {
                ChocolateNutcracker.ChocolateType.Dark  => 1156390,
                ChocolateNutcracker.ChocolateType.White => 1156392,
                _                                       => 1156391
            };
        }

        var choc = ChocolateFigure.ChocolateName(r.ChocolateKind);
        return $"{char.ToUpperInvariant(choc[0])}{choc[1..]} Chocolate {r.FigureKind}";
    }

    public static int ArtOf(ChocoRecipe r) =>
        r.ItemType == typeof(SweetCocoaButter) ? 0x103D :
        r.ItemType == typeof(ChocolateNutcracker) ? 39952 :
        ChocolateFigure.ArtOf(r.FigureKind);

    /// <summary>The craft gump's name for a resource: pinned's and ServUO's clilocs where they exist, else plain words.</summary>
    public static TextDefinition ResourceName(Type type)
    {
        if (type == typeof(SackOfSugar)) return 1079997;
        if (type == typeof(CocoaButter)) return 1079998;
        if (type == typeof(CocoaLiquor)) return 1079999;
        if (type == typeof(Vanilla)) return 1080000;
        if (type == typeof(DarkChocolate)) return 1079994;
        if (type == typeof(MilkChocolate)) return 1079995;
        if (type == typeof(WhiteChocolate)) return 1079996;
        if (type == typeof(SweetCocoaButter)) return 1156401;
        if (type == typeof(GoldFoil)) return "foil sheet";
        if (typeof(ChocolateMold).IsAssignableFrom(type))
        {
            return type.Name.Replace("ChocolateMold", "").ToLowerInvariant() + " chocolate mold";
        }

        return type.Name;
    }

    [Usage("Chocolatiering [status]")]
    [Description("Chocolatiering in Cooking (cc-P785): says the table in force (Configuration/chocolatiering.json, read at start) and how many of its recipes are in Cooking, and checks the file on disk now; a changed file takes effect at the next restart.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.Any)]
    private static void OnCommand(CommandEventArgs e)
    {
        foreach (var line in Status())
        {
            e.Mobile.SendMessage(line);
        }
    }

    public static List<string> Status()
    {
        var t = ChocolatieringTable.Current;
        var lines = new List<string>
        {
            $"Chocolatiering {(t.Enabled ? "on" : "off")} ({t.Source}): {t.Recipes.Count} recipes, {_byCraft.Count} in Cooking; " +
            $"provisioners sell {string.Join(", ", t.Provisioner.Select(v => $"{v.Type} {v.Price}gp"))}."
        };

        if (System.IO.File.Exists(ChocolatieringTable.FilePath))
        {
            var errors = new List<string>();
            ChocolatieringTable.Parse(System.IO.File.ReadAllText(ChocolatieringTable.FilePath), "check", errors);
            lines.Add(errors.Count == 0
                ? "The file on disk is good; changes in it take effect at the next restart."
                : $"The file on disk would be refused: {string.Join("; ", errors)}");
        }

        return lines;
    }
}
