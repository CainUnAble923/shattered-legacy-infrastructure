// cc-P128: ServUO pub57 Scripts/Items/Consumables/ValentineChocolate.cs -> ours. A blessed Valentine's chocolate: dark,
// milk or white at random, with one of 22 messages on its properties. The title and the message are saved.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ValentineChocolate : Food
{
    private static readonly int[] _labels =
    {
        1114827, // "Someone Likes You"
        1114834, // "You're Sexy"
        1114836, // "You're The Best"
        1114822, // "Be Mine"
        1114829, // "Always Together"
        1114839, // "Sweet Memories"
        1114833, // "Hot Stuff"
        1114830, // "Thinking Of You"
        1114838, // "*hug*"
        1114832, // "*wink*"
        1114826, // "You're Sweet"
        1114818, // "Sweet Dreams"
        1114831, // "Kiss Me"
        1114825, // "Be My Valentine"
        1114835, // "Tasty!"
        1114840, // "How About A Date?"
        1114823, // "You're Cute"
        1114841, // "Let's Be Impulsive"
        1114821, // "Yours-4-Ever"
        1114824, // "Let's Be Friends"
        1114837, // "Someone Loves You"
        1114828  // "True Love"
    };

    [SerializableField(0)]
    [InvalidateProperties]
    private int _title;

    [SerializableField(1)]
    [InvalidateProperties]
    private int _label;

    [Constructible]
    public ValentineChocolate() : base(2538)
    {
        Weight = 1.0;
        LootType = LootType.Blessed;

        switch (Utility.Random(3))
        {
            case 0:
                Hue = 1125;
                _title = 1079994; // Dark chocolate
                break;
            case 1:
                Hue = 1121;
                _title = 1079995; // Milk chocolate
                break;
            case 2:
                Hue = 1150;
                _title = 1079996; // White Chocolate
                break;
        }

        _label = _labels[Utility.Random(_labels.Length)];
    }

    public override int LabelNumber => _title;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(_label);
    }
}
