using ModernUO.Serialization;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class PlagueRat : BaseCreature
    {
        [Constructible]
        public PlagueRat() : base(AIType.AI_Melee)
        {
            Body = 0xD7;
            Hue = 1710;
            BaseSoundID = 0x188;

            SetStr(59);
            SetDex(51);
            SetInt(17);

            SetHits(92);
            SetMana(0);

            SetDamage(4, 8);

            SetDamageType(ResistanceType.Physical, 100);

            SetResistance(ResistanceType.Physical, 35, 40);
            SetResistance(ResistanceType.Fire, 20, 25);
            SetResistance(ResistanceType.Cold, 25, 35);
            SetResistance(ResistanceType.Energy, 35, 40);

            SetSkill(SkillName.MagicResist, 25.1, 30.0);
            SetSkill(SkillName.Tactics, 34.5, 40.0);
            SetSkill(SkillName.Wrestling, 40.5, 45.0);

            Fame = 300;
            Karma = -300;

            VirtualArmor = 38;
        }

        public override string CorpseName => "a plague rat corpse";
        public override string DefaultName => "a Clan Ribbon Plague Rat";

        public override int Meat => 1;
        public override int Hides => 6;

        public override FoodType FavoriteFood =>
            FoodType.Fish | FoodType.Meat | FoodType.FruitsAndVeggies | FoodType.Eggs;

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Rich);
        }
    }
}
