// cc-P123: what the six 2016 crafts' bulk order deeds share, ported from ServUO pub57 Scripts/Services/BulkOrders/
// SmallBODs/ and LargeBODs/. The deeds themselves (Small and Large Carpentry, Fletching, Tinker, Cooking, Alchemy and
// Inscription BODs) are beside this file, one per ServUO file, on pinned's SmallBOD and LargeBOD as P122 kept them. Each
// is a new type, so no saved deed changes shape.
//
// What each deed does is ServUO's, file by file: its hue, its lists (CraftBulkEntries.cs), its material chances, the roll in
// CreateRandomFor (amount by skill, a material the player's skill can work, exceptional at (skill + 80) / 200 from 70.1,
// and an entry the player can make, by the craft system's own success and exceptional chances), and its large deed's lists
// and rolls. The combine is ServUO's SmallBOD.EndCombine (:310-381) and LargeBOD.EndCombine (:217-279): pinned's own
// combine knows armour, clothing and weapons only, and these crafts make potions, scrolls, food, furniture and tools, which
// stack, so a stack fills its count at once as ServUO's does.
//
// Changes from ServUO, argued in the notes:
//   - "Exceptional" and "made of a material" are read from the item's Quality and Resource properties (pinned has no
//     IQuality or IResource). Many of these crafts' items carry neither in pinned (food, potions, scrolls, most furniture
//     and tinker parts), so ServUO's rolls would ask for something no item can be. A deed never asks for exceptional on a
//     type that has no quality, or for a material on a type that records none (CanBeExceptional, CanUseMaterial).
//   - ServUO's GraphicHue (the picture's tint in the gump) is not carried; pinned's gumps draw none.
//   - Skill is the skill's Base, as P122's smith and tailor rolls read it (ServUO adds a racial bonus floor).

using System;
using System.Collections.Generic;
using System.Reflection;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Gumps;
using Server.Items;

namespace Server.Engines.BulkOrders;

public static class CraftBOD
{
    // -- What an item is ---------------------------------------------------------------------------------------------

    private static readonly Dictionary<Type, (PropertyInfo Quality, PropertyInfo Resource)> _props = new();

    private static (PropertyInfo Quality, PropertyInfo Resource) Props(Type type)
    {
        if (!_props.TryGetValue(type, out var p))
        {
            var q = type.GetProperty("Quality", BindingFlags.Public | BindingFlags.Instance);
            var r = type.GetProperty("Resource", BindingFlags.Public | BindingFlags.Instance);

            p = (
                q is { CanRead: true } && q.PropertyType.IsEnum && Enum.IsDefined(q.PropertyType, "Exceptional") ? q : null,
                r is { CanRead: true } && r.PropertyType == typeof(CraftResource) ? r : null
            );

            _props[type] = p;
        }

        return p;
    }

    /// <summary>True when an item of this type can be exceptional on this shard (ServUO: is IQuality).</summary>
    public static bool CanBeExceptional(Type type) => type != null && Props(type).Quality != null;

    /// <summary>True when an item of this type records the material it was made of (ServUO: is IResource).</summary>
    public static bool CanUseMaterial(Type type) => type != null && Props(type).Resource != null;

    public static bool IsExceptional(Item item) =>
        Props(item.GetType()).Quality?.GetValue(item)?.ToString() == "Exceptional";

    public static BulkMaterialType GetMaterial(Item item) =>
        Props(item.GetType()).Resource?.GetValue(item) is CraftResource r ? GetMaterial(r) : BulkMaterialType.None;

    /// <summary>Pinned's SmallBOD.GetMaterial, and ServUO's woods (SmallBOD.cs:400-440).</summary>
    public static BulkMaterialType GetMaterial(CraftResource resource) =>
        resource switch
        {
            CraftResource.OakWood   => BulkMaterialType.OakWood,
            CraftResource.AshWood   => BulkMaterialType.AshWood,
            CraftResource.YewWood   => BulkMaterialType.YewWood,
            CraftResource.Heartwood => BulkMaterialType.Heartwood,
            CraftResource.Bloodwood => BulkMaterialType.Bloodwood,
            CraftResource.Frostwood => BulkMaterialType.Frostwood,
            _                       => SmallBOD.GetMaterial(resource)
        };

    /// <summary>ServUO SmallBOD.GetRequiredSkill (:383-436).</summary>
    public static double GetRequiredSkill(BulkMaterialType type) =>
        type switch
        {
            BulkMaterialType.DullCopper => 65.0,
            BulkMaterialType.ShadowIron => 70.0,
            BulkMaterialType.Copper     => 75.0,
            BulkMaterialType.Bronze     => 80.0,
            BulkMaterialType.Gold       => 85.0,
            BulkMaterialType.Agapite    => 90.0,
            BulkMaterialType.Verite     => 95.0,
            BulkMaterialType.Valorite   => 100.0,
            BulkMaterialType.Spined     => 65.0,
            BulkMaterialType.Horned     => 80.0,
            BulkMaterialType.Barbed     => 99.0,
            BulkMaterialType.OakWood    => 65.0,
            BulkMaterialType.AshWood    => 75.0,
            BulkMaterialType.YewWood    => 85.0,
            BulkMaterialType.Heartwood or BulkMaterialType.Bloodwood or BulkMaterialType.Frostwood => 95.0,
            _ => 0.0
        };

