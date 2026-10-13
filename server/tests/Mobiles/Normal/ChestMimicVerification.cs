// ChestMimicVerification.cs
//
// cc-P50, F-32: the chest mimic as a monster, drawn by item art (ChestMimicShell) over a creature whose body draws
// nothing. Notes in shard-migration notes/cc-P50-chest-mimic-monster.md.
//
// Facts:
//   1. It constructs at each level 1 to 6 with that level's row of ChestMimic.Levels (level 4 is OSI's Mimic), and
//      refuses any other level.
//   2. It serializes and deserializes with its level, facing, look and chest; its look round-trips with its mimic.
//   3. A chest becomes a mimic on the same tile and facing, in one step: the chest is internal, the look stands on
//      the chest's tile showing the chest's own graphic, then plays the waking frames.
//   4. Each state shows the registered art IDs, in both facings; the lid shut shows the stock chest and takes no damage.
//   5. It never moves: a step is refused and it stays on its tile.
//   6. Death leaves only the corpse, drawn as the dead mimic, holding the chest's contents; the look and the chest are
//      gone. A staff delete leaves nothing of the mimic and puts the chest back where it stood.
//   7. A target aimed at its look lands on it while it lives, and nothing in it references PetMimic.
//   8. The server's tiledata for the mimic IDs is ours (ChestMimicArt.Records), so its look blocks.

