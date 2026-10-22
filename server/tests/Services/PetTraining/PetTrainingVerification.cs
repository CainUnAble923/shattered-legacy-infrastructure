// cc-P130: Pet Training part 2, training itself (customizations/Services/Pet Training/, five more upstream patches).
// Decisions 2a (the training state in PetTrainingStore, its own save file) and 4 (evo pets and pack mules excluded by
// type) of 2026-10-15.
//
// What these facts pin, in the brief's words:
//  - the tables: ServUO's 137 definition rows by name (the ones this shard lacks are listed, not invented) and its 102
//    training points; a stock Dragon builds with four slots (ServUO's clamp at construction), a bear with one of three,
//    a tamable without a row is Untrainable at its own slots;
//  - a tamed pet gains training points: Begin Animal Training grants the starting points, battle fills the progress
//    bar through the damage hook, a trained stat rises and the slot step raises the slots, the followers and the taming
//    the pet asks for;
//  - planning applies an ability: the plan keeps an entry, a magical ability zeroes the moves it requires, and applying
//    a special ability, a magical ability (with its required moves) and a resistance changes the creature;
//  - a pet at its cap refuses more: no training at the top slot, no ability outside its definition, and spent points
//    end training;
//  - slot lowering works at the trainer: a low roll at construction lowers the range by one, the trainer's greeting,
//    and training a lowered pet steps it up at the trainer's rates;
//  - the gate opens on the right skill: the training gate fills the bar of a pet in training with its master near, and
//    only then;
//  - evo pets and pack mules gain nothing (and their own tests pass unchanged);
//  - the patches: the taming target reads CurrentTameSkill and calls OnAfterTame, the lore skill opens the training
//    lore gump (and refuses a second), a pet in training gains Focus, a trained weapon ability is the one swung;
//  - storage: the training state, the plan, the lowered slot and the tame requirement survive the store's own
//    Serialize/Deserialize, and a deleted creature's record is gone.
//
// Test-host facts this file meets (notes/port-recipe.md): everything with a timer is made after ShardTestClock.Arm();
// Configure is not run for us (the store is configured by the facts that need it; the helper's tables build lazily);
// a player's gumps reach the wire only on a NetState with an account. Each fact deletes what it made.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.SkillHandlers;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

// A slot-lowerable creature whose construction rolls are chosen: CanLowerSlot matches the type NAME (ServUO's
// _SlotLowerables holds Nightmare), so the two test types are both named Nightmare, each in a nested class of its own,
// and the definition is the creature's own, so the table is not consulted.
public static class LowRoll
{
    public class Nightmare : BaseCreature
    {
        private static readonly TrainingDefinition Def = new(
            typeof(Nightmare), Class.MagicalAndNecromantic, MagicalAbility.Variety2, PetTrainingHelper.SpecialAbilityNone,
            PetTrainingHelper.WepAbility2, PetTrainingHelper.AreaEffectArea1, 3, 5
        );

        public override TrainingDefinition TrainingDefinition => Def;

        public Nightmare() : base(AIType.AI_Melee)
        {
            Body = 0x74;
            Tamable = true;
            MinTameSkill = 95.1;
            SetStr(100);
            SetHits(100);
            PetTrainingHooks.SetAverage(this, 0, 100, 10);
            PetTrainingHooks.SetAverage(this, 0, 100, 20);
            PetTrainingHooks.SetAverage(this, 0, 100, 30);
            ControlSlots = 3;
        }
    }
}

public static class HighRoll
{
    public class Nightmare : BaseCreature
    {
        private static readonly TrainingDefinition Def = new(
            typeof(Nightmare), Class.MagicalAndNecromantic, MagicalAbility.Variety2, PetTrainingHelper.SpecialAbilityNone,
            PetTrainingHelper.WepAbility2, PetTrainingHelper.AreaEffectArea1, 3, 5
        );

        public override TrainingDefinition TrainingDefinition => Def;

        public Nightmare() : base(AIType.AI_Melee)
        {
            Body = 0x74;
            Tamable = true;
            MinTameSkill = 95.1;
            SetStr(100);
            SetHits(100);
            PetTrainingHooks.SetAverage(this, 0, 100, 90);
            PetTrainingHooks.SetAverage(this, 0, 100, 80);
            PetTrainingHooks.SetAverage(this, 0, 100, 70);
            ControlSlots = 3;
        }
    }
}

// A bear that records OnAfterTame (ServUO's hook, reached through the AnimalTaming patch).
public class WatchfulBear : GrizzlyBear
{
    public Mobile TamedBy { get; private set; }

    public override void OnAfterTame(Mobile tamer)
    {
        base.OnAfterTame(tamer);
        TamedBy = tamer;
    }
}

[Collection("Sequential UOContent Tests")]
public class PetTrainingVerification
{
    private readonly ITestOutputHelper _out;

