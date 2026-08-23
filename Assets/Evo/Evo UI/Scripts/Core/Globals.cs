using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Manages and creates global references to be re-used accross different elements.
    /// </summary>
    public class Globals
    {
        public static Canvas globalCanvas;

        /// <summary>
        /// Used for dynamic objects where Canvas is required.
        /// </summary>
        public static Canvas GetCanvas()
        {
            if (globalCanvas == null)
            {
                GameObject canvasGO = new("Global Canvas [Generated]");

                Canvas canvas = canvasGO.AddComponent<Canvas>();
                CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();

                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.vertexColorAlwaysGammaSpace = true;
                canvas.sortingOrder = 1000;

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(2560, 1440);
                scaler.matchWidthOrHeight = 0.5f;

                globalCanvas = canvas;
            }

            return globalCanvas;
        }
    }
}