using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine.SceneManagement;
using ScavSetLib;

namespace UnknownPerformance
{
    public static class PluginInfo
    {
        public const string GUID = "com.kanisuko.unknownperformance";
        public const string Name = "Unknown Performance";
        public const string Version = "1.2.0";
    }

    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInDependency("com.kanisuko.scavsetlib")] // Depend on our custom Settings Injection API!
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; } = null!;
        public static ManualLogSource Log { get; private set; } = null!;
        private Harmony? _harmony;

        internal static ModConfig Cfg { get; private set; } = null!;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // 1. Initialize configuration
            Cfg = new ModConfig(Config);

            if (!Cfg.ModEnabled.Value)
            {
                Log.LogInfo($"[{PluginInfo.Name}] Disabled via configuration.");
                return;
            }

            // 2. Register Native settings inside ScavSetLib SettingsManager
            RegisterPerformanceOptions();

            // 3. Apply initial quality configurations
            ApplyPhysicsQuality(Cfg.PhysicsQuality.Value);
            ConfigureIncrementalGC();

            // 4. Hook into Scene Loads for clean garbage sweeps
            SceneManager.sceneLoaded += OnSceneLoaded;

            // 5. Instantiate the dynamic playground simplifier MonoBehaviour
            var _ = PlaygroundSimplifier.Instance;

            // 6. Apply Harmony Patches for active performance tuning
            _harmony = new Harmony(PluginInfo.GUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.LogInfo($"[{PluginInfo.Name} v{PluginInfo.Version}] Advanced performance suite loaded successfully!");
        }

        private void RegisterPerformanceOptions()
        {
            // Register Playground Sprites style dropdown
            SettingsManager.RegisterDropdown(
                name: "Perf_Playground_Sprites",
                category: Setting.SettingCategory.Video,
                choices: new string[] { "Off", "Flat Shapes", "Flat Blocks" },
                defaultValue: Cfg.PlaygroundSprites.Value,
                onApply: (val) => {
                    Cfg.PlaygroundSprites.Value = val;
                    Log.LogInfo($"Playground Sprites visual style changed: {val}");
                    PlaygroundSimplifier.Instance.ResetSweepCaches();
                    PlaygroundSimplifier.Instance.ApplyPlaygroundSimplification(val, Cfg.DisableFoliage.Value);
                },
                valueGetter: () => Cfg.PlaygroundSprites.Value,
                cleanName: "Playground Visual Style",
                cleanChoiceNames: new string[] { "Off (Native Detail)", "Flat Silhouette (Playground)", "Flat Blocks (Extremely Fast)" },
                description: "Absurdly simplifies game sprites and world elements. Flat Silhouette keeps shapes but strips textures into solid average colors. Flat Blocks turns everything into literal colored rectangles."
            );

            // Register Disable Foliage & Decor toggle
            SettingsManager.RegisterBool(
                name: "Perf_Playground_Foliage",
                category: Setting.SettingCategory.Video,
                defaultValue: Cfg.DisableFoliage.Value,
                onApply: (val) => {
                    Cfg.DisableFoliage.Value = val;
                    Log.LogInfo($"Disable Foliage & Decor setting changed: {val}");
                    PlaygroundSimplifier.Instance.ResetSweepCaches();
                    PlaygroundSimplifier.Instance.ApplyPlaygroundSimplification(Cfg.PlaygroundSprites.Value, val);
                },
                valueGetter: () => Cfg.DisableFoliage.Value,
                cleanName: "Disable Foliage & Decor",
                description: "Completely disables small decorative grass, vines, foliage, debris, and blood splatters. Gives the world a clean, minimalist playground style and improves CPU/GPU performance."
            );

            // Register Playground Theme dropdown
            SettingsManager.RegisterDropdown(
                name: "Perf_Playground_Theme",
                category: Setting.SettingCategory.Video,
                choices: new string[] { "None", "Vaporwave", "GameBoy", "Cyberpunk", "Blueprint" },
                defaultValue: Cfg.PlaygroundTheme.Value,
                onApply: (val) => {
                    Cfg.PlaygroundTheme.Value = val;
                    Log.LogInfo($"Playground Theme changed: {val}");
                    PlaygroundSimplifier.Instance.ResetSweepCaches();
                    PlaygroundSimplifier.Instance.ApplyPlaygroundSimplification(Cfg.PlaygroundSprites.Value, Cfg.DisableFoliage.Value);
                },
                valueGetter: () => Cfg.PlaygroundTheme.Value,
                cleanName: "Playground Color Palette",
                cleanChoiceNames: new string[] { "None (Dynamic Colors)", "Neon Vaporwave", "Retro GameBoy", "Cyberpunk Amber", "Monochrome Blueprint" },
                description: "Applies a stylized retro color scheme over simplified playground elements."
            );

            // Register Instant Debris Vaporization toggle
            SettingsManager.RegisterBool(
                name: "Perf_Vaporize_Debris",
                category: Setting.SettingCategory.Video,
                defaultValue: Cfg.VaporizeDebris.Value,
                onApply: (val) => {
                    Cfg.VaporizeDebris.Value = val;
                    Log.LogInfo($"Instant Debris Vaporization setting changed: {val}");
                },
                valueGetter: () => Cfg.VaporizeDebris.Value,
                cleanName: "Instant Debris Vaporization",
                description: "Vaporizes broken glass, vents, and crate debris into lightweight puffs instantly, completely eliminating physics solver simulation overhead."
            );

            // Register Minimalist Audio toggle
            SettingsManager.RegisterBool(
                name: "Perf_Minimal_Audio",
                category: Setting.SettingCategory.Video,
                defaultValue: Cfg.MinimalistAudio.Value,
                onApply: (val) => {
                    Cfg.MinimalistAudio.Value = val;
                    Log.LogInfo($"Minimalist Audio setting changed: {val}");
                    PlaygroundSimplifier.Instance.ApplyMinimalistAudio(val);
                },
                valueGetter: () => Cfg.MinimalistAudio.Value,
                cleanName: "Minimalist Retro Audio",
                description: "Disables CPU-heavy room-acoustic reverb zones, DSP filters (lowpass/echo), and mutes environmental humming loops, keeping direct SFX crisp."
            );

            // Register Line of Sight frame skipper setting (VIDEO category)
            SettingsManager.RegisterDropdown(
                name: "Perf_LOS_Speed",
                category: Setting.SettingCategory.Video,
                choices: new string[] { "Every Frame", "Every 2 Frames", "Every 3 Frames", "Every 4 Frames" },
                defaultValue: Cfg.LosSpeed.Value,
                onApply: (val) => {
                    Cfg.LosSpeed.Value = val;
                    Log.LogInfo($"Line Of Sight refresh rate setting changed: {val}");
                },
                valueGetter: () => Cfg.LosSpeed.Value,
                cleanName: "LOS Refresh Interval",
                cleanChoiceNames: new string[] { "Every Frame (Heavy)", "Every 2 Frames (Fast)", "Every 3 Frames (Ultra)", "Every 4 Frames (Extreme)" },
                description: "Controls how frequently Line Of Sight checks are performed for AI. Higher intervals significantly improve FPS in crowded areas, with minor visual delay."
            );

            // Register Particles & Splatters maximum cap (VIDEO category)
            SettingsManager.RegisterDropdown(
                name: "Perf_Particle_Cap",
                category: Setting.SettingCategory.Video,
                choices: new string[] { "25 Particles", "50 Particles", "100 Particles", "200 Particles", "Unlimited" },
                defaultValue: Cfg.MaxParticles.Value,
                onApply: (val) => {
                    Cfg.MaxParticles.Value = val;
                    Log.LogInfo($"Max Splatters & Particles cap changed: {val}");
                },
                valueGetter: () => Cfg.MaxParticles.Value,
                cleanName: "Splatter Particle Cap",
                cleanChoiceNames: new string[] { "25 (Fastest)", "50 (Highly Optimized)", "100 (Balanced)", "200 (Heavy)", "Unlimited" },
                description: "Limits the maximum active splatter, blood, and spark particles on the screen. Lower limits prevent CPU and GPU bottlenecking during heavy combat."
            );

            // Register Physics iteration quality slider (VIDEO/Performance category)
            SettingsManager.RegisterDropdown(
                name: "Perf_Physics_Quality",
                category: Setting.SettingCategory.Video,
                choices: new string[] { "Low", "Medium", "High" },
                defaultValue: Cfg.PhysicsQuality.Value,
                onApply: (val) => {
                    Cfg.PhysicsQuality.Value = val;
                    ApplyPhysicsQuality(val);
                },
                valueGetter: () => Cfg.PhysicsQuality.Value,
                cleanName: "Physics 2D Solver Quality",
                cleanChoiceNames: new string[] { "Low (Fastest Physics)", "Medium (Optimized)", "High (Native Default)" },
                description: "Adjusts the solver iteration accuracy for 2D physics. Lower iterations reduce CPU overhead from collision solving without affecting normal gameplay."
            );

            // Register GC garbage sweeps optimization (VIDEO/Performance category)
            SettingsManager.RegisterBool(
                name: "Perf_GC_Optimize",
                category: Setting.SettingCategory.Video,
                defaultValue: Cfg.GcOptimize.Value,
                onApply: (val) => {
                    Cfg.GcOptimize.Value = val;
                    ConfigureIncrementalGC();
                },
                valueGetter: () => Cfg.GcOptimize.Value,
                cleanName: "GC Memory Sweeper",
                description: "Optimizes Unity Garbage Collection time slices to keep gameplay completely micro-stutter free, combined with automated memory sweeping."
            );
        }

        public static void ApplyPhysicsQuality(int quality)
        {
            try
            {
                if (quality == 0) // Low (Fastest)
                {
                    UnityEngine.Physics2D.positionIterations = 3;
                    UnityEngine.Physics2D.velocityIterations = 2;
                }
                else if (quality == 1) // Medium (Optimized)
                {
                    UnityEngine.Physics2D.positionIterations = 5;
                    UnityEngine.Physics2D.velocityIterations = 3;
                }
                else // High (Default)
                {
                    UnityEngine.Physics2D.positionIterations = 8;
                    UnityEngine.Physics2D.velocityIterations = 3;
                }
                Log.LogInfo($"Physics2D solver solver quality applied: PositionIterations={UnityEngine.Physics2D.positionIterations}, VelocityIterations={UnityEngine.Physics2D.velocityIterations}");
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to adjust Physics2D iterations: " + ex.Message);
            }
        }

        private void ConfigureIncrementalGC()
        {
            if (Cfg.GcOptimize.Value)
            {
                try
                {
                    // Target ~1.5ms slice for GC to keep game frame deliveries completely stutter-free
                    UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds = 1500000;
                    Log.LogInfo("Incremental GC timeslice configured to 1,500,000ns (1.5ms).");
                }
                catch (Exception ex)
                {
                    Log.LogDebug("Incremental GC property set unsupported or skipped: " + ex.Message);
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Cfg.GcOptimize.Value)
            {
                Log.LogInfo($"Scene Loaded: '{scene.name}'. Sweeping memory allocation leftovers via GC.Collect().");
                TriggerGCSweep();
            }
        }

        public static void TriggerGCSweep()
        {
            try
            {
                System.GC.Collect();
                Log.LogInfo("Explicit GC.Collect execution finished successfully.");
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to force GC.Collect: " + ex.Message);
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _harmony?.UnpatchSelf();
        }
    }
}
