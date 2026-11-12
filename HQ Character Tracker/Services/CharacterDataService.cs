using System.Text.Json;
using HQ_Character_Tracker.Models;

namespace HQ_Character_Tracker.Services;

public sealed class CharacterDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<CharacterSheet> LoadCharacterAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var character = await JsonSerializer.DeserializeAsync<CharacterSheet>(stream, JsonOptions, cancellationToken);
        return character ?? throw new JsonException("The selected file does not contain a valid character sheet.");
    }

    public async Task<List<CharacterSheet>> LoadRosterAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var characters = document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement.Deserialize<List<CharacterSheet>>(JsonOptions),
            JsonValueKind.Object => [document.RootElement.Deserialize<CharacterSheet>(JsonOptions)
                ?? throw new JsonException("The selected file does not contain a valid character sheet.")],
            _ => throw new JsonException("The selected file must contain a character roster array or a character sheet object.")
        };

        return characters ?? throw new JsonException("The selected file does not contain a valid character roster.");
    }

    public async Task<string> SaveRosterAsync(IReadOnlyCollection<CharacterSheet> characters, CancellationToken cancellationToken = default)
    {
        foreach (var character in characters)
        {
            if (character.Id == Guid.Empty)
                character.Id = Guid.NewGuid();
        }

        var path = Path.Combine(FileSystem.AppDataDirectory, "roster.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, characters, JsonOptions, cancellationToken);
        return path;
    }

    public async Task<string> SaveCharacterAsync(CharacterSheet character, string fileName, CancellationToken cancellationToken = default)
    {
        var safeFileName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = "Hero";

        if (character.Id == Guid.Empty)
            character.Id = Guid.NewGuid();

        var path = Path.Combine(FileSystem.AppDataDirectory, $"{safeFileName}-{character.Id:N}.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, character, JsonOptions, cancellationToken);
        return path;
    }

    public async Task<CharacterClassCatalog> LoadClassesAsync(CancellationToken cancellationToken = default) =>
        await LoadCatalogAsync<CharacterClassCatalog>("classes.json", cancellationToken);

    public async Task<EquipmentCatalog> LoadEquipmentAsync(CancellationToken cancellationToken = default) =>
        await LoadCatalogAsync<EquipmentCatalog>("equipment.json", cancellationToken);

    public async Task<SpellCatalog> LoadSpellsAsync(CancellationToken cancellationToken = default) =>
        await LoadCatalogAsync<SpellCatalog>("spells.json", cancellationToken);

    private static async Task<T> LoadCatalogAsync<T>(string fileName, CancellationToken cancellationToken)
        where T : class, new()
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken) ?? new T();
    }
}
