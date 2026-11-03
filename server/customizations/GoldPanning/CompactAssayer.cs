// cc-P781: the Compact assayer, the Miners' Compact's provisioner for gold panning. A shopkeeper (pinned BaseVendor):
// he sells the prospector's pan and buys gold nuggets at gold-panning.json's prices. A Compact member may instead hand
// him nuggets (drop them on him) to be entered in the Compact's ledger as standing, the size's standing each. Notes in
// shard-migration notes/cc-P781-gold-panning.md.
//
// Placed by ClusterFAssayerSeeder on every world load, beside each Miners' Compact Liaison standing in the world (the
// New Haven mine camp and the Minoc hall today), when no assayer stands within assayer.seedRange of it. Idempotent: a
// second load, or [ClusterFSeedAssayers, places nothing more. No coordinates of his own, so he follows the liaison.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Commands;
using Server.Engines.GoldPanning;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class CompactAssayer : BaseVendor
{
    public const string GuildKey = "mining";

    private readonly List<SBInfo> _sbInfos = new();

    // Fixed names: NameList is not loaded in the test host.
    private static readonly string[] Names = ["Bertram Silt", "Agnes Hollowell", "Dunstan Pike", "Morwen Cask", "Ewart Brindle"];

    [Constructible]
    public CompactAssayer() : base("the Compact assayer")
    {
        Name = Names[Utility.Random(Names.Length)];
    }

    protected override List<SBInfo> SBInfos => _sbInfos;

    public override void InitSBInfo()
    {
        _sbInfos.Add(new SBCompactAssayer());
    }

    public override void InitOutfit()
    {
        AddItem(new Shirt(0x8A4));
        AddItem(new LongPants(0x455));
        AddItem(new Boots(0x455));
        AddItem(new HalfApron(0x973));
        AddItem(new WideBrimHat(0x8A4));
    }

    /// <summary>A Compact member's nuggets, entered as standing. Returns the standing given (0 when refused).</summary>
    public static int EnterInLedger(PlayerMobile pm, GoldNugget nugget, out string reply)
    {
        var row = GoldPanningTable.Current.Size(nugget.SizeKey);
        if (!GoldPanningTable.Current.Enabled || row == null || row.Standing <= 0)
        {
            reply = "I am not taking nuggets for the ledger just now. I will buy them for coin, though.";
            return 0;
        }

        if (!ClusterFGuildSystem.IsJoined(pm, GuildKey))
        {
            reply = "Only the Compact's own have a line in my ledger. I will buy those for coin, if you like.";
            return 0;
        }

        var standing = row.Standing * nugget.Amount;
        ClusterFAccountPersistence.GetOrCreateGuild(pm).AddReputation(GuildKey, standing);
        reply = $"{nugget.Amount} {nugget.SizeKey} {(nugget.Amount == 1 ? "nugget" : "nuggets")}, weighed and entered: {standing} standing with the Compact.";
        nugget.Delete();
        return standing;
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (dropped is GoldNugget nugget && from is PlayerMobile pm)
        {
            var given = EnterInLedger(pm, nugget, out var reply);
            SayTo(from, reply);
            return given > 0;
        }

        return base.OnDragDrop(from, dropped);
    }
}

/// <summary>The assayer's shop: the pan for sale, the three nuggets bought, at the table's prices when he is made.</summary>
public class SBCompactAssayer : SBInfo
{
    public override IShopSellInfo SellInfo { get; } = new InternalSellInfo();

    public override List<GenericBuyInfo> BuyInfo { get; } = new InternalBuyInfo();

    public class InternalBuyInfo : List<GenericBuyInfo>
    {
        public InternalBuyInfo()
        {
            var t = GoldPanningTable.Current;
            Add(new GenericBuyInfo("a prospector's pan", typeof(ProspectorsPan), t.Pan.Price, 20, ProspectorsPan.PanItemID, 0x973));
        }
    }

    public class InternalSellInfo : GenericSellInfo
    {
        public InternalSellInfo()
        {
            foreach (var row in GoldPanningTable.Current.Sizes)
                if (row.Gold > 0)
                    Add(GoldPanning.TypeFor(row.Size), row.Gold);
        }
    }
}

