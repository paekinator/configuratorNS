using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Manages shared camera captures for multiple BlurOverlay instances.
    /// Automatically created when needed - no manual setup required.
    /// </summary>
    public class BlurOverlayManager : MonoBehaviour
    {
        static BlurOverlayManager instance;
        public static BlurOverlayManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new("[Evo UI - Blur Manager]");
                    instance = go.AddComponent<BlurOverlayManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        class CameraCapture
        {
            public Camera camera;
            public RenderTexture texture;
            public List<BlurOverlay> subscribers = new();
            public int updateInterval;
            public int frameCounter;
        }

        readonly Dictionary<Camera, CameraCapture> captureMap = new();
        readonly List<CameraCapture> activeCaptures = new();

        void OnDestroy()
        {
            for (int i = 0; i < activeCaptures.Count; i++)
            {
                var capture = activeCaptures[i];
                if (capture.texture != null)
                {
                    capture.texture.Release();
                    Destroy(capture.texture);
                }
            }

            captureMap.Clear();
            activeCaptures.Clear();
        }

        void Update()
        {
            for (int i = 0; i < activeCaptures.Count; i++)
            {
                var capture = activeCaptures[i];

                // Skip if no subscribers or update interval is 0 (manual updates only)
                if (capture.subscribers.Count == 0 || capture.updateInterval == 0)
                    continue;

                capture.frameCounter++;
                if (capture.frameCounter >= capture.updateInterval)
                {
                    capture.frameCounter = 0;
                    CaptureCamera(capture);

                    // Notify all subscribers to re-blur using cached iteration
                    for (int j = 0; j < capture.subscribers.Count; j++)
                    {
                        var overlay = capture.subscribers[j];
                        if (overlay != null && overlay.isActiveAndEnabled)
                        {
                            overlay.OnSharedCaptureUpdated();
                        }
                    }
                }
            }
        }

        public void RegisterOverlay(BlurOverlay overlay, Camera camera, int updateInterval)
        {
            if (camera == null)
                return;

            if (!captureMap.TryGetValue(camera, out CameraCapture capture))
            {
                capture = new CameraCapture
                {
                    camera = camera,
                    updateInterval = updateInterval
                };
                captureMap[camera] = capture;
                activeCaptures.Add(capture); // Add to flat list for Update tracking
            }

            if (!capture.subscribers.Contains(overlay))
                capture.subscribers.Add(overlay);

            // Use the highest update interval (lowest frequency) among all subscribers
            // This ensures we don't update more often than necessary
            capture.updateInterval = Mathf.Max(capture.updateInterval, updateInterval);

            // Initial capture
            EnsureCaptureTexture(capture);
            CaptureCamera(capture);
        }

        public void UnregisterOverlay(BlurOverlay overlay, Camera camera)
        {
            if (camera == null || !captureMap.TryGetValue(camera, out CameraCapture capture))
                return;

            capture.subscribers.Remove(overlay);

            // Cleanup if no more subscribers
            if (capture.subscribers.Count == 0)
            {
                if (capture.texture != null)
                {
                    capture.texture.Release();
                    Destroy(capture.texture);
                }

                captureMap.Remove(camera);
                activeCaptures.Remove(capture);
            }
        }

        public RenderTexture GetCaptureTexture(Camera camera, int baseDownsample)
        {
            if (!captureMap.TryGetValue(camera, out CameraCapture capture))
                return null;

            EnsureCaptureTexture(capture, baseDownsample);
            return capture.texture;
        }

        public void ManualCapture(Camera camera)
        {
            if (!captureMap.TryGetValue(camera, out CameraCapture capture))
                return;

            CaptureCamera(capture);

            // Notify subscribers
            for (int i = 0; i < capture.subscribers.Count; i++)
            {
                var overlay = capture.subscribers[i];
                if (overlay != null && overlay.isActiveAndEnabled) { overlay.OnSharedCaptureUpdated(); }
            }
        }

        void EnsureCaptureTexture(CameraCapture capture, int baseDownsample = 2)
        {
            if (capture.camera == null)
                return;

            int w = Mathf.Max(2, capture.camera.pixelWidth / Mathf.Max(1, baseDownsample));
            int h = Mathf.Max(2, capture.camera.pixelHeight / Mathf.Max(1, baseDownsample));

            if (capture.texture == null || capture.texture.width != w || capture.texture.height != h)
            {
                if (capture.texture != null)
                {
                    capture.texture.Release();
                    Destroy(capture.texture);
                }

                capture.texture = new RenderTexture(w, h, 0, RenderTextureFormat.Default)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false
                };
            }
        }

        void CaptureCamera(CameraCapture capture)
        {
            if (capture.camera == null || capture.texture == null)
                return;

            RenderTexture prevRT = capture.camera.targetTexture;
            capture.camera.targetTexture = capture.texture;
            capture.camera.Render();
            capture.camera.targetTexture = prevRT;
        }
    }
}