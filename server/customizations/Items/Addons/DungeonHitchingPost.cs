// ServUO: Items/Addons/DungeonHitchingPost.cs (cc-P196, a leaf dependency of the Blackthorn Dungeon: [GenBlackthorn
// places one at the dungeon's hitching area, 6428,2677, on both facets, because pets cannot enter the Trammel castle).
//
// A hitching post that stables and returns pets like an animal trainer: a Stable context entry (6126, 30 gold from
// the pack or the bank, a week's board) with the trainer's refusals, a Claim All entry (6127) and the "stable" and
// "claim" keywords (claim alone returns every pet, anything else with the keyword lists them), each pet returned
// only while the follower slots allow.
//
// Conversion notes: a player's stabled pets are PlayerMobile.Stabled here (a HashSet made on the first stabling,
// AddStabled / RemoveStabled; ServUO keeps the list on Mobile), so a non-player gets the trainer's refusal; Allured has no pinned member (an allured pet is a Mondain's
// Legacy charm, not on this shard) and that refusal is dropped; Insensitive.Equals is the InsensitiveEquals
// extension; the gump is pinned's legacy Gump, OnResponse takes an "in RelayInfo"; context entries take
// OnClick(from, target). The property lines are the name only (AddNameProperties), as ServUO's GetProperties does.

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

[Flippable(0x14E7, 0x14E8)]
[SerializationGenerator(0, false)]
public partial class DungeonHitchingPost : Item
{
    public const int StableFee = 30;

    [Constructible]
    public DungeonHitchingPost() : base(0x14E7) => Movable = false;

    public override int LabelNumber => 1025351; // hitching post

    public override bool ForceShowProperties => ObjectPropertyList.Enabled;

    public override bool HandlesOnSpeech => true;

