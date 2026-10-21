using System;
namespace Tidewatch {
  /// <summary>Decoupled game -> audio/FX notifications. Sfx ids are mapped to clips in GameAudio.</summary>
  public static class GameEvents {
    public static event Action<string> Sfx;
    public static void Play(string id) => Sfx?.Invoke(id);
  }
}
