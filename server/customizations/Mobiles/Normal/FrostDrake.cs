// ServUO: Mobiles/Normal/FrostDrake.cs (cc-P129). A ColdDrake named "a cold drake", as ServUO has it; serialization by
// the generator. ServUO's coldAura definition names FrostDrake beside ColdDrake, so the inherited aura is its own.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class FrostDrake : ColdDrake
{
    [Constructible]
    public FrostDrake()
    {
    }

    public override string CorpseName => "a drake corpse";
    public override string DefaultName => "a cold drake";
}
