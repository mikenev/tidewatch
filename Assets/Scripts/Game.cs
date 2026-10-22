using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
namespace Tidewatch {
  public enum GameState { Menu, Playing, Won, Lost }

  /// <summary>Core loop: island select, waves from config, city hearts, gold, build slots, moving defensive ships between
  /// waves, speed and pause, win/lose with stars. Keys: Space = start/call wave, F = 1x/2x, P/Esc = pause,
  /// R/Enter = restart on the end screen, 1/2 = pick an island on the menu.</summary>
  public class Game : MonoBehaviour {
    public static Game I;
    public const float SlotTapRadius = 0.8f;
    /// <summary>Island to start straight away after a scene reload (retry); -1 = show the island select.</summary>
    static int pendingIsland = -1;
    public GameConfigData Cfg { get; private set; }
    public IslandDef Island { get; private set; }
    public int IslandIndex { get; private set; } = -1;
    public readonly List<Lane> Lanes = new();
    public readonly List<BuildSlot> Slots = new();
    public readonly List<Ship> Ships = new();
    readonly HashSet<string> onceLogs = new();
    bool armoredHintShown;
    /// <summary>One-off gameplay hint shown by the HUD until HintUntil (unscaled time).</summary>
    public string Hint { get; private set; } public float HintUntil { get; private set; }
    /// <summary>Log a line once per island run (fog/current proofs for the smoke test without log spam).</summary>
    public void LogOnce(string key, string msg){ if (onceLogs.Add(key)) Debug.Log(msg); }
    public int Gold { get; private set; }
    public int Hearts { get; private set; }
    public GameState State { get; private set; } = GameState.Menu;
    public int WavesStarted { get; private set; }
    public int TotalWaves => Island != null ? Island.waves.Length : 0;
    /// <summary>Seconds until the next wave starts by itself; -1 = waiting (before wave 1, or while a wave is still spawning).</summary>
    public float Countdown { get; private set; } = -1f;
    public bool Paused { get; private set; }
    public bool Fast { get; private set; }
    public BuildSlot Selected { get; private set; }
    /// <summary>Buoy whose ship is being moved (tap a free buoy to finish); null when not moving.</summary>
    public BuildSlot Moving { get; private set; }
    public bool AnyBuilt { get; private set; }
    public int StarsEarned { get; private set; }
    public bool InBattle => spawningGroups > 0 || Ships.Count > 0;
    public bool CanCallWave => State == GameState.Playing && !Paused && WavesStarted < TotalWaves && spawningGroups == 0;
    public int EarlyBonus => WavesStarted > 0 && Countdown > 0f ? Mathf.CeilToInt(Countdown * Island.earlyGoldPerSecond) : 0;
    public bool CanMove(BuildSlot s) => State == GameState.Playing && s && s.defense && s.defense.IsShip && !InBattle && Gold >= Cfg.moveFee && FreeBuoyExists();

    int spawningGroups; int[] alive, groupsLeft; bool[] bonusPaid; Camera cam;

    void Awake(){ I = this; Time.timeScale = 1f; }
    void Start(){
      Cfg = GameConfigData.Load(); cam = Camera.main;
      for (int i = 0; i < Cfg.islands.Length; i++)
        Debug.Log($"[TW] menu island={Cfg.islands[i].id} stars={Progress.Stars(Cfg.islands[i].id)} unlocked={Progress.Unlocked(Cfg, i)}");
      int p = pendingIsland; pendingIsland = -1;
      if (p >= 0 && Progress.Unlocked(Cfg, p)) StartIsland(p);
    }

