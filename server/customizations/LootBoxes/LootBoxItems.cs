// cc-P79 (F-40): the loot box and the things only a box gives. Notes: shard-migration notes/cc-P79-loot-boxes-fanfare.md.
//
// LootBox                its contents are rolled when it is earned (LootBoxRoller) and kept inside it on the internal map;
//                        opening hands them over and never re-rolls. Bound to the earning account. Opens only in a guarded
//                        region or in a house where the opener is owner, co-owner or friend. Deletes itself once empty;
//                        a box opened and not emptied is removed after LootTable.AbandonedBoxDays (ClusterFLootBoxes).
// BoundCosmeticSash      a box cosmetic: wearable, uniquely hued, bound to the account.
// LoopScrollFragment     five make one loop scroll. No loop scroll exists yet (F-12), so the combine is a hook.
// EpicComponent          a component no recipe uses yet (F-42), with a System line hinting at undiscovered recipes.
// LootBoxPlaceholder     a clearly named stand-in for a reward whose real item does not exist yet (listed in the notes).
//
// Account binding (no pinned mechanism binds to an account; pinned's Nontransferable binds to the backpack only): a
// bound item refuses a secure trade, a drop on the ground, a drop onto anyone, a drop into or onto anything not carried
// by a character of its account (so another player's pack, a house chest, a vendor), and a lift by anyone else.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Gumps;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Regions;

namespace Server.Items;

public static class AccountBinding
{
    public const string Refusal = "That is bound to its owner's account.";

    public static bool SameAccount(Mobile? m, string? account) =>
        m?.Account != null && account != null && m.Account.Username == account;

    /// <summary>Whether a bound item may rest where it would land: carried (at any depth) by a character of its account.</summary>
    public static bool MayRestIn(Item? target, string? account) =>
        target?.RootParent is Mobile holder && SameAccount(holder, account);

    public static bool Refuse(Mobile from)
    {
        from.SendMessage(0x22, Refusal);
        return false;
    }
}

