// Reads an item's tooltip back as (cliloc, argument) pairs, so a shard test can assert a property list.
//
// Until this existed a test could not: notes/new-haven-quest-picker.md section 6 records that asserting an
// ObjectPropertyList "means implementing IPropertyList in the test", and nobody had. It does not. Pinned's own
// ObjectPropertyList is a plain class in Server that needs no network, no map and no client; it writes the 0xD6
// packet into Buffer, and the entries are a flat list after a fixed 15-byte header:
//
//     int32 cliloc (big-endian) | uint16 byte length (big-endian) | UTF-16LE argument
//
// terminated by a zero cliloc (ObjectPropertyList.cs Add/InternalAdd/Terminate, pinned 7c9215d97). So this
// builds the real list the real way and decodes it; nothing here re-implements the property path it is checking.
// Arguments come back as the client receives them: tab-separated where the cliloc takes several, and a localized
// argument as "#<number>". See shard-migration/notes/armour-set-completion.md.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server;

namespace ShatteredLegacy.Tests;

public static class PropertyListReader
{
    private const int HeaderLength = 15;

    public static List<(int Number, string Argument)> Read(Item item)
    {
        var opl = new ObjectPropertyList(item);
        item.GetProperties(opl);
        opl.Terminate();

        var buffer = opl.Buffer;
        var entries = new List<(int, string)>();
        var pos = HeaderLength;

        while (pos + 6 <= buffer.Length)
        {
            var number = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(pos));

            if (number == 0)
            {
                break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(pos + 4));
            entries.Add((number, Encoding.Unicode.GetString(buffer, pos + 6, length)));
            pos += 6 + length;
        }

        return entries;
    }

    public static bool Has(this List<(int Number, string Argument)> list, int number) =>
        list.Any(e => e.Number == number);

    public static bool Has(this List<(int Number, string Argument)> list, int number, string argument) =>
        list.Any(e => e.Number == number && e.Argument == argument);

    public static string Describe(this List<(int Number, string Argument)> list) =>
        string.Join(" | ", list.Select(e => e.Argument.Length == 0 ? $"{e.Number}" : $"{e.Number}:{e.Argument}"));
}
