using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XIVMitigationPlugin.Services;

/// <summary>
/// Dessine un cadre coloré + un timer en live sur les slots de la hotbar
/// contenant les spells à lancer maintenant ou bientôt.
/// </summary>
public class HotbarHighlighter : IDisposable
{
    private readonly IGameGui     _gameGui;
    private readonly IDataManager _dataManager;

    // Police FFXIV baked à 18 px — rendu net, pas de flou de scaling
    private readonly IFontHandle _timerFont;

    // Nom anglais de spell → Action RowId FFXIV
    private readonly Dictionary<string, uint> _nameToId = new(StringComparer.OrdinalIgnoreCase);

    // Nom anglais → nom dans la langue du client (FR, EN, DE, JP…)
    private readonly Dictionary<string, string> _nameToLocal = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Spells à surligner ce frame : nom → secondes restantes avant la méca
    /// (négatif = méca déjà passée mais dans la fenêtre passée).
    /// Mis à jour par OverlayWindow chaque Draw.
    /// </summary>
    public readonly Dictionary<string, double> ActiveSpells = new(StringComparer.OrdinalIgnoreCase);

    // Format ImGui : 0xAABBGGRR  (A=alpha, B=blue, G=green, R=red)
    private const uint BorderAlert   = 0xFF0000FF; // rouge vif 100 % opaque (NOW/SOON)
    private const uint FillAlert     = 0x550000FF; // rouge ~33 % opaque
    private const uint BorderPreview = 0xFF0000FF; // rouge vif 100 % opaque (prochain)
    private const uint FillPreview   = 0x220000FF; // rouge très léger (aperçu)

    private const uint TextColor     = 0xFFFFFFFF; // blanc opaque
    private const uint TextShadow    = 0xFF000000; // noir opaque (contour)

    /// <summary>True = spells dans ActiveSpells qui sont juste un aperçu (pas encore urgents).</summary>
    public bool IsPreviewMode { get; set; }

