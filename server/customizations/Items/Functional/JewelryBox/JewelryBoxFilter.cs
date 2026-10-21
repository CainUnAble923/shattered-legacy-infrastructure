// cc-P199: ServUO pub57 Scripts/Items/Functional/JewelryBox/JewelryBoxFilter.cs. The jewelry box's view filter: at most
// one of ring, bracelet, earrings, necklace or talisman (setting one to true clears the others), or none for "All".
//
// Differences from ServUO:
// - It is a pinned generator-serialized object owned by its box (the BOBFilter shape, pinned
//   Engines/Bulk Orders/Books/BOBFilter.cs): the constructor takes the owner, and the five flags are saved through
//   their properties, so loading runs ServUO's setters exactly as ServUO's reader constructor does
//   (JewelryBoxFilter.cs:91-105). ServUO writes version 0 and nothing else for the default filter (:107-123); here all
//   five flags are always written.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0)]
    public partial class JewelryBoxFilter
    {
        [DirtyTrackingEntity]
        private IEntity _owner;

        public JewelryBoxFilter(IEntity owner) => _owner = owner;

        public bool IsDefault => !Ring && !Bracelet && !Earrings && !Necklace && !Talisman;

        public void Clear()
        {
            _ring = false;
            _bracelet = false;
            _earrings = false;
            _necklace = false;
            _talisman = false;
            (_owner as ISerializable)?.MarkDirty();
        }

        [SerializableProperty(0)]
        public bool Ring
        {
            get => _ring;
            set
            {
                if (value)
                {
                    Clear();
                }

                _ring = value;
                (_owner as ISerializable)?.MarkDirty();
            }
        }

        [SerializableProperty(1)]
        public bool Bracelet
        {
            get => _bracelet;
            set
            {
                if (value)
                {
                    Clear();
                }

                _bracelet = value;
                (_owner as ISerializable)?.MarkDirty();
            }
        }

        [SerializableProperty(2)]
        public bool Earrings
        {
            get => _earrings;
            set
            {
                if (value)
                {
                    Clear();
                }

                _earrings = value;
                (_owner as ISerializable)?.MarkDirty();
            }
        }

        [SerializableProperty(3)]
        public bool Necklace
        {
            get => _necklace;
            set
            {
                if (value)
                {
                    Clear();
                }

                _necklace = value;
                (_owner as ISerializable)?.MarkDirty();
            }
        }

        [SerializableProperty(4)]
        public bool Talisman
        {
            get => _talisman;
            set
            {
                if (value)
                {
                    Clear();
                }

                _talisman = value;
                (_owner as ISerializable)?.MarkDirty();
            }
        }
    }
}
