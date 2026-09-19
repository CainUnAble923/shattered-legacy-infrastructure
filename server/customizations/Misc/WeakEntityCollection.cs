// ServUO: Misc/WeakEntityCollection.cs (CC4 Despise).
//
// A named bag of entities that a world generator adds to as it places things, so that a matching
// delete command ([DeleteDespise) can tear the whole set down again later. ServUO persists it through
// its own Persistence helper into Saves/WeakEntityCollection/WeakEntityCollection.bin; ModernUO's
// equivalent is a GenericPersistence subclass, which the world save calls like any other system and
// which lands at Saves/WeakEntityCollection/WeakEntityCollection.bin as well.
//
// Ported once here rather than worked around per dungeon: five of the Revamped Dungeons row's 255
// files use it (DespiseRevamped x2, Shame Revamped x2, Covetous Void Spawn), 35 files across ServUO.
//
// Conversion notes: the ServUO save format (version 1: key, count, serials) is reproduced, but this is
// fresh content with no existing save, so the version-0 branch (strong item/mobile lists) is dropped.
// Serials are resolved through World.FindEntity exactly as ServUO does.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Server;

public static class WeakEntityCollection
{
    public sealed class EntityCollection : List<IEntity>
    {
        public EntityCollection() : this(0x400)
        {
        }

        public EntityCollection(int capacity) : base(capacity)
        {
        }

        public IEnumerable<Item> Items => this.OfType<Item>();
        public IEnumerable<Mobile> Mobiles => this.OfType<Mobile>();
    }

    private static readonly Dictionary<string, EntityCollection> _collections =
        new(StringComparer.OrdinalIgnoreCase);

    private static WeakEntityCollectionPersistence _persistence;

    public static void Configure()
    {
        _persistence = new WeakEntityCollectionPersistence();
    }

    internal static void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(1); // version, ServUO's

        writer.WriteEncodedInt(_collections.Count);

        foreach (var (key, col) in _collections)
        {
            writer.Write(key);

            col.RemoveAll(ent => ent == null || ent.Deleted);

            writer.WriteEncodedInt(col.Count);

            foreach (var ent in col)
            {
                writer.Write(ent.Serial);
            }
        }
    }

    internal static void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version

        var entries = reader.ReadEncodedInt();

        for (var i = 0; i < entries; i++)
        {
            var key = reader.ReadString();
            var count = reader.ReadEncodedInt();
            var col = new EntityCollection(count);

            for (var j = 0; j < count; j++)
            {
                var ent = World.FindEntity(reader.ReadSerial());

                if (ent?.Deleted == false)
                {
                    col.Add(ent);
                }
            }

            _collections[key] = col;
        }
    }

    public static EntityCollection GetCollection(string name)
    {
        if (!_collections.TryGetValue(name, out var col) || col == null)
        {
            _collections[name] = col = new EntityCollection();
        }

        return col;
    }

    public static bool HasCollection(string name) => name != null && _collections.ContainsKey(name);

    public static void Add(string key, IEntity entity)
    {
        if (entity == null || entity.Deleted)
        {
            return;
        }

        var col = GetCollection(key);

        if (col != null && !col.Contains(entity))
        {
            col.Add(entity);
        }
    }

    public static bool Remove(string key, IEntity entity)
    {
        if (entity == null)
        {
            return false;
        }

        var col = GetCollection(key);

        return col != null && col.Remove(entity);
    }

    public static int Clean(string key)
    {
        var removed = 0;
        var col = GetCollection(key);

        if (col != null)
        {
            var ents = col.Count;

            while (--ents >= 0)
            {
                if (ents < col.Count && col[ents].Deleted)
                {
                    col.RemoveAt(ents);
                    ++removed;
                }
            }
        }

        return removed;
    }

    public static int Delete(string key)
    {
        var deleted = 0;
        var col = GetCollection(key);

        if (col != null)
        {
            var ents = col.Count;

            while (--ents >= 0)
            {
                if (ents < col.Count)
                {
                    col[ents].Delete();
                    ++deleted;
                }
            }

            col.Clear();
        }

        _collections.Remove(key);

        return deleted;
    }
}

public sealed class WeakEntityCollectionPersistence : GenericPersistence
{
    public WeakEntityCollectionPersistence() : base("WeakEntityCollection", 10)
    {
    }

    public override void Serialize(IGenericWriter writer) => WeakEntityCollection.Serialize(writer);

    public override void Deserialize(IGenericReader reader) => WeakEntityCollection.Deserialize(reader);
}
