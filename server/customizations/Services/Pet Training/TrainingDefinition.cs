// cc-P130: ServUO Scripts/Services/Pet Training/TrainingDefinition.cs -> ours, line for line: what one creature type can be
// trained in (its class, the magical schools, the special abilities, weapon moves and area effects it may pick) and its
// control-slot range. The table of them is PetTrainingHelper.Definitions.

using System;
using System.Linq;
using Server.Items;

namespace Server.Mobiles;

public class TrainingDefinition
{
    public Type CreatureType { get; }
    public Class Class { get; }
    public MagicalAbility MagicalAbilities { get; }
    public SpecialAbility[] SpecialAbilities { get; }
    public WeaponAbility[] WeaponAbilities { get; }
    public AreaEffect[] AreaEffects { get; }

    public int ControlSlotsMin { get; }
    public int ControlSlotsMax { get; }

    public TrainingDefinition(
        Type type,
        Class classification,
        MagicalAbility magicalAbility,
        SpecialAbility[] specialAbility,
        WeaponAbility[] weaponAbility,
        AreaEffect[] areaEffect,
        int controlmin,
        int controlmax
    )
    {
        CreatureType = type;
        Class = classification;
        MagicalAbilities = magicalAbility;
        SpecialAbilities = specialAbility;
        WeaponAbilities = weaponAbility;
        AreaEffects = areaEffect;

        ControlSlotsMin = controlmin;
        ControlSlotsMax = controlmax;
    }

    public bool HasSpecialAbility(SpecialAbility ability) =>
        SpecialAbilities != null && SpecialAbilities.Any(a => a == ability);

    public bool HasAreaEffect(AreaEffect ability) => AreaEffects != null && AreaEffects.Any(a => a == ability);
}
