// cc-P199: ServUO pub57 Scripts/Items/Resource/WorkableGlass.cs. Workable glass (1154170), a stackable commodity of
// weight 1 on art 19328 (0x4B80).
//
// One shape difference: ServUO sets Weight = 1.0 in the constructor (WorkableGlass.cs:23); this overrides
// DefaultWeight => 1.0, pinned's idiom, so the weight is not a saved field. Same weight. ServUO writes version 1 with
// nothing behind it; this starts at version 0 (new content, no saves to read).

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class WorkableGlass : Item, ICommodity
    {
        [Constructible]
        public WorkableGlass(int amount = 1) : base(19328)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1154170; // workable glass
        public override double DefaultWeight => 1.0;

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
