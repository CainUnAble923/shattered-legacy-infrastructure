// cc-P199: ServUO pub57 Scripts/Items/Functional/ChickenCoop.cs. The chicken coop (art 0x4513/0x4514, weight 20), a
// house stable for chickens: locked down in a house, anyone with secure access at its level (co-owners by default) can
// stable up to three chickens, chicken lizards or battle chicken lizards in it (context entry 6126 or saying "stable"),
// and claim them back (entry 6127, or "claim" for all of them, or any other claim phrase for a list gump). A stabled
// chicken is internalized and uncontrolled, as at an animal trainer; deleting the coop deletes what is in it.
// ServUO makes it by carpentry (DefCarpentry.cs:231, see the report); this shard has no recipe for it yet.
//
// The nested StableEntry, ClaimAllEntry, ClaimListGump and StableTarget are private to ChickenCoop, as in ServUO, so
// they do not collide with pinned AnimalTrainer's nested types of the same names.
//
// Differences from ServUO:
//   - Serialization is hand-written (Serialize/Deserialize), not the generator: the coop's state is a
//     Dictionary<Mobile, List<BaseCreature>>, a nested collection (the HammerOfHephaestus.cs precedent for a
//     dictionary). The layout is ServUO's version 2 (level, owner count, then per owner the mobile, a count and the
//     creatures); version 1's early return and the "Weight == 1" migration are dropped, there being no older saves,
//     and the weight is DefaultWeight => 20.0. As in ServUO, every chicken read back is marked IsStabled (pinned does
//     not save IsStabled, BaseCreature.cs:785, and stops the pending-delete timer when it is set, :897-907).
//   - IChopable is pinned's IChoppable (Engines/Harvest/Core/IChoppable..cs).
//   - The Allured check (ChickenCoop.cs:316-320, "You can't stable that!") is dropped: BaseCreature.Allured is
//     ServUO's (Mondain's Legacy allure), pinned and this shard have no such member or system.
//   - ServUO's claim loops do `pet.IsStabled = false` on a pet they have just found null (:228-233, :366-371), a null
//     dereference; here the assignment is skipped for a null pet, as pinned AnimalTrainer.cs:300-306 does.
//   - The two gumps and entries take pinned's shapes: ClaimListGump is a DynamicGump with ServUO's layout, and the
//     entries use OnClick(Mobile from, IEntity target).

using System;
using System.Collections.Generic;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

[Flippable(0x4513, 0x4514)]
public class ChickenCoop : Item, ISecurable, IChoppable
{
    public static readonly int MaxStables = 3;

    private readonly Dictionary<Mobile, List<BaseCreature>> _stored = new();

    [Constructible]
    public ChickenCoop() : base(0x4513) => Level = SecureLevel.CoOwners;

    public ChickenCoop(Serial serial) : base(serial)
    {
    }

    public override int LabelNumber => 1112570; // a chicken coop

    public override double DefaultWeight => 20.0;

    [CommandProperty(AccessLevel.GameMaster)]
    public SecureLevel Level { get; set; }

    public Dictionary<Mobile, List<BaseCreature>> Stored => _stored;

    public override bool ForceShowProperties => ObjectPropertyList.Enabled;

    public override bool HandlesOnSpeech => true;

    public void OnChop(Mobile from)
    {
        if (CheckAccess(from))
        {
            Effects.PlaySound(GetWorldLocation(), Map, 0x3B3);
            from.SendLocalizedMessage(500461); // You destroy the item.

            Delete();
        }
    }

