using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using XIVMitigationPlugin.Models;

namespace XIVMitigationPlugin.Services;

/// <summary>
/// Charge et expose le plan XIVMitigation exporté depuis le site.
/// </summary>
public class PlanLoader
{
    public SavedPlan? Plan         { get; private set; }
    public string?   ErrorMessage  { get; private set; }

    /// <summary>True si un plan valide avec des assignations est chargé.</summary>
    public bool IsLoaded => Plan?.PluginAssignments is { Count: > 0 };

    // ── Groupes pré-calculés pour éviter du LINQ à chaque frame ─────────────
    /// <summary>
    /// Timestamps distincts triés croissants (une entrée par mécanique).
    /// </summary>
    public List<double> MechanicTimes { get; private set; } = [];

    /// <summary>
    /// Toutes les assignations indexées par timestamp (même clé que MechanicTimes).
    /// </summary>
    public Dictionary<double, List<PluginAssignment>> ByTime { get; private set; } = [];

    // ── Options de désérialisation ────────────────────────────────────────────
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ─────────────────────────────────────────────────────────────────────────
    public bool LoadFromFile(string path)
    {
        ErrorMessage = null;
        Plan         = null;
        MechanicTimes.Clear();
        ByTime.Clear();

        try
        {
            if (!File.Exists(path))
            {
                ErrorMessage = $"Fichier introuvable : {path}";
                return false;
            }

            var json = File.ReadAllText(path);
            Plan = JsonSerializer.Deserialize<SavedPlan>(json, JsonOptions);

            if (Plan?.PluginAssignments is not { Count: > 0 })
            {
                ErrorMessage = "Aucune assignation trouvée. Re-exporte le plan depuis XIVMitigation.";
                Plan = null;
                return false;
            }

            // Trier par temps puis pré-calculer l'index
            Plan.PluginAssignments.Sort((a, b) => a.Time.CompareTo(b.Time));

            foreach (var a in Plan.PluginAssignments)
            {
                if (!ByTime.TryGetValue(a.Time, out var list))
                {
                    list = [];
                    ByTime[a.Time] = list;
                    MechanicTimes.Add(a.Time);
                }
                list.Add(a);
            }

            Plugin.Log.Information(
                $"[XIVMit] Plan chargé : {Plan.Boss} — {Plan.PluginAssignments.Count} assignations / {MechanicTimes.Count} méchas");

            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de lecture : {ex.Message}";
            Plugin.Log.Error(ex, "[XIVMit] Échec du chargement du plan");
            return false;
        }
    }
}
