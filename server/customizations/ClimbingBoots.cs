// ClimbingBoots.cs
//
// cc-P238 (F-36, ours). The Miners' Compact's climbing boots, three tiers sold by the Liaison in the satchels' pattern
// (MinersCompactLiaisonGump, Climbing Boots page): each upgrade consumes the boots before it.
//
//   Surveyor's     hop 4                        hue Verite     (the Surveyor's Ore Satchel's, its rank)
//   Delver's       hop 8,  Surefoot (half stamina)       Valorite (the Deepdelver's Satchel's)
//   Deepwarden's   hop 12, Surefoot, Ledge memory          Platinum (the Master Expedition Satchel's)
//
// The graphic is pinned's Boots, 0x170B (Items/Clothing/Shoes.cs:42), worn on the shoes layer (BaseShoes, Shoes.cs:9).
// Blessed, and bound to the buying account as the loot boxes' cosmetics are (AccountBinding, LootBoxes/LootBoxItems.cs):
// they cannot be traded, dropped, or lifted by another account. Lost boots are recovered at the Liaison, the registry key
// "compact.climbing_boots_t{n}" saying which tier the account owns.
//
// The type named ClimbingBoots is what cc-P192's Fountain of Youth checks for its "climbing" access kind
// (FountainOfYouth/FountainPool.cs:75): from this build on, its climbing-only pools are used.

using ModernUO.Serialization;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public abstract partial class ClimbingBoots : BaseShoes
{
    public const int Graphic = 0x170B; // pinned Boots

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _boundAccount;

    // Deepwarden's ledge memory: the last climbed tile.
    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Map _ledgeMap;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Point3D _ledgeLocation;

    protected ClimbingBoots(string boundAccount) : base(Graphic)
    {
        _boundAccount = boundAccount ?? "";
        Hue = CraftResources.GetHue(TierMetal);
        LootType = LootType.Blessed;
        Weight = 3.0;
    }

    /// <summary>1 Surveyor's, 2 Delver's, 3 Deepwarden's.</summary>
    public abstract int Tier { get; }

    /// <summary>The metal whose hue the tier wears: the satchel of the same rank's.</summary>
    protected abstract CraftResource TierMetal { get; }

    public static string RegistryKey(int tier) => $"compact.climbing_boots_t{tier}";

    public static ClimbingBoots Create(int tier, string boundAccount) => tier switch
    {
        1 => new SurveyorsClimbingBoots(boundAccount),
        2 => new DelversClimbingBoots(boundAccount),
        _ => new DeepwardensClimbingBoots(boundAccount)
    };

    public override string DefaultName => ClusterFClimbing.Tier(Tier).Name;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"Hop range: {ClusterFClimbing.Tier(Tier).HopRange} tiles");
        if (Tier >= 2)
        {
            list.Add("Surefoot: half the stamina for each hop");
        }

        if (Tier >= 3)
        {
            list.Add("Ledge memory: target yourself to return to your last climbed ledge");
        }

        list.Add("Bound to your account");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        if (Parent != pm)
        {
            pm.SendMessage(0x22, ClusterFClimbing.Message(ClusterFClimbing.HopResult.NotWorn, Tier));
            return;
        }

        pm.SendMessage(Tier >= 3 ? "Where do you climb to? (Target yourself to return to your last ledge.)" : "Where do you climb to?");
        pm.BeginTarget(-1, true, TargetFlags.None, (m, targeted) =>
        {
            if (m is not PlayerMobile who || Deleted)
            {
                return;
            }

            ClusterFClimbing.HopResult result;
            if (targeted == who)
            {
                result = ClusterFClimbing.TryLedgeReturn(who, this);
            }
            else if (targeted is IPoint3D p)
            {
                result = ClusterFClimbing.TryHop(who, this, p.X, p.Y);
            }
            else
            {
                return;
            }

            if (result != ClusterFClimbing.HopResult.Hopped)
            {
                who.SendMessage(0x22, ClusterFClimbing.Message(result, Tier));
            }
        });
    }

    // Boots a GM made with [add carry no account until a character puts them on; then they bind to that account.
    private bool Unbound => string.IsNullOrEmpty(_boundAccount);

    private bool Owns(Mobile m) => Unbound || AccountBinding.SameAccount(m, _boundAccount);

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => AccountBinding.Refuse(from);

    public override bool OnDroppedToWorld(Mobile from, Point3D p) => AccountBinding.Refuse(from);

    public override bool OnDroppedToMobile(Mobile from, Mobile target) =>
        target == from && Owns(from) ? base.OnDroppedToMobile(from, target) : AccountBinding.Refuse(from);

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
        Unbound || AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedInto(from, target, p) : AccountBinding.Refuse(from);

    public override bool OnDroppedOnto(Mobile from, Item target) =>
        Unbound || AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedOnto(from, target) : AccountBinding.Refuse(from);

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
    {
        if (from.AccessLevel < AccessLevel.GameMaster && !Owns(from))
        {
            reject = LRReason.CannotLift;
            return false;
        }

        return base.CheckLift(from, item, ref reject);
    }

    public override bool CanEquip(Mobile m) =>
        (m.AccessLevel >= AccessLevel.GameMaster || Owns(m)) && base.CanEquip(m);

    public override bool OnEquip(Mobile from)
    {
        if (Unbound && from.AccessLevel == AccessLevel.Player && from.Account != null)
        {
            BoundAccount = from.Account.Username;
        }

        return base.OnEquip(from);
    }
}

[SerializationGenerator(0, false)]
public partial class SurveyorsClimbingBoots : ClimbingBoots
{
    [Constructible]
    public SurveyorsClimbingBoots() : this("")
    {
    }

    public SurveyorsClimbingBoots(string boundAccount) : base(boundAccount)
    {
    }

    public override int Tier => 1;
    protected override CraftResource TierMetal => CraftResource.Verite;
}

[SerializationGenerator(0, false)]
public partial class DelversClimbingBoots : ClimbingBoots
{
    [Constructible]
    public DelversClimbingBoots() : this("")
    {
    }

    public DelversClimbingBoots(string boundAccount) : base(boundAccount)
    {
    }

    public override int Tier => 2;
    protected override CraftResource TierMetal => CraftResource.Valorite;
}

[SerializationGenerator(0, false)]
public partial class DeepwardensClimbingBoots : ClimbingBoots
{
    [Constructible]
    public DeepwardensClimbingBoots() : this("")
    {
    }

    public DeepwardensClimbingBoots(string boundAccount) : base(boundAccount)
    {
    }

    public override int Tier => 3;
    protected override CraftResource TierMetal => CraftResource.Platinum;
}
