// ServUO: Mobiles/Normal/ToxicSlith.cs (CC6 batch 2). Values verbatim; serialization by the generator. ServUO sets
// no Fame or Karma on the sliths; neither does this.
// SetSpecialAbility(DragonBreath) is NOT dropped (CC6 follow-up, Q-054; D-56 retired, it never was a loss): ToxicSlith is
// in no DragonBreathDefinition.Uses list (Services/Pet Training/SpecialAbility.cs:860-1063), so ServUO gives it the
// default definition, 100% fire at 0.16 of current hits every 30-45 s, which pinned ModernUO carries line for line as
// MonsterAbilities.FireBreath through BaseCreature.GetMonsterAbilities() (:1131). A toxic slith breathes fire, not
// poison: its poison is in its Poisoning skill and its 100% poison resistance, not its breath.
// Dropped: DragonBlood => 6 (D-50: pinned BaseCreature has no DragonBlood virtual and its corpse carve yields no dragon's
// blood).
// ServUO's 5% drop switch is `Utility.Random(2)` with cases 0 and 2, so the SlithEye branch is unreachable
// there and here: a toxic slith drops a venom sac 2.5% of the time and never an eye. Kept as written.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ToxicSlith : BaseCreature
{
    [Constructible]
    public ToxicSlith() : base(AIType.AI_Melee)
    {
        Body = 734;
        Hue = 476;

        SetStr(223, 306);
        SetDex(231, 258);
        SetInt(30, 35);

        SetHits(197, 215);
        SetStam(231, 258);

        SetDamage(6, 24);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 35, 45);
        SetResistance(ResistanceType.Fire, 0, 9);
        SetResistance(ResistanceType.Cold, 5, 10);
        SetResistance(ResistanceType.Poison, 100, 100);
        SetResistance(ResistanceType.Energy, 5, 7);

        SetSkill(SkillName.MagicResist, 95.4, 98.3);
        SetSkill(SkillName.Tactics, 85.5, 90.9);
        SetSkill(SkillName.Wrestling, 90.4, 95.1);
        SetSkill(SkillName.Poisoning, 90.0, 110.0);
    }

    public override string CorpseName => "a slith corpse";
    public override string DefaultName => "a toxic slith";

    // ServUO: SetSpecialAbility(SpecialAbility.DragonBreath), the default (fire) definition.
    private static readonly MonsterAbility[] _abilities = { MonsterAbilities.FireBreath };
    public override MonsterAbility[] GetMonsterAbilities() => _abilities;

    public override int Meat => 6;
    public override int Hides => 11;
    public override HideType HideType => HideType.Horned;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average, 2);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.05)
        {
            switch (Utility.Random(2))
            {
                case 0:
                    c.DropItem(new ToxicVenomSac());
                    break;
                case 2:
                    c.DropItem(new SlithEye());
                    break;
            }
        }

        if (Utility.RandomDouble() < 0.25)
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
