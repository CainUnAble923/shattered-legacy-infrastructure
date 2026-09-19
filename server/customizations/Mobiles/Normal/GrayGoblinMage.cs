// ServUO: Mobiles/Normal/GrayGoblinMage.cs (CC6 batch 1). Values verbatim; serialization by the generator; ServUO's
// version-0 body/hue upgrade path in Deserialize dropped (new content starts at version 0).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class GrayGoblinMage : BaseCreature
{
    [Constructible]
    public GrayGoblinMage() : base(AIType.AI_Mage)
    {
        Body = 723;
        Hue = 1900;
        BaseSoundID = 0x600;

        SetStr(227, 285);
        SetDex(70, 88);
        SetInt(451, 499);

        SetHits(129, 151);
        SetStam(70, 88);
        SetMana(451, 499);

        SetDamage(5, 7);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 40, 50);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 25, 32);
        SetResistance(ResistanceType.Poison, 10, 19);
        SetResistance(ResistanceType.Energy, 10, 20);

        SetSkill(SkillName.MagicResist, 141.9, 147.1);
        SetSkill(SkillName.Tactics, 80.7, 86.9);
        SetSkill(SkillName.Anatomy, 81.9, 89.4);
        SetSkill(SkillName.Wrestling, 90.5, 104.2);
        SetSkill(SkillName.Magery, 105.5, 119.1);
        SetSkill(SkillName.EvalInt, 94.9, 107.7);

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

    public override string CorpseName => "a goblin mage corpse";
    public override string DefaultName => "a gray goblin mage";

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
