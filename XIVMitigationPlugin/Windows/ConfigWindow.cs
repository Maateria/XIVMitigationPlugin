using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace XIVMitigationPlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    // Buffer pour l'InputText (ImGui a besoin d'une string mutable)
    private string filePathBuf  = "";
    private string statusMsg    = "";
    private bool   statusOk;

    private static readonly string[] Roles = ["MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2"];

    public ConfigWindow(Plugin plugin)
        : base("XIVMitigation — Paramètres###xivmit-config")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(440, 280),
            MaximumSize = new Vector2(640, 480),
        };
        this.plugin  = plugin;
        filePathBuf  = plugin.Configuration.PlanFilePath;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var config = plugin.Configuration;

        // ── Section : fichier plan ────────────────────────────────────────────
        ImGui.TextColored(new Vector4(0.40f, 0.85f, 1f, 1f), "Fichier plan");
        ImGui.Separator();
        ImGui.TextDisabled("Exporte ton plan depuis XIVMitigation, puis charge-le ici.");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(-90);
        ImGui.InputText("##planpath", ref filePathBuf, 512);

        ImGui.SameLine();
        if (ImGui.Button("Charger", new Vector2(80, 0)))
        {
            config.PlanFilePath = filePathBuf.Trim();
            config.Save();

            statusOk  = plugin.PlanLoader.LoadFromFile(config.PlanFilePath);
            statusMsg = statusOk
                ? $"✓  {plugin.PlanLoader.Plan?.Boss}"
                : $"✗  {plugin.PlanLoader.ErrorMessage}";
        }

        if (statusMsg.Length > 0)
        {
            var col = statusOk
                ? new Vector4(0.2f, 0.85f, 0.4f, 1f)
                : new Vector4(1f,   0.4f,  0.4f, 1f);
            ImGui.TextColored(col, statusMsg);
        }

        ImGui.Spacing();

        // ── Section : paramètres ──────────────────────────────────────────────
        ImGui.TextColored(new Vector4(0.40f, 0.85f, 1f, 1f), "Paramètres");
        ImGui.Separator();

        // Rôle du joueur
        var roleIdx = Array.IndexOf(Roles, config.PlayerRole);
        if (roleIdx < 0) roleIdx = 0;
        ImGui.SetNextItemWidth(110);
        if (ImGui.Combo("Mon rôle", ref roleIdx, Roles, Roles.Length))
        {
            config.PlayerRole = Roles[roleIdx];
            config.Save();
        }

        // Lead time
        var leadTime = config.LeadTime;
        ImGui.SetNextItemWidth(110);
        if (ImGui.SliderFloat("Anticipation (s)", ref leadTime, 0f, 15f, "%.0f s"))
        {
            config.LeadTime = leadTime;
            config.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("← Highlight X secondes avant la mécha");

        // Nombre de méchas affichées
        var count = config.VisibleCount;
        ImGui.SetNextItemWidth(110);
        if (ImGui.SliderInt("Méchas affichées", ref count, 1, 6))
        {
            config.VisibleCount = count;
            config.Save();
        }

        // Afficher tous les rôles
        var showAll = config.ShowAllRoles;
        if (ImGui.Checkbox("Afficher tous les rôles (pas seulement le mien)", ref showAll))
        {
            config.ShowAllRoles = showAll;
            config.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("/xivmit         — afficher/masquer l'overlay");
        ImGui.TextDisabled("/xivmit config  — ouvrir cette fenêtre");
    }
}