    public override void Delete()
    {
        if (_stored.Count > 0)
        {
            var masterList = new List<List<BaseCreature>>(_stored.Values);

            for (var i = 0; i < masterList.Count; i++)
            {
                for (var j = 0; j < masterList[i].Count; j++)
                {
                    if (masterList[i][j]?.Deleted == false)
                    {
                        masterList[i][j].Delete();
                    }
                }
            }

            _stored.Clear();
        }

        base.Delete();
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (CheckAccess(from))
        {
            SetSecureLevelEntry.AddTo(from, this, ref list);

            list.Add(new StableEntry());

            if (_stored.TryGetValue(from, out var pets) && pets.Count > 0)
            {
                list.Add(new ClaimAllEntry());
            }
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        AddNameProperties(list);
    }

    public static int GetMaxStabled(Mobile from) => 3;

    public void BeginClaimList(Mobile from)
    {
        if (Deleted || !from.CheckAlive() || !_stored.TryGetValue(from, out var stabled) || stabled == null)
        {
            return;
        }

        for (var i = 0; i < stabled.Count; i++)
        {
            var pet = stabled[i];

            if (pet?.Deleted != false)
            {
                if (pet != null)
                {
                    pet.IsStabled = false;
                }

                stabled.RemoveAt(i);
                --i;
            }
        }

        if (stabled.Count > 0)
        {
            from.SendGump(new ClaimListGump(this, from, stabled));
        }
        else
        {
            from.SendLocalizedMessage(502671); // But I have no animals stabled with me at the moment!
        }
    }

    public void EndClaimList(Mobile from, BaseCreature pet)
    {
        if (Deleted || !from.CheckAlive() || !_stored.TryGetValue(from, out var stabled))
        {
            return;
        }

        if (from.Followers + pet.ControlSlots <= from.FollowersMax)
        {
            pet.SetControlMaster(from);

            if (pet.Summoned)
            {
                pet.SummonMaster = from;
            }

            pet.ControlTarget = from;
            pet.ControlOrder = OrderType.Follow;

            pet.MoveToWorld(from.Location, from.Map);

            pet.IsStabled = false;

            stabled.Remove(pet);

            from.SendLocalizedMessage(1042559); // Here you go... and good day to you!
        }
        else
        {
            from.SendLocalizedMessage(1049612, pet.Name); // ~1_NAME~ remained in the stables because you have too many followers.
        }
    }

    public void BeginStable(Mobile from)
    {
        if (Deleted || !from.CheckAlive() || !CanUse() || !CheckAccess(from))
        {
            return;
        }

        if (GetCount() >= MaxStables)
        {
            from.SendLocalizedMessage(1114325); // There is no more room in your chicken coop!
        }
        else
        {
            from.Target = new StableTarget(this);
            from.SendLocalizedMessage(1112559); // Which chicken do you wish to stable?
        }
    }

    private int GetCount()
    {
        var count = 0;

        foreach (var bcList in _stored.Values)
        {
            if (bcList != null)
            {
                count += bcList.Count;
            }
        }

        return count;
    }

    public void EndStable(Mobile from, BaseCreature pet)
    {
        if (Deleted || !from.CheckAlive() || !CanUse() || !CheckAccess(from))
        {
            return;
        }

        if (!pet.Controlled || pet.ControlMaster != from)
        {
            from.SendLocalizedMessage(1042562); // You do not own that pet!
        }
        else if (pet.IsDeadPet)
        {
            from.SendLocalizedMessage(1049668); // Living pets only, please.
        }
        else if (pet.Summoned)
        {
            from.SendLocalizedMessage(502673); // I can not stable summoned creatures.
        }
        else if (pet.Body.IsHuman)
        {
            from.SendLocalizedMessage(502672); // HA HA HA! Sorry, I am not an inn.
        }
        else if (pet.Combatant != null && pet.InRange(pet.Combatant, 12) && pet.Map == pet.Combatant.Map)
        {
            from.SendLocalizedMessage(1042564); // I'm sorry.  Your pet seems to be busy.
        }
        else if (GetCount() >= MaxStables)
        {
            from.SendLocalizedMessage(1114325); // There is no more room in your chicken coop!
        }
        else
        {
            pet.ControlTarget = null;
            pet.ControlOrder = OrderType.Stay;
            pet.Internalize();

            pet.SetControlMaster(null);
            pet.SummonMaster = null;

            pet.IsStabled = true;

            if (Core.SE)
            {
                pet.Loyalty = BaseCreature.MaxLoyalty; // Wonderfully happy
            }

            if (!_stored.TryGetValue(from, out var stabled))
            {
                _stored[from] = stabled = new List<BaseCreature>();
            }

            if (!stabled.Contains(pet))
            {
                stabled.Add(pet);
            }

            from.SendMessage("Your chicken has been stabled.");
        }
    }

    public void Claim(Mobile from)
    {
        if (Deleted || !from.CheckAlive() || !_stored.TryGetValue(from, out var stabled))
        {
            return;
        }

        var claimed = false;
        var stabledCount = 0;

        for (var i = 0; i < stabled.Count; ++i)
        {
            var pet = stabled[i];

            if (pet?.Deleted != false)
            {
                if (pet != null)
                {
                    pet.IsStabled = false;
                }

                stabled.RemoveAt(i);
                --i;
                continue;
            }

            ++stabledCount;

            if (from.Followers + pet.ControlSlots <= from.FollowersMax)
            {
                pet.SetControlMaster(from);

                if (pet.Summoned)
                {
                    pet.SummonMaster = from;
                }

                pet.ControlTarget = from;
                pet.ControlOrder = OrderType.Follow;

                pet.MoveToWorld(from.Location, from.Map);

                pet.IsStabled = false;

                if (Core.SE)
                {
                    pet.Loyalty = BaseCreature.MaxLoyalty; // Wonderfully Happy
                }

                stabled.RemoveAt(i);
                --i;

                claimed = true;
            }
            else
            {
                from.SendLocalizedMessage(1049612, pet.Name); // ~1_NAME~ remained in the stables because you have too many followers.
            }
        }

        if (claimed)
        {
            from.SendLocalizedMessage(1042559); // Here you go... and good day to you!
        }
        else if (stabledCount == 0)
        {
            from.SendLocalizedMessage(502671); // But I have no animals stabled with me at the moment!
        }
    }

    public bool CheckAccess(Mobile m)
    {
        var h = BaseHouse.FindHouseAt(this);

        return h?.HasSecureAccess(m, Level) == true;
    }

    public bool CanUse() => IsLockedDown;

    public override void OnSpeech(SpeechEventArgs e)
    {
        if (CheckAccess(e.Mobile) && IsLockedDown)
        {
            if (!e.Handled && e.HasKeyword(0x0008))
            {
                e.Handled = true;
                BeginStable(e.Mobile);
            }
            else if (!e.Handled && e.HasKeyword(0x0009))
            {
                e.Handled = true;

                if (!e.Speech.InsensitiveEquals("claim"))
                {
                    BeginClaimList(e.Mobile);
                }
                else
                {
                    Claim(e.Mobile);
                }
            }
            else
            {
                base.OnSpeech(e);
            }
        }
    }

    public override void Serialize(IGenericWriter writer)
    {
        base.Serialize(writer);

        writer.Write(2); // version, ServUO's

        writer.Write((int)Level);
        writer.Write(_stored.Count);

        foreach (var (owner, pets) in _stored)
        {
            writer.Write(owner);
            writer.Write(pets.Count);

            foreach (var bc in pets)
            {
                writer.Write(bc);
            }
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        base.Deserialize(reader);

        reader.ReadInt(); // version

        Level = (SecureLevel)reader.ReadInt();

        var c = reader.ReadInt();

        for (var i = 0; i < c; i++)
        {
            var owner = reader.ReadEntity<Mobile>();
            var count = reader.ReadInt();
            var list = new List<BaseCreature>();

            for (var j = 0; j < count; j++)
            {
                if (reader.ReadEntity<Mobile>() is BaseCreature bc)
                {
                    bc.IsStabled = true;
                    list.Add(bc);
                }
            }

            if (owner != null && list.Count > 0)
            {
                _stored.Add(owner, list);
            }
        }
    }

    private class StableEntry : ContextMenuEntry
    {
        public StableEntry() : base(6126, 12)
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            (target as ChickenCoop)?.BeginStable(from);
        }
    }

    private class ClaimAllEntry : ContextMenuEntry
    {
        public ClaimAllEntry() : base(6127, 12)
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            (target as ChickenCoop)?.Claim(from);
        }
    }

