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
        private const float SweepInterval = 0.25f; // Periodic sweep to optimize dynamically spawned chunks

        // Cache system to prevent duplicate texture/sprite creations
        private static readonly Dictionary<Sprite, Sprite> _silhouetteSpriteCache = new Dictionary<Sprite, Sprite>();
        private static readonly Dictionary<Sprite, Sprite> _blockSpriteCache = new Dictionary<Sprite, Sprite>();
        private static readonly Dictionary<Sprite, Color> _spriteColorCache = new Dictionary<Sprite, Color>();

        // Tracks already processed components to avoid redundant calculations
        private static readonly HashSet<int> _processedRenderers = new HashSet<int>();
        private static readonly HashSet<int> _processedTilemaps = new HashSet<int>();

        // Restore caches to revert changes instantly when toggling settings in real-time
        private static readonly Dictionary<SpriteRenderer, Sprite> _originalSprites = new Dictionary<SpriteRenderer, Sprite>();
        private static readonly Dictionary<SpriteRenderer, Color> _originalColors = new Dictionary<SpriteRenderer, Color>();
        private static readonly Dictionary<Tile, Sprite> _originalTileSprites = new Dictionary<Tile, Sprite>();
        private static readonly Dictionary<Tile, Color> _originalTileColors = new Dictionary<Tile, Color>();
        private static readonly HashSet<SpriteRenderer> _disabledFoliageRenderers = new HashSet<SpriteRenderer>();

        private static Material? _guiMaterial;
        private int _lastMode = 0;
        private bool _lastDisableFoliage = false;

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
            // Clear memory-leak-prone references upon scene changes
            _processedRenderers.Clear();
            _processedTilemaps.Clear();
            _originalSprites.Clear();
            _originalColors.Clear();
            _disabledFoliageRenderers.Clear();
        }

        public void ResetSweepCaches()
        {
            _processedRenderers.Clear();
            _processedTilemaps.Clear();
        }

        private void Update()
        {
            int mode = Plugin.Cfg.PlaygroundSprites.Value; // 0 = Off, 1 = Silhouette, 2 = Flat Block
            bool disableFoliage = Plugin.Cfg.DisableFoliage.Value;

            // Handle transition states (toggled settings mid-game)
            if (mode != _lastMode || disableFoliage != _lastDisableFoliage)
            {
                if (mode == 0 && _lastMode > 0)
                {
                    RestoreOriginalSprites();
                }
                if (!disableFoliage && _lastDisableFoliage)
                {
                    RestoreOriginalFoliage();
                }
                
                _lastMode = mode;
                _lastDisableFoliage = disableFoliage;
                ResetSweepCaches();
            }

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
                // 1. Process SpriteRenderers
                var renderers = FindObjectsOfType<SpriteRenderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    int id = renderer.GetInstanceID();

                    // Check if foliage or decor to disable
                    if (disableFoliage && IsFoliageOrDecor(renderer))
                    {
                        if (renderer.enabled)
                        {
                            renderer.enabled = false;
                            _disabledFoliageRenderers.Add(renderer);
                        }
                        _processedRenderers.Add(id);
                        continue;
                    }

                    if (mode > 0 && !_processedRenderers.Contains(id))
                    {
                        // Check if we should exempt characters, items, weapons, UI, etc.
                        if (ShouldExempt(renderer))
                        {
                            _processedRenderers.Add(id);
                            continue;
                        }

                        SimplifySpriteRenderer(renderer, mode);
                        _processedRenderers.Add(id);
                    }
                }

                // 2. Process Tilemaps
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

        private static bool ShouldExempt(SpriteRenderer r)
        {
            if (r == null) return true;

            // 1. Direct name checks for crucial interactive/vital entities
            string name = r.gameObject.name.ToLower();
            if (name.Contains("player") || name.Contains("body") || name.Contains("limb") || 
                name.Contains("head") || name.Contains("arm") || name.Contains("leg") || 
                name.Contains("hand") || name.Contains("foot") || name.Contains("eye") || 
                name.Contains("ear") || name.Contains("item") || name.Contains("weapon") || 
                name.Contains("bullet") || name.Contains("projectile") || name.Contains("monster") || 
                name.Contains("enemy") || name.Contains("creature") || name.Contains("trader") ||
                name.Contains("terminal") || name.Contains("console") || name.Contains("ui") ||
                name.Contains("canvas") || name.Contains("hud") || name.Contains("text") ||
                name.Contains("cursor") || name.Contains("moodle"))
            {
                return true;
            }

            // 2. Traversal up parent hierarchy to detect characters, NPCs, items, weapons, and UI
            Transform? curr = r.transform;
            while (curr != null)
            {
                string currName = curr.gameObject.name.ToLower();
                if (currName.Contains("player") || currName.Contains("trader") || 
                    currName.Contains("enemy") || currName.Contains("monster") || 
                    currName.Contains("ui") || currName.Contains("terminal") || 
                    currName.Contains("console"))
                {
                    return true;
                }

                // Check component type names to bypass direct assembly referencing limits
                Component[] comps = curr.GetComponents<Component>();
                foreach (var comp in comps)
                {
                    if (comp == null) continue;
                    string typeName = comp.GetType().Name.ToLower();
                    if (typeName.Contains("body") || typeName.Contains("limb") || typeName.Contains("item") || 
                        typeName.Contains("trader") || typeName.Contains("player") || typeName.Contains("creature") || 
                        typeName.Contains("npc") || typeName.Contains("enemy") || typeName.Contains("monster") || 
                        typeName.Contains("weapon") || typeName.Contains("bullet") || typeName.Contains("projectile") ||
                        typeName.Contains("ui") || typeName.Contains("canvas") || typeName.Contains("hud") ||
                        typeName.Contains("moodle"))
                    {
                        return true;
                    }
                }
                curr = curr.parent;
            }

            return false;
        }

        private static void SimplifySpriteRenderer(SpriteRenderer renderer, int mode)
        {
            if (renderer.sprite == null) return;

            Sprite original = renderer.sprite;

            // Cache original sprite and color before simplification
            if (!_originalSprites.ContainsKey(renderer))
            {
                _originalSprites[renderer] = original;
                _originalColors[renderer] = renderer.color;
            }

            Sprite? simplified = GetSimplifiedSprite(original, mode, out Color avgColor);

            if (simplified != null)
            {
                renderer.sprite = simplified;
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

                    // Cache original tile values
                    if (!_originalTileSprites.ContainsKey(tile))
                    {
                        _originalTileSprites[tile] = original;
                        _originalTileColors[tile] = tile.color;
                    }

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

            int targetWidth = Mathf.Min((int)original.rect.width, 32);
            int targetHeight = Mathf.Min((int)original.rect.height, 32);
            targetWidth = Mathf.Max(targetWidth, 1);
            targetHeight = Mathf.Max(targetHeight, 1);

            RenderTexture tmp = RenderTexture.GetTemporary(
                targetWidth,
                targetHeight,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear
            );

            DrawTexturePart(tex, original.rect, tmp);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;

            Texture2D readableTex = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            readableTex.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            readableTex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);

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
            readableTex.filterMode = FilterMode.Point;
            readableTex.wrapMode = TextureWrapMode.Clamp;
            readableTex.Apply();

            float pivotX = original.pivot.x / original.rect.width;
            float pivotY = original.pivot.y / original.rect.height;
            Vector2 pivot = new Vector2(pivotX, pivotY);

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

        public void RestoreOriginalSprites()
        {
            Plugin.Log.LogInfo("[PlaygroundSimplifier] Reverting all modified SpriteRenderers & Tiles back to their original texture details...");
            
            // Restore SpriteRenderers
            foreach (var kvp in _originalSprites)
            {
                var r = kvp.Key;
                if (r != null)
                {
                    r.sprite = kvp.Value;
                    if (_originalColors.TryGetValue(r, out Color col))
                    {
                        r.color = col;
                    }
                }
            }
            _originalSprites.Clear();
            _originalColors.Clear();

            // Restore Tiles
            bool refreshed = false;
            foreach (var kvp in _originalTileSprites)
            {
                var tile = kvp.Key;
                if (tile != null)
                {
                    tile.sprite = kvp.Value;
                    if (_originalTileColors.TryGetValue(tile, out Color col))
                    {
                        tile.color = col;
                    }
                    refreshed = true;
                }
            }
            _originalTileSprites.Clear();
            _originalTileColors.Clear();

            if (refreshed)
            {
                var tilemaps = FindObjectsOfType<Tilemap>();
                foreach (var tilemap in tilemaps)
                {
                    if (tilemap != null)
                    {
                        tilemap.RefreshAllTiles();
                    }
                }
            }
        }

        public void RestoreOriginalFoliage()
        {
            Plugin.Log.LogInfo("[PlaygroundSimplifier] Restoring all disabled decorative foliage & grass objects...");
            foreach (var r in _disabledFoliageRenderers)
            {
                if (r != null)
                {
                    r.enabled = true;
                }
            }
            _disabledFoliageRenderers.Clear();
        }
    }
}
