// cc-P408: three Treasures of Sorcerer's Dungeon rewards that ServUO keeps with other seasonal events, ported here
// because this event is the first on the shard to hand them out:
//   SterlingSilverRing  ServUO Services/Seasonal Events/TreasuresOfKhaldun/Items/SterlingSilverRing.cs (50 points)
//   BootsOfEscaping     ServUO Services/Seasonal Events/TreasuresOfKotlCity/Items/Rewards.cs:327 (50 points)
//   TalonsOfEscaping    the same file :355 (50 points)
// When Treasures of Khaldun or Kotl City is ported, these are already here; do not port them twice.
//
// SterlingSilverRing: IsArtifact is dropped (no pinned reader, the recipe). Its double-click opens
// ApplySkillBonusGump (below) to set its one skill bonus, +20 in one of six combat skills, once.
// BootsOfEscaping and TalonsOfEscaping have no [Constructable] in ServUO (only the reward gump makes them, through
// Activator); here they are [Constructible] so staff can [add them, which ServUO's cannot. LeatherTalons is the
// shard's (Items/Equipment/Clothing/LeatherTalons.cs, on BaseSetClothing; Attributes is BaseClothing's).

using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SterlingSilverRing : SilverRing
{
    public static readonly SkillName[] Skills =
    {
        SkillName.Archery,
        SkillName.Fencing,
        SkillName.Macing,
        SkillName.Swords,
        SkillName.Throwing,
        SkillName.Wrestling
    };

    [Constructible]
    public SterlingSilverRing()
    {
        Attributes.RegenHits = 3;
        Attributes.RegenMana = 5;
        Attributes.WeaponDamage = 75;
    }

    public override int LabelNumber => 1155606; // Stirling Silver Ring

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;

    public bool HasSkillBonus => SkillBonuses.Skill_1_Value != 0;

    public override void OnDoubleClick(Mobile m)
    {
        if (IsChildOf(m.Backpack) && m is PlayerMobile pm && !HasSkillBonus)
        {
            pm.SendGump(new ApplySkillBonusGump(pm, this, SkillBonuses, Skills, 20, 0));
        }
        else
        {
            base.OnDoubleClick(m);
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (!HasSkillBonus)
        {
            list.Add(1155609); // Double Click to Set Skill Bonus
        }
    }
}

[SerializationGenerator(0, false)]
public partial class BootsOfEscaping : ThighBoots
{
    [Constructible]
    public BootsOfEscaping()
    {
        Attributes.BonusDex = 4;
        Attributes.RegenStam = 1;
    }

    public override int LabelNumber => 1155607; // Boots of Escaping
}

[SerializationGenerator(0, false)]
public partial class TalonsOfEscaping : LeatherTalons
{
    [Constructible]
    public TalonsOfEscaping()
    {
        Attributes.BonusDex = 4;
        Attributes.RegenStam = 1;
    }

    public override int LabelNumber => 1155682; // Talons of Escaping
}

/// <summary>
///     ServUO Gumps/ApplySkillBonusGump.cs: one button per skill (its cliloc name), then a confirmation; on yes, and only
///     if the item is still in the pack or worn, the bonus is set at the given index. ServUO's confirmation is its
///     GenericConfirmCallbackGump titled with the skill's name; pinned has none, so it is pinned's WarningGump with the
///     skill's cliloc as its header (AosSkillBonuses.GetLabel, ServUO's SkillInfo.Localization) and the same text, and
///     a "no" reopens this gump as ServUO's Refresh does.
/// </summary>
public class ApplySkillBonusGump : DynamicGump
{
    private readonly PlayerMobile _user;
    private readonly Item _item;
    private readonly AosSkillBonuses _bonuses;
    private readonly SkillName[] _skills;
    private readonly double _value;
    private readonly int _index;

    public ApplySkillBonusGump(PlayerMobile user, Item item, AosSkillBonuses bonuses, SkillName[] skills, double value, int index)
        : base(50, 50)
    {
        _user = user;
        _item = item;
        _bonuses = bonuses;
        _skills = skills;
        _value = value;
        _index = index;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();

        builder.AddBackground(0, 0, 400, _skills.Length * 22 + 65, 83);

        builder.AddHtmlLocalized(15, 15, 400, 20, 1155610, 0x7FFF); // Please Chooose a Skill

        var y = 40;

        for (var i = 0; i < _skills.Length; i++)
        {
            builder.AddButton(15, y, 4005, 4007, i + 100);
            builder.AddHtmlLocalized(50, y, 200, 20, AosSkillBonuses.GetLabel(_skills[i]), 0x7FFF);

            y += 22;
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var id = info.ButtonID - 100;

        if (id < 0 || id >= _skills.Length)
        {
            return;
        }

        var selection = _skills[id];

        // ServUO picks 1155611 for a weapon and for anything else alike.
        _user.SendGump(
            new WarningGump(
                AosSkillBonuses.GetLabel(selection),
                0x7FFF,
                1155611, // Are you sure you wish to apply the selected skill bonus to this item?
                0x7FFF,
                420,
                200,
                okay =>
                {
                    if (!okay)
                    {
                        _user.SendGump(new ApplySkillBonusGump(_user, _item, _bonuses, _skills, _value, _index));
                        return;
                    }

                    if (!_item.Deleted && (_item.IsChildOf(_user.Backpack) || _user.Items.Contains(_item)))
                    {
                        _user.SendLocalizedMessage(1155612); // A skill bonus has been applied to the item!
                        _bonuses.SetValues(_index, selection, _value);

                        _item.InvalidateProperties();
                    }
                }
            )
        );
    }
}
