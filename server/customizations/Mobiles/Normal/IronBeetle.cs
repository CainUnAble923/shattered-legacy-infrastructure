// ServUO: Mobiles/Normal/IronBeetle.cs (cc-P129). Values verbatim; serialization by the generator. The mining pet
// of Ter Mur, one of the two creature spawn orphans cc-P119 left (tools/spawn-orphans.txt).
//
// Pinned members that differ, each checked in D:\UO\ModernUO-pinned:
//   HarvestSystem.Construct(type, from)        ServUO passes a third (tool) argument; pinned has two.
//   HarvestDefinition.Validate(tileID, isLand) pinned takes the land flag; the beetle mines land tiles.
//   GetContextMenuEntries                      pinned's list is a ref PooledRefList; BaseHarvestTool's
//                                              ToggleMiningStoneEntry is private there, so the two entries are
//                                              reproduced here with the same clilocs and the same OnClick.
//   MiningType                                 absent; pinned's entry takes a bool (stone or not) instead.
//   GetControlChance                           the profile's HasCustomized() is the ability core's (cc-P121).
//   Timer.DelayCall(delay, interval, callback) returns a DelayCallTimer here; the timer is restarted after a load
//                                              ([AfterDeserialization]) exactly as ServUO's Deserialize does.
// One fix, D-4 rule (broken, not missing): ServUO never stops the mining timer, so a deleted beetle's timer ticks
// every 5 s for the life of the process (DoMining returns at Map == null). It is stopped in OnAfterDelete here.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Harvest;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class IronBeetle : BaseCreature
{
    [Constructible]
    public IronBeetle() : base(AIType.AI_Melee)
    {
        Body = 714;
        BaseSoundID = 397;

        SetStr(816, 883);
        SetDex(68, 73);
        SetInt(40, 49);

        SetHits(762, 830);

        SetDamage(15, 20);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 55, 60);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 45, 55);

        SetSkill(SkillName.Anatomy, 80.1, 85.0);
        SetSkill(SkillName.MagicResist, 125.1, 130.0);
        SetSkill(SkillName.Tactics, 90.1, 100.0);
        SetSkill(SkillName.Wrestling, 90.1, 110.0);
        SetSkill(SkillName.Mining, 50.1, 70.0);

        Skills.Mining.Cap = 120;

        Fame = 15000;
        Karma = -15000;

        Tamable = true;
        MinTameSkill = 71.1;
        ControlSlots = 4;

        VirtualArmor = 38;

        StartMining();
    }

    public override string CorpseName => "an iron beetle corpse";
    public override string DefaultName => "an iron beetle";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
        AddLoot(LootPack.Gems);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Controlled)
        {
            return;
        }

        if (!Controlled && Utility.RandomDouble() < 0.03)
        {
            c.DropItem(new LuckyCoin());
        }

        if (!Controlled && Utility.RandomDouble() < 0.1)
        {
            c.DropItem(new UndamagedIronBeetleScale());
        }
    }

    public override bool SubdueBeforeTame => true;
    public override bool StatLossAfterTame => true;

    public override bool OverrideBondingReqs() => true;

    public override double GetControlChance(Mobile m, bool useBaseSkill = false)
    {
        if (PetTrainingHelper.Enabled)
        {
            var profile = PetTrainingHelper.GetAbilityProfile(this);

            if (profile != null && profile.HasCustomized())
            {
                return base.GetControlChance(m, useBaseSkill);
            }
        }

        return 1.0;
    }

    public override int GetAngerSound() => 0x21D;
    public override int GetIdleSound() => 0x21D;
    public override int GetAttackSound() => 0x162;
    public override int GetHurtSound() => 0x163;
    public override int GetDeathSound() => 0x21D;

    #region Mining
    private static readonly TimeSpan MiningInterval = TimeSpan.FromSeconds(5.0);

    private Timer _miningTimer;
    private DateTime _nextOreEat;

    private void StartMining()
    {
        _miningTimer = Timer.DelayCall(MiningInterval, MiningInterval, DoMining);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        StartMining();
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        _miningTimer?.Stop();
        _miningTimer = null;
    }

    private static void GetMiningOffset(Direction d, ref int x, ref int y)
    {
        switch (d & Direction.Mask)
        {
            case Direction.North: --y; break;
            case Direction.South: ++y; break;
            case Direction.West: --x; break;
            case Direction.East: ++x; break;
            case Direction.Right: ++x; --y; break;
            case Direction.Left: --x; ++y; break;
            case Direction.Down: ++x; ++y; break;
            case Direction.Up: --x; --y; break;
        }
    }

    public override void OnThink()
    {
        base.OnThink();

        if (Owners.Count > 0 || _nextOreEat > Core.Now)
        {
            return;
        }

        _nextOreEat = Core.Now + TimeSpan.FromSeconds(3.0);

        if (0.5 > Utility.RandomDouble())
        {
            foreach (var item in Map.GetItemsInRange(Location, 1))
            {
                if (item is BaseOre)
                {
                    // Epic coolness: turn to the ore hue!
                    Hue = item.Hue;

                    item.Delete();

                    return;
                }
            }
        }
    }

    public void DoMining()
    {
        if (Map == null || Map == Map.Internal)
        {
            return;
        }

        // We may not mine while we are fighting
        if (Combatant != null)
        {
            return;
        }

        HarvestSystem system = Mining.System;
        var def = Mining.System.OreAndStone;

        // Our target is the land tile under us
        var map = Map;
        var loc = Location;
        int x = 0, y = 0;
        GetMiningOffset(Direction, ref x, ref y);
        loc.X += x;
        loc.Y += y;
        var tileId = map.Tiles.GetLandTile(loc.X, loc.Y).ID & 0x3FFF;

        if (!def.Validate(tileId, true))
        {
            return;
        }

        var bank = def.GetBank(map, loc.X, loc.Y);

        if (bank == null || bank.Current < def.ConsumedPerHarvest)
        {
            return;
        }

        var vein = bank.Vein;

        if (vein == null)
        {
            return;
        }

        var primary = vein.PrimaryResource;
        var fallback = def.Resources[0];

        var resource = system.MutateResource(this, null, def, map, loc, vein, primary, fallback);

        var skillBase = Skills[def.Skill].Base;

        if (skillBase >= resource.ReqSkill && CheckSkill(def.Skill, resource.MinSkill, resource.MaxSkill))
        {
            var type = system.GetResourceType(this, null, def, map, loc, resource);

            if (type != null)
            {
                type = system.MutateType(type, this, null, def, map, loc, resource);
            }

            if (type != null)
            {
                var item = system.Construct(type, this);

                if (item != null)
                {
                    if (item.Stackable)
                    {
                        var amount = def.ConsumedPerHarvest;
                        var feluccaAmount = def.ConsumedPerFeluccaHarvest;

                        var inFelucca = map == Map.Felucca;

                        item.Amount = inFelucca ? feluccaAmount : amount;
                    }

                    bank.Consume(item.Amount, this);

                    item.MoveToWorld(loc, map);

                    system.DoHarvestingEffect(this, null, def, map, loc);
                    system.DoHarvestingSound(this, null, def, null);

                    // Mine for gems
                    var bonus = def.GetBonusResource();

                    if (bonus?.Type != null && skillBase >= bonus.ReqSkill)
                    {
                        var bonusItem = system.Construct(bonus.Type, this);

                        bonusItem?.MoveToWorld(loc, map);
                    }
                }
            }
        }
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (Controlled && ControlMaster == from && from is PlayerMobile pm)
        {
            list.Add(new ContextMenuEntry(pm.ToggleMiningStone ? 6179 : 6178) { Color = 0x421F });

            list.Add(new ToggleMiningStoneEntry(pm, false, 6176)); // Set To Ore
            list.Add(new ToggleMiningStoneEntry(pm, true, 6177));  // Set To Ore and Stone
        }
    }

    // Pinned BaseHarvestTool.ToggleMiningStoneEntry (private there), the same clilocs and rules.
    private class ToggleMiningStoneEntry : ContextMenuEntry
    {
        private readonly bool _value;

        public ToggleMiningStoneEntry(PlayerMobile pm, bool value, int number) : base(number)
        {
            _value = value;
            Enabled = value
                ? !pm.ToggleMiningStone && pm.StoneMining && pm.Skills.Mining.Base >= 100.0
                : pm.ToggleMiningStone;
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (from is not PlayerMobile pm)
            {
                return;
            }

            var oldValue = pm.ToggleMiningStone;

            if (_value)
            {
                if (oldValue)
                {
                    pm.SendLocalizedMessage(1054023); // You are already set to mine both ore and stone!
                }
                else if (!pm.StoneMining || pm.Skills.Mining.Base < 100.0)
                {
                    // You have not learned how to mine stone or you do not have enough skill!
                    pm.SendLocalizedMessage(1054024);
                }
                else
                {
                    pm.ToggleMiningStone = true;
                    pm.SendLocalizedMessage(1054022); // You are now set to mine both ore and stone.
                }
            }
            else if (oldValue)
            {
                pm.ToggleMiningStone = false;
                pm.SendLocalizedMessage(1054020); // You are now set to mine only ore.
            }
            else
            {
                pm.SendLocalizedMessage(1054021); // You are already set to mine only ore!
            }
        }
    }
    #endregion
}
