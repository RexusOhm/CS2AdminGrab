using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_Admin_Grab.Config;
using CS2_Admin_Grab.Models;
using CS2_Admin_Grab.Services;
using CS2_Admin_Grab.Utils;
using Microsoft.Extensions.Logging;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CS2_Admin_Grab.Core;

/// <summary>
/// Тонкий координатор: связывает сервисы, обрабатывает команды и события.
/// Не содержит бизнес-логики физики, визуализации или ray trace.
/// </summary>
public class GrabManager
{
    private readonly IRayTraceService _rayTrace;
    private readonly IGrabPhysics _physics;
    private readonly IGrabVisualizer _visualizer;
    private readonly GrabSessionManager _sessions;
    private readonly AdminGrabConfig _config;
    private readonly ILogger _logger;

    public GrabManager(
        IRayTraceService rayTrace,
        IGrabPhysics physics,
        IGrabVisualizer visualizer,
        GrabSessionManager sessions,
        AdminGrabConfig config,
        ILogger logger)
    {
        _rayTrace = rayTrace;
        _physics = physics;
        _visualizer = visualizer;
        _sessions = sessions;
        _config = config;
        _logger = logger;
    }

    #region Commands

    public void OnToggleGrab(CCSPlayerController? admin, CommandInfo info)
    {
        if (!IsPlayerValid(admin)) return;
        if (!CanGrabPlayers(admin!) && !CanGrabPhysics(admin!))
        {
            admin!.PrintToCenter("Нет прав для использования граба");
            return;
        }

        int adminId = admin!.UserId!.Value;

        if (_sessions.HasSession(adminId))
        {
            _sessions.ReleaseSession(adminId);
            admin.PrintToCenter("Цель отпущена");
        }
        else
        {
            TryStartGrab(admin);
        }
    }

    public void OnIncreaseDistance(CCSPlayerController? admin, CommandInfo info) =>
        AdjustDistance(admin, _config.ScrollStep);

    public void OnDecreaseDistance(CCSPlayerController? admin, CommandInfo info) =>
        AdjustDistance(admin, -_config.ScrollStep);

    private void AdjustDistance(CCSPlayerController? admin, float delta)
    {
        if (!IsPlayerValid(admin)) return;
        int adminId = admin!.UserId!.Value;

        var session = _sessions.GetSession(adminId);
        if (session == null) return;

        session.Distance = Math.Clamp(session.Distance + delta, _config.MinDistance, _config.MaxDistance);
        admin.PrintToCenter($"Дистанция: {session.Distance:F0}");
    }

    #endregion

