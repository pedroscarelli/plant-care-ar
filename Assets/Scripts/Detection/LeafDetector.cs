using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;

namespace CuidAR.Detection
{
    /// <summary>
    /// Runs the YOLOv8 model (ONNX, imported by the Unity Inference Engine) on one image
    /// and returns the leaves it found.
    /// </summary>
    public sealed class LeafDetector : IDisposable
    {
        readonly Worker m_Worker;
        readonly Tensor<float> m_Input;
        readonly string[] m_Labels;
        readonly int m_InputSize;
        readonly List<LeafDetection> m_Results = new();
        bool m_Disposed;

        public float confidenceThreshold = 0.5f;
        public float iouThreshold = 0.45f;
        public int maxDetections = 20;

        public IReadOnlyList<string> Labels => m_Labels;

        public LeafDetector(ModelAsset modelAsset, string[] labels, int inputSize)
        {
            m_Labels = labels;
            m_InputSize = inputSize;

            var model = ModelLoader.Load(modelAsset);
            // GPU when the device supports compute shaders (Metal on iPhone, Vulkan/GLES 3.1 on most Androids).
            var backend = SystemInfo.supportsComputeShaders ? BackendType.GPUCompute : BackendType.CPU;
            m_Worker = new Worker(model, backend);

            // NCHW: 1 image, 3 channels (RGB), height, width. Values 0..1, as in training.
            m_Input = new Tensor<float>(new TensorShape(1, 3, inputSize, inputSize));
        }

        /// <summary>
        /// Boxes are normalized to the input image (origin at the top-left).
        /// The returned list is reused: copy what you need before calling again.
        /// </summary>
        public async Awaitable<List<LeafDetection>> DetectAsync(Texture image)
        {
            m_Results.Clear();
            if (m_Disposed)
                return m_Results;

            // Default transform: NCHW layout, row 0 = top of the image.
            TextureConverter.ToTensor(image, m_Input, new TextureTransform());
            m_Worker.Schedule(m_Input);

            var output = m_Worker.PeekOutput() as Tensor<float>;
            // Copy the result to the CPU without blocking the frame.
            using var cpuOutput = await output.ReadbackAndCloneAsync();
            if (m_Disposed)
                return m_Results;

            Decode(cpuOutput);
            return m_Results;
        }

        void Decode(Tensor<float> output)
        {
            int numClasses = output.shape[1] - 4;
            int numAnchors = output.shape[2];
            if (numClasses != m_Labels.Length)
                Debug.LogWarning($"[CuidAR] Model has {numClasses} classes but the labels file lists {m_Labels.Length}.");

            YoloDecoder.Decode(output.AsReadOnlySpan(), numClasses, numAnchors, m_InputSize,
                confidenceThreshold, iouThreshold, maxDetections, m_Results);

            for (int i = 0; i < m_Results.Count; i++)
            {
                var detection = m_Results[i];
                detection.label = detection.classId < m_Labels.Length ? m_Labels[detection.classId] : $"class_{detection.classId}";
                m_Results[i] = detection;
            }
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;
            m_Disposed = true;
            m_Input.Dispose();
            m_Worker.Dispose();
        }
    }
}
