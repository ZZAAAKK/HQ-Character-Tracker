using System.Text.Json;
using HQ_Character_Tracker.Models;
using HQ_Character_Tracker.Services;
using Xunit;

namespace HQ_Character_Tracker.Tests;

public sealed class RosterFileCodecTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    [Fact]
    public async Task SaveAndLoad_RoundTripsVersionedHqctFile()
    {
        var characters = new[] { new CharacterSheet { Name = "Test Hero", Notes = "Roster note" } };
        using var stream = new MemoryStream();

        await RosterFileCodec.SerializeAsync(stream, characters, JsonOptions, CancellationToken.None);
        var contents = stream.ToArray();

        Assert.Equal(new byte[] { 0x48, 0x51, 0x43, 0x54, 1 }, contents[..5]);

        var roster = await RosterFileCodec.DeserializeAsync(new MemoryStream(contents), JsonOptions, CancellationToken.None);
        var loadedCharacter = Assert.Single(roster);
        Assert.Equal("Test Hero", loadedCharacter.Name);
        Assert.Equal("Roster note", loadedCharacter.Notes);
    }

    [Fact]
    public async Task LoadRoster_AcceptsLegacyJsonArray()
    {
        var contents = JsonSerializer.SerializeToUtf8Bytes(new[] { new CharacterSheet { Name = "Legacy Hero" } });

        var roster = await RosterFileCodec.DeserializeAsync(new MemoryStream(contents), JsonOptions, CancellationToken.None);

        Assert.Equal("Legacy Hero", Assert.Single(roster).Name);
    }

    [Fact]
    public async Task LoadRoster_AcceptsLegacySingleCharacterObject()
    {
        var contents = JsonSerializer.SerializeToUtf8Bytes(new CharacterSheet { Name = "Single Hero" });

        var roster = await RosterFileCodec.DeserializeAsync(new MemoryStream(contents), JsonOptions, CancellationToken.None);

        Assert.Equal("Single Hero", Assert.Single(roster).Name);
    }

    [Fact]
    public async Task LoadRoster_RejectsUnsupportedContainerVersion()
    {
        var contents = new byte[] { 0x48, 0x51, 0x43, 0x54, 2 };

        await Assert.ThrowsAsync<JsonException>(() => RosterFileCodec.DeserializeAsync(new MemoryStream(contents), JsonOptions, CancellationToken.None));
    }
}
