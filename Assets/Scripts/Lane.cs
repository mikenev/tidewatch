using UnityEngine;
namespace Tidewatch {
  /// <summary>A waypoint sea lane, tagged with the ship sizes that may use it. Positions are by distance travelled.</summary>
  public class Lane {
    public readonly string id; public readonly string[] sizes; public readonly Vector2[] points; readonly float[] cum;
    public float Length { get; }
    public Lane(LaneDef d){
      id = d.id; sizes = d.sizes; points = d.points; cum = new float[points.Length];
      for (int i = 1; i < points.Length; i++) cum[i] = cum[i - 1] + Vector2.Distance(points[i - 1], points[i]);
      Length = cum[points.Length - 1];
    }
    public static bool SizeAllowed(string[] sizes, string size) => sizes == null || sizes.Length == 0 || System.Array.IndexOf(sizes, size) >= 0;
    public bool Allows(string size) => SizeAllowed(sizes, size);
    int Seg(float d){ for (int i = 0; i < points.Length - 2; i++) if (d <= cum[i + 1]) return i; return points.Length - 2; }
    public Vector2 Pos(float d){
      d = Mathf.Clamp(d, 0, Length); int i = Seg(d); float len = cum[i + 1] - cum[i];
      return Vector2.Lerp(points[i], points[i + 1], len > 1e-5f ? (d - cum[i]) / len : 0f);
    }
    public Vector2 Dir(float d){ int i = Seg(Mathf.Clamp(d, 0, Length)); return (points[i + 1] - points[i]).normalized; }
  }
}
