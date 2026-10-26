// cc-P626: the Merchants' Exchange courier letter, Persuasion's work route (customizations ClusterFPersuasion). A sealed
// letter for one courier, addressed to a kind of NPC in a named town. Handing it to the right NPC (drag it onto them)
// consumes it and gives the courier one Persuasion gain event at the letter's difficulty. It has no value to any vendor,
// cannot be delivered by anyone else, and goes stale after a few hours.

using System;
using System.Text;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExchangeLetter : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _courier;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _issuer;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _recipientType;

    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _town;

    [SerializableField(4)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Map _destinationMap;

    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _difficulty;

    [SerializableField(6)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _expires;

    public ExchangeLetter(
        Mobile courier, Mobile issuer, Type recipientType, string town, Map map, int difficulty, DateTime expires
    ) : base(0x0E34)
    {
        _courier = courier;
        _issuer = issuer;
        _recipientType = recipientType.FullName;
        _town = town;
        _destinationMap = map;
        _difficulty = difficulty;
        _expires = expires;
        Name = "a sealed Exchange letter";
        Hue = 0x8A5;
        Weight = 0.1;
        LootType = LootType.Blessed;
    }

    public bool Expired => Core.Now >= _expires;

    /// <summary>"a banker in Minoc".</summary>
    public string Instructions => $"{Kind(_recipientType)} in {_town}";

    public bool IsRecipient(Mobile m) =>
        m is { Deleted: false, Alive: true, Player: false } && m != _issuer && m.Map == _destinationMap &&
        m.GetType().FullName == _recipientType && ClusterFPersuasion.TownOf(m) == _town;

    // "Banker" a banker, "InnKeeper" an inn keeper.
    public static string Kind(string typeName)
    {
        var name = typeName[(typeName.LastIndexOf('.') + 1)..];
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                sb.Append(' ');
            }

            sb.Append(char.ToLowerInvariant(name[i]));
        }

        var kind = sb.ToString();
        return ("aeiou".Contains(kind[0]) ? "an " : "a ") + kind;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"For {Instructions}");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (Expired)
        {
            from.SendMessage(0x3B2, "This letter's business has gone stale.");
            return;
        }

        var left = _expires - Core.Now;
        from.SendMessage(0x59, $"A sealed letter for {Instructions}. Hand it over yourself within {(int)Math.Ceiling(left.TotalMinutes)} minutes.");
    }

    public override bool OnDroppedToMobile(Mobile from, Mobile target)
    {
        if (from is PlayerMobile pm && !target.Player)
        {
            ClusterFPersuasion.Deliver(pm, this, target);
        }
        else
        {
            from.SendMessage(0x3B2, "The letter is sealed for the Exchange's own business.");
        }

        return false; // delivered (and deleted) or refused: either way it is not dropped on them
    }
}
