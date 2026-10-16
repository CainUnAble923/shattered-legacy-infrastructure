// ServUO: Mobiles/Normal/WildTiger.cs (cc-P129): WildTiger and its two children, WildWhiteTiger and WildBlackTiger,
// one file as in ServUO. Values verbatim; serialization by the generator.
//
// Pinned BaseMount takes (bodyID, itemID, aiType), no name (DreadWarhorse's shape in cc-P119), so ServUO's (string name)
// constructor is each type's DefaultName. CanRide, the one persisted field, is [SerializableField(0)]. ServUO's
// version-0 and version-1 upgrade branches (Rider cleared, BleedAttack re-set) are dead for new content.
// HealChance => .167 is pinned's CanHeal. SetWeaponAbility(BleedAttack) is GetWeaponAbility; GraspingClaw goes to
// the profile. [TypeAlias("Server.Mobiles.Tiger")] kept: pinned declares no Tiger (checked, and the type-identity
// gate would catch one). The three pelts are leaf ports beside this file (Time Of Legends resources).

using ModernUO.Serialization;
using Server.Engines.Plants;
using Server.Items;

namespace Server.Mobiles;

[TypeAlias("Server.Mobiles.Tiger")]
[SerializationGenerator(0, false)]
public partial class WildTiger : BaseMount
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _canRide;

    public virtual Item GetPelt => new TigerPelt(4);

    [Constructible]
    public WildTiger() : base(Utility.RandomBool() ? 1254 : 1255, 16071, AIType.AI_Melee)
    {
        if (Body == 1255)
        {
            ItemID = 16072;
        }

        SetStr(496, 554);
        SetDex(88, 124);
        SetInt(94, 163);

        SetHits(352, 450);

        SetDamage(18, 24);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 56, 75);
        SetResistance(ResistanceType.Fire, 21, 40);
        SetResistance(ResistanceType.Cold, 55, 64);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 25, 35);

        SetSkill(SkillName.MagicResist, 90.8, 97.5);
        SetSkill(SkillName.Anatomy, 0);
        SetSkill(SkillName.Tactics, 100.2, 102.5);
        SetSkill(SkillName.Wrestling, 90.1, 94.4);

        Fame = 11000;
        Karma = -11000;

        if (Core.ML && Utility.RandomDouble() < .33)
        {
            PackItem(Seed.RandomPeculiarSeed(Utility.RandomList(1, 1, 1, 1, 2, 2, 2, 3, 3, 4)));
        }

        Tamable = true;
        ControlSlots = 2;
        MinTameSkill = 95.1;

        SetSpecialAbility(SpecialAbility.GraspingClaw);

        _canRide = false;
    }

    public override string CorpseName => "a tiger corpse";
    public override string DefaultName => "a wild tiger";

    public override bool CanHeal => true; // ServUO: HealChance => .167

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

    public override void OnDoubleClick(Mobile from)
    {
        if (CanRide)
        {
            base.OnDoubleClick(from);
        }
        else if (from.AccessLevel >= AccessLevel.GameMaster && !Body.IsHuman)
        {
            Backpack?.DisplayTo(from);
        }
    }

    public override int GetIdleSound() => 0x673;
    public override int GetAngerSound() => 0x670;
    public override int GetHurtSound() => 0x672;
    public override int GetDeathSound() => 0x671;

    public override double WeaponAbilityChance => 0.5;

    public override int Meat => 2;
    public override FoodType FavoriteFood => FoodType.Meat;
    public override int TreasureMapLevel => 1;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 1);
    }

    public override void OnCarve(Mobile from, Corpse corpse, Item with)
    {
        if (!Controlled && corpse?.Carved == false)
        {
            from.SendLocalizedMessage(1156197); // You cut away some pelts, but they remain on the corpse.
            corpse.DropItem(GetPelt);
        }

        base.OnCarve(from, corpse, with);
    }
}

[SerializationGenerator(0, false)]
public partial class WildWhiteTiger : WildTiger
{
    public override Item GetPelt => new WhiteTigerPelt(4);

    [Constructible]
    public WildWhiteTiger()
    {
        Hue = 2500;
    }

    public override string CorpseName => "a tiger corpse";
    public override string DefaultName => "a wild white tiger";
}

[SerializationGenerator(0, false)]
public partial class WildBlackTiger : WildTiger
{
    public override Item GetPelt => new BlackTigerPelt(4);

    [Constructible]
    public WildBlackTiger()
    {
        Hue = 1175;
    }

    public override string CorpseName => "a tiger corpse";
    public override string DefaultName => "a wild black tiger";
}
