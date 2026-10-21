using UnityEngine;
namespace Tidewatch {
  /// <summary>Island progress in PlayerPrefs (browser storage on WebGL): best stars per island, saved after each win.
  /// An island is unlocked when it is the first one or the previous island has been won (1+ stars).</summary>
  public static class Progress {
    static string Key(string islandId) => "tw_stars_" + islandId;
    public static int Stars(string islandId) => PlayerPrefs.GetInt(Key(islandId), 0);
    public static bool Unlocked(GameConfigData cfg, int index) => index == 0 || (index > 0 && index < cfg.islands.Length && Stars(cfg.islands[index - 1].id) > 0);
    /// <summary>Keeps the best result; returns true if it improved.</summary>
    public static bool Record(string islandId, int stars){
      if (stars <= Stars(islandId)) return false;
      PlayerPrefs.SetInt(Key(islandId), stars); PlayerPrefs.Save(); return true;
    }
  }
}
