using System.Collections.Generic;
using UnityEngine;
namespace Tidewatch {
  /// <summary>Placeholder IMGUI HUD. Drawing only; taps are hit-tested here from PointerInput (one path for mouse and touch),
  /// so IMGUI's own events never trigger actions. Layout is in virtual pixels (about 600 tall) scaled to the screen.</summary>
  public class Hud : MonoBehaviour {
    public static Hud I;
    struct Btn { public Rect r; public string label, icon; public System.Action act; public bool on; }
    readonly List<Btn> btns = new(); readonly List<Rect> blockers = new();
    float s = 1f, W, H; Texture2D white; GUIStyle label, big, button, center;
    const float Bh = 72f, Pad = 10f, PanelBw = 220f;
    Rect panel; string panelTitle;

    void Awake(){ I = this; white = Texture2D.whiteTexture; }

    void Add(Rect r, string text, System.Action act, bool on = true, string icon = null) => btns.Add(new Btn{ r = r, label = text, act = act, on = on, icon = icon });

    /// <summary>Bottom panel of `n` buttons; returns their rects.</summary>
    Rect[] Panel(int n, string title){
      float w = n * PanelBw + (n + 1) * Pad, py = H - Bh - Pad * 2, px = W * 0.5f - w * 0.5f;
      panel = new Rect(px, py, w, Bh + Pad * 2); panelTitle = title; blockers.Add(panel);
      var r = new Rect[n]; for (int i = 0; i < n; i++) r[i] = new Rect(px + Pad + i * (PanelBw + Pad), py + Pad, PanelBw, Bh);
      return r;
    }

