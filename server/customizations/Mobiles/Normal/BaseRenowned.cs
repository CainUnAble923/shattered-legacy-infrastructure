// ServUO: Mobiles/Normal/BaseRenowned.cs (CC6 batch 5). The base of the thirteen Stygian Abyss "[Renowned]" creatures:
// on death it tallies the damage each PLAYER did (pets and provoked creatures credited to their master), rolls one
// Stygian Abyss artifact (5% from the child's unique list, a further 10% from its shared list) and hands it to one
// damager chosen in proportion to damage done. Ported whole; every member below is ServUO's, including its quirks
// (notes/cc6-creatures-batch5.md section 5). Serialization by the generator: ServUO writes a version and nothing else.
//
// Final parent: BaseCreature. The B5 carrier regex was run over this file and all thirteen ServUO descendants and
// hit nothing (they are creatures); the descendant walk is in the note.
//
// ServUO's constructor is base(aiType, mode, 18, 1, 0.1, 0.2). 18 is one of the rare perception values ServUO does
// NOT rewrite to 16 (notes/port-recipe.md, "Range perception"), so it is carried; the two speed literals are dead in
// ServUO and every renowned lands on ModernUO's Medium (Q-008).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public abstract partial class BaseRenowned : BaseCreature
{
    // ServUO leaves this null until OnBeforeDeath creates it. It is created here as well so RegisterDamage and
    // AwardArtifact can be driven directly by a test; OnBeforeDeath still replaces it, exactly as ServUO does.
    private Dictionary<Mobile, int> _damageEntries = new();

    public BaseRenowned(AIType aiType) : this(aiType, FightMode.Closest)
    {
    }

    public BaseRenowned(AIType aiType, FightMode mode) : base(aiType, mode, 18)
    {
    }

    public abstract Type[] UniqueSAList { get; }
    public abstract Type[] SharedSAList { get; }

    public virtual bool NoGoodies => false;

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

        // ServUO walks the FULL table here, not validEntries: the eligibility check sizes the roll and nothing else.
        // Copied (note section 5).
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
            if (NoGoodies)
            {
                return base.OnBeforeDeath();
            }

            _damageEntries = new Dictionary<Mobile, int>();

            RegisterDamageTo(this);
            AwardArtifact(GetArtifact());
        }

        return base.OnBeforeDeath();
    }
}
