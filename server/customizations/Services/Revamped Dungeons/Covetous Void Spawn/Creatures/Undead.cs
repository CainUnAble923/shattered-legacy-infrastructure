// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Creatures/Undead.cs (cc-P194). The Undead void type:
// angered spirit, bone swordslinger, vile cadaver, diseased lich, covetous revenant. The revenant's shroud and boots
// (hue 0x4001) are worn only by the plain constructor's creature, as on ServUO; its (level, voidSpawn) one has no
// sound id either (ServUO's TODO).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class AngeredSpirit : CovetousCreature
{
    [Constructible]
    public AngeredSpirit() : base(AIType.AI_Mage)
    {
        Body = 3;
        BaseSoundID = 471;
    }

    [Constructible]
    public AngeredSpirit(int level, bool voidSpawn) : base(AIType.AI_Mage, level, voidSpawn)
    {
        Body = 3;
        BaseSoundID = 471;
    }

    public override string CorpseName => "an angered spirit corpse";
    public override string DefaultName => "an angered spirit";
}

[SerializationGenerator(0, false)]
public partial class BoneSwordSlinger : CovetousCreature
{
    [Constructible]
    public BoneSwordSlinger() : base(AIType.AI_Melee)
    {
        Name = "a bone swordslinger";
        Body = 147;
        BaseSoundID = 451;
    }

    [Constructible]
    public BoneSwordSlinger(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Name = "a bone sword slinger";
        Body = 147;
        BaseSoundID = 451;
    }

    public override string CorpseName => "a bone swordslinger corpse";
}

[SerializationGenerator(0, false)]
public partial class VileCadaver : CovetousCreature
{
    [Constructible]
    public VileCadaver() : base(AIType.AI_Melee)
    {
        Body = 154;
        BaseSoundID = 471;
    }

    [Constructible]
    public VileCadaver(int level, bool voidSpawn) : base(AIType.AI_Melee, level, voidSpawn)
    {
        Body = 154;
        BaseSoundID = 471;
    }

    public override string CorpseName => "a vile cadaver";
    public override string DefaultName => "a vile cadaver";
}

[SerializationGenerator(0, false)]
public partial class DiseasedLich : CovetousCreature
{
    [Constructible]
    public DiseasedLich() : base(AIType.AI_Mage)
    {
        Body = 24;
        BaseSoundID = 0x3E9;
    }

    [Constructible]
    public DiseasedLich(int level, bool voidSpawn) : base(AIType.AI_Mage, level, voidSpawn)
    {
        Body = 24;
        BaseSoundID = 0x3E9;
    }

    public override string CorpseName => "a liche's corpse";
    public override string DefaultName => "a diseased lich";
}

[SerializationGenerator(0, false)]
public partial class CovetousRevenant : CovetousCreature
{
    [Constructible]
    public CovetousRevenant() : base(AIType.AI_Mage)
    {
        Body = 400;
        Hue = 0x847E;

        var shroud = new Robe();
        shroud.ItemID = 0x2683;
        this.SetWearable(shroud, 0x4001);

        var boots = new Boots();
        this.SetWearable(boots, 0x4001);
    }

    [Constructible]
    public CovetousRevenant(int level, bool voidSpawn) : base(AIType.AI_Mage, level, voidSpawn)
    {
        Body = 400;
    }

    public override string CorpseName => "a revenant corpse";
    public override string DefaultName => "a covetous revenant";

    public override bool AlwaysMurderer => true;
}
