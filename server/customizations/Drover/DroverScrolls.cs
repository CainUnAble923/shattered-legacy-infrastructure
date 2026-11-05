// cc-P795: the pages of the Drover's Handbook. Drop one on the book to learn it, as any spell scroll. Animal Trainers sell them.
// Stock blank scroll art with a hue of their own, as the client has no scroll for these spells.

using ModernUO.Serialization;
using Server.Spells.Drover;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class CalmScroll : SpellScroll
{
    [Constructible]
    public CalmScroll(int amount = 1) : base(DroverSettings.CalmId, DroverSettings.ScrollArt, amount) => Hue = DroverSettings.ScrollHue;

    public override string DefaultName => "a drover's page: Calm";
}

[SerializationGenerator(0, false)]
public partial class HerdScroll : SpellScroll
{
    [Constructible]
    public HerdScroll(int amount = 1) : base(DroverSettings.HerdId, DroverSettings.ScrollArt, amount) => Hue = DroverSettings.ScrollHue;

    public override string DefaultName => "a drover's page: Herd";
}

[SerializationGenerator(0, false)]
public partial class LureScroll : SpellScroll
{
    [Constructible]
    public LureScroll(int amount = 1) : base(DroverSettings.LureId, DroverSettings.ScrollArt, amount) => Hue = DroverSettings.ScrollHue;

    public override string DefaultName => "a drover's page: Lure";
}
