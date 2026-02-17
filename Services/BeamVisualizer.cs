using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_Admin_Grab.Models;
using CS2_Admin_Grab.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CS2_Admin_Grab.Services;

/// <summary>
/// Визуализация захвата через env_beam сущности.
/// Управляет жизненным циклом beam-лучей, wireframe box и HUD.
/// </summary>
public class BeamVisualizer : IGrabVisualizer
{
    private static readonly QAngle AngleZero = new(0, 0, 0);
    private static readonly Vector VecZero = new(0, 0, 0);
    private static readonly Color GrabBeamColor = Color.Cyan;
    private static readonly Color BoxBeamColor = Color.Lime;
    private const float GrabBeamWidth = 2f;
    private const float BoxBeamWidth = 0.25f;
    private const int BoxEdgeCount = 12;

    // Пре-аллоцированный массив для вершин bounding box (избегаем аллокаций в OnTick)
    private readonly Vector[] _boxVertices = new Vector[8];

    public void CreateVisuals(GrabSession session, Vector beamStart)
    {
        session.GrabBeam = CreateBeamEntity(beamStart, beamStart, GrabBeamColor, GrabBeamWidth);
        //SetTargetGlow(session);//todo заменить на создание и удаление пропов!
    }

    private static void SetTargetGlow(GrabSession session, bool isActive = true)
    {
        var ent = session.Target.ResolveEntity()?.As<CDynamicProp>();
        if (ent == null) return;
        ent.Glow.GlowColorOverride = Color.Lime;
        ent.Glow.GlowRange = 15000;
        ent.Glow.GlowTeam = -1;
        ent.Glow.GlowType = 3;
        ent.Glow.GlowRangeMin = 40;
        Utilities.SetStateChanged(ent, "CBaseModelEntity", "m_Glow");
    }

    public void UpdateVisuals(GrabSession session, Vector beamStart, Vector? beamEnd, CBaseEntity? targetEntity)
    {
        UpdateGrabBeam(session, beamStart, beamEnd);

        if (targetEntity != null)
            UpdateBoxVisualization(session, targetEntity);
    }

    public void DestroyVisuals(GrabSession session)
    {
        KillBeam(session.GrabBeam);
        session.GrabBeam = null;

        if (session.BoxBeams != null)
        {
            foreach (var beam in session.BoxBeams)
                KillBeam(beam);
            session.BoxBeams = null;
        }
    }

    public void UpdateHud(CCSPlayerController admin, string label, float distance) =>
        admin.PrintToCenter($"Цель: {label}\nДист: {MathF.Round(distance)}");

    #region Beam Management

    private void UpdateGrabBeam(GrabSession session, Vector beamStart, Vector? beamEnd)
    {
        if (beamEnd == null) return;

        if (session.GrabBeam == null || !session.GrabBeam.IsValid)
            session.GrabBeam = CreateBeamEntity(beamStart, beamEnd, GrabBeamColor, GrabBeamWidth);
        else
            TeleportBeam(session.GrabBeam, beamStart, beamEnd);
    }

    private void UpdateBoxVisualization(GrabSession session, CBaseEntity entity)
    {
        if (entity.Collision == null) return;

        if (!AreBoxBeamsValid(session))
        {
            DestroyBoxBeams(session);
            session.BoxBeams = CreateBoxBeams(entity);
        }

        RenderBox(session.BoxBeams!, entity);
    }

    private bool AreBoxBeamsValid(GrabSession session)
    {
        if (session.BoxBeams == null || session.BoxBeams.Count != BoxEdgeCount)
            return false;

        for (int i = 0; i < session.BoxBeams.Count; i++)
        {
            if (!session.BoxBeams[i].IsValid) return false;
        }
        return true;
    }

    private List<CEnvBeam> CreateBoxBeams(CBaseEntity entity)
    {
        var beams = new List<CEnvBeam>(BoxEdgeCount);
        Vector origin = entity.AbsOrigin!;
        for (int i = 0; i < BoxEdgeCount; i++)
        {
            var b = CreateBeamEntity(origin, origin, BoxBeamColor, BoxBeamWidth);
            if (b != null) beams.Add(b);
        }
        return beams;
    }

    private void DestroyBoxBeams(GrabSession session)
    {
        if (session.BoxBeams == null) return;
        foreach (var b in session.BoxBeams) KillBeam(b);
        session.BoxBeams = null;
    }

    private void RenderBox(List<CEnvBeam> beams, CBaseEntity entity)
    {
        if (beams.Count < BoxEdgeCount) return;

        Vector mins = entity.Collision!.Mins;
        Vector maxs = entity.Collision.Maxs;
        Vector origin = entity.AbsOrigin!;
        QAngle angles = entity.AbsRotation ?? new QAngle(0, 0, 0);

        VectorMath.GetAngleVectors(angles, out Vector f, out Vector l, out Vector u);

        // Рассчитываем 8 вершин OBB, переиспользуя массив
        int idx = 0;
        for (int z = 0; z < 2; z++)
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                    _boxVertices[idx++] = VectorMath.TransformPoint(
                        new Vector(
                            x == 0 ? mins.X : maxs.X,
                            y == 0 ? mins.Y : maxs.Y,
                            z == 0 ? mins.Z : maxs.Z),
                        origin, f, l, u);

        // Нижняя грань
        TeleportBeam(beams[0], _boxVertices[0], _boxVertices[1]);
        TeleportBeam(beams[1], _boxVertices[1], _boxVertices[3]);
        TeleportBeam(beams[2], _boxVertices[3], _boxVertices[2]);
        TeleportBeam(beams[3], _boxVertices[2], _boxVertices[0]);
        // Верхняя грань
        TeleportBeam(beams[4], _boxVertices[4], _boxVertices[5]);
        TeleportBeam(beams[5], _boxVertices[5], _boxVertices[7]);
        TeleportBeam(beams[6], _boxVertices[7], _boxVertices[6]);
        TeleportBeam(beams[7], _boxVertices[6], _boxVertices[4]);
        // Вертикальные рёбра
        TeleportBeam(beams[8], _boxVertices[0], _boxVertices[4]);
        TeleportBeam(beams[9], _boxVertices[1], _boxVertices[5]);
        TeleportBeam(beams[10], _boxVertices[2], _boxVertices[6]);
        TeleportBeam(beams[11], _boxVertices[3], _boxVertices[7]);
    }

    #endregion

    #region Low-level Beam Operations

    private static CEnvBeam? CreateBeamEntity(Vector start, Vector end, Color color, float width)
    {
        var beam = Utilities.CreateEntityByName<CEnvBeam>("env_beam");
        if (beam == null) return null;

        beam.Render = color;
        beam.Width = width;
        beam.Teleport(start, AngleZero, VecZero);
        beam.EndPos.X = end.X;
        beam.EndPos.Y = end.Y;
        beam.EndPos.Z = end.Z;
        beam.DispatchSpawn();
        return beam;
    }

    private static void TeleportBeam(CBeam? beam, Vector start, Vector end)
    {
        if (beam == null || !beam.IsValid) return;

        beam.Teleport(start, AngleZero, VecZero);
        beam.EndPos.X = end.X;
        beam.EndPos.Y = end.Y;
        beam.EndPos.Z = end.Z;
        Utilities.SetStateChanged(beam, "CBeam", "m_vecEndPos");
    }

    private static void KillBeam(CEnvBeam? beam)
    {
        if (beam != null && beam.IsValid)
            beam.AcceptInput("Kill");
    }

    #endregion
}