    // -- ServUO's material chances -----------------------------------------------------------------------------------

    /// <summary>ServUO SmallCarpentryBOD / SmallFletchingBOD (and their large deeds): none, oak .. frostwood.</summary>
    public static readonly double[] WoodChances =
    [
        0.513718750, // None
        0.292968750, // Oak
        0.117187500, // Ash
        0.046875000, // Yew
        0.018750000, // Heartwood
        0.007500000, // Bloodwood
        0.003000000  // Frostwood
    ];

    /// <summary>ServUO SmallTinkerBOD / LargeTinkerBOD: none, dull copper .. valorite.</summary>
    public static readonly double[] MetalChances =
    [
        0.501953125, // None
        0.250000000, // Dull Copper
        0.125000000, // Shadow Iron
        0.062500000, // Copper
        0.031250000, // Bronze
        0.015625000, // Gold
        0.007812500, // Agapite
        0.003906250, // Verite
        0.001953125  // Valorite
    ];

    // -- ServUO's CreateRandomFor, the parts every craft shares --------------------------------------------------------

    public static int AmountFor(double skill) =>
        skill switch
        {
            >= 70.1 => Utility.RandomList(10, 15, 20, 20),
            >= 50.1 => Utility.RandomList(10, 15, 15, 20),
            _       => Utility.RandomList(10, 10, 15, 20)
        };

    /// <summary>Up to 20 tries for a material the player's skill can work (ServUO, each CreateRandomFor).</summary>
    public static BulkMaterialType RollMaterialFor(double skill, BulkMaterialType start, double[] chances)
    {
        for (var i = 0; i < 20; ++i)
        {
            var check = BaseBOD.GetRandomMaterial(start, chances);

            if (skill >= GetRequiredSkill(check))
            {
                return check;
            }
        }

        return BulkMaterialType.None;
    }

    public static bool RollExceptional(double skill) => skill >= 70.1 && (skill + 80.0) / 200.0 > Utility.RandomDouble();

    /// <summary>An entry the player can make (and make exceptional when asked), as ServUO picks it.</summary>
    public static SmallBulkEntry PickEntry(Mobile m, SmallBulkEntry[] entries, CraftSystem system, bool reqExceptional)
    {
        var valid = new List<SmallBulkEntry>();

        foreach (var entry in entries)
        {
            var item = system.CraftItems.SearchFor(entry.Type);

            if (item == null)
            {
                continue;
            }

            var chance = item.GetSuccessChance(m, null, system, false, out var allRequiredSkills);

            if (!allRequiredSkills || chance < 0.0)
            {
                continue;
            }

            if (reqExceptional)
            {
                chance = item.GetExceptionalChance(system, chance, m);
            }

            if (chance > 0.0)
            {
                valid.Add(entry);
            }
        }

        return valid.Count > 0 ? valid.RandomElement() : null;
    }

    // -- The combine (ServUO SmallBOD.EndCombine :310-381, LargeBOD.EndCombine :217-279) -------------------------------

    public static void EndCombineSmall(SmallBOD deed, Mobile from, Item item, Func<Item, bool> checkType)
    {
        if (item?.IsChildOf(from.Backpack) != true)
        {
            from.SendLocalizedMessage(1045158); // You must have the item in your backpack to target it.
            return;
        }

        if (deed.AmountCur >= deed.AmountMax)
        {
            // The maximum amount of requested items have already been combined to this deed.
            from.SendLocalizedMessage(1045166);
            return;
        }

        if (!checkType(item))
        {
            from.SendLocalizedMessage(1045169); // The item is not in the request.
            return;
        }

        if (deed.Material != BulkMaterialType.None && GetMaterial(item) != deed.Material)
        {
            from.SendLocalizedMessage(1157310); // The item is not made from the requested resource.
            return;
        }

        if (deed.RequireExceptional && !IsExceptional(item))
        {
            from.SendLocalizedMessage(1045167); // The item must be exceptional.
            return;
        }

        if (item.Amount > 1)
        {
            if (deed.AmountCur + item.Amount > deed.AmountMax)
            {
                // You have provided more than which has been requested by this deed.
                from.SendLocalizedMessage(1157222);
                return;
            }

            deed.AmountCur += item.Amount;
            item.Delete();
        }
        else
        {
            item.Delete();
            ++deed.AmountCur;
        }

        from.SendLocalizedMessage(1045170); // The item has been combined with the deed.
        from.SendGump(new SmallBODGump(deed));

        if (deed.AmountCur < deed.AmountMax)
        {
            deed.BeginCombine(from);
        }
    }

