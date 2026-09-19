// ServUO: Items/Functional/DespiseAnkh.cs (CC4 Despise).
//
// The two ankhs at the top of the good side (5474, 525, 79) and the evil side (5472, 754, 10). Using one
// hands a WispOrb of its alignment to a player whose karma has the same sign, one orb per player.
//
// Dropped (D-30): the Whispering With Wisps hooks. ServUO completes a Town Cryer quest objective here
// (QuestHelper.HasQuest<WhisperingWithWispsQuest>, TownCryerSystem.CompleteQuest) and, in OnMovement,
// tells a quest holder "You have found an ankh" as they walk up. Both belong to the Town Cryer quest
// system, which is not on this shard; HandlesOnMovement/OnMovement served only that hint and go too.

using System.Linq;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Despise;

[SerializationGenerator(0, false)]
public partial class DespiseAnkh : BaseAddon
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Alignment _alignment;

    public DespiseAnkh(Alignment alignment)
    {
        _alignment = alignment;

        switch (alignment)
        {
            default:
            case Alignment.Good:
                AddComponent(new AddonComponent(4), 0, 0, 0);
                AddComponent(new AddonComponent(5), +1, 0, 0);
                break;
            case Alignment.Evil:
                AddComponent(new AddonComponent(2), 0, 0, 0);
                AddComponent(new AddonComponent(3), 0, -1, 0);
                break;
        }
    }

    public override void OnComponentUsed(AddonComponent c, Mobile from)
    {
        if (from.InRange(c.Location, 3) && from.Backpack != null)
        {
            if (WispOrb.Orbs.Any(x => x.Owner == from))
            {
                LabelTo(from, 1153357); // Thou can guide but one of us.
                return;
            }

            var alignment = Alignment.Neutral;

            if (from.Karma > 0 && _alignment == Alignment.Good)
            {
                alignment = Alignment.Good;
            }
            else if (from.Karma < 0 && _alignment == Alignment.Evil)
            {
                alignment = Alignment.Evil;
            }

            if (alignment != Alignment.Neutral)
            {
                var orb = new WispOrb(from, alignment);
                from.Backpack.DropItem(orb);
                from.SendLocalizedMessage(1153355); // I will follow thy guidance.
            }
            else
            {
                LabelTo(from, 1153350); // Thy spirit be not compatible with our goals!
            }
        }
    }
}
