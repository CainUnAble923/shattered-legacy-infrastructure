// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Artifacts/EnchantedCoralBracelet.cs -> ours, the High
// Seas fishing port. An Osiredon unique artifact.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class EnchantedCoralBracelet : SilverBracelet
{
    [Constructible]
    public EnchantedCoralBracelet()
    {
        Hue = 1548;
        Attributes.BonusHits = 5;
        Attributes.RegenMana = 1;
        Attributes.AttackChance = 5;
        Attributes.DefendChance = 15;
        Attributes.CastSpeed = 1;
        Attributes.CastRecovery = 3;
        Attributes.SpellDamage = 10;
    }

    public override int LabelNumber => 1116624; // Enchanted Coral Bracelet
}
