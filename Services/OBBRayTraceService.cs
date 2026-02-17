using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_Admin_Grab.Models;
using CS2_Admin_Grab.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CS2_Admin_Grab.Services;

/// <summary>
/// Реализация трассировки лучей на основе OBB (Oriented Bounding Box).
/// Переводит луч в локальную систему координат объекта для учёта вращения.
/// </summary>
public class OBBRayTraceService : IRayTraceService
{
    private const float MaxTraceDistance = 8096f;
    private const float AimMagnetismRadius = 12f;

    private static readonly HashSet<string> GrabbableEntityNames = new()
    {
        "prop_physics",
        "prop_physics_override",
        "prop_physics_multiplayer",
        "chicken"
    };

    public TraceResult TraceForGrabbable(CCSPlayerController admin, bool includePlayers, bool includePhysics)
    {
        var adminPawn = admin.PlayerPawn.Value;
        if (adminPawn == null || !adminPawn.IsValid)
            return TraceResult.Miss;

        Vector rayOrigin = adminPawn.AbsOrigin! + new Vector(0, 0, adminPawn.ViewOffset.Z);
        Vector forward = VectorMath.GetForwardVector(adminPawn.EyeAngles);

        float closestDist = MaxTraceDistance;
        GrabTarget? closestTarget = null;

        // 1. Проверяем игроков
        if (includePlayers)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (!player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected ||
                    !player.PawnIsAlive || player.UserId == admin.UserId)
                    continue;

                var pawn = player.PlayerPawn.Value;
                if (pawn == null || !pawn.IsValid) continue;

                if (CheckIntersectionOBBWithPredict(rayOrigin, forward, pawn, out float dist) && dist < closestDist)
                {
                    closestDist = dist;
                    closestTarget = new GrabTarget.Player(player.UserId!.Value);
                }
            }
        }

        // 2. Проверяем физические объекты
        if (includePhysics)
        {
            foreach (var ent in Utilities.GetAllEntities())
            {
                if (ent == null || !ent.IsValid || ent.Index == 0) continue;
                if (!IsGrabbableEntity(ent)) continue;

                var baseEnt = ent.As<CBaseEntity>();
                if (baseEnt == null) continue;

                if (CheckIntersectionOBBWithPredict(rayOrigin, forward, baseEnt, out float dist) && dist < closestDist)
                {
                    closestDist = dist;
                    closestTarget = new GrabTarget.Entity((int)ent.Index);
                }
            }
        }

        if (closestTarget == null) return TraceResult.Miss;

        return new TraceResult
        {
            Hit = true,
            Distance = closestDist,
            Target = closestTarget
        };
    }

    private static bool IsGrabbableEntity(CEntityInstance ent)
    {
        string name = ent.DesignerName;
        if (string.IsNullOrEmpty(name)) return false;
        return GrabbableEntityNames.Contains(name)
            || name.StartsWith("weapon_")
            || name.EndsWith("_projectile")
            || name.StartsWith("item_");
    }

    private static bool CheckIntersectionOBBWithPredict(
        Vector rayOrigin, Vector rayDir, CBaseEntity entity, out float distance)
    {
        distance = float.MaxValue;
        if (entity.Collision == null) return false;

        Vector mins = entity.Collision.Mins - new Vector(AimMagnetismRadius, AimMagnetismRadius, AimMagnetismRadius);
        Vector maxs = entity.Collision.Maxs + new Vector(AimMagnetismRadius, AimMagnetismRadius, AimMagnetismRadius);

        Vector entOrigin = entity.AbsOrigin ?? new Vector(0, 0, 0);
        QAngle entRotation = entity.AbsRotation ?? new QAngle(0, 0, 0);
        Vector velocity = entity.AbsVelocity ?? new Vector(0, 0, 0);

        bool hit = CheckOBBInternal(rayOrigin, rayDir, entOrigin, entRotation, mins, maxs, out distance);

        // Компенсация скорости: если объект движется > 500 units/s, проверяем предыдущую позицию
        if (!hit && velocity.LengthSqr() > 250000f)
        {
            Vector prevOrigin = entOrigin - (velocity * 0.0078f);
            hit = CheckOBBInternal(rayOrigin, rayDir, prevOrigin, entRotation, mins, maxs, out distance);
        }

        return hit;
    }

    private static bool CheckOBBInternal(
        Vector rayOrigin, Vector rayDir, Vector entOrigin, QAngle entRotation,
        Vector mins, Vector maxs, out float distance)
    {
        GetOBBBasisVectors(entRotation, out Vector fwd, out Vector right, out Vector up);

        Vector delta = rayOrigin - entOrigin;

        Vector rOriginLocal = new Vector(
            VectorMath.Dot(delta, fwd),
            VectorMath.Dot(delta, right),
            VectorMath.Dot(delta, up)
        );

        Vector rDirLocal = new Vector(
            VectorMath.Dot(rayDir, fwd),
            VectorMath.Dot(rayDir, right),
            VectorMath.Dot(rayDir, up)
        );

        return IntersectAABB(rOriginLocal, rDirLocal, mins, maxs, out distance);
    }

    /// <summary>
    /// Рассчитывает базисные векторы OBB для slab intersection теста.
    /// Отличается от стандартного Source Engine GetAngleVectors тем, что вторая ось —
    /// Right (а не Left), что соответствует правой системе координат для AABB-теста.
    /// Эта формулировка напрямую воспроизводит математику оригинальной реализации.
    /// </summary>
    private static void GetOBBBasisVectors(QAngle angles, out Vector forward, out Vector right, out Vector up)
    {
        const float Deg2Rad = MathF.PI / 180f;

        float p = angles.X * Deg2Rad;
        float y = angles.Y * Deg2Rad;
        float r = angles.Z * Deg2Rad;

        float sp = MathF.Sin(p);
        float cp = MathF.Cos(p);
        float sy = MathF.Sin(y);
        float cy = MathF.Cos(y);
        float sr = MathF.Sin(r);
        float cr = MathF.Cos(r);

        forward = new Vector(cp * cy, cp * sy, -sp);

        // Right = -Left(Source Engine), используется для OBB slab intersection
        right = new Vector(
            -sr * sp * cy - cr * sy,
            -sr * sp * sy + cr * cy,
            -sr * cp
        );

        up = new Vector(
            cr * sp * cy + -sr * sy,
            cr * sp * sy + sr * cy,
            cr * cp
        );
    }

    private static bool IntersectAABB(Vector start, Vector dir, Vector min, Vector max, out float distance)
    {
        distance = float.MaxValue;
        float tMin = 0f;
        float tMax = MaxTraceDistance;

        if (!IntersectAxis(start.X, dir.X, min.X, max.X, ref tMin, ref tMax)) return false;
        if (!IntersectAxis(start.Y, dir.Y, min.Y, max.Y, ref tMin, ref tMax)) return false;
        if (!IntersectAxis(start.Z, dir.Z, min.Z, max.Z, ref tMin, ref tMax)) return false;

        distance = tMin;
        return true;
    }

    private static bool IntersectAxis(float pos, float dir, float min, float max, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(dir) < 1e-6f)
            return pos >= min && pos <= max;

        float t1 = (min - pos) / dir;
        float t2 = (max - pos) / dir;

        if (t1 > t2) (t1, t2) = (t2, t1);
        if (t1 > tMin) tMin = t1;
        if (t2 < tMax) tMax = t2;

        return tMin <= tMax;
    }
}
