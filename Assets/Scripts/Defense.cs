using UnityEngine;
namespace Tidewatch {
  /// <summary>A built defense (Cannon Battery on land, Gunboat on a buoy). Fires at the ship furthest along its lane.</summary>
  public class Defense : MonoBehaviour {
    public DefenseDef def; public int level; public int spent;
    public DefenseLevel L => def.levels[level];
    public bool CanUpgrade => level + 1 < def.levels.Length;
    public int UpgradeCost => CanUpgrade ? def.levels[level + 1].cost : 0;
    float cd; Transform aim; GameObject visual;

    public void Init(DefenseDef d){ def = d; level = 0; spent = d.levels[0].cost; Rebuild(); }
    public void Upgrade(){ if (!CanUpgrade) return; level++; spent += L.cost; Rebuild(); }

    void Rebuild(){
      if (visual) Destroy(visual);
      visual = new GameObject("Visual"); visual.transform.SetParent(transform, false);
      int lv = level + 1; var vt = visual.transform;
      if (def.id == "gunboat"){
        aim = new GameObject("Aim").transform; aim.SetParent(vt, false);   // unscaled pivot
        Art.Make(aim, $"ship_gunboat_{lv}", Art.Hull, new Vector2(1.4f, 0.8f), lv > 1 ? new Color(0.2f, 0.32f, 0.6f) : new Color(0.32f, 0.45f, 0.7f), 8);
        if (!Art.Has($"ship_gunboat_{lv}")) Art.Make(aim, null, Art.Square, new Vector2(0.35f, 0.35f), new Color(0.95f, 0.85f, 0.4f), 9);
      } else {
        Art.Make(vt, $"tower_cannon_{lv}", Art.Square, new Vector2(1.0f, 1.0f), lv > 1 ? new Color(0.45f, 0.42f, 0.4f) : new Color(0.62f, 0.58f, 0.52f), 8);
        aim = new GameObject("Aim").transform; aim.SetParent(vt, false);
        bool realBarrel = Art.Has("tower_cannon_barrel");
        Art.Make(aim, "tower_cannon_barrel", Art.Square, new Vector2(0.7f, 0.24f), new Color(0.15f, 0.15f, 0.17f), 9, realBarrel ? Vector2.zero : new Vector2(0.35f, 0f));
        if (!realBarrel && lv > 1) Art.Make(vt, null, Art.Circle, new Vector2(0.3f, 0.3f), new Color(0.95f, 0.8f, 0.2f), 10, new Vector2(-0.3f, 0.3f));   // upgrade badge
      }
    }

    void Update(){
      cd -= Time.deltaTime;
      var g = Game.I; if (!g || g.State != GameState.Playing) return;
      var t = g.FindTarget(transform.position, L.range); if (!t) return;
      Vector2 d = (Vector2)t.transform.position - (Vector2)transform.position;
      float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
      aim.rotation = Quaternion.Euler(0, 0, def.id == "gunboat" ? ang - 90f : ang);   // gunboat turns its broadside to the target
      if (cd <= 0f){
        cd = L.interval;
        Projectile.Fire(transform.position, t, L.damage, L.projectileSpeed);
        GameEvents.Play(def.id == "gunboat" ? "gunboat_fire" : "cannon_fire");
      }
    }
  }
}
