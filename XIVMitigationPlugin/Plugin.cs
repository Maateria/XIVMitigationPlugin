using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using XIVMitigationPlugin.Services;
using XIVMitigationPlugin.Windows;

namespace XIVMitigationPlugin;

public sealed class Plugin : IDalamudPlugin
{
    // ── Services Dalamud injectés ─────────────────────────────────────────────
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager         CommandManager  { get; private set; } = null!;
    [PluginService] internal static IClientState            ClientState     { get; private set; } = null!;
    [PluginService] internal static ICondition              Condition       { get; private set; } = null!;
    [PluginService] internal static IFramework              Framework       { get; private set; } = null!;
    [PluginService] internal static IPluginLog              Log             { get; private set; } = null!;
    [PluginService] internal static IGameGui                GameGui         { get; private set; } = null!;
    [PluginService] internal static IDataManager            DataManager     { get; private set; } = null!;

    private const string Command = "/xivmit";

    // ── Propriétés publiques ──────────────────────────────────────────────────
    public Configuration      Configuration      { get; init; }
    public PlanLoader         PlanLoader         { get; init; }
    public CombatTracker      CombatTracker      { get; init; }
    public HotbarHighlighter  HotbarHighlighter  { get; init; }

    // ── UI ────────────────────────────────────────────────────────────────────
    public readonly WindowSystem WindowSystem = new("XIVMitigationPlugin");
    private OverlayWindow OverlayWindow { get; init; }
    private ConfigWindow  ConfigWindow  { get; init; }

    // ─────────────────────────────────────────────────────────────────────────
    public Plugin()
    {
        Configuration     = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        PlanLoader        = new PlanLoader();
        CombatTracker     = new CombatTracker(this);
        HotbarHighlighter = new HotbarHighlighter(GameGui, DataManager);

        OverlayWindow = new OverlayWindow(this);
        ConfigWindow  = new ConfigWindow(this);

        WindowSystem.AddWindow(OverlayWindow);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "/xivmit → toggle overlay  |  /xivmit config → paramètres",
        });

        PluginInterface.UiBuilder.Draw         += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += ConfigWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi   += OverlayWindow.Toggle;

        // Recharger le plan si un chemin était déjà configuré
        if (!string.IsNullOrWhiteSpace(Configuration.PlanFilePath))
            PlanLoader.LoadFromFile(Configuration.PlanFilePath);

        Log.Information("[XIVMit] Plugin chargé.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw         -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= ConfigWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi   -= OverlayWindow.Toggle;

        CombatTracker.Dispose();
        HotbarHighlighter.Dispose();
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        OverlayWindow.Dispose();
        CommandManager.RemoveHandler(Command);
    }

    /// <summary>
    /// Draw principal : d'abord les fenêtres ImGui, ensuite les cadres hotbar.
    /// L'ordre est important — OverlayWindow.Draw met à jour ActiveSpells avant DrawHighlights.
    /// </summary>
    private void OnDraw()
    {
        WindowSystem.Draw();
        if (OverlayWindow.IsOpen && CombatTracker.InCombat)
            HotbarHighlighter.DrawHighlights();
    }

    private void OnCommand(string command, string args)
    {
        var arg = args.Trim();
        if (arg.Equals("config", System.StringComparison.OrdinalIgnoreCase))
            ConfigWindow.Toggle();
        else if (arg.Equals("debug", System.StringComparison.OrdinalIgnoreCase))
            HotbarHighlighter.DumpState();
        else
            OverlayWindow.Toggle();
    }
}
