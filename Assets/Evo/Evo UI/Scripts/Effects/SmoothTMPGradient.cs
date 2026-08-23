using UnityEngine;
using TMPro;

namespace Evo.UI
{
    /// <summary>
    /// Applies a horizontal gradient to TextMeshPro text components.
    /// This component handles per-character gradient coloring for smooth text transitions.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    [HelpURL(Constants.HelpUrl + "effects/tmp-gradient")]
    [AddComponentMenu("Evo/UI/Effects/Smooth TMP Gradient")]
    public class SmoothTMPGradient : MonoBehaviour
    {
        TextMeshProUGUI tmpComponent;
        bool needsUpdate = true;
        int lastCharacterCount = -1;
        Color lastTopLeft;
        Color lastTopRight;
        Color[] cachedSteps = new Color[16];
        VertexGradient[] cachedGradients = new VertexGradient[16];

        void Awake() => TryGetComponent(out tmpComponent);

        void OnEnable()
        {
            needsUpdate = true;
            ApplyGradient();
        }

        void LateUpdate()
        {
            if (tmpComponent != null && (tmpComponent.havePropertiesChanged || NeedsGradientUpdate()))
                ApplyGradient();
        }

        bool NeedsGradientUpdate()
        {
            if (tmpComponent == null)
                return false;

            // Check if text length changed
            int currentCharCount = tmpComponent.textInfo.characterCount;
            if (currentCharCount != lastCharacterCount)
            {
                lastCharacterCount = currentCharCount;
                return true;
            }

            // Check if gradient colors changed
            if (!tmpComponent.enableVertexGradient)
                return false;

            if (lastTopLeft != tmpComponent.colorGradient.topLeft || lastTopRight != tmpComponent.colorGradient.topRight)
            {
                lastTopLeft = tmpComponent.colorGradient.topLeft;
                lastTopRight = tmpComponent.colorGradient.topRight;
                return true;
            }

            return needsUpdate;
        }

        void EnsureArrayCapacity(int requiredSize)
        {
            if (cachedSteps.Length < requiredSize)
            {
                // Double the size or use required size to prevent frequent resizing
                int newSize = Mathf.Max(cachedSteps.Length * 2, requiredSize);
                cachedSteps = new Color[newSize];
                cachedGradients = new VertexGradient[newSize];
            }
        }

        void ApplyGradient()
        {
            if (tmpComponent == null)
            {
                TryGetComponent(out tmpComponent);

                if (tmpComponent == null)
                    return;
            }

            if (!tmpComponent.enableVertexGradient)
                return;

            tmpComponent.ForceMeshUpdate();
            TMP_TextInfo textInfo = tmpComponent.textInfo;
            int count = textInfo.characterCount;

            // No characters to process
            if (count == 0)
                return;

            // Ensure our cached arrays are large enough
            int requiredSteps = count + 1;
            EnsureArrayCapacity(requiredSteps);

            // Calculate gradient steps into cached array
            CalculateGradientSteps(
                tmpComponent.colorGradient.topLeft,
                tmpComponent.colorGradient.topRight,
                requiredSteps,
                cachedSteps
            );

            // Create vertex gradients for each character
            for (int i = 0; i < requiredSteps - 1; i++)
            {
                cachedGradients[i] = new VertexGradient(
                    cachedSteps[i],     // topLeft
                    cachedSteps[i + 1], // topRight
                    cachedSteps[i],     // bottomLeft
                    cachedSteps[i + 1]  // bottomRight
                );
            }

            // Apply gradients to each character
            ApplyGradientsToCharacters(textInfo, cachedGradients, count);

            needsUpdate = false;
        }

        void ApplyGradientsToCharacters(TMP_TextInfo textInfo, VertexGradient[] gradients, int characterCount)
        {
            for (int charIndex = 0; charIndex < characterCount; charIndex++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];

                // Skip invisible characters
                if (!charInfo.isVisible)
                    continue;

                int materialIndex = charInfo.materialReferenceIndex;
                Color32[] colors = textInfo.meshInfo[materialIndex].colors32;
                int vertexIndex = charInfo.vertexIndex;

                // Apply gradient to the four vertices of the character
                colors[vertexIndex + 0] = gradients[charIndex].bottomLeft;
                colors[vertexIndex + 1] = gradients[charIndex].topLeft;
                colors[vertexIndex + 2] = gradients[charIndex].bottomRight;
                colors[vertexIndex + 3] = gradients[charIndex].topRight;
            }

            // Update the mesh with new colors
            tmpComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        static void CalculateGradientSteps(Color start, Color end, int steps, Color[] buffer)
        {
            if (steps <= 1)
            {
                buffer[0] = start;
                return;
            }

            // Calculate color component increments
            float rStep = (end.r - start.r) / (steps - 1);
            float gStep = (end.g - start.g) / (steps - 1);
            float bStep = (end.b - start.b) / (steps - 1);
            float aStep = (end.a - start.a) / (steps - 1);

            // Generate interpolated colors directly into the buffer
            for (int i = 0; i < steps; i++)
            {
                buffer[i].r = start.r + (rStep * i);
                buffer[i].g = start.g + (gStep * i);
                buffer[i].b = start.b + (bStep * i);
                buffer[i].a = start.a + (aStep * i);
            }
        }

        /// <summary>
        /// Forces a gradient refresh.
        /// </summary>
        public void RefreshGradient() => needsUpdate = true;
    }
}