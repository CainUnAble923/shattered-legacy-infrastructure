// cc-P363: quest markers over NPCs (customizations/QuestMarkers.cs), keys (ShardKeys.cs) and the coloured moongates
// (ShardMoongates.cs). Notes: shard-migration notes/cc-P363-quest-markers-keys-gates.md.
//
// Facts:
//   1. Rook shows "!" to a tutorial participant whose next lesson waits, and "?" once the core is done; a player near
//      the same NPC who is not in the tutorial is sent nothing, and the effect goes only to its own player's client
//      (a fixed effect on Rook with the marker's art). The League Registrar shows "!" to an unregistered player. With
//      clusterf.questMarkers off, nothing is sent.
//   2. A key opens only its lock: a door refuses no key, another lock's key and a stock key blank, and opens for its
//      own key (staff pass); a consuming door takes the key. A keyed-tier loot box stays shut without a Vault key and
//      opens with one, which it takes; the tier below is keyless.
//   3. Each gate colour places with its own art (never a stock moongate ID) and teleports to its target; a keyed gate
//      lets only its key's carrier through; the guild hall hub sends a traveller to a hall's Travel point.

#nullable enable

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.QuestMarkers;
using Server.Items;
using Server.LootBoxes;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class QuestMarkersKeysGatesVerification
{
    private static readonly Point3D Spot = new(1520, 1520, 0);

    public QuestMarkersKeysGatesVerification()
    {
        ShardTestHost.EnsureAccounts();
        ClusterFTutorial.Enabled = true;
        LootTable.Current = LootTable.Defaults;
        FanfareLines.Current = FanfareLines.Defaults;
        ClusterFLootBoxes.LoopProvider = _ => 1;
        ClusterFFanfare.FlushAll();
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p363{Guid.NewGuid():N}"[..16], "p363-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(Point3D at)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Keyholder" };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(at, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Dispose()
        {
            ClusterFFanfare.FlushAll();
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Ns.Dispose();
            Accounts.Remove(Account);
        }
    }

    // The 0xC0 effect packets sent to this client since `from`: (source serial, item ID).
    private static List<(uint Source, int ItemId)> EffectsSince(NetState ns, int from)
    {
        var wire = ns.SendBuffer.GetReadSpan()[from..];
        var found = new List<(uint, int)>();

        var len = OutgoingEffectPackets.HuedEffectLength;

        for (var i = 0; i + len <= wire.Length; i += len)
        {
            if (wire[i] != 0xC0)
            {
                break;
            }

            found.Add((BinaryPrimitives.ReadUInt32BigEndian(wire[(i + 2)..]), BinaryPrimitives.ReadUInt16BigEndian(wire[(i + 10)..])));
        }

        return found;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void AMarkerShowsForAnOfferedQuestAndSwitchesToReadyWhenItIs()
    {
        var rook = new TutorialRecruiter();
        var registrar = new LeagueRegistrar();
        using var trainee = new Player(Spot);
        using var bystander = new Player(new Point3D(Spot.X + 2, Spot.Y, 0));

        try
        {
            rook.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y + 1, 0), Map.Trammel);
            registrar.MoveToWorld(new Point3D(Spot.X - 2, Spot.Y, 0), Map.Trammel);
            Assert.True(ClusterFTutorial.TryEnter(trainee.Pm));
            trainee.Pm.MoveToWorld(Spot, Map.Trammel);
            Assert.True(ClusterFAccountPersistence.GetOrCreate(bystander.Account).Tutorial.State == TutorialState.NotStarted);

            // "!": the trainee's next lesson waits with Rook.
            var sent = new List<(PlayerMobile, Mobile, int)>();
            void Record(PlayerMobile pm, Mobile npc, int id) => sent.Add((pm, npc, id));
            QuestMarkers.Sent += Record;

            try
            {
                var before = trainee.Ns.SendBuffer.GetReadSpan().Length;
                var bystanderBefore = bystander.Ns.SendBuffer.GetReadSpan().Length;
                var shown = QuestMarkers.ShowTo(trainee.Pm);

                Assert.Contains((rook, ShardItemArt.QuestOfferedMarker), shown.Select(s => ((Mobile)s.Npc, s.ItemId)));
                Assert.Equal(QuestMarkerState.Offered, QuestMarkers.StateFor(trainee.Pm, rook));
                Assert.Contains(((uint)rook.Serial, 0xB600), EffectsSince(trainee.Ns, before));
                Assert.Equal(bystanderBefore, bystander.Ns.SendBuffer.GetReadSpan().Length); // nothing went to the bystander

                // The League Registrar shows "!" to an account not registered with the League.
                Assert.Contains((registrar, ShardItemArt.QuestOfferedMarker), shown.Select(s => ((Mobile)s.Npc, s.ItemId)));

                // The bystander, who is not in the tutorial, sees nothing over Rook.
                var theirs = QuestMarkers.ShowTo(bystander.Pm);
                Assert.DoesNotContain(theirs, s => s.Npc == rook);
                Assert.Equal(QuestMarkerState.None, QuestMarkers.StateFor(bystander.Pm, rook));

                // "?": every core lesson done, Rook has the graduation to hand over.
                foreach (var s in TutorialStations.Core)
                {
                    ClusterFTutorial.RecordOf(trainee.Pm)!.StationsDone.Add(s.Key);
                }

                before = trainee.Ns.SendBuffer.GetReadSpan().Length;
                shown = QuestMarkers.ShowTo(trainee.Pm);
                Assert.Contains((rook, ShardItemArt.QuestReadyMarker), shown.Select(s => ((Mobile)s.Npc, s.ItemId)));
                Assert.DoesNotContain((rook, ShardItemArt.QuestOfferedMarker), shown.Select(s => ((Mobile)s.Npc, s.ItemId)));
                Assert.Contains(((uint)rook.Serial, 0xB603), EffectsSince(trainee.Ns, before));
                Assert.Contains(sent, s => s.Item1 == trainee.Pm && s.Item2 == rook && s.Item3 == ShardItemArt.QuestReadyMarker);
                Assert.DoesNotContain(sent, s => s.Item1 == bystander.Pm && s.Item2 == rook);

                // The setting off: nothing.
                ServerConfiguration.SetSetting(QuestMarkers.SettingKey, false);
                try
                {
                    Assert.Empty(QuestMarkers.ShowTo(trainee.Pm));
                }
                finally
                {
                    ServerConfiguration.SetSetting(QuestMarkers.SettingKey, true);
                }
            }
            finally
            {
                QuestMarkers.Sent -= Record;
            }
        }
        finally
        {
            rook.Delete();
            registrar.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AKeyOpensOnlyItsLock()
    {
        using var p = new Player(Spot);
        var door = ShardLockedDoor.BossRoom(DoorFacing.WestCW, "Rootdeep");
        door.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, 0), Map.Trammel);
        var consuming = new ShardLockedDoor(DoorFacing.WestCW, "test.once", "a one-use key", consumeKey: true);
        consuming.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, 0), Map.Trammel);
        var litter = new List<Item> { door, consuming };

        try
        {
            Assert.Equal("boss.rootdeep", door.LockTag);

            // No key, another lock's key, a stock key blank (KeyValue 0, as the door's own stock value): shut.
            door.Use(p.Pm);
            Assert.False(door.Open);

            var other = ShardKeys.BossRoomKey("Wrong Dungeon");
            var blank = new Key(KeyType.Iron);
            p.Pm.Backpack.DropItem(other);
            p.Pm.Backpack.DropItem(blank);
            litter.Add(other);
            litter.Add(blank);
            Assert.False(other.Opens(door));
            door.Use(p.Pm);
            Assert.False(door.Open);

            // Its own key: open, still locked behind (the key is kept).
            var key = ShardKeys.BossRoomKey("Rootdeep");
            p.Pm.Backpack.DropItem(key);
            litter.Add(key);
            Assert.True(key.Opens(door));
            Assert.False(key.Opens(consuming));
            Assert.InRange(key.ItemID, ShardItemArt.FirstKey, ShardItemArt.FirstKey + ShardItemArt.KeyCount - 1);
            door.Use(p.Pm);
            Assert.True(door.Open);
            Assert.True(door.Locked);
            Assert.False(key.Deleted);

            // A consuming door takes its key; a second try finds none.
            var once = new ShardKey("test.once", "a one-use key");
            p.Pm.Backpack.DropItem(once);
            consuming.Use(p.Pm);
            Assert.True(consuming.Open);
            Assert.True(once.Deleted);

            // Staff pass without a key.
            var shut = ShardLockedDoor.BossRoom(DoorFacing.WestCW, "Elsewhere");
            shut.MoveToWorld(new Point3D(Spot.X - 1, Spot.Y, 0), Map.Trammel);
            litter.Add(shut);
            p.Pm.AccessLevel = AccessLevel.GameMaster;
            shut.Use(p.Pm);
            Assert.True(shut.Open);
            p.Pm.AccessLevel = AccessLevel.Player;

            // The keyed loot box tier: shut without a Vault key, open with one, which it takes. The tier below is keyless.
            Assert.Equal(6, ShardKeys.KeyTier);
            var region = new GuardedRegion("p363 town", Map.Trammel, 50, new Rectangle2D(Spot.X - 10, Spot.Y - 10, 20, 20));
            region.Register();
            try
            {
                p.Pm.MoveToWorld(Spot, Map.Trammel);
                var five = ClusterFLootBoxes.MakeBox(p.Pm, BoxTheme.Miner, 5, null, "tier five", null);
                litter.Add(five);
                Assert.IsType<LootBox>(five);

                var box = (ShardLockedLootBox)ClusterFLootBoxes.MakeBox(p.Pm, BoxTheme.Miner, 6, null, "tier six", null);
                p.Pm.Backpack.DropItem(box);
                box.OnDoubleClick(p.Pm); // the boss key in the pack does not fit
                Assert.False(box.Unlocked);
                Assert.False(box.Deleted);

                var vault = ShardKeys.VaultKey();
                p.Pm.Backpack.DropItem(vault);
                box.OnDoubleClick(p.Pm);
                ClusterFFanfare.FlushAll();
                Assert.True(box.Deleted || box.Unlocked);
                Assert.True(vault.Deleted);
                if (!box.Deleted)
                {
                    litter.Add(box);
                }

                // A boss kill's Vault key: below the chance, one; at it, none.
                var dropped = ShardKeys.OnBossKilled(p.Pm, 0.0);
                Assert.NotNull(dropped);
                Assert.True(dropped!.IsChildOf(p.Pm.Backpack));
                litter.Add(dropped);
                Assert.Null(ShardKeys.OnBossKilled(p.Pm, ShardKeys.VaultKeyChance));
            }
            finally
            {
                region.Unregister();
            }
        }
        finally
        {
            foreach (var i in litter)
            {
                i.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void EachGateColourPlacesAndTeleports()
    {
        using var p = new Player(Spot);
        var litter = new List<Item>();
        var stock = new[] { 0xF6C, 0xDDA, 0x1FD4 };

        try
        {
            foreach (var color in new[] { ShardGateColor.Blue, ShardGateColor.Red, ShardGateColor.Black, ShardGateColor.White })
            {
                var to = new Point3D(1600 + 4 * (int)color, 1600, 0);
                var gate = new ShardMoongate(color, to, Map.Trammel);
                litter.Add(gate);
                gate.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, 0), Map.Trammel);

                Assert.Equal(ShardItemArt.GateItemId(color), gate.ItemID);
                Assert.InRange(gate.ItemID, ShardItemArt.FirstGate, ShardItemArt.FirstGate + 4 * ShardItemArt.GateFrames - 1);
                Assert.DoesNotContain(gate.ItemID, stock);
                Assert.Equal($"a {color.ToString().ToLowerInvariant()} moongate", gate.DefaultName);

                p.Pm.MoveToWorld(Spot, Map.Trammel);
                gate.UseGate(p.Pm);
                Assert.Equal(to, p.Pm.Location);
                Assert.Equal(Map.Trammel, p.Pm.Map);
            }

            // Staff recolour: the art follows.
            var recolour = litter.OfType<ShardMoongate>().First();
            recolour.Color = ShardGateColor.White;
            Assert.Equal(ShardItemArt.GateItemId(ShardGateColor.White), recolour.ItemID);

            // A League trial gate lets only its chapter's key through.
            var trial = ShardGates.LeagueTrialGate(2, new Point3D(1650, 1600, 0), Map.Trammel);
            litter.Add(trial);
            Assert.False(trial.MayPass(p.Pm));
            var wrong = ShardKeys.LeagueTrialGateKey(3);
            p.Pm.Backpack.DropItem(wrong);
            litter.Add(wrong);
            Assert.False(trial.MayPass(p.Pm));
            var right = ShardKeys.LeagueTrialGateKey(2);
            p.Pm.Backpack.DropItem(right);
            litter.Add(right);
            Assert.True(trial.MayPass(p.Pm));

            // The guild hall hub: a trip to another hall's Travel point, on that hall's facet.
            var hq = ShardGates.Hall(ShardGates.HeadquartersKey)!;
            var hub = new ShardMoongate(ShardGateColor.White) { Network = ShardMoongate.GuildHallNetwork };
            litter.Add(hub);
            hub.MoveToWorld(hq.Travel, Map.Maps[hq.MapIndex]);
            var halls = ShardGates.Destinations(hub);
            Assert.DoesNotContain(halls, h => h.Key == ShardGates.HeadquartersKey);
            var dest = halls.First(h => h.MapIndex == 1);
            p.Pm.MoveToWorld(hub.Location, hub.Map);
            hub.TravelTo(p.Pm, dest.Travel, Map.Maps[dest.MapIndex]);
            Assert.Equal(dest.Travel, p.Pm.Location);
            Assert.Equal(Point3D.Zero, hub.Target); // the hub keeps no target of its own
        }
        finally
        {
            foreach (var i in litter)
            {
                i.Delete();
            }
        }
    }
}
