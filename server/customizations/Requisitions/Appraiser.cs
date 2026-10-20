// cc-P150 (F-47): the Appraiser, a League NPC who buys any magic item no requisition wants, at AppraiserPricing's floor
// (property count and intensity, near stock shopkeeper levels). Stands in the New Haven field office and in the major
// bank towns, placed by ClusterFRequisitionSeeder (below) on every world load, duplicate-safe, never from pinned spawn
// data. An item that is not magic is refused: a shopkeeper buys those.
//
//   Double-click or "Talk"   AppraiserGump: Appraise (a quote), Sell (a quote with Sell and Keep), Which requisition
//                            wants this (the first open request the item fills, if any), the requisitions board.
//   Drop an item on him      the Sell quote at once; the item stays with the player until Sell is pressed.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.Commands;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Appraiser : BaseCreature
{
    public const int Range = 4;

    // Fixed names: NameList is not loaded in the test host, and a League clerk may as well have a steady one.
    private static readonly string[] MaleNames   = ["Corwin", "Aldous", "Perrin", "Thaddeus", "Osric", "Lucan"];
    private static readonly string[] FemaleNames = ["Mirabel", "Ysolde", "Tamsin", "Rosalind", "Averil", "Lenore"];

    [Constructible]
    public Appraiser() : base(AIType.AI_Animal, FightMode.None, 10, 1)
    {
        var female = Utility.RandomBool();
        Name   = (female ? FemaleNames : MaleNames)[Utility.Random(MaleNames.Length)];
        Title  = "the League Appraiser";
        Body   = female ? 0x191 : 0x190;
        Hue    = Race.Human.RandomSkinHue();
        Female = female;

        SetStr(50);
        SetDex(50);
        SetInt(90);
        SetHits(100);

        Fame  = 0;
        Karma = 5000;

        AddItem(new FancyShirt { Movable = false, Hue = 0x455 });
        AddItem(new LongPants  { Movable = false, Hue = 0x1BB });
        AddItem(new Boots      { Movable = false, Hue = 0x455 });
        AddItem(new HalfApron  { Movable = false, Hue = 0x8AB });

        var hair = new Item(female ? 0x203C : 0x2048) { Movable = false, Hue = Race.Human.RandomHairHue() };
        hair.Layer = Layer.Hair;
        AddItem(hair);
    }

    public override bool IsInvulnerable => true;
    public override bool ClickTitle      => true;
    public override bool ShowFameTitle   => false;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm) return;

        if (!pm.InRange(Location, Range))
        {
            pm.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        pm.CloseGump<AppraiserGump>();
        pm.SendGump(new AppraiserGump(pm, this));
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (from is not PlayerMobile pm || !pm.InRange(Location, Range))
            return false;

        AppraiserGump.Quote(pm, this, dropped, true);
        return false; // the item stays with the player until Sell is pressed
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from is PlayerMobile pm && pm.InRange(Location, Range))
            list.Add(new TalkEntry(pm, this));
    }

    private sealed class TalkEntry : ContextMenuEntry
    {
        private readonly PlayerMobile _pm;
        private readonly Appraiser _npc;

        public TalkEntry(PlayerMobile pm, Appraiser npc) : base(6146) // "Talk"
        {
            _pm = pm;
            _npc = npc;
        }

        public override void OnClick(Mobile from, IEntity target) => _npc.OnDoubleClick(_pm);
    }

    /// <summary>Sells the item: deletes it and pays the price. False with a reason when it cannot be sold.</summary>
    public static bool Sell(PlayerMobile pm, Appraiser npc, Item item, out string message)
    {
        if (npc == null || npc.Deleted || !pm.InRange(npc.Location, Range) || pm.Map != npc.Map)
        {
            message = "The Appraiser is not within reach.";
            return false;
        }

        if (item == null || item.Deleted || pm.Backpack == null || !item.IsChildOf(pm.Backpack))
        {
            message = "The item must be in your backpack.";
            return false;
        }

        if (item.LootType == LootType.Blessed)
        {
            message = "The Appraiser does not buy blessed items.";
            return false;
        }

        var price = AppraiserPricing.Price(item);
        if (price <= 0)
        {
            message = "That has no magic properties. A shopkeeper will buy it.";
            return false;
        }

        var name = item.Name ?? item.GetType().Name;
        item.Delete();
        ClusterFRequisitions.GiveGold(pm, price);
        message = $"The Appraiser pays {price} gold for the {name}.";
        return true;
    }
}

