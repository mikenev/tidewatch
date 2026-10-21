using System.Collections.Generic;
using UnityEngine;
namespace Tidewatch {
  /// <summary>Storm Isle night look: a dark overlay, tiled animated rain (fx_rain, 4 frames) and lightning strikes
  /// (fx_lightning, 4 frames, plus a screen flash) every few seconds. Generated placeholders when the art is missing.
  /// Runs on scaled time, so it freezes with pause and the end screens.</summary>
  public class Weather : MonoBehaviour {
    const float W = 19.2f, H = 10.8f, Fps = 12f;
    Sprite[] rain, bolt; readonly List<SpriteRenderer> drops = new(); SpriteRenderer flash, strike;
    float t, nextStrike = 3f, strikeT = -1f;
    public int Strikes { get; private set; }

    public static Weather Create(Transform parent){
      var go = new GameObject("Weather"); go.transform.SetParent(parent, false); return go.AddComponent<Weather>();
    }
    void Start(){
      rain = Art.Frames("fx_rain"); bolt = Art.Frames("fx_lightning");
      Art.Make(transform, null, Art.Square, new Vector2(W + 2f, H + 2f), new Color(0.04f, 0.06f, 0.16f, 0.28f), 40);   // night
      if (rain.Length > 0){
        float tile = rain[0].bounds.size.x;
        for (float x = -W / 2 + tile / 2; x < W / 2 + tile; x += tile)
          for (float y = -H / 2 + tile / 2; y < H / 2 + tile; y += tile){
            var sr = Art.Make(transform, null, rain[0], Vector2.one * tile, new Color(1, 1, 1, 0.55f), 41, new Vector2(x, y)); sr.sprite = rain[0]; drops.Add(sr);
          }
      } else {   // placeholder rain: thin streaks
        var rnd = new System.Random(7);
        for (int i = 0; i < 90; i++)
          drops.Add(Art.Make(transform, null, Art.Square, new Vector2(0.025f, 0.45f), new Color(0.75f, 0.85f, 1f, 0.35f), 41,
            new Vector2((float)rnd.NextDouble() * W - W / 2, (float)rnd.NextDouble() * H - H / 2)));
        foreach (var d in drops) d.transform.rotation = Quaternion.Euler(0, 0, 12f);
      }
      flash = Art.Make(transform, null, Art.Square, new Vector2(W + 2f, H + 2f), new Color(0.85f, 0.9f, 1f, 0f), 43);
      strike = Art.Make(transform, null, bolt.Length > 0 ? bolt[0] : Art.Square, bolt.Length > 0 ? (Vector2)bolt[0].bounds.size * 2f : new Vector2(0.12f, 4f), Color.white, 42);
      if (bolt.Length > 0) strike.sprite = bolt[0];
      strike.enabled = false;
      Debug.Log($"[TW] weather night rainFrames={rain.Length} lightningFrames={bolt.Length}");
    }
    void Update(){
      float dt = Time.deltaTime; if (dt <= 0f) return;
      t += dt;
      if (rain.Length > 0){ var f = rain[(int)(t * Fps) % rain.Length]; foreach (var d in drops) d.sprite = f; }
      else foreach (var d in drops){
        var p = d.transform.localPosition; p.y -= 9f * dt; p.x -= 1.9f * dt;
        if (p.y < -H / 2 - 0.5f){ p.y += H + 1f; p.x += 2.5f; } if (p.x < -W / 2 - 0.5f) p.x += W + 1f;
        d.transform.localPosition = p;
      }
      nextStrike -= dt;
      if (nextStrike <= 0f){
        nextStrike = Random.Range(6f, 11f); strikeT = 0f; Strikes++;
        strike.transform.localPosition = new Vector2(Random.Range(-7f, 7f), Random.Range(1f, 3f)); strike.enabled = true;
        GameEvents.Play("thunder");
        Debug.Log($"[TW] lightning strike {Strikes}");
      }
      if (strikeT >= 0f){
        strikeT += dt;
        flash.color = new Color(0.85f, 0.9f, 1f, Mathf.Clamp01(0.45f - strikeT * 1.6f));
        if (bolt.Length > 0) strike.sprite = bolt[Mathf.Min(bolt.Length - 1, (int)(strikeT * Fps))];
        if (strikeT > 0.35f){ strike.enabled = false; flash.color = new Color(1, 1, 1, 0); strikeT = -1f; }
      }
    }
  }

  /// <summary>The fog bank: soft drifting grey blobs drawn over the ships (fog isn't painted into the background).</summary>
  public class FogBank : MonoBehaviour {
    readonly List<Transform> puffs = new(); readonly List<Vector2> home = new(); float t;
    public static void Create(Transform parent, Vector2 pos, float radius){
      var go = new GameObject("FogBank"); go.transform.SetParent(parent, false); go.transform.position = pos;
      var fb = go.AddComponent<FogBank>(); var rnd = new System.Random(3);
      Art.Make(go.transform, null, Art.Circle, Vector2.one * radius * 2.1f, new Color(0.72f, 0.76f, 0.82f, 0.45f), 30);
      for (int i = 0; i < 9; i++){
        float a = i / 9f * Mathf.PI * 2f, r = radius * (0.35f + 0.4f * (float)rnd.NextDouble());
        var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        var sr = Art.Make(go.transform, null, Art.Circle, Vector2.one * radius * (0.8f + 0.4f * (float)rnd.NextDouble()), new Color(0.8f, 0.83f, 0.88f, 0.3f), 31, p);
        fb.puffs.Add(sr.transform); fb.home.Add(p);
      }
    }
    void Update(){
      t += Time.deltaTime;
      for (int i = 0; i < puffs.Count; i++) puffs[i].localPosition = home[i] + new Vector2(Mathf.Sin(t * 0.4f + i), Mathf.Cos(t * 0.3f + i * 1.7f)) * 0.15f;
    }
  }
}
