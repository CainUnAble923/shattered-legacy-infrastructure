// cc-P168 (Chase, 2026-10-18 13:27: "I also need BODs to show what they would give like points but for guild members").
// What handing a deed in pays a member of the deed's guild, for the deed gump (BulkOrderDeedGumpLines) and its tooltip
// (SmallBOD/LargeBOD.GetProperties, BOD-properties-guild-pay.patch).
//
// The deed's quote. The gold behind every deed payout is a roll, +-10% around the table (pinned Rewards.cs:474-500,
// ServUO's CraftRewardCalculators.Vary), and the Society's Seals are worked out from that gold. So a number shown and a
// number paid could not be equal if each rolled for itself. Each deed therefore gets one quote, rolled once by the same
// calls the turn-in made before (below), kept for the deed in memory, and read by both the display and the turn-in
// (ClusterFSmithBODPayout.Pay and its choice gump, BulkOrderVendors.TurnIn): what is shown is what is paid. A quote is
// rolled again when the deed's terms change (a bribe raises its amount or metal) and after a restart (not saved), and
// either way the display and the payout read the same one. The calls:
//   - Society of Smiths (smith deeds): BlacksmithGuildmaster.ComputeGuildReward (the Seals and standing
//     ClusterFSmithBODPayout.Pay gives) and ClusterFSmithBODPayout.CashOutGold (what its CashOut pays), by the member's
//     Completed orders setting: bank shows the Seals, cash out the gold, ask both.
//   - The other deed-giving guilds (Tailors' Circle, Foresters' Union, Tinkers' Union, Maritime Brotherhood, Order of the
//     Alembic, Arcane Society): their guild masters take deeds on the stock 2016 rules (BulkOrderVendors.TurnIn), which pay
//     gold to the bank (BulkOrderSystem.ComputeGold) and turn-in points and no guild currency or standing (their own
//     payouts are F-18, F-20, F-25). A member sees that gold beside the points line.
// A non-member, and a member of some other guild only, sees what a non-member sees: the 2016 points line in the gump, and
// nothing new in the tooltip. The viewer is the player whose backpack holds the deed (the gump's own rule, Viewer).

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server.Accounting;
using Server.Mobiles;

namespace Server.Engines.BulkOrders;

public static class BulkOrderGuildPay
{
    public const string SmithingKey = "smithing";

    /// <summary>
    /// One deed's payout, each part rolled once, when first read (see the file's header). Each part is rolled only when
    /// asked for, so a turn-in that never shows anything rolls exactly what it rolled before (a stock 2016 turn-in reads
    /// StockGold alone).
    /// </summary>
    public sealed class DeedQuote
    {
        private readonly BaseBOD _deed;
        private (int seals, int standing, int checks)? _society;
        private int? _cashOutGold;
        private int? _stockGold;

        public DeedQuote(BaseBOD deed)
        {
            _deed = deed;
            (Material, AmountMax, Exceptional) = Terms(deed);
        }

        public BulkMaterialType Material { get; }
        public int AmountMax { get; }
        public bool Exceptional { get; }

        // Society: Seals (bank), standing and skill checks (either way), by BlacksmithGuildmaster.ComputeGuildReward.
        private (int seals, int standing, int checks) Society =>
            _society ??= _deed is SmallSmithBOD or LargeSmithBOD ? BlacksmithGuildmaster.ComputeGuildReward(_deed) : (0, 0, 0);

        public int Seals => Society.seals;
        public int Standing => Society.standing;
        public int SkillChecks => Society.checks;

        /// <summary>Society, cash out: ClusterFSmithBODPayout.CashOutGold.</summary>
        public int CashOutGold => _cashOutGold ??= ClusterFSmithBODPayout.CashOutGold(_deed);

        /// <summary>The 2016 turn-in's bank gold: BulkOrderSystem.ComputeGold.</summary>
        public int StockGold => _stockGold ??= BulkOrderSystem.ComputeGold(_deed);
    }

    private static readonly ConditionalWeakTable<BaseBOD, DeedQuote> _quotes = new();

    private static (BulkMaterialType material, int amountMax, bool exceptional) Terms(BaseBOD deed) => deed switch
    {
        SmallBOD s => (s.Material, s.AmountMax, s.RequireExceptional),
        LargeBOD l => (l.Material, l.AmountMax, l.RequireExceptional),
        _          => (BulkMaterialType.None, 0, false)
    };

