using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CS2_Admin_Grab.Config;
using CS2_Admin_Grab.Core;
using CS2_Admin_Grab.Services;
using Microsoft.Extensions.Logging;

namespace CS2_Admin_Grab;

/// <summary>
/// Точка входа плагина — Composition Root.
/// Собирает зависимости и регистрирует команды/события.
/// Не содержит бизнес-логики.
/// </summary>
[MinimumApiVersion(220)]
public class AdminGrabPlugin : BasePlugin, IPluginConfig<AdminGrabConfig>
{
    public override string ModuleName => "Admin Grab";
    public override string ModuleVersion => "0.1.0";
    public override string ModuleAuthor => "Rexus Ohm";

    public AdminGrabConfig Config { get; set; } = null!;

    private GrabManager _grabManager = null!;

    public void OnConfigParsed(AdminGrabConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        // --- Composition Root: собираем граф зависимостей ---
        IRayTraceService rayTrace = new OBBRayTraceService();
        IGrabPhysics physics = new SpringDamperPhysics(Config);
        IGrabVisualizer visualizer = new BeamVisualizer();
        var sessions = new GrabSessionManager(Config, visualizer);

        _grabManager = new GrabManager(rayTrace, physics, visualizer, sessions, Config, Logger);

        // --- Команды ---
        AddCommand("css_grab", "Взять/Отпустить игрока или объект (Toggle)", _grabManager.OnToggleGrab);
        AddCommand("css_grab_in", "Увеличить дистанцию", _grabManager.OnIncreaseDistance);
        AddCommand("css_grab_out", "Уменьшить дистанцию", _grabManager.OnDecreaseDistance);

        // --- События ---
        RegisterEventHandler<EventPlayerDisconnect>(_grabManager.OnPlayerDisconnect);
        RegisterEventHandler<EventPlayerDeath>(_grabManager.OnPlayerDeath);
        RegisterEventHandler<EventRoundStart>(_grabManager.OnRoundStart);
        RegisterListener<Listeners.OnTick>(_grabManager.OnTick);

        Logger.LogInformation("[AdminGrab] Plugin loaded");
    }

    public override void Unload(bool hotReload)
    {
        DeregisterEventHandler<EventPlayerDisconnect>(_grabManager.OnPlayerDisconnect);
        DeregisterEventHandler<EventPlayerDeath>(_grabManager.OnPlayerDeath);
        DeregisterEventHandler<EventRoundStart>(_grabManager.OnRoundStart);
        RemoveListener<Listeners.OnTick>(_grabManager.OnTick);

        Logger.LogInformation("[AdminGrab] Plugin unloaded");
    }
}
