using UnityEngine;
using UnityEngine.InputSystem;
namespace Tidewatch {
  /// <summary>One tap/click path for mouse and touch (new Input System). Positions are screen pixels, bottom-left origin.
  /// A mouse press right after a touch is ignored so a browser's emulated mouse can't double-fire.</summary>
  public static class PointerInput {
    static float lastTouch = -9f; static int frame = -1; static bool hit; static Vector2 hitPos;
    public static bool TryPress(out Vector2 pos){
      if (frame != Time.frameCount){ frame = Time.frameCount; hit = Poll(out hitPos); }
      pos = hitPos; return hit;
    }
    static bool Poll(out Vector2 pos){
      var ts = Touchscreen.current;
      if (ts != null) foreach (var t in ts.touches)
        if (t.press.wasPressedThisFrame){ pos = t.position.ReadValue(); lastTouch = Time.unscaledTime; return true; }
      var m = Mouse.current;
      if (m != null && m.leftButton.wasPressedThisFrame && Time.unscaledTime - lastTouch > 0.5f){ pos = m.position.ReadValue(); return true; }
      pos = default; return false;
    }
    public static bool AnyInput(){
      var kb = Keyboard.current; if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
      return TryPress(out _);
    }
  }
}
