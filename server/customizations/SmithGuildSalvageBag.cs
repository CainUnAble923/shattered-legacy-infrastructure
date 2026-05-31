using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Craft;
using Server.Mobiles;

namespace Server.Items;

// ─────────────────────────────────────────────────────────────────────────────
// SmithGuildSalvageBag — Society of Smiths guild salvage bag
//
// Issued to members on join.  Fully replaces the vanilla SalvageBag for
// smithing guild members.
//
// Enhancements over the vanilla SalvageBag:
//   1. Post-Valorite metal support — Platinum through Celestial.
//      Resmelt difficulty scales with extended skill (>100 Mining required).
//   2. Smithing Seals — guild members earn seals for each item smelted,
//      scaled by material tier.
//   3. [TODO — Guild Enhancement: "Forge Efficiency" upgrade]
//      Non-exceptional crafted items auto-filed into matching SmithGuildBook
//      BODs instead of smelting.  Enable via a future guild upgrade flag.
//      See HammerBODAutoFill.TryAutoFill for the matching pattern.
// ─────────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class SmithGuildSalvageBag : Bag
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private const int BagHue  = 1154; // Society of Smiths blue
    private const int BagItemID = 0xE76; // standard bag

    [Constructible]
    public SmithGuildSalvageBag()
    {
        Hue      = BagHue;
        ItemID   = BagItemID;
        Name     = "Smith Guild Salvage Bag";
        LootType = LootType.Blessed;
        Weight   = 2.0;
    }

    public SmithGuildSalvageBag(Serial serial) : base(serial) { }

    public override int LabelNumber => 1079931; // "Salvage Bag" cliloc (reused)

    // ── Resmelt difficulty table ───────────────────────────────────────────────
    // Vanilla metals cap at 99.0 (base Mining).
    // Post-Valorite metals require extended Mining skill (power scrolls).
    // Scale mirrors ClusterFSmithCommissions skill ranges.

    private static double ResmeltDifficulty(CraftResource r) => r switch
    {
        CraftResource.DullCopper  =>  65.0,
        CraftResource.ShadowIron  =>  70.0,
        CraftResource.Copper      =>  75.0,
        CraftResource.Bronze      =>  80.0,
        CraftResource.Gold        =>  85.0,
        CraftResource.Agapite     =>  90.0,
        CraftResource.Verite      =>  95.0,
        CraftResource.Valorite    =>  99.0,
        // Post-Valorite — requires extended Mining skill
        CraftResource.Platinum    => 105.0,
        CraftResource.Toxic       => 115.0,
        CraftResource.Blaze       => 130.0,
        CraftResource.Frost       => 150.0,
        CraftResource.Obsidian    => 175.0,
        CraftResource.Mythril     => 200.0,
        CraftResource.Adamantium  => 250.0,
        CraftResource.Celestial   => 300.0,
        _                         =>   0.0,  // Iron — no check
    };

    // ── Seals per item smelted ────────────────────────────────────────────────
    // Awarded only to guild members.  Small passive income scaled by tier.

    private static int SealReward(CraftResource r) => r switch
    {
        CraftResource.Iron                               => 1,
        CraftResource.DullCopper or CraftResource.ShadowIron => 2,
        CraftResource.Copper     or CraftResource.Bronze     => 3,
        CraftResource.Gold       or CraftResource.Agapite    => 5,
        CraftResource.Verite     or CraftResource.Valorite   => 7,
        CraftResource.Platinum   or CraftResource.Toxic      => 10,
        CraftResource.Blaze      or CraftResource.Frost      => 14,
        CraftResource.Obsidian   or CraftResource.Mythril    => 18,
        CraftResource.Adamantium                             => 24,
        CraftResource.Celestial                              => 30,
        _                                                    =>  0,
    };

    // ── Context menu ──────────────────────────────────────────────────────────

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from.Alive)
        {
            var inPack      = IsChildOf(from.Backpack);
            var hasIngotable = inPack && HasResmeltable();
            var hasCuttable  = inPack && HasScissorable();
            list.Add(new SalvageIngotsEntry(hasIngotable));
            list.Add(new SalvageClothEntry(hasCuttable));
            list.Add(new SalvageAllEntry(hasIngotable && hasCuttable));
        }
    }

    // ── Resmelt ───────────────────────────────────────────────────────────────

    private bool _failure;

    private bool TryResmelt(Mobile from, Item item, CraftResource resource)
    {
        try
        {
            if (CraftResources.GetType(resource) != CraftResourceType.Metal)
                return false;

            var info = CraftResources.GetInfo(resource);
            if (info == null || info.ResourceTypes.Length == 0)
                return false;

            var craftItem = DefBlacksmithy.CraftSystem.CraftItems.SearchFor(item.GetType());
            if (craftItem == null || craftItem.Resources.Count == 0)
                return false;

            var craftResource = craftItem.Resources[0];
            if (craftResource.Amount < 2)
                return false;

            var difficulty = ResmeltDifficulty(resource);
            var ingot      = info.ResourceTypes[0].CreateInstance<Item>();

            // Ingot amount — use raw skill (no 100-cap) to reward extended Mining
            if (item is DragonBardingDeed
                || item is BaseArmor  a && a.PlayerConstructed
                || item is BaseWeapon w && w.PlayerConstructed
                || item is BaseClothing c && c.PlayerConstructed)
            {
                var mining = from.Skills.Mining.Value;
                var amount = ((4 + mining) * craftResource.Amount - 4) * 0.0068;
                ingot.Amount = Math.Max(2, (int)amount);
            }
            else
            {
                ingot.Amount = 2;
            }

            if (difficulty > from.Skills.Mining.Value)
            {
                _failure = true;
                ingot.Delete();
                return false; // skill check failed — item stays in bag, no seals
            }

            item.Delete();
            from.AddToBackpack(ingot);
            from.PlaySound(0x2A);
            from.PlaySound(0x240);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        return false;
    }

    // ── Salvage ingots ────────────────────────────────────────────────────────

    private void SalvageIngots(Mobile from)
    {
        if (from.Backpack == null) return;

        var hasTool = false;
        foreach (var tool in from.Backpack.FindItemsByType<BaseTool>())
        {
            if (tool.CraftSystem == DefBlacksmithy.CraftSystem)
            {
                hasTool = true;
                break;
            }
        }

        if (!hasTool)
        {
            from.SendLocalizedMessage(1079822); // need a blacksmithing tool
            return;
        }

        DefBlacksmithy.CheckAnvilAndForge(from, 2, out _, out var forge);
        if (!forge)
        {
            from.SendLocalizedMessage(1044265); // must be near a forge
            return;
        }

        var salvaged    = 0;
        var notSalvaged = 0;
        var totalSeals  = 0;

        // Collect resources for seal accounting before items are deleted
        var resources = new List<CraftResource>();
        foreach (var item in Items)
        {
            if (item?.Deleted != false) continue;
            if (item is BaseArmor  ba) resources.Add(ba.Resource);
            else if (item is BaseWeapon bw) resources.Add(bw.Resource);
            else resources.Add(CraftResource.None);
        }

        _failure = false;
        var idx = 0;

        using var queue = EnumerateItems();
        foreach (var item in queue)
        {
            if (item?.Deleted != false) { idx++; continue; }

            CraftResource res = CraftResource.None;
            bool ok = false;

            if (item is BaseArmor armor)
            {
                res = armor.Resource;
                ok  = TryResmelt(from, armor, res);
            }
            else if (item is BaseWeapon weapon)
            {
                res = weapon.Resource;
                ok  = TryResmelt(from, weapon, res);
            }
            else if (item is DragonBardingDeed)
            {
                res = CraftResource.Iron;
                ok  = TryResmelt(from, item, res);
            }

            if (ok) { salvaged++; totalSeals += SealReward(res); }
            else    { notSalvaged++; }
            idx++;
        }

        if (_failure)
        {
            from.SendLocalizedMessage(1079975); // failed to smelt some metal
            _failure = false;
        }
        else
        {
            from.SendLocalizedMessage(1079973, $"{salvaged}\t{salvaged + notSalvaged}");
        }

        // Award Smithing Seals to guild members + achievement notification
        if (from is PlayerMobile pm && pm.Account is IAccount acct)
        {
            // Smelting achievement — track regardless of guild membership
            if (salvaged > 0)
                ClusterFAchievementSystem.NotifyIngotsSmelted(pm, salvaged);

            if (totalSeals > 0 && ClusterFGuildSystem.IsJoined(acct, "smithing"))
            {
                var data = ClusterFAccountPersistence.GetOrCreate(acct);
                data.AddCurrency("smithing", totalSeals);
                pm.SendMessage(0x59, $"[Salvage] +{totalSeals} Smithing Seal{(totalSeals == 1 ? "" : "s")} earned.");
            }
        }
    }

    // ── Salvage cloth — mirrors vanilla exactly ────────────────────────────────

    private static readonly Type[] _clothTypes =
    {
        typeof(Leather), typeof(Cloth), typeof(SpinedLeather), typeof(HornedLeather),
        typeof(BarbedLeather), typeof(Bandage), typeof(Bone),
    };

    private void SalvageCloth(Mobile from)
    {
        if (from.Backpack == null) return;

        var scissors = from.Backpack.FindItemByType<Scissors>();
        if (scissors == null)
        {
            from.SendLocalizedMessage(1079823); // need scissors
            return;
        }

        var salvaged    = 0;
        var notSalvaged = 0;

        using (var queue = EnumerateItems())
        {
            foreach (var item in queue)
            {
                if (item is not IScissorable scissorable) continue;

                if (Scissors.CanScissor(from, scissorable) && scissorable.Scissor(from, scissors))
                    salvaged++;
                else
                    notSalvaged++;
            }
        }

        from.SendLocalizedMessage(1079974, $"{salvaged}\t{salvaged + notSalvaged}");

        using (var queue = EnumerateItems())
        {
            foreach (var item in queue)
            {
                if (item.InTypeList(_clothTypes))
                    from.AddToBackpack(item);
            }
        }
    }

    private void SalvageAll(Mobile from)
    {
        SalvageIngots(from);
        SalvageCloth(from);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool HasResmeltable()
    {
        foreach (var i in Items)
        {
            if (i?.Deleted == false && (
                    i is BaseWeapon w && CraftResources.GetType(w.Resource) == CraftResourceType.Metal ||
                    i is BaseArmor  a && CraftResources.GetType(a.Resource) == CraftResourceType.Metal ||
                    i is DragonBardingDeed))
                return true;
        }
        return false;
    }

    private bool HasScissorable()
    {
        foreach (var i in Items)
        {
            if (i is not IScissorable || i.Deleted) continue;
            if (i is BaseClothing or Cloth or BoltOfCloth or Hides or BonePile ||
                i is BaseArmor a && CraftResources.GetType(a.Resource) == CraftResourceType.Leather)
                return true;
        }
        return false;
    }

    // ── Join bonus ────────────────────────────────────────────────────────────

    /// <summary>Issues a SmithGuildSalvageBag when a player joins the Society of Smiths.</summary>
    public static void OnSmithingJoined(PlayerMobile pm)
    {
        if (pm.Backpack == null) return;

        foreach (var item in pm.Backpack.Items)
            if (item is SmithGuildSalvageBag) return;

        pm.Backpack.DropItem(new SmithGuildSalvageBag());
        pm.SendMessage(0x44, "You have been issued a Smith Guild Salvage Bag. " +
            "Place metal items inside and right-click to smelt them down — " +
            "guild members earn Smithing Seals for each item salvaged.");
    }

    // ── Context menu entries ──────────────────────────────────────────────────

    private class SalvageIngotsEntry : ContextMenuEntry
    {
        public SalvageIngotsEntry(bool enabled) : base(6277) => Enabled = enabled;

        public override void OnClick(Mobile from, IEntity target)
        {
            if (from.CheckAlive() && target is SmithGuildSalvageBag { Deleted: false } bag)
                bag.SalvageIngots(from);
        }
    }

    private class SalvageClothEntry : ContextMenuEntry
    {
        public SalvageClothEntry(bool enabled) : base(6278) => Enabled = enabled;

        public override void OnClick(Mobile from, IEntity target)
        {
            if (from.CheckAlive() && target is SmithGuildSalvageBag { Deleted: false } bag)
                bag.SalvageCloth(from);
        }
    }

    private class SalvageAllEntry : ContextMenuEntry
    {
        public SalvageAllEntry(bool enabled) : base(6276) => Enabled = enabled;

        public override void OnClick(Mobile from, IEntity target)
        {
            if (from.CheckAlive() && target is SmithGuildSalvageBag { Deleted: false } bag)
                bag.SalvageAll(from);
        }
    }
}