    // ─────────────────────────────────────────────────────────────────────────
    public HotbarHighlighter(IGameGui gameGui, IDataManager dataManager)
    {
        _gameGui     = gameGui;
        _dataManager = dataManager;
        _timerFont   = Plugin.PluginInterface.UiBuilder.FontAtlas
                            .NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Axis18));
        BuildCache();
    }

    // ── Cache nom de spell (EN) → Action RowId ────────────────────────────────
    private void BuildCache()
    {
        try
        {
            // Feuille EN : noms utilisés comme clés (correspondance avec le JSON)
            var enSheet = _dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>(ClientLanguage.English);
            if (enSheet == null)
            {
                Plugin.Log.Warning("[XIVMit] Feuille Action (EN) Lumina introuvable.");
                return;
            }

            // RowId → nom EN (pour construire la traduction ensuite).
            // On utilise l'affectation directe (pas TryAdd) pour que les noms en
            // double — typiquement les anciennes pet-actions Scholar converties en
            // actions joueur en 7.0 (ex: "Whispering Dawn", "Fey Illumination") —
            // soient toujours résolus vers le RowId le plus récent (le plus élevé),
            // qui correspond à la version réellement placée sur la hotbar.
            var idToEnName = new Dictionary<uint, string>();
            foreach (var row in enSheet)
            {
                var name = row.Name.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _nameToId[name]      = row.RowId;  // ← écrase si doublon (garde le plus haut RowId)
                    idToEnName[row.RowId] = name;
                }
            }

            // Feuille dans la langue du client → nom localisé
            var clientLang = Plugin.ClientState.ClientLanguage;
            var localSheet = _dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>(clientLang);
            if (localSheet != null)
            {
                foreach (var row in localSheet)
                {
                    var localName = row.Name.ToString();
                    if (!string.IsNullOrWhiteSpace(localName) &&
                        idToEnName.TryGetValue(row.RowId, out var enName))
                        _nameToLocal[enName] = localName;
                }
            }

            Plugin.Log.Information(
                $"[XIVMit] HotbarHighlighter : {_nameToId.Count} actions EN indexées, " +
                $"{_nameToLocal.Count} traductions ({clientLang}).");
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "[XIVMit] Échec du cache d'actions Lumina.");
        }
    }

    /// <summary>
    /// Retourne le nom localisé (langue du client) d'un spell dont le nom EN est fourni.
    /// Retourne le nom EN tel quel si aucune traduction n'est trouvée.
    /// </summary>
    public string ToLocalName(string englishName) =>
        _nameToLocal.TryGetValue(englishName, out var local) ? local : englishName;

    // ── DrawHighlights ────────────────────────────────────────────────────────
    /// <summary>Appelé depuis UiBuilder.Draw (après WindowSystem.Draw).</summary>
    public unsafe void DrawHighlights()
    {
        if (ActiveSpells.Count == 0) return;

        // Résoudre les noms → IDs + conserver le timer associé
        var targetTimers = new Dictionary<uint, double>();
        foreach (var (spell, timer) in ActiveSpells)
        {
            if (_nameToId.TryGetValue(spell, out var id))
                targetTimers[id] = timer;
            else
                Plugin.Log.Verbose($"[XIVMit] '{spell}' non trouvé dans le cache EN.");
        }

        if (targetTimers.Count == 0) return;

        var uiModule = UIModule.Instance();
        if (uiModule == null) return;

        var hotbarModule = uiModule->GetRaptureHotbarModule();
        if (hotbarModule == null) return;

        // Couleurs selon mode alerte ou aperçu
        var borderCol = IsPreviewMode ? BorderPreview : BorderAlert;
        var fillCol   = IsPreviewMode ? FillPreview   : FillAlert;

        var drawList = ImGui.GetForegroundDrawList();

        for (var barIndex = 0; barIndex < 10; barIndex++)
        {
            var addonName    = barIndex == 0 ? "_ActionBar" : $"_ActionBar0{barIndex}";
            var addonWrapper = _gameGui.GetAddonByName(addonName, 1);
            if (addonWrapper.IsNull || !addonWrapper.IsVisible) continue;

            var addon = (AtkUnitBase*)addonWrapper.Address;

            // Accès ref pour éviter de copier le struct complet sur la pile
            ref var module = ref *hotbarModule;
            var slots = module.Hotbars[barIndex].Slots;

            for (var slotIdx = 0; slotIdx < slots.Length; slotIdx++)
            {
                var slot = slots[slotIdx];

                if (slot.CommandType != RaptureHotbarModule.HotbarSlotType.Action) continue;
                if (!targetTimers.TryGetValue(slot.CommandId, out var timeRemaining)) continue;

                // Offset confirmé depuis le dump NodeList : slot 0 → Node ID 8, slot N → ID N+8
                var node = addon->GetNodeById((uint)(slotIdx + 8));
                if (node == null) continue;

                // insetUnits = 12 design-units de chaque côté pour cibler l'icône.
                // yShift = décalage vers le bas pour centrer verticalement sur l'art du spell.
                var (min, max) = ComputeScreenRect(node, addon, insetUnits: 12f);
                if (min == max) continue;

                var yShift = 1f * addon->Scale;
                min = new Vector2(min.X, min.Y + yShift);
                max = new Vector2(max.X, max.Y + yShift);

                // ── Cadre ────────────────────────────────────────────────────
                drawList.AddRectFilled(min, max, fillCol,   0f);
                drawList.AddRect      (min, max, borderCol, 0f, ImDrawFlags.None, 10f);

                // ── Timer centré sur l'icône (police Axis18 baked → rendu net) ──
                var label = timeRemaining <= 0
                    ? "NOW"
                    : timeRemaining < 10.0
                        ? $"{timeRemaining:F1}s"
                        : $"{(int)timeRemaining}s";

                using (_timerFont.Push())
                {
                    var textSize = ImGui.CalcTextSize(label);
                    var textPos  = new Vector2(
                        (min.X + max.X) / 2f - textSize.X / 2f,
                        (min.Y + max.Y) / 2f - textSize.Y / 2f);

                    // Contour noir (4 directions)
                    drawList.AddText(new Vector2(textPos.X - 1, textPos.Y),     TextShadow, label);
                    drawList.AddText(new Vector2(textPos.X + 1, textPos.Y),     TextShadow, label);
                    drawList.AddText(new Vector2(textPos.X,     textPos.Y - 1), TextShadow, label);
                    drawList.AddText(new Vector2(textPos.X,     textPos.Y + 1), TextShadow, label);
                    // Texte principal blanc
                    drawList.AddText(textPos, TextColor, label);
                }
            }
        }
    }

    // ── DumpState ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Logue l'état courant dans /xllog pour diagnostiquer
    /// les problèmes de correspondance spell ↔ hotbar.
    /// </summary>
    public unsafe void DumpState()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[XIVMit Debug] ========================================");
        sb.AppendLine($"[XIVMit Debug] Cache EN : {_nameToId.Count} actions.");
        sb.AppendLine($"[XIVMit Debug] ActiveSpells ({ActiveSpells.Count}) : {string.Join(", ", ActiveSpells.Keys)}");

        foreach (var (spell, timer) in ActiveSpells)
        {
            if (_nameToId.TryGetValue(spell, out var id))
                sb.AppendLine($"[XIVMit Debug]   '{spell}' (t={timer:F1}s) → ActionId {id}  ✓");
            else
                sb.AppendLine($"[XIVMit Debug]   '{spell}' → NON TROUVÉ dans le cache  ✗");
        }

        // Scanner les hotbars
        var uiModule = UIModule.Instance();
        if (uiModule == null) { sb.AppendLine("[XIVMit Debug] UIModule NULL"); Plugin.Log.Information(sb.ToString()); return; }

        var hotbarModule = uiModule->GetRaptureHotbarModule();
        if (hotbarModule == null) { sb.AppendLine("[XIVMit Debug] HotbarModule NULL"); Plugin.Log.Information(sb.ToString()); return; }

        ref var module = ref *hotbarModule;

        for (var barIndex = 0; barIndex < 10; barIndex++)
        {
            var addonName    = barIndex == 0 ? "_ActionBar" : $"_ActionBar0{barIndex}";
            var addonWrapper = _gameGui.GetAddonByName(addonName, 1);
            bool visible     = !addonWrapper.IsNull && addonWrapper.IsVisible;
            if (!visible) continue;

            var slots = module.Hotbars[barIndex].Slots;
            for (var slotIdx = 0; slotIdx < slots.Length; slotIdx++)
            {
                var slot = slots[slotIdx];
                if (slot.CommandId == 0) continue;
                sb.AppendLine($"[XIVMit Debug]   Bar{barIndex} Slot{slotIdx:D2} → type={slot.CommandType} id={slot.CommandId}");
            }
        }

        // Dump les nodes des bars qui ont nos spells
        var targetIds2 = new HashSet<uint>();
        foreach (var spell in ActiveSpells.Keys)
            if (_nameToId.TryGetValue(spell, out var sid)) targetIds2.Add(sid);

        for (var barIndex = 0; barIndex < 10; barIndex++)
        {
            var addonName2    = barIndex == 0 ? "_ActionBar" : $"_ActionBar0{barIndex}";
            var addonWrapper2 = _gameGui.GetAddonByName(addonName2, 1);
            if (addonWrapper2.IsNull || !addonWrapper2.IsVisible) continue;

            ref var module2 = ref *hotbarModule;
            bool hasTarget = false;
            for (var slotIdx = 0; slotIdx < module2.Hotbars[barIndex].Slots.Length; slotIdx++)
            {
                if (targetIds2.Contains(module2.Hotbars[barIndex].Slots[slotIdx].CommandId)) { hasTarget = true; break; }
            }
            if (!hasTarget) continue;

            var addon2 = (AtkUnitBase*)addonWrapper2.Address;
            sb.AppendLine($"[XIVMit Debug] Bar{barIndex} addon->Scale={addon2->Scale:F3}  NodeList ({addon2->UldManager.NodeListCount} nodes) :");
            for (var i = 0; i < addon2->UldManager.NodeListCount; i++)
            {
                var n = addon2->UldManager.NodeList[i];
                if (n == null) continue;
                sb.AppendLine($"[XIVMit Debug]   NodeList[{i:D2}] ID={n->NodeId:D3} type={(ushort)n->Type:D5} w={n->Width:D3} h={n->Height:D3} scaleX={n->ScaleX:F2} screen=({n->ScreenX:F0},{n->ScreenY:F0})");
            }
        }

        Plugin.Log.Information(sb.ToString());
    }

    // ── Calcul de la position écran d'un node ─────────────────────────────────
    /// <summary>
    /// Utilise ScreenX/ScreenY (déjà transformés) + (Width/Height − 2×insetUnits) × Scale.
    /// insetUnits est exprimé en design-units FFXIV (pas en pixels écran).
    /// </summary>
    private static unsafe (Vector2 min, Vector2 max) ComputeScreenRect(
        AtkResNode* node, AtkUnitBase* addon, float insetUnits = 0f)
    {
        var scale = addon->Scale;
        var min   = new Vector2(node->ScreenX + insetUnits * scale,
                                node->ScreenY + insetUnits * scale);
        var max   = new Vector2(node->ScreenX + (node->Width  - insetUnits) * scale,
                                node->ScreenY + (node->Height - insetUnits) * scale);
        return (min, max);
    }

    public void Dispose()
    {
        _timerFont.Dispose();
    }
}
