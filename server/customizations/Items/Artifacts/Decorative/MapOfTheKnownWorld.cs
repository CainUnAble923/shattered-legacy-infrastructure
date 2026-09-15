using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Multis;
using Server.Network;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/MapOfTheKnownWorld.cs (CC9 batch 5). A securable wall map that shows one
    // gump image when double-clicked within two tiles. The secure level is the one persisted field.
    [Flippable(0x3BB6, 0x3BB7)]
    [SerializationGenerator(0, false)]
    public partial class MapOfTheKnownWorld : Item, ISecurable
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private SecureLevel _level;

        [Constructible]
        public MapOfTheKnownWorld() : base(0x3BB6) => LootType = LootType.Blessed;

        public override string DefaultName => "a map of the known world";
        public override double DefaultWeight => 1.0;

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, ref list);
            SetSecureLevelEntry.AddTo(from, this, ref list);
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from.InRange(GetWorldLocation(), 2))
            {
                from.CloseGump<InternalGump>();
                from.SendGump(new InternalGump());
            }
            else
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            }
        }

        private class InternalGump : Gump
        {
            public InternalGump() : base(50, 50) => AddImage(0, 0, 0x12B);
        }
    }
}
