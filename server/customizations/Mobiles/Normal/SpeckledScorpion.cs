// ServUO: Mobiles/Normal/SpeckledScorpion.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SpeckledScorpion : Scorpion
{
    [Constructible]
    public SpeckledScorpion()
    {
        Tamable = false;
    }

    // Scorpion is stock (pinned Mobiles/Monsters/Reptile/Melee/Scorpion.cs, partial); this level has its own version slot.
    public override string CorpseName => "a speckled scorpion corpse";
    public override string DefaultName => "a speckled scorpion";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        
        if (Utility.RandomDouble() < 0.4)
        {
            c.DropItem(new SpeckledPoisonSac());
        }
    }
}
