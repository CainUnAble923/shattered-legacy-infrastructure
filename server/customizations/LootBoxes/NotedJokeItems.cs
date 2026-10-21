// cc-P263: the "Noted by the System" joke items and the Dented Box. Notes: shard-migration
// notes/cc-P263-noted-by-the-system-achievements.md (the reward table, and the items that still need art or live data).
//
// Dented Box             LootBox.Dent: an achievement's earn box made small (NotedReward.BoxTier) with one guaranteed joke
//                        item (ClusterFNotedBySystem.Dent). It opens like any loot box.
// NotedJokeDeco          a joke item to own and show, on a stock graphic and hue. No stats, no effect.
// LuckyUnderwear, CloakOfHastyDeparture, SquiresNecktie, FriendshipBracelet, MulesSunhat
//                        wearable joke items on stock clothing and jewelry. No stats, no effect; the "+0 Luck" and
//                        "Returnable" are property lines, nothing more.
//
// Every one is blessed and bound to the earner's account, as the box cosmetics are (AccountBinding), and is personalized
// when the box is filled (INotedJoke.Personalize): bound, and for some a line with the earner's name or count.

#nullable enable

using ModernUO.Serialization;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

public partial class LootBox
{
    public const string DentedBoxName = "Dented";
    public const int DentedBoxHue = 0x0973;

    /// <summary>cc-P263: makes this a Dented Box and puts <paramref name="joke"/> in it, shown as rare.</summary>
    public void Dent(Item joke)
    {
        BoxName = DentedBoxName;
        Hue = DentedBoxHue;
        _rareSlots.Add(_contents.Count);
        joke.Internalize();
        _contents.Add(joke);
        this.MarkDirty();
    }

    public bool Holds(Item item) => _contents.Contains(item);
}

/// <summary>A joke item a Dented Box carries: bound and personalized to the earner when the box is filled.</summary>
public interface INotedJoke
{
    void Personalize(PlayerMobile pm);
}

/// <summary>The binding every joke item shares (the same rules as BoundCosmeticSash's).</summary>
public static class NotedJokeBinding
{
    public static bool CheckLift(Mobile from, string? account, ref LRReason reject)
    {
        if (from.AccessLevel < AccessLevel.GameMaster && !AccountBinding.SameAccount(from, account))
        {
            reject = LRReason.CannotLift;
            return false;
        }

        return true;
    }
}

[SerializationGenerator(0, false)]
public abstract partial class NotedJokeDeco : Item, INotedJoke
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _boundAccount;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _inscription;

    protected NotedJokeDeco(int itemId, int hue, double weight) : base(itemId)
    {
        _boundAccount = "";
        _inscription = "";
        Hue = hue;
        Weight = weight;
        LootType = LootType.Blessed;
    }

    /// <summary>What it says, besides being bound. Each item's own joke.</summary>
    public abstract string Joke { get; }

    public virtual void Personalize(PlayerMobile pm)
    {
        BoundAccount = pm.Account?.Username ?? "";
        Inscription = InscriptionFor(pm);
    }

    protected virtual string InscriptionFor(PlayerMobile pm) => "";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(Joke);
        if (!string.IsNullOrEmpty(_inscription))
        {
            list.Add(_inscription);
        }

        list.Add("Bound to your account");
    }

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => AccountBinding.Refuse(from);

    public override bool OnDroppedToMobile(Mobile from, Mobile target) =>
        target == from && AccountBinding.SameAccount(from, _boundAccount) ? base.OnDroppedToMobile(from, target) : AccountBinding.Refuse(from);

    // Another character's pack is refused; a deco item may stand on a floor or in a chest (the earner's house), where only
    // a lift by anyone else is refused.
    public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
        target.RootParent is Mobile holder && !AccountBinding.SameAccount(holder, _boundAccount)
            ? AccountBinding.Refuse(from)
            : base.OnDroppedInto(from, target, p);

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) =>
        NotedJokeBinding.CheckLift(from, _boundAccount, ref reject) && base.CheckLift(from, item, ref reject);
}

[SerializationGenerator(0, false)]
public partial class StrategicBoulder : NotedJokeDeco
{
    [Constructible]
    public StrategicBoulder() : base(0x1363, 0x03E5, 10.0)
    {
    }

    public override string DefaultName => "a Strategic Boulder";
    public override string Joke => "It has never once been reached";
}

