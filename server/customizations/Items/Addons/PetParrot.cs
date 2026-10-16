// ServUO: Items/Addons/PetParrot.cs (cc-P129). The house parrot a ParrotItem becomes on a perch. Values verbatim;
// serialization by the generator: Birth is the one persisted field, and ServUO's "Frozen = true" on load is
// [AfterDeserialization]. Pinned members: OnStatsQuery sends the status through NetState.SendMobileStatus (what
// pinned's own OnStatsQuery does, Mobile.cs:7960), Utility.InUpdateRange takes two points, GetProperties takes an
// IPropertyList, FruitsAndVegies is FruitsAndVeggies, and ParrotWafer is ours (cc-P128).

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Multis;
using Server.Network;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class PetParrot : BaseCreature
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _birth;

    [Constructible]
    public PetParrot() : this(DateTime.MinValue, null, 0)
    {
    }

    [Constructible]
    public PetParrot(DateTime birth, string name, int hue) : base(AIType.AI_Animal, FightMode.None)
    {
        Title = "the parrot";
        Body = 0x11A;
        BaseSoundID = 0xBF;

        SetStr(1, 5);
        SetDex(25, 30);
        SetInt(2);

        SetHits(1, Str);
        SetStam(25, Dex);
        SetMana(0);

        SetResistance(ResistanceType.Physical, 2);

        SetSkill(SkillName.MagicResist, 4);
        SetSkill(SkillName.Tactics, 4);
        SetSkill(SkillName.Wrestling, 4);

        CantWalk = true;
        Frozen = true;
        Blessed = true;

        _birth = birth != DateTime.MinValue ? birth : Core.Now;

        if (name != null)
        {
            Name = name;
        }

        if (hue > 0)
        {
            Hue = hue;
        }
    }

    public override string DefaultName => "a pet parrot";

    public override bool NoHouseRestrictions => true;

    public override FoodType FavoriteFood => FoodType.FruitsAndVeggies;

    public static int GetWeeks(DateTime birth)
    {
        var span = Core.Now - birth;

        return (int)(span.TotalDays / 7);
    }

    public override void OnStatsQuery(Mobile from)
    {
        if (from.Map == Map && Utility.InUpdateRange(Location, from.Location) && from.CanSee(this))
        {
            var house = BaseHouse.FindHouseAt(this);

            if (house != null && house.IsCoOwner(from) && from.AccessLevel == AccessLevel.Player)
            {
                from.SendLocalizedMessage(1072625); // As the house owner, you may rename this Parrot.
            }

            from.NetState.SendMobileStatus(from, this);
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        var weeks = GetWeeks(_birth);

        if (weeks == 1)
        {
            list.Add(1072626); // 1 week old
        }
        else if (weeks > 1)
        {
            list.Add(1072627, weeks.ToString()); // ~1_AGE~ weeks old
        }
    }

    public override bool CanBeRenamedBy(Mobile from)
    {
        if (from.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        var house = BaseHouse.FindHouseAt(this);

        return house != null && house.IsCoOwner(from);
    }

    public override void OnSpeech(SpeechEventArgs e)
    {
        base.OnSpeech(e);

        if (Utility.RandomDouble() < 0.05)
        {
            Say(e.Speech);
            PlaySound(0xC0);
        }
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (dropped is ParrotWafer)
        {
            dropped.Delete();

            switch (Utility.Random(6))
            {
                case 0:
                    Say(1072602, "#" + Utility.RandomMinMax(1012003, 1012010));
                    break; // I just flew in from ~1_CITYNAME~ and boy are my wings tired!
                case 1:
                    Say(1072603);
                    break; // Wind in the sails!  Wind in the sails!
                case 2:
                    Say(1072604);
                    break; // Arrrr, matey!
                case 3:
                    Say(1072605);
                    break; // Loot and plunder!  Loot and plunder!
                case 4:
                    Say(1072606);
                    break; // I want a cracker!
                case 5:
                    Say(1072607);
                    break; // I'm just a house pet!
            }

            PlaySound(Utility.RandomMinMax(0xBF, 0xC3));
            Direction = Utility.GetDirection(Location, from.Location);

            return true;
        }

        return false;
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Frozen = true;
    }
}
