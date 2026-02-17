using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_Admin_Grab.Config;
using CS2_Admin_Grab.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CS2_Admin_Grab.Services;

/// <summary>
/// Физика захвата на основе spring-damper модели.
/// Параметры берутся из конфигурации, а не из хардкод-констант.
/// </summary>
public class SpringDamperPhysics : IGrabPhysics
{
    private readonly AdminGrabConfig _config;

    public SpringDamperPhysics(AdminGrabConfig config)
    {
        _config = config;
    }

    public void ApplyToPlayer(CCSPlayerPawn pawn, Vector currentPos, Vector targetPos)
    {
        Vector currentCenter = currentPos + new Vector(0, 0, 36f);
        Vector targetCenter = targetPos + new Vector(0, 0, 36f);
        Vector delta = targetCenter - currentCenter;
        Vector newVel = (delta * _config.Gain) + (pawn.AbsVelocity * _config.Damping);
        pawn.Teleport(pawn.AbsOrigin, pawn.EyeAngles, VectorMath.ClampLength(newVel, _config.MaxVelocity));
    }

    public void ApplyToEntity(CBaseEntity entity, Vector currentPos, Vector targetPos)
    {
        Vector delta = targetPos - currentPos;
        Vector newVel = (delta * _config.Gain) + (entity.AbsVelocity * _config.Damping);
        entity.Teleport(entity.AbsOrigin, entity.AbsRotation, VectorMath.ClampLength(newVel, _config.MaxVelocity));
    }
}