[SerializationGenerator(0, false)]
public partial class TheRock : NotedJokeDeco
{
    [Constructible]
    public TheRock() : base(0x1368, 0x0455, 10.0)
    {
    }

    public override string DefaultName => "The Rock";
    public override string Joke => "Mined from an empty vein. Somehow";
}

[SerializationGenerator(0, false)]
public partial class BucketOfPopcorn : NotedJokeDeco
{
    [Constructible]
    public BucketOfPopcorn() : base(0x14E0, 0x0035, 2.0)
    {
    }

    public override string DefaultName => "a Bucket of Popcorn";
    public override string Joke => "For watching other people's fights";
}

[SerializationGenerator(0, false)]
public partial class OkayestOwnerNameplate : NotedJokeDeco
{
    [Constructible]
    public OkayestOwnerNameplate() : base(0x0BD2, 0x0000, 2.0)
    {
    }

    public override string DefaultName => "a \"World's Okayest Owner\" nameplate";
    public override string Joke => "Awarded by the surviving pets";
}

[SerializationGenerator(0, false)]
public partial class ParticipationTrophy : NotedJokeDeco
{
    [Constructible]
    public ParticipationTrophy() : base(0xB4D1, 0x0000, 3.0)
    {
    }

    public override string DefaultName => "a Participation Trophy";
    public override string Joke => "You tried. A lot";

    protected override string InscriptionFor(PlayerMobile pm) =>
        $"Engraved: {pm.Name}, for {ClusterFAchievementSystem.GetCounter(pm.Account?.Username ?? "", ClusterFAchievementSystem.NotedCounter(TriggerKind.CraftFailStreak)):N0} failures in a row";
}

[SerializationGenerator(0, false)]
public partial class PersonalizedTombstone : NotedJokeDeco
{
    [Constructible]
    public PersonalizedTombstone() : base(0x1165, 0x0000, 10.0)
    {
    }

    public override string DefaultName => "a Personalized Tombstone";
    public override string Joke => "Reserved. Frequently used";

    protected override string InscriptionFor(PlayerMobile pm) => $"Here lies {pm.Name}. Again. At home.";
}

[SerializationGenerator(0, false)]
public partial class LuckyRug : NotedJokeDeco
{
    [Constructible]
    public LuckyRug() : base(0x0AB3, 0x0044, 3.0)
    {
    }

    public override string DefaultName => "a Lucky Rug";
    public override string Joke => "Stand here. Exactly here";
}

[SerializationGenerator(0, false)]
public partial class SouvenirToeTag : NotedJokeDeco
{
    [Constructible]
    public SouvenirToeTag() : base(0x0E34, 0x0481, 0.1)
    {
    }

    public override string DefaultName => "a Souvenir Toe Tag";
    public override string Joke => "Cause of death: arrival";

    protected override string InscriptionFor(PlayerMobile pm) => $"Name: {pm.Name}";
}

[SerializationGenerator(0, false)]
public partial class DizzyRunebook : NotedJokeDeco
{
    [Constructible]
    public DizzyRunebook() : base(0x22C5, 0x0489, 1.0)
    {
    }

    public override string DefaultName => "a Dizzy Runebook";
    public override string Joke => "Its pages are still spinning. It holds no runes";
}

[SerializationGenerator(0, false)]
public partial class HourglassOfEventually : NotedJokeDeco
{
    [Constructible]
    public HourglassOfEventually() : base(0x1810, 0x0000, 1.0)
    {
    }

    public override string DefaultName => "an Hourglass of Eventually";
    public override string Joke => "The sand will get there. Eventually";
}

[SerializationGenerator(0, false)]
public partial class BoxShapedBox : NotedJokeDeco
{
    [Constructible]
    public BoxShapedBox() : base(0x09A8, 0x0000, 1.0)
    {
    }

    public override string DefaultName => "a Box-Shaped Box";
    public override string Joke => "Opens anywhere. There is nothing in it";
}

[SerializationGenerator(0, false)]
public partial class BoxOfSmallerBoxes : NotedJokeDeco
{
    [Constructible]
    public BoxOfSmallerBoxes() : base(0x09AA, 0x0000, 2.0)
    {
    }

    public override string DefaultName => "a Box of Smaller Boxes";
    public override string Joke => "It's boxes all the way down. Thanks for watching";
}

// -- Wearables ----------------------------------------------------------------------

