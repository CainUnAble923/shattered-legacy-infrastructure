// ServUO: Mobiles/Bosses/Navery/Navrey.cs (CC6 batch 8, Part C). Navrey Night-Eyes, the Underworld's spider boss.
// She is a plain BaseCreature: nothing here needs BasePeerless, and she is in this batch because she belongs with the
// Abyss tier, not because she was gated (batch 7 section 9 had her under Q-052 by mistake).
//
// The TYPE is named by pinned's spawn entry, "NavreyNightEyes" (Underworld.json), not ServUO's "Navrey": Q-056 is
// still open and this follows batch 7's recommendation (option 2, the spawn-data spelling, no alias) provisionally.
// If Q-056 says option 1, this becomes `Navrey` with [TypeAlias("Server.Mobiles.NavreyNightEyes")]; no save exists.
//
// What changed, each proved against D:\UO\ModernUO-pinned:
//   NavreysController m_Spawner        Navrey's Lair (ServUO Services/Underworld/Navrey's Lair, 651 lines) is not
//                                      ported. The serialized item reference, the OnNavreyKilled call and the
//                                      spawner-taking constructor are gone; she takes a parameterless constructor so
//                                      pinned's spawner can build her (ServUO's Navrey has none, and would fail on
//                                      the same entry). UsedPillars, a GM property the lair set, is kept as ServUO
//                                      has it: unserialized. D-83.
//   the Green With Envy eye hand-out   PlayerMobile.Quests / GreenWithEnvyQuest are S5's engine (Q-025); the loop is
//                                      dropped. EyeOfNavrey is ported beside her and nothing produces it. D-84.
//   SetSpecialAbility(Webbing)         pinned has no MonsterAbility that throws a web (D-82, with the batch's others).
//   TeleportChance => 0                no such virtual in pinned; MageAI.cs:19,215 keeps its own 5% (D-85).
//   Loot.MysticismScrollTypes          pinned ships the sixteen Mysticism scrolls (Items/Skill Items/Magical/Scrolls/
//                                      Mysticism) but Loot has no array for them and RandomScroll routes
//                                      SpellbookType.Mystic to the regular list, so ServUO's array is carried here.
//   Poison.Parasitic                   ServUO's static is GetPoison("DeadlyParasitic"); pinned names the kinds
//                                      individually (Misc/PoisonKinds.cs), so DeadlyParasitic.
//   ScrollOfTranscendence              pinned spells the class ScrollofTranscendence; same CreateRandom(min, max).
//   c.AddItem                          as written (Container.AddItem); the coin and artifact use DropItem as written.
//
// ServUO quirks kept: the scroll loop re-rolls its bound every iteration, and RandomScroll(0, Length, ...) can index
// one past the array and pack nothing (both bug-list section 3's kind).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class NavreyNightEyes : BaseCreature
{
    [CommandProperty(AccessLevel.GameMaster)]
    public bool UsedPillars { get; set; }

    private static readonly Type[] _artifact =
    {
        typeof(NightEyes),
        typeof(Tangle1)
    };

    // ServUO Misc/Loot.cs:306-311, m_MysticismScrollTypes, in its order.
    public static readonly Type[] MysticismScrollTypes =
    {
        typeof(NetherBoltScroll), typeof(HealingStoneScroll), typeof(PurgeMagicScroll), typeof(EagleStrikeScroll),
        typeof(AnimatedWeaponScroll), typeof(StoneFormScroll), typeof(SpellTriggerScroll), typeof(CleansingWindsScroll),
        typeof(BombardScroll), typeof(SpellPlagueScroll), typeof(HailStormScroll), typeof(NetherCycloneScroll),
        typeof(RisingColossusScroll), typeof(SleepScroll), typeof(MassSleepScroll), typeof(EnchantScroll)
    };

    [Constructible]
    public NavreyNightEyes() : base(AIType.AI_Mage)
    {
        Body = 735;
        BaseSoundID = 389;

        SetStr(1000, 1500);
        SetDex(200, 250);
        SetInt(150, 200);

        SetHits(30000, 35000);

        SetDamage(25, 40);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Fire, 25);
        SetDamageType(ResistanceType.Energy, 25);

        SetResistance(ResistanceType.Physical, 55, 65);
        SetResistance(ResistanceType.Fire, 45, 55);
        SetResistance(ResistanceType.Cold, 60, 70);
        SetResistance(ResistanceType.Poison, 100);
        SetResistance(ResistanceType.Energy, 65, 80);

        SetSkill(SkillName.Anatomy, 50.0, 80.0);
        SetSkill(SkillName.EvalInt, 90.0, 100.0);
        SetSkill(SkillName.Magery, 90.0, 100.0);
        SetSkill(SkillName.MagicResist, 100.0, 130.0);
        SetSkill(SkillName.Meditation, 80.0, 100.0);
        SetSkill(SkillName.Poisoning, 100.0);
        SetSkill(SkillName.Tactics, 90.0, 100.0);
        SetSkill(SkillName.Wrestling, 91.6, 98.2);

        Fame = 24000;
        Karma = -24000;

        VirtualArmor = 90;

        for (var i = 0; i < Utility.RandomMinMax(1, 3); i++)
        {
            // ServUO: Loot.RandomScroll(0, Loot.MysticismScrollTypes.Length, SpellbookType.Mystic)
            PackItem(Loot.Construct<SpellScroll>(MysticismScrollTypes, Utility.RandomMinMax(0, MysticismScrollTypes.Length)));
        }

        // ServUO: SetSpecialAbility(SpecialAbility.Webbing); D-82.
    }

    public override string CorpseName => "a navrey corpse";
    public override string DefaultName => "Navrey Night-Eyes";

    public override bool AlwaysMurderer => true;
    public override Poison PoisonImmune => Poison.DeadlyParasitic;
    public override Poison HitPoison => Poison.Lethal;
    public override int Meat => 1;

    public static void DistributeRandomArtifact(BaseCreature bc, Type[] typelist)
    {
        var random = Utility.Random(typelist.Length);
        var item = Loot.Construct(typelist[random]);
        DistributeArtifact(DemonKnight.FindRandomPlayer(bc), item);
    }

    public static void DistributeArtifact(Mobile to, Item artifact)
    {
        if (artifact == null)
        {
            return;
        }

        if (to != null)
        {
            var pack = to.Backpack;

            if (pack == null || !pack.TryDropItem(to, artifact, false))
            {
                to.BankBox.DropItem(artifact);
            }

            to.SendLocalizedMessage(502088); // A special gift has been placed in your backpack.
        }
        else
        {
            artifact.Delete();
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.AosSuperBoss, 3);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        // ServUO: if (m_Spawner != null) m_Spawner.OnNavreyKilled(); D-83.

        if (Utility.RandomBool())
        {
            c.AddItem(new UntranslatedAncientTome());
        }

        if (0.1 >= Utility.RandomDouble())
        {
            c.AddItem(ScrollofTranscendence.CreateRandom(30, 30));
        }

        if (0.1 >= Utility.RandomDouble())
        {
            c.AddItem(new TatteredAncientScroll());
        }

        if (Utility.RandomDouble() < 0.10)
        {
            c.DropItem(new LuckyCoin());
        }

        if (Utility.RandomDouble() < 0.025)
        {
            DistributeRandomArtifact(this, _artifact);
        }

        // ServUO: an EyeOfNavrey to every looting-rights holder on the Green With Envy quest (Vernix). D-84.
    }
}
