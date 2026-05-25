using CounterStrikeSharp.API.Core;
using CS2_Admin_Grab.Models;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CS2_Admin_Grab.Services;

/// <summary>
/// Контракт для визуализации захвата.
/// Управляет beam-лучами, wireframe box и HUD.
/// </summary>
public interface IGrabVisualizer
{
    void CreateVisuals(GrabSession session, Vector beamStart);
    void UpdateVisuals(GrabSession session, Vector beamStart, Vector? beamEnd, CBaseEntity? targetEntity);
    void DestroyVisuals(GrabSession session);
    void UpdateHud(CCSPlayerController admin, string entityClass, string targetName, float currentDist, float targetDist, AdminGrabPlugin plugin);
}
