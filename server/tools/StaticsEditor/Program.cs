/// <summary>
/// StaticsEditor — adds map statics to UO client files for the
/// Miners' Compact Outpost at the New Haven South Mine (Trammel).
///
/// Edits statics1.mul + staidx1.mul in the target client folder.
/// Creates .bak backups before writing.
///
/// Usage: dotnet run [clientPath]
///   default clientPath = C:\UO\Classic Client - Experimental Copy
/// </summary>

using System.Text;

var clientPath = args.Length > 0 ? args[0] : @"C:\UO\Classic Client - Experimental Copy";

var staidxFile  = Path.Combine(clientPath, "staidx1.mul");
var staticsFile = Path.Combine(clientPath, "statics1.mul");

if (!File.Exists(staidxFile) || !File.Exists(staticsFile))
{
    Console.WriteLine($"ERROR: staidx1.mul or statics1.mul not found in:\n  {clientPath}");
    return 1;
}

// ── New statics to inject ────────────────────────────────────────────────────
// All on Trammel (map 1). Coordinates are world tiles.
// ItemID = UO art graphic ID (same as server-side Graphic value)
// Z = vertical offset (0 = ground level)
// Hue = 0 for default colour

var newStatics = new List<StaticEntry>
{
    // ── Zone A — Rest & Camp ─────────────────────────────────────────────────
    new(3510, 2742,  0, 0x0969, 0),  // Wooden stool
    new(3511, 2742,  0, 0x0B2C, 0),  // Small table (bar table south)
    new(3512, 2743,  0, 0x0A57, 0),  // Bedroll (rolled)
    new(3513, 2743,  0, 0x0A57, 0),  // Bedroll (rolled)

    // ── Zone B — Forge & Work ────────────────────────────────────────────────
    // Forge + anvil are already in the world as server items (ClusterFSouthMineDecor)
    new(3509, 2745,  0, 0x197A, 0),  // Bellows (forge bellows)
    new(3512, 2746,  0, 0x0F39, 0),  // Shovel (tool display)
    new(3513, 2746,  0, 0x0E86, 0),  // Pickaxe (tool display)
    new(3507, 2747,  0, 0x0FBB, 0),  // Water barrel / trough

    // ── Zone C — Storage & Ore ───────────────────────────────────────────────
    new(3515, 2745,  0, 0x0E77, 0),  // Barrel (supplies)
    new(3516, 2745,  0, 0x0E77, 0),  // Barrel
    new(3517, 2745,  0, 0x0E77, 0),  // Barrel
    new(3515, 2748,  0, 0x0E3D, 0),  // Wooden crate
    new(3517, 2748,  0, 0x0E3D, 0),  // Wooden crate
    new(3518, 2746,  0, 0x19B9, 0),  // Iron ore pile (display)
    new(3518, 2748,  0, 0x09AB, 0),  // Metal strongbox (survey records)

    // ── Zone D — Entrance & Signage ──────────────────────────────────────────
    new(3512, 2739,  0, 0x0BD2, 0),  // Hanging sign post
    new(3508, 2741,  5, 0x0F6A, 0),  // Torch (raised on post height)
    new(3516, 2741,  5, 0x0F6A, 0),  // Torch
    new(3509, 2740,  0, 0x0F5E, 0),  // Fence post (wooden)
    new(3511, 2740,  0, 0x0F5E, 0),  // Fence post
    new(3513, 2740,  0, 0x0F5E, 0),  // Fence post
    new(3515, 2740,  0, 0x0F5E, 0),  // Fence post
};

// ── Map 1 (Trammel) dimensions ───────────────────────────────────────────────
// 7168 × 4096 tiles → 896 × 512 blocks (each block = 8×8 tiles)
const int MapHeightBlocks = 512; // 4096 / 8

// ── Load staidx ──────────────────────────────────────────────────────────────
Console.WriteLine("Reading staidx1.mul...");
var staidxBytes = File.ReadAllBytes(staidxFile);
int totalBlocks  = staidxBytes.Length / 12;
Console.WriteLine($"  {totalBlocks} blocks in index.");

// Parse into mutable structure: offset, length (extra ignored)
var blockOffsets = new int[totalBlocks];
var blockLengths = new int[totalBlocks];
for (int i = 0; i < totalBlocks; i++)
{
    blockOffsets[i] = BitConverter.ToInt32(staidxBytes, i * 12);
    blockLengths[i] = BitConverter.ToInt32(staidxBytes, i * 12 + 4);
}

// ── Load statics into per-block lists ────────────────────────────────────────
Console.WriteLine("Reading statics1.mul...");
var staticsBytes = File.ReadAllBytes(staticsFile);

// One list per block (each entry = 7 bytes: id(2) x(1) y(1) z(1) hue(2))
var blockData = new List<byte[]>[totalBlocks];
for (int i = 0; i < totalBlocks; i++)
    blockData[i] = new List<byte[]>();

