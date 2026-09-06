using Server.Items;

namespace Server.Mobiles
{
    /// <summary>
    /// Undead warlord and quest target for Cleansing Old Haven.
    /// </summary>
    [CorpseName("Drelgor's corpse")]
    public class DrelgorTheImpaler : BaseCreature
    {
        [Constructable]
        public DrelgorTheImpaler()
            : base(AIType.AI_Melee, FightMode.Closest, 10, 1)
        {
            Name = "Drelgor the Impaler";
            Body = 0x190;
            Hue = 0x8421;
            BaseSoundID = 0x480;

            SetStr(180, 210);
            SetDex(80, 100);
            SetInt(80, 100);
            SetHits(450, 550);
            SetStam(80, 100);
            SetMana(80, 100);
            SetDamage(16, 24);
            SetDamageType(ResistanceType.Physical, 70);
            SetDamageType(ResistanceType.Cold, 30);
            SetResistance(ResistanceType.Physical, 40, 50);
            SetResistance(ResistanceType.Fire, 20, 30);
            SetResistance(ResistanceType.Cold, 55, 65);
            SetResistance(ResistanceType.Poison, 30, 40);
            SetResistance(ResistanceType.Energy, 30, 40);
            SetSkill(SkillName.Swords, 90.0, 100.0);
            SetSkill(SkillName.Tactics, 85.0, 95.0);
            SetSkill(SkillName.MagicResist, 75.0, 85.0);
            SetSkill(SkillName.Wrestling, 70.0, 80.0);
            SetSkill(SkillName.Anatomy, 70.0, 80.0);
            Fame = 8000;
            Karma = -8000;
            VirtualArmor = 45;

            AddItem(new PlateChest { Movable = false, Hue = 0x835 });
            AddItem(new PlateLegs { Movable = false, Hue = 0x835 });
            AddItem(new PlateArms { Movable = false, Hue = 0x835 });
            AddItem(new PlateGloves { Movable = false, Hue = 0x835 });
            AddItem(new PlateHelm { Movable = false, Hue = 0x835 });
            AddItem(new VikingSword { Movable = false, Hue = 0x835 });
            AddItem(new MetalShield { Movable = false, Hue = 0x835 });
        }

        public DrelgorTheImpaler(Serial serial)
            : base(serial)
        {
        }

        public override bool AlwaysMurderer { get { return true; } }
        public override bool ShowFameTitle { get { return false; } }
        public override bool IgnoreYoungProtection { get { return true; } }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Rich);
            AddLoot(LootPack.Average);
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.WriteEncodedInt(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadEncodedInt();
        }
    }
}
