// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Mobiles/Corgul/SoulboundPirateRaider.cs -> ours,
// High Seas, second half. A soulbound pirate raider: an archer (a bow or crossbow, 25 arrows or bolts), 200-250 hits.
// One of Corgul the Soulbinder's crew: CorgulTheSoulBinder.SpawnHelpers raises 15 at each of the island's five landing
// spots, and they die with him.
//
// No deliberate differences from ServUO. (Its GetWeaponAbility reads the two-handed layer, where a
// crossbow is and a one-handed bow is not; kept.)

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SoulboundPirateRaider : BaseCreature
{
    public override bool ClickTitle => false;
    public override bool AlwaysMurderer => true;

    public override WeaponAbility GetWeaponAbility()
    {
        Item weapon = FindItemOnLayer(Layer.TwoHanded);

        if (weapon == null)
            return null;

        if (weapon is BaseWeapon)
        {
            if (Utility.RandomBool())
                return ((BaseWeapon)weapon).PrimaryAbility;
            else
                return ((BaseWeapon)weapon).SecondaryAbility;
        }
        return null;
    }

    [Constructible]
    public SoulboundPirateRaider()
        : base(AIType.AI_Melee)
    {
        Name = "a soulbound pirate raider";
        Body = 0x190;
        Hue = Race.Human.RandomSkinHue();
        Utility.AssignRandomHair(this);

        SetStr(150, 200);
        SetDex(125, 150);
        SetInt(95, 110);

        SetHits(200, 250);

        SetDamage(15, 25);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 45, 55);
        SetResistance(ResistanceType.Fire, 45, 55);
        SetResistance(ResistanceType.Cold, 45, 55);
        SetResistance(ResistanceType.Poison, 45, 55);
        SetResistance(ResistanceType.Energy, 45, 55);

        SetSkill(SkillName.MagicResist, 50.0, 75.5);
        SetSkill(SkillName.Archery, 90.0, 105.5);
        SetSkill(SkillName.Tactics, 90.0, 105.5);
        SetSkill(SkillName.Anatomy, 90.0, 105.5);

        Fame = 2000;
        Karma = -2000;

        AddItem(new TricorneHat());
        AddItem(new LeatherArms());
        AddItem(new FancyShirt());
        AddItem(new ShortPants());
        AddItem(new Cutlass());
        AddItem(new Boots(Utility.RandomNeutralHue()));
        AddItem(new GoldEarrings());

        Item bow;

        switch (Utility.Random(4))
        {
            default:
            case 0: bow = new CompositeBow(); PackItem(new Arrow(25)); break;
            case 1: bow = new Crossbow(); PackItem(new Bolt(25)); break;
            case 2: bow = new Bow(); PackItem(new Arrow(25)); break;
            case 3: bow = new HeavyCrossbow(); PackItem(new Bolt(25)); break;
        }

        AddItem(bow);
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 2);
    }
}