public class AppraiserGump : Gump
{
    private readonly PlayerMobile _pm;
    private readonly Appraiser _npc;
    private readonly Item _item;   // the item quoted, when this is a quote
    private readonly bool _offer;  // a quote with Sell

    private const int W = 440;

    public const int BtnClose = 0;
    public const int BtnAppraise = 1;
    public const int BtnSell = 2;
    public const int BtnWhoWants = 3;
    public const int BtnBoard = 4;
    public const int BtnConfirmSell = 5;

    public AppraiserGump(PlayerMobile pm, Appraiser npc) : this(pm, npc, null, false) { }

    public AppraiserGump(PlayerMobile pm, Appraiser npc, Item item, bool offer) : base(100, 100)
    {
        _pm = pm;
        _npc = npc;
        _item = item;
        _offer = offer;

        Closable = true;
        Disposable = true;

        var h = item == null ? 250 : 230;
        AddBackground(0, 0, W, h, 9270);
        AddAlphaRegion(10, 10, W - 20, h - 20);
        AddLabel(20, 14, 1154, $"{npc.Name}, the League Appraiser");
        AddImageTiled(10, 36, W - 20, 2, 9304);

        if (item == null)
        {
            AddHtml(20, 44, W - 40, 60,
                "<BASEFONT COLOR=#DDDDDD>I buy magic items no requisition wants: a floor price by how many properties a piece has and how strong they are, near what a shopkeeper pays. " +
                "Fill a requisition instead and you get the same gold on top of Renown and standing.</BASEFONT>", false, false);

            var y = 112;
            AddButton(20, y, 4005, 4007, BtnAppraise); AddLabel(55, y + 2, 999, "Appraise an item (a quote, nothing sold)"); y += 28;
            AddButton(20, y, 4005, 4007, BtnSell);     AddLabel(55, y + 2, 999, "Sell an item");                            y += 28;
            AddButton(20, y, 4005, 4007, BtnWhoWants); AddLabel(55, y + 2, 999, "Which requisition wants this?");           y += 28;
            AddButton(20, y, 4005, 4007, BtnBoard);    AddLabel(55, y + 2, 999, "The requisitions board");
        }
        else
        {
            var name = item.Name ?? item.GetType().Name;
            var price = AppraiserPricing.Price(item);
            AddHtml(20, 44, W - 40, 40, $"<BASEFONT COLOR=#FFFFFF>{name}</BASEFONT>", false, false);
            AddHtml(20, 70, W - 40, 60, $"<BASEFONT COLOR=#DDDDDD>{AppraiserPricing.Explain(item)}</BASEFONT>", false, false);

            var wants = price > 0 ? ClusterFRequisitions.FirstMatch(pm, item) : null;
            if (wants != null)
                AddHtml(20, 128, W - 40, 40,
                    $"<BASEFONT COLOR=#FFD700>A requisition wants this: {ClusterFRequisitions.Describe(wants)} ({ClusterFGuildSystem.GetDef(wants.GuildKey)?.Name ?? wants.GuildKey}). Turn it in there for the same gold, plus Renown and standing.</BASEFONT>", false, false);

            if (offer && price > 0)
            {
                AddButton(20, h - 40, 4023, 4025, BtnConfirmSell);
                AddLabel(55, h - 38, 1154, $"Sell for {price} gold");
            }
        }

        AddButton(W - 90, h - 40, 4017, 4019, BtnClose);
        AddLabel(W - 55, h - 38, 999, item == null ? "Close" : "Keep");
    }

    public static void Quote(PlayerMobile pm, Appraiser npc, Item item, bool offer)
    {
        pm.CloseGump<AppraiserGump>();
        pm.SendGump(new AppraiserGump(pm, npc, item, offer));
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != _pm)
            return;

