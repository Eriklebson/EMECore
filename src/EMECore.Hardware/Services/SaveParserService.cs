using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class SaveParserService
{
    public async Task<Dictionary<string, object>> ParseAsync(SaveFile saveFile)
    {
        try
        {
            if (saveFile.RawData == null)
            {
                saveFile.RawData = await File.ReadAllBytesAsync(saveFile.FullPath);
            }

            var format = saveFile.Format == SaveFormat.Unknown
                ? DetectFormat(saveFile.FullPath)
                : saveFile.Format;

            saveFile.Format = format;

            var result = format switch
            {
                SaveFormat.Json => ParseJson(saveFile.RawData),
                SaveFormat.Xml => ParseXml(saveFile.RawData),
                SaveFormat.Ini => ParseIni(saveFile.RawData),
                SaveFormat.Text => ParseText(saveFile.RawData),
                SaveFormat.Csv => ParseCsv(saveFile.RawData),
                SaveFormat.Binary => ParseBinary(saveFile.RawData),
                SaveFormat.Sqlite => await ParseSqliteAsync(saveFile.FullPath),
                _ => new Dictionary<string, object>()
            };

            result["_filePath"] = saveFile.FullPath;
            result["_fileName"] = saveFile.FileName;
            result["_lastModified"] = saveFile.LastModified.ToString("o");
            result["_format"] = format.ToString();
            result["_fileSize"] = saveFile.FileSize;

            saveFile.ParsedData = result;
            return result;
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    public Dictionary<string, object> GetNestedValue(Dictionary<string, object> data, string keyPath)
    {
        var keys = keyPath.Split('.');
        object current = data;

        foreach (var key in keys)
        {
            if (current is Dictionary<string, object> dict && dict.TryGetValue(key, out var next))
            {
                current = next;
            }
            else if (current is JsonElement element)
            {
                current = element;
            }
            else
            {
                return new Dictionary<string, object>();
            }
        }

        if (current is Dictionary<string, object> result)
            return result;

        return new Dictionary<string, object> { ["value"] = current };
    }

    public object? GetNestedValue(Dictionary<string, object> data, string keyPath, object? defaultValue = null)
    {
        var keys = keyPath.Split('.');
        object current = data;

        foreach (var key in keys)
        {
            if (current is Dictionary<string, object> dict && dict.TryGetValue(key, out var next))
            {
                current = next;
            }
            else if (current is JsonElement element)
            {
                return element;
            }
            else
            {
                return defaultValue;
            }
        }

        return current;
    }

    private static SaveFormat DetectFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".json" => SaveFormat.Json,
            ".xml" => SaveFormat.Xml,
            ".sav" or ".save" or ".dat" or ".bin" => SaveFormat.Binary,
            ".cfg" or ".ini" or ".conf" => SaveFormat.Ini,
            ".db" or ".sqlite" or ".sqlite3" => SaveFormat.Sqlite,
            ".txt" or ".log" => SaveFormat.Text,
            ".csv" => SaveFormat.Csv,
            _ => SaveFormat.Unknown
        };
    }

    private static Dictionary<string, object> ParseJson(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        var doc = JsonDocument.Parse(json);
        return JsonElementToDict(doc.RootElement);
    }

    private static Dictionary<string, object> JsonElementToDict(JsonElement element)
    {
        var dict = new Dictionary<string, object>();
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                dict[prop.Name] = prop.Value;
            }
        }
        return dict;
    }

    private static Dictionary<string, object> ParseXml(byte[] data)
    {
        var xml = Encoding.UTF8.GetString(data);
        var doc = XDocument.Parse(xml);
        var dict = new Dictionary<string, object>();
        if (doc.Root != null)
        {
            dict = XElementToDict(doc.Root);
        }
        return dict;
    }

    private static Dictionary<string, object> XElementToDict(XElement element)
    {
        var dict = new Dictionary<string, object>();
        foreach (var attr in element.Attributes())
        {
            dict[$"@{attr.Name}"] = attr.Value;
        }
        foreach (var child in element.Elements())
        {
            var childDict = XElementToDict(child);
            dict[child.Name.LocalName] = childDict.Count > 0 ? childDict : (object)(child.Value ?? "");
        }
        if (!element.HasElements && !string.IsNullOrEmpty(element.Value))
        {
            dict["#text"] = element.Value;
        }
        return dict;
    }

    private static Dictionary<string, object> ParseIni(byte[] data)
    {
        var lines = Encoding.UTF8.GetString(data).Split('\n');
        var dict = new Dictionary<string, object>();
        var sections = new Dictionary<string, Dictionary<string, object>>();
        string currentSection = "";

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                sections[currentSection] = new Dictionary<string, object>();
            }
            else if (line.Contains('='))
            {
                var eqIdx = line.IndexOf('=');
                var key = line[..eqIdx].Trim();
                var value = line[(eqIdx + 1)..].Trim();

                if (sections.ContainsKey(currentSection))
                    sections[currentSection][key] = value;
                else
                    dict[key] = value;
            }
        }

        foreach (var section in sections)
        {
            dict[section.Key] = section.Value;
        }

        return dict;
    }

    private static Dictionary<string, object> ParseText(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        var dict = new Dictionary<string, object>();
        var lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.Contains('='))
            {
                var eqIdx = line.IndexOf('=');
                var key = line[..eqIdx].Trim();
                var value = line[(eqIdx + 1)..].Trim();
                dict[key] = value;
            }
            else
            {
                dict[$"_line_{i}"] = line;
            }
        }

        dict["_lineCount"] = lines.Length;
        dict["_rawText"] = text;
        return dict;
    }

    private static Dictionary<string, object> ParseCsv(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        var lines = text.Split('\n');
        var dict = new Dictionary<string, object>();

        if (lines.Length == 0) return dict;

        var headers = lines[0].Split(',');
        var rows = new List<Dictionary<string, string>>();

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var values = line.Split(',');
            var row = new Dictionary<string, string>();
            for (int j = 0; j < Math.Min(headers.Length, values.Length); j++)
            {
                row[headers[j].Trim()] = values[j].Trim();
            }
            rows.Add(row);
        }

        dict["_headers"] = headers;
        dict["_rows"] = rows;
        dict["_rowCount"] = rows.Count;

        return dict;
    }

    private static Dictionary<string, object> ParseBinary(byte[] data)
    {
        var dict = new Dictionary<string, object>();
        dict["_rawBytes"] = data;
        dict["_size"] = data.Length;

        var text = Encoding.UTF8.GetString(data);

        var patterns = new[]
        {
            "TESV_SAVEGame",
            "TES4_SAVE",
            "CKWD_SAVE",
            "Fallout4 Save Game",
            "FalloutNV",
            "Fallout3",
            "Morrowind",
            "Oblivion"
        };

        foreach (var pattern in patterns)
        {
            if (text.Contains(pattern))
            {
                dict["_engine"] = "CreationEngine";
                dict["_magic"] = pattern;
                break;
            }
        }

        if (data.Length >= 4)
        {
            dict["_header"] = $"0x{data[0]:X2}{data[1]:X2}{data[2]:X2}{data[3]:X2}";
        }

        var strings = ExtractStrings(data);
        if (strings.Count > 0)
        {
            dict["_strings"] = strings;
            dict["_stringCount"] = strings.Count;
        }

        return dict;
    }

    private static List<string> ExtractStrings(byte[] data)
    {
        var strings = new List<string>();
        var current = new StringBuilder();

        foreach (var b in data)
        {
            if (b >= 32 && b < 127)
            {
                current.Append((char)b);
            }
            else
            {
                if (current.Length >= 4)
                {
                    strings.Add(current.ToString());
                }
                current.Clear();
            }
        }

        if (current.Length >= 4)
            strings.Add(current.ToString());

        return strings;
    }

    private static async Task<Dictionary<string, object>> ParseSqliteAsync(string filePath)
    {
        var dict = new Dictionary<string, object>();
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={filePath};Mode=ReadOnly");
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            var tables = new List<string>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            dict["_tables"] = tables;

            foreach (var table in tables)
            {
                try
                {
                    using var dataCmd = connection.CreateCommand();
                    dataCmd.CommandText = $"SELECT * FROM [{table}] LIMIT 100";
                    var rows = new List<Dictionary<string, object>>();
                    using (var reader = await dataCmd.ExecuteReaderAsync())
                    {
                        var schema = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            schema.Add(reader.GetName(i));
                        }

                        while (await reader.ReadAsync())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[schema[i]] = reader.IsDBNull(i) ? "" : reader.GetValue(i);
                            }
                            rows.Add(row);
                        }
                    }
                    dict[table] = rows;
                }
                catch { }
            }
        }
        catch { }

        return dict;
    }
}
