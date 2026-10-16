// ServUO: Mobiles/Normal/GargishOutcast.cs (cc-P129). Values verbatim; serialization by the generator.
//
// AI_Mystic is AI_Mage (D-41's shape): pinned has no Mystic AI, so both halves of ServUO's coin run AI_Mage; the
// Mysticism half still casts its Animated Weapon from OnThink as written (pinned's AnimatedWeaponSpell). The gargish
// cloth is pinned's Gargish*Type1 (AGENTS.md, equivalents; cc-P119's finding); the robe is a leaf port beside this
// file (Items/Equipment/Clothing/GargishRobe.cs). AcquireOnApproach => true is dropped: pinned acquires on approach
// for every creature (BaseCreature.AcquireOnApproachRange, no switch), so the override has nothing to turn on.
//
// DEVIATION, small: ServUO names the outcast from its "Gargoyle Male" and "Gargoyle Female" name lists, which pinned's
// names.json does not carry. Pinned's gargish names are its "gargoyle vendor" list (Ansiraal, Aurlem, Betra, ...),
// used here for both sexes; without it the outcast would have no name. Reverses when the two lists reach names.json.

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Spells;
using Server.Spells.Mysticism;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class GargishOutcast : BaseCreature
{
    private DateTime _nextSummon;

    [Constructible]
    public GargishOutcast() : base(AIType.AI_Mage)
    {
        Race = Race.Gargoyle;
        Title = "the Gargish Outcast";

        SetStr(150);
        SetInt(150);
        SetDex(150);

        SetHits(1000, 1200);
        SetMana(450, 600);

        SetDamage(15, 19);

        if (Utility.RandomBool())
        {
            Name = NameList.RandomName("gargoyle vendor"); // ServUO: "Gargoyle Male" (see the header)
            Female = false;
            Body = 666;
        }
        else
        {
            Name = NameList.RandomName("gargoyle vendor"); // ServUO: "Gargoyle Female"
            Female = true;
            Body = 667;
        }

        Utility.AssignRandomHair(this, true);
        if (!Female)
        {
            Utility.AssignRandomFacialHair(this, true);
        }

        Hue = Race.RandomSkinHue();

        BaseWeapon wep = Utility.Random(3) switch
        {
            1 => new SoulGlaive(),
            2 => new Boomerang(),
            _ => new Cyclone()
        };

        wep.Attributes.SpellChanneling = 1;
        AddImmovableItem(wep);
        AddImmovableItem(new GargishClothChestType1 { Hue = Utility.RandomNeutralHue() });
        AddImmovableItem(new GargishClothArmsType1 { Hue = Utility.RandomNeutralHue() });
        AddImmovableItem(new GargishClothLegsType1 { Hue = Utility.RandomNeutralHue() });
        AddImmovableItem(new GargishClothKiltType1 { Hue = Utility.RandomNeutralHue() });

        if (Utility.RandomBool())
        {
            AddImmovableItem(new GargishRobe());
        }

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 10, 25);
        SetResistance(ResistanceType.Fire, 40, 65);
        SetResistance(ResistanceType.Cold, 40, 65);
        SetResistance(ResistanceType.Poison, 40, 65);
        SetResistance(ResistanceType.Energy, 40, 65);

        SetSkill(SkillName.MagicResist, 120.0);
        SetSkill(SkillName.Tactics, 50.1, 60.0);
        SetSkill(SkillName.Throwing, 120.0);
        SetSkill(SkillName.Anatomy, 0.0, 10.0);
        SetSkill(SkillName.Magery, 50.0, 80.0);
        SetSkill(SkillName.EvalInt, 50.0, 80.0);
        SetSkill(SkillName.Meditation, 120);

        Fame = 12000;
        Karma = -12000;

        if (.5 > Utility.RandomDouble())
        {
            ChangeAIType(AIType.AI_Mage);

            SetSkill(SkillName.Necromancy, 90, 105);
            SetSkill(SkillName.SpiritSpeak, 90, 105);
        }
        else
        {
            SetSkill(SkillName.Mysticism, 90, 105);
            SetSkill(SkillName.Focus, 90, 105);
        }

        _nextSummon = Core.Now;
    }

    private void AddImmovableItem(Item item)
    {
        item.LootType = LootType.Blessed;
        AddItem(item);
    }

    public override Poison PoisonImmune => Poison.Deadly;
    public override bool AlwaysMurderer => true;
    public override bool ReacquireOnMovement => true;
    public override int AcquireOnApproachRange => 8;

    public override WeaponAbility GetWeaponAbility()
    {
        if (Weapon is BaseWeapon weapon)
        {
            return Utility.RandomBool() ? weapon.PrimaryAbility : weapon.SecondaryAbility;
        }

        return WeaponAbility.WhirlwindAttack;
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich);
        AddLoot(LootPack.MedScrolls, 2);
        AddLoot(LootPack.HighScrolls, 2);
    }

    public override void OnThink()
    {
        base.OnThink();

        if (Combatant == null || _nextSummon > Core.Now)
        {
            return;
        }

        if (Mana > 40 && Followers + 4 <= FollowersMax)
        {
            Spell spell = new AnimatedWeaponSpell(this);
            spell.Cast();
            _nextSummon = Core.Now + TimeSpan.FromSeconds(30);
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _nextSummon = Core.Now;
    }
}