    void Layout(){
      btns.Clear(); blockers.Clear(); panel = default; panelTitle = null;
      s = Mathf.Max(0.3f, Mathf.Min(Screen.height / 600f, Screen.width / 1066f)); W = Screen.width / s; H = Screen.height / s;
      var g = Game.I; if (!g || g.Cfg == null) return;
      if (Screen.height > Screen.width){ blockers.Add(new Rect(0, 0, W, H)); return; }   // portrait: rotate overlay only
      if (g.State == GameState.Menu){
        blockers.Add(new Rect(0, 0, W, H));
        int n = g.Cfg.islands.Length; float bw = 300, gap = 30, x0 = W * 0.5f - (n * bw + (n - 1) * gap) * 0.5f;
        for (int i = 0; i < n; i++){
          var isl = g.Cfg.islands[i]; bool open = Progress.Unlocked(g.Cfg, i); int st = Progress.Stars(isl.id); int idx = i;
          Add(new Rect(x0 + i * (bw + gap), H * 0.5f - 40, bw, 130),
              $"{i + 1}. {isl.name}\n{(open ? $"Stars {st}/3   {isl.waves.Length} waves" : "Locked: win the previous island")}", () => g.StartIsland(idx), open);
        }
        return;
      }
      if (g.State != GameState.Playing){
        blockers.Add(new Rect(0, 0, W, H));
        Add(new Rect(W * 0.5f - 340, H * 0.5f + 40, 320, Bh + 8), g.State == GameState.Won ? "Play again" : "Try again", g.Restart);
        Add(new Rect(W * 0.5f + 20, H * 0.5f + 40, 320, Bh + 8), "Island select", g.ToMenu);
        return;
      }
      blockers.Add(new Rect(0, 0, W, Bh + Pad * 2));
      float x = W - Pad - 120; Add(new Rect(x, Pad, 120, Bh), g.Paused ? "Resume" : "Pause", g.TogglePause, true, g.Paused ? null : "ui_icon_pause");
      x -= 100 + Pad; Add(new Rect(x, Pad, 100, Bh), g.Fast ? "2x" : "1x", g.ToggleSpeed, !g.Paused, "ui_icon_speed");
      x -= 290 + Pad;
      string wl = g.WavesStarted == 0 ? "Start wave 1" : g.WavesStarted >= g.TotalWaves ? "Final wave" :
        g.CanCallWave || g.Countdown > 0f ? $"Call wave {g.WavesStarted + 1}" + (g.EarlyBonus > 0 ? $"  +{g.EarlyBonus}g" : "") +
          (g.Countdown > 0f ? $"\nauto in {Mathf.CeilToInt(g.Countdown)}s" : "") : "Wave incoming";
      Add(new Rect(x, Pad, 290, Bh), wl, g.CallWave, g.CanCallWave, "ui_icon_wave");
      if (g.Paused){
        blockers.Add(new Rect(0, 0, W, H));
        Add(new Rect(W * 0.5f - 160, H * 0.5f + 20, 320, Bh + 8), "Resume", g.TogglePause);
        return;
      }
      if (g.Moving){
        var r = Panel(1, $"Tap a free buoy to move the {g.Moving.defense.def.name} ({g.Cfg.moveFee} gold)");
        Add(r[0], "Cancel move", g.CancelMove);
        return;
      }
      var sel = g.Selected; if (!sel) return;
      if (!sel.defense){
        var defs = g.Cfg.DefensesForSlot(sel.type);
        var r = Panel(defs.Count + 1, sel.type == "buoy" ? "Moor a ship" : "Build a tower");
        for (int i = 0; i < defs.Count; i++){ var d = defs[i]; Add(r[i], $"{d.name}\n{d.levels[0].cost} gold", () => g.Build(sel, d), g.Gold >= d.levels[0].cost, "ui_icon_" + d.id); }
        Add(r[defs.Count], "Close", () => g.Select(null));
      } else {
        var df = sel.defense; bool ship = df.IsShip;
        var r = Panel(ship ? 4 : 3, $"{df.def.name}  level {df.level + 1}/{df.def.levels.Length}{(df.Disabled ? "  (disabled)" : "")}");
        if (df.CanUpgrade) Add(r[0], $"Upgrade\n{df.UpgradeCost} gold", () => g.Upgrade(sel), g.Gold >= df.UpgradeCost, "ui_icon_upgrade");
        else Add(r[0], "Max level", null, false);
        Add(r[1], $"Sell\n+{g.SellValue(sel)} gold", () => g.Sell(sel), true, "ui_icon_sell");
        if (ship) Add(r[2], g.InBattle ? "Move\nbetween waves" : $"Move\n{g.Cfg.moveFee} gold", () => g.BeginMove(sel), g.CanMove(sel));
        Add(r[ship ? 3 : 2], "Close", () => g.Select(null));
      }
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
      center = new GUIStyle(label){ alignment = TextAnchor.MiddleCenter };
      button = new GUIStyle(label){ fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
    }
    void Box(Rect r, Color c){ var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old; }

    // ---------- Art Dev UI sprites (Resources/Sprites/ui_*), drawn when present; flat boxes otherwise ----------
    static Texture2D Tex(string name){ var sp = Art.Load(name); return sp ? sp.texture : null; }
    GUIStyle btnSkin, panelSkin; bool skinsTried;
    void Skins(){
      if (skinsTried) return; skinsTried = true;
      var b = Tex("ui_button"); if (b){ btnSkin = new GUIStyle(); btnSkin.normal.background = b; btnSkin.border = new RectOffset(24, 24, 24, 24); }
      var p = Tex("ui_panel"); if (p){ panelSkin = new GUIStyle(); panelSkin.normal.background = p; panelSkin.border = new RectOffset(32, 32, 32, 32); }
    }
    void Sliced(Rect r, GUIStyle skin, Color c, Color fallback){
      if (skin == null){ Box(r, fallback); return; }
      var old = GUI.color; GUI.color = c; GUI.Box(r, GUIContent.none, skin); GUI.color = old;
    }
    /// <summary>Draws an icon sprite fitted into r (keeps aspect). False if the sprite is missing.</summary>
    static bool Icon(Rect r, string name, Color c){
      var t = Tex(name); if (!t) return false;
      float a = (float)t.width / t.height, w = Mathf.Min(r.width, r.height * a), h = w / a;
      var old = GUI.color; GUI.color = c; GUI.DrawTexture(new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h), t, ScaleMode.ScaleToFit); GUI.color = old;
      return true;
    }
    void DrawButton(Btn b){
      Sliced(b.r, btnSkin, b.on ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f), b.on ? new Color(0.95f, 0.7f, 0.2f, 0.95f) : new Color(0.4f, 0.4f, 0.4f, 0.9f));
      var tr = b.r; var ic = b.on ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
      if (b.icon != null && Tex(b.icon)){
        float sz = b.r.height - 14f;
        if (b.r.width <= 120f && b.label.Length > 3){ Icon(new Rect(b.r.center.x - sz * 0.5f, b.r.y + 7, sz, sz), b.icon, ic); return; }   // small: icon only
        Icon(new Rect(b.r.x + 8, b.r.y + 7, sz, sz), b.icon, ic);
        tr = new Rect(b.r.x + sz + 10, b.r.y, b.r.width - sz - 14, b.r.height);
      }
      GUI.Label(tr, b.label, button);
    }