    public void StartIsland(int index){
      if (State != GameState.Menu || index < 0 || index >= Cfg.islands.Length || !Progress.Unlocked(Cfg, index)) return;
      IslandIndex = index; Island = Cfg.islands[index]; onceLogs.Clear();
      Gold = Island.startGold; Hearts = Island.hearts;
      int n = Island.waves.Length; alive = new int[n]; groupsLeft = new int[n]; bonusPaid = new bool[n];
      foreach (var l in Island.lanes) Lanes.Add(new Lane(l));
      Art.Tint = Cfg.TintFor(Island.id);
      if (cam && Island.palette != null) cam.backgroundColor = Art.Hex(Island.palette.sea, cam.backgroundColor);
      MapBuilder.Build(this);
      State = GameState.Playing; ApplyTime();
      Debug.Log($"[TW] ready island={Island.id} waves={n} gold={Gold} hearts={Hearts} slots={Slots.Count} lanes={Lanes.Count}");
    }

    void FitCamera(){
      if (!cam) return;
      float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
      cam.orthographicSize = Mathf.Max(5.4f, 9.6f / aspect);   // whole 19.2 x 10.8 map always on screen
    }

    void Update(){
      FitCamera();
      HandleInput();
      if (State != GameState.Playing) return;
      if (Countdown > 0f){ Countdown -= Time.deltaTime; if (Countdown <= 0f){ Countdown = -1f; StartWave(false); } }
      bool allPaid = true;
      for (int w = 0; w < TotalWaves; w++){
        if (w < WavesStarted && groupsLeft[w] == 0 && alive[w] == 0 && !bonusPaid[w]){
          bonusPaid[w] = true; AddGold(Island.waves[w].bonus); GameEvents.Play("coin");
          Debug.Log($"[TW] wave {w + 1} cleared bonus={Island.waves[w].bonus} gold={Gold} hearts={Hearts}");
        }
        allPaid &= bonusPaid[w];
      }
      if (allPaid && Ships.Count == 0) EndGame(GameState.Won);
    }

