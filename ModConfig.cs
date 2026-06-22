using BepInEx.Configuration;

namespace UnknownPerformance
{
    public class ModConfig
    {
        public ConfigEntry<bool> ModEnabled { get; }
        public ConfigEntry<int> LosSpeed { get; }
        public ConfigEntry<int> MaxParticles { get; }
        public ConfigEntry<int> PhysicsQuality { get; }
        public ConfigEntry<bool> GcOptimize { get; }
        public ConfigEntry<int> TextureQuality { get; }
        public ConfigEntry<int> FpsLimit { get; }
        public ConfigEntry<bool> VSync { get; }
        public ConfigEntry<int> AntiAliasing { get; }
        public ConfigEntry<bool> ShadowsEnabled { get; }

        // --- Dynamic Resolution Settings ---
        public ConfigEntry<bool> DynResEnabled { get; }
        public ConfigEntry<int> DynResMinScale { get; }
        public ConfigEntry<int> DynResMinFps { get; }
        public ConfigEntry<int> DynResTargetFps { get; }

        public ModConfig(ConfigFile cfg)
        {
            ModEnabled = cfg.Bind("General", "ModEnabled", true, "Enable or disable the Unknown Performance mod.");
            LosSpeed = cfg.Bind("Performance", "LosSpeed", 1, "Line Of Sight Refresh Rate. 0 = Every Frame, 1 = Every 2 Frames, 2 = Every 3 Frames, 3 = Every 4 Frames.");
            MaxParticles = cfg.Bind("Performance", "MaxParticles", 2, "Max active particles/debris cap. 0 = 25, 1 = 50, 2 = 100, 3 = 200, 4 = Unlimited.");
            PhysicsQuality = cfg.Bind("Performance", "PhysicsQuality", 1, "Physics 2D solver iteration quality. 0 = Low (Fastest), 1 = Medium (Balanced), 2 = High (Default).");
            GcOptimize = cfg.Bind("Performance", "GcOptimize", true, "Enable garbage collection stutter-reduction optimization.");
            TextureQuality = cfg.Bind("Performance", "TextureQuality", 0, "Texture resolution quality. 0 = Full, 1 = Half, 2 = Quarter, 3 = One-Eighth.");
            FpsLimit = cfg.Bind("Performance", "FpsLimit", 60, "Target Frame Rate (30-240, where 240 is Unlimited).");
            VSync = cfg.Bind("Performance", "VSync", true, "Enable or disable V-Sync (Vertical Synchronization).");
            AntiAliasing = cfg.Bind("Performance", "AntiAliasing", 0, "Multi-Sample Anti-Aliasing (MSAA) quality. 0 = No Anti-Aliasing, 1 = 2x MSAA, 2 = 4x MSAA, 3 = 8x MSAA.");
            ShadowsEnabled = cfg.Bind("Performance", "ShadowsEnabled", true, "Enable or disable shadows and real-time reflection probes.");

            DynResEnabled = cfg.Bind("Performance", "DynResEnabled", false, "Enable or disable Dynamic Resolution scaling based on real-time FPS.");
            DynResMinScale = cfg.Bind("Performance", "DynResMinScale", 0, "Minimum resolution scaling percentage. 0 = 50%, 1 = 60%, 2 = 70%, 3 = 80%, 4 = 90%.");
            DynResMinFps = cfg.Bind("Performance", "DynResMinFps", 0, "FPS threshold below which the resolution scales down to its minimum percentage. 0 = 30 FPS, 1 = 40 FPS, 2 = 50 FPS, 3 = 60 FPS.");
            DynResTargetFps = cfg.Bind("Performance", "DynResTargetFps", 0, "Target FPS above which the game runs at 100% resolution. 0 = 60 FPS, 1 = 75 FPS, 2 = 90 FPS, 3 = 120 FPS, 4 = 144 FPS.");
        }
    }
}
