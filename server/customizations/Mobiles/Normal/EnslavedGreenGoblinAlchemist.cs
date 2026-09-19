// ServUO: Mobiles/Normal/EnslavedGreenGoblinAlchemist.cs (CC6 batch 1). Values verbatim (including the corpse name's article and the
// fixed min == max stat ranges); serialization by the generator. ServUO names this one
// "Green Goblin Alchemist" with no "Enslaved", and gives it the free goblins' body 723 rather than 334; both kept.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class EnslavedGreenGoblinAlchemist : BaseCreature
{
    [Constructible]
    public EnslavedGreenGoblinAlchemist() : base(AIType.AI_Melee)
    {
        Body = 723;
        BaseSoundID = 0x600;

        SetStr(289, 289);
        SetDex(72, 72);
        SetInt(113, 113);

        SetHits(196, 196);
        SetStam(72, 72);
        SetMana(113, 113);

        SetDamage(5, 7);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 45, 49);
        SetResistance(ResistanceType.Fire, 50, 53);
        SetResistance(ResistanceType.Cold, 25, 30);
        SetResistance(ResistanceType.Poison, 40, 42);
        SetResistance(ResistanceType.Energy, 15, 18);

        SetSkill(SkillName.MagicResist, 124.1, 126.2);
        SetSkill(SkillName.Tactics, 75.3, 83.6);
        SetSkill(SkillName.Anatomy, 0.0, 0.0);
        SetSkill(SkillName.Wrestling, 90.4, 94.7);

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
    public override string DefaultName => "Green Goblin Alchemist";

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
