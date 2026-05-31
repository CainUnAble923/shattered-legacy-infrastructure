using ModernUO.Serialization;

namespace Server.Items;

/// <summary>
/// Migration stub — replaced by DevTestingCrystal.
/// Keeps old saves from throwing type-not-found errors.
/// Any tokens remaining in the world are inert (no commands, no bypass logic).
/// GMs can [delete them manually or let them sit harmlessly.
/// </summary>
[SerializationGenerator(0, false)]
public partial class CompactTestingToken : Item
{
    public CompactTestingToken() : base(0x1ECD) { }
}
