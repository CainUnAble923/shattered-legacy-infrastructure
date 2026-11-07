// PetSettingsFile.cs
//
// cc-P809: the small JSON loader the pet perch and horse barding settings share. A file under Configuration/, written with
// the built-in defaults on first start and read at start. A bad file is refused whole, every error is printed, and the
// defaults stay in force. Notes: shard-migration notes/cc-P809-pet-perch-horse-barding.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Server.Mobiles;

public static class PetSettingsFile
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    public static string PathOf(string fileName) => Path.Combine(Core.BaseDirectory, "Configuration", fileName);

    public static string ToJson<T>(T settings) => JsonSerializer.Serialize(settings, _json) + "\n";

    /// <summary>Reads settings, or returns null and fills errors: accepted whole or not at all.</summary>
    public static T Parse<T>(string json, Action<T, List<string>> validate, List<string> errors) where T : class
    {
        T s;

        try
        {
            s = JsonSerializer.Deserialize<T>(json, _json);
        }
        catch (JsonException ex)
        {
            errors.Add($"not valid JSON: {ex.Message}");
            return null;
        }

        if (s == null)
        {
            errors.Add("the file is empty.");
            return null;
        }

        validate(s, errors);
        return errors.Count == 0 ? s : null;
    }

    /// <summary>Loads the file into a settings object, or writes the defaults when it is missing. Prints what it did.</summary>
    public static T LoadOrWriteDefaults<T>(string fileName, Action<T, List<string>> validate) where T : class, new()
    {
        var path = PathOf(fileName);

        if (!File.Exists(path))
        {
            var defaults = new T();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, ToJson(defaults));
                Console.WriteLine($"[{fileName}] was missing; wrote the built-in defaults to {path}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{fileName}] is missing and could not be written ({ex.Message}); using the built-in defaults.");
            }

            return defaults;
        }

        var errors = new List<string>();
        T s = null;

        try
        {
            s = Parse(File.ReadAllText(path), validate, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"could not read it: {ex.Message}");
        }

        if (s == null)
        {
            Console.WriteLine($"[{fileName}] REFUSED, using the built-in defaults:");

            foreach (var e in errors)
            {
                Console.WriteLine($"[{fileName}]   {e}");
            }

            return new T();
        }

        return s;
    }

    public static void Range(double value, double min, double max, string name, List<string> errors)
    {
        if (double.IsNaN(value) || value < min || value > max)
        {
            errors.Add($"{name} must be from {min} to {max} (it is {value}).");
        }
    }
}
