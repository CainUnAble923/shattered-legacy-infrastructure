// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Creatures/Reptile.cs (cc-P194). The Reptile void type:
// war alligator, magma lizard, vicious drake, corrupted wyvern, covetous wyrm.
//
// Conversion notes: CovetousWyrm's plain constructor asks for AI_Necro, which pinned's AIType lacks; AI_Mage (the
// Q-050 finding: pinned MageAI casts Necromancy from skill).

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class WarAlligator : CovetousCreature
{
    [Constructible]
    public WarAlligator() : base(AIType.AI_Melee)
    {
        Body = 0xCA;
        BaseSoundID = 660;
    }

    [Constructible]
    public WarAlligator(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Body = 0xCA;
        BaseSoundID = 660;
    }

    public override string CorpseName => "an alligator corpse";
    public override string DefaultName => "a war alligator";
}

[SerializationGenerator(0, false)]
public partial class MagmaLizard : CovetousCreature
{
    [Constructible]
    public MagmaLizard() : base(AIType.AI_Melee)
    {
        Body = 0xCE;
        Hue = Utility.RandomList(0x647, 0x650, 0x659, 0x662, 0x66B, 0x674);
        BaseSoundID = 0x5A;
    }

    [Constructible]
    public MagmaLizard(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Body = 0xCE;
        Hue = Utility.RandomList(0x647, 0x650, 0x659, 0x662, 0x66B, 0x674);
        BaseSoundID = 0x5A;
    }

    public override string CorpseName => "a magma lizard corpse";
    public override string DefaultName => "a magma lizard";
}

[SerializationGenerator(0, false)]
public partial class ViciousDrake : CovetousCreature
{
    [Constructible]
    public ViciousDrake() : base(AIType.AI_Melee)
    {
        Body = Utility.RandomList(60, 61);
        BaseSoundID = 362;
    }

    [Constructible]
    public ViciousDrake(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Body = Utility.RandomList(60, 61);
        BaseSoundID = 362;
    }

    public override string CorpseName => "a drake corpse";
    public override string DefaultName => "a vicious drake";
}

[SerializationGenerator(0, false)]
public partial class CorruptedWyvern : CovetousCreature
{
    [Constructible]
    public CorruptedWyvern() : base(AIType.AI_Mage)
    {
        Body = 62;
        BaseSoundID = 362;
    }

    [Constructible]
    public CorruptedWyvern(int level, bool voidSpawn) : base(AIType.AI_Mage, level, voidSpawn)
    {
        Body = 62;
        BaseSoundID = 362;
    }

    public override string CorpseName => "a wyvern corpse";
    public override string DefaultName => "a corrupted wyvern";
}

[SerializationGenerator(0, false)]
public partial class CovetousWyrm : CovetousCreature
{
    [Constructible]
    public CovetousWyrm() : base(AIType.AI_Mage)
    {
        Body = 106;
        BaseSoundID = 362;
    }

    [Constructible]
    public CovetousWyrm(int level, bool voidSpawn) : base(AIType.AI_Mage, level, voidSpawn)
    {
        Body = 106;
        BaseSoundID = 362;
    }

    public override string CorpseName => "a covetous wyrm corpse";
    public override string DefaultName => "a covetous wyrm";
}
