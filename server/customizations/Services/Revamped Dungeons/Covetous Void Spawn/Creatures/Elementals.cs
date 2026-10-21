// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Creatures/Elementals.cs (cc-P194). The Elemental void
// type: earth, water, vortex, searing and venom elementals. ServUO gives the water elemental AI_Mage in its plain
// constructor and AI_Melee in the (level, voidSpawn) one; both are carried as written.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class CovetousEarthElemental : CovetousCreature
{
    [Constructible]
    public CovetousEarthElemental() : base(AIType.AI_Melee)
    {
        Body = 14;
        BaseSoundID = 268;
    }

    [Constructible]
    public CovetousEarthElemental(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Body = 14;
        BaseSoundID = 268;
    }

    public override string CorpseName => "an earth elemental corpse";
    public override string DefaultName => "an earth elemental";
}

[SerializationGenerator(0, false)]
public partial class CovetousWaterElemental : CovetousCreature
{
    [Constructible]
    public CovetousWaterElemental() : base(AIType.AI_Mage)
    {
        Body = 16;
        BaseSoundID = 278;
    }

    [Constructible]
    public CovetousWaterElemental(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Body = 16;
        BaseSoundID = 278;
    }

    public override string CorpseName => "a water elemental corpse";
    public override string DefaultName => "a water elemental";
}

[SerializationGenerator(0, false)]
public partial class VortexElemental : CovetousCreature
{
    [Constructible]
    public VortexElemental() : base(AIType.AI_Melee)
    {
        Body = 13;
        Hue = 0x4001;
        BaseSoundID = 655;
    }

    [Constructible]
    public VortexElemental(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Body = 13;
        Hue = 0x4001;
        BaseSoundID = 655;
    }

    public override string CorpseName => "a vortex elemental corpse";
    public override string DefaultName => "a vortex elemental";
}

[SerializationGenerator(0, false)]
public partial class SearingElemental : CovetousCreature
{
    [Constructible]
    public SearingElemental() : base(AIType.AI_Mage)
    {
        Body = 15;
        BaseSoundID = 838;
    }

    [Constructible]
    public SearingElemental(int level, bool voidSpawn) : base(AIType.AI_Mage, level, voidSpawn)
    {
        Body = 15;
        BaseSoundID = 838;
    }

    public override string CorpseName => "a searing elemental corpse";
    public override string DefaultName => "a searing elemental";
}

[SerializationGenerator(0, false)]
public partial class VenomElemental : CovetousCreature
{
    [Constructible]
    public VenomElemental() : base(AIType.AI_Mage)
    {
        Body = 162;
        BaseSoundID = 263;
    }

    [Constructible]
    public VenomElemental(int level, bool voidSpawn) : base(AIType.AI_Mage, level, voidSpawn)
    {
        Body = 162;
        BaseSoundID = 263;
    }

    public override string CorpseName => "a venom elemental corpse";
    public override string DefaultName => "a venom elemental";

    public override bool BleedImmune => true;
    public override Poison PoisonImmune => Poison.Lethal;
    public override Poison HitPoison => Poison.Lethal;
    public override double HitPoisonChance => 0.75;
    public override int TreasureMapLevel => 5;
}
