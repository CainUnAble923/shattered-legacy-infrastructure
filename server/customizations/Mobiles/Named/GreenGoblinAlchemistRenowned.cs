// ServUO: Mobiles/Named/GreenGoblinAlchemistRenowned.cs (CC6 batch 5). Values verbatim; serialization by the generator.
// Dropped: AllureImmune => true (no reader in pinned ModernUO; see GrayGoblinMageRenowned).

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class GreenGoblinAlchemistRenowned : BaseRenowned
{
    [Constructible]
    public GreenGoblinAlchemistRenowned() : base(AIType.AI_Melee)
    {
        Title = "[Renowned]";
        Body = 723;
        BaseSoundID = 0x600;

        SetStr(600, 650);
        SetDex(50, 70);
        SetInt(100, 250);

        SetHits(1000, 1500);

        SetDamage(5, 7);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50, 55);
        SetResistance(ResistanceType.Fire, 55, 60);
        SetResistance(ResistanceType.Cold, 40, 50);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 20, 25);

        SetSkill(SkillName.MagicResist, 120.0, 125.0);
        SetSkill(SkillName.Tactics, 95.0, 100.0);
        SetSkill(SkillName.Wrestling, 100.0, 110.0);

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

    public override string CorpseName => "Green Goblin Alchemist [Renowned] corpse";
    public override string DefaultName => "Green Goblin Alchemist";

    public override Type[] UniqueSAList => new[] { typeof(ObsidianEarrings), typeof(TheImpalersPick) };
    public override Type[] SharedSAList => Array.Empty<Type>();

    public override int GetAngerSound() => 0x600;
    public override int GetIdleSound() => 0x600;
    public override int GetAttackSound() => 0x5FD;
    public override int GetHurtSound() => 0x5FF;
    public override int GetDeathSound() => 0x5FE;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 2);
    }
}
