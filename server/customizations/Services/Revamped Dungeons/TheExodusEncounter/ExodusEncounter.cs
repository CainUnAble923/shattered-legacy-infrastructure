// cc-P195: the Exodus Encounter (ServUO Services/Revamped Dungeons/TheExodusEncounter), the shared pieces.
//
// The encounter: in Ilshenar's Exodus Dungeon, Dupre's men, zealots, drones, sentinels, a juggernaut and a minion lord
// hold the keys to a ritual (the Summoning Rite, the Sacrificial Dagger, the Robe of Rite, the Summoning Altar deed),
// won by fighting them, stealing from zealots, picking the hidden supply chests, or crafting them; the Nexus machines
// about the dungeon print punch cards that a Keypunch Reader turns into a Nexus deed. A party builds the Summoning Tome
// at a shrine, each member reads the rite and bleeds on the dagger, the owner begins the ritual, and the party is
// carried to Ver Lor Reg where Clockwork Exodus waits; its death starts a ten-minute exit and a new Exodus rises.
//
// What this file holds that ServUO keeps on PlayerMobile or nowhere:
//   - HasValiantStatReward (ServUO PlayerMobile's flag for the Scroll of Valiant Commendation) as a saved set of
//     players, GenericPersistence "ExodusEncounter" (pinned's PlayerMobile is not ours to patch for one flag).
//   - UseSummoningRite (ServUO PlayerMobile's flag between the rite and the dagger) is read from the tome altars'
//     own ritual lists (ExodusTomeAltar.HasUsedRite), which is what the flag stood for.
//   - SetWearable (ServUO BaseCreature: hue, not lootable, equip or pack) as Wear, for the creatures' gear.
//   - The stock Exodus minion's Mechanical Component drop (ServUO Mobiles/Normal/ExodusMinion.cs:225-232, one in
//     five; pinned's ExodusMinion has none), through the creature death event, so the Craftsman path has its start.
//   - The places ServUO hard-codes, named once.
// Notes: shard-migration/notes/cc-P195-port-exodus-encounter.md.

using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Exodus;

public static class ExodusEncounter
{
    public const string CollectionKey = "exodusencounter";

    /// <summary>ServUO VerLorRegController.Initialize: the controller's tile in Ilshenar.</summary>
    public static readonly Point3D ControllerLocation = new(849, 648, -40);

    /// <summary>ServUO VerLorRegController.Start: where Clockwork Exodus rises (home range 4).</summary>
    public static readonly Point3D ExodusLocation = new(854, 642, -40);

    /// <summary>ServUO ExodusTomeAltar.m_TeleportDest: where the ritual's party arrives in Ilshenar.</summary>
    public static readonly Point3D BattlegroundArrival = new(764, 640, 0);

    /// <summary>ServUO GenerateTheExodusEncounter.Generate: the way out, and the ankh beside it.</summary>
    public static readonly Point3D MoongateLocation = new(761, 641, 0);
    public static readonly Point3D AnkhLocation = new(757, 641, 0);

    /// <summary>ServUO Spawns/TheExodusEncounterQuest.xml, the "Arch Zealot" row: one Hunter, range 0.</summary>
    public static readonly Point3D ArchZealotLocation = new(946, 641, -79);

    private static ExodusEncounterPersistence _persistence;
    private static readonly HashSet<Mobile> _valiantRewarded = new();

    public static void Configure()
    {
        _persistence = new ExodusEncounterPersistence();
    }

    public static IReadOnlyCollection<Mobile> ValiantRewarded => _valiantRewarded;

    public static bool HasValiantStatReward(Mobile m) => m != null && _valiantRewarded.Contains(m);

    public static void SetValiantStatReward(Mobile m)
    {
        if (m != null)
        {
            _valiantRewarded.Add(m);
        }
    }

    /// <summary>Test support: forgets a player's reward.</summary>
    public static void ClearValiantStatReward(Mobile m) => _valiantRewarded.Remove(m);

    /// <summary>ServUO BaseCreature.SetWearable(item, hue, dropChance 0): hued, never lootable, worn or packed.</summary>
    public static void Wear(BaseCreature bc, Item item, int hue = -1)
    {
        if (hue > -1)
        {
            item.Hue = hue;
        }

        item.Movable = false;

        if (!bc.EquipItem(item))
        {
            bc.PackItem(item);
        }
    }

    /// <summary>ServUO Mobiles/Normal/ExodusMinion.cs:225-232: one minion corpse in five holds a Mechanical Component.</summary>
    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        if (bc is ExodusMinion { Controlled: false, Summoned: false } && bc.Corpse is { Deleted: false } corpse &&
            Utility.RandomDouble() < 0.2)
        {
            corpse.DropItem(new MechanicalComponent());
        }
    }

    public static void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version

        var live = new List<Mobile>();

        foreach (var m in _valiantRewarded)
        {
            if (m?.Deleted == false)
            {
                live.Add(m);
            }
        }

        writer.WriteEncodedInt(live.Count);

        foreach (var m in live)
        {
            writer.Write(m);
        }
    }

    public static void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var m = reader.ReadEntity<Mobile>();

            if (m?.Deleted == false)
            {
                _valiantRewarded.Add(m);
            }
        }
    }
}

public sealed class ExodusEncounterPersistence : GenericPersistence
{
    public ExodusEncounterPersistence() : base("ExodusEncounter", 10)
    {
    }

    public override void Serialize(IGenericWriter writer) => ExodusEncounter.Serialize(writer);

    public override void Deserialize(IGenericReader reader) => ExodusEncounter.Deserialize(reader);
}
