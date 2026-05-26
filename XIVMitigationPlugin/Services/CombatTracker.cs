using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace XIVMitigationPlugin.Services;

/// <summary>
/// Suit l'état du combat via ICondition et expose un timer en secondes.
/// Abonne IFramework.Update au constructeur — appeler Dispose() à la fin.
/// </summary>
public sealed class CombatTracker : IDisposable
{
    private readonly Plugin  plugin;
    private          bool     wasInCombat;
    private          DateTime combatStart;

    /// <summary>True si le joueur est actuellement en combat.</summary>
    public bool   InCombat       { get; private set; }

    /// <summary>Secondes écoulées depuis le début du combat (0 si hors combat).</summary>
    public double ElapsedSeconds { get; private set; }

    // ── Événements ────────────────────────────────────────────────────────────
    /// <summary>Déclenché à la seconde où le combat commence.</summary>
    public event Action? OnCombatStart;

    /// <summary>Déclenché à la seconde où le combat se termine (wipe ou victoire).</summary>
    public event Action? OnCombatEnd;

    // ─────────────────────────────────────────────────────────────────────────
    public CombatTracker(Plugin plugin)
    {
        this.plugin  = plugin;
        Plugin.Framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework _)
    {
        var inCombat = Plugin.Condition[ConditionFlag.InCombat];

        if (inCombat && !wasInCombat)
        {
            combatStart    = DateTime.UtcNow;
            ElapsedSeconds = 0;
            InCombat       = true;
            Plugin.Log.Information("[XIVMit] Combat démarré");
            OnCombatStart?.Invoke();
        }
        else if (!inCombat && wasInCombat)
        {
            ElapsedSeconds = 0;
            InCombat       = false;
            Plugin.Log.Information("[XIVMit] Combat terminé");
            OnCombatEnd?.Invoke();
        }

        wasInCombat = inCombat;

        if (inCombat)
            ElapsedSeconds = (DateTime.UtcNow - combatStart).TotalSeconds;
    }

    public void Dispose() => Plugin.Framework.Update -= OnUpdate;
}
