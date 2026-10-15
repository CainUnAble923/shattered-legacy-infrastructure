// cc-P123: the two guild masters who give and take bulk orders but are not shopkeepers: the Foresters' Union's (carpentry
// and fletching) and the Order of the Alembic's (alchemy), for their members, on the stock 2016 rules (Chase's answer 2 to
// P118, P118's giver table). Their own guild payouts are F-18, F-20 and F-25 work, not here.
//
// The shopkeeper guild masters (Tailors' Circle, Tinkers' Union, Arcane Society, Maritime Brotherhood) get the stock vendor
// path (BulkOrderVendors.cs). These two are not BaseVendors, so they carry the same three things themselves: "Bulk Order
// Info" (3006152, the stock entry's number) for an offer, "Claim Rewards" (1155593), and the turn-in when a deed is dropped
// on them, each through BulkOrderVendors' shared Offer, ClaimRewards and TurnIn. The Foresters' guild master gives two
// crafts: a member with both skills picks the craft in a small gump (the two crafts' names); with one, it is that craft.
// No bribes here: ServUO's bribe is a shopkeeper's, and its state is kept per vendor.

using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.BulkOrders;
using Server.Gumps;
using Server.Network;
using Server.Systems.FeatureFlags;

namespace Server.Mobiles;

public static class BulkOrderGivers
{
    public const string ForestersKey = "foresters";

    /// <summary>A member of the guild with some of the craft's skill (the stock vendors' rule: skill above 0).</summary>
    public static bool IsMemberWithSkill(Mobile from, string guildKey, SkillName skill) =>
        BulkOrderSystem.NewSystemEnabled && from is PlayerMobile { Account: IAccount } pm &&
        ClusterFGuildSystem.IsJoined(pm, guildKey) && pm.Skills[skill].Base > 0;

    /// <summary>The crafts a non-shopkeeper guild master gives, and its guild.</summary>
    public static (BODType[] Crafts, string GuildKey) GiverOf(Mobile giver) =>
        giver switch
        {
            ForestersGuildmaster => ([BODType.Carpentry, BODType.Fletching], ForestersKey),
            AlembicGuildmaster   => ([BODType.Alchemy], ClusterFAlembicGuild.Key),
            _                    => ([], null)
        };

    /// <summary>The crafts this player may be given and may turn in here.</summary>
    public static List<BODType> CraftsFor(Mobile giver, Mobile from)
    {
        var (crafts, key) = GiverOf(giver);
        var result = new List<BODType>();

        foreach (var type in crafts)
        {
            if (IsMemberWithSkill(from, key, BulkOrderSystem.GetSkillForBOD(type)))
            {
                result.Add(type);
            }
        }

        return result;
    }

    /// <summary>
    /// The shopkeeper guild masters' Bulk Order Info, Bribe and Claim Rewards, for a member with the skill. Pinned's
    /// BaseVendor adds them only when IsActiveVendor, which a guild master is not (the shard's BaseGuildmaster: no shop);
    /// ServUO's smith guildmaster turns its shop on instead. Not the Society of Smiths' (no 2016 craft: its own path).
    /// </summary>
    public static void AddGuildmasterEntries(BaseVendor master, Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        if (ContentFeatureFlags.BulkOrders && from.Alive && !master.IsActiveVendor &&
            BulkOrderVendors.IsNewSystemVendor(master) && master.SupportsBulkOrders(from))
        {
            list.Add(new GuildmasterBulkOrderInfoEntry());
            BulkOrderVendors.AddContextEntries(master, from, ref list);
        }
    }

