using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Maui.Controls.Shapes;
using HQ_Character_Tracker.Models;
using HQ_Character_Tracker.Services;

namespace HQ_Character_Tracker;

public partial class CharacterSheetPage : ContentPage
{
    private const int MaximumCharacterCount = 4;
    private const double CharacterCardWidth = 760;
    private const double CharacterCardSpacing = 14;
    private readonly CharacterDataService _dataService = new();
    private readonly List<CharacterSheet> _characters = [new()];
    private int _currentCarouselIndex;
    private CharacterClassCatalog _classCatalog = new();
    private List<string> _equipmentOptions = [];
    private List<string> _spellOptions = [];
    private Dictionary<string, string> _equipmentDescriptions = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _spellDescriptions = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDescriptionModalOpen;
#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _keyboardAcceleratorRoot;
#endif

    public CharacterSheetPage()
    {
        InitializeComponent();
        if (FindByName("SaveRosterButton") is Button saveRosterButton)
        {
            saveRosterButton.Clicked += OnSaveRosterClicked;
            ApplyButtonHoverFeedback(saveRosterButton);
        }
        if (FindByName("LoadRosterButton") is Button loadRosterButton)
            ApplyButtonHoverFeedback(loadRosterButton);
        if (FindByName("ThemePicker") is Picker themePicker)
        {
            themePicker.ItemsSource = new[] { "Light", "Dark", "System" };
            themePicker.SelectedItem = Preferences.Get("AppTheme", "Dark");
            themePicker.SelectedIndexChanged += (_, _) =>
            {
                if (themePicker.SelectedItem is not string theme)
                    return;

                Preferences.Set("AppTheme", theme);
                Application.Current!.UserAppTheme = theme switch
                {
                    "Light" => AppTheme.Light,
                    "System" => AppTheme.Unspecified,
                    _ => AppTheme.Dark
                };
                RebuildCharacterCards();
            };
        }
        Application.Current!.RequestedThemeChanged += (_, _) => RebuildCharacterCards();
        PreviousButton.Clicked += OnPreviousCardClicked;
        NextButton.Clicked += OnNextCardClicked;
        ApplyButtonHoverFeedback(PreviousButton);
        ApplyButtonHoverFeedback(NextButton);
#if WINDOWS
        HandlerChanged += (_, _) => AttachCarouselKeyboardAccelerators();
        AttachCarouselKeyboardAccelerators();
#endif
        InitializeAsync();
    }

#if WINDOWS
    private void AttachCarouselKeyboardAccelerators()
    {
        if (Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement root || ReferenceEquals(root, _keyboardAcceleratorRoot))
            return;

        if (_keyboardAcceleratorRoot is not null)
            _keyboardAcceleratorRoot.KeyboardAccelerators.Clear();

        _keyboardAcceleratorRoot = root;
        var previousAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.Left
        };
        previousAccelerator.Invoked += (_, args) =>
        {
            args.Handled = true;
            MainThread.BeginInvokeOnMainThread(() => OnPreviousCardClicked(this, EventArgs.Empty));
        };

        var nextAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.Right
        };
        nextAccelerator.Invoked += (_, args) =>
        {
            args.Handled = true;
            MainThread.BeginInvokeOnMainThread(() => OnNextCardClicked(this, EventArgs.Empty));
        };

        root.KeyboardAccelerators.Add(previousAccelerator);
        root.KeyboardAccelerators.Add(nextAccelerator);
    }
