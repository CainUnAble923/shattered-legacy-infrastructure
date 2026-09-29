// P7: the drops pinned already wrote and left switched off.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh.
//
// Pinned 7c9215d97 ships Miasma's and Lady Lissith's OnDeath inside a comment block marked "TODO: uncomment once
// added", waiting on armour-set types it never shipped. Ours declare them, and Miasma-pinned-waiting-drop.patch and
// LadyLissith-pinned-waiting-drop.patch remove the comment markers. If a pinned bump moves either block, the patch
// stops applying and the build fails. If a patch stops reaching the build, OnDeath is the base one again, nothing is
// dropped, and the facts here go red.
//
// The rolls are 2.5% and then 1 in 16, too rare to meet through real kills, so the facts script BuiltInRng.Generator
// (internal set, visible to UOContent.Tests) the way pinned's own Server.Tests PredictableRandom does. The creature is
// built BEFORE the script goes in, so its stat ranges come from the real generator. The scripted values reach every
// Utility call inside OnDeath, including base.OnDeath and the CreatureDeathEvent handlers. None of those drops
// anything into the container the facts inspect.
// See shard-migration/notes/cc-P7-pinned-waiting-drops.md.

using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PinnedWaitingDropsVerification
{
    private readonly ITestOutputHelper _out;

    public PinnedWaitingDropsVerification(ITestOutputHelper output) => _out = output;

    // Pinned Miasma.cs:54-69, in case order. The index is the Utility.Random(16) result that picks it.
    private static readonly Type[] MiasmaDrops =
    [
        typeof(MyrmidonGloves), typeof(MyrmidonGorget), typeof(MyrmidonLegs), typeof(MyrmidonArms),
        typeof(PaladinArms), typeof(PaladinGorget), typeof(LeafweaveLegs), typeof(DeathChest),
        typeof(DeathGloves), typeof(DeathLegs), typeof(GreymistGloves), typeof(GreymistArms),
        typeof(AssassinChest), typeof(AssassinArms), typeof(HunterGloves), typeof(HunterLegs)
    ];

    private sealed class ScriptedRandom(double nextDouble, int next) : System.Random
    {
        public override double NextDouble() => nextDouble;
        public override int Next() => next;
        public override int Next(int maxValue) => Math.Clamp(next, 0, maxValue - 1);
        public override int Next(int minValue, int maxValue) => Math.Clamp(minValue + next, minValue, maxValue - 1);
        public override long NextInt64(long maxValue) => Math.Clamp(next, 0, maxValue - 1);
        public override long NextInt64(long minValue, long maxValue) => Math.Clamp(minValue + next, minValue, maxValue - 1);
    }

    // Kill `creature` through its own OnDeath into a fresh container with the generator scripted, and hand back what
    // landed in the container. The container is what the uncommented c.DropItem lines write to.
    private static Container DieInto(BaseCreature creature, double nextDouble, int next)
    {
        var c = new Backpack();
        var original = BuiltInRng.Generator;
        BuiltInRng.Generator = new ScriptedRandom(nextDouble, next);
        try
        {
            creature.OnDeath(c);
        }
        finally
        {
            BuiltInRng.Generator = original;
        }

        return c;
    }

    [Fact]
    public void EveryDropTypeIsOursConstructibleAndResolvesByItsName()
    {
        // Two parts of the D35 lesson. Each name the uncommented code spells must be the type we ship, not a near-miss
        // twin, and it must construct.
        Type[] all = [..MiasmaDrops, typeof(GreymistChest)];
        foreach (var t in all)
        {
            Assert.Same(t, AssemblyHandler.FindTypeByName(t.Name));
            Assert.Same(t, AssemblyHandler.FindTypeByFullName($"Server.Items.{t.Name}", false));
            var item = Assert.IsAssignableFrom<Item>(Activator.CreateInstance(t));
            Assert.False(item.Deleted);
            item.Delete();
        }

        // The spawn files place both creatures by these names (Labyrinth.json:15, TwistedWeald.json:15).
        Assert.Same(typeof(Miasma), AssemblyHandler.FindTypeByName("Miasma"));
        Assert.Same(typeof(LadyLissith), AssemblyHandler.FindTypeByName("LadyLissith"));
    }

    [Fact]
    public void MiasmaDropsEachOfPinnedsSixteenIntoTheContainer()
    {
        for (var pick = 0; pick < MiasmaDrops.Length; pick++)
        {
            var miasma = new Miasma();
            miasma.MoveToWorld(new Point3D(1000, 1000, 0), Map.Malas);

            var c = DieInto(miasma, 0.0, pick);
            var dropped = Assert.Single(c.Items);
            Assert.IsType(MiasmaDrops[pick], dropped);
            _out.WriteLine($"Utility.Random(16) = {pick,2} -> {dropped.GetType().Name}");
            c.Delete();
        }
    }

    [Fact]
    public void MiasmaDropsNothingOnARollAtItsChance()
    {
        // `Utility.RandomDouble() < 0.025`: exactly 0.025 misses. This pins pinned's chance, not one we chose.
        var miasma = new Miasma();
        miasma.MoveToWorld(new Point3D(1000, 1000, 0), Map.Malas);
        var c = DieInto(miasma, 0.025, 0);
        Assert.Empty(c.Items);
        c.Delete();
    }

    [Fact]
    public void LadyLissithDropsGreymistChestAndNothingElse()
    {
        var lissith = new LadyLissith();
        lissith.MoveToWorld(new Point3D(1000, 1000, 0), Map.Ilshenar);

        // Every roll passes, so the only thing between the silk and parrot lines and the container is their comment.
        var c = DieInto(lissith, 0.0, 0);
        var dropped = Assert.Single(c.Items);
        Assert.IsType<GreymistChest>(dropped);
        c.Delete();

        var again = new LadyLissith();
        again.MoveToWorld(new Point3D(1000, 1000, 0), Map.Ilshenar);
        c = DieInto(again, 0.025, 0);
        Assert.Empty(c.Items);
        c.Delete();
    }
}
