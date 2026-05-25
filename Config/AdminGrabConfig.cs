using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace CS2_Admin_Grab.Config;

/// <summary>
/// Конфигурация плагина Admin Grab.
/// Все физические параметры вынесены сюда для настройки без перекомпиляции.
/// Файл: configs/plugins/Admin Grab/Admin Grab.json
/// </summary>
public class AdminGrabConfig : BasePluginConfig
{
    [JsonPropertyName("Allow grab physics objects")]
    public bool AllowGrabPhysicsObjects { get; set; } = true;

    /// <summary>
    /// Флаг привилегии для захвата игроков. Пустая строка "" — запрет для всех.
    /// </summary>
    [JsonPropertyName("Permission Flag Players")]
    public string? PermissionFlagPlayers { get; set; } = "@css/ban";

    /// <summary>
    /// Флаг привилегии для захвата физических объектов (пропы, оружие, гранаты и т.д.).
    /// Пустая строка "" — запрет для всех. null — не требуется (любой с доступом к команде).
    /// </summary>
    [JsonPropertyName("Permission Flag Physics")]
    public string? PermissionFlagPhysics { get; set; } = "@css/vip";

    // --- Физика spring-damper ---

    [JsonPropertyName("Gain")]
    public float Gain { get; set; } = 25f;

    [JsonPropertyName("Damping")]
    public float Damping { get; set; } = 0.60f;

    [JsonPropertyName("Max Velocity")]
    public float MaxVelocity { get; set; } = 3500f;

    [JsonPropertyName("Stuck Threshold")]
    public float StuckThreshold { get; set; } = 0.5f;

    // --- Дистанция ---

    [JsonPropertyName("Min Distance")]
    public float MinDistance { get; set; } = 60f;

    [JsonPropertyName("Max Distance")]
    public float MaxDistance { get; set; } = 2000f;

    [JsonPropertyName("Scroll Step")]
    public float ScrollStep { get; set; } = 25f;

    [JsonPropertyName("Button Step")]
    public float ButtonStep { get; set; } = 10f;
    
    // --- Дроп ---
    
    [JsonPropertyName("Throw Force")]
    public float ThrowForce { get; set; } = 1500f;

    [JsonPropertyName("Throw Force Vertical")]
    public float ThrowForceVertical { get; set; } = 150f;
}
