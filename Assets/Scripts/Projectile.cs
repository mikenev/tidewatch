using UnityEngine;
namespace Tidewatch {
  /// <summary>Homing cannonball. If the target sinks first, it lands where the target was and does nothing.</summary>
  public class Projectile : MonoBehaviour {
    Ship target; Vector2 aim; float damage, speed;
    public static void Fire(Vector2 from, Ship t, float damage, float speed){
      var go = new GameObject("Cannonball"); go.transform.position = from;
      var p = go.AddComponent<Projectile>(); p.target = t; p.aim = t.transform.position; p.damage = damage; p.speed = speed;
      Art.Make(go.transform, "cannonball", Art.Circle, new Vector2(0.2f, 0.2f), new Color(0.15f, 0.15f, 0.18f), 20);
    }
    void Update(){
      if (target && !target.Dead) aim = target.transform.position;
      var pos = Vector2.MoveTowards(transform.position, aim, speed * Time.deltaTime); transform.position = pos;
      if ((pos - aim).sqrMagnitude < 0.0025f){
        if (target && !target.Dead) target.Damage(damage);
        Destroy(gameObject);
      }
    }
  }
}
