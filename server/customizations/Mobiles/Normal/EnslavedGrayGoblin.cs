// ServUO: Mobiles/Normal/EnslavedGrayGoblin.cs (CC6 batch 1). Values verbatim (including the corpse name's article and the
// fixed min == max stat ranges); serialization by the generator.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class EnslavedGrayGoblin : BaseCreature
{
    [Constructible]
    public EnslavedGrayGoblin() : base(AIType.AI_Melee)
    {
        Body = 334;
        BaseSoundID = 0x600;

        SetStr(321, 321);
        SetDex(64, 64);
        SetInt(147, 147);

        SetHits(179, 179);
        SetStam(64, 64);
        SetMana(147, 147);

        SetDamage(5, 7);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50, 50);
        SetResistance(ResistanceType.Fire, 38, 38);
        SetResistance(ResistanceType.Cold, 32, 32);
        SetResistance(ResistanceType.Poison, 12, 12);
        SetResistance(ResistanceType.Energy, 11, 11);

        SetSkill(SkillName.MagicResist, 121.6, 121.6);
        SetSkill(SkillName.Tactics, 90.0, 90.0);
        SetSkill(SkillName.Anatomy, 82.0, 82.0);
        SetSkill(SkillName.Wrestling, 99.2, 99.2);

        Fame = 1500;
        Karma = -1500;

        VirtualArmor = 28;

        switch (Utility.Random(20))
        {
            case 0:
                PackItem(new Scimitar());
                break;
            case 1:
                PackItem(new Katana());
                break;
            case 2:
                PackItem(new WarMace());
                break;
            case 3:
                PackItem(new WarHammer());
                break;
            case 4:
                PackItem(new Kryss());
                break;
            case 5:
                PackItem(new Pitchfork());
                break;
        }

        PackItem(new ThighBoots());

        switch (Utility.Random(3))
        {
            case 0:
                PackItem(new Ribs());
                break;
            case 1:
                PackItem(new Shaft());
                break;
            case 2:
                PackItem(new Candle());
                break;
        }

        if (0.2 > Utility.RandomDouble())
        {
            PackItem(new BolaBall());
        }
    }

    public override string CorpseName => "an goblin corpse";
    public override string DefaultName => "Enslaved Gray Goblin";

    public override int GetAngerSound() => 0x600;
    public override int GetIdleSound() => 0x600;
    public override int GetAttackSound() => 0x5FD;
    public override int GetHurtSound() => 0x5FF;
    public override int GetDeathSound() => 0x5FE;

    public override bool CanRummageCorpses => true;
    public override int TreasureMapLevel => 1;
    public override int Meat => 1;
    // Inert on both emulators: neither list in SavagesAndOrcs names this type, so IsEnemy is never true
    // through it, and ServUO under Core.TOL consults Tribe (None here) rather than OppositionGroup at all.
    // Ported unchanged.
    public override OppositionGroup OppositionGroup => OppositionGroup.SavagesAndOrcs;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}