    void HandleInput(){
      var kb = Keyboard.current;
      if (kb != null){
        if (State == GameState.Menu){
          if (kb.digit1Key.wasPressedThisFrame) StartIsland(0);
          if (kb.digit2Key.wasPressedThisFrame) StartIsland(1);
          if (kb.digit3Key.wasPressedThisFrame) StartIsland(2);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
          if (kb.uKey.wasPressedThisFrame){ foreach (var isl in Cfg.islands) Progress.Record(isl.id, 1); ToMenu(); }   // dev: unlock all
#endif
        } else if (State == GameState.Playing){
          if (kb.spaceKey.wasPressedThisFrame) CallWave();   // ignored while paused (CanCallWave)
          if (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) TogglePause();
          if (kb.fKey.wasPressedThisFrame) ToggleSpeed();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
          if (kb.kKey.wasPressedThisFrame){   // dev: galleon test - spawn one, take it to 85% hp, then sink it with a single hit
            var gd = Cfg.Enemy("galleon"); var gl = LaneFor(gd, null);
            if (gl != null){ var g = SpawnShip(gd, gl, 0, gl.Length * 0.3f); g.Damage(gd.hp * 0.15f, 1f); Debug.Log($"[TW] dev: one-shot galleon at hp={g.hp:0}"); g.Damage(gd.hp * 5f, 1f); }
          }
#endif
        } else if (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Restart();
      }
      if (!PointerInput.TryPress(out var screen)) return;
      if (Hud.I && Hud.I.Click(screen)) return;
      if (State != GameState.Playing || Paused || !cam) return;
      Vector2 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
      BuildSlot best = null; float bd = SlotTapRadius;
      foreach (var s in Slots){ float d = Vector2.Distance(world, s.transform.position); if (d < bd){ bd = d; best = s; } }
      if (Moving){ if (best && best.type == "buoy" && !best.defense) MoveTo(best); return; }
      Select(best);
    }

    public void Select(BuildSlot s){
      if (Selected) Selected.Show(false, 0f);
      Selected = s;
      if (s){ s.Show(true, s.defense ? s.defense.L.range : Cfg.DefenseForSlot(s.type).levels[0].range); GameEvents.Play("ui_click"); }
    }

    // ---------- waves ----------
    public void CallWave(){ if (CanCallWave) StartWave(true); }
    void StartWave(bool calledByPlayer){
      if (State != GameState.Playing || WavesStarted >= TotalWaves || spawningGroups > 0) return;
      CancelMove();
      int bonus = calledByPlayer ? EarlyBonus : 0;
      if (bonus > 0){ AddGold(bonus); GameEvents.Play("coin"); }
      Countdown = -1f;
      int w = WavesStarted++; var wd = Island.waves[w];
      groupsLeft[w] = wd.groups.Length; spawningGroups += wd.groups.Length;
      foreach (var g in wd.groups) StartCoroutine(SpawnGroup(w, g));
      GameEvents.Play("wave_horn");
      if (!armoredHintShown) foreach (var g in wd.groups){ var e = Cfg.Enemy(g.enemy); if (e != null && e.armor > 0f && !e.boss){
        armoredHintShown = true; Hint = "Armored: use mortars or chain shot"; HintUntil = Time.unscaledTime + 6f; Debug.Log($"[TW] hint wave {w + 1}: {Hint}"); break; } }
      Debug.Log($"[TW] wave {w + 1}/{TotalWaves} started early={calledByPlayer} earlyBonus={bonus} gold={Gold}");
    }
    IEnumerator SpawnGroup(int w, SpawnGroup g){
      if (g.delay > 0f) yield return new WaitForSeconds(g.delay);
      var def = Cfg.Enemy(g.enemy); var lane = LaneFor(def, g.lane);
      if (def == null || lane == null) Debug.LogError($"[TW] wave {w + 1}: bad group {g.enemy}");
      else for (int i = 0; i < g.count && State == GameState.Playing; i++){
        SpawnShip(def, LaneFor(def, g.lane) ?? lane, w, 0f);
        if (i < g.count - 1) yield return new WaitForSeconds(Mathf.Max(0.05f, g.interval));
      }
      groupsLeft[w]--; spawningGroups--;
      if (spawningGroups == 0 && WavesStarted < TotalWaves && State == GameState.Playing) Countdown = Island.waveGap;
    }
    /// <summary>A random lane (or the named one) whose size tags allow this ship.</summary>
    Lane LaneFor(EnemyDef def, string laneId){
      if (def == null) return null;
      var ok = new List<Lane>();
      foreach (var l in Lanes) if ((string.IsNullOrEmpty(laneId) || l.id == laneId) && l.Allows(def.size)) ok.Add(l);
      return ok.Count == 0 ? null : ok[Random.Range(0, ok.Count)];
    }
    Ship SpawnShip(EnemyDef def, Lane lane, int w, float dist){
      if (def.size == "large" && lane.Large != null) lane = lane.Large;   // size-specific path (keeps big hulls off islands)
      var go = new GameObject(def.id); var s = go.AddComponent<Ship>(); s.Init(def, lane, w, dist);
      Ships.Add(s); alive[w]++;
      if (lane.Current != 1f) LogOnce("cur" + lane.id + def.id, $"[TW] current lane={lane.id} x{lane.Current:0.##} {def.id} speed={s.Speed:0.##} base={def.speed:0.##}");
      if (def.boss){ GameEvents.Play("galleon_horn"); Debug.Log($"[TW] boss {def.id} spawned on lane {lane.id}"); }
      return s;
    }
    /// <summary>Dread Galleon: launch `count` ships at the parent's position (its own lane if their size may use it).</summary>
    public void Launch(Ship parent, string enemyId, int count){
      var def = Cfg.Enemy(enemyId); if (def == null || State != GameState.Playing) return;
      var lane = parent.lane.Base.Allows(def.size) ? parent.lane.Base : LaneFor(def, null); if (lane == null) return;
      float d0 = lane == parent.lane.Base ? Mathf.Clamp01(parent.dist / parent.lane.Length) * lane.Length : 0f;
      for (int i = 0; i < count; i++) SpawnShip(def, lane, parent.wave, Mathf.Clamp(d0 + 0.35f * (i - (count - 1) * 0.5f), 0f, lane.Length - 0.1f));
      GameEvents.Play("launch");
      Debug.Log($"[TW] {parent.def.id} launched {count} {def.id} hp={Mathf.Max(0, parent.hp):0}");
    }
    public void ShipSunk(Ship s){
      if (s.Dead) return; s.Dead = true; Ships.Remove(s); alive[s.wave]--;
      AddGold(s.def.gold); GameEvents.Play("ship_sink"); s.Sink(); Fx.Splash(s.transform.position, s.def.boss ? 1.2f : 0.5f);
      if (s.def.boss) Debug.Log($"[TW] boss {s.def.id} sunk gold={Gold} launches={s.Launches}");
    }
    public void ShipReachedHarbor(Ship s){
      if (s.Dead) return; s.Dead = true; Ships.Remove(s); alive[s.wave]--;
      Hearts = Mathf.Max(0, Hearts - s.def.hearts); GameEvents.Play("harbor_breach"); Destroy(s.gameObject);
      Debug.Log($"[TW] {s.def.id} reached the harbor hearts={Hearts}");
      if (Hearts <= 0) EndGame(GameState.Lost);
    }
    /// <summary>Fire Raft reached a defense: it blows up (no gold) and disables the nearest defense.</summary>
    public void RaftExploded(Ship raft, Defense target){
      if (raft.Dead) return; raft.Dead = true; Ships.Remove(raft); alive[raft.wave]--;
      target.Disable(raft.def.disableTime); Fx.Blast(raft.transform.position, raft.def.blastRange * 0.6f);
      GameEvents.Play("raft_blast"); Destroy(raft.gameObject);
      Debug.Log($"[TW] fireraft exploded: {target.def.id} on slot {target.GetComponentInParent<BuildSlot>().index} disabled {raft.def.disableTime}s");
    }
    /// <summary>Ship in range that is furthest along its lane (least distance left to the harbor).</summary>
    public Ship FindTarget(Vector2 from, float range){
      Ship best = null; float bestRem = float.MaxValue, r2 = range * range;
      float fog2 = Island.fogReveal * Island.fogReveal;
      foreach (var s in Ships){
        float d2 = ((Vector2)s.transform.position - from).sqrMagnitude;
        if (s.Dead || d2 > r2 || (s.InFog && d2 > fog2)) continue;   // fog bank: only seen from close by
        if (s.Remaining < bestRem){ bestRem = s.Remaining; best = s; }
      }
      if (best && best.InFog) LogOnce("fogT", $"[TW] fog: targeted {best.def.id} at close range");
      return best;
    }
    /// <summary>Copy of the live ships within range (safe to damage while iterating).</summary>
    public List<Ship> ShipsInRange(Vector2 at, float range){
      var l = new List<Ship>(); float r2 = range * range;
      foreach (var s in Ships) if (!s.Dead && ((Vector2)s.transform.position - at).sqrMagnitude <= r2) l.Add(s);
      return l;
    }
    public bool DefenseWithin(Vector2 at, float range){
      foreach (var s in Slots) if (s.defense && Vector2.Distance(at, s.transform.position) <= range) return true;
      return false;
    }
    /// <summary>Nearest working defense within range, or null.</summary>
    public Defense NearestDefense(Vector2 at, float range){
      Defense best = null; float bd = range;
      foreach (var s in Slots) if (s.defense && !s.defense.Disabled){ float d = Vector2.Distance(at, s.transform.position); if (d <= bd){ bd = d; best = s.defense; } }
      return best;
    }

    // ---------- economy / building ----------
    void AddGold(int g){ Gold += g; }
    bool Spend(int cost){ if (Gold < cost){ GameEvents.Play("ui_error"); return false; } Gold -= cost; return true; }
    public int SellValue(BuildSlot s) => s.defense ? Mathf.FloorToInt(s.defense.spent * Cfg.sellRefund) : 0;
    public void Build(BuildSlot s, DefenseDef d){
      if (!s || s.defense || d == null || d.slot != s.type || State != GameState.Playing) return;
      if (!Spend(d.levels[0].cost)) return;
      var go = new GameObject(d.id); go.transform.SetParent(s.transform, false);
      s.defense = go.AddComponent<Defense>(); s.defense.Init(d); AnyBuilt = true;
      GameEvents.Play("build"); s.Show(true, s.defense.L.range);
      Debug.Log($"[TW] built {d.id} on slot {s.index} gold={Gold}");
    }
    public void Upgrade(BuildSlot s){
      if (!s || !s.defense || !s.defense.CanUpgrade || State != GameState.Playing) return;
      if (!Spend(s.defense.UpgradeCost)) return;
      s.defense.Upgrade(); GameEvents.Play("upgrade"); s.Show(true, s.defense.L.range);
      Debug.Log($"[TW] upgraded {s.defense.def.id} on slot {s.index} to level {s.defense.level + 1} gold={Gold}");
    }
    public void Sell(BuildSlot s){
      if (!s || !s.defense || State != GameState.Playing) return;
      int refund = SellValue(s); AddGold(refund);
      Destroy(s.defense.gameObject); s.defense = null; GameEvents.Play("sell");
      Debug.Log($"[TW] sold slot {s.index} refund={refund} gold={Gold}");
      Select(null);
    }
    bool FreeBuoyExists(){ foreach (var s in Slots) if (s.type == "buoy" && !s.defense) return true; return false; }
    public void BeginMove(BuildSlot s){
      if (!CanMove(s)) { GameEvents.Play("ui_error"); return; }
      Moving = s; Select(null); s.Show(true, 0f);
      foreach (var b in Slots) if (b.type == "buoy" && !b.defense) b.Show(true, 0f);
    }
    public void CancelMove(){
      if (!Moving) return;
      foreach (var b in Slots) if (b.type == "buoy") b.Show(false, 0f);
      Moving = null;
    }
    void MoveTo(BuildSlot to){
      var from = Moving; CancelMove();
      if (!CanMove(from) || !to || to.defense || to.type != "buoy" || !Spend(Cfg.moveFee)) return;
      var d = from.defense; from.defense = null; to.defense = d; d.transform.SetParent(to.transform, false);
      GameEvents.Play("ship_move");
      Debug.Log($"[TW] moved {d.def.id} from slot {from.index} to slot {to.index} fee={Cfg.moveFee} gold={Gold}");
      Select(to);
    }

    // ---------- speed / pause / end ----------
    void ApplyTime(){ Time.timeScale = State != GameState.Playing || Paused ? 0f : (Fast ? 2f : 1f); }
    public void TogglePause(){ if (State != GameState.Playing) return; Paused = !Paused; ApplyTime(); GameEvents.Play("ui_click"); }
    public void ToggleSpeed(){ if (State != GameState.Playing || Paused) return; Fast = !Fast; ApplyTime(); GameEvents.Play("ui_click"); }
    void EndGame(GameState s){
      if (State != GameState.Playing) return;
      State = s; CancelMove(); Select(null); ApplyTime();
      ClearBattlefield();
      if (s == GameState.Won){ StarsEarned = Cfg.Stars(Hearts, Island.hearts); Progress.Record(Island.id, StarsEarned); }
      Debug.Log($"[TW] {(s == GameState.Won ? "WON" : "LOST")} island={Island.id} hearts={Hearts} stars={StarsEarned} gold={Gold} waves={WavesStarted}");
    }
    /// <summary>End screen: remove every ship (live or mid-sink), cannonball and effect so nothing shows frozen behind it.</summary>
    void ClearBattlefield(){
      StopAllCoroutines();
      int n = 0;
      foreach (var sh in FindObjectsByType<Ship>(FindObjectsSortMode.None)){ Destroy(sh.gameObject); n++; }
      foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(p.gameObject);
      foreach (var f in FindObjectsByType<Fx>(FindObjectsSortMode.None)) Destroy(f.gameObject);
      Ships.Clear();
      Debug.Log($"[TW] end screen cleared ships={n}");
    }
    /// <summary>Replay the same island.</summary>
    public void Restart(){ pendingIsland = IslandIndex; Reload(); }
    public void ToMenu(){ pendingIsland = -1; Reload(); }
    void Reload(){ Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
  }
}
