using System.Text;
using System.Buffers.Binary;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

/// <summary>
/// Parser para saves do Elden Ring (formato SL2).
/// Baseado na especificação: https://github.com/oisis/EldenRing-SaveForge/blob/main/docs/sl2-binary-format-spec.md
/// E no código de referência: https://github.com/BenGrn/EldenRingSaveCopier
/// </summary>
public class EldenRingSaveParser
{
    private const string BND4_MAGIC = "BND4";

    private const int SLOT_START_INDEX = 0x310;
    private const int SLOT_LENGTH = 0x280000;
    private const int SLOT_WITH_CHECKSUM = 0x280010;

    private const int SAVE_HEADERS_SECTION_START_INDEX = 0x19003B0;
    private const int SAVE_HEADERS_SECTION_LENGTH = 0x60000;
    private const int SAVE_HEADER_START_INDEX = 0x1901D0E;
    private const int SAVE_HEADER_LENGTH = 0x24C;

    private const int CHAR_ACTIVE_STATUS_START_INDEX = 0x1901D04;
    private const int CHAR_NAME_LENGTH = 0x22;
    private const int CHAR_LEVEL_LOCATION = 0x22;
    private const int CHAR_PLAYED_START_INDEX = 0x26;

    private const int MAGIC_PATTERN_SIZE = 64;
    private static readonly byte[] MagicPattern = {
        0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    private const int EVENT_FLAGS_OFFSET = 0x1C0000;
    private const int EVENT_FLAGS_SIZE = 0x1BF99F;

    public EldenRingSaveData? ParseFromFile(string filePath)
    {
        try
        {
            var data = File.ReadAllBytes(filePath);
            var result = ParseFromBytes(data);
            if (result != null)
                result.FilePath = filePath;
            return result;
        }
        catch { return null; }
    }

    public EldenRingSaveData? ParseFromBytes(byte[] data)
    {
        if (data.Length < SLOT_START_INDEX + SLOT_WITH_CHECKSUM)
            return null;

        var magic = Encoding.ASCII.GetString(data, 0, 4);
        if (magic != BND4_MAGIC)
            return null;

        var save = new EldenRingSaveData
        {
            FileSize = data.Length
        };

        var activeSlots = ExtractActiveSlots(data);

        for (int slot = 0; slot < 10; slot++)
        {
            var isActive = activeSlots[slot];
            var headerInfo = ExtractCharacterFromHeader(data, slot);

            if (headerInfo != null && headerInfo.IsValid)
            {
                save.TotalSlots++;
                headerInfo.SlotIndex = slot;
                headerInfo.IsActive = isActive;

                if (headerInfo.Level > save.MaxLevel)
                {
                    save.MaxLevel = headerInfo.Level;
                    save.PlayerName = headerInfo.Name;
                }

                save.Slots.Add(headerInfo);
            }
        }

        if (save.TotalSlots == 0)
            return null;

        return save;
    }

    private bool[] ExtractActiveSlots(byte[] data)
    {
        var active = new bool[10];
        try
        {
            for (int i = 0; i < 10; i++)
            {
                active[i] = data[CHAR_ACTIVE_STATUS_START_INDEX + i] == 1;
            }
        }
        catch { }
        return active;
    }

    private EldenRingCharacter? ExtractCharacterFromHeader(byte[] data, int slotIndex)
    {
        try
        {
            var headerOffset = SAVE_HEADER_START_INDEX + (slotIndex * SAVE_HEADER_LENGTH);

            if (headerOffset + SAVE_HEADER_LENGTH > data.Length)
                return null;

            var nameBytes = new byte[CHAR_NAME_LENGTH * 2];
            Array.Copy(data, headerOffset, nameBytes, 0, nameBytes.Length);
            var name = Encoding.Unicode.GetString(nameBytes);
            var nullIdx = name.IndexOf('\0');
            if (nullIdx >= 0) name = name[..nullIdx];
            name = name.Trim();

            var level = data[headerOffset + CHAR_LEVEL_LOCATION];

            var secondsPlayed = BinaryPrimitives.ReadInt32LittleEndian(
                data.AsSpan(headerOffset + CHAR_PLAYED_START_INDEX, 4));

            var character = new EldenRingCharacter
            {
                Name = name,
                Level = level,
                SecondsPlayed = secondsPlayed,
                IsValid = true
            };

            return character;
        }
        catch
        {
            return null;
        }
    }

    public EldenRingCharacter? ExtractDetailedCharacterData(byte[] data, int slotIndex)
    {
        try
        {
            var slotOffset = SLOT_START_INDEX + (slotIndex * SLOT_WITH_CHECKSUM);

            if (slotOffset + SLOT_LENGTH > data.Length)
                return null;

            var slotData = new byte[SLOT_LENGTH];
            Array.Copy(data, slotOffset + 16, slotData, 0, SLOT_LENGTH);

            var magicOffset = FindMagicOffset(slotData);
            if (magicOffset < 0)
                return null;

            var character = ExtractPlayerGameData(slotData, magicOffset);
            if (character != null)
            {
                character.SlotIndex = slotIndex;
                character.IsValid = true;
            }

            return character;
        }
        catch
        {
            return null;
        }
    }

    private int FindMagicOffset(byte[] slotData)
    {
        for (int i = 0; i <= slotData.Length - MAGIC_PATTERN_SIZE; i += 4)
        {
            if (CompareBytes(slotData, i, MagicPattern, 0, MAGIC_PATTERN_SIZE))
                return i;
        }
        return -1;
    }

    private static bool CompareBytes(byte[] data, int dataOffset, byte[] pattern, int patternOffset, int length)
    {
        for (int i = 0; i < length; i++)
        {
            if (data[dataOffset + i] != pattern[patternOffset + i])
                return false;
        }
        return true;
    }

    private EldenRingCharacter? ExtractPlayerGameData(byte[] slotData, int magicOffset)
    {
        try
        {
            var character = new EldenRingCharacter();

            character.Vigor = BinaryPrimitives.ReadUInt32LittleEndian(slotData.AsSpan(magicOffset - 379, 4));
            character.Mind = BinaryPrimitives.ReadUInt32LittleEndian(slotData.AsSpan(magicOffset - 375, 4));
            character.Endurance = BinaryPrimitives.ReadUInt32LittleEndian(slotData.AsSpan(magicOffset - 371, 4));
            character.Strength = BinaryPrimitives.ReadUInt32LittleEndian(slotData.AsSpan(magicOffset - 367, 4));
            character.Dexterity = BinaryPrimitives.ReadUInt32LittleEndian(slotData.AsSpan(magicOffset - 363, 4));
            character.Intelligence = BinaryPrimitives.ReadUInt32LittleEndian(slotData.AsSpan(magicOffset - 359, 4));
            character.Faith = BinaryPrimitives.ReadUInt32LittleEndian(slotData.AsSpan(magicOffset - 355, 4));
            character.Arcane = BinaryPrimitives.ReadUInt32LittleEndian(slotData.AsSpan(magicOffset - 351, 4));

            character.Level = (int)BinaryPrimitives.ReadUInt32LittleEndian(slotData.AsSpan(magicOffset - 335, 4));
            character.Runes = BinaryPrimitives.ReadUInt32LittleEndian(slotData.AsSpan(magicOffset - 331, 4));
            character.SoulMemory = BinaryPrimitives.ReadUInt32LittleEndian(slotData.AsSpan(magicOffset - 327, 4));

            var nameBytes = new byte[CHAR_NAME_LENGTH * 2];
            Array.Copy(slotData, magicOffset - 283, nameBytes, 0, nameBytes.Length);
            character.Name = Encoding.Unicode.GetString(nameBytes);
            var nullIdx = character.Name.IndexOf('\0');
            if (nullIdx >= 0) character.Name = character.Name[..nullIdx];
            character.Name = character.Name.Trim();

            character.Gender = slotData[magicOffset - 249];
            character.Class = slotData[magicOffset - 248];

            return character;
        }
        catch
        {
            return null;
        }
    }

    public EventFlags? ExtractEventFlags(byte[] data, int slotIndex)
    {
        try
        {
            var slotOffset = SLOT_START_INDEX + (slotIndex * SLOT_WITH_CHECKSUM);

            if (slotOffset + SLOT_LENGTH > data.Length)
                return null;

            var slotData = new byte[SLOT_LENGTH];
            Array.Copy(data, slotOffset + 16, slotData, 0, SLOT_LENGTH);

            var magicOffset = FindMagicOffset(slotData);
            if (magicOffset < 0)
                return null;

            var flagsOffset = magicOffset + EVENT_FLAGS_OFFSET;
            if (flagsOffset + EVENT_FLAGS_SIZE > slotData.Length)
                return null;

            var flags = new EventFlags();
            flags.Data = new byte[EVENT_FLAGS_SIZE];
            Array.Copy(slotData, flagsOffset, flags.Data, 0, EVENT_FLAGS_SIZE);

            return flags;
        }
        catch
        {
            return null;
        }
    }

    private static int CountSetBits(byte[] data)
    {
        int count = 0;
        foreach (var b in data)
        {
            count += System.Numerics.BitOperations.PopCount(b);
        }
        return count;
    }
}

public class EldenRingSaveData
{
    public string FilePath { get; set; } = "";
    public long FileSize { get; set; }
    public int TotalSlots { get; set; }
    public string PlayerName { get; set; } = "";
    public int MaxLevel { get; set; }
    public List<EldenRingCharacter> Slots { get; set; } = new();

