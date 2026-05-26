using Dalamud.Configuration;
using System;

namespace XIVMitigationPlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Chemin absolu vers le fichier JSON exporté depuis XIVMitigation.</summary>
    public string PlanFilePath { get; set; } = "";

    /// <summary>Rôle du joueur dans le groupe (MT, OT, H1, H2, M1, M2, R1, R2).</summary>
    public string PlayerRole { get; set; } = "H1";

    /// <summary>
    /// Délai d'anticipation en secondes : la mécanique s'affiche en jaune
    /// X secondes avant d'être nécessaire.
    /// </summary>
    public float LeadTime { get; set; } = 5f;

    /// <summary>Nombre de méchaniques futures affichées simultanément.</summary>
    public int VisibleCount { get; set; } = 3;

    /// <summary>Afficher tous les rôles et pas seulement le sien.</summary>
    public bool ShowAllRoles { get; set; } = false;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
