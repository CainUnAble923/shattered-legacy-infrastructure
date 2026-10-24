// cc-P280 (Chase, answer 5): the first consolidation items, P179's C1 to C3.
//   C1 the Guild Ledger: a page of the Register (LeagueRegisterGump.Page.Ledger), free.
//   C2 the League Field Satchel: Bronze, 5,000 Renown (and a little gold, answer 8). One satchel that holds the guild
//      satchels: the ore, lumber, hunter's and essence satchels, one of each family. Each keeps its own code inside it
//      (its filters, weight reduction and caps), because the code that fills them finds them anywhere in the backpack
//      (FindItemByType searches nested containers). Putting one in costs the merge fee in that satchel's guild currency
//      (answer 8: guild currencies pay for guild-specific parts), charged once it is in; taking one out, or moving one
//      inside it, is free. While it is directly in its owner's backpack, whatever is inside counts as ONE item toward
//      the backpack's limit, which is the point: the guild satchels (and what they hold) stop eating the pack's 125.
//      Anywhere else (the bank box, a house's secure storage, a bag) its contents count in full (cc-P451, P418 #6).
//   C3 the Combined Logbook: Dull Copper, 500 Renown. One book for the Prospector's and the Forester's logbooks; it opens
//      either, for a member of its guild, and buying it takes both old logbooks from the pack (they keep no data of their
//      own: both read the account's records).

using System;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.LeagueRegister;

public static class LeagueConsolidation
{
    public const string MakeRoom = "Make room in your pack first.";

    /// <summary>The satchel families the Field Satchel takes, and the guild whose currency pays the merge fee.</summary>
    public static string GuildFor(Item item) => item switch
    {
        CompactOreSatchel      => "mining",
        ForestersLumberSatchel => "foresters",
        HuntersSatchel         => "rangers",
        ArtificersSatchel      => "artificers",
        _                      => null
    };

    public static string RefuseSatchel(PlayerMobile pm) =>
        ClusterFLeagueRanks.GetRank(pm) < LeagueRegisterSettings.SatchelRank
            ? $"The League Field Satchel needs {ClusterFLeagueRanks.RankName(LeagueRegisterSettings.SatchelRank)}."
            : LeaguePrice.Refuse(pm, LeagueRegisterSettings.SatchelRenown, LeagueRegisterSettings.SatchelGold);

    public static string RefuseLogbook(PlayerMobile pm) =>
        ClusterFLeagueRanks.GetRank(pm) < LeagueRegisterSettings.LogbookRank
            ? $"The Combined Logbook needs {ClusterFLeagueRanks.RankName(LeagueRegisterSettings.LogbookRank)}."
            : LeaguePrice.Refuse(pm, LeagueRegisterSettings.LogbookRenown, LeagueRegisterSettings.LogbookGold);

    // cc-P451 (P418 #5): what is bought goes into the pack first; with no room it is refused before anything is taken.
    private static bool Deliver(PlayerMobile pm, Item item, int renown, int gold, out string message)
    {
        if (!pm.PlaceInBackpack(item))
        {
            item.Delete();
            message = MakeRoom;
            return false;
        }

        if (!LeaguePrice.Pay(pm, renown, gold))
        {
            item.Delete();
            message = "You cannot pay for that.";
            return false;
        }

        message = null;
        return true;
    }

    public static bool BuySatchel(PlayerMobile pm, out string message)
    {
        message = RefuseSatchel(pm);
        if (message != null ||
            !Deliver(pm, new LeagueFieldSatchel(), LeagueRegisterSettings.SatchelRenown, LeagueRegisterSettings.SatchelGold, out message))
        {
            return false;
        }

        message = "A League Field Satchel is in your pack. Drop your guild satchels into it.";
        return true;
    }

    public static bool BuyLogbook(PlayerMobile pm, out string message)
    {
        message = RefuseLogbook(pm);
        if (message != null ||
            !Deliver(pm, new CombinedLogbook(), LeagueRegisterSettings.LogbookRenown, LeagueRegisterSettings.LogbookGold, out message))
        {
            return false;
        }

        var olds = new System.Collections.Generic.List<Item>();
        foreach (var old in pm.Backpack.FindItemsByType<Item>(true, i => i is ProspectorsLogbook or ForestersLogbook))
        {
            olds.Add(old);
        }

        olds.ForEach(old => old.Delete());
        message = "A Combined Logbook is in your pack; your old logbooks are in it now.";
        return true;
    }
}

[SerializationGenerator(0, false)]
public partial class LeagueFieldSatchel : Container
{
    [Constructible]
    public LeagueFieldSatchel() : base(0xA272)
    {
        Name = "a League Field Satchel";
        Hue = 0x8A5;
        LootType = LootType.Blessed;
        Weight = 2.0;
    }

    // One satchel of each family (cc-P451, P418 #3: counted here as satchels, Items.Count, not by pinned's item total).
    public override int DefaultMaxItems => 4;

    /// <summary>Directly in a player's own backpack: the one place its contents count as nothing (P418 #6).</summary>
    public bool InOwnersPack => Parent is Container pack && pack.Parent is PlayerMobile pm && pm.Backpack == pack;

    // In the owner's pack, whatever is inside counts as one item toward the pack: the satchel itself, and its children's
    // item counts stop here. Anywhere else they count in full, as any container's do. Parent is set before an add's
    // totals and cleared after a removal's (pinned Item.AddItem, RemoveItem), so both see the same place.
    public override int GetTotal(TotalType type)
    {
        if (type != TotalType.Items)
        {
            return base.GetTotal(type);
        }

        if (InOwnersPack)
        {
            return 0;
        }

        var total = 0;
        foreach (var item in Items)
        {
            if (!item.IsVirtualItem)
            {
                total += item.TotalItems + 1;
            }
        }

        return total;
    }

