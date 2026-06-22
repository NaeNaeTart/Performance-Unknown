using System;
using UnityEngine;

namespace UnknownPerformance
{
    public class DynamicResolutionManager : MonoBehaviour
    {
        public static DynamicResolutionManager Instance { get; private set; } = null!;

        private float _currentFps = 60f;
        private float _activeScale = 1.0f;
        private bool _wasEnabled = false;

        private static System.Reflection.PropertyInfo? _renderScaleProp = null;
        private static object? _currentPipeline = null;
        private static bool _lookedUpProp = false;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Plugin.Cfg == null || !Plugin.Cfg.ModEnabled.Value)
            {
                RestoreDefault();
                return;
            }

            bool enabled = Plugin.Cfg.DynResEnabled.Value;

            if (!enabled)
            {
                if (_wasEnabled)
                {
                    RestoreDefault();
                    _wasEnabled = false;
                }
                return;
            }

            _wasEnabled = true;

            // 1. Calculate smoothed real-time FPS (lerped to avoid erratic jitter in resolution scaling)
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                float instantFps = 1.0f / dt;
                _currentFps = Mathf.Lerp(_currentFps, instantFps, dt * 2.0f);
            }

            // 2. Obtain Dynamic Resolution range configs
            float minScale = 0.5f;
            int minScaleSel = Plugin.Cfg.DynResMinScale.Value;
            if (minScaleSel == 1) minScale = 0.6f;
            else if (minScaleSel == 2) minScale = 0.7f;
            else if (minScaleSel == 3) minScale = 0.8f;
            else if (minScaleSel == 4) minScale = 0.9f;

            float minFps = 30f;
            int minFpsSel = Plugin.Cfg.DynResMinFps.Value;
            if (minFpsSel == 1) minFps = 40f;
            else if (minFpsSel == 2) minFps = 50f;
            else if (minFpsSel == 3) minFps = 60f;

            float targetFps = 60f;
            int targetFpsSel = Plugin.Cfg.DynResTargetFps.Value;
            if (targetFpsSel == 1) targetFps = 75f;
            else if (targetFpsSel == 2) targetFps = 90f;
            else if (targetFpsSel == 3) targetFps = 120f;
            else if (targetFpsSel == 4) targetFps = 144f;

            // Ensure Target FPS is strictly greater than Min FPS
            if (targetFps <= minFps)
            {
                targetFps = minFps + 10f;
            }

            // 3. Compute target resolution scale
            float targetScale = 1.0f;
            if (_currentFps >= targetFps)
            {
                targetScale = 1.0f;
            }
            else if (_currentFps <= minFps)
            {
                targetScale = minScale;
            }
            else
            {
                float t = (_currentFps - minFps) / (targetFps - minFps);
                targetScale = Mathf.Lerp(minScale, 1.0f, t);
            }

            // 4. Smooth scale transition: fast scale down, slow scale up (standard AAA practice)
            float scaleChangeSpeed = (targetScale < _activeScale) ? 0.4f : 0.08f;
            float newScale = Mathf.MoveTowards(_activeScale, targetScale, dt * scaleChangeSpeed);
            newScale = Mathf.Clamp(newScale, minScale, 1.0f);

            // 5. Apply only when the value has visibly changed
            if (Mathf.Abs(newScale - _activeScale) > 0.002f || !Mathf.Approximately(_activeScale, 1.0f))
            {
                _activeScale = newScale;
                ApplyActiveScale();
            }
        }

        private void SetRenderScale(float scale)
        {
            try
            {
                var currentPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                if (currentPipeline == null) return;

                if (!_lookedUpProp || (object?)_currentPipeline != (object?)currentPipeline)
                {
                    _currentPipeline = currentPipeline;
                    _renderScaleProp = currentPipeline.GetType().GetProperty("renderScale", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    _lookedUpProp = true;
                }

                if (_renderScaleProp != null)
                {
                    _renderScaleProp.SetValue(currentPipeline, scale);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Failed to apply dynamic resolution renderScale: " + ex.Message);
            }
        }

        private void ApplyActiveScale()
        {
            SetRenderScale(_activeScale);
        }

        private void RestoreDefault()
        {
            try
            {
                _activeScale = 1.0f;
                SetRenderScale(1.0f);
                Plugin.Log.LogInfo("Dynamic Resolution restored to default 100%.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Failed to restore dynamic resolution scale: " + ex.Message);
            }
        }
    }
}
