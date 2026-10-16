// ServUO: Mobiles/Normal/PlatinumDrake.cs (cc-P129). Values verbatim; serialization by the generator.
// CrimsonDrake's shape: the element is [SerializableField(0)] _elementType, two [Constructible] constructors are kept,
// TrainingDefinition is P130's, DragonBlood is dropped. This drake calls nothing in Pet Training's ability half.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class PlatinumDrake : BaseCreature, IElementalCreature
{
    [SerializableField(0, setter: "private")]
    private ElementType _elementType;

    [Constructible]
    public PlatinumDrake() : this((ElementType)Utility.Random(5))
    {
    }

    [Constructible]
    public PlatinumDrake(ElementType type) : base(AIType.AI_Melee)
    {
        _elementType = type;

        switch (type)
        {
            case ElementType.Physical:
                Body = 0x589;
                Hue = 0;
                SetDamageType(ResistanceType.Physical, 100);
                break;
            case ElementType.Fire:
                Body = 0x58A;
                Hue = 33929;
                SetDamageType(ResistanceType.Physical, 0);
                SetDamageType(ResistanceType.Fire, 100);
                break;
            case ElementType.Cold:
                Body = 0x58A;
                Hue = 34134;
                SetDamageType(ResistanceType.Physical, 0);
                SetDamageType(ResistanceType.Cold, 100);
                break;
            case ElementType.Poison:
                Body = 0x58A;
                Hue = 34136;
                SetDamageType(ResistanceType.Physical, 0);
                SetDamageType(ResistanceType.Poison, 100);
                break;
            case ElementType.Energy:
                Body = 0x58A;
                Hue = 34141;
                SetDamageType(ResistanceType.Physical, 0);
                SetDamageType(ResistanceType.Energy, 100);
                break;
        }

        Female = true;
        BaseSoundID = 362;

        SetStr(400, 430);
        SetDex(133, 152);
        SetInt(101, 140);

        SetHits(241, 258);

        SetDamage(11, 17);

        SetResistance(ResistanceType.Physical, 30, 50);
        SetResistance(ResistanceType.Fire, 30, 50);
        SetResistance(ResistanceType.Cold, 30, 50);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 30, 50);

        SetSkill(SkillName.MagicResist, 65.1, 80.0);
        SetSkill(SkillName.Tactics, 65.1, 90.0);
        SetSkill(SkillName.Wrestling, 65.1, 80.0);
        SetSkill(SkillName.DetectHidden, 50.0, 60.0);
        SetSkill(SkillName.Focus, 5.0, 20.0);

        Fame = 5500;
        Karma = -5500;

        VirtualArmor = 46;

        Tamable = true;
        ControlSlots = 2;
        MinTameSkill = 85.0;

        PackReg(3);
    }

    public override string CorpseName => "a platinum drake corpse";
    public override string DefaultName => "Platinum Drake";

    public override bool ReacquireOnMovement => !Controlled;
    public override int TreasureMapLevel => 2;
    public override int Meat => 10;
    public override int Hides => 22;
    public override HideType HideType => HideType.Horned;
    public override int Scales => 2;
    public override ScaleType ScaleType => ScaleType.Black;
    public override FoodType FavoriteFood => FoodType.Meat | FoodType.Fish;
    public override bool CanFly => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
        AddLoot(LootPack.MedScrolls, 2);
    }
}
