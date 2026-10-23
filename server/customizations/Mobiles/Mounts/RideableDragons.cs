// cc-P277 (P139-D6): the rideable red dragon, and the colorable dragon beside it, on TheArt's bodies (Custom Animation
// Set, servuo.dev 1697). Notes in shard-migration notes/cc-P277-rideable-red-dragon.md.
//
// The client side: body 2086 (2087) is a creature body in our anim7 (player-package records.json bodyAnims, slot 207
// and 208, ANIMAL); the mount item 0x6E87 (0x6E88) has our tiledata record on the Mount layer whose Animation is that
// body, which is how TazUO draws a ridden mount (Item.GetGraphicForAnimation: the mount item's tiledata AnimID). The
// values below are records.json's; player-package/tests/Build-UoOverrides.Tests.ps1 compares the two.
//
// STAFF ONLY, PLACEHOLDERS FOR CHASE: the numbers are a stock horse's (pinned Horse.cs) so the dragons are no stronger
// than any mount, they cannot be tamed, and nothing places or sells them. How players get one (a League or
// achievement reward, or a tame) and what it should be able to do are Chase's call. A GameMaster rides one at once
// ([add RideableRedDragon, then double-click it: pinned BaseMount.OnDoubleClick lets GameMaster and up ride any mount).

using ModernUO.Serialization;

namespace Server.Mobiles;

public static class RideableDragonArt
{
    public const int RedBody = 2086;
    public const int RedMountItem = 0x6E87;
    public const int ColorableBody = 2087;
    public const int ColorableMountItem = 0x6E88;

    // A stock horse's numbers (pinned Horse.cs), for both: placeholders until Chase decides what a dragon mount is.
    internal static void SetPlaceholderStats(BaseMount m)
    {
        m.SetStr(22, 98);
        m.SetDex(56, 75);
        m.SetInt(6, 10);

        m.SetHits(28, 45);
        m.SetMana(0);

        m.SetDamage(3, 4);

        m.SetDamageType(ResistanceType.Physical, 100);

        m.SetResistance(ResistanceType.Physical, 15, 20);

        m.SetSkill(SkillName.MagicResist, 25.1, 30.0);
        m.SetSkill(SkillName.Tactics, 29.3, 44.0);
        m.SetSkill(SkillName.Wrestling, 29.3, 44.0);

        m.Fame = 300;
        m.Karma = 300;

        m.Tamable = false;
        m.ControlSlots = 1;
    }
}

[SerializationGenerator(0, false)]
public partial class RideableRedDragon : BaseMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RideableRedDragon() : base(RideableDragonArt.RedBody, RideableDragonArt.RedMountItem, AIType.AI_Animal, FightMode.Aggressor)
    {
        BaseSoundID = 362; // the stock dragon's
        RideableDragonArt.SetPlaceholderStats(this);
    }

    public override string DefaultName => "a red riding dragon";
    public override string CorpseName => "a red riding dragon corpse";
    public override FoodType FavoriteFood => FoodType.Meat;
}

[SerializationGenerator(0, false)]
public partial class RideableColorableDragon : BaseMount
{
    // The art is drawn to take a hue: the staff sets one ([set hue), and the mount item follows it (BaseMount.Hue).
    [Constructible(AccessLevel.GameMaster)]
    public RideableColorableDragon() : base(RideableDragonArt.ColorableBody, RideableDragonArt.ColorableMountItem, AIType.AI_Animal, FightMode.Aggressor)
    {
        BaseSoundID = 362;
        RideableDragonArt.SetPlaceholderStats(this);
    }

    public override string DefaultName => "a riding dragon";
    public override string CorpseName => "a riding dragon corpse";
    public override FoodType FavoriteFood => FoodType.Meat;
}
