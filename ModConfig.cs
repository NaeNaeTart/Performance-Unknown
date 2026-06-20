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

        public ModConfig(ConfigFile cfg)
        {
            ModEnabled = cfg.Bind("General", "ModEnabled", true, "Enable or disable the Unknown Performance mod.");
            LosSpeed = cfg.Bind("Performance", "LosSpeed", 1, "Line Of Sight Refresh Rate. 0 = Every Frame, 1 = Every 2 Frames, 2 = Every 3 Frames, 3 = Every 4 Frames.");
            MaxParticles = cfg.Bind("Performance", "MaxParticles", 2, "Max active particles/debris cap. 0 = 25, 1 = 50, 2 = 100, 3 = 200, 4 = Unlimited.");
            PhysicsQuality = cfg.Bind("Performance", "PhysicsQuality", 1, "Physics 2D solver iteration quality. 0 = Low (Fastest), 1 = Medium (Balanced), 2 = High (Default).");
            GcOptimize = cfg.Bind("Performance", "GcOptimize", true, "Enable garbage collection stutter-reduction optimization.");
        }
    }
}