    public static bool CheckSmallType(SmallBOD deed, Type itemType) =>
        deed.Type != null && (itemType == deed.Type || itemType.IsSubclassOf(deed.Type));

    public static void EndCombineLarge(LargeBOD deed, Mobile from, Item item, Func<SmallBOD, Type, bool> checkType)
    {
        if (item?.IsChildOf(from.Backpack) != true)
        {
            from.SendLocalizedMessage(1045158); // You must have the item in your backpack to target it.
            return;
        }

        if (item is not SmallBOD small)
        {
            from.SendLocalizedMessage(1045159); // That is not a bulk order.
            return;
        }

        LargeBulkEntry entry = null;

        foreach (var e in deed.Entries)
        {
            if (checkType(small, e.Details.Type))
            {
                entry = e;
                break;
            }
        }

        if (entry == null)
        {
            from.SendLocalizedMessage(1045160); // That is not a bulk order for this large request.
        }
        else if (deed.RequireExceptional && !small.RequireExceptional)
        {
            from.SendLocalizedMessage(1045161); // Both orders must be of exceptional quality.
        }
        else if (deed.Material != BulkMaterialType.None && small.Material != deed.Material)
        {
            from.SendLocalizedMessage(1157311); // Both orders must use the same resource type.
        }
        else if (deed.AmountMax != small.AmountMax)
        {
            // The two orders have different requested amounts and cannot be combined.
            from.SendLocalizedMessage(1045163);
        }
        else if (small.AmountCur < small.AmountMax)
        {
            from.SendLocalizedMessage(1045164); // The order to combine with is not completed.
        }
        else if (entry.Amount >= deed.AmountMax)
        {
            // The maximum amount of requested items have already been combined to this deed.
            from.SendLocalizedMessage(1045166);
        }
        else
        {
            entry.Amount += small.AmountCur;
            small.Delete();

            from.SendLocalizedMessage(1045165); // The orders have been combined.
            from.SendGump(new LargeBODGump(deed));

            if (!deed.Complete)
            {
                deed.BeginCombine(from);
            }
        }
    }

    /// <summary>ServUO SmallBOD.CheckType(Type) as LargeBOD.CheckType calls it: the large entry's type is the small's.</summary>
    public static bool CheckLargeType(SmallBOD small, Type entryType) =>
        small.Type != null && entryType != null && (entryType == small.Type || entryType.IsSubclassOf(small.Type));

    // -- Large deeds -------------------------------------------------------------------------------------------------

    private static readonly Dictionary<SmallBulkEntry[], SmallBulkEntry[]> _makeable = new();

    /// <summary>
    /// A large deed's entries: ServUO's list less any item this craft cannot make on the shard. A large deed fills only
    /// from small deeds, and no small deed is offered for an item nobody can make (PickEntry), so such an entry would make
    /// the whole deed unfillable. Tinkering's left-hand spoons, forks and knives make the plain ones' entries
    /// (SmallTinkerBOD.CheckTinkerType). Kept per list, once.
    /// </summary>
    public static LargeBulkEntry[] Convert(LargeBOD owner, SmallBulkEntry[] list, CraftSystem system)
    {
        if (!_makeable.TryGetValue(list, out var kept))
        {
            var result = new List<SmallBulkEntry>();

            foreach (var e in list)
            {
                if (system.CraftItems.SearchFor(e.Type) != null || SmallTinkerBOD.HasMakeableTwin(e.Type, system))
                {
                    result.Add(e);
                }
            }

            _makeable[list] = kept = result.ToArray();
        }

        return LargeBulkEntry.ConvertEntries(owner, kept);
    }

    /// <summary>
    /// ServUO rolls exceptional on 82.5% of large deeds, and a material from the craft's chances. Here neither is asked of a
    /// list whose first item cannot carry it (ServUO's rule, read from the item as for small deeds).
    /// </summary>
    public static void RollLarge(
        LargeBOD deed, int hue, LargeBulkEntry[] entries, bool exceptionalAllowed, BulkMaterialType start, double[] chances
    )
    {
        var first = entries.Length > 0 ? entries[0].Details.Type : null;

        deed.Hue = hue;
        deed.AmountMax = Utility.RandomList(10, 15, 20, 20);
        deed.Entries = entries;
        deed.RequireExceptional = exceptionalAllowed && CanBeExceptional(first) && 0.825 > Utility.RandomDouble();
        deed.Material = chances != null && CanUseMaterial(first)
            ? BaseBOD.GetRandomMaterial(start, chances)
            : BulkMaterialType.None;
    }

    public static int Hue(BODType type) => BulkOrderSystem.GetBodHue(type);
}
