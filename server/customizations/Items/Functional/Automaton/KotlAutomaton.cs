// cc-P199: ServUO pub57 Scripts/Items/Functional/Automaton/KotlAutomaton.cs. The Kotl automaton, a Time of Legends
// construct (body 1406): wild, it guards Kotl City (AutomatonStatue wakes it); built from a KotlAutomatonHead, it is a
// 4-slot bonded pet only a Grandmaster tinker can command, hued and resisted by the head's metal (OnResourceChanged:
// the metal's armour resists added to 45-50 each, its weapon elemental damage taken out of physical). When the
// owned automaton dies it is stabled away and a BrokenAutomatonHead goes into the owner's pack; a tinker's repair tool
// on the head (ServUO Repair.cs:233-238) spends 75 ingots of its metal and brings it back, and ServUO's Veterinarian
// resurrects it for free (Veterinarian.cs:47-53, 60-61). ServUO keeps the class in Server.Items, as here.
//
// Creature rules (port recipe):
//   - ServUO's base(AIType.AI_Melee, FightMode.Closest, 10, 1, 0.2, 0.4) is base(AIType.AI_Melee): 10 runs at 16 in
//     ServUO (BaseCreature.cs:2353-2377), pinned's default; the speeds are dead arguments, pinned's Medium fallback.
//   - SetWeaponAbility(ParalyzingBlow) + SetWeaponAbility(Disarm) (KotlAutomaton.cs:76-77) is a GetWeaponAbility
//     override picking one of the two at random, as ServUO's profile does. The version-0 copies in Deserialize
//     (:208-212) are an upgrade path new content never runs.
//   - The "Missing Wrestling Mastery Ability" comment (:166) is ServUO's own gap; nothing to port.
//
// Differences from ServUO, each forced by pinned's BaseCreature:
//   - IRepairableMobile (KotlAutomaton.cs:7) does not exist in pinned (see Mobiles/Normal/ClockworkScorpion.cs, D-64):
//     RepairResource is kept as a plain property for BrokenAutomatonHead and for the Repair patch if one lands.
//     ServUO's Repair.cs:691 asks 80 Tinkering to repair a KotlAutomaton with a repair tool.
//   - CanTransfer => false (:191-194) has no counterpart: pinned's PetOrders.IssueTransfer (Mobiles/AI/BaseAI/
//     PetOrders.cs:382-430) asks only CanBeControlledBy, which here is GetControlChance > 0, so the automaton can be
//     transferred to another Grandmaster tinker. Needs a hook (see the report).
//   - CanFriend => false (:196-199) becomes AllowNewPetFriend => false, pinned's refusal (PetOrders.cs:332), which says
//     1005482 ("Your pet does not seem to be interested in making new friends right now") where ServUO's fall-through
//     says nothing. The automaton still gets no friends.
//   - CanBeDistracted => false (:175) has no pinned member; ServUO's own default is !Core.ML, false on this shard.

