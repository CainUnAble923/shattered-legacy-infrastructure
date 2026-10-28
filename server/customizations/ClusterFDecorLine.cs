// One decoration line (a pinned .cfg block holding a single entry), built by pinned's own DecorationList.Read and
// Generate (Commands/Object Creation/Decorate.cs:1308-1380, :1220-1292), so construction and the duplicate check are
// pinned's. ClusterFAnkhs and ClusterFRootdeepSeeder place their ankhs with it. It was ClusterFNewHavenServicesSeeder's
// DecorEntry (cc-P17), which cc-P678 retired with the ML plaza's decoration.

using System.IO;
using System.Text;
using Server.Commands;

namespace Server;

internal sealed class ClusterFDecorLine
{
    public ClusterFDecorLine(string header, int x, int y, int z)
    {
        Header = header;
        Location = new Point3D(x, y, z);
    }

    /// <summary>The header as the .cfg writes it ("AnkhWest 0x0003 (Hue=0x47E)").</summary>
    public string Header { get; }

    public Point3D Location { get; }

    public DecorationList Read()
    {
        var text = $"{Header}\n{Location.X} {Location.Y} {Location.Z}\n";
        using var reader = new StreamReader(new MemoryStream(Encoding.ASCII.GetBytes(text)));
        return DecorationList.Read(reader);
    }
}