    public class GuildmasterBulkOrderInfoEntry : ContextMenuEntry
    {
        public GuildmasterBulkOrderInfoEntry() : base(BulkOrderInfoEntry.Cliloc, 10)
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (ContentFeatureFlags.BulkOrders && target is BaseVendor master && master.SupportsBulkOrders(from))
            {
                BulkOrderVendors.TryOffer(master, from);
            }
        }
    }

    public static void AddContextEntries(Mobile giver, Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        if (ContentFeatureFlags.BulkOrders && from is PlayerMobile && CraftsFor(giver, from).Count > 0)
        {
            list.Add(new BulkOrderInfoEntry());
            list.Add(new ClaimRewardsEntry());
        }
    }

    /// <summary>Picks the craft (asking when there are two), then offers or claims.</summary>
    public static void Choose(Mobile giver, PlayerMobile pm, bool claim)
    {
        var crafts = CraftsFor(giver, pm);

        if (crafts.Count == 1)
        {
            Act(giver, pm, crafts[0], claim);
        }
        else if (crafts.Count > 1)
        {
            pm.CloseGump<BulkOrderCraftGump>();
            pm.SendGump(new BulkOrderCraftGump(giver, crafts, claim));
        }
    }

    public static void Act(Mobile giver, PlayerMobile pm, BODType type, bool claim)
    {
        if (giver.Deleted || !pm.InRange(giver.Location, 10) || !CraftsFor(giver, pm).Contains(type))
        {
            return;
        }

        if (claim)
        {
            BulkOrderVendors.ClaimRewards(giver, pm, type);
        }
        else
        {
            BulkOrderVendors.Offer(giver, pm, type);
        }
    }

    /// <summary>
    /// A deed dropped on the guild master. False (not mine) for anything but a deed from a player, so the creature's own
    /// drop handling runs; otherwise accepted says whether the deed was taken (else it goes back to the pack).
    /// </summary>
    public static bool TryTurnIn(Mobile giver, Mobile from, Item dropped, out bool accepted)
    {
        accepted = false;

        if (!BulkOrderSystem.NewSystemEnabled || dropped is not BaseBOD bod || from is not PlayerMobile pm ||
            GiverOf(giver).Crafts.Length == 0)
        {
            return false;
        }

        var type = BulkOrderSystem.GetBODType(bod);
        var valid = type != null && CraftsFor(giver, pm).Contains(type.Value);

        accepted = BulkOrderVendors.TurnIn(giver, pm, bod, type ?? GiverOf(giver).Crafts[0], valid);
        return true;
    }

    public class BulkOrderInfoEntry : ContextMenuEntry
    {
        public const int Cliloc = 3006152; // Bulk Order Info

        public BulkOrderInfoEntry() : base(Cliloc, 10)
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is Mobile giver && from is PlayerMobile pm && ContentFeatureFlags.BulkOrders)
            {
                Choose(giver, pm, false);
            }
        }
    }

    public class ClaimRewardsEntry : ContextMenuEntry
    {
        public const int Cliloc = 1155593; // Claim Rewards

        public ClaimRewardsEntry() : base(Cliloc, 3)
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is Mobile giver && from is PlayerMobile pm)
            {
                Choose(giver, pm, true);
            }
        }
    }
}

/// <summary>The Foresters' guild master's question to a member with both skills: carpentry or fletching.</summary>
public class BulkOrderCraftGump : Gump
{
    private readonly Mobile _giver;
    private readonly List<BODType> _crafts;
    private readonly bool _claim;

    public BulkOrderCraftGump(Mobile giver, List<BODType> crafts, bool claim) : base(100, 100)
    {
        _giver = giver;
        _crafts = crafts;
        _claim = claim;

        AddPage(0);
        AddBackground(0, 0, 240, 60 + crafts.Count * 30, 5054);
        AddHtmlLocalized(20, 15, 200, 20, claim ? ClaimRewardsCliloc : BulkOrderInfoCliloc, 0x7FFF);

        for (var i = 0; i < crafts.Count; i++)
        {
            AddButton(20, 45 + i * 30, 4005, 4007, 1 + i);
            AddHtmlLocalized(55, 45 + i * 30, 160, 20, CraftName(crafts[i]), 0x7FFF);
        }
    }

    private const int BulkOrderInfoCliloc = 3006152; // Bulk Order Info
    private const int ClaimRewardsCliloc = 1155593;  // Claim Rewards

    /// <summary>The craft's skill name, as ServUO's filter gump names it.</summary>
    public static int CraftName(BODType type) =>
        type switch
        {
            BODType.Carpentry   => 1044071,
            BODType.Fletching   => 1044068,
            BODType.Tinkering   => 1044097,
            BODType.Alchemy     => 1044060,
            BODType.Inscription => 1044083,
            BODType.Cooking     => 1044073,
            BODType.Tailor      => 1044094,
            _                   => 1044067
        };

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var index = info.ButtonID - 1;

        if (sender.Mobile is PlayerMobile pm && index >= 0 && index < _crafts.Count)
        {
            BulkOrderGivers.Act(_giver, pm, _crafts[index], _claim);
        }
    }
}

/// <summary>The Order of the Alembic's guild master: alchemy deeds for members (BulkOrderGivers).</summary>
public partial class AlembicGuildmaster
{
    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);
        BulkOrderGivers.AddContextEntries(this, from, ref list);
    }

    public override bool OnDragDrop(Mobile from, Item dropped) =>
        BulkOrderGivers.TryTurnIn(this, from, dropped, out var accepted) ? accepted : base.OnDragDrop(from, dropped);
}

/// <summary>
/// The Foresters' Union's guild master: carpentry and fletching deeds for members (BulkOrderGivers). Its context menu is
/// in ForestersGuildmaster.cs, which calls BulkOrderGivers.AddContextEntries.
/// </summary>
public partial class ForestersGuildmaster
{
    public override bool OnDragDrop(Mobile from, Item dropped) =>
        BulkOrderGivers.TryTurnIn(this, from, dropped, out var accepted) ? accepted : base.OnDragDrop(from, dropped);
}
