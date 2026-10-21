using UnityEngine;
namespace Tidewatch {
  /// <summary>A built defense. Projectile kinds fire at the ship furthest along its lane (Cannon Battery, Mortar Fort splash,
  /// Chain-Shot Post slow, Gunboat); the Net Tender aura slows every ship in range. Fire Rafts can disable it for a while.</summary>
  public class Defense : MonoBehaviour {
    public DefenseDef def; public int level; public int spent;
    public DefenseLevel L => def.levels[level];
    public bool CanUpgrade => level + 1 < def.levels.Length;
    public int UpgradeCost => CanUpgrade ? def.levels[level + 1].cost : 0;
    public bool IsShip => def.slot == "buoy";
    public bool Disabled => Time.time < disabledUntil;
    float cd, disabledUntil = -1f; Transform aim; GameObject visual, disabledMark; SpriteRenderer auraRing;

    public void Init(DefenseDef d){ def = d; level = 0; spent = d.levels[0].cost; Rebuild(); }
    public void Upgrade(){ if (!CanUpgrade) return; level++; spent += L.cost; Rebuild(); }
    public void Disable(float seconds){ disabledUntil = Mathf.Max(disabledUntil, Time.time + seconds); }

    void Rebuild(){
      if (visual) Destroy(visual);
      visual = new GameObject("Visual"); visual.transform.SetParent(transform, false);
      int lv = level + 1; var vt = visual.transform; aim = null; auraRing = null;
      float tint = 1f - 0.12f * level;
      Color C(float r, float g, float b) => new Color(r * tint, g * tint, b * tint);
      string art = def.id switch { "cannon" => $"tower_cannon_{lv}", "mortar" => $"tower_mortar_{lv}", "chain" => $"tower_chain_{lv}",
                                   "gunboat" => $"ship_gunboat_{lv}", "nettender" => $"ship_nettender_{lv}", _ => null };
      bool real = Art.Has(art);
      switch (def.id){
        case "gunboat":
        case "nettender":
          aim = new GameObject("Aim").transform; aim.SetParent(vt, false);   // unscaled pivot
          bool net = def.id == "nettender";
          Art.Make(aim, art, Art.Hull, net ? new Vector2(1.3f, 0.75f) : new Vector2(1.4f, 0.8f), net ? C(0.3f, 0.6f, 0.45f) : C(0.32f, 0.45f, 0.7f), 8);
          if (!real) Art.Make(aim, null, net ? Art.Circle : Art.Square, new Vector2(0.35f, 0.35f), net ? new Color(0.85f, 0.8f, 0.6f) : new Color(0.95f, 0.85f, 0.4f), 9);
          if (net){ auraRing = Art.Make(vt, null, Art.Circle, Vector2.one * L.range * 2f, new Color(0.8f, 0.95f, 1f, 0.12f), 1); aim.rotation = Quaternion.Euler(0, 0, 90); aim = null; }
          break;
        case "mortar":
          Art.Make(vt, art, Art.Square, new Vector2(1.0f, 1.0f), C(0.45f, 0.36f, 0.3f), 8);
          if (!real) Art.Make(vt, null, Art.Circle, new Vector2(0.55f, 0.55f), new Color(0.1f, 0.1f, 0.1f), 9);
          break;
        case "chain":
          Art.Make(vt, art, Art.Square, new Vector2(0.95f, 0.95f), C(0.45f, 0.5f, 0.58f), 8);
          aim = new GameObject("Aim").transform; aim.SetParent(vt, false);
          bool rb = Art.Has("tower_chain_barrel");
          Art.Make(aim, "tower_chain_barrel", Art.Square, new Vector2(0.6f, 0.16f), new Color(0.75f, 0.75f, 0.8f), 9, rb ? Vector2.zero : new Vector2(0.3f, 0f));
          break;
        default:   // cannon
          Art.Make(vt, art, Art.Square, new Vector2(1.0f, 1.0f), C(0.62f, 0.58f, 0.52f), 8);
          aim = new GameObject("Aim").transform; aim.SetParent(vt, false);
          bool realBarrel = Art.Has("tower_cannon_barrel");
          Art.Make(aim, "tower_cannon_barrel", Art.Square, new Vector2(0.7f, 0.24f), new Color(0.15f, 0.15f, 0.17f), 9, realBarrel ? Vector2.zero : new Vector2(0.35f, 0f));
          break;
      }
      if (!real) for (int i = 0; i < level; i++) Art.Make(vt, null, Art.Circle, new Vector2(0.24f, 0.24f), new Color(0.95f, 0.8f, 0.2f), 10, new Vector2(-0.35f + i * 0.28f, 0.38f));   // level pips
      disabledMark = Art.Make(vt, null, Art.Circle, new Vector2(1.3f, 1.3f), new Color(0.1f, 0.05f, 0.05f, 0.6f), 15).gameObject;
      disabledMark.SetActive(Disabled);
    }

    void Update(){
      cd -= Time.deltaTime;
      var g = Game.I; if (!g || g.State != GameState.Playing) return;
      bool off = Disabled; if (disabledMark.activeSelf != off) disabledMark.SetActive(off);
      if (off) return;
      if (def.IsAura){
        foreach (var s in g.ShipsInRange(transform.position, L.range)) s.ApplySlow(L.slow, 0.15f);
        return;
      }
      var t = g.FindTarget(transform.position, L.range); if (!t) return;
      Vector2 d = (Vector2)t.transform.position - (Vector2)transform.position;
      float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
      if (aim) aim.rotation = Quaternion.Euler(0, 0, def.id == "gunboat" ? ang - 90f : ang);   // gunboat turns its broadside to the target
      if (cd <= 0f){
        cd = L.interval;
        Projectile.Fire(transform.position, t, L);
        GameEvents.Play(def.id + "_fire");
      }
    }
  }
}
