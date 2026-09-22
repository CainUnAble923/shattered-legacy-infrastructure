// ServUO: Mobiles/Normal/BaseSABosses.cs (CC6 batch 8; Q-052 answered yes). The abstract base of the four Stygian
// Abyss bosses: Medusa, the Slasher of Veils, the Stygian Dragon and Niporailem. On death it tallies the damage each
// PLAYER did (pets credited to their master), rolls one SA artifact (5% from the child's unique list, a further 10%
// from its shared list) and hands it to one damager chosen in proportion to damage done - the same code our
// BaseRenowned (batch 5) carries, which is where the translations below were first built and tested.
//
// Final parent: BasePeerless (batch 8's altar-less port, D-79). ServUO's [TypeAlias("Server.Mobiles.BaseSABosses")]
// is that tree's own earlier spelling of this abstract type; no save of ours has ever carried either name and an
// abstract type is never a saved type name, so the alias is not reproduced.
//
// ServUO quirks kept, bug-list section 3's kind: AwardArtifact walks the FULL damage table after sizing the roll on
// the eligible subset; and OnDeath builds a "toGive" list on Felucca and Ter Mur and then does nothing with it.
// ServUO's constructor is base(ai, mode, 10, 1, active, passive): 10 is rewritten to 16 there and the speeds are
// dead (Q-008), so this mirrors pinned BaseCreature's signature.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public abstract partial class BaseSABoss : BasePeerless
{
    // ServUO leaves this null until OnBeforeDeath creates it. Created here as well so RegisterDamage and
    // AwardArtifact can be driven directly by a test; OnBeforeDeath still replaces it, exactly as ServUO does.
    private Dictionary<Mobile, int> _damageEntries = new();

    public BaseSABoss(
        AIType aiType,
        FightMode fightMode = FightMode.Closest,
        int rangePerception = DefaultRangePerception,
        int rangeFight = 1
    ) : base(aiType, fightMode, rangePerception, rangeFight)
    {
    }

    public override bool GiveMLSpecial => false;

    public abstract Type[] UniqueSAList { get; }
    public abstract Type[] SharedSAList { get; }

    public virtual bool NoGoodies => false;

    public override bool DropPrimer => false;

    public virtual void RegisterDamageTo(Mobile m)
    {
        if (m == null)
        {
            return;
        }

        foreach (var de in m.DamageEntries)
        {
            var damager = de.Damager;

            var master = damager.GetDamageMaster(m);

            if (master != null)
            {
                damager = master;
            }

            RegisterDamage(damager, de.DamageGiven);
        }
    }

    public void RegisterDamage(Mobile from, int amount)
    {
        if (from == null || !from.Player)
        {
            return;
        }

        if (_damageEntries.ContainsKey(from))
        {
            _damageEntries[from] += amount;
        }
        else
        {
            _damageEntries.Add(from, amount);
        }
    }

    public void AwardArtifact(Item artifact)
    {
        if (artifact == null)
        {
            return;
        }

        var totalDamage = 0;

        var validEntries = new Dictionary<Mobile, int>();

        foreach (var kvp in _damageEntries)
        {
            if (IsEligible(kvp.Key, artifact))
            {
                validEntries.Add(kvp.Key, kvp.Value);
                totalDamage += kvp.Value;
            }
        }

        var randomDamage = Utility.RandomMinMax(1, totalDamage);

        totalDamage = 0;

        // ServUO walks the FULL table here, not validEntries. Copied.
        foreach (var kvp in _damageEntries)
        {
            totalDamage += kvp.Value;

            if (totalDamage > randomDamage)
            {
                GiveArtifact(kvp.Key, artifact);
                break;
            }
        }
    }

    public void GiveArtifact(Mobile to, Item artifact)
    {
        if (to == null || artifact == null)
        {
            return;
        }

        to.PlaySound(0x5B4);

        var pack = to.Backpack;

        if (pack == null || !pack.TryDropItem(to, artifact, false))
        {
            artifact.Delete();
        }
        else
        {
            // For your valor in combating the fallen beast, a special artifact has been bestowed on you.
            to.SendLocalizedMessage(1062317);
        }
    }

    public bool IsEligible(Mobile m, Item artifact) =>
        m.Player && m.Alive && m.InRange(Location, 32) && m.Backpack != null && m.Backpack.CheckHold(m, artifact, false);

    public Item GetArtifact()
    {
        var random = Utility.RandomDouble();

        if (0.05 >= random)
        {
            return CreateArtifact(UniqueSAList);
        }

        if (0.15 >= random)
        {
            return CreateArtifact(SharedSAList);
        }

        return null;
    }

    public Item CreateArtifact(Type[] list)
    {
        if (list.Length == 0)
        {
            return null;
        }

        var type = list[Utility.Random(list.Length)];

        return Loot.Construct(type);
    }

    public override bool OnBeforeDeath()
    {
        if (!NoKillAwards)
        {
            _damageEntries = new Dictionary<Mobile, int>();

            RegisterDamageTo(this);
            AwardArtifact(GetArtifact());
        }

        return base.OnBeforeDeath();
    }

    public override void OnDeath(Container c)
    {
        if (Map == Map.Felucca || Map == Map.TerMur)
        {
            // ServUO: "TODO: Confirm SE change or AoS one too?" - the list is built and never used. Copied.
            var rights = GetLootingRights(DamageEntries, HitsMax);
            var toGive = new List<Mobile>();

            for (var i = rights.Count - 1; i >= 0; --i)
            {
                var ds = rights[i];

                if (ds.m_HasRight)
                {
                    toGive.Add(ds.m_Mobile);
                }
            }
        }

        base.OnDeath(c);
    }
}
