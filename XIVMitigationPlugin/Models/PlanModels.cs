using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XIVMitigationPlugin.Models;

/// <summary>Miroir C# du format SavedPlan exporté par XIVMitigation.</summary>
public class SavedPlan
{
    [JsonPropertyName("boss")]
    public string Boss { get; set; } = "";

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("roster")]
    public Dictionary<string, RosterEntry>? Roster { get; set; }

    [JsonPropertyName("pluginAssignments")]
    public List<PluginAssignment>? PluginAssignments { get; set; }
}

/// <summary>
/// Une assignation aplatie : à <see cref="Time"/> secondes de combat,
/// le rôle <see cref="Role"/> doit lancer <see cref="Spell"/>
/// pour la mécanique <see cref="Mechanic"/>.
/// </summary>
public class PluginAssignment
{
    [JsonPropertyName("time")]
    public double Time { get; set; }

    [JsonPropertyName("mechanic")]
    public string Mechanic { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("spell")]
    public string Spell { get; set; } = "";
}

public class RosterEntry
{
    [JsonPropertyName("name")]
    public LocalizedName? Name { get; set; }

    [JsonPropertyName("iconPath")]
    public string IconPath { get; set; } = "";
}

public class LocalizedName
{
    [JsonPropertyName("en")]
    public string En { get; set; } = "";

    [JsonPropertyName("fr")]
    public string Fr { get; set; } = "";
}