    public PetTrainingVerification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D Origin = new(1160, 1160, 0); // Ter Mur is 1280 wide

    private static Point3D At(int dx, int dy) => new(Origin.X + dx, Origin.Y + dy, Origin.Z);

    private static PlayerMobile NewPlayer(Point3D loc)
    {
        var pm = new PlayerMobile { Player = true, Young = false, RawStr = 100, RawDex = 100, RawInt = 100 };
        pm.AddItem(new Backpack());
        pm.Skills.AnimalTaming.Cap = 120.0;
        pm.Skills.AnimalTaming.Base = 120.0;
        pm.Skills.AnimalLore.Cap = 120.0;
        pm.Skills.AnimalLore.Base = 120.0;
        pm.MoveToWorld(loc, Map.TerMur);
        pm.Hits = pm.HitsMax;
        return pm;
    }

    private static PlayerMobile Online(PlayerMobile pm, out NetState ns)
    {
        ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm; // login does this; Mobile.NetState does not, and a gump reply reads it

        if (Server.Accounting.Security.AccountSecurity.CurrentAlgorithm ==
            Server.Accounting.Security.PasswordProtectionAlgorithm.None)
        {
            Server.Accounting.Security.AccountSecurity.CurrentAlgorithm =
                Server.Accounting.Security.PasswordProtectionAlgorithm.PBKDF2;
        }

        ns.Account = new Server.Accounting.Account($"p130-{pm.Serial.Value:X8}", "p130-test-only");
        return pm;
    }

    private static T Place<T>(T bc, Point3D loc) where T : BaseCreature
    {
        bc.MoveToWorld(loc, Map.TerMur);
        return bc;
    }

    // No assertion here: the facts call this before their try, and a red one must still delete what it made.
    private static T Tame<T>(T bc, PlayerMobile pm) where T : BaseCreature
    {
        bc.Owners.Add(pm);
        bc.SetControlMaster(pm);
        return bc;
    }

    private static GrizzlyBear TamedBear(PlayerMobile pm, Point3D loc) => Tame(Place(new GrizzlyBear(), loc), pm);

    // A wild creature hits the pet (or is hit by it) through the hook AOS.Damage calls, until the bar moves.
    private static void Fight(BaseCreature pet, BaseCreature wild, int rounds)
    {
        for (var i = 0; i < rounds; i++)
        {
            var damage = 1;
            PetTrainingHooks.OnDamage(pet, wild, ref damage, AbilityDamageType.Melee);
        }
    }

    private static void DeleteAll(params Mobile[] mobiles)
    {
        foreach (var m in mobiles)
        {
            m?.Delete();
        }
    }

    private static void DeleteAll(params Item[] items)
    {
        foreach (var i in items)
        {
            i?.Delete();
        }
    }

    // -- the wire -------------------------------------------------------------------------------------------------------

    internal record WireGump(int Length, string Layout, List<string> Strings);

