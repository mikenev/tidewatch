using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Tidewatch;

/// <summary>Headless scene setup + WebGL build (pipeline from Broomstick Bash Free Flight). Usage:
/// Unity -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod BuildWeb.Build -logFile build.log
/// (no -quit; this calls EditorApplication.Exit). TW_DEV=1 makes a development build in Builds/WebGLDev (gitignored).</summary>
public static class BuildWeb {
  const string ScenePath = "Assets/Scenes/Main.unity";

  [MenuItem("Tidewatch/Rebuild Main Scene")]
  public static void SetupScene(){
    Directory.CreateDirectory("Assets/Scenes");
    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    var camGo = new GameObject("Main Camera"){ tag = "MainCamera" };
    var cam = camGo.AddComponent<Camera>();
    cam.orthographic = true; cam.orthographicSize = 5.4f; cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = new Color(0.13f, 0.55f, 0.72f); cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;
    camGo.transform.position = new Vector3(0, 0, -10); camGo.AddComponent<AudioListener>();
    var g = new GameObject("Game"); g.AddComponent<Game>(); g.AddComponent<Hud>(); g.AddComponent<GameAudio>();
    EditorSceneManager.SaveScene(scene, ScenePath);
    EditorBuildSettings.scenes = new[]{ new EditorBuildSettingsScene(ScenePath, true) };
  }

  /// <summary>Fails the build on a broken wave/lane/defense config.</summary>
  static void ValidateConfig(){
    var ta = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/" + GameConfigData.ResourcePath + ".json");
    if (!ta) throw new System.Exception("config missing");
    var errs = GameConfigData.Parse(ta.text).Validate();
    foreach (var e in errs) Debug.LogError("[Config] " + e);
    if (errs.Count > 0) throw new System.Exception($"config has {errs.Count} error(s)");
    Debug.Log("VALIDATE CONFIG: PASS");
  }

  /// <summary>Page hardening for mobile + desktop, injected into the generated index.html after every build:
  /// canvas touch-action:none and preventDefault on touch events (no zoom / pull-to-refresh / long-press callout),
  /// no context menu on the canvas, and Space / arrow keys don't scroll the page. Unity still receives all events.</summary>
  static void PatchIndexHtml(string outDir){
    var p = Path.Combine(outDir, "index.html"); if (!File.Exists(p)) return;
    var html = File.ReadAllText(p); if (html.Contains("tw-input-guard")) return;
    const string css = "<style id=\"tw-input-guard-css\">html,body{overscroll-behavior:none;}#unity-canvas{touch-action:none;-ms-touch-action:none;user-select:none;-webkit-user-select:none;-webkit-touch-callout:none;-webkit-tap-highlight-color:transparent;outline:none}</style>";
    const string js = @"<script id=""tw-input-guard"">(function(){
  var c=document.getElementById('unity-canvas'); if(!c) return;
  var stop=function(e){ if(e.cancelable) e.preventDefault(); };
  ['touchstart','touchmove','touchend','touchcancel'].forEach(function(t){ c.addEventListener(t,stop,{passive:false}); });
  ['gesturestart','gesturechange','gestureend','dblclick','contextmenu'].forEach(function(t){ c.addEventListener(t,stop,{passive:false}); });
  document.addEventListener('contextmenu',function(e){ if(e.target===c) e.preventDefault(); });
  window.addEventListener('keydown',function(e){
    var k=e.key||'', tag=(e.target&&e.target.tagName)||'';
    if((k===' '||k==='Spacebar'||k.indexOf('Arrow')===0) && !/INPUT|TEXTAREA|SELECT/.test(tag)) e.preventDefault();
  },{passive:false});
  if(!c.hasAttribute('tabindex')) c.setAttribute('tabindex','-1');
  c.addEventListener('pointerdown',function(){ try{ c.focus(); }catch(_){} });
})();</script>";
    html = html.Replace("</head>", css + "\n  </head>").Replace("</body>", js + "\n  </body>");
    File.WriteAllText(p, html);
  }

  [MenuItem("Tidewatch/Build WebGL to docs")]
  public static void Build(){
    int code = 1;
    try {
      ValidateConfig();
      SetupScene();
      PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
      PlayerSettings.WebGL.decompressionFallback = true;
      PlayerSettings.WebGL.threadsSupport = false;
      PlayerSettings.productName = "Tidewatch"; PlayerSettings.companyName = "mikenev";
      PlayerSettings.defaultWebScreenWidth = 1280; PlayerSettings.defaultWebScreenHeight = 720;
      PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
      bool dev = System.Environment.GetEnvironmentVariable("TW_DEV") == "1";
      if (dev){ PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace; PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.Full); }
      else PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
      var outDir = Path.GetFullPath(dev ? "Builds/WebGLDev" : "docs");
      var r = BuildPipeline.BuildPlayer(new[]{ ScenePath }, outDir, BuildTarget.WebGL, dev ? BuildOptions.Development : BuildOptions.None);
      Debug.Log("BUILD RESULT: " + r.summary.result + " size=" + r.summary.totalSize);
      if (r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded){ File.WriteAllText(Path.Combine(outDir, ".nojekyll"), ""); PatchIndexHtml(outDir); code = 0; }
    } catch (System.Exception e){ Debug.LogException(e); }
    if (Application.isBatchMode) EditorApplication.Exit(code);
  }
}
