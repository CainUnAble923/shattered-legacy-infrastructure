// cc-P128: ServUO pub57 Scripts/Items/Consumables/ParrotWafer.cs -> ours. Parrot wafers (ML; Cooking 37.5 from dough,
// honey and ten raw fish steaks). ServUO's pet parrot eats them; neither tree has the parrot yet (patch-backlog P14).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ParrotWafer : Item
{
    [Constructible]
    public ParrotWafer() : base(0x2FD6)
    {
        Hue = 0x38;
        Stackable = true;
    }

    public override int LabelNumber => 1072904; // Parrot Wafers
}
