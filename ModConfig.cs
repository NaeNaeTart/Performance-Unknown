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
        public ConfigEntry<int> PlaygroundSprites { get; }
        public ConfigEntry<bool> DisableFoliage { get; }
        public ConfigEntry<int> PlaygroundTheme { get; }
        public ConfigEntry<bool> VaporizeDebris { get; }
        public ConfigEntry<bool> MinimalistAudio { get; }

        public ModConfig(ConfigFile cfg)
        {
            ModEnabled = cfg.Bind("General", "ModEnabled", true, "Enable or disable the Unknown Performance mod.");
            LosSpeed = cfg.Bind("Performance", "LosSpeed", 1, "Line Of Sight Refresh Rate. 0 = Every Frame, 1 = Every 2 Frames, 2 = Every 3 Frames, 3 = Every 4 Frames.");
            MaxParticles = cfg.Bind("Performance", "MaxParticles", 2, "Max active particles/debris cap. 0 = 25, 1 = 50, 2 = 100, 3 = 200, 4 = Unlimited.");
            PhysicsQuality = cfg.Bind("Performance", "PhysicsQuality", 1, "Physics 2D solver iteration quality. 0 = Low (Fastest), 1 = Medium (Balanced), 2 = High (Default).");
            GcOptimize = cfg.Bind("Performance", "GcOptimize", true, "Enable garbage collection stutter-reduction optimization.");
            PlaygroundSprites = cfg.Bind("Playground", "PlaygroundSprites", 0, "Simplify sprites to colored/textureless versions. 0 = Off, 1 = Flat Silhouette, 2 = Flat Block.");
            DisableFoliage = cfg.Bind("Playground", "DisableFoliage", false, "Disable decorative grass, foliage, debris, and splatters.");
            PlaygroundTheme = cfg.Bind("Playground", "PlaygroundTheme", 0, "Styled color palette overlay. 0 = None, 1 = Neon Vaporwave, 2 = Retro GameBoy, 3 = Cyberpunk Amber, 4 = Monochrome Blueprint.");
            VaporizeDebris = cfg.Bind("Playground", "VaporizeDebris", false, "Instantly vaporize breakable physical debris into lightweight fade-out puffs to eliminate physics simulation lag.");
            MinimalistAudio = cfg.Bind("Playground", "MinimalistAudio", false, "Mutes continuous environmental audio loops and disables heavy DSP audio filters/reverb.");
        }
    }
}