#endif

    private static Color ThemeColor(string light, string dark)
    {
        var application = Application.Current;
        var theme = application?.UserAppTheme is { } userTheme && userTheme != AppTheme.Unspecified
            ? userTheme
            : application?.RequestedTheme ?? AppTheme.Light;
        return Color.FromArgb(theme == AppTheme.Dark ? dark : light);
    }

    private async void InitializeAsync()
    {
        try
        {
            _classCatalog = await _dataService.LoadClassesAsync();
            var equipment = (await _dataService.LoadEquipmentAsync()).Equipment;
            _equipmentOptions = equipment.Select(item => item.Name).ToList();
            _equipmentDescriptions = equipment.ToDictionary(item => item.Name, item => item.Description, StringComparer.OrdinalIgnoreCase);
            var spells = (await _dataService.LoadSpellsAsync()).Spells;
            _spellOptions = spells.Select(item => item.Name).ToList();
            _spellDescriptions = spells.ToDictionary(item => item.Name, item => item.Description, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            StatusLabel.Text = $"Could not load settings: {exception.Message}";
        }

        RebuildCharacterCards();
    }

    private void RebuildCharacterCards()
    {
        CharacterCards.Children.Clear();
        for (var index = 0; index < _characters.Count; index++)
        {
            var character = _characters[index];
            CharacterCards.Children.Add(BuildCharacterCard(character, index));
        }

        var addButton = new Button
        {
            Text = "+",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            WidthRequest = 54,
            HeightRequest = 54,
            Padding = 0,
            BackgroundColor = ThemeColor("#9A6B2F", "#9A6B2F"),
            TextColor = ThemeColor("#FFFFFF", "#FFFFFF"),
            VerticalOptions = LayoutOptions.Center,
            IsEnabled = _characters.Count < MaximumCharacterCount
        };
        ApplyButtonHoverFeedback(addButton);
        SemanticProperties.SetHint(addButton, _characters.Count < MaximumCharacterCount
            ? "Add a character sheet after the last card"
            : $"Maximum of {MaximumCharacterCount} character sheets reached");
        addButton.Clicked += OnAddCharacterClicked;
        CharacterCards.Children.Add(addButton);
        UpdateCarouselNavigation();
    }

    private void OnCarouselScrolled(object? sender, ScrolledEventArgs e) => UpdateCarouselNavigation();

    private void UpdateCarouselNavigation()
    {
        if (PreviousButton is null || NextButton is null || CarouselPositionLabel is null)
            return;

        var viewportCenter = SheetScroll.ScrollX + SheetScroll.Width / 2;
        var closestIndex = 0;
        var closestDistance = double.MaxValue;
        var cardStart = CharacterCards.Padding.Left;
        for (var index = 0; index < _characters.Count; index++)
        {
            var card = CharacterCards.Children[index];
            var cardWidth = card.Width > 0 ? card.Width : CharacterCardWidth;
            var cardCenter = cardStart + cardWidth / 2;
            var distance = Math.Abs(viewportCenter - cardCenter);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = index;
            }
            cardStart += cardWidth + CharacterCardSpacing;
        }

        _currentCarouselIndex = closestIndex;
        PreviousButton.IsEnabled = _currentCarouselIndex > 0;
        NextButton.IsEnabled = _currentCarouselIndex < _characters.Count - 1;
        CarouselPositionLabel.Text = $"{_currentCarouselIndex + 1} of {_characters.Count}";
    }

    private async void OnPreviousCardClicked(object? sender, EventArgs e)
    {
        await ScrollToCharacterAsync(Math.Max(0, _currentCarouselIndex - 1));
    }

    private async void OnNextCardClicked(object? sender, EventArgs e)
    {
        await ScrollToCharacterAsync(Math.Min(_currentCarouselIndex + 1, _characters.Count - 1));
    }

    private async Task ScrollToCharacterAsync(int index)
    {
        if (index < 0 || index >= _characters.Count || index >= CharacterCards.Children.Count)
            return;

        var offset = CharacterCards.Padding.Left;
        for (var cardIndex = 0; cardIndex < index; cardIndex++)
        {
            var card = CharacterCards.Children[cardIndex];
            offset += (card.Width > 0 ? card.Width : CharacterCardWidth) + CharacterCardSpacing;
        }

        _currentCarouselIndex = index;
        await SheetScroll.ScrollToAsync(offset, 0, true);
        UpdateCarouselNavigation();
    }

    private View BuildCharacterCard(CharacterSheet character, int index)
    {
        var cardContent = new VerticalStackLayout { Spacing = 16 };
        var cardHeader = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        cardHeader.Children.Add(new Label
        {
            Text = $"CHARACTER {index + 1}",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.5,
            TextColor = ThemeColor("#9A6B2F", "#D4B06B"),
            VerticalTextAlignment = TextAlignment.Center
        });

        var actions = new HorizontalStackLayout { Spacing = 8 };
        //var exportButton = CreateActionButton("Export", "#34465A", Colors.White);
        //exportButton.Clicked += async (_, _) => await ExportCharacterAsync(character);
        var removeButton = CreateActionButton("Remove", ThemeColor("#FCE8E6", "#4B2528"), ThemeColor("#B42318", "#FFB4AB"));
        removeButton.Clicked += async (_, _) => await RemoveCharacterAsync(character);
        SemanticProperties.SetHint(removeButton, $"Remove {GetCharacterTitle(character, index)}");
        //actions.Children.Add(exportButton);
        actions.Children.Add(removeButton);
        Grid.SetColumn(actions, 1);
        cardHeader.Children.Add(actions);
        cardContent.Children.Add(cardHeader);

        var identityGrid = CreateThreeColumnGrid();
        var nameEntry = CreateTextEntry(character.Name, "Enter a hero name", Keyboard.Text, text => character.Name = text ?? string.Empty);
        AddToGrid(identityGrid, BuildField("Character name", nameEntry), 0, 0);
        var classPicker = new Picker
        {
            ItemsSource = _classCatalog.Classes.Select(item => item.Name).ToList(),
            SelectedItem = character.CharacterClass,
            //Title = "Choose a class",
            TitleColor = ThemeColor("#667085", "#B0BAC7"),
            TextColor = ThemeColor("#202A36", "#F2F4F7"),
            BackgroundColor = ThemeColor("#F7F8FA", "#263445")
        };
        var goldEntry = CreateNumericEntry(character.GoldCoins, "Gold coins", value => character.GoldCoins = value);
        character.BodyPoints = Math.Max(0, character.BodyPoints);
        character.MaximumBodyPoints = Math.Max(character.BodyPoints, character.MaximumBodyPoints);
        character.MindPoints = Math.Max(0, character.MindPoints);
        character.MaximumMindPoints = Math.Max(character.MindPoints, character.MaximumMindPoints);
        character.AttackDice = Math.Max(0, character.AttackDice);
        character.RangedAttackDice = Math.Max(0, character.RangedAttackDice);
        character.DefenceDice = Math.Max(0, character.DefenceDice);
        var bodyPointsValue = CreatePointValueLabel(character.BodyPoints);
        var maximumBodyPointsValue = CreatePointValueLabel(character.MaximumBodyPoints);
        var mindPointsValue = CreatePointValueLabel(character.MindPoints);
        var maximumMindPointsValue = CreatePointValueLabel(character.MaximumMindPoints);
        var meleeDiceValue = CreatePointValueLabel(character.AttackDice);
        var rangedDiceValue = CreatePointValueLabel(character.RangedAttackDice);
        var defenceDiceValue = CreatePointValueLabel(character.DefenceDice);
        classPicker.SelectedIndexChanged += (_, _) =>
        {
            if (classPicker.SelectedItem is not string selectedClass)
                return;

            var definition = _classCatalog.Classes.FirstOrDefault(item => item.Name == selectedClass);
            if (definition is null)
                return;

            character.CharacterClass = definition.Name;
            character.BodyPoints = character.MaximumBodyPoints = definition.BodyPoints;
            character.MindPoints = character.MaximumMindPoints = definition.MindPoints;
            character.AttackDice = definition.AttackDice;
            character.DefenceDice = definition.DefenceDice;
            character.RangedAttackDice = 0;
            bodyPointsValue.Text = definition.BodyPoints.ToString();
            maximumBodyPointsValue.Text = definition.BodyPoints.ToString();
            mindPointsValue.Text = definition.MindPoints.ToString();
            maximumMindPointsValue.Text = definition.MindPoints.ToString();
            meleeDiceValue.Text = definition.AttackDice.ToString();
            rangedDiceValue.Text = character.RangedAttackDice.ToString();
            defenceDiceValue.Text = definition.DefenceDice.ToString();
        };
        AddToGrid(identityGrid, BuildField("Character class", classPicker), 1, 0);
        AddToGrid(identityGrid, BuildField("Gold coins", goldEntry), 2, 0);
        cardContent.Children.Add(identityGrid);

        var combatSection = CreateSection("COMBAT STATS");
        var combatGrid = CreateTwoColumnGrid();
        AddToGrid(combatGrid, BuildPointField(
            "❤️ BODY POINTS",
            bodyPointsValue,
            maximumBodyPointsValue,
            () => character.BodyPoints,
            value => character.BodyPoints = value,
            () => character.MaximumBodyPoints,
            value => character.MaximumBodyPoints = value,
            true), 0, 0);
        AddToGrid(combatGrid, BuildPointField(
            "🧠 MIND POINTS",
            mindPointsValue,
            maximumMindPointsValue,
            () => character.MindPoints,
            value => character.MindPoints = value,
            () => character.MaximumMindPoints,
            value => character.MaximumMindPoints = value,
            true), 1, 0);
        combatSection.Children.Add(combatGrid);
        combatSection.Children.Add(BuildCombatDiceField(character, meleeDiceValue, rangedDiceValue, defenceDiceValue));
        cardContent.Children.Add(WrapSection(combatSection));

        var equipmentItems = new VerticalStackLayout { Spacing = 4 };
        const string equipmentPlaceholder = "Tap + to add equipment";
        var equipmentSection = CreateSection("EQUIPMENT");
        RefreshItemList(equipmentItems, character.Equipment, equipmentPlaceholder, descriptions: _equipmentDescriptions);
        equipmentSection.Children.Add(BuildSelectionListRow(
            equipmentItems,
            equipmentPlaceholder,
            "Choose equipment",
            _equipmentOptions,
            character.Equipment,
            descriptions: _equipmentDescriptions));

        var spellItems = new VerticalStackLayout { Spacing = 4 };
        const string spellPlaceholder = "Tap + to add a spell";
        var resetSpellsButton = CreateActionButton("Reset to Ready", ThemeColor("#E6E9EE", "#34465A"), ThemeColor("#263445", "#F2F4F7"));
        resetSpellsButton.FontSize = 12;
        resetSpellsButton.Padding = new Thickness(8, 4);
        resetSpellsButton.Clicked += (_, _) =>
        {
            character.CastSpells.Clear();
            RefreshItemList(spellItems, character.Spells, spellPlaceholder, character.CastSpells, _spellDescriptions);
        };
        var spellSection = CreateSection("SPELLS & ABILITIES", resetSpellsButton);
        RefreshItemList(spellItems, character.Spells, spellPlaceholder, character.CastSpells, _spellDescriptions);
        spellSection.Children.Add(BuildSelectionListRow(
            spellItems,
            spellPlaceholder,
            "Choose a spell",
            _spellOptions,
            character.Spells,
            character.CastSpells,
            _spellDescriptions));

        var belongingsSections = new VerticalStackLayout { Spacing = 12 };
        belongingsSections.Children.Add(WrapSection(equipmentSection));
        belongingsSections.Children.Add(WrapSection(spellSection));
        cardContent.Children.Add(belongingsSections);

        var notesEditor = new Editor
        {
            Text = character.Notes,
            Placeholder = "Quest notes, treasures, conditions...",
            TextColor = ThemeColor("#202A36", "#F2F4F7"),
            PlaceholderColor = ThemeColor("#667085", "#B0BAC7"),
            BackgroundColor = ThemeColor("#F7F8FA", "#263445"),
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 100
        };
        notesEditor.TextChanged += (_, args) => character.Notes = args.NewTextValue ?? string.Empty;
        var notesSection = CreateSection("NOTES");
        notesSection.Children.Add(WrapInput(notesEditor));
        cardContent.Children.Add(WrapSection(notesSection));

        return new Border
        {
            Content = cardContent,
            Padding = 20,
            BackgroundColor = ThemeColor("#FFFFFF", "#1B2633"),
            Stroke = ThemeColor("#D0D5DD", "#3B4858"),
            StrokeThickness = 1.5,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            HorizontalOptions = LayoutOptions.Fill,
            WidthRequest = CharacterCardWidth
        };
    }

    private static Grid CreateTwoColumnGrid() => new()
    {
        ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
        RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
        ColumnSpacing = 14,
        RowSpacing = 12
    };

    private static Grid CreateThreeColumnGrid() => new()
    {
        ColumnDefinitions =
        {
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Star)
        },
        RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
        ColumnSpacing = 14,
        RowSpacing = 12
    };

    private static void AddToGrid(Grid grid, View view, int column, int row)
    {
        Grid.SetColumn(view, column);
        Grid.SetRow(view, row);
        grid.Children.Add(view);
    }

    private static VerticalStackLayout BuildField(string label, View control, bool isBold = false) => new()
    {
        Spacing = 5,
        Children =
        {
            new Label { Text = label, FontSize = 13, TextColor = ThemeColor("#475467", "#C2CAD4"), FontAttributes = FontAttributes.Bold },
            WrapInput(control)
        }
    };

    private static Border WrapInput(View control)
    {
        var border = new Border
        {
            Content = control,
            Padding = 1,
            BackgroundColor = ThemeColor("#F7F8FA", "#263445"),
            Stroke = ThemeColor("#D0D5DD", "#3B4858"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 }
        };
        Action<bool> updateHighlight = isHovered =>
        {
            border.Stroke = isHovered ? Color.FromArgb("#C49A42") : ThemeColor("#D0D5DD", "#3B4858");
        };
        if (control is Picker or Entry or Editor)
            AddPointerHoverFeedback(control, updateHighlight, control is Picker);
        else if (control is Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is Entry childEntry)
                    AddPointerHoverFeedback(childEntry, updateHighlight, false);
            }
        }
        return border;
    }

    private static Label CreatePointValueLabel(int value) => new()
    {
        Text = value.ToString(),
        FontSize = 18,
        FontAttributes = FontAttributes.Bold,
        TextColor = ThemeColor("#202A36", "#F2F4F7"),
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };

    private static VerticalStackLayout BuildPointField(
        string label,
        Label currentValue,
        Label maximumValue,
        Func<int> getCurrent,
        Action<int> setCurrent,
        Func<int> getMaximum,
        Action<int> setMaximum,
        bool isBold)
    {
        var values = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var decrementCurrentButton = CreatePointButton("−", $"Decrease current {label.ToLowerInvariant()}");
        var incrementCurrentButton = CreatePointButton("+", $"Increase current {label.ToLowerInvariant()}");
        var decrementMaximumButton = CreatePointButton("−", $"Decrease maximum {label.ToLowerInvariant()}");
        var incrementMaximumButton = CreatePointButton("+", $"Increase maximum {label.ToLowerInvariant()}");
        decrementCurrentButton.Clicked += (_, _) => SetCurrentValue((int)Math.Max(0L, (long)getCurrent() - 1));
        incrementCurrentButton.Clicked += (_, _) => SetCurrentValue((int)Math.Min(int.MaxValue, (long)getCurrent() + 1));
        decrementMaximumButton.Clicked += (_, _) => SetMaximumValue((int)Math.Max(0L, (long)getMaximum() - 1));
        incrementMaximumButton.Clicked += (_, _) => SetMaximumValue((int)Math.Min(int.MaxValue, (long)getMaximum() + 1));

        values.Children.Add(decrementCurrentButton);
        Grid.SetColumn(currentValue, 1);
        values.Children.Add(currentValue);
        Grid.SetColumn(incrementCurrentButton, 2);
        values.Children.Add(incrementCurrentButton);
        var slash = new Label { Text = "/", TextColor = ThemeColor("#667085", "#B0BAC7"), VerticalTextAlignment = TextAlignment.Center };
        Grid.SetColumn(slash, 3);
        values.Children.Add(slash);
        Grid.SetColumn(decrementMaximumButton, 4);
        values.Children.Add(decrementMaximumButton);
        Grid.SetColumn(maximumValue, 5);
        values.Children.Add(maximumValue);
        Grid.SetColumn(incrementMaximumButton, 6);
        values.Children.Add(incrementMaximumButton);

        void SetCurrentValue(int value)
        {
            var maximum = Math.Max(0, getMaximum());
            var boundedValue = Math.Clamp((long)value, 0, maximum);
            setCurrent((int)boundedValue);
            currentValue.Text = boundedValue.ToString();
            UpdateButtonStates();
        }

        void UpdateButtonStates()
        {
            var current = Math.Max(0, getCurrent());
            var maximum = Math.Max(current, getMaximum());
            decrementCurrentButton.IsEnabled = current > 0;
            incrementCurrentButton.IsEnabled = current < maximum;
            decrementMaximumButton.IsEnabled = maximum > current;
            incrementMaximumButton.IsEnabled = maximum < int.MaxValue;
        }

        void SetMaximumValue(int value)
        {
            var boundedValue = Math.Clamp((long)value, getCurrent(), int.MaxValue);
            setMaximum((int)boundedValue);
            maximumValue.Text = boundedValue.ToString();
            UpdateButtonStates();
        }

        UpdateButtonStates();
        return BuildField(label, values, isBold);
    }

    private static Grid BuildCombatDiceField(CharacterSheet character, Label meleeValue, Label rangedValue, Label defenceValue)
    {
        var combatDiceGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 14
        };
        combatDiceGrid.Children.Add(BuildDiceField(
            "⚔ MELEE",
            meleeValue,
            () => character.AttackDice,
            value => character.AttackDice = value));

        AddCombatDiceDivider(combatDiceGrid, 1);
        var rangedField = BuildDiceField(
            "🏹 RANGED",
            rangedValue,
            () => character.RangedAttackDice,
            value => character.RangedAttackDice = value);
        Grid.SetColumn(rangedField, 2);
        combatDiceGrid.Children.Add(rangedField);

        AddCombatDiceDivider(combatDiceGrid, 3);
        var defenceField = BuildDiceField(
            "🛡️ DEFENCE",
            defenceValue,
            () => character.DefenceDice,
            value => character.DefenceDice = value);
        Grid.SetColumn(defenceField, 4);
        combatDiceGrid.Children.Add(defenceField);
        return combatDiceGrid;
    }

    private static void AddCombatDiceDivider(Grid grid, int column)
    {
        var divider = new BoxView
        {
            WidthRequest = 1,
            HeightRequest = 52,
            Color = ThemeColor("#D8D1C4", "#4A5665"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(divider, column);
        grid.Children.Add(divider);
    }

    private static VerticalStackLayout BuildDiceField(string label, Label valueLabel, Func<int> getValue, Action<int> setValue)
    {
        var decrementButton = CreatePointButton("−", $"Decrease {label.ToLowerInvariant()}");
        var incrementButton = CreatePointButton("+", $"Increase {label.ToLowerInvariant()}");
        var values = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        decrementButton.Clicked += (_, _) => SetValue(Math.Max(0, getValue() - 1));
        incrementButton.Clicked += (_, _) => SetValue(getValue() < int.MaxValue ? getValue() + 1 : int.MaxValue);
        values.Children.Add(decrementButton);
        Grid.SetColumn(valueLabel, 1);
        values.Children.Add(valueLabel);
        Grid.SetColumn(incrementButton, 2);
        values.Children.Add(incrementButton);

        var heading = CreateSubheading(label);
        var field = new VerticalStackLayout { Spacing = 5, Children = { heading, values } };

        void SetValue(int value)
        {
            var boundedValue = Math.Clamp((long)value, 0, int.MaxValue);
            setValue((int)boundedValue);
            valueLabel.Text = boundedValue.ToString();
            decrementButton.IsEnabled = boundedValue > 0;
            incrementButton.IsEnabled = boundedValue < int.MaxValue;
        }

        decrementButton.IsEnabled = getValue() > 0;
        incrementButton.IsEnabled = getValue() < int.MaxValue;
        return field;
    }

    private static Button CreatePointButton(string text, string hint)
    {
        var button = CreateActionButton(text, ThemeColor("#E6E9EE", "#34465A"), ThemeColor("#263445", "#F2F4F7"));
        button.WidthRequest = 40;
        button.HeightRequest = 40;
        button.Padding = 0;
        button.FontSize = 20;
        SemanticProperties.SetHint(button, hint);
        return button;
    }

    private static VerticalStackLayout CreateSection(string title, View? action = null)
    {
        var stack = new VerticalStackLayout { Spacing = 10 };
        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        header.Children.Add(CreateSubheading(title));
        if (action is not null)
        {
            Grid.SetColumn(action, 1);
            header.Children.Add(action);
        }
        stack.Children.Add(header);
        return stack;
    }

    private static Label CreateSubheading(string title) => new()
    {
        Text = title,
        FontSize = 12,
        FontAttributes = FontAttributes.Bold,
        CharacterSpacing = 0.8,
        TextColor = ThemeColor("#475467", "#C2CAD4"),
        VerticalTextAlignment = TextAlignment.Center
    };

    private static Border WrapSection(VerticalStackLayout content)
    {
        return new Border
        {
            Content = content,
            Padding = 14,
            BackgroundColor = ThemeColor("#FAFAFB", "#202B38"),
            Stroke = ThemeColor("#E2E5E9", "#3B4858"),
            StrokeShape = new RoundRectangle { CornerRadius = 10 }
        };
    }

    private static Entry CreateTextEntry(string? text, string placeholder, Keyboard keyboard, Action<string?> onChanged)
    {
        var entry = new Entry
        {
            Text = text,
            Placeholder = placeholder,
            Keyboard = keyboard,
            TextColor = ThemeColor("#202A36", "#F2F4F7"),
            PlaceholderColor = ThemeColor("#667085", "#B0BAC7"),
            BackgroundColor = ThemeColor("#F7F8FA", "#263445")
        };
        entry.TextChanged += (_, args) => onChanged(args.NewTextValue);
        return entry;
    }

    private static Entry CreateNumericEntry(int value, string placeholder, Action<int> onChanged)
    {
        var entry = CreateTextEntry(value.ToString(), placeholder, Keyboard.Numeric, text => onChanged(int.TryParse(text, out var number) ? number : 0));
        entry.HorizontalTextAlignment = TextAlignment.Center;
        return entry;
    }

    private Grid BuildSelectionListRow(
        VerticalStackLayout itemHost,
        string placeholder,
        string selectionTitle,
        List<string> options,
        List<string> selectedItems,
        List<string>? castItems = null,
        IReadOnlyDictionary<string, string>? descriptions = null)
    {
        var row = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8
        };
        row.Children.Add(itemHost);
        var addButton = new Button
        {
            Text = "+",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            WidthRequest = 44,
            HeightRequest = 44,
            Padding = 0,
            BackgroundColor = ThemeColor("#34465A", "#34465A"),
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Start
        };
        SemanticProperties.SetHint(addButton, $"{selectionTitle} to add to the list");
        ApplyButtonHoverFeedback(addButton);
        addButton.Clicked += async (_, _) => await SelectCatalogItemAsync(selectionTitle, options, selectedItems, itemHost, placeholder, castItems, descriptions);
        Grid.SetColumn(addButton, 1);
        row.Children.Add(addButton);
        return row;
    }

    private async Task SelectCatalogItemAsync(
        string title,
        List<string> options,
        List<string> selectedItems,
        VerticalStackLayout itemHost,
        string placeholder,
        List<string>? castItems,
        IReadOnlyDictionary<string, string>? descriptions)
    {
        var availableOptions = options
            .Where(option => !selectedItems.Contains(option, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (availableOptions.Length == 0)
        {
            await DisplayAlert("No items available", "There are no more items to add from the current settings.", "OK");
            return;
        }

        var selectedItem = await DisplayActionSheet(title, "Cancel", null, availableOptions);
        if (string.IsNullOrWhiteSpace(selectedItem) || selectedItem == "Cancel")
            return;

        selectedItems.Add(selectedItem);
        RefreshItemList(itemHost, selectedItems, placeholder, castItems, descriptions);
    }

    private void RefreshItemList(
        VerticalStackLayout host,
        List<string> items,
        string placeholder,
        List<string>? castItems = null,
        IReadOnlyDictionary<string, string>? descriptions = null)
    {
        host.Children.Clear();
        if (items.Count == 0)
        {
            host.Children.Add(new Label
            {
                Text = placeholder,
                FontSize = 13,
                FontAttributes = FontAttributes.Italic,
                TextColor = ThemeColor("#667085", "#B0BAC7"),
                InputTransparent = true,
                VerticalTextAlignment = TextAlignment.Center,
                MinimumHeightRequest = 44
            });
            return;
        }

        foreach (var item in items)
        {
            var itemName = new Label
            {
                Text = item,
                TextColor = ThemeColor("#344054", "#E0E5EB"),
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.WordWrap
            };
            var descriptionText = descriptions is not null && descriptions.TryGetValue(item, out var description) && !string.IsNullOrWhiteSpace(description)
                ? description
                : "No description available.";
            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 8
            };
            row.Children.Add(itemName);

            var infoButton = CreateActionButton("ⓘ", ThemeColor("#E6E9EE", "#34465A"), ThemeColor("#263445", "#F2F4F7"));
            infoButton.FontSize = 16;
            infoButton.Padding = new Thickness(8, 2);
            SemanticProperties.SetHint(infoButton, $"Show description for {item}");
            infoButton.Clicked += async (_, _) => await ShowItemDescriptionAsync(item, descriptionText);
            Grid.SetColumn(infoButton, 1);
            row.Children.Add(infoButton);

            if (castItems is not null)
            {
                var isCast = castItems.Contains(item, StringComparer.OrdinalIgnoreCase);
                var castButton = CreateActionButton(isCast ? "Cast" : "Ready",
                    isCast ? ThemeColor("#FCE8E6", "#4B2528") : ThemeColor("#E8F3E8", "#1F3D2B"),
                    isCast ? ThemeColor("#B42318", "#FFB4AB") : ThemeColor("#247A43", "#86D7A0"));
                castButton.FontSize = 12;
                castButton.Padding = new Thickness(8, 2);
                SemanticProperties.SetHint(castButton, $"Mark {item} as {(isCast ? "ready" : "cast")}");
                castButton.Clicked += (_, _) =>
                {
                    if (castItems.Contains(item, StringComparer.OrdinalIgnoreCase))
                        castItems.RemoveAll(castItem => string.Equals(castItem, item, StringComparison.OrdinalIgnoreCase));
                    else
                        castItems.Add(item);
                    RefreshItemList(host, items, placeholder, castItems, descriptions);
                };
                Grid.SetColumn(castButton, 2);
                row.Children.Add(castButton);
            }
            var removeButton = CreateActionButton("Remove", ThemeColor("#F3F4F6", "#34404D"), ThemeColor("#475467", "#D0D7E0"));
            removeButton.FontSize = 12;
            removeButton.Padding = new Thickness(8, 2);
            removeButton.Clicked += (_, _) =>
            {
                items.Remove(item);
                castItems?.RemoveAll(castItem => string.Equals(castItem, item, StringComparison.OrdinalIgnoreCase));
                RefreshItemList(host, items, placeholder, castItems, descriptions);
            };
            Grid.SetColumn(removeButton, castItems is null ? 2 : 3);
            row.Children.Add(removeButton);
            host.Children.Add(row);
        }
    }

    private async Task ShowItemDescriptionAsync(string itemName, string description)
    {
        if (_isDescriptionModalOpen)
            return;

        _isDescriptionModalOpen = true;
        var closeButton = CreateActionButton("Close", ThemeColor("#34465A", "#34465A"), Colors.White);
        closeButton.Clicked += async (_, _) =>
        {
            await Navigation.PopModalAsync();
            _isDescriptionModalOpen = false;
        };

        var modalContent = new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                new Label
                {
                    Text = itemName,
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = ThemeColor("#202A36", "#F2F4F7")
                },
                new ScrollView
                {
                    Content = new Label
                    {
                        Text = description,
                        FontSize = 15,
                        TextColor = ThemeColor("#344054", "#E0E5EB")
                    },
                    HeightRequest = 300
                },
                closeButton
            }
        };

        var popupPage = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#99000000"),
            Content = new Grid
            {
                Padding = 24,
                Children =
                {
                    new Border
                    {
                        Content = modalContent,
                        Padding = 20,
                        WidthRequest = Math.Min(560, DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density - 48),
                        BackgroundColor = ThemeColor("#FFFFFF", "#1B2633"),
                        Stroke = ThemeColor("#D0D5DD", "#3B4858"),
                        StrokeShape = new RoundRectangle { CornerRadius = 16 },
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center
                    }
                }
            }
        };

        try
        {
            await Navigation.PushModalAsync(popupPage);
        }
        catch
        {
            _isDescriptionModalOpen = false;
            throw;
        }
    }

    private static Button CreateActionButton(string text, string background, Color foreground)
        => CreateActionButton(text, Color.FromArgb(background), foreground);

    private static Button CreateActionButton(string text, Color background, Color foreground)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = background,
            TextColor = foreground,
            Padding = new Thickness(12, 8),
            FontSize = 13
        };
        ApplyButtonHoverFeedback(button);
        return button;
    }

    private static void ApplyButtonHoverFeedback(Button button)
    {
        var originalBorderColor = button.BorderColor;
        var originalBorderWidth = button.BorderWidth;
        AddPointerHoverFeedback(button, isHovered =>
        {
            button.BorderColor = isHovered ? Color.FromArgb("#C49A42") : originalBorderColor;
            button.BorderWidth = isHovered ? 1 : originalBorderWidth;
        }, true);
    }

    private static void AddPointerHoverFeedback(View element, Action<bool> onHover, bool handCursor)
    {
        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerEntered += (_, _) =>
        {
            onHover(true);
            SetNativePointerCursor(handCursor);
        };
        pointerGesture.PointerMoved += (_, _) => SetNativePointerCursor(handCursor);
        pointerGesture.PointerExited += (_, _) =>
        {
            onHover(false);
            SetNativePointerCursor(false);
        };
        element.GestureRecognizers.Add(pointerGesture);
    }

