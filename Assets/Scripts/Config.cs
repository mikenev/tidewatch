using System;
using System.Collections.Generic;
using UnityEngine;
namespace Tidewatch {
  /// <summary>Data loaded from Assets/Resources/Config/tidewatch.json (enemies, defenses, islands with lanes, slots and waves).</summary>
  /// <summary>armor: fraction of damage blocked (pierce on the shot ignores it). Fire Raft: blastRange/disableTime.
  /// Boss: spawnEnemy x spawnCount launched each time it loses another spawnEvery of its max HP.</summary>
  [Serializable] public class EnemyDef {
    public string id, name, size, color, spawnEnemy; public float hp, speed, w, h, armor, blastRange, disableTime, spawnEvery;
    public int gold, hearts, spawnCount; public bool boss;
  }
  /// <summary>splash: radius (mortar). slow/slowTime: speed multiplier and duration on hit (chain shot) or in range (aura).
  /// pierce: 0-1 share of armor ignored.</summary>
  [Serializable] public class DefenseLevel { public int cost; public float damage, range, interval, projectileSpeed, splash, slow, slowTime, pierce; }
  /// <summary>kind: "" = fires projectiles, "aura" = slows every ship in range (Net Tender).</summary>
  [Serializable] public class DefenseDef {
    public string id, name, slot, kind; public DefenseLevel[] levels;
    public bool IsAura => kind == "aura";
  }
  [Serializable] public class IslandTint { public string island, tint; }
  [Serializable] public class Palette { public string sea, shallows, sand, grass, reef; }
  /// <summary>current: multiplies ship speed on this lane (0 or missing = 1).</summary>
  /// <summary>largePoints (optional): a nudged path used only by large ships, e.g. to keep the Galleon's hull off an island.</summary>
  [Serializable] public class LaneDef { public string id; public string[] sizes; public Vector2[] points; public float current; public Vector2[] largePoints; }
  [Serializable] public class LandDef { public Vector2 pos; public float radius; }
  [Serializable] public class SlotDef { public string type; public Vector2 pos; }
  [Serializable] public class SpawnGroup { public string enemy, lane; public int count; public float interval, delay; }
  [Serializable] public class WaveDef { public int bonus; public SpawnGroup[] groups; }
  [Serializable] public class IslandDef {
    public string id, name, background; public int hearts, startGold; public float waveGap, earlyGoldPerSecond;
    /// <summary>fog: a fog bank (radius 0 = none). Ships inside it are hidden and can only be targeted by defenses within
    /// fogReveal of them. night: dark overlay with rain and lightning.</summary>
    public LandDef fog; public float fogReveal = 1.6f; public bool night;
    public bool HasFog => fog != null && fog.radius > 0f;
    public Vector2 harbor; public Palette palette; public LaneDef[] lanes; public LandDef[] land, reefs; public SlotDef[] slots; public WaveDef[] waves;
  }

