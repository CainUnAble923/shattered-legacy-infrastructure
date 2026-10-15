// ServUO: Mobiles/Normal/OrcChopper.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using ModernUO.Serialization;
using Server.Items;
using Server.Misc;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class OrcChopper : BaseCreature
{
    [Constructible]
    public OrcChopper()
        : base(AIType.AI_Melee)
    {
        Body = 7;
        BaseSoundID = 0x45A;
        Hue = 0x96D;

        SetStr(147, 245);
        SetDex(91, 115);
        SetInt(61, 85);

        SetHits(97, 139);

        SetDamage(4, 13);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 25, 35);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 15, 25);
        SetResistance(ResistanceType.Poison, 15, 25);
        SetResistance(ResistanceType.Energy, 25, 30);

        SetSkill(SkillName.MagicResist, 60.1, 85.0);
        SetSkill(SkillName.Tactics, 75.1, 90.0);
        SetSkill(SkillName.Wrestling, 60.1, 85.0);

        Fame = 4500;
        Karma = -4500;

        VirtualArmor = 54;

        PackItem(new Log(Utility.RandomMinMax(1, 10)));
        PackItem(new Board(Utility.RandomMinMax(10, 20)));
        PackItem(new ExecutionersAxe());

        // TODO: Skull?
        switch (Utility.Random(7))
        {
            case 0:
                PackItem(new Arrow());
                break;
            case 1:
                PackItem(new Lockpick());
                break;
            case 2:
                PackItem(new Shaft());
                break;
            case 3:
                PackItem(new Ribs());
                break;
            case 4:
                PackItem(new Bandage());
                break;
            case 5:
                PackItem(new BeverageBottle(BeverageType.Wine));
                break;
            case 6:
                PackItem(new Jug(BeverageType.Cider));
                break;
        }

        if (Core.AOS)
        {
            PackItem(Loot.RandomNecromancyReagent());
        }

        if (0.5 > Utility.RandomDouble())
        {
            PackItem(new Yeast());
        }
    }

    public override string CorpseName => "an orcish corpse";
    public override string DefaultName => "an orc chopper";

    public override InhumanSpeech SpeechType => InhumanSpeech.Orc;
    public override bool CanRummageCorpses => true;
    public override int Meat => 1;

    // ServUO's Tribe => TribeType.Orc is pinned's OppositionGroup, already given below (AGENTS.md, equivalents).

    public override OppositionGroup OppositionGroup => OppositionGroup.SavagesAndOrcs;

    // ServUO: SetWeaponAbility(WhirlwindAttack) and SetWeaponAbility(CrushingBlow), one picked per use.
    public override WeaponAbility GetWeaponAbility() =>
        Utility.RandomBool() ? WeaponAbility.WhirlwindAttack : WeaponAbility.CrushingBlow;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        c.DropItem(new DoubleAxe());

        if (Utility.RandomDouble() < 0.1)
        {
            c.DropItem(new EvilOrcHelm());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager, 2);
    }

    // Stock Orc's shape (pinned Mobiles/Monsters/Humanoid/Melee/Orc.cs), the same logic as ServUO's.
    public override bool IsEnemy(Mobile m) =>
        (!m.Player || m.FindItemOnLayer<OrcishKinMask>(Layer.Helm) == null) && base.IsEnemy(m);

    public override void AggressiveAction(Mobile aggressor, bool criminal)
    {
        base.AggressiveAction(aggressor, criminal);

        if (aggressor.FindItemOnLayer(Layer.Helm) is OrcishKinMask item)
        {
            AOS.Damage(aggressor, 50, 0, 100, 0, 0, 0);
            item.Delete();
            aggressor.FixedParticles(0x36BD, 20, 10, 5044, EffectLayer.Head);
            aggressor.PlaySound(0x307);
        }
    }
}
