// ServUO: Mobiles/Normal/RuddyBoura.cs (CC6 batch 2). Values verbatim; serialization by the generator; the one
// persisted bool (GatheredFur, ServUO version 2) is a [SerializableField].
// Dropped: SetSpecialAbility(ColossalBlow) (D-51: Pet Training, declined B3, the Saurosaurus precedent); DragonBlood => 8 (D-50:
// pinned BaseCreature has no DragonBlood virtual and its corpse carve yields no dragon's blood).
// ICarvable.Carve is void here (Server/Interfaces.cs:16-19), bool in ServUO; the return paths become plain
// returns. BladedItemTarget dispatches it (UOContent/Targets/BladedItemTarget.cs:34), as ServUO's does.
// Fur and FurType are this type's own members (Mobiles/FurType.cs explains why), not BaseCreature overrides.

using ModernUO.Serialization;
using Server.Items;
using Server.Network;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class RuddyBoura : BaseCreature, ICarvable
{
    [SerializableField(0, setter: "private")]
    private bool _gatheredFur;

    [Constructible]
    public RuddyBoura() : base(AIType.AI_Animal, FightMode.Aggressor)
    {
        Body = 715;

        SetStr(396, 480);
        SetDex(68, 82);
        SetInt(16, 20);

        SetHits(435, 509);
        SetStam(68, 82);
        SetMana(16, 20);

        SetDamage(16, 20);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 35, 40);
        SetResistance(ResistanceType.Cold, 10, 20);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 30, 40);

        SetSkill(SkillName.Anatomy, 86.6, 88.8);
        SetSkill(SkillName.MagicResist, 69.7, 87.7);
        SetSkill(SkillName.Tactics, 83.3, 88.8);
        SetSkill(SkillName.Wrestling, 86.6, 87.9);

        Tamable = true;
        ControlSlots = 2;
        MinTameSkill = 19.1;

        Fame = 5000;
        Karma = -2500;

        VirtualArmor = 16;
    }

    public override string CorpseName => "a boura corpse";
    public override string DefaultName => "a ruddy boura";

    public override int Meat => 10;
    public override int Hides => 20;
    public override HideType HideType => HideType.Spined;
    public override FoodType FavoriteFood => FoodType.FruitsAndVeggies;

    // ServUO: public override int Fur / FurType on BaseCreature. Here they are the creature's own.
    public int Fur => _gatheredFur ? 0 : 30;
    public FurType FurType => FurType.LightBrown;

    public void Carve(Mobile from, Item item)
    {
        if (!_gatheredFur)
        {
            var fur = new Fur(FurType, Fur);

            if (from.Backpack == null || !from.Backpack.TryDropItem(from, fur, false))
            {
                from.SendLocalizedMessage(1112352); // You would not be able to place the gathered boura fur in your backpack!
                fur.Delete();
            }
            else
            {
                from.SendLocalizedMessage(1112353); // You place the gathered boura fur into your backpack.
                GatheredFur = true;
            }
        }
        else
        {
            PrivateOverheadMessage(MessageType.Regular, 0x3B2, 1112354, from.NetState); // The boura glares at you and will not let you shear its fur.
        }
    }

    public override int GetIdleSound() => 1507;
    public override int GetAngerSound() => 1504;
    public override int GetHurtSound() => 1506;
    public override int GetDeathSound() => 1505;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (!Controlled)
        {
            c.DropItem(new BouraSkin());
        }
    }
}
