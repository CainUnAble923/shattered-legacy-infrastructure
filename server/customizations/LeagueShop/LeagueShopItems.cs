// cc-P229 (F-13): the League shop's own items. Cosmetics and decor only: no item properties, nothing blessed but the
// League rune (built in LeagueShop.MakeLeagueRune from pinned's RecallRune), nothing that grants skill or stat points.
//
// LeagueTabard          pinned's Surcoat art (0x1FFD / 0x1FFE, MiddleTorso.cs), the League hue, cannot be dyed (F-13).
// LeagueCloak           pinned's Cloak art (0x1515 / 0x1530), the cloak hue.
// LeagueRegalia*        pinned's Robe (0x1F03), Cloak and Sandals (0x170D) art, the regalia hue.
// LeagueBanner          the pole banner art pinned's veteran Banner uses (0x15AE / 0x15AF, Banner.cs:146), League hue.
// LeagueTrophyStatuette the SE "sculpture" art (0x2848 / 0x2849, pinned ManStatuetteSouth/EastArtifact,
//                       SEDecorationArtifacts.cs:373, :384), engraved with the buyer's name and AP when bought.
// LeaguePigment         one use, the League hue, dyes what the Clean Up pigments dye (pinned BasePigmentsOfTokuno.IsValidItem).

using ModernUO.Serialization;
using Server.Engines.LeagueRewards;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
[Flippable(0x1FFD, 0x1FFE)]
public partial class LeagueTabard : Surcoat
{
    [Constructible]
    public LeagueTabard() : base(LeagueShop.LeagueHue)
    {
    }

    public override string DefaultName => "a League tabard";

    // F-13: "League hue, cannot be dyed".
    public override bool Dye(Mobile from, DyeTub sender)
    {
        from.SendLocalizedMessage(sender.FailMessage);
        return false;
    }
}

[SerializationGenerator(0, false)]
[Flippable(0x1515, 0x1530)]
public partial class LeagueCloak : Cloak
{
    [Constructible]
    public LeagueCloak() : base(LeagueShop.CloakHue)
    {
    }

    public override string DefaultName => "a League cloak";
}

[SerializationGenerator(0, false)]
public partial class LeagueRegaliaRobe : Robe
{
    [Constructible]
    public LeagueRegaliaRobe() : base(LeagueShop.RegaliaHue)
    {
    }

    public override string DefaultName => "a League regalia robe";
}

[SerializationGenerator(0, false)]
[Flippable(0x1515, 0x1530)]
public partial class LeagueRegaliaCloak : Cloak
{
    [Constructible]
    public LeagueRegaliaCloak() : base(LeagueShop.RegaliaHue)
    {
    }

    public override string DefaultName => "a League regalia cloak";
}

[SerializationGenerator(0, false)]
public partial class LeagueRegaliaSandals : Sandals
{
    [Constructible]
    public LeagueRegaliaSandals() : base(LeagueShop.RegaliaHue)
    {
    }

    public override string DefaultName => "League regalia sandals";
}

[SerializationGenerator(0, false)]
[Flippable(SouthArt, SouthArt + 1)]
public partial class LeagueBanner : Item
{
    public const int SouthArt = 0x15AE;

    [Constructible]
    public LeagueBanner() : base(SouthArt) => Hue = LeagueShop.LeagueHue;

    public override double DefaultWeight => 5.0;

    public override string DefaultName => "a League banner";
}

[SerializationGenerator(0, false)]
[Flippable(Art, Art + 1)]
public partial class LeagueTrophyStatuette : Item
{
    public const int Art = 0x2848;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _ownerName;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _achievementPoints;

    [Constructible]
    public LeagueTrophyStatuette() : base(Art)
    {
    }

    public LeagueTrophyStatuette(Mobile owner) : this()
    {
        _ownerName = owner.Name;
        _achievementPoints = LeagueShop.DataOf(owner)?.AchievementPoints ?? 0;
    }

    public override double DefaultWeight => 10.0;

    public override string DefaultName => "an achievement trophy";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (!string.IsNullOrEmpty(_ownerName))
        {
            list.Add($"Awarded to {_ownerName}");
            list.Add($"{_achievementPoints:N0} Achievement Points");
        }
    }
}

[SerializationGenerator(0, false)]
public partial class LeaguePigment : Item
{
    [Constructible]
    public LeaguePigment() : base(0xEFF) => Hue = LeagueShop.LeagueHue;

    public override double DefaultWeight => 1.0;

    public override string DefaultName => "League-hue pigment";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060584, 1); // uses remaining: ~1_val~
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        from.SendLocalizedMessage(1070929); // Select the artifact or enhanced magic item to dye.
        from.Target = new DyeTarget(this);
    }

    // The Clean Up pigments' checks (CleanUpPigments.cs, CompassionPigment.DyeTarget), one use.
    private class DyeTarget : Target
    {
        private readonly LeaguePigment _pigment;

        public DyeTarget(LeaguePigment pigment) : base(8, false, TargetFlags.None) => _pigment = pigment;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Item item || _pigment.Deleted)
            {
                return;
            }

            if (!item.IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1062334); // This item must be in your backpack to be used.
            }
            else if (item is MetalPigmentsOfTokuno or LesserPigmentsOfTokuno or PigmentsOfTokuno or CompassionPigment or LeaguePigment)
            {
                from.SendLocalizedMessage(1042083); // You cannot dye that.
            }
            else if (item.IsLockedDown)
            {
                from.SendLocalizedMessage(1070932); // You may not dye artifacts and enhanced magic items which are locked down.
            }
            else if (BasePigmentsOfTokuno.IsValidItem(item))
            {
                item.Hue = _pigment.Hue;
                from.PlaySound(0x23E);
                _pigment.Delete();
            }
            else
            {
                from.SendLocalizedMessage(1070931); // You can only dye artifacts and enhanced magic items with this tub.
            }
        }
    }
}
