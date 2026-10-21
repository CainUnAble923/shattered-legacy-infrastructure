// cc-P280 (Chase, answer 4 and his addition): repair, as a League action and as items, crafted by smiths with high Arms
// Lore (F-26, cc-P226 made Arms Lore the repair skill: field maintenance and the smith repair's weaken chance).
//
//   - Field Repair Deed: one use; repairs one damaged item worn or in the backpack.
//   - Field Repair Kit: a number of charges (setting), each one item, for gear and for tools with durability (pickaxes,
//     hatchets and every other harvest tool are weapons in pinned, so they wear and repair like gear).
//   - Remote Repair Deed: one use; the League repair pass below, for a player below Blaze (the League action's rank).
//   - League repair (the Register's Blaze action): every damaged item worn, carried, and in the bank box ("at a
//     distance"), in one pass.
// "Repaired" means durability back to the maximum, as a smith's repair restores it, with no loss of maximum: the item was
// paid for with the crafter's ingots and Arms Lore. LeagueRegisterSettings.RepairPoints can make it a fixed amount instead.
// Durability is pinned's IDurability (weapons, armor, shields, clothing and jewellery that wear). Guild tools without
// durability (the Hammer of Hephaestus counts uses) are restored by their guilds, as before; P178 will price that.
//
// The crafting entries (Blacksmithy, plus Arms Lore at the setting's threshold as a second required skill) are added to
// DefBlacksmithy at ServerStarted, as BlacksmithyCraftRegistrations does.

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Items;
using Server.Targeting;

namespace Server.Engines.LeagueRegister;

public static class LeagueRepair
{
    public static bool NeedsRepair(Item item) => item is IDurability d && !item.Deleted && d.HitPoints < d.MaxHitPoints;

    /// <summary>Repairs one item: to the maximum, or by RepairPoints when that setting is above 0. True when it changed.</summary>
    public static bool Repair(Item item)
    {
        if (!NeedsRepair(item))
        {
            return false;
        }

        var d = (IDurability)item;
        var points = LeagueRegisterSettings.RepairPoints;
        d.HitPoints = points > 0 ? System.Math.Min(d.MaxHitPoints, d.HitPoints + points) : d.MaxHitPoints;
        return true;
    }

    /// <summary>Whether the item is the mobile's own and on them: worn, or anywhere in the backpack.</summary>
    public static bool IsCarried(Mobile from, Item item) =>
        item.Parent == from || from.Backpack != null && item.IsChildOf(from.Backpack);

    /// <summary>The pass: every damaged item worn, in the backpack and in the bank box. Returns how many were repaired.</summary>
    public static int RepairAll(Mobile from)
    {
        var items = new List<Item>();
        foreach (var item in from.Items)
        {
            if (item is not Container)
            {
                items.Add(item);
            }
        }

        foreach (var container in new[] { from.Backpack, from.FindItemOnLayer(Layer.Bank) as Container })
        {
            if (container == null)
            {
                continue;
            }

            foreach (var item in container.FindItemsByType<Item>())
            {
                items.Add(item);
            }
        }

        var count = 0;
        foreach (var item in items)
        {
            if (Repair(item))
            {
                count++;
            }
        }

        return count;
    }

    public static string Describe(int count) =>
        count == 0 ? "Nothing you wear, carry or bank needs repair." : $"Repaired {count} item{(count == 1 ? "" : "s")}.";

    /// <summary>A one-item repair from a deed or a kit: a target on the item; `used` runs when one was repaired.</summary>
    public static void BeginTarget(Mobile from, System.Action used)
    {
        from.SendMessage(0x59, "Target the item to repair.");
        from.BeginTarget(-1, false, TargetFlags.None, (m, targeted) =>
        {
            if (targeted is not Item item || !IsCarried(m, item))
            {
                m.SendMessage(0x22, "The item must be worn or in your backpack.");
            }
            else if (item is not IDurability)
            {
                m.SendMessage(0x22, "That cannot be repaired.");
            }
            else if (!Repair(item))
            {
                m.SendMessage(0x22, "That is already in full repair.");
            }
            else
            {
                m.SendMessage(0x44, "You repair the item.");
                m.PlaySound(0x2A);
                used();
            }
        });
    }

