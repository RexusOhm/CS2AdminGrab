using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
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
    private readonly AdminGrabPlugin _plugin;

    public GrabManager(
        IRayTraceService rayTrace,
        IGrabPhysics physics,
        IGrabVisualizer visualizer,
        GrabSessionManager sessions,
        AdminGrabConfig config,
        ILogger logger,
        AdminGrabPlugin plugin)
    {
        _rayTrace = rayTrace;
        _physics = physics;
        _visualizer = visualizer;
        _sessions = sessions;
        _config = config;
        _logger = logger;
        _plugin = plugin;
    }
    
    #region ThrowTarget

    private bool IsHoldingTarget(int adminUserId)
    {
        return _sessions.HasSession(adminUserId);
    }

    /// <summary>
    /// Бросает захваченную цель вперед с импульсом и отпускает сессию.
    /// </summary>
    private void ThrowTarget(CCSPlayerController admin)
    {
        Server.PrintToChatAll("DROP_ThrowTarget");
        
        int adminId = admin.UserId ?? -1;
        var session = _sessions.GetSession(adminId);
        if (session == null || admin.PlayerPawn.Value == null) return;

        var targetEntity = session.Target.ResolveEntity();
        if (targetEntity != null && targetEntity.IsValid)
        {
            // Рассчитываем вектор направления взгляда администратора
            Vector forward = VectorMath.GetForwardVector(admin.PlayerPawn.Value.EyeAngles);
            
            // Задаем силу броска
            float throwForce = _config.ThrowForce;
            Vector throwVelocity = forward * throwForce;
            throwVelocity.Z += _config.ThrowForceVertical; // Добавляем импульс вверх для красивой параболы

            if (session.Target is GrabTarget.Player playerTarget)
            {
                var targetController = playerTarget.ResolveController();
                if (targetController?.PlayerPawn.Value is { } targetPawn)
                {
                    // Сначала восстанавливаем гравитацию/скорость, чтобы игрок полетел физично
                    targetPawn.VelocityModifier = session.OriginalSpeed;
                    targetPawn.GravityScale = session.OriginalGravity;

                    targetPawn.Teleport(null, null, throwVelocity);
                }
            }
            else
            {
                targetEntity.Teleport(null, null, throwVelocity);
            }
        }

        _sessions.ReleaseSession(adminId);
        admin.PrintToCenter("Цель брошена!");
    }
    
    #endregion

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
    
    public HookResult OnDropWeapon(DynamicHook hook)
    {
        Server.PrintToChatAll("DROP_onDrop");

        var controller = hook.GetParam<CCSPlayerController>(0);  // теперь это контроллер
        if (controller == null || !controller.IsValid || controller.UserId == null)
            return HookResult.Continue;

        // Если у этого администратора активна сессия захвата
        if (IsHoldingTarget(controller.UserId.Value))
        {
            ThrowTarget(controller);
            return HookResult.Handled; // блокируем выброс оружия
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

        // Обработка ввода (включая кнопку удара E)
        ProcessInput(admin, session);

        Vector targetIdealPos = CalculateIdealPosition(adminPawn, session, currentPos);
        DetectAndCorrectCollision(adminPawn, session, currentPos, targetIdealPos);
        ApplyPhysics(session, targetEntity, currentPos, targetIdealPos);
        UpdateVisuals(session, adminPawn, targetEntity);

        // Расчёт расстояний для HUD-меню
        Vector eyePos = adminPawn.AbsOrigin! + new Vector(0, 0, adminPawn.ViewOffset.Z);
        float currentDistance = VectorMath.Distance(eyePos, currentPos);

        // Сборка информации о классе и имени
        string classEntity = targetEntity.DesignerName;
        string targetName = "";

        if (session.Target is GrabTarget.Player playerTarget)
        {
            var targetController = playerTarget.ResolveController();
            if (targetController != null && targetController.IsValid)
            {
                targetName = targetController.PlayerName;
            }
        }
        else
        {
            // Для обычных энтити берём Targetname, если он задан
            targetName = targetEntity.As<CEntityInstance>().Entity?.Name ?? "";
        }

        // Вызов HUD-меню через абстракцию IGrabVisualizer. Никакого HTML внутри менеджера!
        _visualizer.UpdateHud(admin, classEntity, targetName, currentDistance, session.Distance, _plugin);

        session.LastTargetPosition = new Vector(currentPos.X, currentPos.Y, currentPos.Z);
    }

    #endregion

    #region Pipeline Steps

    private void ProcessInput(CCSPlayerController admin, GrabSession session)
    {
        var buttons = admin.Buttons;

        // Изменение дистанции (на ЛКМ / ПКМ)
        if ((buttons & PlayerButtons.Attack) != 0)
            session.Distance = Math.Clamp(session.Distance + _config.ButtonStep, _config.MinDistance, _config.MaxDistance);
        else if ((buttons & PlayerButtons.Attack2) != 0)
            session.Distance = Math.Clamp(session.Distance - _config.ButtonStep, _config.MinDistance, _config.MaxDistance);

        // Обработка кнопки E (PlayerButtons.Use) для удара
        if ((buttons & PlayerButtons.Use) != 0)
        {
            float currentTime = Server.CurrentTime;
            if (currentTime - session.LastHitTime >= 0.5f)
            {
                session.LastHitTime = currentTime;
                TryDamageTarget(admin, session);
            }
        }
    }
    
    private void TryDamageTarget(CCSPlayerController admin, GrabSession session)
    {
        if (session.Target is GrabTarget.Player playerTarget)
        {
            var targetController = playerTarget.ResolveController();
            if (targetController != null && targetController.IsValid && targetController.PlayerPawn.Value != null)
            {
                var targetPawn = targetController.PlayerPawn.Value;
                int currentHp = targetPawn.Health;
                int newHp = Math.Max(0, currentHp - 5);
                int dmg = 5;
                
                HitPlayer(admin,targetController, dmg);
                // Сообщения игрокам
                targetController.PrintToCenter($"Вас ударил админ!");
                admin.PrintToCenter($"Вы ударили игрока {targetController.PlayerName}. Осталось HP: {newHp}");
                
                // Если здоровье опустилось до 0, убиваем игрока
                if (newHp <= 0)
                {
                    targetPawn.CommitSuicide(false, true);
                    _sessions.ReleaseSession(admin.UserId ?? -1);
                }
            }
        }
        else
        {
            admin.PrintToCenter("Эту цель нельзя ударить (не является игроком)");
        }
    }
    private static int PtrSize => Schema.GetClassSize("CTakeDamageInfo");
    private static int PtrResultSize => Schema.GetClassSize("CTakeDamageResult");

    private void HitPlayer(CCSPlayerController attacker, CCSPlayerController victim, int damage)
    {
        //todo сделать _sessions.ReleaseSession(admin.UserId ?? -1); когда хп жертвы = 0;
        if (victim.Pawn.Value == null) return;
        var oldHealth = victim.Pawn.Value.Health;

        var ptr = Marshal.AllocHGlobal(PtrSize);

        for (var i = 0; i < PtrSize; i++)
            Marshal.WriteByte(ptr, i, 0);

        var damageInfo = new CTakeDamageInfo(ptr);
        var attackerInfo = new CAttackerInfo(attacker);

        Marshal.StructureToPtr(attackerInfo, new IntPtr(ptr.ToInt64() + 0x88), false);

        if (attacker.Team == victim.Team)
            attacker = victim;

        Schema.SetSchemaValue(damageInfo.Handle, "CTakeDamageInfo", "m_hInflictor", attacker.PawnIsAlive ? attacker.Pawn.Raw : attacker.PlayerPawn.Raw);
        Schema.SetSchemaValue(damageInfo.Handle, "CTakeDamageInfo", "m_hAttacker", attacker.Pawn.Raw);

        damageInfo.Damage = damage;
        damageInfo.BitsDamageType = DamageTypes_t.DMG_BLAST_SURFACE;

        var ptr2 = Marshal.AllocHGlobal(PtrResultSize);

        for (var i = 0; i < PtrResultSize; i++)
            Marshal.WriteByte(ptr2, i, 0);

        var damageResult = new CTakeDamageResult(ptr2);
        Schema.SetSchemaValue(damageResult.Handle, "CTakeDamageResult", "m_pOriginatingInfo", damageInfo.Handle);

        damageResult.HealthLost = damage;
        damageResult.DamageDealt = damage;
        damageResult.PreModifiedDamage = damage;
        damageResult.TotalledHealthLost = damage;
        damageResult.TotalledDamageDealt = damage;

        VirtualFunctions.CBaseEntity_TakeDamageOld.Invoke(victim.Pawn.Value, damageInfo, damageResult);
        Marshal.FreeHGlobal(ptr);
        Marshal.FreeHGlobal(ptr2);
    }
    
    [StructLayout(LayoutKind.Explicit)]
    private struct CAttackerInfo
    {
        public CAttackerInfo(CEntityInstance attacker)
        {
            NeedInit = false;
            IsWorld = true;
            Attacker = attacker.EntityHandle.Raw;
            if (attacker.DesignerName != "cs_player_controller") return;

            var controller = attacker.As<CCSPlayerController>();
            IsWorld = false;
            IsPawn = true;
            AttackerUserId = (ushort)(controller.UserId ?? 0xFFFF);
            TeamNum = controller.TeamNum;
            TeamChecked = controller.TeamNum;
        }

        [FieldOffset(0x0)] public bool NeedInit = true;
        [FieldOffset(0x1)] public bool IsPawn = false;
        [FieldOffset(0x2)] public bool IsWorld = false;

        [FieldOffset(0x4)]
        public UInt32 Attacker;

        [FieldOffset(0x8)]
        public ushort AttackerUserId;

        [FieldOffset(0x0C)] public int TeamChecked = -1;
        [FieldOffset(0x10)] public int TeamNum = -1;
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

            if (result.Target is GrabTarget.Player { IsValid: false })
                return;

            var adminId = admin.UserId!.Value;
            var distance = Math.Clamp(result.Distance, _config.MinDistance, _config.MaxDistance);
            _sessions.CreateSession(adminId, result.Target, distance, admin.PlayerPawn.Value);

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
        p != null && p.IsValid && p.PawnIsAlive && p.Connected == PlayerConnectedState.Connected;

    private bool CanGrabPlayers(CCSPlayerController admin) =>
        !string.IsNullOrEmpty(_config.PermissionFlagPlayers)
        && AdminManager.PlayerHasPermissions(admin, _config.PermissionFlagPlayers);

    private bool CanGrabPhysics(CCSPlayerController admin) =>
        _config.PermissionFlagPhysics == null
        || (!string.IsNullOrEmpty(_config.PermissionFlagPhysics)
            && AdminManager.PlayerHasPermissions(admin, _config.PermissionFlagPhysics));

    #endregion
}
