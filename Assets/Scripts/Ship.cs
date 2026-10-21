using UnityEngine;
namespace Tidewatch {
  /// <summary>An enemy ship sailing its lane toward the harbor. Handles armor, slows, the Fire Raft blast and the
  /// Dread Galleon launching dinghies as it takes damage.</summary>
  public class Ship : MonoBehaviour {
    public EnemyDef def; public Lane lane; public int wave; public float dist, hp; public bool Dead;
    public float Remaining => lane.Length - dist;
    public float SpeedMul => Time.time > slowUntil ? 1f : slowFactor;
    public float Speed => def.speed * SpeedMul;
    SpriteRenderer body; Transform bodyT, bar; Color baseColor; float flash, sinkT = -1f, slowFactor = 1f, slowUntil = -1f;
    int launches; bool realArt;

    public void Init(EnemyDef d, Lane l, int w, float startDist = 0f){
      def = d; lane = l; wave = w; hp = d.hp; dist = startDist;
      realArt = Art.Has("enemy_" + d.id);
      baseColor = realArt ? Art.Tint : Art.Hex(d.color, new Color(0.55f, 0.25f, 0.2f));
      bodyT = new GameObject("Hull").transform; bodyT.SetParent(transform, false);   // unscaled pivot: rotates hull + sail together
      body = Art.Make(bodyT, "enemy_" + d.id, Art.Hull, new Vector2(d.w, d.h), baseColor, d.boss ? 9 : 10);
      if (!realArt){
        if (d.blastRange > 0f) Art.Make(bodyT, null, Art.Circle, new Vector2(d.h * 0.7f, d.h * 0.7f), new Color(1f, 0.85f, 0.2f), 11);       // fire
        else if (d.armor > 0f) Art.Make(bodyT, null, Art.Square, new Vector2(d.w * 0.55f, d.h * 0.5f), new Color(0.7f, 0.72f, 0.75f), 11);  // iron plate
        if (d.size != "small" || d.boss) Art.Make(bodyT, null, Art.Square, new Vector2(0.14f, d.h * 0.85f), new Color(0.95f, 0.93f, 0.85f), 12);   // sail
        if (d.boss) Art.Make(bodyT, null, Art.Square, new Vector2(0.14f, d.h * 0.85f), new Color(0.15f, 0.12f, 0.12f), 12, new Vector2(-d.w * 0.25f, 0));
        if (d.size == "small" && d.blastRange <= 0f) Art.Make(bodyT, null, Art.Square, new Vector2(0.14f, d.h * 0.85f), new Color(0.95f, 0.93f, 0.85f), 11);
      }
      var barRoot = new GameObject("HpBar").transform; barRoot.SetParent(transform, false); barRoot.localPosition = new Vector3(0, d.h * 0.5f + 0.25f);
      float bw = d.boss ? 2f : 0.8f;
      Art.Make(barRoot, null, Art.Square, new Vector2(bw, 0.1f), new Color(0.1f, 0.1f, 0.1f, 0.8f), 13);
      bar = Art.Make(barRoot, null, Art.Square, new Vector2(bw, 0.1f), d.boss ? new Color(0.95f, 0.3f, 0.3f) : new Color(0.3f, 0.95f, 0.3f), 14).transform;
      barRoot.gameObject.SetActive(d.boss);
      Place();
    }
    void Place(){
      transform.position = lane.Pos(dist);
      var dir = lane.Dir(dist); bodyT.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }
    void Update(){
      if (sinkT >= 0f){
        sinkT += Time.deltaTime; float k = Mathf.Clamp01(1f - sinkT / 0.6f);
        bodyT.localScale = Vector3.one * (0.4f + 0.6f * k); body.color = new Color(baseColor.r, baseColor.g, baseColor.b, k);
        if (sinkT >= 0.6f) Destroy(gameObject);
        return;
      }
      if (Dead) return;
      dist += Speed * Time.deltaTime;
      if (dist >= lane.Length){ Game.I.ShipReachedHarbor(this); return; }
      Place();
      if (def.blastRange > 0f){ var near = Game.I.NearestDefense(transform.position, def.blastRange); if (near){ Game.I.RaftExploded(this, near); return; } }
      Color c = SpeedMul < 1f ? Color.Lerp(baseColor, new Color(0.6f, 0.75f, 1f), 0.45f) : baseColor;   // slowed: blue tint
      if (flash > 0f){ flash -= Time.deltaTime; if (flash > 0f) c = Color.Lerp(c, Color.white, 0.6f); }
      body.color = c;
    }
    public void ApplySlow(float factor, float seconds){
      float now = Time.time;
      if (now > slowUntil){ slowFactor = factor; slowUntil = now + seconds; }
      else { slowFactor = Mathf.Min(slowFactor, factor); slowUntil = Mathf.Max(slowUntil, now + seconds); }
    }
    /// <summary>Armor blocks `armor` of the damage, minus the shot's pierce share.</summary>
    public void Damage(float dmg, float pierce = 0f){
      if (Dead) return;
      hp -= dmg * (1f - def.armor * (1f - Mathf.Clamp01(pierce))); flash = 0.1f; GameEvents.Play("ball_hit");
      bar.parent.gameObject.SetActive(true);
      float f = Mathf.Clamp01(hp / def.hp), bw = def.boss ? 2f : 0.8f;
      bar.localScale = new Vector3(bw * f / Art.Square.bounds.size.x, bar.localScale.y, 1); bar.localPosition = new Vector3(-bw * 0.5f * (1f - f), 0);
      if (hp <= 0f){ Game.I.ShipSunk(this); return; }
      if (!string.IsNullOrEmpty(def.spawnEnemy) && def.spawnEvery > 0f){
        int due = Mathf.FloorToInt((1f - hp / def.hp) / def.spawnEvery);
        while (launches < due){ launches++; Game.I.Launch(this, def.spawnEnemy, def.spawnCount); }
      }
    }
    public void Sink(){ sinkT = 0f; if (bar) bar.parent.gameObject.SetActive(false); }
  }
}
