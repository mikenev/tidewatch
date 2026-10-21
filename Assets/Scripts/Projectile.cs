using UnityEngine;
namespace Tidewatch {
  /// <summary>Cannonball. Direct shots home on the target; if it sinks first the ball lands where it was and does nothing.
  /// Splash shots (mortar) are lobbed at a led ground point and hit every ship within the splash radius there.</summary>
  public class Projectile : MonoBehaviour {
    Ship target; Vector2 aim; DefenseLevel L; bool splash;
    public static void Fire(Vector2 from, Ship t, DefenseLevel level){
      var go = new GameObject("Cannonball"); go.transform.position = from;
      var p = go.AddComponent<Projectile>(); p.target = t; p.L = level; p.splash = level.splash > 0f;
      p.aim = t.transform.position;
      if (p.splash){   // lead the target along its lane by the flight time
        float flight = Vector2.Distance(from, p.aim) / level.projectileSpeed;
        p.aim = t.lane.Pos(t.dist + t.Speed * flight);
      }
      Art.Make(go.transform, "cannonball", Art.Circle, Vector2.one * (p.splash ? 0.32f : 0.2f), new Color(0.15f, 0.15f, 0.18f), 20);
    }
    void Update(){
      if (!splash && target && !target.Dead) aim = target.transform.position;
      var pos = Vector2.MoveTowards(transform.position, aim, L.projectileSpeed * Time.deltaTime); transform.position = pos;
      if ((pos - aim).sqrMagnitude > 0.0025f) return;
      if (splash){
        var g = Game.I;
        if (g) foreach (var s in g.ShipsInRange(aim, L.splash)) s.Damage(L.damage, L.pierce);
        Fx.Splash(aim, L.splash); GameEvents.Play("mortar_impact");
      } else if (target && !target.Dead){
        if (L.slow > 0f) target.ApplySlow(L.slow, L.slowTime);
        target.Damage(L.damage, L.pierce);
      }
      Destroy(gameObject);
    }
  }

  /// <summary>Placeholder one-shot effects (expanding fading circle).</summary>
  public class Fx : MonoBehaviour {
    SpriteRenderer sr; float t, life, size; Color c;
    public static void Splash(Vector2 at, float radius) => Make(at, radius * 2f, new Color(1f, 1f, 1f, 0.5f), 0.35f);
    public static void Blast(Vector2 at, float radius) => Make(at, radius * 2f, new Color(1f, 0.5f, 0.1f, 0.6f), 0.5f);
    static void Make(Vector2 at, float size, Color c, float life){
      var go = new GameObject("Fx"); go.transform.position = at; var f = go.AddComponent<Fx>();
      f.sr = Art.Make(go.transform, null, Art.Circle, Vector2.one, c, 25); f.c = c; f.life = life; f.size = size;
    }
    void Update(){
      t += Time.deltaTime; float k = Mathf.Clamp01(t / life);
      transform.localScale = Vector3.one * size * (0.4f + 0.6f * k); sr.color = new Color(c.r, c.g, c.b, c.a * (1f - k));
      if (k >= 1f) Destroy(gameObject);
    }
  }
}
