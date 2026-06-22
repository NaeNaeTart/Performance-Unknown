using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace UnknownPerformance
{
    public class PlaygroundSimplifier : MonoBehaviour
    {
        private static PlaygroundSimplifier? _instance;
        public static PlaygroundSimplifier Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("PlaygroundSimplifier");
                    _instance = go.AddComponent<PlaygroundSimplifier>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private float _timeSinceLastSweep = 0f;
        private const float SweepInterval = 0.25f; // 4 times a second for snappy responsiveness

        // Cache system to prevent duplicate texture/sprite creations and CPU/GPU memory leaks
        private static readonly Dictionary<Sprite, Sprite> _silhouetteSpriteCache = new Dictionary<Sprite, Sprite>();
        private static readonly Dictionary<Sprite, Sprite> _blockSpriteCache = new Dictionary<Sprite, Sprite>();
        private static readonly Dictionary<Sprite, Color> _spriteColorCache = new Dictionary<Sprite, Color>();

        private static readonly HashSet<int> _processedRenderers = new HashSet<int>();
        private static readonly HashSet<int> _processedTilemaps = new HashSet<int>();

        private static Material? _guiMaterial;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResetSweepCaches();
        }

        public void ResetSweepCaches()
        {
            _processedRenderers.Clear();
            _processedTilemaps.Clear();
            Plugin.Log.LogInfo("[PlaygroundSimplifier] Caches cleared for scene reload/settings transition.");
        }

        private void Update()
        {
            // Retrieve current settings
            int mode = Plugin.Cfg.PlaygroundSprites.Value; // 0 = Off, 1 = Silhouette, 2 = Flat Block
            bool disableFoliage = Plugin.Cfg.DisableFoliage.Value;

            if (mode == 0 && !disableFoliage) return;

            _timeSinceLastSweep += Time.deltaTime;
            if (_timeSinceLastSweep >= SweepInterval)
            {
                _timeSinceLastSweep = 0f;
                ApplyPlaygroundSimplification(mode, disableFoliage);
            }
        }

        public void ApplyPlaygroundSimplification(int mode, bool disableFoliage)
        {
            try
            {
                // 1. Process all SpriteRenderers
                var renderers = FindObjectsOfType<SpriteRenderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    int id = renderer.GetInstanceID();

                    // Check if foliage or decor to disable
                    if (disableFoliage && IsFoliageOrDecor(renderer))
                    {
                        renderer.enabled = false;
                        _processedRenderers.Add(id);
                        continue;
                    }

                    if (mode > 0 && !_processedRenderers.Contains(id))
                    {
                        SimplifySpriteRenderer(renderer, mode);
                        _processedRenderers.Add(id);
                    }
                }

                // 2. Process all Tilemaps
                var tilemaps = FindObjectsOfType<Tilemap>();
                foreach (var tilemap in tilemaps)
                {
                    if (tilemap == null) continue;
                    int id = tilemap.GetInstanceID();

                    if (mode > 0 && !_processedTilemaps.Contains(id))
                    {
                        SimplifyTilemap(tilemap, mode);
                        _processedTilemaps.Add(id);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[PlaygroundSimplifier] Error in ApplyPlaygroundSimplification: " + ex.Message);
            }
        }

        private static bool IsFoliageOrDecor(SpriteRenderer r)
        {
            if (r == null) return false;
            string name = r.gameObject.name.ToLower();
            if (name.Contains("grass") || name.Contains("foliage") || name.Contains("flora") || 
                name.Contains("moss") || name.Contains("shrub") || name.Contains("plant") ||
                name.Contains("vines") || name.Contains("sandvine") || name.Contains("droppings") ||
                name.Contains("debris") || name.Contains("foliagedecor"))
            {
                return true;
            }

            if (r.sprite != null)
            {
                string spriteName = r.sprite.name.ToLower();
                if (spriteName.Contains("grass") || spriteName.Contains("foliage") || spriteName.Contains("flora") || 
                    spriteName.Contains("moss") || spriteName.Contains("shrub") || spriteName.Contains("plant") ||
                    spriteName.Contains("vine") || spriteName.Contains("debris"))
                {
                    return true;
                }
            }
            return false;
        }

        private static void SimplifySpriteRenderer(SpriteRenderer renderer, int mode)
        {
            if (renderer.sprite == null) return;

            Sprite original = renderer.sprite;
            Sprite? simplified = GetSimplifiedSprite(original, mode, out Color avgColor);

            if (simplified != null)
            {
                renderer.sprite = simplified;
                // Preserve transparency modulation if the original renderer was faded
                renderer.color = new Color(avgColor.r, avgColor.g, avgColor.b, renderer.color.a * avgColor.a);
            }
        }

        private static void SimplifyTilemap(Tilemap tilemap, int mode)
        {
            try
            {
                BoundsInt bounds = tilemap.cellBounds;
                TileBase[] allTiles = tilemap.GetTilesBlock(bounds);

                var uniqueTiles = new HashSet<Tile>();
                foreach (var tileBase in allTiles)
                {
                    if (tileBase is Tile tile)
                    {
                        uniqueTiles.Add(tile);
                    }
                }

                bool refreshed = false;
                foreach (var tile in uniqueTiles)
                {
                    if (tile == null || tile.sprite == null) continue;

                    Sprite original = tile.sprite;
                    Sprite? simplified = GetSimplifiedSprite(original, mode, out Color avgColor);

                    if (simplified != null)
                    {
                        tile.sprite = simplified;
                        tile.color = new Color(avgColor.r, avgColor.g, avgColor.b, tile.color.a * avgColor.a);
                        refreshed = true;
                    }
                }

                if (refreshed)
                {
                    tilemap.RefreshAllTiles();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[PlaygroundSimplifier] Failed to simplify tilemap: " + ex.Message);
            }
        }

        public static Sprite? GetSimplifiedSprite(Sprite original, int mode, out Color avgColor)
        {
            avgColor = Color.white;
            if (original == null) return null;

            var cache = (mode == 1) ? _silhouetteSpriteCache : _blockSpriteCache;
            if (cache.TryGetValue(original, out Sprite cached))
            {
                _spriteColorCache.TryGetValue(original, out avgColor);
                return cached;
            }

            try
            {
                Sprite simplified = CreateSimplifiedSpriteAsset(original, mode, out avgColor);
                cache[original] = simplified;
                _spriteColorCache[original] = avgColor;
                return simplified;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[PlaygroundSimplifier] Failed to simplify sprite '{original.name}': {ex.Message}. Falling back to original.");
                return original;
            }
        }

        private static Sprite CreateSimplifiedSpriteAsset(Sprite original, int mode, out Color averageColor)
        {
            averageColor = Color.white;
            Texture2D tex = original.texture;
            if (tex == null) return original;

            // Downsample target resolution to keep calculations instant and texture memory extremely low
            int targetWidth = Mathf.Min((int)original.rect.width, 32);
            int targetHeight = Mathf.Min((int)original.rect.height, 32);
            targetWidth = Mathf.Max(targetWidth, 1);
            targetHeight = Mathf.Max(targetHeight, 1);

            // Create temporary RenderTexture
            RenderTexture tmp = RenderTexture.GetTemporary(
                targetWidth,
                targetHeight,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear
            );

            // Copy the sprite's exact source rectangle onto the downsampled RenderTexture
            DrawTexturePart(tex, original.rect, tmp);

            // Read pixels back to CPU
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;

            Texture2D readableTex = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            readableTex.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            readableTex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);

            // Compute average color of visible pixels & flatten texture
            Color[] pixels = readableTex.GetPixels();
            float rSum = 0, gSum = 0, bSum = 0, aSum = 0;
            int count = 0;

            bool keepAlpha = (mode == 1);

            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                if (p.a > 0.05f)
                {
                    rSum += p.r;
                    gSum += p.g;
                    bSum += p.b;
                    aSum += p.a;
                    count++;
                }
                // Convert to white (so it is colored purely by the renderer color multiplier)
                pixels[i] = new Color(1f, 1f, 1f, keepAlpha ? p.a : 1f);
            }

            if (count > 0)
            {
                averageColor = new Color(rSum / count, gSum / count, bSum / count, aSum / count);
            }
            else
            {
                averageColor = Color.white;
            }

            readableTex.SetPixels(pixels);
            readableTex.filterMode = FilterMode.Point; // Crisp retro/playground scaling
            readableTex.wrapMode = TextureWrapMode.Clamp;
            readableTex.Apply();

            // Calculate pivot coordinates normalized to [0, 1]
            float pivotX = original.pivot.x / original.rect.width;
            float pivotY = original.pivot.y / original.rect.height;
            Vector2 pivot = new Vector2(pivotX, pivotY);

            // Scaled pixelsPerUnit to maintain exact in-game world scale
            float scaleFactor = (float)targetWidth / original.rect.width;
            float pixelsPerUnit = original.pixelsPerUnit * scaleFactor;

            Sprite simplified = Sprite.Create(readableTex, new Rect(0, 0, targetWidth, targetHeight), pivot, pixelsPerUnit);
            simplified.name = original.name + "_simplified_" + mode;

            return simplified;
        }

        private static void DrawTexturePart(Texture2D source, Rect sourceRect, RenderTexture dest)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = dest;

            GL.PushMatrix();
            GL.LoadOrtho();

            // Clear to transparent
            GL.Clear(true, true, Color.clear);

            Material mat = GetDefaultGUIMaterial();
            if (mat != null)
            {
                mat.mainTexture = source;
                for (int i = 0; i < mat.passCount; i++)
                {
                    if (mat.SetPass(i))
                    {
                        GL.Begin(GL.QUADS);
                        float uMin = sourceRect.x / source.width;
                        float uMax = sourceRect.xMax / source.width;
                        float vMin = sourceRect.y / source.height;
                        float vMax = sourceRect.yMax / source.height;

                        GL.TexCoord2(uMin, vMin); GL.Vertex3(0f, 0f, 0.1f);
                        GL.TexCoord2(uMin, vMax); GL.Vertex3(0f, 1f, 0.1f);
                        GL.TexCoord2(uMax, vMax); GL.Vertex3(1f, 1f, 0.1f);
                        GL.TexCoord2(uMax, vMin); GL.Vertex3(1f, 0f, 0.1f);
                        GL.End();
                    }
                }
            }

            GL.PopMatrix();
            RenderTexture.active = previous;
        }

        private static Material GetDefaultGUIMaterial()
        {
            if (_guiMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default") ?? Shader.Find("Legacy Shaders/Transparent/Diffuse");
                if (shader != null)
                {
                    _guiMaterial = new Material(shader);
                }
            }
            return _guiMaterial!;
        }
    }
}
