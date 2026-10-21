// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Items/ExodusAlterAddon.cs (cc-P195): the four hued stone
// steps the Summoning Tome stands on, placed and deleted by ExodusTomeAltar. ServUO's spelling kept.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExodusAlterAddon : BaseAddon
{
    public const int StepHue = 2702;

    [Constructible]
    public ExodusAlterAddon()
    {
        AddStep(0x3F9, 0, 1, 5);
        AddStep(0x3FA, 1, 0, 5);
        AddStep(0x3F7, 0, 0, 5);
        AddStep(0x3F8, 1, 1, 5);
    }

    private void AddStep(int id, int x, int y, int z)
    {
        var component = new AddonComponent(id) { Hue = StepHue };
        AddComponent(component, x, y, z);
    }
}
