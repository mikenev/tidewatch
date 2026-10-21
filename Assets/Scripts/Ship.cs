using UnityEngine;
namespace Tidewatch {
  /// <summary>An enemy ship sailing its lane toward the harbor.</summary>
  public class Ship : MonoBehaviour {
    public EnemyDef def; public Lane lane; public int wave; public float dist, hp; public bool Dead;
    public float Remaining => lane.Length - dist;
    SpriteRenderer body; Transform bodyT, bar; Color baseColor; float flash, sinkT = -1f;

    public void Init(EnemyDef d, Lane l, int w){
      def = d; lane = l; wave = w; hp = d.hp;
      baseColor = Art.Hex(d.color, new Color(0.55f, 0.25f, 0.2f));
      bodyT = new GameObject("Hull").transform; bodyT.SetParent(transform, false);   // unscaled pivot: rotates hull + sail together
      body = Art.Make(bodyT, "enemy_" + d.id, Art.Hull, new Vector2(d.w, d.h), baseColor, 10);
      if (!Art.Has("enemy_" + d.id)) Art.Make(bodyT, null, Art.Square, new Vector2(0.14f, d.h * 0.85f), new Color(0.95f, 0.93f, 0.85f), 11);   // placeholder sail
      var barRoot = new GameObject("HpBar").transform; barRoot.SetParent(transform, false); barRoot.localPosition = new Vector3(0, d.h * 0.5f + 0.25f);
      Art.Make(barRoot, null, Art.Square, new Vector2(0.8f, 0.1f), new Color(0.1f, 0.1f, 0.1f, 0.8f), 12);
      bar = Art.Make(barRoot, null, Art.Square, new Vector2(0.8f, 0.1f), new Color(0.3f, 0.95f, 0.3f), 13).transform;
      barRoot.gameObject.SetActive(false);
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
      dist += def.speed * Time.deltaTime;
      if (dist >= lane.Length){ Game.I.ShipReachedHarbor(this); return; }
      Place();
      if (flash > 0f){ flash -= Time.deltaTime; body.color = flash > 0f ? Color.Lerp(baseColor, Color.white, 0.6f) : (Art.Has("enemy_" + def.id) ? Color.white : baseColor); }
    }
    public void Damage(float dmg){
      if (Dead) return;
      hp -= dmg; flash = 0.1f; GameEvents.Play("ball_hit");
      bar.parent.gameObject.SetActive(true);
      float f = Mathf.Clamp01(hp / def.hp); bar.localScale = new Vector3(0.8f * f / Art.Square.bounds.size.x, bar.localScale.y, 1); bar.localPosition = new Vector3(-0.4f * (1f - f), 0);
      if (hp <= 0f) Game.I.ShipSunk(this);
    }
    public void Sink(){ sinkT = 0f; if (bar) bar.parent.gameObject.SetActive(false); if (Art.Has("enemy_" + def.id)) baseColor = Color.white; }
  }
}
