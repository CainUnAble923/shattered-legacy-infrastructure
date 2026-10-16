// cc-P128: ServUO pub57 Scripts/Services/Plants/PottedCoffeePlant.cs -> ours. A potted coffee plant (a house
// decoration with a secure level): it grows a stage every 23 hours, checked hourly, and at stage four anyone the level
// allows picks a coffee pod from it, which drops it back a stage. Coffee grounds and the pod are the two resources;
// ServUO's coffee-mug cooking entry is not registered (CoffeeMug is in neither tree).

using System;
using ModernUO.Serialization;
using Server.Engines.Plants;
using Server.Gumps;
using Server.Multis;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class CoffeeGrounds : Item
{
    [Constructible]
    public CoffeeGrounds(int amount = 1) : base(0x573B)
    {
        Hue = 1022;
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1155735; // Coffee Grounds
}

[SerializationGenerator(0, false)]
public partial class CoffeePod : Item
{
    [Constructible]
    public CoffeePod(int amount = 1) : base(Utility.RandomBool() ? 0x9A24 : 0x9A25)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1123484; // Coffee Pod
}

[SerializationGenerator(0, false)]
public partial class PottedCoffeePlant : Item, ISecurable
{
    public static readonly TimeSpan CheckDelay = TimeSpan.FromHours(23.0);

    [SerializableField(0, fieldChanged: nameof(OnPlantStatusChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private PlantStatus _plantStatus;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private SecureLevel _level;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _nextGrowth;

    private TimerExecutionToken _timerToken;

    [Constructible]
    public PottedCoffeePlant() : base(0x9A20)
    {
        Weight = 5.0;
        PlantStatus = PlantStatus.Stage1;
        _nextGrowth = Core.Now + CheckDelay;
        StartTimer();
    }

    public override int LabelNumber => 1123480; // Potted Coffee Plant

    private void OnPlantStatusChanged(PlantStatus oldValue, PlantStatus newValue)
    {
        ItemID = newValue switch
        {
            PlantStatus.Stage2 => 0x9A21,
            PlantStatus.Stage3 => 0x9A22,
            PlantStatus.Stage4 => 0x9A23,
            _                  => 0x9A20
        };
    }

    public bool CheckAccessible(Mobile from, Item item)
    {
        if (from.AccessLevel >= AccessLevel.GameMaster)
        {
            return true; // Staff can access anything
        }

        var house = BaseHouse.FindHouseAt(item);

        if (house == null)
        {
            return false;
        }

        return Level switch
        {
            SecureLevel.Owner    => house.IsOwner(from),
            SecureLevel.CoOwners => house.IsCoOwner(from),
            SecureLevel.Friends  => house.IsFriend(from),
            SecureLevel.Anyone   => true,
            SecureLevel.Guild    => house.IsGuildMember(from),
            _                    => false
        };
    }

    public void OnTick()
    {
        if (PlantStatus < PlantStatus.Stage4)
        {
            if (NextGrowth < Core.Now)
            {
                PlantStatus++;
                NextGrowth = Core.Now + CheckDelay;
            }
        }
        else
        {
            StopTimer();
        }
    }

    public void StopTimer() => _timerToken.Cancel();

    public void StartTimer()
    {
        if (_timerToken.Running)
        {
            return;
        }

        Timer.StartTimer(TimeSpan.FromHours(1.0), TimeSpan.FromHours(1.0), OnTick, out _timerToken);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(this, 4))
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3E9, 1019045); // I can't reach that.
            return;
        }

        if (!CheckAccessible(from, this))
        {
            return;
        }

        if (PlantStatus == PlantStatus.Stage4)
        {
            LabelTo(from, 1155694); // *You carefully pick some pods from the plant*
            PlantStatus--;
            from.AddToBackpack(new CoffeePod());
            NextGrowth = Core.Now + CheckDelay;
            StartTimer();
        }
        else
        {
            from.SendLocalizedMessage(1155695); // The plant is not ready to be picked from.
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        StopTimer();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (PlantStatus < PlantStatus.Stage4)
        {
            StartTimer();
        }
    }
}
