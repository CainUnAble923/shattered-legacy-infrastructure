// ServUO: Services/MiniChampionSystem/MiniChampSpawnInfo.cs (cc-P197). One creature type of a mini champion
// spawn's current level: how many must be killed (Required), how many have been (Killed), how many were ever
// spawned (Spawned, capped at Required x 2 - 1), and the live ones. Every second (MiniChamp.OnSlice) Slice counts
// the dead and brings a straggler more than SpawnRange + 10 from the controller back home, then Respawn fills
// the level back up to Required, each new creature untamable, homed on the controller (or at the BossSpawnPoint
// when one is set), and carrying the spawn's essence when its fame beats a roll of 100,000 or it is a Renowned.
//
// A nested generated class, pinned's SpawnerEntry shape (Engines/Spawners/SpawnerEntry.cs): the owning
// controller is the [DirtyTrackingEntity] and the constructor the generator uses takes it, so the owner ServUO
// writes as a field is implicit here; the four counters, the type and the creature list are saved as ServUO's.
// Map.GetSpawnPosition is ServUO's Map.cs:1159-1175 (NavreysController.GetSpawnPosition).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.MiniChamps;

[SerializationGenerator(0, false)]
public partial class MiniChampSpawnInfo
{
    [DirtyTrackingEntity]
    private MiniChamp _owner;

    [SerializableField(0)]
    private int _killed;

    [SerializableField(1)]
    private int _spawned;

    [SerializableField(2)]
    private int _required;

    [SerializableField(3)]
    private Type _monsterType;

    [Tidy]
    [SerializableField(4)]
    private List<Mobile> _creatures;

    // Declared first: the generator picks the first constructor that takes the owning entity.
    public MiniChampSpawnInfo(MiniChamp controller)
    {
        _owner = controller;
        _creatures = new List<Mobile>();
    }

    public MiniChampSpawnInfo(MiniChamp controller, MiniChampTypeInfo typeInfo) : this(controller)
    {
        _required = typeInfo.Required;
        _monsterType = typeInfo.SpawnType;

        _killed = 0;
        _spawned = 0;
    }

    public MiniChamp Owner => _owner;

    public int MaxSpawned => Required * 2 - 1;

    public bool Done => Killed >= Required;

    public bool Slice()
    {
        var killed = false;
        var list = new List<Mobile>(Creatures);

        foreach (var creature in list)
        {
            if (creature == null || creature.Deleted)
            {
                Creatures.Remove(creature);
                Killed++;

                killed = true;
            }
            else if (!creature.InRange(_owner.Location, _owner.SpawnRange + 10))
            {
                // bring to home
                var map = _owner.Map;
                var loc = NavreysController.GetSpawnPosition(map, _owner.Location, _owner.SpawnRange);

                creature.MoveToWorld(loc, map);
            }
        }

        return killed;
    }

    public bool Respawn()
    {
        var spawned = false;

        while (Creatures.Count < Required && Spawned < MaxSpawned)
        {
            if (Activator.CreateInstance(MonsterType) is not BaseCreature bc)
            {
                break;
            }

            var map = _owner.Map;
            var loc = NavreysController.GetSpawnPosition(map, _owner.Location, _owner.SpawnRange);

            if (_owner.BossSpawnPoint != Point3D.Zero)
            {
                loc = _owner.BossSpawnPoint;
            }

            bc.Home = _owner.Location;
            bc.RangeHome = _owner.SpawnRange;
            bc.Tamable = false;
            bc.OnBeforeSpawn(loc, map);
            bc.MoveToWorld(loc, map);

            if (bc.Fame > Utility.Random(100000) || bc is BaseRenowned)
            {
                DropEssence(bc);
            }

            Creatures.Add(bc);

            ++Spawned;

            spawned = true;
        }

        return spawned;
    }

    private void DropEssence(BaseCreature bc)
    {
        var essenceType = MiniChampInfo.GetInfo(_owner.Type).EssenceType;

        if (essenceType == null)
        {
            return;
        }

        Item essence = null;

        try
        {
            essence = Activator.CreateInstance(essenceType) as Item;
        }
        catch
        {
            // ServUO: swallowed
        }

        if (essence != null)
        {
            bc.PackItem(essence);
        }
    }

    public void AddProperties(IPropertyList list, int cliloc)
    {
        list.Add(cliloc, $"{MonsterType.Name}: Killed {Killed}/{Required}, Spawned {Spawned}/{MaxSpawned}");
    }
}