    public override void UpdateTotal(Item sender, TotalType type, int delta)
    {
        if (type != TotalType.Items || !InOwnersPack)
        {
            base.UpdateTotal(sender, type, delta);
        }
    }

    /// <summary>
    /// A guild satchel going in: one of each family, four in all. Anything else asking is on its way into one of the
    /// guild satchels inside (pinned Container.CheckHold walks up the parents with the item being inserted), so only the
    /// containers above are asked. A non-satchel dropped on this one directly is refused at the drop (Insert). This
    /// satchel has no weight limit of its own: the guild satchels' caps and the containers above bound it.
    /// </summary>
    public override bool CheckHold(Mobile m, Item item, bool message, bool checkItems, int plusItems, int plusWeight)
    {
        if (LeagueConsolidation.GuildFor(item) is { } key && RefuseSatchel(item, key) is { } why)
        {
            if (message)
            {
                m?.SendMessage(0x22, why);
            }

            return false;
        }

        // In the owner's pack, what goes in here adds no items there (the weight still counts).
        if (InOwnersPack && !item.IsVirtualItem)
        {
            plusItems -= item.TotalItems + 1;
        }

        for (var parent = Parent; parent is Item above; parent = above.Parent)
        {
            if (above is Container container)
            {
                return container.CheckHold(m, item, message, checkItems, plusItems, plusWeight);
            }
        }

        return true;
    }

    private string RefuseSatchel(Item satchel, string key)
    {
        var held = 0;
        foreach (var item in Items)
        {
            if (item == satchel)
            {
                continue;
            }

            if (LeagueConsolidation.GuildFor(item) == key)
            {
                return "It already holds a satchel of that kind.";
            }

            held++;
        }

        return MaxItems > 0 && held >= MaxItems ? $"It holds {MaxItems} satchels, one of each kind." : null;
    }

    public override bool OnDragDropInto(Mobile from, Item item, Point3D p) =>
        Insert(from, item, () => base.OnDragDropInto(from, item, p));

    // OnDragDrop and Mobile.PlaceInBackpack come here.
    public override bool TryDropItem(Mobile from, Item dropped, bool sendFullMessage, bool playSound) =>
        Insert(from, dropped, () => base.TryDropItem(from, dropped, sendFullMessage, playSound));

    /// <summary>
    /// A direct insertion: guild satchels only, and the merge fee in that satchel's guild currency (answer 8), checked
    /// before and charged only once the satchel is in (cc-P451, P418 #4). Moving one around inside costs nothing:
    /// Mobile.Lift records where a lifted item came from.
    /// </summary>
    private bool Insert(Mobile from, Item item, Func<bool> insert)
    {
        if (LeagueConsolidation.GuildFor(item) is not { } key)
        {
            from?.SendMessage(0x22, "Only the guilds' ore, lumber, hunter's and essence satchels go in here.");
            return false;
        }

        var moving = item.Parent == this || item.Parent == null && item.GetBounce()?.Parent == this;
        var fee = from == null || from.AccessLevel > AccessLevel.Player || moving ? 0 : LeagueRegisterSettings.SatchelMergeFee;
        var guild = fee > 0 && from.Account is IAccount ? ClusterFAccountPersistence.GetOrCreateGuild(from) : null;
        var name = ClusterFGuildSystem.GetDef(key)?.Name ?? key;
        if (fee > 0 && (guild == null || guild.GetCurrency(key) < fee))
        {
            from.SendMessage(0x22, $"Merging that satchel costs {fee} of the {name}'s currency.");
            return false;
        }

        if (!insert())
        {
            return false;
        }

        if (fee > 0)
        {
            guild.SpendCurrency(key, fee);
            from.SendMessage(0x44, $"The satchel is merged ({fee} paid to the {name}).");
        }

        return true;
    }
}

[SerializationGenerator(0, false)]
public partial class CombinedLogbook : Item
{
    [Constructible]
    public CombinedLogbook() : base(0x1C11)
    {
        Name = "a Combined Logbook";
        Hue = 0x8A5;
        LootType = LootType.Blessed;
        Weight = 1.0;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm || !IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        pm.CloseGump<CombinedLogbookGump>();
        pm.SendGump(new CombinedLogbookGump(pm));
    }
}

public class CombinedLogbookGump : Gump
{
    private const int BtnProspector = 1;
    private const int BtnForester = 2;

    private readonly PlayerMobile _pm;

    public CombinedLogbookGump(PlayerMobile pm) : base(120, 100)
    {
        _pm = pm;
        AddBackground(0, 0, 300, 130, 9270);
        RequirementStyle.AddSolidPanel(this, 300, 130);
        AddLabel(20, 14, RequirementStyle.HeadingHue, "Combined Logbook");
        Row(44, BtnProspector, "Prospector's Logbook", "mining");
        Row(74, BtnForester, "Forester's Logbook", "foresters");
    }

    private void Row(int y, int button, string label, string guild)
    {
        if (ClusterFGuildSystem.IsJoined(_pm, guild))
        {
            AddButton(20, y, 4005, 4007, button);
            AddLabel(56, y + 2, 999, label);
        }
        else
        {
            AddLabel(56, y + 2, 0x3B2, $"{label} (not a member)");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_pm.Account is not IAccount acct)
        {
            return;
        }

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        if (info.ButtonID == BtnProspector && ClusterFGuildSystem.IsJoined(_pm, "mining"))
        {
            _pm.SendGump(new ProspectorsLogbookGump(_pm, data));
        }
        else if (info.ButtonID == BtnForester && ClusterFGuildSystem.IsJoined(_pm, "foresters"))
        {
            _pm.SendGump(new ForestersLogbookGump(_pm, data));
        }
    }
}
