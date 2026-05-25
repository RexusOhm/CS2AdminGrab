using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CS2_Admin_Grab.Models;

/// <summary>
/// Полное состояние одной сессии захвата.
/// Объединяет все данные, ранее разбросанные по 7 словарям.
/// </summary>
public class GrabSession
{
    public int AdminUserId { get; }
    public GrabTarget Target { get; }
    public float Distance { get; set; }
    public Vector? LastTargetPosition { get; set; }

    // Сохранённое состояние цели (для восстановления при отпускании)
    public float OriginalSpeed { get; init; }
    public float OriginalGravity { get; init; }

    // Визуальные ресурсы (управляются IGrabVisualizer)
    public CEnvBeam? GrabBeam { get; set; }
    public List<CEnvBeam>? BoxBeams { get; set; }
    
    // --- Новые свойства для механики ударов и бросков ---
    public float LastHitTime { get; set; } = 0f; // Время последнего удара (кнопка E)
    public float TargetDistanceSetting { get; set; } // Расстояние уставки (заданное админом)

    public GrabSession(int adminUserId, GrabTarget target, float distance)
    {
        AdminUserId = adminUserId;
        Target = target;
        Distance = distance;
        TargetDistanceSetting = distance;
    }
}
