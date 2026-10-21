using UnityEditor;
using UnityEngine;

/// <summary>Applies the Assets/ART_AUDIO_LIST.md import settings to art and audio dropped into Resources:
/// sprites at 100 PPU, bilinear, no mips, full-rect; SFX Decompress On Load / Vorbis 0.4; music Streaming / Vorbis 0.5.</summary>
public class TidewatchImport : AssetPostprocessor {
  void OnPreprocessTexture(){
    if (!assetPath.StartsWith("Assets/Resources/Sprites/")) return;
    var ti = (TextureImporter)assetImporter;
    bool sheet = System.IO.Path.GetFileName(assetPath).StartsWith("fx_");
    ti.textureType = TextureImporterType.Sprite;
    ti.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
    ti.spritePixelsPerUnit = 100; ti.filterMode = FilterMode.Bilinear; ti.wrapMode = TextureWrapMode.Clamp;
    ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.maxTextureSize = 2048;
    ti.textureCompression = TextureImporterCompression.Compressed;
    var st = new TextureImporterSettings(); ti.ReadTextureSettings(st);
    st.spriteMeshType = SpriteMeshType.FullRect;
    if (System.IO.Path.GetFileNameWithoutExtension(assetPath) == "tower_cannon_barrel"){ st.spriteAlignment = (int)SpriteAlignment.Custom; st.spritePivot = new Vector2(0f, 0.5f); }
    ti.SetTextureSettings(st);
  }
  void OnPreprocessAudio(){
    if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
    var ai = (AudioImporter)assetImporter; bool music = assetPath.Contains("/Music/");
    var s = ai.defaultSampleSettings;
    s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
    s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = music ? 0.5f : 0.4f; s.preloadAudioData = true;
    ai.defaultSampleSettings = s; ai.loadInBackground = music;
  }
}