[SerializationGenerator(0, false)]
public partial class LootBox : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private BoxTheme _theme;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _tier;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _boundAccount;

    [SerializableField(3)]
    private List<Item> _contents;

    [SerializableField(4)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _gold;

    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _renown;

    [SerializableField(6)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _boxName;

    [SerializableField(7)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _earnedFor;

    [SerializableField(8)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _openedAt;

    // Which of the contents came from the rare pool (by index in Contents), for the "rare" fanfare line.
    [SerializableField(9)]
    private List<int> _rareSlots;

    [Constructible]
    public LootBox() : this(BoxTheme.Explorer, 1, "", "staff")
    {
    }

    public LootBox(BoxTheme theme, int tier, string boundAccount, string earnedFor) : base(0x9AA)
    {
        _theme = theme;
        _tier = Math.Clamp(tier, 1, 6);
        _boundAccount = boundAccount;
        _earnedFor = earnedFor;
        _contents = new List<Item>();
        _rareSlots = new List<int>();
        var (name, hue) = LootBoxRoller.NameAndHue(theme, _tier, null);
        _boxName = name;
        Hue = hue;
        Weight = 2.0;
    }

    public override string DefaultName => $"{_boxName} Box";

    public bool IsRareSlot(int index) => _rareSlots.Contains(index);

    /// <summary>Fills the box at earn time. The items go to the internal map until opened.</summary>
    public void Fill(string boxName, int hue, int gold, int renown, List<(Item Item, bool Rare)> contents)
    {
        BoxName = boxName;
        Hue = hue;
        Gold = gold;
        Renown = renown;

        foreach (var (item, rare) in contents)
        {
            if (rare)
            {
                _rareSlots.Add(_contents.Count);
            }

            item.Internalize();
            _contents.Add(item);
        }

        this.MarkDirty();
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{LootBoxRoller.ThemeTitle(_theme)} box, tier {_tier}");
        list.Add($"For: {_earnedFor}");
        list.Add("Bound to your account");
        list.Add("Open in a guarded town or your house");
    }

    // -- Account binding --

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => AccountBinding.Refuse(from);

    public override bool OnDroppedToWorld(Mobile from, Point3D p) => AccountBinding.Refuse(from);

    public override bool OnDroppedToMobile(Mobile from, Mobile target) =>
        target == from && AccountBinding.SameAccount(from, _boundAccount) ? base.OnDroppedToMobile(from, target) : AccountBinding.Refuse(from);

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
        AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedInto(from, target, p) : AccountBinding.Refuse(from);

    public override bool OnDroppedOnto(Mobile from, Item target) =>
        AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedOnto(from, target) : AccountBinding.Refuse(from);

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
    {
        if (from.AccessLevel < AccessLevel.GameMaster && !AccountBinding.SameAccount(from, _boundAccount))
        {
            reject = LRReason.CannotLift;
            return false;
        }

        return base.CheckLift(from, item, ref reject);
    }

    // -- Opening --

    /// <summary>A guarded region whose guards are on, or a house where the player is owner, co-owner or friend.</summary>
    public static bool IsSafeToOpen(Mobile m)
    {
        if (m.Region?.GetRegion<GuardedRegion>() is { } guarded && !guarded.IsDisabled())
        {
            return true;
        }

        return BaseHouse.FindHouseAt(m) is { } house && house.IsFriend(m);
    }

    public const string UnsafeLine = "System: Not here. Open it in a guarded town, or in a house where you are an owner, co-owner or friend.";

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        if (!AccountBinding.SameAccount(pm, _boundAccount))
        {
            pm.SendMessage(0x22, AccountBinding.Refusal);
            return;
        }

        if (!IsChildOf(pm.Backpack))
        {
            pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        if (!IsSafeToOpen(pm))
        {
            pm.SendMessage(0x22, UnsafeLine);
            return;
        }

        Open(pm);
    }

    /// <summary>
    /// Hands everything over (pack, else bank), shows what it was, sends the flying loot (cc-P242), plays the fanfare,
    /// and deletes the box when empty.
    /// </summary>
    public List<string> Open(PlayerMobile pm)
    {
        var shown = new List<string>();
        var rare = false;
        var boxInBox = false;
        var handedOver = new List<Item>(); // cc-P242: what flies out of the box
        OpenedAt = Core.Now;

        if (_gold > 0)
        {
            ClusterFGuildPromotion.Deliver(pm, new Gold(_gold));
            shown.Add($"{_gold:N0} gold");
            Gold = 0;
        }

        if (_renown > 0 && pm.Account is Server.Accounting.IAccount acct)
        {
            ClusterFAccountPersistence.GetOrCreate(acct).GrantRenown(_renown, RenownSource.Achievement);
            shown.Add($"{_renown:N0} Renown");
            Renown = 0;
        }

        for (var i = 0; i < _contents.Count; i++)
        {
            var item = _contents[i];
            if (item == null || item.Deleted)
            {
                continue;
            }

            shown.Add(LootBoxRoller.Describe(item));
            rare |= IsRareSlot(i);
            boxInBox |= item is LootBox;
            handedOver.Add(item);
            ClusterFGuildPromotion.Deliver(pm, item);
        }

        _contents.Clear();
        _rareSlots.Clear();
        this.MarkDirty();

        pm.CloseGump<LootBoxRevealGump>();
        pm.SendGump(new LootBoxRevealGump(DefaultName, shown));
        pm.SendMessage(0x44, $"System: You open the {DefaultName}.");

        var level = _tier >= 4 ? FanfareLevel.Major : FanfareLevel.Minor;

        // cc-P242: the best item flies out of the box now, as it opens; the fanfare below follows when its window closes.
        ClusterFFlyingLoot.Play(pm, handedOver, level == FanfareLevel.Major);
        if (boxInBox)
        {
            ClusterFFanfare.Play(pm, level, FanfareLines.BoxInBox, tier: _tier, voiceKey: $"box:{_boxName}");
        }
        else if (rare)
        {
            ClusterFFanfare.Play(pm, level, FanfareLines.BoxRare, tier: _tier, voiceKey: $"box:{_boxName}");
        }
        else
        {
            ClusterFFanfare.Play(pm, level, "", tier: _tier, line: "", voiceKey: $"box:{_boxName}");
        }

        Delete();
        return shown;
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        foreach (var item in _contents)
        {
            item?.Delete();
        }
    }
}

/// <summary>The reveal: what was in the box. Background 9270, outside the gump art range Fiddle-Me-This replaces (3500 to 3508).</summary>
public class LootBoxRevealGump : Gump
{
    public readonly List<string> Shown;

    public LootBoxRevealGump(string boxName, List<string> shown) : base(140, 120)
    {
        Shown = shown;
        var h = 90 + shown.Count * 22;

        AddPage(0);
        AddBackground(0, 0, 380, h, 9270);
        AddAlphaRegion(10, 10, 360, h - 20);
        AddLabel(24, 18, 1154, $"Inside the {boxName}");

        for (var i = 0; i < shown.Count; i++)
        {
            AddLabel(30, 46 + i * 22, 0x44, shown[i]);
        }

        AddLabel(24, h - 34, 999, "Everything is in your pack (or your bank, if your pack was full).");
    }
}

[SerializationGenerator(0, false)]
public partial class BoundCosmeticSash : BodySash
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _boundAccount;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _label;

    [Constructible]
    public BoundCosmeticSash() : this("", "Box cosmetic", 0x0386)
    {
    }

    public BoundCosmeticSash(string boundAccount, string label, int hue)
    {
        _boundAccount = boundAccount;
        _label = label;
        Hue = hue;
        LootType = LootType.Blessed;
    }

    public override string DefaultName => $"a {_label} sash";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Cosmetic, bound to your account");
    }

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => AccountBinding.Refuse(from);

    public override bool OnDroppedToWorld(Mobile from, Point3D p) => AccountBinding.Refuse(from);

    public override bool OnDroppedToMobile(Mobile from, Mobile target) =>
        target == from && AccountBinding.SameAccount(from, _boundAccount) ? base.OnDroppedToMobile(from, target) : AccountBinding.Refuse(from);

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
        AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedInto(from, target, p) : AccountBinding.Refuse(from);

    public override bool OnDroppedOnto(Mobile from, Item target) =>
        AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedOnto(from, target) : AccountBinding.Refuse(from);

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
    {
        if (from.AccessLevel < AccessLevel.GameMaster && !AccountBinding.SameAccount(from, _boundAccount))
        {
            reject = LRReason.CannotLift;
            return false;
        }

        return base.CheckLift(from, item, ref reject);
    }
}

[SerializationGenerator(0, false)]
public partial class LoopScrollFragment : Item
{
    public const int FragmentsPerScroll = 5;

    [Constructible]
    public LoopScrollFragment(int amount = 1) : base(0x0EF3)
    {
        Stackable = true;
        Amount = amount;
        Hue = 0x08AF;
        Weight = 0.1;
    }

    public override string DefaultName => "a loop scroll fragment";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{FragmentsPerScroll} make one loop scroll");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (Amount < FragmentsPerScroll)
        {
            from.SendMessage($"You need {FragmentsPerScroll} fragments to make a loop scroll.");
            return;
        }

        ClusterFLootBoxes.CombineLoopScroll(from, this);
    }
}

