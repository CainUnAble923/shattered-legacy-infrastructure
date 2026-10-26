// cc-P630 (P567 design, stage 1): the Echo of the Stygian Dragon, a brief, weak helper a Mythril League citizen calls
// (LeagueEchoSystem). A direct BaseCreature, not a StygianDragon child, so none of the boss's attacks, resources, trophy or
// level class come with the body. Inspired by TwilightRaven's summoning-system-v2 idea (P405 notes); no code copied.
//
// Never a reward: Fame and Karma 0, NoKillAwards, no loot, no resources, an empty pack. Lethal damage or Release dissolves
// it (OnBeforeDeath deletes it and returns false), so there is no corpse and no death event. A saved Echo is deleted after
// the world loads; it never resumes. Its harm checks go through LeagueEchoPolicy: wild creatures only.

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Engines.LeagueEchoes;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class LeagueDragonEcho : BaseCreature
{
    public const int EchoBody = 826; // the Stygian Dragon's (pinned bodyTable.cfg types it Monster)
    public const int EchoHue = Server.Engines.LeagueRewards.LeagueShop.LeagueHue;
    public const int SummonSound = 0x216;

    [Constructible(AccessLevel.Administrator)]
    public LeagueDragonEcho() : base(AIType.AI_Melee, FightMode.None)
    {
        Body = EchoBody;
        Hue = EchoHue;
        BaseSoundID = 362;

        SetStr(200);
        SetDex(100);
        SetInt(50);
        SetHits(300);
        SetStam(100);
        SetMana(0);

        SetDamage(12, 16);
        SetDamageType(ResistanceType.Physical, 75);
        SetDamageType(ResistanceType.Fire, 25);

        SetResistance(ResistanceType.Physical, 35);
        SetResistance(ResistanceType.Fire, 35);
        SetResistance(ResistanceType.Cold, 35);
        SetResistance(ResistanceType.Poison, 35);
        SetResistance(ResistanceType.Energy, 35);

        SetSkill(SkillName.Wrestling, 80.0);
        SetSkill(SkillName.Tactics, 80.0);
        SetSkill(SkillName.MagicResist, 80.0);

        for (var i = 0; i < Skills.Length; i++)
        {
            Skills[i].SetLockNoRelay(SkillLock.Locked);
        }

        Fame = 0;
        Karma = 0;
        VirtualArmor = 0;
        ControlSlots = LeagueEchoSettings.ControlSlots;
        NoKillAwards = true;
    }

    /// <summary>The account that called it, kept past BaseCreature clearing its master, for the registry's cleanup.</summary>
    public IAccount? OwnerAccount { get; set; }

    /// <summary>The character that called it.</summary>
    public Mobile? Owner { get; set; }

    public override string DefaultName => "an Echo of the Stygian Dragon";

    // No mana (the design's 0): BaseCreature's ManaMax falls back to Int when no seed is set.
    public override int ManaMax => 0;

    public override bool DeleteCorpseOnDeath => true;
    public override bool IsDispellable => Summoned;
    public override double DispelDifficulty => 100.0;
    public override double DispelFocus => 20.0;
    public override bool CanFlee => false;
    public override bool BardImmune => false;
    public override bool CanAutoStable => false;
    public override bool IsBondable => false;
    public override bool CanDrop => false;
    public override int TreasureMapLevel => -1;
    public override int Meat => 0;
    public override int Hides => 0;
    public override int Scales => 0;
    public override int Feathers => 0;
    public override int Wool => 0;

    public override void GenerateLoot()
    {
    }

    public override void GenerateLoot(bool spawning)
    {
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("A brief memory of the dragon, bound to help");
    }

    // Lethal damage, Release and a kill command all end here: it dissolves instead of dying.
    public override bool OnBeforeDeath()
    {
        Dissolve();
        return false;
    }

    public void Dissolve()
    {
        if (Deleted)
        {
            return;
        }

        if (Map != null && Map != Map.Internal)
        {
            Effects.SendLocationParticles(EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x3728, 8, 20, 5042);
            Effects.PlaySound(Location, Map, 0x201);
        }

        Delete();
    }

    public override bool OnDragDrop(Mobile from, Item dropped) => false;

    public override bool IsEnemy(Mobile m) => LeagueEchoPolicy.IsLegalTarget(m) && base.IsEnemy(m);

    public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness)
    {
        if (!LeagueEchoPolicy.IsLegalTarget(target) || LeagueEchoPolicy.CheckPlace(Location, Map) != EchoPlace.Ok)
        {
            return false;
        }

        // The owner's ordinary harm rules apply to what the Echo hits for them (never back into this override).
        if (Owner is { Deleted: false } owner && !owner.CanBeHarmful(target, false))
        {
            return false;
        }

        return base.CanBeHarmful(target, message, ignoreOurBlessedness);
    }

    public override void OnAfterDelete()
    {
        LeagueEchoSystem.OnEchoDeleted(this);
        base.OnAfterDelete();
    }

    [AfterDeserialization]
    private void DeleteAfterLoad()
    {
        // A save taken during an Echo's short life: it never resumes and never registers.
        Timer.StartTimer(TimeSpan.Zero, Delete);
    }
}
