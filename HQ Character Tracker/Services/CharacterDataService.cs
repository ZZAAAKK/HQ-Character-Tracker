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
        => await RosterFileCodec.DeserializeAsync(stream, JsonOptions, cancellationToken);

    public async Task<string> SaveRosterAsync(IReadOnlyCollection<CharacterSheet> characters, string path, CancellationToken cancellationToken = default)
    {
        foreach (var character in characters)
        {
            if (character.Id == Guid.Empty)
                character.Id = Guid.NewGuid();
        }

        await using var stream = File.Create(path);
        await RosterFileCodec.SerializeAsync(stream, characters, JsonOptions, cancellationToken);
        return path;
    }

    public async Task<string> SaveCharacterAsync(CharacterSheet character, string fileName, CancellationToken cancellationToken = default)
    {
        var safeFileName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = "Hero";

        if (character.Id == Guid.Empty)
            character.Id = Guid.NewGuid();

        var path = Path.Combine(UserDataDirectory.Path, $"{safeFileName}-{character.Id:N}.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, character, JsonOptions, cancellationToken);
        return path;
    }

    public async Task<CharacterClassCatalog> LoadClassesAsync(CancellationToken cancellationToken = default) =>
        await LoadMergedCatalogAsync<CharacterClassCatalog>("classes.json", cancellationToken, (baseCatalog, customCatalog) =>
        {
            customCatalog.Classes.ForEach(item => item.IsCustom = true);
            baseCatalog.Classes.AddRange(customCatalog.Classes);
        });

    public async Task<EquipmentCatalog> LoadEquipmentAsync(CancellationToken cancellationToken = default) =>
        await LoadMergedCatalogAsync<EquipmentCatalog>("equipment.json", cancellationToken, (baseCatalog, customCatalog) =>
        {
            customCatalog.Equipment.ForEach(item => item.IsCustom = true);
            baseCatalog.Equipment.AddRange(customCatalog.Equipment);
        });

    public async Task<SpellCatalog> LoadSpellsAsync(CancellationToken cancellationToken = default) =>
        await LoadMergedCatalogAsync<SpellCatalog>("spells.json", cancellationToken, (baseCatalog, customCatalog) =>
        {
            customCatalog.Spells.ForEach(item => item.IsCustom = true);
            baseCatalog.Spells.AddRange(customCatalog.Spells);
        });

    private static async Task<T> LoadMergedCatalogAsync<T>(string fileName, CancellationToken cancellationToken, Action<T, T> merge)
        where T : class, new()
    {
        var catalog = await LoadCatalogAsync<T>(fileName, cancellationToken);
        var customPath = Path.Combine(FileSystem.AppDataDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}_custom.JSON");
        if (!File.Exists(customPath))
            return catalog;

        await using var stream = File.OpenRead(customPath);
        var customCatalog = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        if (customCatalog is not null)
            merge(catalog, customCatalog);

        return catalog;
    }

    private static async Task<T> LoadCatalogAsync<T>(string fileName, CancellationToken cancellationToken)
        where T : class, new()
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken) ?? new T();
    }
}
