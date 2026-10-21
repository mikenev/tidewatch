using UnityEngine;
namespace Tidewatch {
  /// <summary>A fixed build spot: "land" (towers) or "buoy" (defensive ships). Tap to open the build/upgrade menu.</summary>
  public class BuildSlot : MonoBehaviour {
    public string type; public int index; public Defense defense;
    SpriteRenderer ring, select;
    public void Init(string t, int i){
      type = t; index = i; name = $"Slot{i}_{t}";
      if (t == "buoy") Art.Make(transform, "slot_buoy", Art.Circle, new Vector2(0.7f, 0.7f), new Color(0.95f, 0.35f, 0.25f), 4);
      else Art.Make(transform, "slot_land", Art.Square, new Vector2(1.1f, 1.1f), new Color(0.72f, 0.6f, 0.4f), 4);
      select = Art.Make(transform, null, Art.Circle, new Vector2(1.5f, 1.5f), new Color(1f, 0.95f, 0.4f, 0.45f), 3);
      ring = Art.Make(transform, null, Art.Circle, Vector2.one, new Color(1f, 1f, 1f, 0.16f), 2);
      Show(false, 0f);
    }
    public void Show(bool on, float range){
      select.enabled = on; ring.enabled = on && range > 0f;
      if (range > 0f) ring.transform.localScale = Vector3.one * (range * 2f / Art.Circle.bounds.size.x);
    }
  }
}
