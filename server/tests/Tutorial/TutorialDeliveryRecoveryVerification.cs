// TutorialDeliveryRecoveryVerification.cs
//
// cc-P613 (P573 #1, #2; P586 U1). Notes in shard-migration notes/cc-P613-fix-drafts-from-p586.md.
//
// Facts:
//   1. The Farmhand's bandages: a pack full by item count, a pack full by weight, and no pack at all get no stack, none
//      on the ground, and the account's allowance stays unspent however often the trainee asks; with room again, one
//      counted stack. Successful issues still stop at the saved cap.
//   2. An active record (core, wings or remedial) whose character is gone (no serial, deleted, or moved to another
//      account) carries on with the account's next character, at login or at Rook: progress and claims kept, the farm
//      talks to them, a save and load keeps it, and a third character cannot take it over while the second one stands.
//      A living recorded character keeps it: another of the account's characters sees Rook's page read-only.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialDeliveryRecoveryVerification
{
    private static readonly Point3D Spot = new(3395, 2614, 0);

    public TutorialDeliveryRecoveryVerification()
    {
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ClusterFTutorial.Enabled = true;
        FoldMaps.Configure(); // cc-P663: the lessons are on the isle (map 32)
        TutorialFarm.EnsureHooks();
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }
    }

    // An account and its characters, each with a pack and a connection; the first one entered into the tutorial.
    private sealed class Trainees : IDisposable
    {
        public readonly Account Account = new($"p613{Guid.NewGuid():N}"[..16], "p613-test-only");
        public readonly List<PlayerMobile> Characters = new();
        public readonly List<IEntity> Litter = new();
        private readonly List<NetState> _states = new();

        public Trainees(bool enter = true)
        {
            ShardTestClock.Arm();
            var first = Add();
            if (enter)
            {
                Assert.True(ClusterFTutorial.TryEnter(first));
                TutorialTestStart.ToTheFarm(first); // cc-P663: the farm is core.farm, after chat
            }
        }

        public PlayerMobile Add()
        {
            var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = $"Trainee{Characters.Count}", RawStr = 60, RawDex = 60, RawInt = 60 };
            pm.AddItem(new Backpack());
            Account[Characters.Count] = pm;
            pm.MoveToWorld(Spot, Map.Trammel);
            var ns = PacketTestUtilities.CreateTestNetState();
            ns.Account = Account;
            pm.NetState = ns;
            ns.Mobile = pm;
            _states.Add(ns);
            Characters.Add(pm);
            return pm;
        }

        public TutorialRecord Record => ClusterFAccountPersistence.GetOrCreate(Account).Tutorial;

        public void Dispose()
        {
            foreach (var e in Litter)
            {
                e.Delete();
            }

            foreach (var pm in Characters)
            {
                foreach (var m in pm.AllFollowers?.ToList() ?? new List<Mobile>())
                {
                    m.Delete();
                }

                pm.NetState = null;
                pm.Delete();
            }

            foreach (var ns in _states)
            {
                ns.Mobile = null;
                ns.Dispose();
            }

            ClusterFFanfare.FlushAll();
            Accounts.Remove(Account);
        }
    }

    private static TutorialRecord RoundTrip(Account account)
    {
        var w = new BufferWriter(true);
        ClusterFAccountPersistence.GetOrCreate(account).Serialize(w);
        w.Write(0x5E471E1); // a sentinel after the record: the record reads back exactly its own bytes
        var r = new BufferReader(w.Buffer);
        var data = new ClusterFAccountData(r);
        Assert.Equal(0x5E471E1, r.ReadInt());
        return data.Tutorial;
    }

    private static int BandagesOnTheGround() =>
        World.Items.Values.Count(i => i is Bandage && !i.Deleted && i.Parent == null && i.Map == Map.Trammel);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ABandageThatCannotBeDeliveredSpendsNothing()
    {
        foreach (var why in new[] { "items", "weight", "no pack" })
        {
            using var t = new Trainees();
            var pm = t.Characters[0];
            var hand = new TutorialFarmhand();
            t.Litter.Add(hand);
            hand.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
            t.Record.PetClaimed = true;
            t.Record.StationsDone.UnionWith([TutorialFarm.PestsKey, TutorialFarm.FollowKey, TutorialFarm.GuardKey, TutorialFarm.CoreStation]); // cc-P726: on the healing lesson
            Assert.True(TutorialFarm.HealingOn(pm));

            var ballast = new List<Item>();
            var pack = pm.Backpack;
            switch (why)
            {
                case "items":
                    while (pack.TotalItems < pack.MaxItems)
                    {
                        var filler = new Item(0x1F1C);
                        pack.DropItem(filler);
                        ballast.Add(filler);
                    }
                    break;
                case "weight":
                    var stone = new Item(0x1BF2) { Weight = pack.MaxWeight - pack.TotalWeight };
                    pack.DropItem(stone);
                    ballast.Add(stone);
                    break;
                default:
                    pack.Delete();
                    break;
            }

            var ground = BandagesOnTheGround();
            for (var i = 0; i < TutorialFarm.BandageIssues + 2; i++)
            {
                TutorialFarm.BeginHealing(pm); // cc-P726: the healing lesson hands the bandages
            }

            Assert.True(TutorialFarm.GiveBandages(pm) != TutorialFarm.BandageGift.Given, why);
            Assert.Equal(0, t.Record.BandagesIssued);
            Assert.Equal(ground, BandagesOnTheGround());
            Assert.Null(pm.Backpack?.FindItemByType<Bandage>());

            // Room again: one counted stack, through the Farmhand.
            ballast.ForEach(b => b.Delete());
            if (pm.Backpack == null)
            {
                pm.AddItem(new Backpack());
            }

            TutorialFarm.BeginHealing(pm); // cc-P726: the healing lesson hands the bandages
            Assert.Equal(1, t.Record.BandagesIssued);
            Assert.NotNull(pm.Backpack!.FindItemByType<Bandage>());
            Assert.Equal(ground, BandagesOnTheGround());
            Assert.Equal(1, RoundTrip(t.Account).BandagesIssued);
        }
    }

    [Fact]
    public void DeliveredBandagesStillStopAtTheSavedCap()
    {
        using var t = new Trainees();
        var pm = t.Characters[0];
        var hand = new TutorialFarmhand();
        t.Litter.Add(hand);
        hand.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        t.Record.PetClaimed = true;
        t.Record.StationsDone.UnionWith([TutorialFarm.PestsKey, TutorialFarm.FollowKey, TutorialFarm.GuardKey, TutorialFarm.CoreStation]); // cc-P726: on the healing lesson

        for (var i = 0; i < TutorialFarm.BandageIssues + 2; i++)
        {
            TutorialFarm.BeginHealing(pm);
            if (pm.Backpack.FindItemByType<Bandage>() is { } stack)
            {
                pm.BankBox.DropItem(stack);
            }
        }

        Assert.Equal(TutorialFarm.BandageIssues, t.Record.BandagesIssued);
        Assert.Equal(TutorialFarm.BandageGift.NoneLeft, TutorialFarm.GiveBandages(pm));
        Assert.Equal(TutorialFarm.BandageIssues, RoundTrip(t.Account).BandagesIssued);
        pm.BankBox.Delete();
    }

    // ---------------------------------------------------------------- 2

    public static IEnumerable<object[]> Orphans()
    {
        foreach (var state in new[] { TutorialState.InCore, TutorialState.InWings, TutorialState.Remedial })
        {
            foreach (var how in new[] { "no serial", "deleted", "other account" })
            {
                foreach (var entry in new[] { "login", "Rook" })
                {
                    yield return new object[] { state, how, entry };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Orphans))]
    public void AnActiveRecordWhoseCharacterIsGoneCarriesOnWithTheNextCharacter(TutorialState state, string how, string entry)
    {
        using var t = new Trainees();
        var first = t.Characters[0];
        var record = t.Record;
        var hand = new TutorialFarmhand();
        t.Litter.Add(hand);
        hand.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        var rook = new TutorialRecruiter();
        t.Litter.Add(rook);
        rook.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, Spot.Z), Map.Trammel);

        // Progress to keep: a pet taken at the farm, pests cleared, a bandage stack issued, and the state under test.
        record.PetClaimed = true;
        record.StationsDone.Add(TutorialFarm.PestsKey);
        record.BandagesIssued = 1;
        if (state == TutorialState.InWings)
        {
            foreach (var s in TutorialStations.Core)
            {
                record.StationsDone.Add(s.Key);
            }
        }

        if (state == TutorialState.Remedial)
        {
            record.WasSkipped = true;
        }

        record.State = state;
        var stations = record.StationsDone.OrderBy(k => k).ToList();
        var startedAt = record.StartedAt;

        var other = new Account($"p613o{Guid.NewGuid():N}"[..16], "p613-test-only");
        try
        {
            switch (how)
            {
                case "no serial":
                    record.CharacterSerial = 0;
                    break;
                case "deleted":
                    first.Delete();
                    break;
                default:
                    t.Account[0] = null!;
                    other[0] = first;
                    break;
            }

            var next = t.Add();
            Assert.False(ClusterFTutorial.IsParticipant(next)); // not yet: only through the entry

            if (entry == "login")
            {
                ClusterFTutorial.OnLogin(next);
            }
            else
            {
                rook.OnDoubleClick(next);
            }

            Assert.True(ClusterFTutorial.IsParticipant(next), $"{state}, {how}, at {entry}: the next character carries on");
            Assert.Equal((uint)next.Serial, record.CharacterSerial);
            Assert.Equal(state, record.State);
            Assert.Equal(stations, record.StationsDone.OrderBy(k => k).ToList());
            Assert.Equal(startedAt, record.StartedAt);
            Assert.True(record.PetClaimed);
            Assert.Equal(1, record.BandagesIssued);
            Assert.Equal(state == TutorialState.Remedial, record.WasSkipped);
            Assert.False(new TutorialRecruiterGump(next).ReadOnly);

            // The farm talks to them where their progress left off (the bandage step), and a lesson finishes for them.
            Assert.Equal(3, TutorialFarm.Talk(next, hand));
            var core = TutorialStations.Core.First(s => !record.StationsDone.Contains(s.Key) || state == TutorialState.InWings);
            if (state != TutorialState.InWings)
            {
                ClusterFTutorial.CompleteStation(next, core.Key);
                Assert.Contains(core.Key, record.StationsDone);
            }

            var copy = RoundTrip(t.Account);
            Assert.Equal((uint)next.Serial, copy.CharacterSerial);
            Assert.Equal(record.State, copy.State);
            Assert.True(copy.PetClaimed);

            // A third character cannot take it over while the second one stands.
            var third = t.Add();
            ClusterFTutorial.OnLogin(third);
            rook.OnDoubleClick(third);
            Assert.Equal((uint)next.Serial, record.CharacterSerial);
            Assert.False(ClusterFTutorial.IsParticipant(third));
            Assert.True(new TutorialRecruiterGump(third).ReadOnly);
        }
        finally
        {
            if (how == "other account")
            {
                other[0] = null!;
                first.Delete();
            }

            Accounts.Remove(other);
        }
    }

    [Fact]
    public void ALivingRecordedCharacterKeepsItsTutorial()
    {
        using var t = new Trainees();
        var first = t.Characters[0];
        var rook = new TutorialRecruiter();
        t.Litter.Add(rook);
        rook.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, Spot.Z), Map.Trammel);

        var alt = t.Add();
        ClusterFTutorial.OnLogin(alt);
        rook.OnDoubleClick(alt);
        Assert.Equal((uint)first.Serial, t.Record.CharacterSerial);
        Assert.True(ClusterFTutorial.IsParticipant(first));
        Assert.False(ClusterFTutorial.IsParticipant(alt));
        var page = new TutorialRecruiterGump(alt);
        Assert.True(page.ReadOnly);
    }
}
