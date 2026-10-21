// cc-P199: ServUO pub57 Scripts/Items/Functional/BaseArtifactLight.cs. An abstract light (pinned BaseLight, so it lights,
// douses and burns like any lamp) that adds the "artifact rarity ~1_val~" property line, gated on a ShowArtifactRarity
// virtual. ServUO's one subclass is the Doom IncenseBurner (Items/Artifacts/Decorative/DoomDecorationArtifacts.cs:1062),
// which is in neither pinned nor ours.
//
// One change: ServUO's IArtifact interface (Server/Interfaces.cs:141, BaseArtifactLight.cs:5) is not implemented. It is a
// ServUO core interface with no definition and no reader in pinned (the same as IsArtifact, port recipe "Items: two more
// differences"); ArtifactRarity is still an abstract member every subclass must give.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public abstract partial class BaseArtifactLight : BaseLight
{
    public BaseArtifactLight(int itemID) : base(itemID)
    {
    }

    public abstract int ArtifactRarity { get; }
    public virtual bool ShowArtifactRarity => true;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (ShowArtifactRarity)
        {
            list.Add(1061078, ArtifactRarity.ToString()); // artifact rarity ~1_val~
        }
    }
}
