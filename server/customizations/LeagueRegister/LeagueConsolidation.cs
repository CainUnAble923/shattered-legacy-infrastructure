// cc-P280 (Chase, answer 5): the first consolidation items, P179's C1 to C3.
//   C1 the Guild Ledger: a page of the Register (LeagueRegisterGump.Page.Ledger), free.
//   C2 the League Field Satchel: Bronze, 5,000 Renown (and a little gold, answer 8). One satchel that holds the guild
//      satchels: the ore, lumber, hunter's and essence satchels, one of each family. Each keeps its own code inside it
//      (its filters, weight reduction and caps), because the code that fills them finds them anywhere in the backpack
//      (FindItemByType searches nested containers). Putting one in costs the merge fee in that satchel's guild currency
//      (answer 8: guild currencies pay for guild-specific parts); taking one out is free. Whatever is inside counts as
//      ONE item toward the backpack's limit, which is the point: the guild satchels (and what they hold) stop eating the
//      pack's 125.
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

    public static bool BuySatchel(PlayerMobile pm, out string message)
    {
        message = RefuseSatchel(pm);
        if (message != null || !LeaguePrice.Pay(pm, LeagueRegisterSettings.SatchelRenown, LeagueRegisterSettings.SatchelGold))
        {
            message ??= "You cannot pay for that.";
            return false;
        }

        pm.AddToBackpack(new LeagueFieldSatchel());
        message = "A League Field Satchel is in your pack. Drop your guild satchels into it.";
        return true;
    }

    public static bool BuyLogbook(PlayerMobile pm, out string message)
    {
        message = RefuseLogbook(pm);
        if (message != null || !LeaguePrice.Pay(pm, LeagueRegisterSettings.LogbookRenown, LeagueRegisterSettings.LogbookGold))
        {
            message ??= "You cannot pay for that.";
            return false;
        }

        var olds = new System.Collections.Generic.List<Item>();
        foreach (var old in pm.Backpack.FindItemsByType<Item>(true, i => i is ProspectorsLogbook or ForestersLogbook))
        {
            olds.Add(old);
        }

        olds.ForEach(old => old.Delete());

        pm.AddToBackpack(new CombinedLogbook());
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

    public override int DefaultMaxItems => 4;

    // Whatever is inside counts as one item toward the pack: the satchel itself. Its children's item counts stop here.
    public override int GetTotal(TotalType type) => type == TotalType.Items ? 0 : base.GetTotal(type);

    public override void UpdateTotal(Item sender, TotalType type, int delta)
    {
        if (type != TotalType.Items)
        {
            base.UpdateTotal(sender, type, delta);
        }
    }

    public override bool CheckHold(Mobile m, Item item, bool message, bool checkItems, int plusItems, int plusWeight)
    {
        if (LeagueConsolidation.GuildFor(item) is not { } key)
        {
            if (message)
            {
                m.SendMessage(0x22, "Only the guilds' ore, lumber, hunter's and essence satchels go in here.");
            }

            return false;
        }

        foreach (var held in Items)
        {
            if (held != item && LeagueConsolidation.GuildFor(held) == key)
            {
                if (message)
                {
                    m.SendMessage(0x22, "It already holds a satchel of that kind.");
                }

                return false;
            }
        }

        return base.CheckHold(m, item, message, checkItems, plusItems, plusWeight);
    }

    public override bool OnDragDropInto(Mobile from, Item item, Point3D p) => Merge(from, item) && base.OnDragDropInto(from, item, p);

    public override bool OnDragDrop(Mobile from, Item dropped) => Merge(from, dropped) && base.OnDragDrop(from, dropped);

    /// <summary>The merge fee, in the satchel's guild currency, before it goes in (answer 8). False refuses the drop.</summary>
    public bool Merge(Mobile from, Item satchel)
    {
        if (from.AccessLevel > AccessLevel.Player || LeagueConsolidation.GuildFor(satchel) is not { } key)
        {
            return true;
        }

        var fee = LeagueRegisterSettings.SatchelMergeFee;
        var guild = from.Account is IAccount ? ClusterFAccountPersistence.GetOrCreateGuild(from) : null;
        if (guild == null || !guild.SpendCurrency(key, fee))
        {
            from.SendMessage(0x22, $"Merging that satchel costs {fee} of the {ClusterFGuildSystem.GetDef(key)?.Name ?? key}'s currency.");
            return false;
        }

        from.SendMessage(0x44, $"The satchel is merged ({fee} paid to the {ClusterFGuildSystem.GetDef(key)?.Name ?? key}).");
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