    private class ClaimListGump : DynamicGump
    {
        private readonly ChickenCoop _post;
        private readonly Mobile _from;
        private readonly List<BaseCreature> _list;

        public ClaimListGump(ChickenCoop post, Mobile from, List<BaseCreature> list) : base(50, 50)
        {
            _post = post;
            _from = from;
            _list = list;

            from.CloseGump<ClaimListGump>();
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();

            builder.AddBackground(0, 0, 325, 50 + _list.Count * 20, 9250);
            builder.AddAlphaRegion(5, 5, 315, 40 + _list.Count * 20);

            builder.AddHtml(15, 15, 275, 20, "<BASEFONT COLOR=#FFFFFF>Select a pet to retrieve from the stables:</BASEFONT>");

            for (var i = 0; i < _list.Count; ++i)
            {
                var pet = _list[i];

                if (pet?.Deleted != false)
                {
                    continue;
                }

                builder.AddButton(15, 39 + i * 20, 10006, 10006, i + 1);
                builder.AddHtml(32, 35 + i * 20, 275, 18, $"<BASEFONT COLOR=#C0C0EE>{pet.Name}</BASEFONT>");
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var index = info.ButtonID - 1;

            if (index >= 0 && index < _list.Count)
            {
                _post.EndClaimList(_from, _list[index]);
            }
        }
    }

    private class StableTarget : Target
    {
        private readonly ChickenCoop _post;

        public StableTarget(ChickenCoop post) : base(12, false, TargetFlags.None) => _post = post;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is ChickenLizard or Chicken or BattleChickenLizard)
            {
                _post.EndStable(from, (BaseCreature)targeted);
            }
            else if (targeted == from)
            {
                from.SendLocalizedMessage(502672); // HA HA HA! Sorry, I am not an inn.
            }
            else
            {
                from.SendLocalizedMessage(1112558); // You may only stable chickens in the chicken coop.
            }
        }
    }
}
