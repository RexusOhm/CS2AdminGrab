using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CS2_Admin_Grab.Models;

/// <summary>
/// Полиморфная модель захваченной цели.
/// Инкапсулирует логику resolve/валидации для каждого типа цели.
/// </summary>
public abstract class GrabTarget
{
    public abstract bool IsValid { get; }
    public abstract CBaseEntity? ResolveEntity();
    public abstract string GetLabel();
    public abstract Vector? GetPosition();
    public abstract Vector? GetBeamEndPosition();

    public sealed class Player : GrabTarget
    {
        public int UserId { get; }

        public Player(int userId) => UserId = userId;

        public CCSPlayerController? ResolveController() =>
            Utilities.GetPlayerFromUserid(UserId);

        public override bool IsValid
        {
            get
            {
                var controller = ResolveController();
                return controller != null
                    && controller.IsValid
                    && controller.PawnIsAlive
                    && controller.Connected == PlayerConnectedState.PlayerConnected
                    && controller.PlayerPawn.Value != null;
            }
        }

        public override CBaseEntity? ResolveEntity() =>
            ResolveController()?.PlayerPawn.Value;

        public override string GetLabel() =>
            ResolveController()?.PlayerName ?? "Unknown";

        public override Vector? GetPosition() =>
            ResolveEntity()?.AbsOrigin;

        public override Vector? GetBeamEndPosition()
        {
            var pos = GetPosition();
            return pos != null ? pos + new Vector(0, 0, 36.0f) : null;
        }
    }

    public sealed class Entity : GrabTarget
    {
        public int EntityIndex { get; }

        public Entity(int entityIndex) => EntityIndex = entityIndex;

        public CBaseEntity? ResolveBaseEntity() =>
            Utilities.GetEntityFromIndex<CBaseEntity>(EntityIndex);

        public override bool IsValid
        {
            get
            {
                try
                {
                    var entity = ResolveBaseEntity();
                    if (entity == null || !entity.IsValid) return false;

                    if (!entity.DesignerName.EndsWith("_projectile") && entity.OwnerEntity.IsValid)
                        return false;

                    if (entity.DesignerName.EndsWith("_projectile") &&
                        entity.As<CBaseCSGrenadeProjectile>().DetonationRecorded)
                        return false;

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public override CBaseEntity? ResolveEntity() => ResolveBaseEntity();

        public override string GetLabel() =>
            ResolveBaseEntity()?.DesignerName ?? "Object";

        public override Vector? GetPosition() =>
            ResolveBaseEntity()?.AbsOrigin;

        public override Vector? GetBeamEndPosition() => GetPosition();
    }
}
