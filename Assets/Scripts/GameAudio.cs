using System.Collections.Generic;
using UnityEngine;
namespace Tidewatch {
  /// <summary>Central audio (Broomstick Bash pattern): music crossfade + rate-limited one-shot SFX by id.
  /// Clips load from Resources/Audio (see Assets/ART_AUDIO_LIST.md); a missing clip is simply silent.
  /// WebGL autoplay: nothing plays until the first key, click or touch; the unlock persists across restarts.</summary>
  public class GameAudio : MonoBehaviour {
    public static GameAudio I;
    [Range(0, 1)] public float musicVolume = 0.35f, sfxVolume = 0.85f;
    public float crossfade = 1.2f;
    static bool unlocked;
    public bool Unlocked => unlocked;
    AudioClip theme, battle; AudioSource musA, musB, sfx, active; AudioClip target;
    struct Rule { public AudioClip clip; public float vol, minGap; }
    readonly Dictionary<string, Rule> rules = new(); readonly Dictionary<string, float> last = new();
    GameState lastState;

    static AudioClip Clip(string path) => Resources.Load<AudioClip>("Audio/" + path);
    void Awake(){
      I = this;
      musA = NewSource(true); musB = NewSource(true); sfx = NewSource(false); sfx.volume = 1f; active = musA;   // one-shots scale by PlayOneShot volume; source must be at 1
      theme = Clip("Music/theme_shanty_loop"); battle = Clip("Music/battle_loop");
      // "a|b": use the first file that exists (dedicated M2 sounds fall back to M1 clips until Audio Dev adds them)
      void R(string id, string files, float v, float gap){ AudioClip c = null; foreach (var f in files.Split('|')) if (!c) c = Clip(f); rules[id] = new Rule{ clip = c, vol = v, minGap = gap }; }
      R("cannon_fire", "SFX/cannon_fire", 0.5f, 0.08f); R("gunboat_fire", "SFX/gunboat_fire", 0.35f, 0.08f);
      R("ball_hit", "SFX/ball_hit", 0.4f, 0.06f); R("ship_sink", "SFX/ship_sink", 0.6f, 0.12f);
      R("harbor_breach", "SFX/harbor_breach", 0.8f, 0.3f); R("coin", "SFX/coin", 0.5f, 0.1f);
      R("build", "SFX/build", 0.6f, 0.1f); R("upgrade", "SFX/upgrade", 0.6f, 0.1f); R("sell", "SFX/sell", 0.6f, 0.1f);
      R("wave_horn", "SFX/wave_horn", 0.7f, 1f); R("ui_click", "SFX/ui_click", 0.35f, 0.08f); R("ui_error", "SFX/ui_error", 0.5f, 0.2f);
      R("mortar_fire", "SFX/mortar_fire|SFX/cannon_fire", 0.6f, 0.15f); R("chain_fire", "SFX/chain_fire|SFX/cannon_fire", 0.3f, 0.1f);
      R("raft_blast", "SFX/raft_blast|SFX/harbor_breach", 0.7f, 0.2f); R("launch", "SFX/galleon_launch|SFX/wave_horn", 0.5f, 0.5f);
      R("victory", "Music/victory_sting", 0.8f, 1f); R("defeat", "Music/defeat_sting", 0.8f, 1f);
    }
    AudioSource NewSource(bool loop){ var a = gameObject.AddComponent<AudioSource>(); a.playOnAwake = false; a.loop = loop; a.volume = 0; return a; }
    void OnEnable(){ GameEvents.Sfx += Play; }
    void OnDisable(){ GameEvents.Sfx -= Play; }
    void Start(){ Music(theme); }

    void Update(){
      if (!unlocked){
        if (!PointerInput.AnyInput()) return;
        unlocked = true; Debug.Log("[Audio] unlocked on first input");
      }
      var g = Game.I;
      if (g){
        if (g.State != lastState){ lastState = g.State; if (g.State == GameState.Won) Play("victory"); else if (g.State == GameState.Lost) Play("defeat"); }
        Music(g.State == GameState.Menu ? theme : g.State != GameState.Playing ? null : g.InBattle && battle ? battle : theme);
      }
      float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, crossfade);
      var other = active == musA ? musB : musA;
      active.volume = Mathf.MoveTowards(active.volume, target && active.clip == target ? musicVolume : 0f, step);
      other.volume = Mathf.MoveTowards(other.volume, 0f, step);
      if (other.isPlaying && other.volume <= 0.001f) other.Stop();
      if (!target && active.isPlaying && active.volume <= 0.001f) active.Stop();
    }

    /// <summary>Switch music with a crossfade; same track = keep playing. Null fades out.</summary>
    public void Music(AudioClip clip){
      target = clip;
      if (!unlocked || !clip || (active.clip == clip && active.isPlaying)) return;
      active = active == musA ? musB : musA; active.clip = clip; active.volume = 0; active.Play();
    }

    public void Play(string id){
      if (!unlocked || !rules.TryGetValue(id, out var r) || !r.clip) return;
      float now = Time.unscaledTime;
      if (last.TryGetValue(id, out var t) && now - t < r.minGap) return;
      last[id] = now; sfx.PlayOneShot(r.clip, r.vol * sfxVolume);
    }
  }
}
