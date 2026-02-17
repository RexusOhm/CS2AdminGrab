using CounterStrikeSharp.API.Modules.Utils;

namespace CS2_Admin_Grab.Utils;

/// <summary>
/// Единственный источник математических функций для работы с векторами и углами.
/// Все тригонометрические операции используют MathF для избежания кастов double→float.
/// </summary>
public static class VectorMath
{
    private const float Deg2Rad = MathF.PI / 180f;

    /// <summary>
    /// Конвертирует QAngle в нормализованный вектор направления "вперёд".
    /// </summary>
    public static Vector GetForwardVector(QAngle angles)
    {
        float pitch = angles.X * Deg2Rad;
        float yaw = angles.Y * Deg2Rad;

        float cp = MathF.Cos(pitch);

        return Normalize(new Vector(
            cp * MathF.Cos(yaw),
            cp * MathF.Sin(yaw),
            -MathF.Sin(pitch)
        ));
    }

    /// <summary>
    /// Рассчитывает три базисных вектора (Forward, Left, Up) на основе QAngle.
    /// Стандартная для Source Engine схема: Pitch (X), Yaw (Y), Roll (Z).
    /// Left направлен влево в системе координат Source Engine.
    /// </summary>
    public static void GetAngleVectors(QAngle angles, out Vector forward, out Vector left, out Vector up)
    {
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

        left = new Vector(
            sr * sp * cy - cr * sy,
            sr * sp * sy + cr * cy,
            sr * cp
        );

        up = new Vector(
            cr * sp * cy + sr * sy,
            cr * sp * sy - sr * cy,
            cr * cp
        );
    }

    /// <summary>
    /// Переводит точку из локальных координат объекта в мировые с учётом вращения и позиции.
    /// </summary>
    public static Vector TransformPoint(Vector localPoint, Vector origin, Vector forward, Vector left, Vector up)
    {
        return new Vector(
            origin.X + forward.X * localPoint.X + left.X * localPoint.Y + up.X * localPoint.Z,
            origin.Y + forward.Y * localPoint.X + left.Y * localPoint.Y + up.Y * localPoint.Z,
            origin.Z + forward.Z * localPoint.X + left.Z * localPoint.Y + up.Z * localPoint.Z
        );
    }

    /// <summary>
    /// Ограничивает длину вектора заданным значением.
    /// </summary>
    public static Vector ClampLength(Vector v, float max)
    {
        float lenSq = LengthSquared(v);
        if (lenSq <= max * max) return v;

        float scale = max / MathF.Sqrt(lenSq);
        return new Vector(v.X * scale, v.Y * scale, v.Z * scale);
    }

    public static float Dot(Vector a, Vector b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static float Length(Vector v) =>
        MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    public static float LengthSquared(Vector v) =>
        v.X * v.X + v.Y * v.Y + v.Z * v.Z;

    public static Vector Normalize(Vector v)
    {
        float len = Length(v);
        return len > 1e-6f
            ? new Vector(v.X / len, v.Y / len, v.Z / len)
            : new Vector(0, 0, 0);
    }

    public static float Distance(Vector a, Vector b)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        float dz = b.Z - a.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
