using System;
using System.Collections.Generic;
using UnityEngine;
namespace Tidewatch {
  /// <summary>Data loaded from Assets/Resources/Config/tidewatch.json (enemies, defenses, islands with lanes, slots and waves).</summary>
  [Serializable] public class EnemyDef { public string id, name, size, color; public float hp, speed, w, h; public int gold, hearts; }
  [Serializable] public class DefenseLevel { public int cost; public float damage, range, interval, projectileSpeed; }
  [Serializable] public class DefenseDef { public string id, name, slot; public DefenseLevel[] levels; }
  [Serializable] public class LaneDef { public string id; public string[] sizes; public Vector2[] points; }
  [Serializable] public class LandDef { public Vector2 pos; public float radius; }
  [Serializable] public class SlotDef { public string type; public Vector2 pos; }
  [Serializable] public class SpawnGroup { public string enemy, lane; public int count; public float interval, delay; }
  [Serializable] public class WaveDef { public int bonus; public SpawnGroup[] groups; }
  [Serializable] public class IslandDef {
    public string id, name, background; public int hearts, startGold; public float waveGap, earlyGoldPerSecond;
    public Vector2 harbor; public LaneDef[] lanes; public LandDef[] land; public SlotDef[] slots; public WaveDef[] waves;
  }

  [Serializable] public class GameConfigData {
    public const string ResourcePath = "Config/tidewatch";
    public float sellRefund = 0.7f;
    public EnemyDef[] enemies; public DefenseDef[] defenses; public IslandDef[] islands;

    public static GameConfigData Load(){
      var ta = Resources.Load<TextAsset>(ResourcePath);
      if (!ta) throw new Exception("Missing config Resources/" + ResourcePath);
      return Parse(ta.text);
    }
    public static GameConfigData Parse(string json) => JsonUtility.FromJson<GameConfigData>(json);

    public EnemyDef Enemy(string id){ foreach (var e in enemies) if (e.id == id) return e; return null; }
    public DefenseDef DefenseForSlot(string slotType){ foreach (var d in defenses) if (d.slot == slotType) return d; return null; }

    /// <summary>Config sanity checks; the WebGL build fails if this returns any error.</summary>
    public List<string> Validate(){
      var err = new List<string>();
      if (sellRefund <= 0 || sellRefund > 1) err.Add("sellRefund must be in (0,1]");
      if (enemies == null || enemies.Length == 0) err.Add("no enemies");
      else foreach (var e in enemies) if (e.hp <= 0 || e.speed <= 0 || e.hearts < 1 || e.hearts > 5) err.Add($"enemy {e.id}: hp/speed must be > 0, hearts 1-5");
      if (defenses == null || defenses.Length == 0) err.Add("no defenses");
      else foreach (var d in defenses){
        if (d.levels == null || d.levels.Length == 0) { err.Add($"defense {d.id}: no levels"); continue; }
        foreach (var l in d.levels) if (l.cost <= 0 || l.damage <= 0 || l.range <= 0 || l.interval <= 0 || l.projectileSpeed <= 0) err.Add($"defense {d.id}: level values must be > 0");
      }
      if (islands == null || islands.Length == 0) { err.Add("no islands"); return err; }
      foreach (var isl in islands){
        if (isl.lanes == null || isl.lanes.Length == 0) err.Add($"{isl.id}: no lanes");
        else foreach (var l in isl.lanes) if (l.points == null || l.points.Length < 2) err.Add($"{isl.id}/{l.id}: lane needs 2+ points");
        if (isl.waves == null || isl.waves.Length == 0) err.Add($"{isl.id}: no waves");
        if (isl.hearts <= 0) err.Add($"{isl.id}: hearts must be > 0");
        if (isl.slots != null) foreach (var s in isl.slots) if (DefenseForSlot(s.type) == null) err.Add($"{isl.id}: no defense for slot type {s.type}");
        if (isl.waves != null) for (int w = 0; w < isl.waves.Length; w++){
          var wave = isl.waves[w];
          if (wave.groups == null || wave.groups.Length == 0) { err.Add($"{isl.id} wave {w + 1}: no groups"); continue; }
          foreach (var g in wave.groups){
            var e = Enemy(g.enemy);
            if (e == null) { err.Add($"{isl.id} wave {w + 1}: unknown enemy {g.enemy}"); continue; }
            if (g.count <= 0) err.Add($"{isl.id} wave {w + 1}: count must be > 0");
            bool laneOk = false;
            if (isl.lanes != null) foreach (var l in isl.lanes)
              if ((string.IsNullOrEmpty(g.lane) || g.lane == l.id) && Lane.SizeAllowed(l.sizes, e.size)) laneOk = true;
            if (!laneOk) err.Add($"{isl.id} wave {w + 1}: no lane for {g.enemy} ({e.size})");
          }
        }
      }
      return err;
    }
  }
}
