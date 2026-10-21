using System.Collections.Generic;
using UnityEngine;
namespace Tidewatch {
  /// <summary>Placeholder IMGUI HUD. Drawing only; taps are hit-tested here from PointerInput (one path for mouse and touch),
  /// so IMGUI's own events never trigger actions. Layout is in virtual pixels (about 600 tall) scaled to the screen.</summary>
  public class Hud : MonoBehaviour {
    public static Hud I;
    struct Btn { public Rect r; public string label; public System.Action act; public bool on; }
    readonly List<Btn> btns = new(); readonly List<Rect> blockers = new();
    float s = 1f, W, H; Texture2D white; GUIStyle label, big, button;
    const float Bh = 72f, Pad = 10f;

    void Awake(){ I = this; white = Texture2D.whiteTexture; }

    void Add(Rect r, string text, System.Action act, bool on = true) => btns.Add(new Btn{ r = r, label = text, act = act, on = on });

    void Layout(){
      btns.Clear(); blockers.Clear();
      s = Mathf.Max(0.3f, Mathf.Min(Screen.height / 600f, Screen.width / 1066f)); W = Screen.width / s; H = Screen.height / s;
      var g = Game.I; if (!g || g.Island == null) return;
      if (Screen.height > Screen.width){ blockers.Add(new Rect(0, 0, W, H)); return; }   // portrait: rotate overlay only
      if (g.State != GameState.Playing){
        blockers.Add(new Rect(0, 0, W, H));
        Add(new Rect(W * 0.5f - 160, H * 0.5f + 40, 320, Bh + 8), g.State == GameState.Won ? "Play again" : "Try again", g.Restart);
        return;
      }
      blockers.Add(new Rect(0, 0, W, Bh + Pad * 2));
      float x = W - Pad - 120; Add(new Rect(x, Pad, 120, Bh), g.Paused ? "Resume" : "Pause", g.TogglePause);
      x -= 100 + Pad; Add(new Rect(x, Pad, 100, Bh), g.Fast ? "2x" : "1x", g.ToggleSpeed, !g.Paused);
      x -= 270 + Pad;
      string wl = g.WavesStarted == 0 ? "Start wave 1" : g.WavesStarted >= g.TotalWaves ? "Final wave" :
        g.CanCallWave ? $"Call wave {g.WavesStarted + 1}" + (g.EarlyBonus > 0 ? $"  +{g.EarlyBonus}g" : "") : "Wave incoming";
      Add(new Rect(x, Pad, 270, Bh), wl, g.CallWave, g.CanCallWave && !g.Paused);
      if (g.Paused){
        blockers.Add(new Rect(0, 0, W, H));
        Add(new Rect(W * 0.5f - 160, H * 0.5f + 20, 320, Bh + 8), "Resume", g.TogglePause);
        return;
      }
      var sel = g.Selected; if (!sel) return;
      float bw = 250, py = H - Bh - Pad * 2, px = W * 0.5f - (3 * bw + 4 * Pad) * 0.5f;
      blockers.Add(new Rect(px, py, 3 * bw + 4 * Pad, Bh + Pad * 2));
      var b0 = new Rect(px + Pad, py + Pad, bw, Bh); var b1 = b0; b1.x += bw + Pad; var b2 = b1; b2.x += bw + Pad;
      if (!sel.defense){
        var d = g.Cfg.DefenseForSlot(sel.type); int cost = g.BuildCost(sel);
        Add(b0, $"Build {d.name}\n{cost} gold", () => g.Build(sel), g.Gold >= cost);
      } else {
        var df = sel.defense;
        if (df.CanUpgrade) Add(b0, $"Upgrade {df.def.name}\n{df.UpgradeCost} gold", () => g.Upgrade(sel), g.Gold >= df.UpgradeCost);
        else Add(b0, $"{df.def.name}\nmax level", null, false);
        Add(b1, $"Sell\n+{g.SellValue(sel)} gold", () => g.Sell(sel));
      }
      Add(b2, "Close", () => g.Select(null));
    }