    public override void GetProperties(IPropertyList list) => AddNameProperties(list);

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from.Alive)
        {
            list.Add(new StableEntry(this, from));

            if (from is PlayerMobile pm && pm.Stabled?.Count > 0)
            {
                list.Add(new ClaimAllEntry(this, from));
            }
        }
    }

    public void BeginStable(Mobile from)
    {
        if (Deleted || !from.CheckAlive())
        {
            return;
        }

        if ((from.Backpack == null || from.Backpack.GetAmount(typeof(Gold)) < StableFee) && Banker.GetBalance(from) < StableFee)
        {
            from.SendLocalizedMessage(502677); // But thou hast not the funds in thy bank account!
        }
        else
        {
            // I charge 30 gold per pet for a real week's stable time. I will withdraw it from thy bank account.
            // Which animal wouldst thou like to stable here?
            from.SendLocalizedMessage(1042558);

            from.Target = new StableTarget(this);
        }
    }

    public void EndStable(Mobile from, BaseCreature pet)
    {
        if (Deleted || !from.CheckAlive())
        {
            return;
        }

        if (pet.Body.IsHuman)
        {
            from.SendLocalizedMessage(502672); // HA HA HA! Sorry, I am not an inn.
        }
        else if (!pet.Controlled)
        {
            from.SendLocalizedMessage(1048053); // You can't stable that!
        }
        else if (pet.ControlMaster != from)
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
        else if (pet is PackLlama or PackHorse or Beetle && pet.Backpack?.Items.Count > 0)
        {
            from.SendLocalizedMessage(1042563); // You need to unload your pet.
        }
        else if (pet.Combatant != null && pet.InRange(pet.Combatant, 12) && pet.Map == pet.Combatant.Map)
        {
            from.SendLocalizedMessage(1042564); // I'm sorry.  Your pet seems to be busy.
        }
        else if (from is not PlayerMobile pm || (pm.Stabled?.Count ?? 0) >= AnimalTrainer.GetMaxStabled(from))
        {
            from.SendLocalizedMessage(1042565); // You have too many pets in the stables!
        }
        else if (from.Backpack?.ConsumeTotal(typeof(Gold), StableFee) == true || Banker.Withdraw(from, StableFee))
        {
            pet.ControlTarget = null;
            pet.ControlOrder = OrderType.Stay;
            pet.Internalize();

            pet.SetControlMaster(null);
            pet.SummonMaster = null;

            pet.IsStabled = true;
            pet.StabledBy = from;

            if (Core.SE)
            {
                pet.Loyalty = BaseCreature.MaxLoyalty; // Wonderfully happy
            }

            pm.AddStabled(pet);

            // [AOS: Your pet has been stabled.] Very well, thy pet is stabled. Thou mayst recover it by saying
            // 'claim' to me. In one real world week, I shall sell it off if it is not claimed!
            from.SendLocalizedMessage(Core.AOS ? 1049677 : 502679);
        }
        else
        {
            from.SendLocalizedMessage(502677); // But thou hast not the funds in thy bank account!
        }
    }

    public void Claim(Mobile from)
    {
        if (Deleted || !from.CheckAlive() || from is not PlayerMobile pm)
        {
            return;
        }

        var claimed = false;
        var stabled = 0;

        foreach (var m in StabledList(pm))
        {
            if (m is not BaseCreature pet || pet.Deleted)
            {
                if (m is BaseCreature dead)
                {
                    dead.IsStabled = false;
                }

                pm.RemoveStabled(m);
                continue;
            }

            ++stabled;

            if (from.Followers + pet.ControlSlots <= from.FollowersMax)
            {
                ReturnPet(pm, pet);
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
        else if (stabled == 0)
        {
            from.SendLocalizedMessage(502671); // But I have no animals stabled with me at the moment!
        }
    }

    public void BeginClaimList(Mobile from)
    {
        if (Deleted || !from.CheckAlive() || from is not PlayerMobile pm)
        {
            return;
        }

        var list = new List<BaseCreature>();

        foreach (var m in StabledList(pm))
        {
            if (m is not BaseCreature pet || pet.Deleted)
            {
                if (m is BaseCreature dead)
                {
                    dead.IsStabled = false;
                }

                pm.RemoveStabled(m);
                continue;
            }

            list.Add(pet);
        }

        if (list.Count > 0)
        {
            from.SendGump(new ClaimListGump(this, from, list));
        }
        else
        {
            from.SendLocalizedMessage(502671); // But I have no animals stabled with me at the moment!
        }
    }

    public void EndClaimList(Mobile from, BaseCreature pet)
    {
        if (pet?.Deleted != false || from.Map != Map || !from.InRange(this, 14) || from is not PlayerMobile pm ||
            pm.Stabled?.Contains(pet) != true || !from.CheckAlive())
        {
            return;
        }

        if (from.Followers + pet.ControlSlots <= from.FollowersMax)
        {
            ReturnPet(pm, pet);
            from.SendLocalizedMessage(1042559); // Here you go... and good day to you!
        }
        else
        {
            from.SendLocalizedMessage(1049612, pet.Name); // ~1_NAME~ remained in the stables because you have too many followers.
        }
    }

    private static void ReturnPet(PlayerMobile pm, BaseCreature pet)
    {
        pet.SetControlMaster(pm);

        if (pet.Summoned)
        {
            pet.SummonMaster = pm;
        }

        pet.ControlTarget = pm;
        pet.ControlOrder = OrderType.Follow;

        pet.MoveToWorld(pm.Location, pm.Map);

        pet.IsStabled = false;

        if (Core.SE)
        {
            pet.Loyalty = BaseCreature.MaxLoyalty; // Wonderfully Happy
        }

        pm.RemoveStabled(pet);
    }

    // A player's stabled set is created on the first stabling (PlayerMobile.AddStabled), so it may be null.
    private static List<Mobile> StabledList(PlayerMobile pm) =>
        pm.Stabled != null ? new List<Mobile>(pm.Stabled) : new List<Mobile>();

    public override void OnSpeech(SpeechEventArgs e)
    {
        if (!e.Handled && e.HasKeyword(0x0008)) // *stable*
        {
            e.Handled = true;
            BeginStable(e.Mobile);
        }
        else if (!e.Handled && e.HasKeyword(0x0009)) // *claim*
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

    private class StableEntry : ContextMenuEntry
    {
        private readonly Mobile _from;
        private readonly DungeonHitchingPost _post;

        public StableEntry(DungeonHitchingPost post, Mobile from) : base(6126, 12) // Stable
        {
            _post = post;
            _from = from;
        }

        public override void OnClick(Mobile from, IEntity target) => _post.BeginStable(_from);
    }

    private class ClaimAllEntry : ContextMenuEntry
    {
        private readonly Mobile _from;
        private readonly DungeonHitchingPost _post;

        public ClaimAllEntry(DungeonHitchingPost post, Mobile from) : base(6127, 12) // Claim All
        {
            _post = post;
            _from = from;
        }

        public override void OnClick(Mobile from, IEntity target) => _post.Claim(_from);
    }

    private class StableTarget : Target
    {
        private readonly DungeonHitchingPost _post;

        public StableTarget(DungeonHitchingPost post) : base(12, false, TargetFlags.None) => _post = post;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is BaseCreature pet)
            {
                _post.EndStable(from, pet);
            }
            else if (targeted == from)
            {
                from.SendLocalizedMessage(502672); // HA HA HA! Sorry, I am not an inn.
            }
            else
            {
                from.SendLocalizedMessage(1048053); // You can't stable that!
            }
        }
    }

    private class ClaimListGump : Gump
    {
        private readonly Mobile _from;
        private readonly List<BaseCreature> _list;
        private readonly DungeonHitchingPost _post;

        public ClaimListGump(DungeonHitchingPost post, Mobile from, List<BaseCreature> list) : base(50, 50)
        {
            _post = post;
            _from = from;
            _list = list;

            from.CloseGump<ClaimListGump>();

            AddPage(0);

            AddBackground(0, 0, 325, 50 + list.Count * 20, 9250);
            AddAlphaRegion(5, 5, 315, 40 + list.Count * 20);

            AddHtml(15, 15, 275, 20, "<BASEFONT COLOR=#FFFFFF>Select a pet to retrieve from the stables:</BASEFONT>");

            for (var i = 0; i < list.Count; ++i)
            {
                var pet = list[i];

                if (pet?.Deleted != false)
                {
                    continue;
                }

                AddButton(15, 39 + i * 20, 10006, 10006, i + 1);
                AddHtml(32, 35 + i * 20, 275, 18, $"<BASEFONT COLOR=#C0C0EE>{pet.Name}</BASEFONT>");
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
}
