using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class Allosaurus : BaseCreature
    {
        [Constructible]
        public Allosaurus() : base(AIType.AI_Melee)
        {
            Body = 1290;

            SetStr(699, 828);
            SetDex(200);
            SetInt(127, 150);

            SetDamage(21, 23);

            SetHits(18000);
            SetMana(48, 70);

            SetResistance(ResistanceType.Physical, 65, 75);
            SetResistance(ResistanceType.Fire, 55, 65);
            SetResistance(ResistanceType.Cold, 60, 70);
            SetResistance(ResistanceType.Poison, 90, 100);
            SetResistance(ResistanceType.Energy, 60, 70);

            SetDamageType(ResistanceType.Physical, 50);
            SetDamageType(ResistanceType.Fire, 50);

            SetSkill(SkillName.MagicResist, 100.0, 110.0);
            SetSkill(SkillName.Tactics, 120.0, 140.0);
            SetSkill(SkillName.Wrestling, 120.0, 150.0);
            SetSkill(SkillName.Poisoning, 50.0, 60.0);
            SetSkill(SkillName.Wrestling, 55.0, 65.0);
            SetSkill(SkillName.Parry, 80.0, 90.0);
            SetSkill(SkillName.Magery, 70.0, 80.0);
            SetSkill(SkillName.EvalInt, 75.0, 85.0);

            Fame = 21000;
            Karma = -21000;
        }

        public override string CorpseName => "an allosaurus corpse";
        public override string DefaultName => "an allosaurus";

        public override int Meat => 3;
        public override int Hides => 11;
        public override HideType HideType => HideType.Horned;
        public override int TreasureMapLevel => 7;

        // ServUO: three SetWeaponAbility calls, which push onto a PetTrainingHelper ability
        // profile. ModernUO's equivalent is this virtual, used by 49 stock creatures.
        public override WeaponAbility GetWeaponAbility() =>
            Utility.Random(3) switch
            {
                0 => WeaponAbility.ArmorPierce,
                1 => WeaponAbility.CrushingBlow,
                _ => WeaponAbility.Disarm
            };

        // ServUO branches this on IsChampionSpawn, which ModernUO's BaseCreature does not have.
        // The non-champion branch is the one that runs. See notes/cc3-eodon.md.
        public override void GenerateLoot()
        {
            AddLoot(LootPack.UltraRich, 3);
        }

        public override int GetIdleSound() => 0x2C4;
        public override int GetAttackSound() => 0x2C0;
        public override int GetDeathSound() => 0x2C1;
        public override int GetAngerSound() => 0x2C4;
        public override int GetHurtSound() => 0x2C3;
    }
}
