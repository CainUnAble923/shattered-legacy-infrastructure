using ModernUO.Serialization;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class Najasaurus : BaseCreature
    {
        [Constructible]
        public Najasaurus() : base(AIType.AI_Melee)
        {
            Body = 1289;
            BaseSoundID = 219;

            SetStr(162, 346);
            SetDex(151, 218);
            SetInt(21, 40);

            SetDamage(13, 24);
            SetHits(737, 854);

            SetDamageType(ResistanceType.Physical, 50);
            SetDamageType(ResistanceType.Poison, 50);

            SetResistance(ResistanceType.Physical, 45, 55);
            SetResistance(ResistanceType.Fire, 50, 60);
            SetResistance(ResistanceType.Cold, 45, 55);
            SetResistance(ResistanceType.Poison, 100);
            SetResistance(ResistanceType.Energy, 35, 45);

            SetSkill(SkillName.MagicResist, 150.0, 190.0);
            SetSkill(SkillName.Tactics, 80.0, 95.0);
            SetSkill(SkillName.Wrestling, 80.0, 100.0);
            SetSkill(SkillName.Poisoning, 90.0, 100.0);
            SetSkill(SkillName.DetectHidden, 45.0, 55.0);

            Fame = 17000;
            Karma = -17000;

            Tamable = true;
            ControlSlots = 2;
            MinTameSkill = 102.0;
        }

        public override string CorpseName => "a najasaurus corpse";
        public override string DefaultName => "a najasaurus";

        public override Poison HitPoison => Poison.Lethal;
        public override Poison PoisonImmune => Poison.Lethal;
        public override bool CanAngerOnTame => true;
        public override int TreasureMapLevel => 2;

        // ServUO's only PetTrainingHelper call here is SetMagicalAbility(MagicalAbility.Poisoning)
        // inside the version-0 deserialize upgrade path, not the constructor. New content starts
        // at version 0 and never runs it, and the constructor already sets Poisoning skill and
        // HitPoison. Dropping it is a no-op on a fresh world.

        public override void GenerateLoot()
        {
            AddLoot(LootPack.FilthyRich);
        }
    }
}