/// <summary>Places a Compact assayer beside each Miners' Compact Liaison in the world, on every world load (cc-P781).</summary>
public static class ClusterFAssayerSeeder
{
    public static void Configure()
    {
        foreach (var line in GoldPanningTable.LoadOrWriteDefaults(GoldPanningTable.FilePath))
            Console.WriteLine($"[GoldPanning] {line}");

        // After the load: things made inside WorldLoad reach World.Mobiles only once it is over.
        EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () => Seed(verbose: false));
        CommandSystem.Register("ClusterFSeedAssayers", AccessLevel.Administrator, OnSeedCommand);
        CommandSystem.Register("GoldPanningStaff", AccessLevel.GameMaster, OnStaffCommand);
    }

    [Usage("ClusterFSeedAssayers")]
    [Description("Places a Compact assayer beside each Miners' Compact Liaison that has none near. It also runs by itself on every world load.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.Any, NoDryRun = true, Summary = "Places missing Compact assayers beside the Miners' Compact Liaisons. It also runs by itself on every world load.")]
    private static void OnSeedCommand(CommandEventArgs e)
    {
        foreach (var line in Seed(verbose: true))
            e.Mobile.SendMessage(line);
    }

    [Usage("GoldPanningStaff [reload|status]")]
    [Description("reload reads Configuration/gold-panning.json again (a bad file is refused whole); status says the numbers in force. Prices in a standing assayer's shop change at the next restart.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.Any)]
    private static void OnStaffCommand(CommandEventArgs e)
    {
        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
        if (mode == "reload")
        {
            foreach (var line in GoldPanningTable.Reload(GoldPanningTable.FilePath))
                e.Mobile.SendMessage(line);
            return;
        }

        var t = GoldPanningTable.Current;
        e.Mobile.SendMessage($"Gold panning {(t.Enabled ? "on" : "off")} ({t.Source}): Mining {t.MinSkill}+, " +
                             string.Join(", ", t.Sizes.Select(s => $"{s.Size} {s.ReqSkill}+ w{s.Weight} {s.Gold}g/{s.Standing}st")) +
                             $"; gems {t.Gems.ChancePercent}%; banks {t.Bank.MinTotal}-{t.Bank.MaxTotal} per {t.Bank.BankWidth}x{t.Bank.BankHeight}.");
    }

    public static List<string> Seed(bool verbose)
    {
        var report = new List<string>();
        int placed = 0, present = 0, skipped = 0;
        var range = GoldPanningTable.Current.Assayer.SeedRange;

        var liaisons = World.Mobiles.Values.OfType<MinersCompactLiaison>()
            .Where(l => !l.Deleted && l.Map != null && l.Map != Map.Internal).ToList();

        foreach (var liaison in liaisons)
        {
            var map = liaison.Map;
            var near = World.Mobiles.Values.OfType<CompactAssayer>()
                .FirstOrDefault(a => !a.Deleted && a.Map == map && a.InRange(liaison.Location, range));
            if (near != null)
            {
                present++;
                if (verbose) report.Add($"Assayer by the liaison at {liaison.Location} on {map.Name}: already at {near.Location}.");
                continue;
            }

            if (!ClusterFRequisitionSeeder.Fit(map, liaison.X + 2, liaison.Y, liaison.Z, out var loc))
            {
                skipped++;
                report.Add($"Assayer by the liaison at {liaison.Location} on {map.Name}: nothing fits near; skipped. Tell a Game Master.");
                continue;
            }

            var assayer = new CompactAssayer { CantWalk = true, Home = loc, RangeHome = 0, Direction = liaison.Direction };
            assayer.MoveToWorld(loc, map);
            placed++;
            report.Add($"Assayer by the liaison at {liaison.Location}: placed at {loc} on {map.Name}.");
        }

        report.Add($"Assayer seed: placed {placed}, already there {present}, skipped {skipped} ({liaisons.Count} liaisons).");
        foreach (var line in report)
            Console.WriteLine($"[ClusterFAssayerSeeder] {line}");
        return report;
    }
}
