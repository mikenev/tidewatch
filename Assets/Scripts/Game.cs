using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
namespace Tidewatch {
  public enum GameState { Playing, Won, Lost }

  /// <summary>Core loop: waves from config, city hearts, gold, build slots, speed and pause, win/lose.
  /// Keys: Space = start/call wave, F = 1x/2x, P/Esc = pause, R/Enter = restart on the end screen.</summary>
  public class Game : MonoBehaviour {
    public static Game I;
    public const float SlotTapRadius = 0.8f;
    public GameConfigData Cfg { get; private set; }
    public IslandDef Island { get; private set; }
    public readonly List<Lane> Lanes = new();
    public readonly List<BuildSlot> Slots = new();
    public readonly List<Ship> Ships = new();
    public int Gold { get; private set; }
    public int Hearts { get; private set; }
    public GameState State { get; private set; }
    public int WavesStarted { get; private set; }
    public int TotalWaves => Island.waves.Length;
    /// <summary>Seconds until the next wave starts by itself; -1 = waiting (before wave 1, or while a wave is still spawning).</summary>
    public float Countdown { get; private set; } = -1f;
    public bool Paused { get; private set; }
    public bool Fast { get; private set; }
    public BuildSlot Selected { get; private set; }
    public bool AnyBuilt { get; private set; }
    public bool InBattle => spawningGroups > 0 || Ships.Count > 0;
    public bool CanCallWave => State == GameState.Playing && WavesStarted < TotalWaves && spawningGroups == 0;
    public int EarlyBonus => WavesStarted > 0 && Countdown > 0f ? Mathf.CeilToInt(Countdown * Island.earlyGoldPerSecond) : 0;

    int spawningGroups; int[] alive, groupsLeft; bool[] bonusPaid; Camera cam;