    /// <summary>Handles a press at a screen position (bottom-left origin). True if the HUD used it.</summary>
    public bool Click(Vector2 screen){
      Layout();
      var p = new Vector2(screen.x / s, (Screen.height - screen.y) / s);
      for (int i = btns.Count - 1; i >= 0; i--){
        if (!btns[i].r.Contains(p)) continue;
        if (btns[i].on && btns[i].act != null){ GameEvents.Play("ui_click"); btns[i].act(); }
        return true;
      }
      foreach (var r in blockers) if (r.Contains(p)) return true;
      return false;
    }

    void Styles(){
      if (label != null) return;
      label = new GUIStyle(GUI.skin.label){ fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft }; label.normal.textColor = Color.white;
      big = new GUIStyle(label){ fontSize = 48, alignment = TextAnchor.MiddleCenter };
      button = new GUIStyle(label){ fontSize = 24, alignment = TextAnchor.MiddleCenter, wordWrap = true };
    }
    void Box(Rect r, Color c){ var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old; }

    void OnGUI(){
      Styles(); Layout();
      var g = Game.I; if (!g || g.Island == null) return;
      GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
      if (Screen.height > Screen.width){
        Box(new Rect(0, 0, W, H), new Color(0.05f, 0.15f, 0.25f, 0.95f));
        GUI.Label(new Rect(0, 0, W, H), "Turn your device\nto landscape", big); return;
      }
      if (g.State == GameState.Playing){
        Box(new Rect(0, 0, W, Bh + Pad * 2), new Color(0.05f, 0.2f, 0.3f, 0.75f));
        string next = g.Countdown > 0f ? $"   Next wave in {Mathf.CeilToInt(g.Countdown)}s" : "";
        GUI.Label(new Rect(Pad * 2, Pad, W, Bh), $"Hearts {g.Hearts}    Gold {g.Gold}    Wave {g.WavesStarted}/{g.TotalWaves}{next}", label);
        if (!g.AnyBuilt && !g.Selected && !g.Paused)
          GUI.Label(new Rect(Pad * 2, H - 60, W, 50), "Tap a sandy plot or a red buoy to build. Then start the wave.", label);
        if (g.Selected && !g.Paused){
          var sel = g.Selected; float bw = 250, py = H - Bh - Pad * 2, px = W * 0.5f - (3 * bw + 4 * Pad) * 0.5f;
          Box(new Rect(px, py, 3 * bw + 4 * Pad, Bh + Pad * 2), new Color(0.05f, 0.2f, 0.3f, 0.85f));
          if (sel.defense) GUI.Label(new Rect(px, py - 40, 600, 36), $"{sel.defense.def.name}  level {sel.defense.level + 1}", label);
        }
        if (g.Paused){ Box(new Rect(0, Bh + Pad * 2, W, H), new Color(0, 0, 0, 0.5f)); GUI.Label(new Rect(0, H * 0.5f - 80, W, 80), "Paused", big); }
      } else {
        bool won = g.State == GameState.Won;
        Box(new Rect(0, 0, W, H), won ? new Color(0.05f, 0.3f, 0.2f, 0.85f) : new Color(0.3f, 0.05f, 0.05f, 0.85f));
        GUI.Label(new Rect(0, H * 0.5f - 150, W, 80), won ? "Victory! Coral Cay is safe" : "The harbor has fallen", big);
        GUI.Label(new Rect(0, H * 0.5f - 70, W, 60), won ? $"Hearts left {g.Hearts}/{g.Island.hearts}" : $"Survived {Mathf.Max(0, g.WavesStarted - 1)} of {g.TotalWaves} waves",
          new GUIStyle(label){ alignment = TextAnchor.MiddleCenter });
      }
      foreach (var b in btns){
        Box(b.r, b.on ? new Color(0.95f, 0.7f, 0.2f, 0.95f) : new Color(0.4f, 0.4f, 0.4f, 0.9f));
        GUI.Label(b.r, b.label, button);
      }
    }
  }
}