    internal static byte[] Inflate(ReadOnlySpan<byte> packed, int expectedLength)
    {
        using var input = new MemoryStream(packed.ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        Assert.Equal(expectedLength, (int)output.Length);
        return output.ToArray();
    }

    // The LAST 0xDD for `gump` on `ns` (a Refresh resends it under the same serial and type id), found and decoded.
    // Fails if it never reached the wire.
    internal static WireGump OnTheWire(NetState ns, BaseGump gump)
    {
        Assert.NotNull(gump);
        var span = ns.SendBuffer.GetReadSpan();
        WireGump last = null;

        for (var i = 0; i + 27 <= span.Length; i++)
        {
            if (span[i] != 0xDD ||
                BinaryPrimitives.ReadUInt32BigEndian(span[(i + 3)..]) != (uint)gump.Serial ||
                BinaryPrimitives.ReadInt32BigEndian(span[(i + 7)..]) != gump.TypeID)
            {
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(span[(i + 1)..]);
            var wire = span.Slice(i, length);

            var pos = 19;
            var layoutPacked = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]) - 4;
            var layoutLength = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[(pos + 4)..]);
            pos += 8;
            var layout = Encoding.ASCII.GetString(Inflate(wire.Slice(pos, layoutPacked), layoutLength));
            pos += layoutPacked;

            var count = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]);
            pos += 4;
            var strings = new List<string>();

            if (count > 0)
            {
                var stringsPacked = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]) - 4;
                var stringsLength = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[(pos + 4)..]);
                pos += 8;
                var raw = Inflate(wire.Slice(pos, stringsPacked), stringsLength);
                var at = 0;

                for (var s = 0; s < count && at + 2 <= raw.Length; s++)
                {
                    var chars = (raw[at] << 8) | raw[at + 1];
                    at += 2;
                    strings.Add(Encoding.BigEndianUnicode.GetString(raw, at, chars * 2));
                    at += chars * 2;
                }
            }

            last = new WireGump(length, layout, strings);
            i += length - 1;
        }

        if (last == null)
        {
            Assert.Fail($"{gump.GetType().Name} (serial {gump.Serial}, type {gump.TypeID}) never reached the wire");
        }

        return last;
    }

    private static int Mark(NetState ns) => ns.SendBuffer.GetReadSpan().Length;

    // Every 0xC1 localized message sent on `ns` since `from`, as (speaker serial, cliloc).
    private static List<(uint, int)> Messages(NetState ns, int from)
    {
        var span = ns.SendBuffer.GetReadSpan();
        var found = new List<(uint, int)>();

        for (var i = from; i + 18 <= span.Length; i++)
        {
            if (span[i] != 0xC1)
            {
                continue;
            }

            var len = (span[i + 1] << 8) | span[i + 2];
            if (len < 48 || i + len > span.Length)
            {
                continue;
            }

            var serial = (uint)((span[i + 3] << 24) | (span[i + 4] << 16) | (span[i + 5] << 8) | span[i + 6]);
            var number = (span[i + 14] << 24) | (span[i + 15] << 16) | (span[i + 16] << 8) | span[i + 17];
            found.Add((serial, number));
            i += len - 1;
        }

        return found;
    }

    // -- The tables ---------------------------------------------------------------------------------------------------

    [Fact]
    public void TheTablesAreServUOs()
    {
        Assert.Equal(137, PetTrainingHelper.DefinitionTypeNames.Count);
        Assert.Equal("Alligator", PetTrainingHelper.DefinitionTypeNames[0]);
        Assert.Equal("CoconutCrab", PetTrainingHelper.DefinitionTypeNames[136]);

        var missing = PetTrainingHelper.MissingDefinitionTypes;
        _out.WriteLine($"definitions resolved: {PetTrainingHelper.Definitions.Count}, missing on this shard: {missing.Count}");
        _out.WriteLine(string.Join(", ", missing));
        Assert.Equal(137, PetTrainingHelper.Definitions.Count + missing.Count);
        // cc-P198 ported DragonTurtleHatchling, the example this line used to name as missing: its definition resolves now.
        Assert.DoesNotContain("DragonTurtleHatchling", missing);
        Assert.True(PetTrainingHelper.Definitions.ContainsKey(typeof(DragonTurtleHatchling)));
        Assert.DoesNotContain("Dragon", missing);

        var dragon = PetTrainingHelper.Definitions[typeof(Dragon)];
        Assert.Equal(Class.MagicalClawedAndTailed, dragon.Class);
        Assert.Equal(MagicalAbility.Dragon2, dragon.MagicalAbilities);
        Assert.Equal((4, 5), (dragon.ControlSlotsMin, dragon.ControlSlotsMax));
        Assert.Same(PetTrainingHelper.WepAbility2, dragon.WeaponAbilities);

        var bear = PetTrainingHelper.Definitions[typeof(GrizzlyBear)];
        Assert.Equal((1, 3), (bear.ControlSlotsMin, bear.ControlSlotsMax));
        Assert.True(bear.HasSpecialAbility(SpecialAbility.GraspingClaw));
        Assert.False(bear.HasSpecialAbility(SpecialAbility.DragonBreath));

        // 10 stats, 5 resistances, 18 skills, 15 magical abilities, 24 special abilities (the ones with a localization),
        // 7 area effects and 23 weapon abilities (ColdWind is not pinned's).
        Assert.Equal(102, PetTrainingHelper.TrainingPoints.Count);
        Assert.DoesNotContain(PetTrainingHelper.WeaponAbilities, a => a == null);
        Assert.Equal(23, PetTrainingHelper.WeaponAbilityLocalizations.Length);

        var str = PetTrainingHelper.GetTrainingPoint(PetStat.Str);
        Assert.Equal((3.0, 1, 700), (str.Weight, str.Start, str.Max));
        Assert.Equal(1061146, str.Name.Number);

        var piercing = PetTrainingHelper.GetTrainingPoint(MagicalAbility.Piercing);
        Assert.Equal(3, piercing.Requirements.Length);
        Assert.Same(WeaponAbility.ArmorIgnore, piercing.Requirements[0].Requirement);

        Assert.Same(SpecialAbility.GraspingClaw, PetTrainingHelper.GetTrainingPoint(SpecialAbility.GraspingClaw).TrainPoint);
        Assert.Null(PetTrainingHelper.GetTrainingPoint(SpecialAbility.ColossalRage)); // past the localization table
        Assert.Equal(1157481, PetTrainingHelper.GetCategoryLocalization(MagicalAbility.Bashing));
        Assert.Equal(1157496, PetTrainingHelper.GetCategoryLocalization(SkillName.Wrestling));
        Assert.Equal(1044103, PetTrainingHelper.GetLocalization(null, SkillName.Wrestling));
    }

    [Fact]
    public void AStockDragonBuildsWithFourSlotsAndATamableWithoutARowIsUntrainable()
    {
        var dragon = Place(new Dragon(), At(0, 0));
        var bear = Place(new GrizzlyBear(), At(1, 0));
        var crane = Place(new ScaledSwampDragon(), At(2, 0)); // tamable, no ServUO row
        var mule = Place(new PackMule(), At(3, 0));

        try
        {
            // ServUO's setter clamps a constructor's 3 to the definition's 4 (BaseCreature.cs:3843-3861).
            Assert.Equal((4, 4, 5), (dragon.ControlSlots, dragon.ControlSlotsMin, dragon.ControlSlotsMax));
            Assert.Equal((1, 1, 3), (bear.ControlSlots, bear.ControlSlotsMin, bear.ControlSlotsMax));

            var def = PetTrainingHelper.GetTrainingDefinition(crane);
            Assert.NotNull(def);
            Assert.Equal(Class.Untrainable, def.Class);
            Assert.Equal((crane.ControlSlots, crane.ControlSlots), (def.ControlSlotsMin, def.ControlSlotsMax));

            Assert.Null(PetTrainingHelper.GetTrainingDefinition(mule));
            Assert.Equal(bear.MinTameSkill, bear.CurrentTameSkill);
        }
        finally
        {
            DeleteAll(dragon, bear, crane, mule);
        }
    }

    // -- A tamed pet gains training points -----------------------------------------------------------------------------

    [Fact]
    public void ATamedPetGainsTrainingPoints()
    {
        ShardTestClock.Arm();
        var pm = Online(NewPlayer(At(0, 10)), out _);
        var bear = TamedBear(pm, At(1, 10));
        var wild = Place(new GrizzlyBear(), At(2, 10));

        try
        {
            Assert.True(bear.Controlled);
            Assert.Null(bear.TrainingProfile);
            Assert.Equal(1, pm.Followers);

            var profile = PetTrainingHelper.GetTrainingProfile(bear, true);
            Assert.False(profile.HasBegunTraining);
            Assert.Equal(0, profile.TrainingPoints);

            profile.BeginTraining();
            Assert.True(profile.HasBegunTraining);
            Assert.Equal(2381, profile.TrainingPoints); // ServUO: a 1 -> 3 slot creature starts with 2381
            Assert.Equal(0.0, profile.TrainingProgress);
            Assert.False(profile.CanApplyOptions);

            Fight(bear, wild, 6000);
            Assert.True(profile.TrainingProgress > 0, "battle fills the bar");
            Assert.True(profile.CanApplyOptions, $"the bar reaches its top ({profile.TrainingProgress})");

            var str = PetTrainingHelper.GetTrainingPoint(PetStat.Str);
            var start = -1;
            PetTrainingHelper.GetStartValue(str, bear, ref start);
            Assert.Equal(bear.RawStr, start);

            var value = start + 20;
            var cost = PetTrainingHelper.GetTotalCost(str, bear, value, start);
            Assert.Equal(60, cost); // 20 points at weight 3.0
            Assert.True(PetTrainingHelper.CanControl(pm, bear, profile));

            Assert.True(PetTrainingHelper.ApplyTrainingPoint(bear, str, value));
            Assert.Equal(value, bear.RawStr);

            var tameBefore = bear.CurrentTameSkill;
            profile.OnTrain(pm, cost);

            Assert.Equal(2, bear.ControlSlots);
            Assert.Equal(2, pm.Followers);
            Assert.True(profile.HasIncreasedControlSlot);
            Assert.Equal(2381 - 60, profile.TrainingPoints);
            // ServUO OnTrain (:164-188) reads the increase before the slot step: 22 from the minimum, 21 one above it.
            Assert.Equal(tameBefore + 22, bear.CurrentTameSkill);

            profile.OnTrain(pm, 10);
            Assert.Equal(2, bear.ControlSlots);
            Assert.Equal(tameBefore + 22 + 3, bear.CurrentTameSkill);
            Assert.Equal(1, profile.TrainedThisLevel);
        }
        finally
        {
            DeleteAll(bear, wild, pm);
        }
    }

    // -- Planning applies an ability -----------------------------------------------------------------------------------

    [Fact]
    public void PlanningAppliesAnAbility()
    {
        var pm = NewPlayer(At(0, 20));
        var bear = TamedBear(pm, At(1, 20));

        try
        {
            Assert.True(bear.Controlled);
            var plan = PetTrainingHelper.GetPlanningProfile(bear, true);
            Assert.NotNull(plan);
            Assert.Same(plan, PetTrainingHelper.GetTrainingProfile(bear).PlanningProfile);

            plan.AddToPlan(WeaponAbility.ArmorIgnore, 100, 100);
            plan.AddToPlan(SpecialAbility.GraspingClaw, 100, 100);
            Assert.Equal(2, plan.Entries.Count);

            // A magical ability zeroes the entries of the kinds its requirements cover (ServUO PlanningProfile.cs:36-58).
            plan.AddToPlan(MagicalAbility.Piercing, 1, 301);
            var move = Assert.Single(plan.Entries, e => e.TrainPoint is WeaponAbility);
            Assert.Equal((0, 0), (move.Value, move.Cost));
            var claw = Assert.Single(plan.Entries, e => e.TrainPoint is SpecialAbility);
            Assert.Equal(100, claw.Cost);

            var profile = PetTrainingHelper.GetAbilityProfile(bear, true);
            Assert.False(profile.HasAbility(SpecialAbility.GraspingClaw));

            Assert.True(PetTrainingHelper.ApplyTrainingPoint(bear, PetTrainingHelper.GetTrainingPoint(SpecialAbility.GraspingClaw), 100));
            Assert.True(profile.HasAbility(SpecialAbility.GraspingClaw));
            Assert.Contains(SpecialAbility.GraspingClaw, profile.Advancements);

            // Piercing brings its required moves (ServUO AbilityProfile.cs:248-270, the seam cc-P121 left).
            Assert.True(PetTrainingHelper.ApplyTrainingPoint(bear, PetTrainingHelper.GetTrainingPoint(MagicalAbility.Piercing), 1));
            Assert.True(profile.HasAbility(MagicalAbility.Piercing));
            Assert.True(profile.HasAbility(WeaponAbility.ArmorIgnore));
            Assert.True(profile.HasAbility(WeaponAbility.ParalyzingBlow));
            Assert.True(profile.HasAbility(WeaponAbility.BleedAttack));

            Assert.True(PetTrainingHelper.ApplyTrainingPoint(bear, PetTrainingHelper.GetTrainingPoint(ResistanceType.Fire), 55));
            Assert.Equal(55, bear.FireResistSeed);

            plan.Clear();
            Assert.Empty(plan.Entries);
        }
        finally
        {
            DeleteAll(bear, pm);
        }
    }

    // -- A pet at its cap refuses more ---------------------------------------------------------------------------------

    [Fact]
    public void APetAtItsCapRefusesMore()
    {
        var pm = NewPlayer(At(0, 30));
        var bear = TamedBear(pm, At(1, 30));
        var wild = Place(new GrizzlyBear(), At(2, 30));

        try
        {
            Assert.True(bear.Controlled);
            // An ability outside the definition is refused: a bear has no breath.
            Assert.False(PetTrainingHelper.ValidateTrainingPoint(bear, SpecialAbility.DragonBreath));
            Assert.False(PetTrainingHelper.ApplyTrainingPoint(bear, PetTrainingHelper.GetTrainingPoint(SpecialAbility.DragonBreath), 100));
            Assert.False(PetTrainingHelper.GetAbilityProfile(bear, true).HasAbility(SpecialAbility.DragonBreath));

            // Spent points end training (ServUO TrainingProfile.TrainingPoints setter).
            var profile = PetTrainingHelper.GetTrainingProfile(bear, true);
            profile.BeginTraining();
            Assert.True(profile.HasBegunTraining);
            profile.TrainingPoints -= profile.TrainingPoints;
            Assert.False(profile.HasBegunTraining);
            Assert.Equal(0, profile.TrainingPoints);

            // At the top slot there is nothing to begin and the bar does not move.
            bear.RemoveFollowers();
            bear.ControlSlots = bear.ControlSlotsMax;
            bear.AddFollowers();
            Assert.Equal(3, bear.ControlSlots);

            profile.BeginTraining();
            Assert.False(profile.HasBegunTraining);
            Assert.False(profile.CanApplyOptions);

            Fight(bear, wild, 500);
            Assert.Equal(0.0, profile.TrainingProgress);
        }
        finally
        {
            DeleteAll(bear, wild, pm);
        }
    }

    // -- Slot lowering works at the trainer ----------------------------------------------------------------------------

    [Fact]
    public void SlotLoweringWorksAtTheTrainer()
    {
        ShardTestClock.Arm();
        var pm = Online(NewPlayer(At(0, 40)), out var ns);
        var low = Place(new LowRoll.Nightmare(), At(1, 40));
        var high = Place(new HighRoll.Nightmare(), At(2, 40));
        // CantWalk: the loop below advances the clock up to sixteen minutes, long enough for a vendor to wander, and a
        // trainer that strays more than 8 tiles from the player never greets. cc-P225 saw this fact red in one
        // full-suite run in three (never when run alone); a still trainer leaves only the coin flip it tests.
        var trainer = Place(new AnimalTrainer { CantWalk = true }, At(0, 46));

        try
        {
            // ServUO CalculateSlots: rolls averaging at or under a third lower the range by one, and the construction
            // assignment lands on the (lowered) minimum.
            Assert.Equal((2, 2, 5), (low.ControlSlots, low.ControlSlotsMin, low.ControlSlotsMax));
            Assert.Equal((3, 3, 5), (high.ControlSlots, high.ControlSlotsMin, high.ControlSlotsMax));
            Assert.True(PetTrainingStore.Get(low, false)?.SlotsLowered);
            Assert.NotEqual(true, PetTrainingStore.Get(high, false)?.SlotsLowered);

            // The trainer greets a player who walks up (AnimalTrainer.PetTraining.cs, half the time).
            var mark = Mark(ns);
            var greeted = false;

            for (var i = 0; i < 60 && !greeted; i++)
            {
                trainer.OnMovement(pm, At(0, 60));
                greeted = Messages(ns, mark).Any(m => m.Item1 == (uint)trainer.Serial && m.Item2 == 1157526);
                ShardTestClock.AdvanceSeconds(16);
            }

            Assert.True(
                greeted,
                $"the trainer says 1157526 to a player who walks up (trainer at {trainer.Location}, player at " +
                $"{pm.Location}, LOS {trainer.InLOS(pm)}, {Messages(ns, mark).Count} message(s) since the mark)"
            );

            // Training the lowered pet steps it up: 2 -> 3 for 1501 starting points, the tame requirement +22 capped
            // at MaxTameRequirement (108).
            Tame(low, pm);
            Assert.True(low.Controlled);
            Assert.Equal(2, pm.Followers);

            var profile = PetTrainingHelper.GetTrainingProfile(low, true);
            profile.BeginTraining();
            Assert.True(profile.HasBegunTraining);
            Assert.Equal(1501, profile.TrainingPoints);
            Assert.Equal(95.1, low.CurrentTameSkill);

            profile.OnTrain(pm, 100);
            Assert.Equal((3, 2, 5), (low.ControlSlots, low.ControlSlotsMin, low.ControlSlotsMax));
            Assert.Equal(3, pm.Followers);
            Assert.Equal(Math.Min(BaseCreature.MaxTameRequirement, 95.1 + 22), low.CurrentTameSkill, 3);
            Assert.Equal(108.0, low.CurrentTameSkill, 3);

            // A later assignment (staff, training) is not clamped again: the range settled at construction.
            high.ControlSlots = 5;
            Assert.Equal((5, 3, 5), (high.ControlSlots, high.ControlSlotsMin, high.ControlSlotsMax));
        }
        finally
        {
            DeleteAll(low, high, trainer, pm);
        }
    }

    // -- The gate opens on the right skill -----------------------------------------------------------------------------

    [Fact]
    public void TheGateOpensOnTheRightSkill()
    {
        ShardTestClock.Arm();
        var pm = Online(NewPlayer(At(0, 50)), out _);
        var training = TamedBear(pm, At(1, 50));
        var idle = TamedBear(pm, At(2, 50));
        var gate = new PetTrainingGate();
        gate.MoveToWorld(At(3, 50), Map.TerMur);

        try
        {
            Assert.True(training.Controlled && idle.Controlled);
            var profile = PetTrainingHelper.GetTrainingProfile(training, true);
            profile.BeginTraining();
            Assert.Equal(0.0, profile.TrainingProgress);

            // A pet not in training walks through: nothing.
            Assert.True(gate.OnMoveOver(idle));
            Assert.NotEqual(true, PetTrainingHelper.GetTrainingProfile(idle)?.HasBegunTraining);

            // Its master far away: nothing.
            pm.MoveToWorld(At(0, 90), Map.TerMur);
            Assert.True(gate.OnMoveOver(training));
            Assert.Equal(0.0, profile.TrainingProgress);

            // Master near, pet in training: the bar fills and the lore gump comes up.
            pm.MoveToWorld(At(0, 50), Map.TerMur);
            Assert.True(gate.OnMoveOver(training));
            Assert.Equal(profile.TrainingProgressMax, profile.TrainingProgress);
            Assert.True(profile.CanApplyOptions);
            Assert.True(pm.HasGump<NewAnimalLoreGump>());
        }
        finally
        {
            DeleteAll(gate);
            DeleteAll(training, idle, pm);
        }
    }

    // -- Evo pets and pack mules gain nothing (decision 4) -------------------------------------------------------------

    [Fact]
    public void EvoPetsAndPackMulesGainNothing()
    {
        ShardTestClock.Arm();
        var pm = NewPlayer(At(0, 60));
        var evo = Tame(Place(new EvoPet("dog"), At(1, 60)), pm);
        var mule = Tame(Place(new PackMule(), At(2, 60)), pm);
        var wild = Place(new GrizzlyBear(), At(3, 60));

        try
        {
            foreach (var pet in new BaseCreature[] { evo, mule })
            {
                Assert.True(pet.Controlled);
                Assert.True(PetTrainingHelper.IsExcluded(pet));
                Assert.Null(PetTrainingHelper.GetTrainingDefinition(pet));
                Assert.Null(PetTrainingHelper.GetTrainingProfile(pet, true));
                Assert.Null(PetTrainingHelper.GetPlanningProfile(pet, true));
                Assert.Equal(pet.ControlSlots, pet.ControlSlotsMin);
                Assert.Equal(pet.ControlSlots, pet.ControlSlotsMax);

                Fight(pet, wild, 300);
                Assert.Null(pet.TrainingProfile);
                Assert.Null(PetTrainingStore.Get(pet, false));
            }
        }
        finally
        {
            DeleteAll(evo, mule, wild, pm);
        }
    }

    // -- The patches ---------------------------------------------------------------------------------------------------

    [Fact]
    public void TamingReadsCurrentTameSkillAndCallsOnAfterTame()
    {
        ShardTestClock.Arm();
        var pm = Online(NewPlayer(At(0, 70)), out var ns);
        // Rooted: a wild bear's AI walks it off during the first tame's twenty seconds, and the second target is then
        // out of range (500446), which is not what this fact is about.
        var bear = Place(new WatchfulBear { CantWalk = true }, At(1, 70));
        var asksMore = Place(new WatchfulBear { CantWalk = true }, At(0, 71));

        try
        {
            Assert.Null(bear.TamedBy);
            AnimalTaming.OnUse(pm);
            Assert.NotNull(pm.Target);
            pm.Target.Invoke(pm, bear);
            ShardTestClock.AdvanceSeconds(20); // 3 to 4 ticks of three seconds

            Assert.True(bear.Controlled, "a 120 tamer tames a bear");
            Assert.Same(pm, bear.ControlMaster);
            Assert.Same(pm, bear.TamedBy); // the AnimalTaming patch's OnTamed
            Assert.NotNull(PetTrainingHelper.GetAbilityProfile(bear));

            // A trained creature asks for its CurrentTameSkill, not its MinTameSkill (the same patch).
            asksMore.CurrentTameSkill = 125.0;
            Assert.True(pm.Skills.AnimalTaming.Value >= asksMore.MinTameSkill);
            var mark = Mark(ns);
            AnimalTaming.OnUse(pm);
            pm.Target.Invoke(pm, asksMore);
            ShardTestClock.AdvanceSeconds(20);

            Assert.False(asksMore.Controlled);
            Assert.Null(asksMore.TamedBy);
            Assert.Contains(Messages(ns, mark), m => m.Item2 == 502806); // You have no chance of taming this creature.
        }
        finally
        {
            DeleteAll(bear, asksMore, pm);
        }
    }

    [Fact]
    public void TheLoreSkillOpensTheTrainingLoreGumpOnceAndBeginsTraining()
    {
        ShardTestClock.Arm();
        var pm = Online(NewPlayer(At(0, 80)), out var ns);
        var bear = TamedBear(pm, At(1, 80));

        try
        {
            Assert.True(bear.Controlled);
            AnimalLore.OnUse(pm);
            Assert.NotNull(pm.Target);
            pm.Target.Invoke(pm, bear);
            Assert.False(pm.HasGump<NewAnimalLoreGump>());
            ShardTestClock.AdvanceSeconds(1.1);

            var gump = pm.FindGump<NewAnimalLoreGump>();
            Assert.NotNull(gump);
            var wire = OnTheWire(ns, gump);
            Assert.Contains("1157487", wire.Layout); // Begin Animal Training
            Assert.DoesNotContain("1157491", wire.Layout); // Pet Training Progress: not yet
            Assert.Contains(wire.Strings, s => s.Contains(bear.Name));

            // A second lore while the gump is open is refused (ServUO AnimalLore.cs:17-20).
            var mark = Mark(ns);
            AnimalLore.OnUse(pm);
            Assert.Contains(Messages(ns, mark), m => m.Item2 == 500118);

            // The Begin Animal Training button.
            gump.OnResponse(ns, new RelayInfo(4, default, default, default, default));
            var profile = PetTrainingHelper.GetTrainingProfile(bear);
            Assert.NotNull(profile);
            Assert.True(profile.HasBegunTraining);

            var refreshed = pm.FindGump<NewAnimalLoreGump>();
            Assert.NotNull(refreshed);
            Assert.Contains("1157491", OnTheWire(ns, refreshed).Layout);

            // The progress tracker, with this pet on it.
            refreshed.OnResponse(ns, new RelayInfo(1, default, default, default, default));
            ShardTestClock.AdvanceSeconds(0.6);
            var tracker = pm.FindGump<PetTrainingProgressGump>();
            Assert.NotNull(tracker);
            Assert.Contains(OnTheWire(ns, tracker).Strings, s => s.Contains(bear.Name));
        }
        finally
        {
            DeleteAll(bear, pm);
        }
    }

    [Fact]
    public void APetInTrainingGainsFocusAndSwingsItsTrainedMove()
    {
        var pm = NewPlayer(At(0, 90));
        var pet = TamedBear(pm, At(1, 90));
        var wild = Place(new GrizzlyBear(), At(2, 90));

        try
        {
            Assert.True(pet.Controlled);

            foreach (var bc in new[] { pet, wild })
            {
                bc.Skills.Focus.Cap = 100.0;
                bc.Skills.Focus.Base = 50.0;
                Assert.Equal(SkillLock.Up, bc.Skills.Focus.Lock); // a creature's default
            }

            // The SkillCheck patch: a pet gains Focus, a wild creature still does not.
            SkillCheck.Gain(pet, pet.Skills.Focus);
            SkillCheck.Gain(wild, wild.Skills.Focus);
            Assert.True(pet.Skills.Focus.Base > 50.0, $"a pet in training gains Focus ({pet.Skills.Focus.Base})");
            Assert.Equal(50.0, wild.Skills.Focus.Base);

            // The BaseWeapon patch's read: a trained move first, the natural one otherwise.
            Assert.Null(pet.GetWeaponAbility());
            Assert.Null(pet.TryGetWeaponAbility());
            Assert.True(PetTrainingHelper.GetAbilityProfile(pet, true).AddAbility(WeaponAbility.Dismount));
            Assert.Same(WeaponAbility.Dismount, pet.TryGetWeaponAbility());
        }
        finally
        {
            DeleteAll(pet, wild, pm);
        }
    }

    // -- Storage (decision 2a) -----------------------------------------------------------------------------------------

    private static byte[] SaveStore()
    {
        PetTrainingStore.Configure();
        var buffer = new byte[1 << 20];
        var writer = new BufferWriter(buffer, true);
        PetTrainingStore.Instance.Serialize(writer);
        writer.Flush();
        return buffer;
    }

    private static void LoadStore(byte[] buffer) => PetTrainingStore.Instance.Deserialize(new BufferReader(buffer));

    [Fact]
    public void TrainingStateSurvivesTheStoresOwnSaveAndADeletedCreatureIsForgotten()
    {
        ShardTestClock.Arm();
        var pm = NewPlayer(At(0, 100));
        var bear = TamedBear(pm, At(1, 100));
        var low = Tame(Place(new LowRoll.Nightmare(), At(2, 100)), pm);
        var gone = TamedBear(pm, At(3, 100));

        try
        {
            Assert.True(bear.Controlled && low.Controlled && gone.Controlled);
            var profile = PetTrainingHelper.GetTrainingProfile(bear, true);
            profile.BeginTraining();
            profile.TrainingProgress = 42.5;
            profile.TrainingMode = TrainingMode.Planning;
            profile.PlanningProfile.AddToPlan(SpecialAbility.GraspingClaw, 100, 100);
            profile.PlanningProfile.AddToPlan(SkillName.Wrestling, 150, 150);
            profile.OnTrain(pm, 300);
            Assert.Equal(2, bear.ControlSlots);
            var tame = bear.CurrentTameSkill;

            PetTrainingHelper.GetTrainingProfile(gone, true).BeginTraining();
            Assert.True(low.ControlSlotsMin == 2 && PetTrainingStore.Get(low, false).SlotsLowered);

            var buffer = SaveStore();

            gone.Delete();
            Assert.Null(PetTrainingStore.Get(gone, false));

            // Load over the live table: the records come back as written.
            LoadStore(buffer);

            var back = PetTrainingHelper.GetTrainingProfile(bear);
            Assert.NotNull(back);
            Assert.True(back.HasBegunTraining);
            Assert.True(back.HasIncreasedControlSlot);
            Assert.Equal(42.5, back.TrainingProgress);
            Assert.Equal(2381 - 300, back.TrainingPoints);
            Assert.Equal(TrainingMode.Planning, back.TrainingMode);
            Assert.Equal(2, back.PlanningProfile.Entries.Count);
            Assert.Same(SpecialAbility.GraspingClaw, back.PlanningProfile.Entries[0].TrainPoint);
            Assert.Equal(SkillName.Wrestling, back.PlanningProfile.Entries[1].TrainPoint);
            Assert.Equal(150, back.PlanningProfile.Entries[1].Cost);
            Assert.Equal(tame, bear.CurrentTameSkill);

            Assert.True(PetTrainingStore.Get(low, false).SlotsLowered);
            Assert.Equal(2, low.ControlSlotsMin);
            Assert.Null(PetTrainingStore.Get(gone, false)); // a deleted creature is not written and not read back
        }
        finally
        {
            DeleteAll(bear, low, gone, pm);
        }
    }
}