using System;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class KotlAutomaton : BaseCreature
{
    [Constructible]
    public KotlAutomaton() : base(AIType.AI_Melee)
    {
        _resource = CraftResource.Iron;

        Body = 1406;
        BaseSoundID = 541;

        SetStr(793, 875);
        SetDex(67, 74);
        SetInt(255, 263);

        SetHits(774, 876);

        SetDamage(15, 20);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 45, 50);
        SetResistance(ResistanceType.Fire, 45, 50);
        SetResistance(ResistanceType.Cold, 45, 50);
        SetResistance(ResistanceType.Poison, 45, 50);
        SetResistance(ResistanceType.Energy, 45, 50);

        SetSkill(SkillName.Anatomy, 90.3, 99.9);
        SetSkill(SkillName.MagicResist, 121.0, 126.7);
        SetSkill(SkillName.Tactics, 82.0, 94.8);
        SetSkill(SkillName.Wrestling, 94.4, 108.4);
        SetSkill(SkillName.DetectHidden, 40.0);
        SetSkill(SkillName.Parry, 70.0, 80.0);

        Fame = 14000;
        Karma = -14000;

        ControlSlots = 4;
    }

    public override string DefaultName => "kotl automaton";

    // Loaded straight into the field (useField), as ServUO's Deserialize reads _Resource: a load must not re-roll the
    // resists OnResourceChanged sets.
    private CraftResource _resource;

    [SerializableProperty(0, useField: nameof(_resource))]
    [CommandProperty(AccessLevel.GameMaster)]
    public CraftResource Resource
    {
        get => _resource;
        set
        {
            var old = _resource;
            _resource = value;

            if (old != _resource)
            {
                OnResourceChanged();
            }

            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public virtual Type RepairResource
    {
        get
        {
            var resInfo = CraftResources.GetInfo(_resource);

            if (resInfo == null || resInfo.ResourceTypes.Length == 0)
            {
                return typeof(IronIngot);
            }

            return resInfo.ResourceTypes[0];
        }
    }

    public override WeaponAbility GetWeaponAbility() =>
        Utility.RandomBool() ? WeaponAbility.ParalyzingBlow : WeaponAbility.Disarm;

    public override double WeaponAbilityChance => 0.33;

    public override bool IsScaredOfScaryThings => false;
    public override bool IsScaryToPets => !Controlled;
    public override FoodType FavoriteFood => FoodType.None;
    public override bool DeleteOnRelease => true;
    public override bool AutoDispel => !Controlled;
    public override bool BleedImmune => true;
    public override bool BardImmune => true;
    public override Poison PoisonImmune => Poison.Lethal;

    // ServUO CanFriend => false (see the header).
    public override bool AllowNewPetFriend => false;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 4);
    }

    public virtual void OnResourceChanged()
    {
        Hue = 0x8000 | CraftResources.GetHue(_resource);

        var resInfo = CraftResources.GetInfo(_resource);

        if (resInfo == null)
        {
            return;
        }

        var attrs = resInfo.AttributeInfo;

        if (attrs == null)
        {
            return;
        }

        SetResistance(ResistanceType.Physical, Utility.RandomMinMax(45, 50) + attrs.ArmorPhysicalResist);
        SetResistance(ResistanceType.Fire, Utility.RandomMinMax(45, 50) + attrs.ArmorFireResist);
        SetResistance(ResistanceType.Cold, Utility.RandomMinMax(45, 50) + attrs.ArmorColdResist);
        SetResistance(ResistanceType.Poison, Utility.RandomMinMax(45, 50) + attrs.ArmorPoisonResist);
        SetResistance(ResistanceType.Energy, Utility.RandomMinMax(45, 50) + attrs.ArmorEnergyResist);

        var fire = attrs.WeaponFireDamage;
        var cold = attrs.WeaponColdDamage;
        var poison = attrs.WeaponPoisonDamage;
        var energy = attrs.WeaponEnergyDamage;
        var physical = 100 - fire - cold - poison - energy;

        SetDamageType(ResistanceType.Physical, physical);
        SetDamageType(ResistanceType.Fire, fire);
        SetDamageType(ResistanceType.Cold, cold);
        SetDamageType(ResistanceType.Poison, poison);
        SetDamageType(ResistanceType.Energy, energy);
    }

    public override double GetControlChance(Mobile m, bool useBaseSkill = false)
    {
        if (m.Skills[SkillName.Tinkering].Base < 100.0)
        {
            m.SendLocalizedMessage(1157043); // You lack the skill to command this Automaton.
            return 0;
        }

        return 1.0;
    }

    public override bool OnBeforeDeath()
    {
        if (!Controlled)
        {
            if (Region.IsPartOf("KotlCity"))
            {
                AutomatonStatue.OnDeath(this);
            }
        }

        return base.OnBeforeDeath();
    }

    public override void OnDeath(Container c)
    {
        var master = GetMaster();

        if (Controlled && master?.Backpack != null)
        {
            var broke = new BrokenAutomatonHead(this);

            ControlTarget = null;
            ControlOrder = OrderType.Stay;
            Internalize();

            IsStabled = true;
            Loyalty = MaxLoyalty;

            master.Backpack.DropItem(broke); // This needs to drop regardless of weight/item count, right?

            master.SendLocalizedMessage(1157048); // A broken automaton head has been placed in your backpack.
        }

        base.OnDeath(c);
    }
}