  [Serializable] public class GameConfigData {
    public const string ResourcePath = "Config/tidewatch";
    public float sellRefund = 0.7f, stars3 = 0.9f, stars2 = 0.5f; public int moveFee = 15;
    public EnemyDef[] enemies; public DefenseDef[] defenses; public IslandDef[] islands;
    /// <summary>Multiply tint for shared sprites per island id (may list future islands).</summary>
    public IslandTint[] islandTints;
    public Color TintFor(string islandId){
      if (islandTints != null) foreach (var t in islandTints) if (t.island == islandId && ColorUtility.TryParseHtmlString(t.tint, out var c)) return c;
      return Color.white;
    }

    public static GameConfigData Load(){
      var ta = Resources.Load<TextAsset>(ResourcePath);
      if (!ta) throw new Exception("Missing config Resources/" + ResourcePath);
      return Parse(ta.text);
    }
    public static GameConfigData Parse(string json) => JsonUtility.FromJson<GameConfigData>(json);

    public EnemyDef Enemy(string id){ foreach (var e in enemies) if (e.id == id) return e; return null; }
    public DefenseDef DefenseForSlot(string slotType){ foreach (var d in defenses) if (d.slot == slotType) return d; return null; }
    public List<DefenseDef> DefensesForSlot(string slotType){ var l = new List<DefenseDef>(); foreach (var d in defenses) if (d.slot == slotType) l.Add(d); return l; }
    public int IslandIndex(string id){ for (int i = 0; i < islands.Length; i++) if (islands[i].id == id) return i; return -1; }
    /// <summary>Stars for a win with `hearts` of `max` left: 3 / 2 / 1 by the stars3 / stars2 heart fractions.</summary>
    public int Stars(int hearts, int max){ float f = max > 0 ? (float)hearts / max : 0f; return hearts <= 0 ? 0 : f >= stars3 ? 3 : f >= stars2 ? 2 : 1; }

    /// <summary>Config sanity checks; the WebGL build fails if this returns any error.</summary>
    public List<string> Validate(){
      var err = new List<string>();
      if (sellRefund <= 0 || sellRefund > 1) err.Add("sellRefund must be in (0,1]");
      if (enemies == null || enemies.Length == 0) err.Add("no enemies");
      else foreach (var e in enemies){
        if (e.hp <= 0 || e.speed <= 0 || e.hearts < 1 || e.hearts > 5) err.Add($"enemy {e.id}: hp/speed must be > 0, hearts 1-5");
        if (e.armor < 0 || e.armor >= 1) err.Add($"enemy {e.id}: armor must be in [0,1)");
        if (!string.IsNullOrEmpty(e.spawnEnemy) && (Enemy(e.spawnEnemy) == null || e.spawnEnemy == e.id || e.spawnCount <= 0 || e.spawnEvery <= 0 || e.spawnEvery >= 1))
          err.Add($"enemy {e.id}: bad spawnEnemy/spawnCount/spawnEvery");
        if (e.blastRange > 0 && e.disableTime <= 0) err.Add($"enemy {e.id}: blastRange needs disableTime > 0");
      }
      if (moveFee < 0) err.Add("moveFee must be >= 0");
      if (islandTints != null) foreach (var t in islandTints) if (!ColorUtility.TryParseHtmlString(t.tint, out _)) err.Add($"islandTints {t.island}: bad colour {t.tint}");
      if (defenses == null || defenses.Length == 0) err.Add("no defenses");
      else foreach (var d in defenses){
        if (d.levels == null || d.levels.Length == 0) { err.Add($"defense {d.id}: no levels"); continue; }
        if (d.levels.Length != 3) err.Add($"defense {d.id}: needs 3 levels (build + 2 upgrades)");
        foreach (var l in d.levels){
          if (l.cost <= 0 || l.range <= 0) err.Add($"defense {d.id}: cost and range must be > 0");
          if (d.IsAura){ if (l.slow <= 0 || l.slow >= 1) err.Add($"defense {d.id}: aura slow must be in (0,1)"); }
          else if (l.damage <= 0 || l.interval <= 0 || l.projectileSpeed <= 0) err.Add($"defense {d.id}: damage/interval/projectileSpeed must be > 0");
          if (l.slow > 0 && !d.IsAura && l.slowTime <= 0) err.Add($"defense {d.id}: slow needs slowTime");
        }
      }
      if (islands == null || islands.Length == 0) { err.Add("no islands"); return err; }
      foreach (var isl in islands){
        if (isl.lanes == null || isl.lanes.Length == 0) err.Add($"{isl.id}: no lanes");
        else foreach (var l in isl.lanes) if (l.points == null || l.points.Length < 2) err.Add($"{isl.id}/{l.id}: lane needs 2+ points");
        if (isl.waves == null || isl.waves.Length == 0) err.Add($"{isl.id}: no waves");
        if (isl.hearts <= 0) err.Add($"{isl.id}: hearts must be > 0");
        if (isl.slots == null || isl.slots.Length == 0) err.Add($"{isl.id}: no slots");
        if (isl.lanes != null) foreach (var l in isl.lanes) if (l.current < 0) err.Add($"{isl.id}/{l.id}: current must be >= 0");
        if (isl.HasFog && isl.fogReveal <= 0) err.Add($"{isl.id}: fogReveal must be > 0");
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
            var sp = string.IsNullOrEmpty(e.spawnEnemy) ? null : Enemy(e.spawnEnemy); bool spOk = sp == null;
            if (sp != null && isl.lanes != null) foreach (var l in isl.lanes) if (Lane.SizeAllowed(l.sizes, sp.size)) spOk = true;
            if (!spOk) err.Add($"{isl.id} wave {w + 1}: no lane for {e.id}'s launched {sp.id}");
          }
        }
      }
      return err;
    }
  }
}
