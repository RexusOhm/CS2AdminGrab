using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CS2_Admin_Grab.Services;

/// <summary>
/// Контракт для физического движка захвата.
/// Отвечает за перемещение цели к идеальной точке.
/// </summary>
public interface IGrabPhysics
{
    void ApplyToPlayer(CCSPlayerPawn pawn, Vector currentPos, Vector targetPos);
    void ApplyToEntity(CBaseEntity entity, Vector currentPos, Vector targetPos);
}
