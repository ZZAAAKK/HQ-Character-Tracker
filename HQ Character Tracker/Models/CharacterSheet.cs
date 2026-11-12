namespace HQ_Character_Tracker.Models;

public sealed class CharacterSheet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Hero";
    public string CharacterClass { get; set; } = "Barbarian";
    public int BodyPoints { get; set; } = 8;
    public int MaximumBodyPoints { get; set; } = 8;
    public int MindPoints { get; set; } = 2;
    public int MaximumMindPoints { get; set; } = 2;
    public int AttackDice { get; set; } = 3;
    public int RangedAttackDice { get; set; }
    public int DefenceDice { get; set; } = 2;
    public int GoldCoins { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<string> Equipment { get; set; } = [];
    public List<string> Spells { get; set; } = [];
    public List<string> CastSpells { get; set; } = [];
}

public sealed class CharacterClassCatalog
{
    public List<CharacterClassSetting> Classes { get; set; } = [];
}

public sealed class CharacterClassSetting
{
    public string Name { get; set; } = string.Empty;
    public int BodyPoints { get; set; }
    public int MindPoints { get; set; }
    public int AttackDice { get; set; }
    public int DefenceDice { get; set; }
}

public sealed class EquipmentCatalog
{
    public List<EquipmentSetting> Equipment { get; set; } = [];
}

public sealed class EquipmentSetting
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class SpellCatalog
{
    public List<SpellSetting> Spells { get; set; } = [];
}

public sealed class SpellSetting
{
    public string Name { get; set; } = string.Empty;
    public string School { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
