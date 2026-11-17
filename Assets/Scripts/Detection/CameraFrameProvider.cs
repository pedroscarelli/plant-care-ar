using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace CuidAR.Detection
{
    /// <summary>
    /// Copies the camera image, exactly as it appears on screen, into the square texture the model reads.
    ///
    /// 1. The AR background material (the same one that draws the camera on screen) is blitted into a
    ///    texture with the screen's aspect ratio, so rotation and cropping already match what the user sees.
    /// 2. The centered square of that image is copied into the model input (inputSize x inputSize).
    ///    Only that square is analyzed; the UI draws it as the scan frame.
    /// </summary>
    public sealed class CameraFrameProvider : IDisposable
    {
        static readonly int k_Crop = Shader.PropertyToID("_Crop");
        static readonly int k_ToSRGB = Shader.PropertyToID("_ToSRGB");
        static readonly int k_FlipY = Shader.PropertyToID("_FlipY");

        readonly ARCameraBackground m_Background;
        readonly int m_InputSize;
        readonly Material m_CropMaterial;
        RenderTexture m_ScreenTexture;

        /// <summary>Square texture fed to the model.</summary>
        public RenderTexture ModelInput { get; }

        /// <summary>Part of the screen that was analyzed (normalized, origin at the top-left).</summary>
        public Rect CropRegion { get; private set; }

        /// <summary>
        /// The AR camera shaders output linear colors in a Linear color-space project, but the model was
        /// trained on normal (sRGB) photos, so the capture is converted back to sRGB.
        /// </summary>
        public bool convertToSRGB = QualitySettings.activeColorSpace == ColorSpace.Linear;

        /// <summary>Flips the model input upside down, for graphics APIs where the blit comes out inverted.</summary>
        public bool flipVertical;

        public CameraFrameProvider(ARCameraBackground background, int inputSize)
        {
            m_Background = background;
            m_InputSize = inputSize;

            var shader = Resources.Load<Shader>("Shaders/CropToModelInput");
            if (shader == null)
                throw new InvalidOperationException("Shader Resources/Shaders/CropToModelInput not found.");
            m_CropMaterial = new Material(shader);

            // Linear (non-sRGB) textures store the values as written, with no automatic conversion.
            ModelInput = new RenderTexture(inputSize, inputSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            ModelInput.Create();
        }

        /// <summary>Centered square of the screen, normalized. Same on top-left or bottom-left origin.</summary>
        public static Rect ComputeCropRegion(int screenWidth, int screenHeight)
        {
            float aspect = (float)screenWidth / screenHeight;
            return aspect < 1f
                ? new Rect(0f, (1f - aspect) / 2f, 1f, aspect)              // portrait
                : new Rect((1f - 1f / aspect) / 2f, 0f, 1f / aspect, 1f);   // landscape
        }

        /// <summary>Returns false while there is no camera image yet.</summary>
        public bool TryCapture()
        {
            var material = m_Background != null ? m_Background.material : null;
            if (material == null)
                return false;

            int screenW = Screen.width, screenH = Screen.height;
            int width, height;
            if (screenW < screenH)
            {
                width = m_InputSize;
                height = Mathf.RoundToInt(m_InputSize * (float)screenH / screenW);
            }
            else
            {
                height = m_InputSize;
                width = Mathf.RoundToInt(m_InputSize * (float)screenW / screenH);
            }
            EnsureScreenTexture(width, height);

            // 1. Camera image as shown on screen.
            Graphics.Blit(null, m_ScreenTexture, material);

            // 2. Centered square -> model input.
            CropRegion = ComputeCropRegion(screenW, screenH);
            var crop = CropRegion;
            m_CropMaterial.SetVector(k_Crop, new Vector4(crop.width, crop.height, crop.x, crop.y));
            m_CropMaterial.SetFloat(k_ToSRGB, convertToSRGB ? 1f : 0f);
            m_CropMaterial.SetFloat(k_FlipY, flipVertical ? 1f : 0f);
            Graphics.Blit(m_ScreenTexture, ModelInput, m_CropMaterial);
            return true;
        }

        void EnsureScreenTexture(int width, int height)
        {
            if (m_ScreenTexture != null && m_ScreenTexture.width == width && m_ScreenTexture.height == height)
                return;
            if (m_ScreenTexture != null)
                m_ScreenTexture.Release();
            m_ScreenTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            m_ScreenTexture.Create();
        }

        public void Dispose()
        {
            if (m_ScreenTexture != null)
                m_ScreenTexture.Release();
            ModelInput.Release();
            UnityEngine.Object.Destroy(m_CropMaterial);
        }
    }
}
