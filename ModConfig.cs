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

        public ModConfig(ConfigFile cfg)
        {
            ModEnabled = cfg.Bind("General", "ModEnabled", true, "Enable or disable the Unknown Performance mod.");
            LosSpeed = cfg.Bind("Performance", "LosSpeed", 1, "Line Of Sight Refresh Rate. 0 = Every Frame, 1 = Every 2 Frames, 2 = Every 3 Frames, 3 = Every 4 Frames.");
            MaxParticles = cfg.Bind("Performance", "MaxParticles", 2, "Max active particles/debris cap. 0 = 25, 1 = 50, 2 = 100, 3 = 200, 4 = Unlimited.");
            PhysicsQuality = cfg.Bind("Performance", "PhysicsQuality", 1, "Physics 2D solver iteration quality. 0 = Low (Fastest), 1 = Medium (Balanced), 2 = High (Default).");
            GcOptimize = cfg.Bind("Performance", "GcOptimize", true, "Enable garbage collection stutter-reduction optimization.");
            TextureQuality = cfg.Bind("Performance", "TextureQuality", 0, "Texture resolution quality. 0 = Full, 1 = Half, 2 = Quarter, 3 = One-Eighth.");
            FpsLimit = cfg.Bind("Performance", "FpsLimit", 3, "Target Frame Rate. 0 = 30 FPS, 1 = 45 FPS, 2 = 60 FPS, 3 = Unlimited.");
            VSync = cfg.Bind("Performance", "VSync", true, "Enable or disable V-Sync (Vertical Synchronization).");
            AntiAliasing = cfg.Bind("Performance", "AntiAliasing", 0, "Multi-Sample Anti-Aliasing (MSAA) quality. 0 = No Anti-Aliasing, 1 = 2x MSAA, 2 = 4x MSAA, 3 = 8x MSAA.");
            ShadowsEnabled = cfg.Bind("Performance", "ShadowsEnabled", true, "Enable or disable shadows and real-time reflection probes.");
        }
    }
}
