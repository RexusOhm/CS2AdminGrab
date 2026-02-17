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
        var currentCenter = currentPos + new Vector(0, 0, 36f);
        var targetCenter = targetPos + new Vector(0, 0, 36f);
        var delta = targetCenter - currentCenter;
        var newVel = (delta * _config.Gain) + (pawn.AbsVelocity * _config.Damping);
        //pawn.Teleport(null, null, VectorMath.ClampLength(newVel, _config.MaxVelocity));
        var vec = VectorMath.ClampLength(newVel, _config.MaxVelocity);
        pawn.AbsVelocity.X += vec.X;
        pawn.AbsVelocity.Y += vec.Y;
        pawn.AbsVelocity.Z += vec.Z;
    }

    public void ApplyToEntity(CBaseEntity entity, Vector currentPos, Vector targetPos)
    {
        var delta = targetPos - currentPos;
        var newVel = (delta * _config.Gain) + (entity.AbsVelocity * _config.Damping);
        //entity.Teleport(null, null, VectorMath.ClampLength(newVel, _config.MaxVelocity));
        var vec = VectorMath.ClampLength(newVel, _config.MaxVelocity);
        entity.AbsVelocity.X += vec.X;
        entity.AbsVelocity.Y += vec.Y;
        entity.AbsVelocity.Z += vec.Z;
    }
}
