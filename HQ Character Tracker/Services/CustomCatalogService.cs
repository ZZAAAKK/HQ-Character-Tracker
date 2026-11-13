using System.Text.Json;
using System.Text.Json.Nodes;

namespace HQ_Character_Tracker.Services;

public enum CatalogKind
{
    Equipment,
    Classes,
    Spells
}

public sealed record CatalogFieldSchema(string Name, JsonValueKind Kind);

public sealed class CustomCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string GetPackagedFileName(CatalogKind kind) => kind switch
    {
        CatalogKind.Equipment => "equipment.json",
        CatalogKind.Classes => "classes.json",
        CatalogKind.Spells => "spells.json",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static string GetCustomFilePath(CatalogKind kind) => Path.Combine(
        FileSystem.AppDataDirectory,
        $"{Path.GetFileNameWithoutExtension(GetPackagedFileName(kind))}_custom.JSON");

    public async Task<IReadOnlyList<CatalogFieldSchema>> LoadSchemaAsync(CatalogKind kind, CancellationToken cancellationToken = default)
    {
        var root = await LoadPackagedDocumentAsync(kind, cancellationToken);
        var collection = GetCollection(root);
        var sample = collection.OfType<JsonObject>().FirstOrDefault()
            ?? throw new JsonException($"The packaged {kind} catalog does not contain a schema record.");

        return sample.Select(property => new CatalogFieldSchema(property.Key, GetValueKind(property.Value))).ToList();
    }

    public async Task<JsonArray> LoadCustomItemsAsync(CatalogKind kind, CancellationToken cancellationToken = default)
    {
        var path = GetCustomFilePath(kind);
        if (!File.Exists(path))
            return [];

        await using var stream = File.OpenRead(path);
        var root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken) as JsonObject
            ?? throw new JsonException($"The custom {kind} catalog must contain a JSON object.");
        var propertyName = await GetCollectionPropertyNameAsync(kind, cancellationToken);
        return root[propertyName] as JsonArray ?? [];
    }

    public async Task SaveCustomItemsAsync(CatalogKind kind, IEnumerable<JsonObject> items, CancellationToken cancellationToken = default)
    {
        var root = await LoadPackagedDocumentAsync(kind, cancellationToken);
        var propertyName = GetCollectionProperty(root).Key;
        root[propertyName] = new JsonArray(items.Select(item => item.DeepClone()).ToArray());

        var path = GetCustomFilePath(kind);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, root, JsonOptions, cancellationToken);
    }

    private static async Task<JsonObject> LoadPackagedDocumentAsync(CatalogKind kind, CancellationToken cancellationToken)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(GetPackagedFileName(kind));
        return await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken) as JsonObject
            ?? throw new JsonException($"The packaged {kind} catalog must contain a JSON object.");
    }

    private async Task<string> GetCollectionPropertyNameAsync(CatalogKind kind, CancellationToken cancellationToken)
    {
        var root = await LoadPackagedDocumentAsync(kind, cancellationToken);
        return GetCollectionProperty(root).Key;
    }

    private static JsonArray GetCollection(JsonObject root) => GetCollectionProperty(root).Value as JsonArray
        ?? throw new JsonException("The packaged catalog must contain a JSON array.");

    private static KeyValuePair<string, JsonNode?> GetCollectionProperty(JsonObject root) => root.FirstOrDefault(property => property.Value is JsonArray);

    private static JsonValueKind GetValueKind(JsonNode? node)
    {
        if (node is null)
            return JsonValueKind.Null;

        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.ValueKind;
    }
}