#if WINDOWS
    [DllImport("user32.dll", EntryPoint = "LoadCursorW")]
    private static extern nint LoadCursor(nint instance, nint cursorName);

    [DllImport("user32.dll", EntryPoint = "SetCursor")]
    private static extern nint SetCursor(nint cursor);

    private static void SetNativePointerCursor(bool handCursor)
    {
        var cursorId = handCursor ? 32649 : 32513;
        SetCursor(LoadCursor(0, cursorId));
    }
#else
    private static void SetNativePointerCursor(bool handCursor)
    {
    }
#endif

    private async void OnAddCharacterClicked(object? sender, EventArgs e)
    {
        if (_characters.Count >= MaximumCharacterCount)
        {
            StatusLabel.Text = $"A maximum of {MaximumCharacterCount} character sheets is allowed";
            return;
        }

        var character = new CharacterSheet();
        if (_classCatalog.Classes.FirstOrDefault() is { } firstClass)
        {
            character.CharacterClass = firstClass.Name;
            character.BodyPoints = character.MaximumBodyPoints = firstClass.BodyPoints;
            character.MindPoints = character.MaximumMindPoints = firstClass.MindPoints;
            character.AttackDice = firstClass.AttackDice;
            character.DefenceDice = firstClass.DefenceDice;
        }

        _characters.Add(character);
        RebuildCharacterCards();
        await Task.Yield();
        await ScrollToCharacterAsync(_characters.Count - 1);
        StatusLabel.Text = "Character sheet added";
    }

    private async Task RemoveCharacterAsync(CharacterSheet character)
    {
        if (!_characters.Contains(character))
            return;

        var name = string.IsNullOrWhiteSpace(character.Name) ? "this character" : character.Name;
        if (!await DisplayAlert("Remove character", $"Are you sure you want to remove {name}?", "Remove", "Cancel"))
            return;

        _characters.Remove(character);
        if (_characters.Count == 0)
            _characters.Add(new CharacterSheet());

        _currentCarouselIndex = Math.Clamp(_currentCarouselIndex, 0, _characters.Count - 1);
        RebuildCharacterCards();
        await Task.Yield();
        await ScrollToCharacterAsync(_currentCarouselIndex);
        StatusLabel.Text = "Character sheet removed";
    }

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync();
            if (file is null)
                return;

            await using var stream = await file.OpenReadAsync();
            var roster = await _dataService.LoadRosterAsync(stream);
            if (roster.Count is 0 or > MaximumCharacterCount)
            {
                await DisplayAlert("Invalid roster", $"A roster must contain between 1 and {MaximumCharacterCount} characters.", "OK");
                return;
            }

            _characters.Clear();
            _characters.AddRange(roster);
            _currentCarouselIndex = 0;
            RebuildCharacterCards();
            await Task.Yield();
            await ScrollToCharacterAsync(0);
            StatusLabel.Text = $"Loaded roster from {file.FileName}";
        }
        catch (JsonException exception)
        {
            await DisplayAlert("Invalid character file", exception.Message, "OK");
        }
        catch (Exception exception)
        {
            await DisplayAlert("Could not open file", exception.Message, "OK");
        }
    }

    private async void OnSaveRosterClicked(object? sender, EventArgs e)
    {
        try
        {
            var path = await _dataService.SaveRosterAsync(_characters);
            StatusLabel.Text = $"Saved {_characters.Count} characters to {path}";
        }
        catch (Exception exception)
        {
            await DisplayAlert("Could not save roster", exception.Message, "OK");
        }
    }

    private async Task ExportCharacterAsync(CharacterSheet character)
    {
        try
        {
            var path = await _dataService.SaveCharacterAsync(character, character.Name);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Export {GetCharacterTitle(character, _characters.IndexOf(character))}",
                File = new ShareFile(path)
            });
            StatusLabel.Text = $"Exported {GetCharacterTitle(character, _characters.IndexOf(character))}";
        }
        catch (Exception exception)
        {
            await DisplayAlert("Could not export character", exception.Message, "OK");
        }
    }

    private static string GetCharacterTitle(CharacterSheet character, int index) =>
        string.IsNullOrWhiteSpace(character.Name) ? $"Character {index + 1}" : character.Name;
}