for (int i = 0; i < totalBlocks; i++)
{
    int offset = blockOffsets[i];
    int length = blockLengths[i];

    if (offset == -1 || length <= 0) continue; // empty block

    for (int pos = offset; pos + 7 <= offset + length; pos += 7)
    {
        var entry = new byte[7];
        Array.Copy(staticsBytes, pos, entry, 0, 7);
        blockData[i].Add(entry);
    }
}

// ── Inject new statics ───────────────────────────────────────────────────────
int injected = 0;
foreach (var s in newStatics)
{
    int blockX = s.X / 8;
    int blockY = s.Y / 8;
    int blockIndex = blockX * MapHeightBlocks + blockY;

    if (blockIndex < 0 || blockIndex >= totalBlocks)
    {
        Console.WriteLine($"  SKIP ({s.X},{s.Y}) — block {blockIndex} out of range.");
        continue;
    }

    byte xInBlock = (byte)(s.X & 7);
    byte yInBlock = (byte)(s.Y & 7);

    // Check for exact duplicate (same tile, same id, same z)
    bool dupe = false;
    foreach (var existing in blockData[blockIndex])
    {
        ushort eId  = BitConverter.ToUInt16(existing, 0);
        byte   eX   = existing[2];
        byte   eY   = existing[3];
        sbyte  eZ   = (sbyte)existing[4];
        if (eId == s.Id && eX == xInBlock && eY == yInBlock && eZ == s.Z)
        { dupe = true; break; }
    }
    if (dupe)
    {
        Console.WriteLine($"  SKIP ({s.X},{s.Y}) id=0x{s.Id:X4} — already present.");
        continue;
    }

    var entry = new byte[7];
    BitConverter.TryWriteBytes(entry.AsSpan(0, 2), s.Id);
    entry[2] = xInBlock;
    entry[3] = yInBlock;
    entry[4] = (byte)(sbyte)s.Z;
    BitConverter.TryWriteBytes(entry.AsSpan(5, 2), s.Hue);

    blockData[blockIndex].Add(entry);
    injected++;
    Console.WriteLine($"  + ({s.X},{s.Y}) z={s.Z,3} id=0x{s.Id:X4}  block={blockIndex} [{xInBlock},{yInBlock}]");
}

if (injected == 0)
{
    Console.WriteLine("\nNothing new to inject — all statics already present. Aborting.");
    return 0;
}

Console.WriteLine($"\n{injected} statics to inject.");

// ── Rebuild statics1.mul ──────────────────────────────────────────────────────
Console.WriteLine("Rebuilding statics1.mul...");
using var newStaticsStream = new MemoryStream();
var newOffsets = new int[totalBlocks];
var newLengths = new int[totalBlocks];

for (int i = 0; i < totalBlocks; i++)
{
    var entries = blockData[i];
    if (entries.Count == 0)
    {
        newOffsets[i] = -1; // 0xFFFFFFFF
        newLengths[i] = 0;
        continue;
    }
    newOffsets[i] = (int)newStaticsStream.Position;
    newLengths[i] = entries.Count * 7;
    foreach (var e in entries)
        newStaticsStream.Write(e, 0, 7);
}

// ── Rebuild staidx1.mul ───────────────────────────────────────────────────────
Console.WriteLine("Rebuilding staidx1.mul...");
using var newStaidxStream = new MemoryStream(totalBlocks * 12);
for (int i = 0; i < totalBlocks; i++)
{
    newStaidxStream.Write(BitConverter.GetBytes(newOffsets[i]));
    newStaidxStream.Write(BitConverter.GetBytes(newLengths[i]));
    newStaidxStream.Write(BitConverter.GetBytes(0)); // extra, always 0
}

// ── Backup & write ────────────────────────────────────────────────────────────
Backup(staidxFile);
Backup(staticsFile);

File.WriteAllBytes(staticsFile, newStaticsStream.ToArray());
File.WriteAllBytes(staidxFile,  newStaidxStream.ToArray());

Console.WriteLine($"\nDone. Injected {injected} statics into:");
Console.WriteLine($"  {staticsFile}");
Console.WriteLine($"  {staidxFile}");
Console.WriteLine("Originals backed up as .bak");
Console.WriteLine("\nOpen CentrED and navigate to ~(3508–3520, 2740–2750) on Trammel to review.");
return 0;

// ── Helpers ───────────────────────────────────────────────────────────────────
static void Backup(string path)
{
    var bak = path + ".bak";
    if (!File.Exists(bak))
    {
        File.Copy(path, bak);
        Console.WriteLine($"  Backed up: {Path.GetFileName(bak)}");
    }
    else
    {
        Console.WriteLine($"  Backup exists, skipping: {Path.GetFileName(bak)}");
    }
}

record StaticEntry(int X, int Y, int Z, ushort Id, ushort Hue);
