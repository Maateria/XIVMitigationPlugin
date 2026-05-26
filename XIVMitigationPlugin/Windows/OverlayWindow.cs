using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace XIVMitigationPlugin.Windows;

/// <summary>
/// Overlay principal : affiche les prochaines méchaniques et les spells
/// à lancer pour le rôle configuré.
/// </summary>
public class OverlayWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    // Fenêtre d'observation : on affiche les méchas entre -2s (passé récent)
    // et +60s (futur proche).
    private const double PastWindow   =  2.0;
    private const double FutureWindow = 60.0;

    public OverlayWindow(Plugin plugin)
        : base("XIVMitigation##overlay",
               ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(260, 80),
            MaximumSize = new Vector2(480, 600),
        };
        IsOpen = true;
    }

    public void Dispose() { }

    // ── Couleurs ─────────────────────────────────────────────────────────────
    private static readonly Vector4 ColorHeader   = new(0.40f, 0.85f, 1.00f, 1f);
    private static readonly Vector4 ColorNow      = new(1.00f, 0.30f, 0.30f, 1f); // rouge
    private static readonly Vector4 ColorSoon     = new(1.00f, 0.85f, 0.10f, 1f); // jaune
    private static readonly Vector4 ColorUpcoming = new(0.65f, 0.65f, 0.65f, 1f); // gris

    private static readonly Vector4 ColorTank     = new(0.36f, 0.60f, 0.97f, 1f); // bleu
    private static readonly Vector4 ColorHealer   = new(0.20f, 0.83f, 0.60f, 1f); // vert
    private static readonly Vector4 ColorMelee    = new(0.97f, 0.45f, 0.45f, 1f); // rouge doux
    private static readonly Vector4 ColorRange    = new(0.98f, 0.75f, 0.14f, 1f); // jaune doré

    private static Vector4 RoleColor(string role) => role switch
    {
        "MT" or "OT" => ColorTank,
        "H1" or "H2" => ColorHealer,
        "M1" or "M2" => ColorMelee,
        "R1" or "R2" => ColorRange,
        _             => new Vector4(1f, 1f, 1f, 1f),
    };

    // ─────────────────────────────────────────────────────────────────────────
    public override void Draw()
    {
        var loader      = plugin.PlanLoader;
        var tracker     = plugin.CombatTracker;
        var config      = plugin.Configuration;
        var highlighter = plugin.HotbarHighlighter;

        // Réinitialiser les spells à surligner pour ce frame
        highlighter.ActiveSpells.Clear();

        // ── Plan non chargé ──────────────────────────────────────────────────
        if (!loader.IsLoaded)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Aucun plan chargé.");
            ImGui.TextDisabled("→ /xivmit config pour charger un fichier.");
            return;
        }

        var plan    = loader.Plan!;
        var elapsed = tracker.ElapsedSeconds;

        // ── En-tête : boss + timer ────────────────────────────────────────────
        ImGui.TextColored(ColorHeader, plan.Boss);
        ImGui.SameLine();

        if (tracker.InCombat)
        {
            var ts = TimeSpan.FromSeconds(elapsed);
            ImGui.Text($"  {ts.Minutes}:{ts.Seconds:D2}");
        }
        else
        {
            ImGui.TextDisabled("  — hors combat —");
        }

        ImGui.Separator();

        // ── Mise à jour du highlight hotbar (passe séparée) ──────────────────
        // Priorité 1 : mécaniques dans la fenêtre d'alerte [-PastWindow, LeadTime]
        // Priorité 2 : si rien, aperçu de la prochaine mécanique (bleu pâle)
        highlighter.IsPreviewMode = false;
        foreach (var t in loader.MechanicTimes)
        {
            var tl = t - elapsed;
            if (tl < -PastWindow) continue;
            if (tl > FutureWindow) break;
            if (tl <= config.LeadTime)
            {
                foreach (var a in loader.ByTime[t])
                {
                    if (config.ShowAllRoles || a.Role == config.PlayerRole)
                    {
                        // Garde le timer le plus urgent si le même spell apparaît à plusieurs mécas
                        if (!highlighter.ActiveSpells.TryGetValue(a.Spell, out var existing) || tl < existing)
                            highlighter.ActiveSpells[a.Spell] = tl;
                    }
                }
            }
        }
        if (highlighter.ActiveSpells.Count == 0)
        {
            // Fallback : la première mécanique à venir (aperçu)
            highlighter.IsPreviewMode = true;
            foreach (var t in loader.MechanicTimes)
            {
                var tl = t - elapsed;
                if (tl <= 0) continue;
                if (tl > FutureWindow) break;
                foreach (var a in loader.ByTime[t])
                    if (config.ShowAllRoles || a.Role == config.PlayerRole)
                        highlighter.ActiveSpells[a.Spell] = tl;
                break; // seulement la première mécanique
            }
        }

        // ── Sélection des méchas à afficher ──────────────────────────────────
        // On parcourt MechanicTimes (trié) et on filtre dans la fenêtre [-2s, +60s].
        var shown = 0;
        foreach (var t in loader.MechanicTimes)
        {
            var timeLeft = t - elapsed;

            // Hors fenêtre
            if (timeLeft < -PastWindow)   continue;
            if (timeLeft >  FutureWindow) break;
            if (shown >= config.VisibleCount) break;

            // Récupère toutes les assignations de cette mécanique
            var allAssignments = loader.ByTime[t];

            // Filtre par rôle si nécessaire
            List<XIVMitigationPlugin.Models.PluginAssignment> toDisplay;
            if (config.ShowAllRoles)
                toDisplay = allAssignments;
            else
            {
                toDisplay = [];
                foreach (var a in allAssignments)
                    if (a.Role == config.PlayerRole) toDisplay.Add(a);
            }

            // Nom de la mécanique (première entrée suffit)
            var mechName = allAssignments[0].Mechanic == "custom"
                ? $"Custom ({t:F0}s)"
                : allAssignments[0].Mechanic;

            // ── Ligne d'en-tête de mécanique ─────────────────────────────────
            Vector4 headerColor;
            string  prefix;

            if (timeLeft <= 0)
            {
                headerColor = ColorNow;
                prefix      = "▶ MAINTENANT";
            }
            else if (timeLeft <= config.LeadTime)
            {
                headerColor = ColorSoon;
                prefix      = $"⚠  {timeLeft:F1}s";
            }
            else
            {
                headerColor = ColorUpcoming;
                prefix      = $"    {timeLeft:F0}s";
            }

            ImGui.TextColored(headerColor, $"{prefix}  {mechName}");

            // ── Lignes de spells ──────────────────────────────────────────────
            if (toDisplay.Count == 0)
            {
                ImGui.TextDisabled("    (rien pour ton rôle)");
            }
            else
            {
                foreach (var a in toDisplay)
                {
                    ImGui.TextColored(RoleColor(a.Role), $"    [{a.Role}]");
                    ImGui.SameLine();
                    ImGui.Text(highlighter.ToLocalName(a.Spell));
                }
            }

            ImGui.Spacing();
            shown++;
        }

        if (shown == 0)
        {
            ImGui.TextDisabled(tracker.InCombat
                ? "Aucune mécanique dans la fenêtre."
                : "En attente du combat…");
        }
    }
}
