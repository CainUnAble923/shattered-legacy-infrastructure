// ServUO: Mobiles/Normal/Kepetch.cs (CC6 batch 2). Values verbatim; serialization by the generator; the one
// persisted bool (GatheredFur, ServUO version 2) is a [SerializableField]. ServUO sets no VirtualArmor.
// Dropped: SetSpecialAbility(ViciousBite) (D-54: Pet Training, declined B3, the Saurosaurus precedent);
// DragonBlood => 8 (D-50). ICarvable.Carve is void here (Server/Interfaces.cs:16-19), bool in ServUO.
// Fur and FurType are this type's own members (Mobiles/FurType.cs explains why).

using ModernUO.Serialization;
using Server.Items;
using Server.Network;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Kepetch : BaseCreature, ICarvable
{
    [SerializableField(0, setter: "private")]
    private bool _gatheredFur;

    [Constructible]
    public Kepetch() : base(AIType.AI_Melee)
    {
        Body = 726;

        SetStr(337, 380);
        SetDex(184, 194);
        SetInt(30, 50);

        SetHits(300, 400);

        SetDamage(7, 17);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 55, 75);
        SetResistance(ResistanceType.Fire, 40, 60);
        SetResistance(ResistanceType.Cold, 40, 50);
        SetResistance(ResistanceType.Poison, 50, 70);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.Anatomy, 119.7, 124.1);
        SetSkill(SkillName.MagicResist, 89.9, 97.4);
        SetSkill(SkillName.Tactics, 117.4, 123.5);
        SetSkill(SkillName.Wrestling, 107.7, 113.9);
        SetSkill(SkillName.DetectHidden, 25.0);
        SetSkill(SkillName.Parry, 60.0, 70.0);

        Fame = 6000;
        Karma = -6000;
    }

    public override string CorpseName => "a kepetch corpse";
    public override string DefaultName => "a kepetch";

    public override int Meat => 5;
    public override int Hides => 14;
    public override HideType HideType => HideType.Spined;
    public override FoodType FavoriteFood => FoodType.FruitsAndVeggies | FoodType.GrainsAndHay;

    // ServUO: public override int Fur / FurType on BaseCreature. Here they are the creature's own.
    public int Fur => _gatheredFur ? 0 : 15;
    public FurType FurType => FurType.Brown;

    public void Carve(Mobile from, Item item)
    {
        if (!_gatheredFur)
        {
            var fur = new Fur(FurType, Fur);

            if (from.Backpack == null || !from.Backpack.TryDropItem(from, fur, false))
            {
                from.SendLocalizedMessage(1112359); // You would not be able to place the gathered kepetch fur in your backpack!
                fur.Delete();
            }
            else
            {
                from.SendLocalizedMessage(1112360); // You place the gathered kepetch fur into your backpack.
                GatheredFur = true;
            }
        }
        else
        {
            PrivateOverheadMessage(MessageType.Regular, 0x3B2, 1112358, from.NetState); // The Kepetch nimbly escapes your attempts to shear its mane.
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average, 2);
    }

    public override int GetIdleSound() => 1545;
    public override int GetAngerSound() => 1542;
    public override int GetHurtSound() => 1544;
    public override int GetDeathSound() => 1543;
}
