// ServUO: Mobiles/Bosses/LadyMelisande.cs (cc-P129). The Blighted Grove peerless, on our BasePeerless (D-79,
// altar-less). Values verbatim; serialization by the generator; the two cooldowns are not persisted in ServUO either.
//
// AI_NecroMage is AI_Mage (Q-050). SetAreaEffect(AuraOfNausea) goes to the profile. SpawnHelpers takes ServUO's own
// altar-less arm (four satyrs around her; the altar's per-fighter arm is D-79's). AddLoot(LootPack.Parrot, 1) is
// ParrotItem.LootPack, the pack pinned keeps in a comment. The Arcanist scroll uses ArcanistScrollTypes.Length - 1
// (ColdDrake's reason). The bark, the tree, the wine, the hair dye and the hatchet are pinned's; the ParrotItem and
// the albino squirrel's crystal are ported beside this file. Reaper, InsaneDryad, StoneHarpy and EnslavedSatyr are
// pinned's.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class LadyMelisande : BasePeerless
{
    [Constructible]
    public LadyMelisande() : base(AIType.AI_Mage)
    {
        Body = 0x102;
        BaseSoundID = 451;

        SetStr(400, 1000);
        SetDex(300, 400);
        SetInt(1500, 1700);

        SetHits(100000);

        SetDamage(11, 18);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Energy, 50);

        SetResistance(ResistanceType.Physical, 40, 60);
        SetResistance(ResistanceType.Fire, 40, 50);
        SetResistance(ResistanceType.Cold, 55, 65);
        SetResistance(ResistanceType.Poison, 70, 75);
        SetResistance(ResistanceType.Energy, 70, 80);

        SetSkill(SkillName.Wrestling, 100, 105);
        SetSkill(SkillName.Tactics, 100, 105);
        SetSkill(SkillName.MagicResist, 120);
        SetSkill(SkillName.Magery, 120);
        SetSkill(SkillName.EvalInt, 120);
        SetSkill(SkillName.Meditation, 120);
        SetSkill(SkillName.Necromancy, 120);
        SetSkill(SkillName.SpiritSpeak, 120);

        PackResources(8);
        PackTalismans(5);

        Timer.DelayCall(TimeSpan.FromSeconds(1), SpawnSatyrs);

        Fame = 25000;
        Karma = -25000;

        VirtualArmor = 50;

        for (var i = 0; i < Utility.RandomMinMax(0, 1); i++)
        {
            PackItem(Loot.RandomScroll(0, Loot.ArcanistScrollTypes.Length - 1, SpellbookType.Arcanist));
        }

        SetAreaEffect(AreaEffect.AuraOfNausea);
    }

    public override string CorpseName => "a lady melisande corpse";
    public override string DefaultName => "a lady melisande";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.SuperBoss, 8);
        AddLoot(ParrotItem.LootPack, 1);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        c.DropItem(new DiseasedBark());
        c.DropItem(new EternallyCorruptTree());

        var drop = Utility.Random(4, 8);

        for (var i = 0; i < drop; i++)
        {
            c.DropItem(new MelisandesFermentedWine());
        }

        if (Utility.RandomDouble() < 0.6)
        {
            c.DropItem(new ParrotItem());
        }

        if (Utility.RandomDouble() < 0.2225)
        {
            switch (Utility.Random(3))
            {
                case 0:
                    c.DropItem(new MelisandesHairDye());
                    break;
                case 1:
                    c.DropItem(new MelisandesCorrodedHatchet());
                    break;
                case 2:
                    c.DropItem(new AlbinoSquirrelImprisonedInCrystal());
                    break;
            }
        }
    }

    public override void OnThink()
    {
        base.OnThink();

        var combatant = Combatant;

        if (combatant != null)
        {
            if (CanTakeLife(combatant))
            {
                TakeLife(combatant);
            }

            if (CanSmackTalk())
            {
                SmackTalk();
            }
        }
    }

    public override void SetLocation(Point3D newLocation, bool isTeleport)
    {
        if (newLocation.Z > -10)
        {
            base.SetLocation(newLocation, isTeleport);
        }
    }

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        if (willKill)
        {
            SpawnHelper(new Reaper(), 6490, 948, 19);
            SpawnHelper(new InsaneDryad(), 6497, 946, 17);
            SpawnHelper(new StoneHarpy(), 6511, 946, 28);

            // Noooooo!  You shall never defeat me.  Even if I should fall, my tree will sustain me and I will rise again.
            Say(1075118);
        }

        base.OnDamage(amount, from, willKill);
    }

    public override bool GivesMLMinorArtifact => true;
    public override Poison PoisonImmune => Poison.Lethal;
    public override int TreasureMapLevel => 5;

    #region Smack Talk
    private DateTime _nextSmackTalk;

    public bool CanSmackTalk()
    {
        if (_nextSmackTalk > Core.Now)
        {
            return false;
        }

        if (Combatant == null)
        {
            return false;
        }

        return Hits > 0.5 * HitsMax;
    }

    public void SmackTalk()
    {
        Say(Utility.RandomMinMax(1075102, 1075115)); // Muahahahaha!  I'll feast on your flesh.

        _nextSmackTalk = Core.Now + TimeSpan.FromSeconds(2 + Utility.RandomDouble() * 3);
    }
    #endregion

    #region Take Life
    private DateTime _nextTakeLife;

    public bool CanTakeLife(Mobile from)
    {
        if (_nextTakeLife > Core.Now)
        {
            return false;
        }

        if (!CanBeHarmful(from))
        {
            return false;
        }

        if (Hits > 0.1 * HitsMax || Hits < 0.025 * HitsMax)
        {
            return false;
        }

        return true;
    }

    public void TakeLife(Mobile from)
    {
        Hits += from.Hits / (from.Player ? 2 : 6);

        FixedParticles(0x376A, 9, 32, 5005, EffectLayer.Waist);
        PlaySound(0x1F2);

        Say(1075117); // Muahahaha!  Your life essence is MINE!
        Say(1075120); // An unholy aura surrounds Lady Melisande as her wounds begin to close.

        _nextTakeLife = Core.Now + TimeSpan.FromSeconds(15 + Utility.RandomDouble() * 45);
    }
    #endregion

    #region Helpers
    public override bool CanSpawnHelpers => true;
    public override int MaxHelpersWaves => 1;

    public override void SpawnHelpers()
    {
        // ServUO: the altar's fighters each get a satyr; without an altar (D-79), ServUO's own else-arm: four satyrs.
        const int count = 4;

        for (var i = 0; i < count; i++)
        {
            SpawnHelper(new EnslavedSatyr(), 4);
        }
    }

    public void SpawnSatyrs()
    {
        SpawnHelper(new EnslavedSatyr(), 6485, 945, 19);
        SpawnHelper(new EnslavedSatyr(), 6486, 948, 22);
        SpawnHelper(new EnslavedSatyr(), 6487, 945, 17);
        SpawnHelper(new EnslavedSatyr(), 6488, 947, 23);
    }
    #endregion
}
