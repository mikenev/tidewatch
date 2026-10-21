using UnityEngine;
namespace Tidewatch {
  /// <summary>Builds the island: background art if present (Resources/Sprites/&lt;background&gt;), else placeholder sea lane,
  /// sand islands and port; then the city and the build slots from config.</summary>
  public static class MapBuilder {
    public static void Build(Game g){
      var isl = g.Island; var root = new GameObject("Map").transform;
      if (Art.Has(isl.background)) Art.Make(root, isl.background, Art.Square, Vector2.one, Color.white, -20);
      else {
        var shallows = new Color(0.33f, 0.78f, 0.84f);
        foreach (var lane in g.Lanes)
          for (float d = 0; d <= lane.Length; d += 0.25f) Art.Make(root, null, Art.Circle, Vector2.one * 0.95f, shallows, -15, lane.Pos(d));
        if (isl.land != null) foreach (var l in isl.land){
          Art.Make(root, null, Art.Circle, Vector2.one * (l.radius * 2f + 0.3f), new Color(0.55f, 0.85f, 0.88f), -12, l.pos);
          Art.Make(root, null, Art.Circle, Vector2.one * l.radius * 2f, new Color(0.96f, 0.9f, 0.72f), -11, l.pos);
          Art.Make(root, null, Art.Circle, Vector2.one * l.radius * 1.2f, new Color(0.45f, 0.72f, 0.35f), -10, l.pos + new Vector2(0.15f, 0.2f));
        }
        float shore = isl.harbor.x - 0.4f;   // mainland strip behind the harbor
        Art.Make(root, null, Art.Square, new Vector2(shore + 12f, 12f), new Color(0.96f, 0.9f, 0.72f), -11, new Vector2((shore - 12f) * 0.5f, 0f));
      }
      var city = new GameObject("City").transform; city.SetParent(root, false); city.position = isl.harbor + new Vector2(-1.0f, 0f);
      if (Art.Has("city_harbor")) Art.Make(city, "city_harbor", Art.Square, Vector2.one, Color.white, -5);
      else {
        Art.Make(city, null, Art.Square, new Vector2(1.6f, 3.4f), new Color(0.55f, 0.5f, 0.45f), -6);    // walls
        Art.Make(city, null, Art.Square, new Vector2(1.3f, 3.1f), new Color(0.85f, 0.8f, 0.7f), -5);
        for (int i = 0; i < 4; i++) Art.Make(city, null, Art.Square, new Vector2(0.45f, 0.45f), i % 2 == 0 ? new Color(0.8f, 0.3f, 0.25f) : new Color(0.3f, 0.45f, 0.75f), -4, new Vector2(i % 2 == 0 ? -0.3f : 0.3f, 1.05f - i * 0.7f));
        Art.Make(city, null, Art.Square, new Vector2(0.9f, 0.3f), new Color(0.5f, 0.35f, 0.2f), -4, new Vector2(0.9f, 0f));   // pier
      }
      for (int i = 0; i < isl.slots.Length; i++){
        var go = new GameObject(); go.transform.SetParent(root, false); go.transform.position = isl.slots[i].pos;
        var s = go.AddComponent<BuildSlot>(); s.Init(isl.slots[i].type, i); g.Slots.Add(s);
      }
    }
  }
}
