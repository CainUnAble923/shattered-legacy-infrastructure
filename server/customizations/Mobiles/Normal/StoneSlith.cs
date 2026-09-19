// ServUO: Mobiles/Normal/StoneSlith.cs (CC6 batch 2). Values verbatim; serialization by the generator. ServUO sets
// no Fame or Karma on the sliths; neither does this.
// Dropped: SetSpecialAbility(GraspingClaw) and SetSpecialAbility(TailSwipe) (D-57: Pet Training, declined B3, the Saurosaurus precedent); DragonBlood => 6
// (D-50: pinned BaseCreature has no DragonBlood virtual and its corpse carve yields no dragon's blood).
// SetWeaponAbility(BleedAttack) is GetWeaponAbility (the CC3 transformation). ServUO's version-0 upgrade path
// in Deserialize re-adds the same two calls and is not ported (new content starts at version 0).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class StoneSlith : BaseCreature
{
    [Constructible]
    public StoneSlith() : base(AIType.AI_Melee)
    {
        Body = 734;

        SetStr(250, 300);
        SetDex(76, 90);
        SetInt(34, 69);

        SetHits(154, 166);
        SetStam(76, 90);
        SetMana(34, 69);

        SetDamage(6, 24);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50, 55);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 10, 20);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 30, 40);

        SetSkill(SkillName.MagicResist, 86.8, 95.1);
        SetSkill(SkillName.Tactics, 82.6, 88.6);
        SetSkill(SkillName.Wrestling, 75.8, 87.4);
        SetSkill(SkillName.Anatomy, 0.0, 2.9);

        Tamable = true;
        ControlSlots = 2;
        MinTameSkill = 65.1;
    }

    public override string CorpseName => "a slith corpse";
    public override string DefaultName => "a stone slith";

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

    public override int Meat => 1;
    public override int Hides => 12;
    public override HideType HideType => HideType.Spined;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average, 2);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (!Controlled && Utility.RandomDouble() <= 0.005)
        {
            c.DropItem(new StoneSlithClaw());
        }

        if (!Controlled && Utility.RandomDouble() < 0.05)
        {
            c.DropItem(new SlithEye());
        }

        if (!Controlled && Utility.RandomDouble() < 0.25)
        {
            switch (Utility.Random(2))
            {
                case 0:
                    c.DropItem(new AncientPotteryFragments());
                    break;
                case 1:
                    c.DropItem(new TatteredAncientScroll());
                    break;
            }
        }
    }
}