    void OnGUI(){
      Styles(); Skins(); Layout();
      var g = Game.I; if (!g || g.Cfg == null) return;
      GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
      if (Screen.height > Screen.width){
        Box(new Rect(0, 0, W, H), new Color(0.05f, 0.15f, 0.25f, 0.95f));
        GUI.Label(new Rect(0, 0, W, H), "Turn your device\nto landscape", big); return;
      }
      if (g.State == GameState.Menu){
        Box(new Rect(0, 0, W, H), new Color(0.05f, 0.25f, 0.38f, 1f));
        GUI.Label(new Rect(0, H * 0.5f - 200, W, 80), "Tidewatch", big);
        GUI.Label(new Rect(0, H * 0.5f - 120, W, 50), "Choose an island", center);
      } else if (g.State == GameState.Playing){
        Box(new Rect(0, 0, W, Bh + Pad * 2), new Color(0.05f, 0.2f, 0.3f, 0.75f));
        if (Tex("ui_heart") && Tex("ui_gold")){
          Icon(new Rect(Pad * 2, Pad + 14, 44, 44), "ui_heart", Color.white); GUI.Label(new Rect(Pad * 2 + 50, Pad, 70, Bh), $"{g.Hearts}", label);
          Icon(new Rect(Pad * 2 + 125, Pad + 14, 44, 44), "ui_gold", Color.white); GUI.Label(new Rect(Pad * 2 + 175, Pad, 100, Bh), $"{g.Gold}", label);
          GUI.Label(new Rect(Pad * 2 + 280, Pad, 200, Bh), $"Wave {g.WavesStarted}/{g.TotalWaves}", label);
        } else GUI.Label(new Rect(Pad * 2, Pad, 480, Bh), $"Hearts {g.Hearts}   Gold {g.Gold}   Wave {g.WavesStarted}/{g.TotalWaves}", label);
        if (!g.AnyBuilt && !g.Selected && !g.Paused)
          GUI.Label(new Rect(Pad * 2, H - 60, W, 50), "Tap a sandy plot or a red buoy to build. Then start the wave.", label);
        if (panelTitle != null && !g.Paused){
          Sliced(new Rect(panel.x - 6, panel.y - 6, panel.width + 12, panel.height + 12), panelSkin, Color.white, new Color(0.05f, 0.2f, 0.3f, 0.85f));
          Box(new Rect(panel.x, panel.y - 44, Mathf.Min(W - panel.x, 700), 40), new Color(0.05f, 0.2f, 0.3f, 0.75f));
          GUI.Label(new Rect(panel.x, panel.y - 40, W, 36), panelTitle, label);
        }
        if (g.Paused){ Box(new Rect(0, Bh + Pad * 2, W, H), new Color(0, 0, 0, 0.5f)); GUI.Label(new Rect(0, H * 0.5f - 80, W, 80), "Paused", big); }
      } else {
        bool won = g.State == GameState.Won;
        Box(new Rect(0, 0, W, H), won ? new Color(0.05f, 0.3f, 0.2f, 0.85f) : new Color(0.3f, 0.05f, 0.05f, 0.85f));
        if (!Icon(new Rect(W * 0.5f - 280, H * 0.5f - 265, 560, 140), won ? "ui_win" : "ui_lose", Color.white))
          GUI.Label(new Rect(0, H * 0.5f - 150, W, 80), won ? $"Victory! {g.Island.name} is safe" : "The harbor has fallen", big);
        else GUI.Label(new Rect(0, H * 0.5f - 120, W, 44), won ? $"{g.Island.name} is safe" : "The harbor has fallen", center);
        GUI.Label(new Rect(0, H * 0.5f - 70, W, 60), won ? $"Hearts left {g.Hearts}/{g.Island.hearts}   Stars {g.StarsEarned}/3" : $"Survived {Mathf.Max(0, g.WavesStarted - 1)} of {g.TotalWaves} waves", center);
      }
      foreach (var b in btns) DrawButton(b);
    }
  }
}