    public Dictionary<string, long> StatsData { get; set; } = new();
    public Dictionary<string, bool> QuestData { get; set; } = new();
}

public class EldenRingCharacter
{
    public string Name { get; set; } = "";
    public int SlotIndex { get; set; }
    public bool IsActive { get; set; }
    public bool IsValid { get; set; }

    public int Level { get; set; }
    public int SecondsPlayed { get; set; }
    public uint Runes { get; set; }
    public uint SoulMemory { get; set; }

    public uint Vigor { get; set; }
    public uint Mind { get; set; }
    public uint Endurance { get; set; }
    public uint Strength { get; set; }
    public uint Dexterity { get; set; }
    public uint Intelligence { get; set; }
    public uint Faith { get; set; }
    public uint Arcane { get; set; }

    public int Gender { get; set; }
    public int Class { get; set; }

    public int CalculatedLevel => (int)(Vigor + Mind + Endurance + Strength + Dexterity + Intelligence + Faith + Arcane - 79);
}

public class EventFlags
{
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public bool IsFlagSet(int flagId)
    {
        if (flagId < 0 || flagId >= Data.Length * 8)
            return false;

        var byteIndex = flagId / 8;
        var bitIndex = flagId % 8;

        return (Data[byteIndex] & (1 << bitIndex)) != 0;
    }

    public void SetFlag(int flagId)
    {
        if (flagId < 0 || flagId >= Data.Length * 8)
            return;

        var byteIndex = flagId / 8;
        var bitIndex = flagId % 8;

        Data[byteIndex] |= (byte)(1 << bitIndex);
    }

    public int CountSetFlags()
    {
        int count = 0;
        foreach (var b in Data)
        {
            count += System.Numerics.BitOperations.PopCount(b);
        }
        return count;
    }
}
