// ServUO: Mobiles/NPCs/WanderingShaman.cs (cc-P198). A forced leaf dependency of the Eodon spawns (eleven of
// Eodon.xml's spawners name it): a WanderingHealer with a savage's name, no hair, its clothing stripped and a tribal
// mask (hue 2500) and bone arms and legs put on. Pinned's WanderingHealer is the parent, unchanged.
//
// ChangeRace => false is ServUO's switch off BaseVendor.CheckMorph. Pinned has no ChangeRace; the same effect is
// CheckMorph doing nothing (pinned's morphs are Gargoyle City, Umbra and Tokuno, none in Eodon, so this changes
// nothing a player sees today and keeps the shaman a shaman if one is ever spawned there). ServUO's version-1
// Deserialize branch deletes a robe left on version-0 saves; new content starts at 0 here and never needs it.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class WanderingShaman : WanderingHealer
{
    [Constructible]
    public WanderingShaman()
    {
        Name = NameList.RandomName("savage");
        Title = "the wandering shaman";
        Hue = 0;

        HairItemID = 0;
        FacialHairItemID = 0;

        var i = Items.Count;

        while (--i >= 0)
        {
            if (i < Items.Count && Items[i] is BaseClothing)
            {
                Items[i].Delete();
            }
        }

        this.SetWearable(new TribalMask(), 2500);
        this.SetWearable(new BoneArms());
        this.SetWearable(new BoneLegs());
    }

    public override void CheckMorph()
    {
    }
}
