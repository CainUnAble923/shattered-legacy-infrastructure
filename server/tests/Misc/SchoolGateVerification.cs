// SchoolGateVerification.cs
//
// cc-P714: the Chivalry and Necromancy initiation gates (ClusterFSchoolGate), their two quests (ClusterFInitiation), the
// shorter start (ClusterFStartChoices in CharacterCreation) and the tutorial's school lessons. Notes:
// shard-migration notes/cc-P714-gated-skills.md.
//
// Facts:
//   1. Without the permission a player's Chivalry or Necromancy cast is refused at Spell.Cast (the patch) with the
//      teacher's name, a book in the pack or not; a power scroll for the school is refused before it is used; Magery is
//      untouched; staff, creatures and a gate switched off are never refused.
//   2. No gain without the permission (ClusterFSkillGain's handlers); a gain after it.
//   3. The cap policy holds an unearned school at the character's own base (0 for a new character), so NPC teaching
//      finds no room; Grant puts back the normal cap and changes no skill; a base above zero is never taken away.
//   4. The permission is per character, saved once, kept through death and a save round trip; both schools on one
//      character; another character on the same account has neither.
//   5. A Place on the Watch, played through: bundles, quartermaster, patrol, three scavengers with personal credit (one
//      killed by someone else alone does not count), the tithe (the balance must rise by 100 after the step began; a full
//      balance gives 100 gold to the fund instead), then the permission and the book, once; a full pack holds the book.
//   6. Names for the Last Door, played through: three graves (robber, then plate), Oren, the binding and its guardian,
//      the warning ("Not yet" keeps the work), then the permission, the book (Curse Weapon and Evil Omen) and ten of each
//      reagent, once; Necromancy and Chivalry stay at their bases.
//   7. Creation: the four templates (Thrower for a gargoyle), the removed professions, the custom list's rules (off-list,
//      over 50, repeats, totals, race weapons, stats); through the creation event a refused request makes no character.
//   8. The tutorial: before initiation a school lesson is a demonstration and referral only (no loan, no trial, no
//      tithing points, no station credit); after it the lesson runs; skipping or graduating grants neither school.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.CharacterCreation;
using Server.Initiation;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Random;
using Server.Spells.Chivalry;
using Server.Spells.Necromancy;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SchoolGateVerification : IDisposable
{
    private readonly List<string> _said = new();
    private readonly Func<Point3D, bool> _fits = TutorialWingTrials.Fits;

    public SchoolGateVerification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        ClusterFSkillCaps.ReloadForTests();
        ClusterFSchoolGate.Mode = "on";
        ClusterFSchoolGate.Said = (_, line) => _said.Add(line);
        ClusterFSchoolGate.ForgetRefusals();
        ClusterFInitiation.Said = (_, line) => _said.Add(line);
    }

    public void Dispose()
    {
        ClusterFSchoolGate.Mode = "auto";
        ClusterFSchoolGate.Said = null;
        ClusterFInitiation.Said = null;
        TutorialWingTrials.Fits = _fits;
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p714{Guid.NewGuid():N}"[..16], "p714-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(Point3D at, Race race = null)
        {
            Pm = new PlayerMobile { Player = true, Race = race ?? Race.Human, Body = 0x190, Name = "Initiate", RawStr = 60, RawDex = 60, RawInt = 40 };
            Pm.Hits = Pm.HitsMax;
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
            foreach (var key in SchoolGateStore.Progress.Keys.Where(k => k.Item1 == Pm.Serial).ToList())
            {
                SchoolGateStore.Progress.Remove(key);
            }

            SchoolGateStore.Permissions.Remove(Pm.Serial);
            TutorialWingTrials.Cancel(Pm);
            foreach (var foe in World.Mobiles.Values.OfType<InitiationFoe>().Where(f => f.Owner == Pm).ToList())
            {
                foe.Delete();
            }

            Pm.NetState = null;
            Ns.Mobile = null;
            Ns.Dispose();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static InitiationNpc Npc(string role, Point3D at)
    {
        var npc = new InitiationNpc(role);
        npc.MoveToWorld(at, Map.Trammel);
        return npc;
    }

    private static List<T> Find<T>(Mobile m) where T : Item
    {
        var list = new List<T>();
        foreach (var item in m.Backpack.FindItemsByType<T>())
        {
            list.Add(item);
        }

        return list;
    }

    private bool SaidContains(string part) => _said.Any(l => l.Contains(part, StringComparison.Ordinal));

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ALockedSchoolsCastAndPowerScrollAreRefusedAndNamesTheTeacher()
    {
        using var p = new Player(new Point3D(1000, 1000, 0));
        p.Pm.Mana = 0; // a cast that passes the gate stops at the mana check, with no refusal line
        p.Pm.Backpack.DropItem(new BookOfChivalry());

        Assert.False(new CloseWoundsSpell(p.Pm).Cast());
        Assert.True(SaidContains("You have not learned Chivalry. Sister Aveline at Fort Whitestone"));
        Assert.Null(p.Pm.Spell);

        ClusterFSchoolGate.ForgetRefusals();
        _said.Clear();
        Assert.False(new CurseWeaponSpell(p.Pm).Cast());
        Assert.True(SaidContains("You have not learned Necromancy. Oren Pike at the Dhampire Ruins"));

        _said.Clear();
        ClusterFSchoolGate.ForgetRefusals();
        var scroll = new PowerScroll(SkillName.Necromancy, 105.0);
        p.Pm.Backpack.DropItem(scroll); // SpecialScroll.CanUse wants it in the pack
        try
        {
            Assert.False(scroll.CanUse(p.Pm));
            Assert.True(SaidContains("You have not learned Necromancy."));
        }
        finally
        {
            scroll.Delete();
        }

        // Magery is not a gated school: the same cast reaches the mana check and says nothing about teachers.
        _said.Clear();
        ClusterFSchoolGate.ForgetRefusals();
        Assert.True(ClusterFSchoolGate.CanCast(p.Pm, new Server.Spells.First.MagicArrowSpell(p.Pm)));
        Assert.Empty(_said);

        // After the permission the gate passes (the cast then stops at the mana check, not at the gate).
        Assert.True(ClusterFSchoolGate.Grant(p.Pm, "chivalry", "test"));
        Assert.False(new CloseWoundsSpell(p.Pm).Cast());
        Assert.False(SaidContains("You have not learned"));
        Assert.True(ClusterFSchoolGate.CanCast(p.Pm, new CloseWoundsSpell(p.Pm)));

        // Staff, creatures and the switch off are never refused.
        p.Pm.AccessLevel = AccessLevel.GameMaster;
        Assert.True(ClusterFSchoolGate.IsPermitted(p.Pm, SkillName.Necromancy));
        p.Pm.AccessLevel = AccessLevel.Player;
        var lich = new Lich();
        try
        {
            Assert.True(ClusterFSchoolGate.IsPermitted(lich, SkillName.Necromancy));
        }
        finally
        {
            lich.Delete();
        }

        ClusterFSchoolGate.Mode = "off";
        Assert.True(ClusterFSchoolGate.IsPermitted(p.Pm, SkillName.Necromancy));
    }

    // ---------------------------------------------------------------- 2

    private sealed class ConstantRandom : System.Random
    {
        protected override double Sample() => 0.0;
        public override double NextDouble() => 0.0;
        public override int Next() => 0;
        public override int Next(int maxValue) => 0;
        public override int Next(int minValue, int maxValue) => minValue;
        public override long NextInt64(long maxValue) => 0;
    }

    [Fact]
    public void NoGainBeforeThePermissionAndAGainAfter()
    {
        ShardTestHost.EnsureSkillChecks();
        var location = Mobile.SkillCheckLocationHandler;
        var directLocation = Mobile.SkillCheckDirectLocationHandler;
        var target = Mobile.SkillCheckTargetHandler;
        var directTarget = Mobile.SkillCheckDirectTargetHandler;
        Mobile.SkillCheckLocationHandler = SkillCheck.Mobile_SkillCheckLocation;
        Mobile.SkillCheckDirectLocationHandler = SkillCheck.Mobile_SkillCheckDirectLocation;
        Mobile.SkillCheckTargetHandler = SkillCheck.Mobile_SkillCheckTarget;
        Mobile.SkillCheckDirectTargetHandler = SkillCheck.Mobile_SkillCheckDirectTarget;
        ClusterFSkillGain.Initialize();
        BuiltInRng.Generator = new ConstantRandom();
        using var p = new Player(new Point3D(1000, 1000, 0));
        try
        {
            ClusterFSkillCaps.ApplyTo(p.Pm);
            for (var i = 0; i < 5; i++)
            {
                Assert.False(p.Pm.CheckSkill(SkillName.Necromancy, 0.0, 100.0));
            }

            Assert.Equal(0, p.Pm.Skills.Necromancy.BaseFixedPoint);

            ClusterFSchoolGate.Grant(p.Pm, "necromancy", "test");
            for (var i = 0; i < 5; i++)
            {
                p.Pm.CheckSkill(SkillName.Necromancy, 0.0, 100.0);
            }

            Assert.True(p.Pm.Skills.Necromancy.BaseFixedPoint > 0);
        }
        finally
        {
            BuiltInRng.Reset();
            Mobile.SkillCheckLocationHandler = location;
            Mobile.SkillCheckDirectLocationHandler = directLocation;
            Mobile.SkillCheckTargetHandler = target;
            Mobile.SkillCheckDirectTargetHandler = directTarget;
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheCapPolicyHoldsAnUnearnedSchoolAtItsBaseAndGrantPutsTheNormalCapBack()
    {
        using var p = new Player(new Point3D(1000, 1000, 0));
        p.Pm.Skills.Necromancy.BaseFixedPoint = 123; // a base from before the gate: kept, not raised
        ClusterFSkillCaps.ApplyTo(p.Pm);

        var normal = (int)Math.Round(ClusterFSkillCaps.IndividualSkillCap * 10);
        Assert.Equal(0, p.Pm.Skills.Chivalry.CapFixedPoint);
        Assert.Equal(123, p.Pm.Skills.Necromancy.CapFixedPoint);
        Assert.Equal(123, p.Pm.Skills.Necromancy.BaseFixedPoint);
        Assert.Equal(normal, p.Pm.Skills.Magery.CapFixedPoint);

        // NPC teaching finds no room under the held cap; after the permission the same lesson would teach.
        var trainer = new Mage();
        try
        {
            trainer.Skills.Chivalry.BaseFixedPoint = 1000;
            var points = 0;
            Assert.NotEqual(BaseCreature.TeachResult.Success, trainer.CheckTeachSkills(SkillName.Chivalry, p.Pm, 0, ref points, false));
            ClusterFSchoolGate.Grant(p.Pm, "chivalry", "test");
            points = 0;
            Assert.Equal(BaseCreature.TeachResult.Success, trainer.CheckTeachSkills(SkillName.Chivalry, p.Pm, 0, ref points, false));
            ClusterFSchoolGate.Revoke(p.Pm, "chivalry", "test");
        }
        finally
        {
            trainer.Delete();
        }

        Assert.True(ClusterFSchoolGate.Grant(p.Pm, "chivalry", "test"));
        Assert.Equal(normal, p.Pm.Skills.Chivalry.CapFixedPoint);
        Assert.Equal(0, p.Pm.Skills.Chivalry.BaseFixedPoint);
        Assert.Equal(123, p.Pm.Skills.Necromancy.CapFixedPoint); // the other school is still held
        Assert.False(ClusterFSchoolGate.Grant(p.Pm, "chivalry", "test")); // once
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ThePermissionIsPerCharacterKeptThroughDeathAndASaveAndBothSchoolsCanBeHeld()
    {
        using var p = new Player(new Point3D(1000, 1000, 0));
        using var other = new Player(new Point3D(1002, 1000, 0));
        ClusterFSchoolGate.Grant(p.Pm, "chivalry", "test");
        ClusterFSchoolGate.Grant(p.Pm, "necromancy", "test");

        p.Pm.Kill();
        p.Pm.Resurrect();
        ClusterFSkillCaps.ApplyTo(p.Pm);
        Assert.True(ClusterFSchoolGate.IsPermitted(p.Pm, SkillName.Chivalry));
        Assert.True(ClusterFSchoolGate.IsPermitted(p.Pm, SkillName.Necromancy));
        Assert.False(ClusterFSchoolGate.IsPermitted(other.Pm, SkillName.Chivalry));

        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        SchoolGateStore.Instance.Serialize(writer);
        writer.Flush();
        SchoolGateStore.Permissions.Clear();
        Assert.False(ClusterFSchoolGate.IsPermitted(p.Pm, SkillName.Chivalry));
        SchoolGateStore.Instance.Deserialize(new BufferReader(buffer));
        Assert.True(ClusterFSchoolGate.IsPermitted(p.Pm, SkillName.Chivalry));
        Assert.True(ClusterFSchoolGate.IsPermitted(p.Pm, SkillName.Necromancy));
        Assert.False(ClusterFSchoolGate.IsPermitted(other.Pm, SkillName.Necromancy));
        Assert.Equal("on", ClusterFSchoolGate.Mode);
    }

    // ---------------------------------------------------------------- 5

    private static void KillFor(InitiationFoe foe, Mobile killer) => foe.Damage(10000, killer);

    [Fact]
    public void APlaceOnTheWatchPlayedThroughGrantsChivalryAndTheBookOnce()
    {
        using var p = new Player(new Point3D(890, 1037, 32));
        var aveline = Npc(InitiationNpc.AvelineRole, ClusterFInitiation.Aveline);
        var qm = Npc(InitiationNpc.QuartermasterRole, ClusterFInitiation.Quartermaster);
        var sergeant = Npc(InitiationNpc.SergeantRole, ClusterFInitiation.Sergeant);
        using var helper = new Player(new Point3D(889, 1080, 11));
        try
        {
            var prog = ClusterFInitiation.Progress(p.Pm, ClusterFInitiation.Chivalry);
            ClusterFInitiation.Talk(p.Pm, aveline);
            ClusterFInitiation.OnButton(p.Pm, ClusterFInitiation.Chivalry, ClusterFInitiation.BtnAccept);
            Assert.Equal(ClusterFInitiation.CBundles, prog.Step);
            Assert.Equal(3, ClusterFInitiation.Tokens(p.Pm, InitiationToken.BundleRole).Count);

            // A lost bundle is replaced by Aveline.
            ClusterFInitiation.Tokens(p.Pm, InitiationToken.BundleRole)[0].Delete();
            ClusterFInitiation.Talk(p.Pm, aveline);
            Assert.Equal(3, ClusterFInitiation.Tokens(p.Pm, InitiationToken.BundleRole).Count);

            ClusterFInitiation.Talk(p.Pm, qm);
            Assert.Equal(ClusterFInitiation.CPatrol, prog.Step);

            p.Pm.MoveToWorld(new Point3D(889, 1079, 11), Map.Trammel);
            ClusterFInitiation.Talk(p.Pm, sergeant);
            Assert.Equal(ClusterFInitiation.CScavengers, prog.Step);
            Assert.Empty(ClusterFInitiation.Tokens(p.Pm, InitiationToken.BundleRole));
            var foes = ClusterFInitiation.FoesOf(p.Pm, InitiationFoe.ScavengerKind);
            Assert.Equal(3, foes.Count);

            // One killed by a helper alone gives no credit; the patrol raises a replacement.
            KillFor(foes[0], helper.Pm);
            Assert.Equal(0, prog.Count);
            ClusterFInitiation.Talk(p.Pm, sergeant);
            foes = ClusterFInitiation.FoesOf(p.Pm, InitiationFoe.ScavengerKind);
            Assert.Equal(3, foes.Count);

            foreach (var foe in foes)
            {
                KillFor(foe, p.Pm);
            }

            Assert.Equal(ClusterFInitiation.CTitheTalk, prog.Step);
            Assert.False(ClusterFSchoolGate.HasPermission(p.Pm, ClusterFSchoolGate.Find("chivalry")));

            // The tithe: an old balance is not proof; the balance must rise by 100 after the step began.
            p.Pm.MoveToWorld(new Point3D(890, 1037, 32), Map.Trammel);
            p.Pm.TithingPoints = 500;
            ClusterFInitiation.Talk(p.Pm, aveline);
            ClusterFInitiation.OnButton(p.Pm, ClusterFInitiation.Chivalry, ClusterFInitiation.BtnTithe);
            Assert.Equal(ClusterFInitiation.CTithe, prog.Step);
            ClusterFInitiation.Talk(p.Pm, aveline);
            Assert.Equal(ClusterFInitiation.CTithe, prog.Step);
            p.Pm.TithingPoints += 100; // what the ankh's tithing gump does with 100 gold
            ClusterFInitiation.Talk(p.Pm, aveline);
            Assert.Equal(ClusterFInitiation.CConfirm, prog.Step);

            // "Not yet" keeps the work; then learn, with a full pack: the permission now, the book when there is room.
            ClusterFInitiation.OnButton(p.Pm, ClusterFInitiation.Chivalry, ClusterFInitiation.BtnLater);
            Assert.Equal(ClusterFInitiation.CConfirm, prog.Step);
            var filler = new List<Item>();
            while (p.Pm.Backpack.TotalItems < p.Pm.Backpack.MaxItems)
            {
                var it = new Bandage();
                p.Pm.Backpack.DropItem(it);
                filler.Add(it);
            }

            ClusterFInitiation.OnButton(p.Pm, ClusterFInitiation.Chivalry, ClusterFInitiation.BtnLearn);
            Assert.True(ClusterFSchoolGate.HasPermission(p.Pm, ClusterFSchoolGate.Find("chivalry")));
            Assert.False(prog.KitGiven);
            Assert.Empty(Find<BookOfChivalry>(p.Pm));
            Assert.Equal(0, p.Pm.Skills.Chivalry.BaseFixedPoint);

            foreach (var it in filler)
            {
                it.Delete();
            }

            ClusterFInitiation.Talk(p.Pm, aveline);
            Assert.True(prog.KitGiven);
            Assert.Single(Find<BookOfChivalry>(p.Pm));
            ClusterFInitiation.Talk(p.Pm, aveline);
            ClusterFInitiation.OnButton(p.Pm, ClusterFInitiation.Chivalry, ClusterFInitiation.BtnLearn);
            Assert.Single(Find<BookOfChivalry>(p.Pm));
            Assert.Equal(ClusterFInitiation.CDone, prog.Step);
            Assert.Equal(0, p.Pm.Skills.Chivalry.BaseFixedPoint);
            Assert.Equal((int)Math.Round(ClusterFSkillCaps.IndividualSkillCap * 10), p.Pm.Skills.Chivalry.CapFixedPoint);
        }
        finally
        {
            aveline.Delete();
            qm.Delete();
            sergeant.Delete();
        }
    }

    [Fact]
    public void AFullTithingBalanceGivesOneHundredGoldToTheReliefFundInstead()
    {
        using var p = new Player(new Point3D(890, 1037, 32));
        var aveline = Npc(InitiationNpc.AvelineRole, ClusterFInitiation.Aveline);
        try
        {
            var prog = ClusterFInitiation.Progress(p.Pm, ClusterFInitiation.Chivalry);
            prog.Step = ClusterFInitiation.CTithe;
            p.Pm.TithingPoints = ClusterFInitiation.TitheCap;
            prog.Mark = p.Pm.TithingPoints;
            ClusterFInitiation.OnButton(p.Pm, ClusterFInitiation.Chivalry, ClusterFInitiation.BtnFund);
            Assert.Equal(ClusterFInitiation.CTithe, prog.Step); // no gold
            p.Pm.Backpack.DropItem(new Gold(150));
            ClusterFInitiation.OnButton(p.Pm, ClusterFInitiation.Chivalry, ClusterFInitiation.BtnFund);
            Assert.Equal(ClusterFInitiation.CConfirm, prog.Step);
            Assert.Equal(50, p.Pm.Backpack.GetAmount(typeof(Gold)));
        }
        finally
        {
            aveline.Delete();
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void NamesForTheLastDoorPlayedThroughGrantsNecromancyTheBookAndReagentsOnce()
    {
        using var p = new Player(ClusterFInitiation.Oren);
        var oren = Npc(InitiationNpc.OrenRole, new Point3D(ClusterFInitiation.Oren.X + 1, ClusterFInitiation.Oren.Y, ClusterFInitiation.Oren.Z));
        var sites = new List<InitiationSite>();
        try
        {
            for (var i = 0; i < ClusterFInitiation.GraveSpots.Length; i++)
            {
                var s = new InitiationSite(InitiationSite.GraveKind, i);
                s.MoveToWorld(ClusterFInitiation.GraveSpots[i], Map.Trammel);
                sites.Add(s);
            }

            var binding = new InitiationSite(InitiationSite.BindingKind, 0);
            binding.MoveToWorld(ClusterFInitiation.BindingSpot, Map.Trammel);
            sites.Add(binding);

            var prog = ClusterFInitiation.Progress(p.Pm, ClusterFInitiation.Necromancy);
            ClusterFInitiation.Talk(p.Pm, oren);
            ClusterFInitiation.OnButton(p.Pm, ClusterFInitiation.Necromancy, ClusterFInitiation.BtnAccept);
            Assert.Equal(ClusterFInitiation.NPlates, prog.Step);
            Assert.Single(ClusterFInitiation.Tokens(p.Pm, InitiationToken.RegisterRole));

            for (var i = 0; i < ClusterFInitiation.GraveSpots.Length; i++)
            {
                p.Pm.MoveToWorld(ClusterFInitiation.GraveSpots[i], Map.Trammel);
                ClusterFInitiation.UseSite(p.Pm, sites[i]);
                var robber = Assert.Single(ClusterFInitiation.FoesOf(p.Pm, InitiationFoe.RobberKind));
                ClusterFInitiation.UseSite(p.Pm, sites[i]); // still guarded: no plate, no second robber
                Assert.Single(ClusterFInitiation.FoesOf(p.Pm, InitiationFoe.RobberKind));
                Assert.Equal(i, prog.Count);
                KillFor(robber, p.Pm);
                ClusterFInitiation.UseSite(p.Pm, sites[i]);
                Assert.Equal(i + 1, prog.Count);
                ClusterFInitiation.UseSite(p.Pm, sites[i]); // a second search gives nothing
                Assert.Equal(i + 1, prog.Count);
            }

            Assert.Equal(3, ClusterFInitiation.Tokens(p.Pm, InitiationToken.PlateRole).Count);
            Assert.Equal(ClusterFInitiation.NReturn, prog.Step);

            p.Pm.MoveToWorld(ClusterFInitiation.Oren, Map.Trammel);
            ClusterFInitiation.Talk(p.Pm, oren);
            Assert.Equal(ClusterFInitiation.NBinding, prog.Step);
            Assert.Empty(ClusterFInitiation.Tokens(p.Pm, InitiationToken.PlateRole));

            p.Pm.MoveToWorld(ClusterFInitiation.BindingSpot, Map.Trammel);
            ClusterFInitiation.UseSite(p.Pm, binding);
            var guardian = Assert.Single(ClusterFInitiation.FoesOf(p.Pm, InitiationFoe.GuardianKind));
            KillFor(guardian, p.Pm);
            Assert.Equal(ClusterFInitiation.NConfirm, prog.Step);
            Assert.False(ClusterFSchoolGate.HasPermission(p.Pm, ClusterFSchoolGate.Find("necromancy")));

            p.Pm.MoveToWorld(ClusterFInitiation.Oren, Map.Trammel);
            p.Pm.Karma = 2000;
            ClusterFInitiation.Talk(p.Pm, oren);
            Assert.True(SaidContains("These spells lower your karma") || SaidContains("lower your karma when you cast them"));
            ClusterFInitiation.OnButton(p.Pm, ClusterFInitiation.Necromancy, ClusterFInitiation.BtnLater);
            Assert.Equal(ClusterFInitiation.NConfirm, prog.Step);
            ClusterFInitiation.OnButton(p.Pm, ClusterFInitiation.Necromancy, ClusterFInitiation.BtnLearn);
            Assert.True(ClusterFSchoolGate.HasPermission(p.Pm, ClusterFSchoolGate.Find("necromancy")));
            Assert.Equal(2000, p.Pm.Karma); // no forced karma change
            Assert.Equal(0, p.Pm.Skills.Necromancy.BaseFixedPoint);

            var book = Assert.Single(Find<NecromancerSpellbook>(p.Pm));
            Assert.Equal(ClusterFInitiation.NecroBookContent, book.Content);
            Assert.Equal(10, p.Pm.Backpack.GetAmount(typeof(PigIron)));
            Assert.Equal(10, p.Pm.Backpack.GetAmount(typeof(BatWing)));
            Assert.Equal(10, p.Pm.Backpack.GetAmount(typeof(GraveDust)));
            Assert.Equal(10, p.Pm.Backpack.GetAmount(typeof(DaemonBlood)));
            Assert.Equal(10, p.Pm.Backpack.GetAmount(typeof(NoxCrystal)));

            // Once: talking again and pressing learn again give nothing more.
            ClusterFInitiation.Talk(p.Pm, oren);
            ClusterFInitiation.OnButton(p.Pm, ClusterFInitiation.Necromancy, ClusterFInitiation.BtnLearn);
            Assert.Single(Find<NecromancerSpellbook>(p.Pm));
            Assert.Equal(10, p.Pm.Backpack.GetAmount(typeof(PigIron)));
            Assert.False(ClusterFSchoolGate.HasPermission(p.Pm, ClusterFSchoolGate.Find("chivalry")));
        }
        finally
        {
            oren.Delete();
            foreach (var s in sites)
            {
                s.Delete();
            }
        }
    }

    [Fact]
    public void PlacingTheTeachersTwicePlacesNothingTheSecondTime()
    {
        ClusterFInitiation.Clear();
        try
        {
            var first = ClusterFInitiation.Place();
            Assert.Equal("Initiation: placed 9 thing(s).", first[^1]);
            var second = ClusterFInitiation.Place();
            Assert.Equal("Initiation: placed 0 thing(s).", second[^1]);
        }
        finally
        {
            ClusterFInitiation.Clear();
        }
    }

    // ---------------------------------------------------------------- 7

    private static (SkillName, byte)[] S(params (SkillName, byte)[] s) => s;

    [Fact]
    public void CreationOffersFourTemplatesAndTheShorterCustomList()
    {
        string why;
        foreach (var removed in new[] { 4, 5, 6, 7 })
        {
            Assert.Null(ClusterFStartChoices.Resolve(removed, [], [45, 35, 10], Race.Human, true, out why));
            Assert.Contains("no longer offered", why);
        }

        var fighter = ClusterFStartChoices.Resolve(1, [], [10, 10, 10], Race.Human, true, out _);
        Assert.Equal("Fighter", fighter.Name);
        Assert.Equal(120, fighter.Skills.Sum(s => s.Value));
        Assert.Equal(90, fighter.Stats.Sum(b => b));
        Assert.Equal("Archer", ClusterFStartChoices.Resolve(8, [], [], Race.Human, true, out _).Name);
        var thrower = ClusterFStartChoices.Resolve(8, [], [], Race.Gargoyle, true, out _);
        Assert.Equal("Thrower", thrower.Name);
        Assert.Contains(thrower.Skills, s => s.Skill == SkillName.Throwing);
        Assert.DoesNotContain(thrower.Skills, s => s.Skill == SkillName.Archery);
        Assert.Equal("Mage", ClusterFStartChoices.Resolve(2, [], [], Race.Elf, true, out _).Name);
        Assert.Equal("Maker", ClusterFStartChoices.Resolve(3, [], [], Race.Human, true, out _).Name);
        foreach (var t in new[] { ClusterFStartChoices.Fighter, ClusterFStartChoices.Archer, ClusterFStartChoices.Thrower, ClusterFStartChoices.Mage, ClusterFStartChoices.Maker })
        {
            Assert.All(t.Skills, s => Assert.Equal(30, s.Value));
            Assert.DoesNotContain(t.Skills, s => ClusterFSchoolGate.Find(s.Skill) != null);
        }

        byte[] st = [45, 35, 10];
        Assert.NotNull(ClusterFStartChoices.Resolve(0, S((SkillName.Swords, 50), (SkillName.Tactics, 50), (SkillName.Healing, 20)), st, Race.Human, true, out _));
        Assert.NotNull(ClusterFStartChoices.Resolve(0, S((SkillName.Mining, 40), (SkillName.Blacksmith, 40), (SkillName.Tinkering, 20)), [40, 20, 20], Race.Human, false, out _));
        foreach (var off in new[] { SkillName.Necromancy, SkillName.Chivalry, SkillName.Bushido, SkillName.Ninjitsu, SkillName.Mysticism, SkillName.Imbuing, SkillName.SpiritSpeak, SkillName.Alchemy })
        {
            Assert.Null(ClusterFStartChoices.Resolve(0, S((off, 50), (SkillName.Tactics, 50), (SkillName.Healing, 20)), st, Race.Human, true, out why));
            Assert.Contains("not on the starting list", why);
        }

        Assert.Null(ClusterFStartChoices.Resolve(0, S((SkillName.Swords, 60), (SkillName.Tactics, 40), (SkillName.Healing, 20)), st, Race.Human, true, out why));
        Assert.Contains("over 50", why);
        Assert.Null(ClusterFStartChoices.Resolve(0, S((SkillName.Swords, 50), (SkillName.Swords, 50), (SkillName.Healing, 20)), st, Race.Human, true, out why));
        Assert.Contains("twice", why);
        Assert.Null(ClusterFStartChoices.Resolve(0, S((SkillName.Swords, 50), (SkillName.Tactics, 50), (SkillName.Healing, 30)), st, Race.Human, true, out why));
        Assert.Contains("not 120", why);
        Assert.Null(ClusterFStartChoices.Resolve(0, S((SkillName.Swords, 50), (SkillName.Tactics, 50), (SkillName.Healing, 20)), st, Race.Human, false, out why));
        Assert.Contains("not 100", why); // a legacy client's 120 is over budget
        Assert.Null(ClusterFStartChoices.Resolve(0, S((SkillName.Archery, 50), (SkillName.Tactics, 50), (SkillName.Healing, 20)), st, Race.Gargoyle, true, out why));
        Assert.Contains("weapon", why);
        Assert.Null(ClusterFStartChoices.Resolve(0, S((SkillName.Throwing, 50), (SkillName.Tactics, 50), (SkillName.Healing, 20)), st, Race.Human, true, out why));
        Assert.Null(ClusterFStartChoices.Resolve(0, S((SkillName.Swords, 50), (SkillName.Tactics, 50), (SkillName.Healing, 20)), [60, 60, 10], Race.Human, true, out why));
        Assert.Contains("stats", why);
    }

    private static bool _startupHooksRun;

    private static void EnsureStartupHooks()
    {
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }
    }

    [Fact]
    public void ARefusedCreationMakesNoCharacterAndAFighterStartsWithItsTemplate()
    {
        EnsureStartupHooks();
        var account = new Account($"p714c{Guid.NewGuid():N}"[..16], "p714-test-only");
        using var ns = PacketTestUtilities.CreateTestNetState();
        var city = new CityInfo("New Haven", "The Bountiful Harvest Inn", 1150168, 3503, 2574, 14, Map.Trammel);
        Mobile made = null;
        try
        {
            var before = World.Mobiles.Count;
            var paladin = new CharacterCreatedEventArgs(ns, account, "Paladinwish", false, 0, [45, 20, 25], city, [],
                0, 0, 0, 0, 0, 0, ClusterFStartChoices.PaladinId, Race.Human);
            CharacterCreation.CharacterCreatedEvent(paladin);
            Assert.Null(paladin.Mobile);
            Assert.Null(account[0]);
            Assert.Equal(before, World.Mobiles.Count);

            var necroCustom = new CharacterCreatedEventArgs(ns, account, "Necrowish", false, 0, [30, 30, 30], city,
                ns.NewCharacterCreation
                    ? [(SkillName.Necromancy, 50), (SkillName.SpiritSpeak, 50), (SkillName.Meditation, 20), (SkillName.Alchemy, 0)]
                    : [(SkillName.Necromancy, 50), (SkillName.SpiritSpeak, 30), (SkillName.Meditation, 20)],
                0, 0, 0, 0, 0, 0, 0, Race.Human);
            CharacterCreation.CharacterCreatedEvent(necroCustom);
            Assert.Null(necroCustom.Mobile);
            Assert.Null(account[0]);

            var fighter = new CharacterCreatedEventArgs(ns, account, "Fighterok", false, 0, [10, 10, 10], city, [],
                0, 0, 0, 0, 0, 0, ClusterFStartChoices.FighterId, Race.Human);
            CharacterCreation.CharacterCreatedEvent(fighter);
            made = fighter.Mobile;
            Assert.NotNull(made);
            Assert.Equal(300, made.Skills.Swords.BaseFixedPoint);
            Assert.Equal(300, made.Skills.Healing.BaseFixedPoint);
            Assert.Equal(0, made.Skills.Chivalry.BaseFixedPoint);
            Assert.Equal(45, made.RawStr);
            Assert.Equal(35, made.RawDex);
            Assert.Equal(10, made.RawInt);
            Assert.False(ClusterFSchoolGate.HasPermission(made, ClusterFSchoolGate.Find("chivalry")));
        }
        finally
        {
            made?.Delete();
            Accounts.Remove(account);
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void BeforeInitiationASchoolLessonIsADemonstrationOnlyAndAfterItTheLessonRuns()
    {
        ClusterFTutorial.Enabled = true;
        TutorialCoreTrials.EnsureHooks();
        TutorialWingTrials.EnsureHooks();
        TutorialWingTrials.Fits = _ => true;
        using var p = new Player(new Point3D(TutorialWingTrials.Yard.X, TutorialWingTrials.Yard.Y, 0));
        Assert.True(ClusterFTutorial.TryEnter(p.Pm));
        p.Pm.TithingPoints = 7;

        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("magic.chivalry")!);
        Assert.Null(TutorialWingTrials.Current(p.Pm));
        Assert.Equal(7, p.Pm.TithingPoints);
        Assert.Empty(Find<BookOfChivalry>(p.Pm));
        Assert.DoesNotContain(p.Pm.SkillMods ?? new List<SkillMod>(), m => m.Skill == SkillName.Chivalry);
        Assert.True(ClusterFSchoolGate.ProgressOf(p.Pm, "chivalry").IntroViewed);
        Assert.False(ClusterFSchoolGate.HasPermission(p.Pm, ClusterFSchoolGate.Find("chivalry")));
        Assert.DoesNotContain("magic.chivalry", ClusterFTutorial.RecordOf(p.Pm)!.StationsDone);

        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("magic.necromancy")!);
        Assert.Null(TutorialWingTrials.Current(p.Pm));
        Assert.Empty(Find<NecromancerSpellbook>(p.Pm));

        ClusterFSchoolGate.Grant(p.Pm, "chivalry", "test");
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("magic.chivalry")!);
        var trial = TutorialWingTrials.Current(p.Pm);
        Assert.NotNull(trial);
        Assert.True(trial!.Armed);
        Assert.Equal(107, p.Pm.TithingPoints);
        TutorialWingTrials.Cancel(p.Pm);
    }

    [Fact]
    public void SkippingTheTutorialGrantsNeitherSchool()
    {
        ClusterFTutorial.Enabled = true;
        TutorialCoreTrials.EnsureHooks();
        using var p = new Player(new Point3D(TutorialWingTrials.Yard.X, TutorialWingTrials.Yard.Y, 0));
        Assert.True(ClusterFTutorial.TryEnter(p.Pm));
        _said.Clear();
        ClusterFTutorial.Skip(p.Pm);
        Assert.False(ClusterFSchoolGate.HasPermission(p.Pm, ClusterFSchoolGate.Find("chivalry")));
        Assert.False(ClusterFSchoolGate.HasPermission(p.Pm, ClusterFSchoolGate.Find("necromancy")));
        Assert.False(ClusterFSchoolGate.IsPermitted(p.Pm, SkillName.Chivalry));
        Assert.False(ClusterFSchoolGate.IsPermitted(p.Pm, SkillName.Necromancy));
    }
}
