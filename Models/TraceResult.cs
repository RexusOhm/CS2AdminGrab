namespace CS2_Admin_Grab.Models;

/// <summary>
/// Результат трассировки луча.
/// Заменяет 5-элементный value tuple на типизированную модель.
/// </summary>
public class TraceResult
{
    public static readonly TraceResult Miss = new() { Hit = false };

    public bool Hit { get; init; }
    public float Distance { get; init; }
    public GrabTarget? Target { get; init; }
}