    // -- Crafting --------------------------------------------------------------------------------------------------

    private static bool _registered;

    public static void Configure()
    {
        EventSink.ServerStarted += Register;
    }

    /// <summary>Adds the three repair items to Blacksmithy. Idempotent; the test host calls it directly.</summary>
    public static void Register()
    {
        if (_registered || DefBlacksmithy.CraftSystem is not { } smith)
        {
            return;
        }

        _registered = true;
        const string group = "Repair Items";

        var index = smith.AddCraft(typeof(FieldRepairDeed), group, "field repair deed", 60.0, 100.0,
            typeof(IronIngot), 1044036, LeagueRegisterSettings.DeedIngots, 1044037);
        smith.AddSkill(index, SkillName.ArmsLore, LeagueRegisterSettings.DeedArmsLore, LeagueRegisterSettings.DeedArmsLore + 20);

        index = smith.AddCraft(typeof(FieldRepairKit), group, "field repair kit", 80.0, 120.0,
            typeof(IronIngot), 1044036, LeagueRegisterSettings.KitIngots, 1044037);
        smith.AddSkill(index, SkillName.ArmsLore, LeagueRegisterSettings.KitArmsLore, LeagueRegisterSettings.KitArmsLore + 20);

        index = smith.AddCraft(typeof(RemoteRepairDeed), group, "remote repair deed", 90.0, 130.0,
            typeof(IronIngot), 1044036, LeagueRegisterSettings.RemoteIngots, 1044037);
        smith.AddRes(index, typeof(StarSapphire), "Star Sapphire", LeagueRegisterSettings.RemoteGems, "You do not have the star sapphire needed.");
        smith.AddSkill(index, SkillName.ArmsLore, LeagueRegisterSettings.RemoteArmsLore, LeagueRegisterSettings.RemoteArmsLore + 20);
    }
}

[SerializationGenerator(0, false)]
public partial class FieldRepairDeed : Item
{
    [Constructible]
    public FieldRepairDeed() : base(0x14F0)
    {
        Name = "a field repair deed";
        Hue = 0x973;
        Weight = 1.0;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Repairs one item, once");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        LeagueRepair.BeginTarget(from, Delete);
    }
}

[SerializationGenerator(0, false)]
public partial class FieldRepairKit : Item, IUsesRemaining
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _usesRemaining;

    [Constructible]
    public FieldRepairKit() : base(0x1EB8)
    {
        Name = "a field repair kit";
        Hue = 0x973;
        Weight = 3.0;
        _usesRemaining = LeagueRegisterSettings.KitCharges;
    }

    public bool ShowUsesRemaining { get => true; set { } }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060584, _usesRemaining); // uses remaining: ~1_val~
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        LeagueRepair.BeginTarget(from, UseCharge);
    }

    public void UseCharge()
    {
        UsesRemaining--;
        InvalidateProperties();
        if (UsesRemaining <= 0)
        {
            Delete();
        }
    }
}

[SerializationGenerator(0, false)]
public partial class RemoteRepairDeed : Item
{
    [Constructible]
    public RemoteRepairDeed() : base(0x14F0)
    {
        Name = "a remote repair deed";
        Hue = 0x48D;
        Weight = 1.0;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Repairs everything you wear, carry and bank, once");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        var count = LeagueRepair.RepairAll(from);
        from.SendMessage(count > 0 ? 0x44 : 0x22, LeagueRepair.Describe(count));
        if (count > 0)
        {
            from.PlaySound(0x2A);
            Delete();
        }
    }
}
