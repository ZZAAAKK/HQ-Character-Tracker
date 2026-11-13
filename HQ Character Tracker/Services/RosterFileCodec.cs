using System.IO.Compression;
using System.Text.Json;
using HQ_Character_Tracker.Models;

namespace HQ_Character_Tracker.Services;

internal static class RosterFileCodec
{
    private static readonly byte[] Magic = [0x48, 0x51, 0x43, 0x54];
    private const byte CurrentVersion = 1;
    private const int HeaderLength = 5;
    private const int MaximumContentSize = 32 * 1024 * 1024;

    public static async Task SerializeAsync(
        Stream destination,
        IReadOnlyCollection<CharacterSheet> characters,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        await destination.WriteAsync(Magic, cancellationToken);
        destination.WriteByte(CurrentVersion);

        await using var compressed = new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: true);
        await JsonSerializer.SerializeAsync(compressed, characters, options, cancellationToken);
    }

    public static async Task<List<CharacterSheet>> DeserializeAsync(
        Stream source,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        var contents = await ReadLimitedAsync(source, cancellationToken);
        if (contents.Length < Magic.Length || !contents.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            return DeserializeJson(contents, options);

        if (contents.Length < HeaderLength)
            throw new JsonException("The .hqct roster file has an incomplete header.");

        var version = contents[Magic.Length];
        if (version != CurrentVersion)
            throw new JsonException($"The .hqct roster format version {version} is not supported.");

        try
        {
            using var compressed = new MemoryStream(contents, HeaderLength, contents.Length - HeaderLength, writable: false);
            await using var decompressor = new GZipStream(compressed, CompressionMode.Decompress);
            var json = await ReadLimitedAsync(decompressor, cancellationToken);
            return DeserializeJson(json, options);
        }
        catch (InvalidDataException exception)
        {
            throw new JsonException("The .hqct roster payload is invalid or damaged.", exception);
        }
    }

    private static List<CharacterSheet> DeserializeJson(byte[] contents, JsonSerializerOptions options)
    {
        using var document = JsonDocument.Parse(contents);
        var characters = document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement.Deserialize<List<CharacterSheet>>(options),
            JsonValueKind.Object => [document.RootElement.Deserialize<CharacterSheet>(options)
                ?? throw new JsonException("The selected file does not contain a valid character sheet.")],
            _ => throw new JsonException("The selected file must contain a character roster array or a character sheet object.")
        };

        return characters ?? throw new JsonException("The selected file does not contain a valid character roster.");
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream source, CancellationToken cancellationToken)
    {
        using var contents = new MemoryStream();
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (contents.Length + bytesRead > MaximumContentSize)
                throw new JsonException("The roster file exceeds the maximum supported size.");

            await contents.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        return contents.ToArray();
    }
}
