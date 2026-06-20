using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace UnknownPerformance
{
    [HarmonyPatch]
    public static class Patches
    {
        // --- 1. Line Of Sight Frame Skipper ---
        private static int _losFrameCount = 0;

        [HarmonyPatch(typeof(PlayerCamera), "HandleLineOfSight")]
        [HarmonyPrefix]
        public static bool HandleLineOfSight_Prefix()
        {
            int interval = 1;
            int setting = Plugin.Cfg.LosSpeed.Value;
            if (setting == 1) interval = 2;
            else if (setting == 2) interval = 3;
            else if (setting == 3) interval = 4;

            if (interval <= 1) return true; // Run every frame natively

            _losFrameCount++;
            if (_losFrameCount >= interval)
            {
                _losFrameCount = 0;
                return true; // Run original method
            }
            return false; // Skip this frame!
        }

        // --- 2. Particle & Splatter Maximum Capper ---
        private static readonly List<GameObject> _trackedParticles = new List<GameObject>();

        private static int GetParticleCapLimit()
        {
            int setting = Plugin.Cfg.MaxParticles.Value;
            if (setting == 0) return 25;
            if (setting == 1) return 50;
            if (setting == 2) return 100;
            if (setting == 3) return 200;
            return 999999; // Unlimited
        }

        private static bool IsParticleResource(string id)
        {
            if (id == null) return false;
            return id.Contains("Particle") || id.Contains("Blood") || id.Contains("Explosion") || 
                   id.Contains("Splatter") || id.Contains("Debris") || id.Contains("Dust") || 
                   id.Contains("Smoke") || id.Contains("Foliage") || id.Contains("Grass");
        }

        private static void EnforceParticleCap()
        {
            int maxCap = GetParticleCapLimit();
            if (maxCap >= 999999) return;

            // Remove any null (already destroyed by Unity) elements
            _trackedParticles.RemoveAll(item => item == null);

            while (_trackedParticles.Count >= maxCap)
            {
                var oldest = _trackedParticles[0];
                if (oldest != null)
                {
                    try
                    {
                        UnityEngine.Object.Destroy(oldest);
                    }
                    catch { }
                }
                _trackedParticles.RemoveAt(0);
            }
        }

        [HarmonyPatch(typeof(Utils), "Create", new[] { typeof(string), typeof(Vector2), typeof(float) })]
        [HarmonyPrefix]
        public static void Create_Prefix1(string id)
        {
            if (IsParticleResource(id))
            {
                EnforceParticleCap();
            }
        }

        [HarmonyPatch(typeof(Utils), "Create", new[] { typeof(string), typeof(Vector2), typeof(float) })]
        [HarmonyPostfix]
        public static void Create_Postfix1(string id, GameObject __result)
        {
            if (__result != null && IsParticleResource(id))
            {
                _trackedParticles.Add(__result);
            }
        }

        [HarmonyPatch(typeof(Utils), "Create", new[] { typeof(string), typeof(Transform) })]
        [HarmonyPrefix]
        public static void Create_Prefix2(string id)
        {
            if (IsParticleResource(id))
            {
                EnforceParticleCap();
            }
        }

        [HarmonyPatch(typeof(Utils), "Create", new[] { typeof(string), typeof(Transform) })]
        [HarmonyPostfix]
        public static void Create_Postfix2(string id, GameObject __result)
        {
            if (__result != null && IsParticleResource(id))
            {
                _trackedParticles.Add(__result);
            }
        }

        // --- 3. Pause Screen Garbage Sweeper ---
        [HarmonyPatch(typeof(PauseHandler), "TogglePause")]
        [HarmonyPostfix]
        public static void TogglePause_Postfix()
        {
            if (PauseHandler.paused && Plugin.Cfg.GcOptimize.Value)
            {
                Plugin.TriggerGCSweep();
            }
        }
    }
}
