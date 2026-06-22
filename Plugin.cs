using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using ScavSetLib;

namespace UnknownPerformance
{
    public static class PluginInfo
    {
        public const string GUID = "com.kanisuko.unknownperformance";
        public const string Name = "Unknown Performance";
        public const string Version = "1.3.1";
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
            ApplyTextureQuality(Cfg.TextureQuality.Value);
            ApplyFpsLimit(Cfg.FpsLimit.Value);
            ApplyVSync(Cfg.VSync.Value);
            ApplyAntiAliasing(Cfg.AntiAliasing.Value);
            ApplyShadowsAndReflections(Cfg.ShadowsEnabled.Value);

            // 4. Hook into Scene Loads for clean garbage sweeps
            SceneManager.sceneLoaded += OnSceneLoaded;

            // 5. Apply Harmony Patches for active performance tuning
            _harmony = new Harmony(PluginInfo.GUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.LogInfo($"[{PluginInfo.Name} v{PluginInfo.Version}] Advanced performance suite loaded successfully!");
        }

        private void RegisterPerformanceOptions()
        {
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

            // Register iGPU Texture Downscaler
            SettingsManager.RegisterDropdown(
                name: "Perf_Texture_Quality",
                category: Setting.SettingCategory.Video,
                choices: new string[] { "Full", "Half", "Quarter", "One-Eighth" },
                defaultValue: Cfg.TextureQuality.Value,
                onApply: (val) => {
                    Cfg.TextureQuality.Value = val;
                    ApplyTextureQuality(val);
                },
                valueGetter: () => Cfg.TextureQuality.Value,
                cleanName: "Texture Quality Downscaler",
                cleanChoiceNames: new string[] { "Full Resolution", "Half Resolution (Fast)", "Quarter Resolution (Faster)", "One-Eighth Resolution (Extreme)" },
                description: "Reduces texture resolution. Lowering this drastically reduces integrated GPU (iGPU) memory bandwidth requirements, yielding massive performance gains on laptop and handheld systems."
            );

            // Register FPS Limiter
            SettingsManager.RegisterDropdown(
                name: "Perf_FPS_Limit",
                category: Setting.SettingCategory.Video,
                choices: new string[] { "30 FPS", "45 FPS", "60 FPS", "Unlimited" },
                defaultValue: Cfg.FpsLimit.Value,
                onApply: (val) => {
                    Cfg.FpsLimit.Value = val;
                    ApplyFpsLimit(val);
                },
                valueGetter: () => Cfg.FpsLimit.Value,
                cleanName: "Frame Rate Limit",
                cleanChoiceNames: new string[] { "30 FPS", "45 FPS", "60 FPS", "Unlimited" },
                description: "Caps the maximum frame rate. Setting a cap prevents your GPU and CPU from drawing unnecessary power, reducing heat and avoiding thermal throttling on mobile/laptop architectures."
            );

            // Register V-Sync Toggle
            SettingsManager.RegisterBool(
                name: "Perf_VSync_Toggle",
                category: Setting.SettingCategory.Video,
                defaultValue: Cfg.VSync.Value,
                onApply: (val) => {
                    Cfg.VSync.Value = val;
                    ApplyVSync(val);
                },
                valueGetter: () => Cfg.VSync.Value,
                cleanName: "Vertical Sync (V-Sync)",
                description: "Synchronizes the game's frame rate with your monitor's refresh rate. Turn OFF to allow custom FPS limits, or ON to prevent screen tearing."
            );

            // Register Anti-Aliasing (MSAA)
            SettingsManager.RegisterDropdown(
                name: "Perf_Anti_Aliasing",
                category: Setting.SettingCategory.Video,
                choices: new string[] { "Disabled", "2x MSAA", "4x MSAA", "8x MSAA" },
                defaultValue: Cfg.AntiAliasing.Value,
                onApply: (val) => {
                    Cfg.AntiAliasing.Value = val;
                    ApplyAntiAliasing(val);
                },
                valueGetter: () => Cfg.AntiAliasing.Value,
                cleanName: "Anti-Aliasing (MSAA)",
                cleanChoiceNames: new string[] { "No Anti-Aliasing (Fastest)", "2x MSAA", "4x MSAA", "8x MSAA" },
                description: "Controls Multi-Sample Anti-Aliasing to smooth jagged edges. Disabling MSAA removes an immense fill-rate bottleneck on low-spec integrated graphics chips."
            );

            // Register Shadows and Real-time reflections toggle
            SettingsManager.RegisterBool(
                name: "Perf_Shadows_Quality",
                category: Setting.SettingCategory.Video,
                defaultValue: Cfg.ShadowsEnabled.Value,
                onApply: (val) => {
                    Cfg.ShadowsEnabled.Value = val;
                    ApplyShadowsAndReflections(val);
                },
                valueGetter: () => Cfg.ShadowsEnabled.Value,
                cleanName: "Shadows & Real-time Reflections",
                description: "Enables or disables standard shadows and real-time reflection probes. Disabling these removes costly lighting and pixel shader calculations, drastically scaling performance on low-end hardware."
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

        public static void ApplyTextureQuality(int val)
        {
            try
            {
                QualitySettings.globalTextureMipmapLimit = val;
                Log.LogInfo($"Applied Texture Quality: globalTextureMipmapLimit={val}");
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to apply Texture Quality: " + ex.Message);
            }
        }

        public static void ApplyFpsLimit(int val)
        {
            try
            {
                int fps = -1;
                if (val == 0) fps = 30;
                else if (val == 1) fps = 45;
                else if (val == 2) fps = 60;
                else fps = -1;

                Application.targetFrameRate = fps;
                Log.LogInfo($"Applied FPS Limit: targetFrameRate={fps}");
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to apply FPS Limit: " + ex.Message);
            }
        }

        public static void ApplyVSync(bool active)
        {
            try
            {
                QualitySettings.vSyncCount = active ? 1 : 0;
                Log.LogInfo($"Applied VSync: vSyncCount={QualitySettings.vSyncCount}");
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to apply VSync: " + ex.Message);
            }
        }

        public static void ApplyAntiAliasing(int val)
        {
            try
            {
                int msaa = 0;
                if (val == 1) msaa = 2;
                else if (val == 2) msaa = 4;
                else if (val == 3) msaa = 8;

                QualitySettings.antiAliasing = msaa;
                Log.LogInfo($"Applied Anti-Aliasing: antiAliasing={msaa}");
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to apply Anti-Aliasing: " + ex.Message);
            }
        }

        public static void ApplyShadowsAndReflections(bool enabled)
        {
            try
            {
                QualitySettings.shadows = enabled ? ShadowQuality.All : ShadowQuality.Disable;
                QualitySettings.realtimeReflectionProbes = enabled;
                Log.LogInfo($"Applied Shadows & Reflections: shadows={QualitySettings.shadows}, realtimeReflectionProbes={QualitySettings.realtimeReflectionProbes}");
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to apply Shadows & Reflections: " + ex.Message);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Reapply iGPU QualitySettings on scene load to override any scene-specific overrides
            ApplyTextureQuality(Cfg.TextureQuality.Value);
            ApplyVSync(Cfg.VSync.Value);
            ApplyAntiAliasing(Cfg.AntiAliasing.Value);
            ApplyShadowsAndReflections(Cfg.ShadowsEnabled.Value);
            ApplyFpsLimit(Cfg.FpsLimit.Value);

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
