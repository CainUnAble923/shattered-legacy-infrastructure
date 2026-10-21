// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Creatures/Repond.cs (cc-P194). The Repond void type:
// lizardman witchdoctor, orc foot soldier, ratman assassin, ogre bone crusher, titan rockhurler.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class LizardmanWitchdoctor : CovetousCreature
{
    [Constructible]
    public LizardmanWitchdoctor() : base(AIType.AI_Mage)
    {
        Body = Utility.RandomList(35, 36);
        BaseSoundID = 417;
    }

    [Constructible]
    public LizardmanWitchdoctor(int level, bool voidSpawn) : base(AIType.AI_Mage, level, voidSpawn)
    {
        Body = Utility.RandomList(35, 36);
        BaseSoundID = 417;
    }

    public override string CorpseName => "a lizardman corpse";
    public override string DefaultName => "a lizardman witchdoctor";
}

[SerializationGenerator(0, false)]
public partial class OrcFootSoldier : CovetousCreature
{
    [Constructible]
    public OrcFootSoldier() : base(AIType.AI_Melee)
    {
        Body = 17;
        BaseSoundID = 0x45A;
    }

    [Constructible]
    public OrcFootSoldier(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Body = 17;
        BaseSoundID = 0x45A;
    }

    public override string CorpseName => "an orcish corpse";
    public override string DefaultName => "an orc foot soldier";
}

[SerializationGenerator(0, false)]
public partial class RatmanAssassin : CovetousCreature
{
    [Constructible]
    public RatmanAssassin() : base(AIType.AI_Melee)
    {
        Body = 42;
        BaseSoundID = 437;
    }

    [Constructible]
    public RatmanAssassin(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Body = 42;
        BaseSoundID = 437;
    }

    public override string CorpseName => "a ratman corpse";
    public override string DefaultName => "ratman assassin";
}

[SerializationGenerator(0, false)]
public partial class OgreBoneCrusher : CovetousCreature
{
    [Constructible]
    public OgreBoneCrusher() : base(AIType.AI_Melee)
    {
        Body = 1;
        BaseSoundID = 427;
    }

    [Constructible]
    public OgreBoneCrusher(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Body = 1;
        BaseSoundID = 427;
    }

    public override string CorpseName => "an ogre corpse";
    public override string DefaultName => "an ogre bone crusher";
}

[SerializationGenerator(0, false)]
public partial class TitanRockHunter : CovetousCreature
{
    [Constructible]
    public TitanRockHunter() : base(AIType.AI_Mage)
    {
        Body = 76;
        BaseSoundID = 609;
    }

    [Constructible]
    public TitanRockHunter(int level, bool voidSpawn) : base(AIType.AI_Mage, level, voidSpawn)
    {
        Body = 76;
        BaseSoundID = 609;
    }

    public override string CorpseName => "a titan corpse";
    public override string DefaultName => "a titan rockhurler";
}
