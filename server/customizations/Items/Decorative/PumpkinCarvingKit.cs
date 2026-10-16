// ServUO: Items/Decorative/PumpkinCarvingKit.cs (cc-P129): the Jack O' Lantern Carving Kit, its target, the
// PumpkinDefinition pair, BaseCarvablePumpkin and the three carvable pumpkins, one file as in ServUO (the Butcher
// packs the kit). Values verbatim; serialization by the generator: CarvedBy is the pumpkin's one persisted field.
// Flipable is pinned's Flippable; GetProperties takes pinned's IPropertyList; pinned BaseLight's UnlitItemID is a
// virtual with a 0 default, overridden here as ServUO overrides it. ServUO's `; ;` typo in UnlitItemID is not kept.

using System.Linq;
using ModernUO.Serialization;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

public class PumpkinDefinition
{
    public int UnlitItemID { get; set; }
    public int LitItemID { get; set; }

    public PumpkinDefinition(int unlit, int lit)
    {
        UnlitItemID = unlit;
        LitItemID = lit;
    }
}

[Flippable(0x992D, 0x992E)]
[SerializationGenerator(0, false)]
public partial class PumpkinCarvingKit : Item
{
    public override int LabelNumber => 1154271; // Jack O' Lantern Carving Kit

    [Constructible]
    public PumpkinCarvingKit() : base(0x992D)
    {
        Weight = 1.0;
        Hue = 1258;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1080058); // This must be in your backpack to use it.
        }
        else
        {
            from.SendLocalizedMessage(1154273); // What do you wish to carve?
            from.Target = new InternalTarget(this);
        }
    }

    private class InternalTarget : Target
    {
        private readonly Item _item;

        public InternalTarget(Item item) : base(2, true, TargetFlags.None)
        {
            _item = item;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is BaseCarvablePumpkin pumpkin)
            {
                from.PlaySound(0x249);

                pumpkin.ItemID = pumpkin.PumpkinDefinition[Utility.Random(pumpkin.PumpkinDefinition.Length)].UnlitItemID;
                pumpkin.CarvedBy = from.Name;

                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1154339); // *You carefully carve the pumpkin*

                _item.Delete();
            }
            else
            {
                from.SendLocalizedMessage(1154272); // That is not suitable for carving.
            }
        }
    }
}

[SerializationGenerator(0, false)]
public abstract partial class BaseCarvablePumpkin : BaseLight
{
    public override int LabelNumber => CarvedBy == null ? 1123239 : 1096937; // Carvable Pumpkin | carved pumpkin

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [InvalidateProperties]
    private string _carvedBy;

    public abstract PumpkinDefinition[] PumpkinDefinition { get; }

    public BaseCarvablePumpkin(int id) : base(id)
    {
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (CarvedBy == null)
        {
            return;
        }

        base.OnDoubleClick(from);
    }

    public override int LitItemID => PumpkinDefinition.FirstOrDefault(x => x.UnlitItemID == ItemID)?.LitItemID ?? 0;
    public override int UnlitItemID => PumpkinDefinition.FirstOrDefault(x => x.LitItemID == ItemID)?.UnlitItemID ?? 0;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (CarvedBy != null)
        {
            list.Add(1154350, CarvedBy); // Carved By ~1_NAME~
        }
    }
}

[SerializationGenerator(0, false)]
public partial class CarvablePumpkinTall : BaseCarvablePumpkin
{
    private static readonly PumpkinDefinition[] _definitions =
    {
        new(0x9934, 0x9931),
        new(0x9935, 0x9936),
        new(0x9939, 0x993A),
        new(0x993D, 0x993E),
        new(0x9941, 0x9942),
        new(0x9945, 0x9946),
        new(0x9949, 0x994A),
        new(0x9951, 0x9952)
    };

    public override PumpkinDefinition[] PumpkinDefinition => _definitions;

    [Constructible]
    public CarvablePumpkinTall() : base(0x992F)
    {
        Weight = 16.0;
    }
}

[SerializationGenerator(0, false)]
public partial class CarvableGordPumpkinTall : BaseCarvablePumpkin
{
    private static readonly PumpkinDefinition[] _definitions =
    {
        new(0x9D23, 0x9D24),
        new(0x9D27, 0x9D28)
    };

    public override PumpkinDefinition[] PumpkinDefinition => _definitions;

    [Constructible]
    public CarvableGordPumpkinTall() : base(0x9D22)
    {
        Weight = 18.0;
    }
}

[SerializationGenerator(0, false)]
public partial class CarvablePlainPumpkin : BaseCarvablePumpkin
{
    private static readonly PumpkinDefinition[] _definitions =
    {
        new(0x9F23, 0x9F24),
        new(0x9F27, 0x9F28)
    };

    public override PumpkinDefinition[] PumpkinDefinition => _definitions;

    [Constructible]
    public CarvablePlainPumpkin() : base(0x9F51)
    {
        Weight = 18.0;
    }
}