    void Awake(){ I = this; Time.timeScale = 1f; }
    void Start(){
      Cfg = GameConfigData.Load(); Island = Cfg.islands[0];
      Gold = Island.startGold; Hearts = Island.hearts;
      int n = Island.waves.Length; alive = new int[n]; groupsLeft = new int[n]; bonusPaid = new bool[n];
      cam = Camera.main;
      foreach (var l in Island.lanes) Lanes.Add(new Lane(l));
      MapBuilder.Build(this);
      Debug.Log($"[TW] ready island={Island.id} waves={n} gold={Gold} hearts={Hearts} slots={Slots.Count}");
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
          Debug.Log($"[TW] wave {w + 1} cleared bonus={Island.waves[w].bonus} gold={Gold}");
        }
        allPaid &= bonusPaid[w];
      }
      if (allPaid && Ships.Count == 0) EndGame(GameState.Won);
    }

    void HandleInput(){
      var kb = Keyboard.current;
      if (kb != null){
        if (State == GameState.Playing){
          if (kb.spaceKey.wasPressedThisFrame) CallWave();
          if (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) TogglePause();
          if (kb.fKey.wasPressedThisFrame) ToggleSpeed();
        } else if (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Restart();
      }
      if (!PointerInput.TryPress(out var screen)) return;
      if (Hud.I && Hud.I.Click(screen)) return;
      if (State != GameState.Playing || Paused || !cam) return;
      Vector2 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
      BuildSlot best = null; float bd = SlotTapRadius;
      foreach (var s in Slots){ float d = Vector2.Distance(world, s.transform.position); if (d < bd){ bd = d; best = s; } }
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
      if (!CanCallWave) return;
      int bonus = calledByPlayer ? EarlyBonus : 0;
      if (bonus > 0){ AddGold(bonus); GameEvents.Play("coin"); }
      Countdown = -1f;
      int w = WavesStarted++; var wd = Island.waves[w];
      groupsLeft[w] = wd.groups.Length; spawningGroups += wd.groups.Length;
      foreach (var g in wd.groups) StartCoroutine(SpawnGroup(w, g));
      GameEvents.Play("wave_horn");
      Debug.Log($"[TW] wave {w + 1}/{TotalWaves} started early={calledByPlayer} earlyBonus={bonus} gold={Gold}");
    }
    IEnumerator SpawnGroup(int w, SpawnGroup g){
      if (g.delay > 0f) yield return new WaitForSeconds(g.delay);
      var def = Cfg.Enemy(g.enemy); var lane = LaneFor(def, g.lane);
      if (def == null || lane == null) Debug.LogError($"[TW] wave {w + 1}: bad group {g.enemy}");
      else for (int i = 0; i < g.count && State == GameState.Playing; i++){
        SpawnShip(def, lane, w);
        if (i < g.count - 1) yield return new WaitForSeconds(Mathf.Max(0.05f, g.interval));
      }
      groupsLeft[w]--; spawningGroups--;
      if (spawningGroups == 0 && WavesStarted < TotalWaves && State == GameState.Playing) Countdown = Island.waveGap;
    }
    Lane LaneFor(EnemyDef def, string laneId){
      if (def == null) return null;
      var ok = new List<Lane>();
      foreach (var l in Lanes) if ((string.IsNullOrEmpty(laneId) || l.id == laneId) && l.Allows(def.size)) ok.Add(l);
      return ok.Count == 0 ? null : ok[Random.Range(0, ok.Count)];
    }
    void SpawnShip(EnemyDef def, Lane lane, int w){
      var go = new GameObject(def.id); var s = go.AddComponent<Ship>(); s.Init(def, lane, w);
      Ships.Add(s); alive[w]++;
    }
    public void ShipSunk(Ship s){
      if (s.Dead) return; s.Dead = true; Ships.Remove(s); alive[s.wave]--;
      AddGold(s.def.gold); GameEvents.Play("ship_sink"); s.Sink();
    }
    public void ShipReachedHarbor(Ship s){
      if (s.Dead) return; s.Dead = true; Ships.Remove(s); alive[s.wave]--;
      Hearts = Mathf.Max(0, Hearts - s.def.hearts); GameEvents.Play("harbor_breach"); Destroy(s.gameObject);
      Debug.Log($"[TW] {s.def.id} reached the harbor hearts={Hearts}");
      if (Hearts <= 0) EndGame(GameState.Lost);
    }
    /// <summary>Ship in range that is furthest along its lane (least distance left to the harbor).</summary>
    public Ship FindTarget(Vector2 from, float range){
      Ship best = null; float bestRem = float.MaxValue, r2 = range * range;
      foreach (var s in Ships){
        if (s.Dead || ((Vector2)s.transform.position - from).sqrMagnitude > r2) continue;
        if (s.Remaining < bestRem){ bestRem = s.Remaining; best = s; }
      }
      return best;
    }

    // ---------- economy / building ----------
    void AddGold(int g){ Gold += g; }
    bool Spend(int cost){ if (Gold < cost){ GameEvents.Play("ui_error"); return false; } Gold -= cost; return true; }
    public int BuildCost(BuildSlot s) => Cfg.DefenseForSlot(s.type).levels[0].cost;
    public int SellValue(BuildSlot s) => s.defense ? Mathf.FloorToInt(s.defense.spent * Cfg.sellRefund) : 0;
    public void Build(BuildSlot s){
      if (!s || s.defense || State != GameState.Playing) return;
      var d = Cfg.DefenseForSlot(s.type); if (!Spend(d.levels[0].cost)) return;
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

    // ---------- speed / pause / end ----------
    void ApplyTime(){ Time.timeScale = State != GameState.Playing || Paused ? 0f : (Fast ? 2f : 1f); }
    public void TogglePause(){ if (State != GameState.Playing) return; Paused = !Paused; ApplyTime(); GameEvents.Play("ui_click"); }
    public void ToggleSpeed(){ if (State != GameState.Playing) return; Fast = !Fast; ApplyTime(); GameEvents.Play("ui_click"); }
    void EndGame(GameState s){
      if (State != GameState.Playing) return;
      State = s; Select(null); ApplyTime();
      Debug.Log($"[TW] {(s == GameState.Won ? "WON" : "LOST")} hearts={Hearts} gold={Gold} waves={WavesStarted}");
    }
    public void Restart(){ Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
  }
}
