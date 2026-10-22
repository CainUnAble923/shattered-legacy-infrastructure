// ServUO: Services/Expansions/Time Of Legends/Mobiles/BritannianInfantry.cs (cc-P198). Values verbatim; serialization
// by the generator.
//
// HealChance => 1.0 is pinned's CanHeal (the WildTiger precedent, cc-P129). SetWearable is the shard's extension
// (cc-P194). The speeds .15/.3 are dead parameters (port recipe) and dropped like every other port's.
// ServUO's IsEnemy override only asks the Myrmidex Invasion whether a mobile is allied with the tribes or the
// Myrmidex, both behind MyrmidexInvasionSystem.Active; the invasion is out of scope, so it is the base IsEnemy, which
// is what ServUO runs with the invasion inactive. AlwaysAttackable inside a BattleRegion is kept as written: ServUO's
// BattleRegion is the invasion's arena, and pinned's own BattleRegion is placed nowhere (regions.json has none), so it
// is false everywhere, as ServUO's is wherever the invasion is not.

using ModernUO.Serialization;
using Server.Items;
using Server.Regions;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class BritannianInfantry : BaseCreature
{
    [Constructible]
    public BritannianInfantry() : base(AIType.AI_Melee)
    {
        SpeechHue = Utility.RandomDyedHue();

        Body = 0x190;
        Hue = 33779;
        Name = NameList.RandomName("male");
        Title = "the Britannian";

        SetStr(115, 150);
        SetDex(150);
        SetInt(25, 44);

        SetDamage(12, 17);

        SetHits(2400);
        SetMana(250);

        SetResistance(ResistanceType.Physical, 25);
        SetResistance(ResistanceType.Fire, 15);
        SetResistance(ResistanceType.Cold, 10);
        SetResistance(ResistanceType.Poison, 15);
        SetResistance(ResistanceType.Energy, 10);

        SetDamageType(ResistanceType.Physical, 100);

        SetSkill(SkillName.MagicResist, 120);
        SetSkill(SkillName.Tactics, 120);
        SetSkill(SkillName.Anatomy, 120);
        SetSkill(SkillName.Swords, 120);
        SetSkill(SkillName.Fencing, 120);
        SetSkill(SkillName.Macing, 120);

        AddImmovableItem(new PlateChest());
        AddImmovableItem(new PlateLegs());
        AddImmovableItem(new PlateArms());
        AddImmovableItem(new PlateGloves());
        AddImmovableItem(new PlateGorget());
        AddImmovableItem(new Kilt(1175));
        AddImmovableItem(new BodySash(1157));
        AddImmovableItem(new Halberd());

        PackGold(Utility.RandomMinMax(250, 300));

        Fame = 7500;
        Karma = 4500;

        Utility.AssignRandomHair(this);
    }

    public override string CorpseName => "a human corpse";

    public override bool CanHeal => true; // ServUO: HealChance => 1.0

    public override bool AlwaysAttackable => Region.IsPartOf<BattleRegion>();
    public override bool ShowFameTitle => false;
    public override bool ClickTitle => false;

    private void AddImmovableItem(Item item)
    {
        item.LootType = LootType.Blessed;
        this.SetWearable(item);
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
    }

    public override WeaponAbility GetWeaponAbility()
    {
        if (Weapon is BaseWeapon wep and not Fists)
        {
            return Utility.RandomDouble() > 0.5 ? wep.PrimaryAbility : wep.SecondaryAbility;
        }

        return null;
    }
}
