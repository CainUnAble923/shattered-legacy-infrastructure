// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Creatures/Abyss.cs (cc-P194). The Abyss void type:
// daemon mongbat, gargoyle assassin, doppleganger, lesser oni, fire daemon. Each has the plain [Constructible]
// (level 60, not a void spawn) and the (level, voidSpawn) constructor the controller and level-3 spawner use.
//
// Conversion notes: DaemonMongbat's plain constructor asks for AI_Necro, which pinned's AIType lacks; AI_Mage
// (pinned MageAI casts Necromancy from skill, the Q-050 finding). LesserOni's SetSpecialAbility(AngryFire) sits
// only in the plain constructor and has no pinned ability; dropped (notes). "a dameon mongbat" is ServUO's spelling.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class DaemonMongbat : CovetousCreature
{
    [Constructible]
    public DaemonMongbat() : base(AIType.AI_Mage)
    {
        Body = 39;
        BaseSoundID = 422;
    }

    [Constructible]
    public DaemonMongbat(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Body = 39;
        BaseSoundID = 422;
    }

    public override string CorpseName => "a mongbat corpse";
    public override string DefaultName => "a dameon mongbat";
}

[SerializationGenerator(0, false)]
public partial class GargoyleAssassin : CovetousCreature
{
    [Constructible]
    public GargoyleAssassin() : base(AIType.AI_Mage)
    {
        Body = 0x4;
        BaseSoundID = 0x174;
    }

    [Constructible]
    public GargoyleAssassin(int level, bool voidSpawn) : base(AIType.AI_Mage, level, voidSpawn)
    {
        Body = 0x4;
        BaseSoundID = 0x174;
    }

    public override string CorpseName => "a gargoyle corpse";
    public override string DefaultName => "a gargoyle assassin";
}

[SerializationGenerator(0, false)]
public partial class CovetousDoppleganger : CovetousCreature
{
    [Constructible]
    public CovetousDoppleganger() : base(AIType.AI_Melee)
    {
        Body = 0x309;
        BaseSoundID = 0x451;
    }

    [Constructible]
    public CovetousDoppleganger(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Body = 0x309;
        BaseSoundID = 0x451;
    }

    public override string CorpseName => "a doppleganger corpse";
    public override string DefaultName => "a doppleganger";
}

[SerializationGenerator(0, false)]
public partial class LesserOni : CovetousCreature
{
    [Constructible]
    public LesserOni() : base(AIType.AI_Mage)
    {
        Body = 241;
    }

    [Constructible]
    public LesserOni(int level, bool voidSpawn) : base(AIType.AI_Mage, level, voidSpawn)
    {
        Body = 241;
    }

    public override string CorpseName => "an oni corpse";
    public override string DefaultName => "a lesser oni";

    public override int GetAngerSound() => 0x4E3;
    public override int GetIdleSound() => 0x4E2;
    public override int GetAttackSound() => 0x4E1;
    public override int GetHurtSound() => 0x4E4;
    public override int GetDeathSound() => 0x4E0;
}

[SerializationGenerator(0, false)]
public partial class CovetousFireDaemon : CovetousCreature
{
    [Constructible]
    public CovetousFireDaemon() : base(AIType.AI_Mage)
    {
        Body = 9;
        BaseSoundID = 357;
    }

    [Constructible]
    public CovetousFireDaemon(int level, bool voidSpawn) : base(AIType.AI_Mage, level, voidSpawn)
    {
        Body = 9;
        BaseSoundID = 357;
    }

    public override string CorpseName => "a fire daemon corpse";
    public override string DefaultName => "a fire daemon";
}
