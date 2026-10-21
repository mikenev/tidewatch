using System.Collections.Generic;
using UnityEngine;
namespace Tidewatch {
  /// <summary>Sprites by name from Resources/Sprites (Art Dev drops files there, see Assets/ART_AUDIO_LIST.md), with generated
  /// placeholder shapes when a file is missing. Real art is shown at its authored size (100 px per unit) and untinted.</summary>
  public static class Art {
    static readonly Dictionary<string, Sprite> cache = new();
    static Sprite circle, square, hull;
    public static Sprite Circle => circle ? circle : circle = Gen(64, 64, (u, v) => Edge(1f - Mathf.Sqrt(u * u + v * v), 64));
    public static Sprite Square => square ? square : square = Gen(8, 8, (u, v) => 1f);
    /// <summary>Top-down hull, bow toward +X.</summary>
    public static Sprite Hull => hull ? hull : hull = Gen(96, 48, (u, v) => {
      float x = (u + 1f) * 0.5f;                                     // 0 stern .. 1 bow
      float half = x < 0.55f ? 1f - Mathf.Pow(Mathf.Max(0, 0.12f - x) / 0.12f, 2f) * 0.5f : Mathf.Sqrt(Mathf.Max(0, 1f - Mathf.Pow((x - 0.55f) / 0.45f, 2f)));
      return Edge(half * 0.95f - Mathf.Abs(v), 48);
    });
    static float Edge(float d, int px) => Mathf.Clamp01(d * px * 0.5f);
    static Sprite Gen(int w, int h, System.Func<float, float, float> alpha){
      var t = new Texture2D(w, h, TextureFormat.RGBA32, false){ filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
      var px = new Color32[w * h];
      for (int y = 0; y < h; y++) for (int x = 0; x < w; x++){
        float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
        px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(u, v)) * 255));
      }
      t.SetPixels32(px); t.Apply(false, true);
      t.hideFlags = HideFlags.DontUnloadUnusedAsset;   // statically cached: survive scene reloads (restart)
      var sp = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w); sp.hideFlags = HideFlags.DontUnloadUnusedAsset;
      return sp;
    }
    public static Sprite Load(string name){
      if (string.IsNullOrEmpty(name)) return null;
      if (!cache.TryGetValue(name, out var s)){ s = Resources.Load<Sprite>("Sprites/" + name); cache[name] = s; }
      return s;
    }
    public static bool Has(string name) => Load(name);
    /// <summary>Child sprite: the named art if present, else `fallback` tinted and scaled to `size` world units.</summary>
    public static SpriteRenderer Make(Transform parent, string artName, Sprite fallback, Vector2 size, Color tint, int order, Vector2 localPos = default){
      var go = new GameObject(artName ?? "Shape"); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
      var sr = go.AddComponent<SpriteRenderer>(); var real = Load(artName);
      if (real){ sr.sprite = real; sr.color = Color.white; }
      else { sr.sprite = fallback; sr.color = tint; var b = fallback.bounds.size; go.transform.localScale = new Vector3(size.x / b.x, size.y / b.y, 1f); }
      sr.sortingOrder = order; return sr;
    }
    public static Color Hex(string hex, Color fallback) => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
  }
}
