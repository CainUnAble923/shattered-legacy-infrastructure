// cc-P199: ServUO pub57 Scripts/Items/Functional/Automaton/BlackrockAutomaton.cs. The blackrock automaton, built from a
// BlackrockAutomatonHead: a KotlAutomaton with hue 0x8497, more strength and physical/fire resist, a third weapon
// ability, a 0.45 ability chance, no metal (OnResourceChanged does nothing) and crystalline blackrock as its repair
// resource (ServUO's own TODO: "Needs to be regular blackrock. This doesn't exist on ServUO").
//
// Creature rules (port recipe): ServUO's three SetWeaponAbility calls (BlackrockAutomaton.cs:49-51), on top of the two
// the KotlAutomaton constructor already added, leave three abilities in ServUO's profile (ParalyzingBlow, Disarm,
// ArmorPierce); the GetWeaponAbility override picks one of the three at random, the Utility.Random(n) switch shape of
// stock DemonKnight.cs. Name -> DefaultName.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BlackrockAutomaton : KotlAutomaton
{
    [Constructible]
    public BlackrockAutomaton()
    {
        Body = 1406;
        BaseSoundID = 541;

        Hue = 0x8497;

        SetStr(800, 900);
        SetDex(67, 74);
        SetInt(255, 263);

        SetHits(774, 876);

        SetDamage(15, 20);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 55, 60);
        SetResistance(ResistanceType.Fire, 55, 60);
        SetResistance(ResistanceType.Cold, 50, 55);
        SetResistance(ResistanceType.Poison, 50, 55);
        SetResistance(ResistanceType.Energy, 45, 50);

        SetSkill(SkillName.Anatomy, 90.3, 99.9);
        SetSkill(SkillName.MagicResist, 121.0, 126.7);
        SetSkill(SkillName.Tactics, 82.0, 94.8);
        SetSkill(SkillName.Wrestling, 94.4, 108.4);
    }

    public override string DefaultName => "blackrock automaton";

    [CommandProperty(AccessLevel.GameMaster)]
    public override Type RepairResource => typeof(CrystallineBlackrock);

    public override WeaponAbility GetWeaponAbility() =>
        Utility.Random(3) switch
        {
            0 => WeaponAbility.ParalyzingBlow,
            1 => WeaponAbility.Disarm,
            _ => WeaponAbility.ArmorPierce
        };

    public override double WeaponAbilityChance => 0.45;

    public override void OnResourceChanged()
    {
    }
}