        switch (info.ButtonID)
        {
            case BtnAppraise:
                _pm.SendMessage(0x3B2, "Which item?");
                _pm.Target = new AppraiserTarget(_pm, _npc, AppraiserTarget.Mode.Quote);
                return;

            case BtnSell:
                _pm.SendMessage(0x3B2, "Which item would you sell?");
                _pm.Target = new AppraiserTarget(_pm, _npc, AppraiserTarget.Mode.Sell);
                return;

            case BtnWhoWants:
                _pm.SendMessage(0x3B2, "Which item?");
                _pm.Target = new AppraiserTarget(_pm, _npc, AppraiserTarget.Mode.WhoWants);
                return;

            case BtnBoard:
                RequisitionsBoard.OpenFor(_pm, null);
                return;

            case BtnConfirmSell when _offer && _item != null:
                var ok = Appraiser.Sell(_pm, _npc, _item, out var message);
                _pm.SendMessage(ok ? 0x3B2 : 0x22, message);
                return;
        }
    }
}

public class AppraiserTarget : Target
{
    public enum Mode { Quote, Sell, WhoWants }

    private readonly PlayerMobile _pm;
    private readonly Appraiser _npc;
    private readonly Mode _mode;

    public AppraiserTarget(PlayerMobile pm, Appraiser npc, Mode mode) : base(2, false, TargetFlags.None)
    {
        _pm = pm;
        _npc = npc;
        _mode = mode;
    }

    protected override void OnTarget(Mobile from, object targeted)
    {
        if (from != _pm)
            return;

        if (targeted is not Item item)
        {
            _pm.SendMessage(0x22, "That is not an item.");
            return;
        }

        switch (_mode)
        {
            case Mode.WhoWants:
            {
                var wants = ClusterFRequisitions.FirstMatch(_pm, item);
                _pm.SendMessage(0x3B2, wants == null
                    ? "No open requisition of yours wants that."
                    : $"{ClusterFGuildSystem.GetDef(wants.GuildKey)?.Name ?? wants.GuildKey} wants it: {ClusterFRequisitions.Describe(wants)}.");
                return;
            }

            default:
                AppraiserGump.Quote(_pm, _npc, item, _mode == Mode.Sell);
                return;
        }
    }
}

/// <summary>
/// Places the Appraisers and the field office's League board (cc-P150) on every world load, duplicate-safe like
/// ClusterFInstitutionSeeder: an Appraiser within 15 tiles of a post on its map counts as placed; a RequisitionsBoard
/// within 5 tiles of the office's tile too. A tile that does not fit (Map.CanFit) is tried at the map's own z and then
/// around it; a post that still fits nowhere is reported and skipped, never forced. [ClusterFSeedRequisitions re-runs it.
/// </summary>
public static class ClusterFRequisitionSeeder
{
    public sealed record Post(string Town, string Where, int MapIndex, int X, int Y, int Z, Direction Facing, bool Probed);

    // Trammel tiles were probed standable with the cc-P15 CanFit port (notes/cc-P77-tools/probe.py); Luna and the Royal
    // City are next to pinned's Banker spawners (Distribution/Data/Spawns) and are checked by CanFit when placed.
    public static readonly Post[] AppraiserPosts =
    [
        new("New Haven", "League field office", 1, 3461, 2599, 18, Direction.West, true),
        new("Britain", "outside the West Bank", 1, 1426, 1694, 0, Direction.North, true),
        new("Trinsic", "by the bank", 1, 1900, 2685, 10, Direction.West, true),
        new("Minoc", "by the bank", 1, 2505, 554, 0, Direction.West, true),
        new("Vesper", "by the bank", 1, 2883, 686, 0, Direction.West, true),
        new("Moonglow", "by the bank", 1, 4473, 1158, 0, Direction.West, true),
        new("Skara Brae", "by the bank", 1, 586, 2148, 0, Direction.East, true),
        new("Luna", "by the bank", 2, 992, 523, -50, Direction.West, false),
        new("Royal City", "by the bank", 5, 838, 3439, -20, Direction.South, false),
    ];

    /// <summary>The League board in the field office: on the north wall, beside the flowerpot (ClusterFRegistrarOfficeLayout).</summary>
    public static readonly Post OfficeBoard = new("New Haven", "League field office, north wall", 1, 3459, 2596, 18, Direction.North, true);

    public static void Configure()
    {
        EventSink.WorldLoad += () => Seed(verbose: false);
        CommandSystem.Register("ClusterFSeedRequisitions", AccessLevel.Administrator, OnSeedCommand);
    }

