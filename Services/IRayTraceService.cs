using CounterStrikeSharp.API.Core;
using CS2_Admin_Grab.Models;

namespace CS2_Admin_Grab.Services;

/// <summary>
/// Контракт для сервиса трассировки лучей.
/// Позволяет подменить реализацию (OBB, нативный TraceRay и т.д.).
/// </summary>
public interface IRayTraceService
{
    TraceResult TraceForGrabbable(CCSPlayerController admin, bool includePlayers, bool includePhysics);
}
