// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Items/BestWaveBoard.cs (cc-P194). A bulletin board,
// "Void Pool - Best Wave", that opens the record wave's scoreboard for the facet it stands on (within three tiles).
// Nothing places it; a GM does.

using ModernUO.Serialization;
using Server.Engines.VoidPool;
using Server.Gumps;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BestWaveBoard : Item
{
    [Constructible]
    public BestWaveBoard() : base(7774)
    {
        Name = "Void Pool - Best Wave";
        Movable = false;
    }

    public override bool ForceShowProperties => true;

    public override void OnDoubleClick(Mobile m)
    {
        if (m is PlayerMobile pm && m.InRange(Location, 3))
        {
            var controller = VoidPoolController.GetInstance(m.Map);

            if (controller != null)
            {
                m.SendGump(new ScoresGump(controller, pm, ScoreType.BestWave));
            }
        }
    }
}
