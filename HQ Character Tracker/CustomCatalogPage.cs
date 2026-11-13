using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HQ_Character_Tracker.Services;

namespace HQ_Character_Tracker;

public sealed class CustomCatalogPage : ContentPage
{
    private readonly CustomCatalogService _catalogService = new();
    private readonly Func<Task> _catalogChanged;
    private readonly Picker _kindPicker = new();
    private readonly VerticalStackLayout _itemsLayout = new() { Spacing = 8 };
    private readonly VerticalStackLayout _formLayout = new() { Spacing = 10, IsVisible = false };
    private readonly Label _statusLabel = new() { FontSize = 12 };
    private readonly Dictionary<string, View> _fieldControls = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<CatalogFieldSchema> _schema = [];
    private JsonArray _customItems = [];
    private JsonObject? _editingItem;

    public CustomCatalogPage(Func<Task> catalogChanged)
    {
        _catalogChanged = catalogChanged;
        Title = "Custom Catalogs";
        BackgroundColor = ThemeColor("#F3F4F6", "#111821");

        var closeButton = new Button { Text = "Done", HorizontalOptions = LayoutOptions.End, BackgroundColor = ThemeColor("#34465A", "#34465A"), TextColor = Colors.White };
        closeButton.Clicked += async (_, _) => await Navigation.PopModalAsync();
        var heading = new Label { Text = "Custom Equipment, Classes & Spells", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = ThemeColor("#202A36", "#F2F4F7") };
        var description = new Label { Text = "Custom entries are saved locally. Packaged entries are not changed.", FontSize = 13, TextColor = ThemeColor("#667085", "#B0BAC7") };
        _kindPicker.Title = "Catalog type";
        _kindPicker.ItemsSource = Enum.GetValues<CatalogKind>().Select(GetKindLabel).ToList();
        _kindPicker.SelectedIndex = 0;
        _kindPicker.SelectedIndexChanged += async (_, _) => await LoadCategoryAsync();

        var addButton = new Button { Text = "Add custom item", BackgroundColor = ThemeColor("#9A6B2F", "#9A6B2F"), TextColor = Colors.White, HorizontalOptions = LayoutOptions.Start };
        addButton.Clicked += (_, _) => ShowEditor(null);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 20),
                Spacing = 12,
                Children = { closeButton, heading, description, _kindPicker, addButton, _statusLabel, _itemsLayout, _formLayout }
            }
        };

        _ = LoadCategoryAsync();
    }

    private CatalogKind SelectedKind => Enum.GetValues<CatalogKind>()[Math.Max(0, _kindPicker.SelectedIndex)];

    private async Task LoadCategoryAsync()
    {
        try
        {
            _schema = await _catalogService.LoadSchemaAsync(SelectedKind);
            _customItems = await _catalogService.LoadCustomItemsAsync(SelectedKind);
            _statusLabel.Text = $"Custom file: {Path.GetFileName(CustomCatalogService.GetCustomFilePath(SelectedKind))}";
            _statusLabel.TextColor = ThemeColor("#667085", "#B0BAC7");
            _formLayout.IsVisible = false;
            RefreshItems();
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"Could not load catalog: {exception.Message}";
            _statusLabel.TextColor = ThemeColor("#B42318", "#FFB4AB");
        }
    }

    private void RefreshItems()
    {
        _itemsLayout.Children.Clear();
        foreach (var item in _customItems.OfType<JsonObject>())
        {
            var name = GetName(item);
            var row = new Grid
            {
                Padding = 10,
                BackgroundColor = ThemeColor("#FFFFFF", "#1D2935"),
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 8
            };
            row.Children.Add(new Label { Text = name, TextColor = ThemeColor("#202A36", "#F2F4F7"), VerticalTextAlignment = TextAlignment.Center });
            var editButton = new Button { Text = "Edit", Padding = new Thickness(12, 4) };
            editButton.Clicked += (_, _) => ShowEditor(item);
            Grid.SetColumn(editButton, 1);
            row.Children.Add(editButton);
            var removeButton = new Button { Text = "Remove", Padding = new Thickness(12, 4), BackgroundColor = ThemeColor("#FCE8E6", "#4B2528"), TextColor = ThemeColor("#B42318", "#FFB4AB") };
            removeButton.Clicked += async (_, _) => await RemoveItemAsync(item);
            Grid.SetColumn(removeButton, 2);
            row.Children.Add(removeButton);
            _itemsLayout.Children.Add(row);
        }

        if (_customItems.Count == 0)
            _itemsLayout.Children.Add(new Label { Text = "No custom entries in this catalog yet.", TextColor = ThemeColor("#667085", "#B0BAC7") });
    }

    private void ShowEditor(JsonObject? item)
    {
        _editingItem = item;
        _fieldControls.Clear();
        _formLayout.Children.Clear();
        _formLayout.Children.Add(new Label
        {
            Text = item is null ? "Create custom item" : $"Edit {GetName(item)}",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = ThemeColor("#202A36", "#F2F4F7")
        });

        foreach (var field in _schema)
        {
            var label = new Label { Text = field.Name, FontAttributes = FontAttributes.Bold, TextColor = ThemeColor("#344054", "#E0E5EB") };
            View input = CreateFieldControl(field, item?[field.Name]);
            _fieldControls[field.Name] = input;
            _formLayout.Children.Add(label);
            _formLayout.Children.Add(input);
        }

        var actions = new HorizontalStackLayout { Spacing = 8 };
        var saveButton = new Button { Text = "Save", BackgroundColor = ThemeColor("#247A43", "#1F3D2B"), TextColor = Colors.White };
        saveButton.Clicked += async (_, _) => await SaveItemAsync();
        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Clicked += (_, _) => _formLayout.IsVisible = false;
        actions.Children.Add(saveButton);
        actions.Children.Add(cancelButton);
        _formLayout.Children.Add(actions);
        _formLayout.IsVisible = true;
    }

    private static View CreateFieldControl(CatalogFieldSchema field, JsonNode? value)
    {
        if (field.Kind == JsonValueKind.True || field.Kind == JsonValueKind.False)
            return new Switch { IsToggled = value?.GetValue<bool>() ?? false };

        var text = value?.ToString() ?? string.Empty;
        if (field.Kind == JsonValueKind.Number)
            return new Entry { Text = text, Keyboard = Keyboard.Numeric, Placeholder = field.Name };
        if (string.Equals(field.Name, "description", StringComparison.OrdinalIgnoreCase))
            return new Editor { Text = text, AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 80, Placeholder = field.Name };
        if (field.Kind == JsonValueKind.String)
            return new Entry { Text = text, Placeholder = field.Name };
        return new Editor { Text = value?.ToJsonString() ?? "null", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 80, Placeholder = field.Name };
    }

    private async Task SaveItemAsync()
    {
        var item = new JsonObject();
        try
        {
            foreach (var field in _schema)
            {
                var value = _fieldControls[field.Name] switch
                {
                    Switch toggle => JsonValue.Create(toggle.IsToggled),
                    Entry entry => CreateValue(field, entry.Text),
                    Editor editor => CreateValue(field, editor.Text),
                    _ => null
                };
                item[field.Name] = value;
            }
        }
        catch (Exception exception)
        {
            await DisplayAlert("Invalid value", exception.Message, "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(GetName(item)))
        {
            await DisplayAlert("Name required", "Enter a name for this custom item.", "OK");
            return;
        }

        var items = _customItems.OfType<JsonObject>().Select(existing => ReferenceEquals(existing, _editingItem) ? item : (JsonObject)existing.DeepClone()).ToList();
        if (_editingItem is null)
            items.Add(item);
        try
        {
            await _catalogService.SaveCustomItemsAsync(SelectedKind, items);
            _customItems = new JsonArray(items.Select(existing => existing.DeepClone()).ToArray());
            _formLayout.IsVisible = false;
            _statusLabel.Text = "Custom catalog saved";
            _statusLabel.TextColor = ThemeColor("#247A43", "#86D7A0");
            RefreshItems();
            await _catalogChanged();
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"Could not save catalog: {exception.Message}";
            _statusLabel.TextColor = ThemeColor("#B42318", "#FFB4AB");
        }
    }

    private static JsonNode? CreateValue(CatalogFieldSchema field, string? text)
    {
        text ??= string.Empty;
        return field.Kind switch
        {
            JsonValueKind.String => JsonValue.Create(text),
            JsonValueKind.Number when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) => JsonValue.Create(integer),
            JsonValueKind.Number when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => JsonValue.Create(number),
            JsonValueKind.Number => throw new FormatException($"{field.Name} must be a number."),
            JsonValueKind.Null when string.IsNullOrWhiteSpace(text) => null,
            JsonValueKind.Null => JsonNode.Parse(text),
            JsonValueKind.Object or JsonValueKind.Array => JsonNode.Parse(text),
            _ => JsonValue.Create(text)
        };
    }

    private async Task RemoveItemAsync(JsonObject item)
    {
        var name = GetName(item);
        if (!await DisplayAlert("Remove custom item", $"Remove {name} from this custom catalog?", "Remove", "Cancel"))
            return;

        var items = _customItems.OfType<JsonObject>()
            .Where(existing => !ReferenceEquals(existing, item))
            .Select(existing => (JsonObject)existing.DeepClone())
            .ToList();
        try
        {
            await _catalogService.SaveCustomItemsAsync(SelectedKind, items);
            _customItems = new JsonArray(items.Select(existing => existing.DeepClone()).ToArray());
            RefreshItems();
            _statusLabel.Text = "Custom item removed";
            _statusLabel.TextColor = ThemeColor("#247A43", "#86D7A0");
            await _catalogChanged();
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"Could not save catalog: {exception.Message}";
            _statusLabel.TextColor = ThemeColor("#B42318", "#FFB4AB");
        }
    }

    private string GetName(JsonObject item) => item.FirstOrDefault(property => string.Equals(property.Key, "name", StringComparison.OrdinalIgnoreCase)).Value?.ToString() ?? string.Empty;

    private static Color ThemeColor(string light, string dark)
    {
        var application = Application.Current;
        var theme = application?.UserAppTheme is { } userTheme && userTheme != AppTheme.Unspecified
            ? userTheme
            : application?.RequestedTheme ?? AppTheme.Light;
        return Color.FromArgb(theme == AppTheme.Dark ? dark : light);
    }

    private static string GetKindLabel(CatalogKind kind) => kind switch
    {
        CatalogKind.Equipment => "Equipment",
        CatalogKind.Classes => "Classes",
        CatalogKind.Spells => "Spells / Abilities",
        _ => kind.ToString()
    };
}
