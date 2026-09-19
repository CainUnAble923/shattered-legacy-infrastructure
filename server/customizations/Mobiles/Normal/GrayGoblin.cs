// ServUO: Mobiles/Normal/GrayGoblin.cs (CC6 batch 1). Values verbatim; serialization by the generator; ServUO's
// version-0 body/hue upgrade path in Deserialize dropped (new content starts at version 0).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class GrayGoblin : BaseCreature
{
    [Constructible]
    public GrayGoblin() : base(AIType.AI_Melee)
    {
        Body = 723;
        Hue = 1900;
        BaseSoundID = 0x600;

        SetStr(258, 327);
        SetDex(62, 80);
        SetInt(103, 150);

        SetHits(159, 194);
        SetStam(62, 80);
        SetMana(103, 150);

        SetDamage(5, 7);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 40, 50);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 25, 32);
        SetResistance(ResistanceType.Poison, 10, 19);
        SetResistance(ResistanceType.Energy, 10, 20);

        SetSkill(SkillName.MagicResist, 120.9, 129.1);
        SetSkill(SkillName.Tactics, 80.6, 89.4);
        SetSkill(SkillName.Anatomy, 80.3, 89.4);
        SetSkill(SkillName.Wrestling, 96.1, 105.5);

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

    public override string CorpseName => "a goblin corpse";
    public override string DefaultName => "a gray goblin";

    public override int GetAngerSound() => 0x600;
    public override int GetIdleSound() => 0x600;
    public override int GetAttackSound() => 0x5FD;
    public override int GetHurtSound() => 0x5FF;
    public override int GetDeathSound() => 0x5FE;

    public override bool CanRummageCorpses => true;
    public override int TreasureMapLevel => 1;
    public override int Meat => 1;
    // ServUO: TribeType.GrayGoblin. See Mobiles/AI/GoblinOppositionGroup.cs.
    public override OppositionGroup OppositionGroup => GoblinOppositionGroup.GrayAndGreenGoblins;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}
