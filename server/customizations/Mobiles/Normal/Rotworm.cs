// ServUO: Mobiles/Normal/Rotworm.cs (CC6 batch 3). Values verbatim; serialization by the generator; the TypeAlias for
// the older "RotWorm" spelling is kept. ServUO's PackBodyPartOrBones (BaseCreature.cs:5658-5671) has no counterpart in
// pinned BaseCreature, so its eight-way switch is inlined in the constructor; all eight items are stock.
//
// Dropped, one D-number each (batch 3 note §5):
//   D-58  SetSpecialAbility(SpecialAbility.BloodDisease): Pet Training, declined B3, the Saurosaurus precedent.
//   D-59  MeatType => MeatType.Rotworm: pinned MeatType is Ribs/Bird/LambLeg (BaseCreature.cs:106) and there is no
//         RawRotwormMeat; the corpse yields ribs.
//   D-60  OnKilledBy: a 20% ArielHavenWritofMembership for a player on the Missing quest, gated on
//         QuestHelper.HasQuest<Missing>, which is ServUO's BaseQuest engine (S5); the item itself is ported.
//   D-61  OnMovement: flees for 5 s from a player carrying a burning CandlewoodTorch, a Missing-quest item that
//         needs SwarmContext; neither is here.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
[TypeAlias("Server.Mobiles.RotWorm")]
public partial class Rotworm : BaseCreature
{
    [Constructible]
    public Rotworm() : base(AIType.AI_Melee)
    {
        Body = 732;

        SetStr(200, 300);
        SetDex(80);
        SetInt(15, 20);

        SetHits(200, 250);
        SetStam(50);

        SetDamage(1, 5);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 35, 45);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 25, 35);
        SetResistance(ResistanceType.Poison, 65, 75);
        SetResistance(ResistanceType.Energy, 25, 35);

        SetSkill(SkillName.MagicResist, 25.0);
        SetSkill(SkillName.Tactics, 25.0);
        SetSkill(SkillName.Wrestling, 50.0);

        Fame = 500;
        Karma = -500;

        PackBodyPartOrBones();
    }

    public override string CorpseName => "a rotworm corpse";
    public override string DefaultName => "a rotworm";

    public override int GetAngerSound() => 0x62D;
    public override int GetIdleSound() => 0x62D;
    public override int GetAttackSound() => 0x62A;
    public override int GetHurtSound() => 0x62C;
    public override int GetDeathSound() => 0x62B;

    public override int Meat => 2;
    // D-59: ServUO MeatType.Rotworm; ModernUO's enum has no such member, so the base's Ribs applies.
    public override FoodType FavoriteFood => FoodType.Fish;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }

    // ServUO BaseCreature.PackBodyPartOrBones, inlined.
    private void PackBodyPartOrBones()
    {
        Item part = Utility.Random(8) switch
        {
            0 => new LeftArm(),
            1 => new RightArm(),
            2 => new Torso(),
            3 => new RightLeg(),
            4 => new LeftLeg(),
            5 => new Bone(),
            6 => new RibCage(),
            _ => new BonePile()
        };

        PackItem(part);
    }
}
