// ServUO: Services/Revamped Dungeons/BlackthornDungeon/InvasionSpawner/InvasionDefinition.cs and the InvasionType
// enum of InvasionController.cs (cc-P196). The ten invasion kinds (an Invader wears one as a property line,
// 1154196 + type: "Dragon Invasion" .. "Nature Invasion") and the spawn definition of an invaded city (its spawn
// rectangles and where Minax's beacon stands). The controller that uses the definitions is the next prompt's
// (notes, Left for the next prompt: it needs an attackable item for the beacon).

namespace Server.Engines.Blackthorn;

public enum InvasionType
{
    Dragons,
    Undead,
    Elemental,
    Daemon,
    Orc,
    Wildlings,
    Frost,
    Arachnid,
    Fey,
    Nature
}

public class InvasionDefinition
{
    public InvasionDefinition(Rectangle2D[] spawn, Point3D beaconLoc)
    {
        SpawnRecs = spawn;
        BeaconLoc = beaconLoc;
    }

    public Rectangle2D[] SpawnRecs { get; }
    public Point3D BeaconLoc { get; }
}