using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ChestMimicVerification
{
    private readonly ITestOutputHelper _out;

    public ChestMimicVerification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D Spot = new(1000, 1000, 0);

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(typeof(T), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    private static WoodenChest PlacedChest(int itemId, Point3D at)
    {
        var chest = new WoodenChest { ItemID = itemId };
        chest.MoveToWorld(at, Map.TerMur);
        return chest;
    }

    [Fact]
    public void EachLevelHasItsRowOfStatsAndLevelFourIsOsisMimic()
    {
        for (var level = 1; level <= 6; level++)
        {
            var row = ChestMimic.LevelRow(level);
            var m = new ChestMimic(level);
            _out.WriteLine($"L{level}: hits {m.HitsMax} str {m.RawStr} dex {m.RawDex} int {m.RawInt} dmg {m.DamageMin}-{m.DamageMax} " +
                           $"magery {m.Skills.Magery.Base} resist {m.Skills.MagicResist.Base} fame {m.Fame}");

            Assert.Equal(level, m.ChestLevel);
            Assert.InRange(m.HitsMax, row.HitsMin, row.HitsMax);
            Assert.Equal(row.Str, m.RawStr);
            Assert.Equal(row.Dex, m.RawDex);
            Assert.Equal(row.Int, m.RawInt);
            Assert.Equal(row.DamageMin, m.DamageMin);
            Assert.Equal(row.DamageMax, m.DamageMax);
            Assert.Equal(row.Physical, m.PhysicalResistanceSeed);
            Assert.Equal(row.Fire, m.FireResistSeed);
            Assert.Equal(row.Cold, m.ColdResistSeed);
            Assert.Equal(row.Poison, m.PoisonResistSeed);
            Assert.Equal(row.Energy, m.EnergyResistSeed);
            Assert.Equal(row.Magery, m.Skills.Magery.Base);
            Assert.Equal(row.EvalInt, m.Skills.EvalInt.Base);
            Assert.Equal(row.MagicResist, m.Skills.MagicResist.Base);
            Assert.Equal(row.Fame, m.Fame);
            Assert.Equal(-row.Fame, m.Karma);
            Assert.Equal(20, m.PhysicalDamage);
            Assert.Equal(20, m.EnergyDamage);
            Assert.Equal(AIType.AI_Mage, m.AI);
            Assert.Equal(ChestMimic.EmptyBody, (int)m.Body);
            Assert.False(m.Tamable);
            m.Delete();
        }

        // Level 4 is OSI's Mimic (ServUO Scripts/Mobiles/Normal/Mimic.cs:16-41).
        var osi = new ChestMimic();
        Assert.Equal(4, osi.ChestLevel);
        Assert.Equal(543, osi.HitsMax);
        Assert.Equal(281, osi.RawStr);
        Assert.Equal(107.5, osi.Skills.Magery.Base);
        Assert.Equal(126.5, osi.Skills.MagicResist.Base);
        osi.Delete();

        Assert.Throws<ArgumentOutOfRangeException>(() => new ChestMimic(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChestMimic(7));
    }

    [Fact]
    public void ItRoundTripsWithItsLevelFacingLookAndChest()
    {
        var chest = PlacedChest(0xE42, Spot);
        var m = ChestMimic.Spring(chest, 5);
        Assert.NotNull(m);
        var shell = m.Shell;

        var copy = RoundTrip(m);
        _out.WriteLine($"copy: level {copy.ChestLevel} facing {copy.LookFacing} shell {copy.Shell?.Serial} chest {copy.SourceChest?.Serial}");
        Assert.Equal(5, copy.ChestLevel);
        Assert.Equal(MimicFacing.WestEast, copy.LookFacing);
        Assert.Same(shell, copy.Shell);
        Assert.Same(chest, copy.SourceChest);
        Assert.Equal(m.HitsMax, copy.HitsMax);

        var shellCopy = RoundTrip(shell);
        Assert.Same(m, shellCopy.Mimic);
        Assert.Equal(shell.ItemID, shellCopy.ItemID);

        m.Delete();
        Assert.True(shell.Deleted);
    }

    [Fact]
    public void AChestBecomesAMimicOnItsTileAndFacingInOneStep()
    {
        foreach (var (itemId, facing) in new[] { (0xE43, MimicFacing.NorthSouth), (0xE42, MimicFacing.WestEast) })
        {
            var at = new Point3D(Spot.X + 4, Spot.Y, Spot.Z);
            var chest = PlacedChest(itemId, at);
            chest.Hue = 0x21;

            var m = ChestMimic.Spring(chest, 3);
            Assert.NotNull(m);
            var shell = m.Shell;

            Assert.Equal(at, m.Location);
            Assert.Same(Map.TerMur, m.Map);
            Assert.Equal(facing, m.LookFacing);
            Assert.Same(Map.Internal, chest.Map);
            Assert.False(chest.Deleted);
            Assert.Equal(at, shell.Location);
            Assert.Same(Map.TerMur, shell.Map);

            // Spring wakes it at once: the first waking frame shows, hue cleared (the chest's look was its hue).
            Assert.Equal(MimicState.Waking, m.State);
            Assert.Equal(ChestMimicArt.Id(facing, ChestMimicArt.Teeth), shell.ItemID);
            Assert.Equal(0, shell.Hue);

            m.Delete();
        }

        // Not on the ground of a real map: refused, nothing made.
        var held = new WoodenChest();
        Assert.Null(ChestMimic.Spring(held, 1));
        held.Delete();
    }

    [Fact]
    public void EachStateShowsItsRegisteredArtAndTheShutLidTakesNoDamage()
    {
        foreach (var facing in new[] { MimicFacing.NorthSouth, MimicFacing.WestEast })
        {
            var b = facing == MimicFacing.NorthSouth ? 0x3C70 : 0x3C79;
            var closed = facing == MimicFacing.NorthSouth ? 0xE43 : 0xE42;

            var m = new ChestMimic(2, facing);
            m.MoveToWorld(Spot, Map.TerMur);
            var shell = m.Shell;
            Assert.NotNull(shell);
            _out.WriteLine($"{facing}: look {m.LookFacing}, idle 0x{m.IdleId:X4}, shell 0x{shell.ItemID:X4} serial {shell.Serial} state {m.State}");

            Assert.Equal(new[] { b + 0, b + 1 }, m.SequenceFor(MimicState.Waking));
            Assert.Equal(new[] { b + 2, b + 3, b + 4, b + 3, b + 1 }, m.SequenceFor(MimicState.Bite));
            Assert.Equal(new[] { b + 0, closed, b + 0 }, m.SequenceFor(MimicState.Hurt));

            Assert.Equal(MimicState.Idle, m.State);
            Assert.Equal(b + 5, shell.ItemID);

            m.PlayBite();
            Assert.Equal(MimicState.Bite, m.State);
            Assert.Equal(b + 2, shell.ItemID);
            m.SkipToIdle();
            Assert.Equal(b + 5, shell.ItemID);

            m.PlayHurt();
            Assert.Equal(b + 0, shell.ItemID);
            m.SkipToIdle();

            m.ShutLid();
            Assert.True(m.LidShut);
            Assert.Equal(closed, shell.ItemID);
            Assert.False(m.CanBeDamaged());
            var hits = m.Hits;
            m.Damage(50, null);
            Assert.Equal(hits, m.Hits);

            // A one-shot never plays over the shut lid.
            m.PlayBite();
            Assert.Equal(closed, shell.ItemID);

            m.OpenLid();
            Assert.Equal(MimicState.Waking, m.State);
            Assert.True(m.CanBeDamaged());

            m.Delete();
        }
    }

    [Fact]
    public void ItNeverMovesFromItsTile()
    {
        var m = new ChestMimic(1);
        m.MoveToWorld(Spot, Map.TerMur);

        Assert.True(m.DisallowAllMoves);
        Assert.False(m.CanFlee);
        // Mobile.Move turns the creature when it faces another way and returns true without stepping
        // (pinned Mobile.cs:4340); asked twice, the second is a real step, which is refused. So: never off its tile.
        foreach (Direction d in new[] { Direction.North, Direction.East, Direction.South, Direction.West, Direction.Running | Direction.Up })
        {
            m.Move(d);
            Assert.False(m.Move(d));
            Assert.Equal(Spot, m.Location);
        }

        Assert.Equal(Spot, m.Location);
        Assert.Equal(Spot, m.Home);
        Assert.Equal(0, m.RangeHome);
        Assert.Equal(Spot, m.Shell.Location);
        m.Delete();
    }

    [Fact]
    public void DeathLeavesOnlyTheDeadMimicCorpseWithTheChestsContentsAndADeleteRestoresTheChest()
    {
        var chest = PlacedChest(0xE43, Spot);
        var gold = new Gold(123);
        chest.DropItem(gold);

        var m = ChestMimic.Spring(chest, 1);
        var shell = m.Shell;
        m.Kill();

        var corpse = Assert.IsAssignableFrom<Corpse>(m.Corpse);
        _out.WriteLine($"corpse 0x{corpse.ItemID:X4} at {corpse.Location}; mimic deleted {m.Deleted}; shell deleted {shell.Deleted}; chest deleted {chest.Deleted}");
        Assert.Contains(corpse.ItemID, new[] { 0x3C77, 0x3C78 });
        Assert.Equal(Spot, corpse.Location);
        Assert.Same(corpse, gold.Parent);
        Assert.True(shell.Deleted);

        m.Delete(); // whether or not the death already deleted it
        Assert.True(chest.Deleted);
        Assert.DoesNotContain(World.Items.Values.OfType<ChestMimicShell>(), s => s.Mimic == m && !s.Deleted);
        corpse.Delete();

        // A staff delete: the chest comes back where it stood, nothing of the mimic stays.
        var at = new Point3D(Spot.X + 2, Spot.Y, Spot.Z);
        var chest2 = PlacedChest(0xE42, at);
        var m2 = ChestMimic.Spring(chest2, 2);
        var shell2 = m2.Shell;
        m2.Delete();
        Assert.True(shell2.Deleted);
        Assert.False(chest2.Deleted);
        Assert.Equal(at, chest2.Location);
        Assert.Same(Map.TerMur, chest2.Map);
        chest2.Delete();

        // Deleting its look (staff targeting what they see) deletes the mimic too.
        var m3 = new ChestMimic(1);
        m3.MoveToWorld(Spot, Map.TerMur);
        m3.Shell.Delete();
        Assert.True(m3.Deleted);
    }

    [Fact]
    public void ATargetOnItsLookLandsOnItAndNothingReferencesPetMimic()
    {
        var m = new ChestMimic(1);
        m.MoveToWorld(Spot, Map.TerMur);
        ITargetRedirect look = m.Shell;
        Assert.Same(m, look.RedirectTarget(null, null));

        var petMimics = World.Items.Values.Count(i => i.GetType().Name == "PetMimic");
        foreach (var t in new[] { typeof(ChestMimic), typeof(ChestMimicShell), typeof(ChestMimicArt) })
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.DoesNotContain(t.GetFields(all), f => f.FieldType.Name.Contains("PetMimic"));
            Assert.DoesNotContain(t.GetProperties(all), p => p.PropertyType.Name.Contains("PetMimic"));
            Assert.DoesNotContain(t.GetMethods(all), x => x.ReturnType.Name.Contains("PetMimic") ||
                                                          x.GetParameters().Any(p => p.ParameterType.Name.Contains("PetMimic")));
        }

        var shell = m.Shell;
        m.Kill();
        Assert.Null(look.RedirectTarget(null, null));
        m.Corpse?.Delete();
        m.Delete();
        Assert.True(shell.Deleted);
        Assert.Equal(petMimics, World.Items.Values.Count(i => i.GetType().Name == "PetMimic"));
    }

    [Fact]
    public void TheServersTiledataForTheMimicIsOurs()
    {
        ShatteredLegacyArt.Apply();

        Assert.Equal(16, ChestMimicArt.Records.Length);
        foreach (var r in ChestMimicArt.Records)
        {
            Assert.InRange(r.Id, ChestMimicArt.FirstId, ChestMimicArt.LastId);
            var d = TileData.ItemTable[r.Id];
            Assert.Equal(r.Name, d.Name);
            Assert.Equal(r.Flags, d.Flags);
            Assert.Equal(r.Height, d.Height);
        }

        // Stills and loop bases block, as the stock chest does; the dead tiles do not; loop frames have no record.
        Assert.True(TileData.ItemTable[0x3C75].Impassable);
        Assert.True(TileData.ItemTable[0x3C75][TileFlag.Animation]);
        Assert.False(TileData.ItemTable[0x3C77].Impassable);
        Assert.DoesNotContain(ChestMimicArt.Records, r => r.Id is 0x3C76 or 0x3C7F);

        Assert.Equal(MimicFacing.WestEast, ChestMimicArt.FacingOf(0xE42));
        Assert.Equal(MimicFacing.NorthSouth, ChestMimicArt.FacingOf(0xE43));
        Assert.Equal(MimicFacing.WestEast, ChestMimicArt.FacingOf(0xE7C));
        Assert.Equal(MimicFacing.NorthSouth, ChestMimicArt.FacingOf(0x9AB));
    }
}
