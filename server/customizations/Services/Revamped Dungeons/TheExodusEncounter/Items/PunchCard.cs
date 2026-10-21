// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Items/PunchCard.cs (cc-P195): what a repaired Nexus prints;
// fifty feed the Keypunch Reader. Six hours.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class PunchCard : BaseDecayingItem
{
    [Constructible]
    public PunchCard() : base(0x0FF4)
    {
        LootType = LootType.Regular;
        Hue = Utility.RandomNondyedHue();
        Weight = 2;
    }

    public override int Lifespan => 21600;
    public override bool UseSeconds => false;

    public override int LabelNumber => 1153867; // Punch Card
}