[SerializationGenerator(0, false)]
public partial class EpicComponent : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _componentName;

    [Constructible]
    public EpicComponent() : this("Starforged Filament")
    {
    }

    public EpicComponent(string componentName) : base(0x1EA7)
    {
        _componentName = componentName;
        Hue = 0x0013;
        Weight = 1.0;
    }

    public override string DefaultName => _componentName;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Epic component");
        list.Add("System: Something, somewhere, calls for this. You have not found the recipe yet.");
    }
}

/// <summary>A stand-in for a reward whose real item does not exist yet. Its kind says what it stands for.</summary>
[SerializationGenerator(0, false)]
public partial class LootBoxPlaceholder : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _kind;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _boundAccount;

    [Constructible]
    public LootBoxPlaceholder() : this("Unnamed", "")
    {
    }

    public LootBoxPlaceholder(string kind, string boundAccount = "") : base(0x14F0)
    {
        _kind = kind;
        _boundAccount = boundAccount;
        Weight = 1.0;
    }

    public override string DefaultName => $"[Placeholder] {_kind}";

    public bool IsBound => !string.IsNullOrEmpty(_boundAccount);

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Stands in for a reward still to come; keep it");
        if (IsBound)
        {
            list.Add("Bound to your account");
        }
    }

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) =>
        !IsBound || AccountBinding.Refuse(from);

    public override bool OnDroppedToWorld(Mobile from, Point3D p) =>
        IsBound ? AccountBinding.Refuse(from) : base.OnDroppedToWorld(from, p);

    public override bool OnDroppedToMobile(Mobile from, Mobile target) =>
        !IsBound || target == from && AccountBinding.SameAccount(from, _boundAccount)
            ? base.OnDroppedToMobile(from, target)
            : AccountBinding.Refuse(from);

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
        !IsBound || AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedInto(from, target, p) : AccountBinding.Refuse(from);
}
