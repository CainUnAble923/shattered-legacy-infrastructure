// ServUO: Items/Internal/InvisibleTile.cs (cc-P197, a leaf dependency of the Underworld generator). Two tiles
// ServUO keeps in one file, kept together here as ServUO has them:
//   InvisibleTile   an unmovable tile (0x2198) players do not see and staff see as the LOS blocker's GM art
//                   (0x36FF): the thirteen tiles that fence the Fountain of Fortune's pool. ServUO's
//                   GetWorldPacketFor is pinned's SendWorldPacketTo, the shape of pinned Items/Misc/LOSBlocker.cs.
//   RevealTile      a tile (0x2E46) a player sees only from the tile itself (update range 1): ServUO's reveal
//                   carpet at the Underworld's two hidden passages. GetUpdateRange is pinned Item.cs:3306.
// ServUO's version is 1 on both; new content here, version 0.

using System;
using System.Buffers.Binary;
using ModernUO.Serialization;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class InvisibleTile : Item
{
    private const ushort GMItemId = 0x36FF;

    [Constructible]
    public InvisibleTile() : base(0x2198) => Movable = false;

    public override string DefaultName => "Invisible Tile";

    public override void SendWorldPacketTo(NetState ns, ReadOnlySpan<byte> world = default)
    {
        var mob = ns.Mobile;
        if (mob?.AccessLevel >= AccessLevel.GameMaster)
        {
            SendGMItem(ns);
        }
        else
        {
            base.SendWorldPacketTo(ns, world);
        }
    }

    private void SendGMItem(NetState ns)
    {
        var buffer = stackalloc byte[OutgoingEntityPackets.MaxWorldEntityPacketLength].InitializePacket();

        int length;

        if (ns.StygianAbyss)
        {
            length = OutgoingEntityPackets.CreateWorldEntity(buffer, this, ns.HighSeas);
            BinaryPrimitives.WriteUInt16BigEndian(buffer[8..10], GMItemId);
        }
        else
        {
            length = OutgoingItemPackets.CreateWorldItem(buffer, this);
            BinaryPrimitives.WriteUInt16BigEndian(buffer[7..9], GMItemId);
        }

        ns.Send(buffer[..length]);
    }
}

[SerializationGenerator(0, false)]
public partial class RevealTile : Item
{
    [Constructible]
    public RevealTile() : base(0x2E46) => Movable = false;

    public override int GetUpdateRange(Mobile m) => m.Player ? 1 : base.GetUpdateRange(m);
}
