// HolidayStockingVerification.cs
//
// cc-P784: the holiday stocking at the winter Festival stall. Notes in shard-migration notes/cc-P784-holiday-stocking.md.
// Each fact was seen red first (the notes' red table).
//
// Facts:
//   1. The prize table: the defaults validate and round-trip as JSON; every prize is a treat or small decor (no deed,
//      special scroll, weapon, armour, jewellery, clothing, talisman or container); a file naming a power scroll, a deed
//      or a weapon is refused whole and the table in force is kept; each row is drawn by its weight; a row's amount is
//      what the player gets.
//   2. The season gate: out of season, and in the wrong season (Halloween), the stocking gives nothing and no token;
//      in winter it gives a prize.
//   3. The daily limit: once a day per account (a second character on the account gets nothing too), a token only on
//      the day's first grab, moving during the wait costs nothing, and the next day it is full again.
//   4. The seeding: it hangs by itself at the stall while the winter stall is up (on the world-load sync and when the
//      stall goes up), once however often the sync runs, and comes down out of season and with the stall.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.RewardStores;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HolidayStockingVerification : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly List<Account> _accounts = [];
    private readonly List<Item> _items = [];
    private readonly Func<DateTime> _clock = FestivalSeasons.Clock;
    private DateTime _now = new(2026, 12, 20, 12, 0, 0);

    private static bool _achievements;

    private static readonly Point3D Spot = new(3460, 2604, 18);

    public HolidayStockingVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        if (!_achievements)
        {
            ClusterFAchievementSystem.Configure();
            _achievements = true;
        }

        HolidayStockingTable.Current = HolidayStockingTable.Defaults;
        FestivalSeasons.Clock = () => _now;
        FestivalSeasons.Forced = FestivalSeasons.Winter;
    }

    public void Dispose()
    {
        FestivalSeasons.Clock = _clock;
        FestivalSeasons.Forced = null;
        HolidayStockingTable.Current = HolidayStockingTable.Defaults;
        HolidayStockings.Clear();
        FestivalMarket.Down();
        foreach (var i in _items)
        {
            i?.Delete();
        }

        foreach (var a in _accounts)
        {
            for (var i = 0; i < a.Length; i++)
            {
                a[i]?.Delete();
            }

            Accounts.Remove(a);
        }
    }

    // ---------------------------------------------------------------- helpers

    private Account NewAccount()
    {
        var a = new Account($"p784{Guid.NewGuid():N}"[..16], "p784-test-only");
        _accounts.Add(a);
        return a;
    }

    private static PlayerMobile NewCharacter(Account account, int slot = 0)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
        pm.AddItem(new Backpack());
        account[slot] = pm;
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private HolidayStocking NewStocking()
    {
        var s = new HolidayStocking();
        s.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        _items.Add(s);
        return s;
    }

    private static int Tokens(PlayerMobile pm) => FestivalTokens.Instance.Balance(pm);

    private static int PackCount(PlayerMobile pm) => pm.Backpack.Items.Count;

    // ---------------------------------------------------------------- 1. the prize table

    [Fact]
    public void ThePrizeTableIsTreatsAndDecorOnlyAndDrawsByWeight()
    {
        var t = HolidayStockingTable.Defaults;
        var errors = new List<string>();
        HolidayStockingTable.Validate(t, errors);
        Assert.Empty(errors);
        Assert.Equal(FestivalSeasons.Winter, t.Season);
        Assert.Equal(6.0, t.HoldSeconds);
        Assert.Equal(1, t.GrabsPerDay);
        Assert.Equal(1, t.TokensOnFirstGrab);

        // Every prize is made, is a real item, and is none of the refused kinds.
        foreach (var row in t.Prizes)
        {
            Assert.NotNull(row.ItemType);
            Assert.Null(HolidayStockingTable.Forbidden(row.ItemType!));
            var prize = HolidayStockings.Make(row);
            Assert.NotNull(prize);
            _items.Add(prize!);
            var count = prize is Bag bag ? bag.Items.Sum(i => i.Amount) : prize!.Amount;
            Assert.Equal(row.Amount, count);
            Assert.DoesNotContain("Deed", prize!.GetType().Name);
            Assert.False(prize is SpecialScroll or BaseWeapon or BaseArmor or BaseJewel or BaseClothing);
        }

        // The JSON round-trip.
        var back = HolidayStockingTable.Parse(HolidayStockingTable.ToJson(t), "round-trip", errors);
        Assert.Empty(errors);
        Assert.NotNull(back);
        Assert.Equal(t.Prizes.Select(p => (p.Type, p.Amount, p.Weight)), back!.Prizes.Select(p => (p.Type, p.Amount, p.Weight)));

        // A table naming a power scroll, a deed or a weapon is refused whole; the table in force is kept.
        foreach (var bad in new[] { "PowerScroll", "StatCapScroll", "HolidayTreeDeed", "Longsword", "GoldRing", "Bag" })
        {
            var table = HolidayStockingTable.BuildDefaults();
            table.Prizes.Add(new StockingPrizeRow { Type = bad, Amount = 1, Weight = 1 });
            var errs = new List<string>();
            Assert.Null(HolidayStockingTable.Parse(HolidayStockingTable.ToJson(table), "bad", errs));
            Assert.Contains(errs, e => e.Contains(bad));
            _out.WriteLine(string.Join("; ", errs));
        }

        Assert.Null(HolidayStockingTable.Parse("{\"seasonz\": \"winter\"}", "unknown", new List<string>()));
        Assert.Same(HolidayStockingTable.Defaults, HolidayStockingTable.Current);

        // Drawn by weight: each row owns its share of [0, 1).
        var total = (double)t.TotalWeight;
        var at = 0.0;
        foreach (var row in t.Prizes)
        {
            Assert.Same(row, t.Draw(at / total));
            Assert.Same(row, t.Draw((at + row.Weight - 0.001) / total));
            at += row.Weight;
        }

        Assert.Same(t.Prizes[^1], t.Draw(0.9999999));
    }

    // ---------------------------------------------------------------- 2. the season gate

    [Fact]
    public void TheStockingGivesOnlyWhileItsSeasonIsOn()
    {
        var pm = NewCharacter(NewAccount());
        var stocking = NewStocking();

        foreach (var off in new[] { "none", FestivalSeasons.Halloween, FestivalSeasons.Spring })
        {
            FestivalSeasons.Forced = off;
            Assert.NotNull(HolidayStockings.CannotGrab(pm, stocking));
            Assert.False(HolidayStockings.Begin(pm, stocking));
            Assert.Null(HolidayStockings.Finish(pm, stocking, pm.Location, 0.0));
            Assert.Equal(0, PackCount(pm));
            Assert.Equal(0, Tokens(pm));
        }

        // Out of season by the dates (no staff force): July.
        FestivalSeasons.Forced = null;
        _now = new DateTime(2027, 7, 4, 12, 0, 0);
        Assert.Null(HolidayStockings.Finish(pm, stocking, pm.Location, 0.0));

        // Winter by the dates: a prize and the token.
        _now = new DateTime(2026, 12, 20, 12, 0, 0);
        Assert.True(HolidayStockings.InSeason);
        Assert.Null(HolidayStockings.CannotGrab(pm, stocking));
        Assert.NotNull(HolidayStockings.Finish(pm, stocking, pm.Location, 0.0));
        Assert.Equal(1, PackCount(pm));
        Assert.Equal(1, Tokens(pm));

        // Off by the table: nothing.
        var off2 = HolidayStockingTable.BuildDefaults();
        off2.Enabled = false;
        HolidayStockingTable.Validate(off2, new List<string>());
        HolidayStockingTable.Current = off2;
        _now = _now.AddDays(1);
        Assert.Null(HolidayStockings.Finish(pm, stocking, pm.Location, 0.0));
        Assert.Equal(1, PackCount(pm));
    }

    // ---------------------------------------------------------------- 3. the daily limit

    [Fact]
    public void OnceADayPerAccountWithATokenOnlyOnTheFirstGrab()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        var alt = NewCharacter(account, 1);
        var stocking = NewStocking();

        // Moving during the wait: nothing, and nothing counted.
        Assert.Null(HolidayStockings.Finish(pm, stocking, new Point3D(Spot.X - 1, Spot.Y, Spot.Z), 0.0));
        Assert.Equal(0, HolidayStockings.GrabsToday(account.Username));

        // Out of range: nothing.
        pm.MoveToWorld(new Point3D(Spot.X - 5, Spot.Y, Spot.Z), Map.Trammel);
        Assert.Null(HolidayStockings.Finish(pm, stocking, pm.Location, 0.0));
        pm.MoveToWorld(Spot, Map.Trammel);

        // The day's grab: one prize, one token.
        Assert.NotNull(HolidayStockings.Finish(pm, stocking, pm.Location, 0.5));
        Assert.Equal(1, PackCount(pm));
        Assert.Equal(1, Tokens(pm));
        Assert.Equal(1, HolidayStockings.GrabsToday(account.Username));

        // Again the same day, later: nothing, from either character on the account.
        _now = _now.AddHours(8);
        Assert.NotNull(HolidayStockings.CannotGrab(pm, stocking));
        Assert.Null(HolidayStockings.Finish(pm, stocking, pm.Location, 0.5));
        Assert.Null(HolidayStockings.Finish(alt, stocking, alt.Location, 0.5));
        Assert.Equal(1, PackCount(pm));
        Assert.Equal(0, PackCount(alt));
        Assert.Equal(1, Tokens(pm));

        // Another account the same day still gets its own.
        var other = NewCharacter(NewAccount());
        Assert.NotNull(HolidayStockings.Finish(other, stocking, other.Location, 0.5));

        // The next day: full again, with the token.
        _now = new DateTime(2026, 12, 21, 9, 0, 0);
        Assert.Null(HolidayStockings.CannotGrab(alt, stocking));
        Assert.NotNull(HolidayStockings.Finish(alt, stocking, alt.Location, 0.5));
        Assert.Equal(1, PackCount(alt));
        Assert.Equal(2, Tokens(pm));

        // Two grabs a day by the table: the second gives a prize but no token.
        var two = HolidayStockingTable.BuildDefaults();
        two.GrabsPerDay = 2;
        HolidayStockingTable.Validate(two, new List<string>());
        HolidayStockingTable.Current = two;
        _now = new DateTime(2026, 12, 22, 9, 0, 0);
        Assert.NotNull(HolidayStockings.Finish(pm, stocking, pm.Location, 0.5));
        Assert.Equal(3, Tokens(pm));
        Assert.NotNull(HolidayStockings.Finish(pm, stocking, pm.Location, 0.5));
        Assert.Equal(3, Tokens(pm));
        Assert.Null(HolidayStockings.Finish(pm, stocking, pm.Location, 0.5));
    }

    // ---------------------------------------------------------------- 4. the seeding

    [Fact]
    public void TheStockingHangsByItselfWithTheWinterStallOnce()
    {
        FestivalMarket.Down();
        HolidayStockings.Clear();
        ClusterFSeasonalState.SetLast(FestivalMarket.StateName, null);

        // No stall, no stocking, even in winter.
        Assert.Empty(HolidayStockings.Sync());
        Assert.Null(HolidayStockings.Stocking);

        // The winter stall going up hangs it, at its spot on the table.
        FestivalMarket.Sync();
        Assert.True(FestivalMarket.IsUp);
        var stocking = HolidayStockings.Stocking;
        Assert.NotNull(stocking);
        Assert.Equal(HolidayStockings.Spot, stocking!.Location);
        Assert.False(stocking.Movable);

        // An existing world: the world-load sync again, any number of times, places nothing more.
        HolidayStockings.Sync();
        HolidayStockings.Sync();
        HolidayStockings.Place();
        Assert.Single(HolidayStockings.Placed());
        Assert.Single(World.Items.Values.OfType<HolidayStocking>(), s => !s.Deleted && s.Map == FestivalMarket.Facet);

        // Taken away by staff while the stall stands: the next sync hangs it again.
        stocking.Delete();
        HolidayStockings.Sync();
        Assert.NotNull(HolidayStockings.Stocking);
        Assert.Single(HolidayStockings.Placed());

        // Another season with the stall up: no stocking.
        FestivalSeasons.Forced = FestivalSeasons.Halloween;
        HolidayStockings.Sync();
        Assert.Null(HolidayStockings.Stocking);
        Assert.Empty(HolidayStockings.Placed());

        // Winter again, then the stall comes down: the stocking with it.
        FestivalSeasons.Forced = FestivalSeasons.Winter;
        HolidayStockings.Sync();
        var again = HolidayStockings.Stocking;
        Assert.NotNull(again);
        FestivalMarket.Down();
        Assert.True(again!.Deleted);
        Assert.Empty(HolidayStockings.Placed());
    }
}