    /// <summary>The deed's quote: the one already made while its terms stand, else a new one.</summary>
    public static DeedQuote Quote(BaseBOD deed)
    {
        if (_quotes.TryGetValue(deed, out var quote) && (quote.Material, quote.AmountMax, quote.Exceptional) == Terms(deed))
        {
            return quote;
        }

        quote = new DeedQuote(deed);
        _quotes.AddOrUpdate(deed, quote);
        return quote;
    }

    /// <summary>The guild whose members turn this deed in at their guild master, by the deed's craft.</summary>
    public static string GuildKeyFor(BaseBOD deed) => BulkOrderSystem.GetBODType(deed) switch
    {
        BODType.Smith                          => SmithingKey,
        BODType.Tailor                         => TailorGuildmaster.GuildKey,
        BODType.Carpentry or BODType.Fletching => BulkOrderGivers.ForestersKey,
        BODType.Tinkering                      => TinkerGuildmaster.GuildKey,
        BODType.Cooking                        => FisherGuildmaster.GuildKey,
        BODType.Alchemy                        => ClusterFAlembicGuild.Key,
        BODType.Inscription                    => MageGuildmaster.GuildKey,
        _                                      => null
    };

    public static PlayerMobile Holder(BaseBOD deed) => deed.RootParent as PlayerMobile;

    public static bool IsMember(PlayerMobile pm, BaseBOD deed) =>
        pm?.Account is IAccount && GuildKeyFor(deed) is { } key && ClusterFGuildSystem.IsJoined(pm, key);

    public static bool IsSmithMember(PlayerMobile pm, BaseBOD deed) =>
        GuildKeyFor(deed) == SmithingKey && IsMember(pm, deed);

    /// <summary>The Society's lines: what the turn-in pays, then the setting it pays by.</summary>
    public static (string pay, string setting) SmithLines(PlayerMobile pm, BaseBOD deed)
    {
        var quote = Quote(deed);
        var (seals, standing, gold) = (quote.Seals, quote.Standing, quote.CashOutGold);
        var mode = ClusterFSmithBODPayout.GetMode(pm);

        var pay = mode switch
        {
            SmithTurnInMode.CashOut => $"Society pays you: +{standing:N0} standing, {gold:N0} gold (cash out)",
            SmithTurnInMode.Ask     => $"Society pays you: +{standing:N0} standing, {seals:N0} Seals or {gold:N0} gold",
            _                       => $"Society pays you: +{standing:N0} standing, {seals:N0} Smithing Seals"
        };

        var setting = $"{ClusterFSmithBODPayout.ToggleLabel} {ClusterFSmithBODPayout.ModeLabel(mode)}";
        if (deed is LargeSmithBOD && !BlacksmithGuildmaster.CanTurnInLarge(pm))
        {
            setting += " (large orders need more Blacksmithy)";
        }

        return (pay, setting);
    }

    /// <summary>Another deed-giving guild's line: the stock turn-in's bank gold.</summary>
    public static string OtherGuildLine(BaseBOD deed)
    {
        var name = ClusterFGuildSystem.GetDef(GuildKeyFor(deed))?.Name ?? "Your guild";
        return $"{name} pays you: {Quote(deed).StockGold:N0} gold to the bank (no guild currency or standing yet)";
    }

    /// <summary>The tooltip lines for the holder: none for a non-member.</summary>
    public static List<string> PropertyLines(BaseBOD deed)
    {
        var lines = new List<string>();
        var pm = Holder(deed);

        if (!IsMember(pm, deed) || !BulkOrderSystem.NewSystemEnabled)
        {
            return lines;
        }

        if (IsSmithMember(pm, deed))
        {
            var (pay, setting) = SmithLines(pm, deed);
            lines.Add(pay);
            lines.Add(setting);
        }
        else
        {
            lines.Add(OtherGuildLine(deed));
        }

        return lines;
    }

    /// <summary>From SmallBOD/LargeBOD.GetProperties (BOD-properties-guild-pay.patch).</summary>
    public static void AddProperties(BaseBOD deed, IPropertyList list)
    {
        foreach (var line in PropertyLines(deed))
        {
            list.Add(line);
        }
    }

    /// <summary>The holder's deeds in the backpack (and its guild books) show new numbers after their setting changes.</summary>
    public static void Refresh(Mobile m)
    {
        if (m.Backpack == null)
        {
            return;
        }

        foreach (var deed in m.Backpack.FindItemsByType<BaseBOD>())
        {
            deed.InvalidateProperties();
        }
    }
}
