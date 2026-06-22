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
        public const string Version = "1.4.1";
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

            // Migration check for FPS Limit from old dropdown index-based configuration
            if (Cfg.FpsLimit.Value < 30)
            {
                int old = Cfg.FpsLimit.Value;
                Cfg.FpsLimit.Value = (old == 0) ? 30 : (old == 1) ? 45 : (old == 2) ? 60 : 240;
                try
                {
                    Config.Save();
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to save migrated configuration file: " + ex.Message);
                }
            }

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

            // 6. Create DynamicResolutionManager GameObject
            GameObject dynResObj = new GameObject("DynamicResolutionManager");
            dynResObj.AddComponent<DynamicResolutionManager>();

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
            SettingsManager.RegisterInt(
                name: "Perf_FPS_Limit",
                category: Setting.SettingCategory.Video,
                min: 30,
                max: 240,
                defaultValue: Cfg.FpsLimit.Value,
                onApply: (val) => {
                    Cfg.FpsLimit.Value = val;
                    ApplyFpsLimit(val);
                },
                valueGetter: () => Cfg.FpsLimit.Value,
                cleanName: "Frame Rate Limit",
                description: "Caps the maximum frame rate. Slide between 30 and 240 FPS (240 FPS sets it to Unlimited)."
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

            // Register Dynamic Resolution Toggle
            SettingsManager.RegisterBool(
                name: "Perf_DynRes_Enabled",
                category: Setting.SettingCategory.Video,
                defaultValue: Cfg.DynResEnabled.Value,
                onApply: (val) => {
                    Cfg.DynResEnabled.Value = val;
                    Log.LogInfo($"Dynamic Resolution enabled changed: {val}");
                },
                valueGetter: () => Cfg.DynResEnabled.Value,
                cleanName: "Dynamic Resolution",
                description: "Enables or disables dynamic scaling of the rendering resolution to maintain a stable, target frame rate in heavy areas."
            );

            // Register Dynamic Resolution Min Scale
            SettingsManager.RegisterDropdown(
                name: "Perf_DynRes_MinScale",
                category: Setting.SettingCategory.Video,
                choices: new string[] { "50%", "60%", "70%", "80%", "90%" },
                defaultValue: Cfg.DynResMinScale.Value,
                onApply: (val) => {
                    Cfg.DynResMinScale.Value = val;
                    Log.LogInfo($"Dynamic Resolution Min Scale changed: {val}");
                },
                valueGetter: () => Cfg.DynResMinScale.Value,
                cleanName: "DynRes Min Scale",
                cleanChoiceNames: new string[] { "50% (Max Performance)", "60%", "70% (Balanced)", "80%", "90% (Max Quality)" },
                description: "The lowest scale factor that dynamic resolution can scale down to. Lower scales offer immense performance increases at the cost of a slightly softer image."
            );

            // Register Dynamic Resolution Min FPS Threshold
            SettingsManager.RegisterDropdown(
                name: "Perf_DynRes_MinFps",
                category: Setting.SettingCategory.Video,
                choices: new string[] { "30 FPS", "40 FPS", "50 FPS", "60 FPS" },
                defaultValue: Cfg.DynResMinFps.Value,
                onApply: (val) => {
                    Cfg.DynResMinFps.Value = val;
                    Log.LogInfo($"Dynamic Resolution Min FPS threshold changed: {val}");
                },
                valueGetter: () => Cfg.DynResMinFps.Value,
                cleanName: "DynRes Min FPS Threshold",
                cleanChoiceNames: new string[] { "30 FPS (Heavy Struggle)", "40 FPS", "50 FPS", "60 FPS (Balanced)" },
                description: "The FPS threshold below which the resolution scaling hits its configured lowest limit to rescue performance."
            );

            // Register Dynamic Resolution Target FPS
            SettingsManager.RegisterDropdown(
                name: "Perf_DynRes_TargetFps",
                category: Setting.SettingCategory.Video,
                choices: new string[] { "60 FPS", "75 FPS", "90 FPS", "120 FPS", "144 FPS" },
                defaultValue: Cfg.DynResTargetFps.Value,
                onApply: (val) => {
                    Cfg.DynResTargetFps.Value = val;
                    Log.LogInfo($"Dynamic Resolution Target FPS changed: {val}");
                },
                valueGetter: () => Cfg.DynResTargetFps.Value,
                cleanName: "DynRes Target FPS",
                cleanChoiceNames: new string[] { "60 FPS (Stable standard)", "75 FPS", "90 FPS (High refresh)", "120 FPS", "144 FPS (Enthusiast)" },
                description: "The frame rate target at or above which the resolution is kept at pristine 100% scale. Resolution scales down dynamically as frame rates fall below this target."
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
                int fps = (val >= 240) ? -1 : val;

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
