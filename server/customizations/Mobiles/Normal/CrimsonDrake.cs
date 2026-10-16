// ServUO: Mobiles/Normal/CrimsonDrake.cs (cc-P129). Values verbatim; serialization by the generator.
//
// The element is the one persisted field: ServUO's m_Type becomes [SerializableField(0)] _elementType, whose generated
// ElementType property is IElementalCreature's member. ServUO's two [Constructable] constructors are kept (the
// parameterless one picks a random element; a default parameter cannot express that, Turkey's shape in cc-P119).
// SetSpecialAbility(DragonBreath): no stock breath, so the profile's pinned twin fires the default definition.
// TrainingDefinition (the poison drake's training table) is not ported: training is Pet Training part 2 (P130).
// DragonBlood => 8 dropped: no property, no item (port recipe).

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class CrimsonDrake : BaseCreature, IElementalCreature
{
    [SerializableField(0, setter: "private")]
    private ElementType _elementType;

    [Constructible]
    public CrimsonDrake() : this((ElementType)Utility.Random(5))
    {
    }

    [Constructible]
    public CrimsonDrake(ElementType type) : base(AIType.AI_Melee)
    {
        _elementType = type;

        switch (type)
        {
            case ElementType.Physical:
                Body = 0x58B;
                Hue = 0;
                SetDamageType(ResistanceType.Physical, 100);
                break;
            case ElementType.Fire:
                Body = 0x58C;
                Hue = 33929;
                SetDamageType(ResistanceType.Physical, 0);
                SetDamageType(ResistanceType.Fire, 100);
                break;
            case ElementType.Cold:
                Body = 0x58C;
                Hue = 34134;
                SetDamageType(ResistanceType.Physical, 0);
                SetDamageType(ResistanceType.Cold, 100);
                break;
            case ElementType.Poison:
                Body = 0x58C;
                Hue = 34136;
                SetDamageType(ResistanceType.Physical, 0);
                SetDamageType(ResistanceType.Poison, 100);
                break;
            case ElementType.Energy:
                Body = 0x58C;
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
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 30, 40);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 30, 50);

        SetSkill(SkillName.MagicResist, 65.1, 80.0);
        SetSkill(SkillName.Tactics, 65.1, 90.0);
        SetSkill(SkillName.Wrestling, 65.1, 80.0);
        SetSkill(SkillName.DetectHidden, 50.0, 60.0);
        SetSkill(SkillName.Focus, 5.0, 10.0);

        Fame = 5500;
        Karma = -5500;

        VirtualArmor = 46;

        Tamable = true;
        ControlSlots = 2;
        MinTameSkill = 85.0;

        PackReg(3);
        SetSpecialAbility(SpecialAbility.DragonBreath);
    }

    // cc-P130: ServUO CrimsonDrake.cs:98-117, a poison drake's own training definition: the table's row with
    // AreaEffectArea2 in place of AreaEffectArea1 (PetTrainingHelper.GetTrainingDefinition asks the creature first).
    private static TrainingDefinition _poisonDrakeDefinition;

    public override TrainingDefinition TrainingDefinition =>
        ElementType == ElementType.Poison
            ? _poisonDrakeDefinition ??= new TrainingDefinition(
                typeof(CrimsonDrake), Class.None, MagicalAbility.Dragon2, PetTrainingHelper.SpecialAbilityNone,
                PetTrainingHelper.WepAbility2, PetTrainingHelper.AreaEffectArea2, 2, 5
            )
            : base.TrainingDefinition;

    public override string CorpseName => "a crimson drake corpse";
    public override string DefaultName => "Crimson Drake";

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
