// ServUO: Services/Expansions/Time Of Legends/Items/CardOfSemidar.cs (cc-P198). The legend cards of the Eodon game
// Semidar: double-click within 3 tiles shows the card's picture (gump 39904 + type, Krampus 39914), and the
// property list names the card (1156396 + type; Rafkin 1156562, Minax 1156981, Krampus 1158799).
//
// The default constructor picks Dupre, Nystul, Shamino or Juonar at random (ServUO's RandomMinMax(0, 3)); the other
// three come only from the typed constructor. ServUO's version 0 + the int it writes is [SerializableField(0)].
// Flipable is pinned's Flippable; ServUO's bare `new Gump` with one image is a DynamicGump with one image (Note.cs's
// shape). Card art 0x9C14/0x9C15 and all eight pictures are in the shard's client data (notes, client check).

using ModernUO.Serialization;
using Server.Gumps;

namespace Server.Items;

[Flippable(0x9C14, 0x9C15)]
[SerializationGenerator(0, false)]
public partial class CardOfSemidar : Item
{
    public enum CardType
    {
        Dupre,
        Nystul,
        Shamino,
        Juonar,
        ProfessorRafkin,
        Minax,
        Krampus
    }

    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private CardType _type;

    [Constructible]
    public CardOfSemidar() : this((CardType)Utility.RandomMinMax(0, 3))
    {
    }

    [Constructible]
    public CardOfSemidar(CardType type) : base(0x9C14) => _type = type;

    public override int LabelNumber => 1156395;

    public int PictureGumpId => _type == CardType.Krampus ? 39914 : 39904 + (int)_type;

    public int TitleNumber =>
        _type switch
        {
            CardType.ProfessorRafkin => 1156562,
            CardType.Minax           => 1156981,
            CardType.Krampus         => 1158799,
            _                        => 1156396 + (int)_type
        };

    public override void OnDoubleClick(Mobile from)
    {
        if (from.InRange(GetWorldLocation(), 3))
        {
            from.SendGump(new CardGump(PictureGumpId));
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(TitleNumber);
    }

    private class CardGump : DynamicGump
    {
        private readonly int _gumpId;

        public CardGump(int gumpId) : base(100, 100) => _gumpId = gumpId;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddImage(0, 0, _gumpId);
        }
    }
}