[SerializationGenerator(0, false)]
public partial class LuckyUnderwear : ShortPants, INotedJoke
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _boundAccount;

    [Constructible]
    public LuckyUnderwear() : base(0x0021)
    {
        _boundAccount = "";
        LootType = LootType.Blessed;
    }

    public override string DefaultName => "Lucky Underwear";

    public void Personalize(PlayerMobile pm) => BoundAccount = pm.Account?.Username ?? "";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("+0 Luck");
        list.Add("Bound to your account");
    }

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => AccountBinding.Refuse(from);

    public override bool OnDroppedToWorld(Mobile from, Point3D p) => AccountBinding.Refuse(from);

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
        AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedInto(from, target, p) : AccountBinding.Refuse(from);

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) =>
        NotedJokeBinding.CheckLift(from, _boundAccount, ref reject) && base.CheckLift(from, item, ref reject);
}

[SerializationGenerator(0, false)]
public partial class CloakOfHastyDeparture : Cloak, INotedJoke
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _boundAccount;

    [Constructible]
    public CloakOfHastyDeparture() : base(0x0496)
    {
        _boundAccount = "";
        LootType = LootType.Blessed;
    }

    public override string DefaultName => "a Cloak of Hasty Departure";

    public void Personalize(PlayerMobile pm) => BoundAccount = pm.Account?.Username ?? "";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Billows impressively as you leave");
        list.Add("Bound to your account");
    }

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => AccountBinding.Refuse(from);

    public override bool OnDroppedToWorld(Mobile from, Point3D p) => AccountBinding.Refuse(from);

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
        AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedInto(from, target, p) : AccountBinding.Refuse(from);

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) =>
        NotedJokeBinding.CheckLift(from, _boundAccount, ref reject) && base.CheckLift(from, item, ref reject);
}

[SerializationGenerator(0, false)]
public partial class SquiresNecktie : BodySash, INotedJoke
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _boundAccount;

    [Constructible]
    public SquiresNecktie() : base(0x0026)
    {
        _boundAccount = "";
        LootType = LootType.Blessed;
    }

    public override string DefaultName => "a Squire's Necktie";

    public void Personalize(PlayerMobile pm) => BoundAccount = pm.Account?.Username ?? "";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("For important meetings about other people's work");
        list.Add("Bound to your account");
    }

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => AccountBinding.Refuse(from);

    public override bool OnDroppedToWorld(Mobile from, Point3D p) => AccountBinding.Refuse(from);

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
        AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedInto(from, target, p) : AccountBinding.Refuse(from);

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) =>
        NotedJokeBinding.CheckLift(from, _boundAccount, ref reject) && base.CheckLift(from, item, ref reject);
}

[SerializationGenerator(0, false)]
public partial class FriendshipBracelet : GoldBracelet, INotedJoke
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _boundAccount;

    [Constructible]
    public FriendshipBracelet()
    {
        _boundAccount = "";
        Hue = 0x0030;
        LootType = LootType.Blessed;
    }

    public override string DefaultName => "a Friendship Bracelet";

    public void Personalize(PlayerMobile pm) => BoundAccount = pm.Account?.Username ?? "";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Returnable");
        list.Add("Bound to your account");
    }

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => AccountBinding.Refuse(from);

    public override bool OnDroppedToWorld(Mobile from, Point3D p) => AccountBinding.Refuse(from);

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
        AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedInto(from, target, p) : AccountBinding.Refuse(from);

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) =>
        NotedJokeBinding.CheckLift(from, _boundAccount, ref reject) && base.CheckLift(from, item, ref reject);
}

[SerializationGenerator(0, false)]
public partial class MulesSunhat : StrawHat, INotedJoke
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _boundAccount;

    [Constructible]
    public MulesSunhat() : base(0x0499)
    {
        _boundAccount = "";
        LootType = LootType.Blessed;
    }

    public override string DefaultName => "a Mule's Sunhat";

    public void Personalize(PlayerMobile pm) => BoundAccount = pm.Account?.Username ?? "";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Ear holes not included");
        list.Add("Bound to your account");
    }

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => AccountBinding.Refuse(from);

    public override bool OnDroppedToWorld(Mobile from, Point3D p) => AccountBinding.Refuse(from);

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
        AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedInto(from, target, p) : AccountBinding.Refuse(from);

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) =>
        NotedJokeBinding.CheckLift(from, _boundAccount, ref reject) && base.CheckLift(from, item, ref reject);
}
