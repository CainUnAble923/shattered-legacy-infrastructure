// ServUO: Services/Expansions/Time Of Legends/Mobiles/DragonTurtleHatchling.cs (cc-P198). Values verbatim;
// serialization by the generator.
//
// SetWeaponAbility(BleedAttack) is pinned's GetWeaponAbility; SetSpecialAbility(DragonBreath) goes to the profile
// (cc-P121). FightMode.Aggressor is carried (not the default); perception 10 is dropped (port recipe: ServUO runs it
// at 16, as pinned does by default). ServUO writes version 1 and its version-0 branch re-sets BleedAttack; new
// content starts at 0 here and never needs it.
//
// OnAfterTame is ServUO's body, through the shard's hook (cc-P130's AnimalTaming patch, cc-P458): on the first tame
// every skill above zero caps at 100 and loses 55% of what taming left it; the stats are kept. ServUO tests
// Owners.Count == 0 because it calls the hook before adding the owner; pinned adds the owner first, so the hook
// passes firstTame instead (a release and re-tame by the same owner is not a first tame).
// OnCarve drops four DragonTurtleScute on a wild hatchling's corpse before the normal carve, as ServUO does.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class DragonTurtleHatchling : BaseCreature
{
    [Constructible]
    public DragonTurtleHatchling() : base(AIType.AI_Mage, FightMode.Aggressor)
    {
        Body = 1294;
        BaseSoundID = 362;

        SetStr(550, 650);
        SetDex(55, 65);
        SetInt(550, 650);

        SetHits(550, 850);

        SetDamage(24, 33);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 65, 85);
        SetResistance(ResistanceType.Fire, 80, 90);
        SetResistance(ResistanceType.Cold, 50, 55);
        SetResistance(ResistanceType.Poison, 60);
        SetResistance(ResistanceType.Energy, 70, 75);

        SetSkill(SkillName.MagicResist, 130, 140);
        SetSkill(SkillName.Tactics, 110, 120);
        SetSkill(SkillName.Wrestling, 140, 150);
        SetSkill(SkillName.Magery, 130, 140);
        SetSkill(SkillName.EvalInt, 100, 119);

        Fame = 16000;
        Karma = -16000;

        Tamable = true;
        ControlSlots = 5;
        MinTameSkill = 104.7;

        SetSpecialAbility(SpecialAbility.DragonBreath);
    }

    public override string CorpseName => "a dragon turtle hatchling corpse";
    public override string DefaultName => "a dragon turtle hatchling";

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

    public override void OnAfterTame(Mobile tamer, bool firstTame)
    {
        if (!firstTame)
        {
            return;
        }

        SkillsCap = Skills.Total;

        foreach (var sk in Skills)
        {
            if (sk.Base > 0)
            {
                sk.Cap = Math.Max(100, sk.Base - sk.Base * 10);
                sk.Base = sk.Base - sk.Base * .55;
            }
        }
    }

    public override int Meat => 4;
    public override FoodType FavoriteFood => FoodType.Fish;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average, 3);
    }

    public override void OnCarve(Mobile from, Corpse corpse, Item with)
    {
        if (!Controlled && corpse?.Carved == false)
        {
            from.SendLocalizedMessage(1156198); // You cut away some pelts, but they remain on the corpse.
            corpse.DropItem(new DragonTurtleScute(4));
        }

        base.OnCarve(from, corpse, with);
    }
}
