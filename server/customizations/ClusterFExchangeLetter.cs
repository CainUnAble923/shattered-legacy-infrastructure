// cc-P626: the Merchants' Exchange courier letter, Persuasion's work route (customizations ClusterFPersuasion). A sealed
// letter for one courier, addressed to a kind of NPC in a named town. Handing it to the right NPC (drag it onto them)
// consumes it and gives the courier one Persuasion gain event at the letter's difficulty. It has no value to any vendor,
// cannot be delivered by anyone else, and goes stale after a few hours.
//
// cc-P647: a courier holds one outstanding letter wherever it is kept (a banked letter still holds the job). Letters keep
// that table themselves, claiming their courier when made and again when loaded, so it adds no save data. A letter for a
// kind of NPC is taken by that kind or a subclass of it (a Banker letter by a gypsy banker or a minter).

using System;
using System.Collections.Generic;
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
        Claim();
    }

    // Each courier's one outstanding letter (cc-P647).
    private static readonly Dictionary<Mobile, ExchangeLetter> _outstanding = new();

    public static ExchangeLetter OutstandingFor(Mobile courier)
    {
        if (courier == null || !_outstanding.TryGetValue(courier, out var letter))
        {
            return null;
        }

        if (!letter.Deleted && letter._courier == courier)
        {
            return letter;
        }

        _outstanding.Remove(courier);
        return null;
    }

    public bool IsOutstanding => !Deleted && OutstandingFor(_courier) == this;

    // When an older save holds several letters for one courier, the one that expires last holds the job.
    private void Claim()
    {
        if (_courier == null)
        {
            return;
        }

        var current = OutstandingFor(_courier);
        if (current == null || current == this || current._expires < _expires)
        {
            _outstanding[_courier] = this;
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization() => Claim();

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        if (_courier != null && _outstanding.TryGetValue(_courier, out var current) && current == this)
        {
            _outstanding.Remove(_courier);
        }
    }

    public bool Expired => Core.Now >= _expires;

    /// <summary>"a banker in Minoc".</summary>
    public string Instructions => $"{Kind(_recipientType)} in {_town}";

    public bool IsRecipient(Mobile m) =>
        m is { Deleted: false, Alive: true, Player: false } && m != _issuer && m.Map == _destinationMap &&
        IsRecipientKind(m) && ClusterFPersuasion.TownOf(m) == _town;

    // The named kind or a subclass of it: the board picks any Banker for a banker letter (cc-P647).
    private bool IsRecipientKind(Mobile m)
    {
        var kind = AssemblyHandler.FindTypeByFullName(_recipientType, false);
        return kind?.IsInstanceOfType(m) ?? m.GetType().FullName == _recipientType;
    }

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
