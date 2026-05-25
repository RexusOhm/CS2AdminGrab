using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CS2_Admin_Grab.Config;
using CS2_Admin_Grab.Models;
using CS2_Admin_Grab.Services;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CS2_Admin_Grab.Core;

/// <summary>
/// Централизованное управление состоянием всех сессий захвата.
/// Единственный словарь вместо семи; обратный индекс target→admin.
/// </summary>
public class GrabSessionManager
{
    private readonly AdminGrabConfig _config;
    private readonly IGrabVisualizer _visualizer;

    private readonly Dictionary<int, GrabSession> _sessions = new();
    private readonly Dictionary<int, int> _targetPlayerToAdmin = new(); // targetUserId → adminUserId

    // Буфер ключей для итерации без аллокаций ToList() каждый тик
    private readonly List<int> _keyBuffer = new();

    public GrabSessionManager(AdminGrabConfig config, IGrabVisualizer visualizer)
    {
        _config = config;
        _visualizer = visualizer;
    }

    public bool HasSession(int adminUserId) => _sessions.ContainsKey(adminUserId);

    public GrabSession? GetSession(int adminUserId) =>
        _sessions.TryGetValue(adminUserId, out var session) ? session : null;

    /// <summary>
    /// Возвращает снимок ключей для безопасной итерации.
    /// Переиспользует внутренний буфер для избежания аллокаций.
    /// </summary>
    public List<int> GetActiveAdminIds()
    {
        _keyBuffer.Clear();
        _keyBuffer.AddRange(_sessions.Keys);
        return _keyBuffer;
    }

    public GrabSession CreateSession(int adminUserId, GrabTarget target, float distance, CCSPlayerPawn? adminPawn)
    {
        float originalSpeed = 1f;
        float originalGravity = 1f;

        if (target is GrabTarget.Player playerTarget)
        {
            var controller = playerTarget.ResolveController();
            if (controller?.PlayerPawn.Value is { } targetPawn)
            {
                originalSpeed = targetPawn.VelocityModifier;
                originalGravity = targetPawn.GravityScale;

                targetPawn.VelocityModifier = 0.0f;
                targetPawn.GravityScale = 0f;
                targetPawn.Teleport(null, null, new Vector(0, 0, 0));
            }

            _targetPlayerToAdmin[playerTarget.UserId] = adminUserId;
        }

        var session = new GrabSession(adminUserId, target, distance)
        {
            OriginalSpeed = originalSpeed,
            OriginalGravity = originalGravity
        };

        _sessions[adminUserId] = session;

        if (adminPawn != null)
        {
            Vector beamStart = GetBeamStartPosition(adminPawn);
            _visualizer.CreateVisuals(session, beamStart);
        }

        return session;
    }

    public void ReleaseSession(int adminUserId)
    {
        if (!_sessions.Remove(adminUserId, out var session)) return;
        
        RestoreTargetState(session);
        _visualizer.DestroyVisuals(session);
        
        var admin = Utilities.GetPlayerFromUserid(adminUserId);
        if (admin != null) MenuManager.CloseActiveMenu(admin);
    }

    public void ReleaseAll()
    {
        foreach (var kvp in _sessions)
        {
            RestoreTargetState(kvp.Value);
            _visualizer.DestroyVisuals(kvp.Value);
        }
        _sessions.Clear();
        _targetPlayerToAdmin.Clear();
    }

    /// <summary>
    /// Находит и освобождает сессию, где данный игрок является целью.
    /// O(1) благодаря обратному индексу.
    /// </summary>
    public void ReleaseByTargetPlayer(int targetUserId)
    {
        if (!_targetPlayerToAdmin.Remove(targetUserId, out int adminUserId)) return;

        var admin = Utilities.GetPlayerFromUserid(adminUserId);
        if (admin != null && admin.IsValid)
            ReleaseSession(adminUserId);
        else
            ForceRemoveSession(adminUserId);
    }

    private void RestoreTargetState(GrabSession session)
    {
        if (session.Target is GrabTarget.Player playerTarget)
        {
            var controller = playerTarget.ResolveController();
            if (controller?.PlayerPawn.Value is { } pawn)
            {
                pawn.VelocityModifier = session.OriginalSpeed;
                pawn.GravityScale = session.OriginalGravity;
            }

            _targetPlayerToAdmin.Remove(playerTarget.UserId);
        }
    }

    private void ForceRemoveSession(int adminUserId)
    {
        if (_sessions.Remove(adminUserId, out var session))
        {
            RestoreTargetState(session);
            _visualizer.DestroyVisuals(session);
        }
    }

    public static Vector GetBeamStartPosition(CCSPlayerPawn pawn) =>
        pawn.AbsOrigin! + new Vector(0, 0, pawn.ViewOffset.Z - 10f);
}