    [Usage("ClusterFSeedRequisitions")]
    [Description("Places the League Appraisers and the field office requisitions board where missing. It also runs by itself on every world load.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.Any, NoDryRun = true, Summary = "Places missing League Appraisers and the field office requisitions board. It also runs by itself on every world load.")]
    private static void OnSeedCommand(CommandEventArgs e)
    {
        foreach (var line in Seed(verbose: true))
            e.Mobile.SendMessage(line);
    }

    public static List<string> Seed(bool verbose)
    {
        var report = new List<string>();
        int placed = 0, present = 0, skipped = 0;

        foreach (var post in AppraiserPosts)
        {
            var map = MapOf(post.MapIndex);
            if (map == null)
            {
                report.Add($"Appraiser, {post.Town}: facet {post.MapIndex} is not loaded, skipped.");
                skipped++;
                continue;
            }

            if (Find<Appraiser>(map, post.X, post.Y, 15) is { } there)
            {
                present++;
                if (verbose) report.Add($"Appraiser, {post.Town}: already at {there.Location}.");
                continue;
            }

            if (!Fit(map, post.X, post.Y, post.Z, out var loc))
            {
                report.Add($"Appraiser, {post.Town}: nothing fits at or around {post.X}, {post.Y}, {post.Z} on {map.Name}; skipped. Tell a Game Master.");
                skipped++;
                continue;
            }

            var npc = new Appraiser { Direction = post.Facing, CantWalk = true, Home = loc, RangeHome = 0 };
            npc.MoveToWorld(loc, map);
            placed++;
            report.Add($"Appraiser, {post.Town} ({post.Where}): placed at {loc} on {map.Name}.");
        }

        var officeMap = MapOf(OfficeBoard.MapIndex);
        if (officeMap != null)
        {
            if (FindItem<RequisitionsBoard>(officeMap, OfficeBoard.X, OfficeBoard.Y, 5) is { } board)
            {
                present++;
                if (verbose) report.Add($"League board: already at {board.Location}.");
            }
            else
            {
                var item = new RequisitionsBoard();
                item.MoveToWorld(new Point3D(OfficeBoard.X, OfficeBoard.Y, OfficeBoard.Z), officeMap);
                placed++;
                report.Add($"League board: placed at {item.Location} ({OfficeBoard.Where}).");
            }
        }

        report.Add($"Requisition seed: placed {placed}, already there {present}, skipped {skipped}.");
        foreach (var line in report)
            Console.WriteLine($"[ClusterFRequisitionSeeder] {line}");
        return report;
    }

    private static Map MapOf(int index) =>
        index >= 0 && index < Map.Maps.Length && Map.Maps[index] is { } m && m != Map.Internal ? m : null;

    private static T Find<T>(Map map, int x, int y, int range) where T : Mobile
    {
        foreach (var m in World.Mobiles.Values)
            if (m is T t && !m.Deleted && m.Map == map && Math.Abs(m.X - x) <= range && Math.Abs(m.Y - y) <= range)
                return t;
        return null;
    }

    private static T FindItem<T>(Map map, int x, int y, int range) where T : Item
    {
        foreach (var i in World.Items.Values)
            if (i is T t && !i.Deleted && i.Map == map && Math.Abs(i.X - x) <= range && Math.Abs(i.Y - y) <= range)
                return t;
        return null;
    }

    /// <summary>The post's tile at its z, else at the map's z there, else the nearest fitting tile within 2.</summary>
    public static bool Fit(Map map, int x, int y, int z, out Point3D loc)
    {
        if (map.CanFit(x, y, z, 16, false, false))
        {
            loc = new Point3D(x, y, z);
            return true;
        }

        var avg = map.GetAverageZ(x, y);
        if (map.CanFit(x, y, avg, 16, false, false))
        {
            loc = new Point3D(x, y, avg);
            return true;
        }

        for (var r = 1; r <= 2; r++)
            for (var dx = -r; dx <= r; dx++)
                for (var dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    foreach (var tz in new[] { z, map.GetAverageZ(x + dx, y + dy) })
                        if (map.CanFit(x + dx, y + dy, tz, 16, false, false))
                        {
                            loc = new Point3D(x + dx, y + dy, tz);
                            return true;
                        }
                }

        loc = Point3D.Zero;
        return false;
    }
}