    #region Events

    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid == null) return HookResult.Continue;

        try
        {
            int userId = @event.Userid.UserId!.Value;

            if (_sessions.HasSession(userId))
                _sessions.ReleaseSession(userId);

            _sessions.ReleaseByTargetPlayer(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnPlayerDisconnect");
        }

        return HookResult.Continue;
    }

    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (@event.Userid == null) return HookResult.Continue;

        try
        {
            _sessions.ReleaseByTargetPlayer(@event.Userid.UserId!.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnPlayerDeath");
        }

        return HookResult.Continue;
    }

    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        try
        {
            foreach (int adminId in _sessions.GetActiveAdminIds())
            {
                var admin = Utilities.GetPlayerFromUserid(adminId);
                if (admin != null && admin.IsValid)
                    admin.PrintToCenter("Цель отпущена");
            }

            _sessions.ReleaseAll();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnRoundStart");
        }

        return HookResult.Continue;
    }

    #endregion

    #region Tick

    public void OnTick()
    {
        foreach (int adminId in _sessions.GetActiveAdminIds())
        {
            var session = _sessions.GetSession(adminId);
            if (session == null) continue;

            try
            {
                ProcessSession(adminId, session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnTick for admin {AdminId}, releasing session", adminId);
                _sessions.ReleaseSession(adminId);
            }
        }
    }

    private void ProcessSession(int adminId, GrabSession session)
    {
        var admin = Utilities.GetPlayerFromUserid(adminId);
        if (!IsPlayerValid(admin) || admin!.PlayerPawn.Value == null)
        {
            _sessions.ReleaseSession(adminId);
            return;
        }

        if (!session.Target.IsValid)
        {
            _sessions.ReleaseSession(adminId);
            admin.PrintToCenter("Цель отпущена");
            return;
        }

        var targetEntity = session.Target.ResolveEntity();
        var currentPos = session.Target.GetPosition();
        if (targetEntity == null || currentPos == null)
        {
            _sessions.ReleaseSession(adminId);
            admin.PrintToCenter("Цель отпущена");
            return;
        }

        var adminPawn = admin.PlayerPawn.Value!;

        ProcessInput(admin, session);
        Vector targetIdealPos = CalculateIdealPosition(adminPawn, session, currentPos);
        DetectAndCorrectCollision(adminPawn, session, currentPos, targetIdealPos);
        ApplyPhysics(session, targetEntity, currentPos, targetIdealPos);
        UpdateVisuals(session, adminPawn, targetEntity);
        _visualizer.UpdateHud(admin, session.Target.GetLabel(), session.Distance);

        session.LastTargetPosition = new Vector(currentPos.X, currentPos.Y, currentPos.Z);
    }

    #endregion

    #region Pipeline Steps

    private void ProcessInput(CCSPlayerController admin, GrabSession session)
    {
        var buttons = admin.Buttons;
        if ((buttons & PlayerButtons.Attack) != 0)
            session.Distance = Math.Clamp(session.Distance + _config.ButtonStep, _config.MinDistance, _config.MaxDistance);
        else if ((buttons & PlayerButtons.Attack2) != 0)
            session.Distance = Math.Clamp(session.Distance - _config.ButtonStep, _config.MinDistance, _config.MaxDistance);
    }

    private Vector CalculateIdealPosition(CCSPlayerPawn adminPawn, GrabSession session, Vector currentPos)
    {
        Vector eyePos = adminPawn.AbsOrigin! + new Vector(0, 0, adminPawn.ViewOffset.Z);
        Vector forward = VectorMath.GetForwardVector(adminPawn.EyeAngles);
        return eyePos + (forward * session.Distance);
    }

    private void DetectAndCorrectCollision(CCSPlayerPawn adminPawn, GrabSession session, Vector currentPos, Vector targetIdealPos)
    {
        if (session.LastTargetPosition == null) return;

        float movedDist = VectorMath.Distance(currentPos, session.LastTargetPosition);
        float distToIdeal = VectorMath.Distance(currentPos, targetIdealPos);

        if (distToIdeal > 10f && movedDist < _config.StuckThreshold)
        {
            Vector eyePos = adminPawn.AbsOrigin! + new Vector(0, 0, adminPawn.ViewOffset.Z);
            float actualDist = VectorMath.Distance(eyePos, currentPos);
            session.Distance = Math.Clamp(actualDist, _config.MinDistance, _config.MaxDistance);
        }
    }

    private void ApplyPhysics(GrabSession session, CBaseEntity targetEntity, Vector currentPos, Vector targetIdealPos)
    {
        // Пересчитываем ideal position после возможной коррекции дистанции
        if (session.Target is GrabTarget.Player)
            _physics.ApplyToPlayer(targetEntity.As<CCSPlayerPawn>(), currentPos, targetIdealPos);
        else
            _physics.ApplyToEntity(targetEntity, currentPos, targetIdealPos);
    }

    private void UpdateVisuals(GrabSession session, CCSPlayerPawn adminPawn, CBaseEntity targetEntity)
    {
        Vector beamStart = GrabSessionManager.GetBeamStartPosition(adminPawn);
        Vector? beamEnd = session.Target.GetBeamEndPosition();
        _visualizer.UpdateVisuals(session, beamStart, beamEnd, targetEntity);
    }

    #endregion

    #region Grab Start

    private void TryStartGrab(CCSPlayerController admin)
    {
        try
        {
            bool canPlayers = CanGrabPlayers(admin);
            bool canPhysics = CanGrabPhysics(admin);

            var result = _rayTrace.TraceForGrabbable(admin, canPlayers, canPhysics && _config.AllowGrabPhysicsObjects);
            if (!result.Hit || result.Target == null)
            {
                admin.PrintToCenter("Не удалось найти цель");
                return;
            }

            if (result.Target is GrabTarget.Player playerTarget && !playerTarget.IsValid)
                return;

            int adminId = admin.UserId!.Value;
            float distance = Math.Clamp(result.Distance, _config.MinDistance, _config.MaxDistance);
            var session = _sessions.CreateSession(adminId, result.Target, distance, admin.PlayerPawn.Value);

            admin.PrintToCenter($"Захвачен: {result.Target.GetLabel()}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TryStartGrab");
        }
    }

    #endregion

    #region Helpers

    private static bool IsPlayerValid(CCSPlayerController? p) =>
        p != null && p.IsValid && p.PawnIsAlive && p.Connected == PlayerConnectedState.PlayerConnected;

    private bool CanGrabPlayers(CCSPlayerController admin) =>
        !string.IsNullOrEmpty(_config.PermissionFlagPlayers)
        && AdminManager.PlayerHasPermissions(admin, _config.PermissionFlagPlayers);

    private bool CanGrabPhysics(CCSPlayerController admin) =>
        _config.PermissionFlagPhysics == null
        || (!string.IsNullOrEmpty(_config.PermissionFlagPhysics)
            && AdminManager.PlayerHasPermissions(admin, _config.PermissionFlagPhysics));

    #endregion
}
