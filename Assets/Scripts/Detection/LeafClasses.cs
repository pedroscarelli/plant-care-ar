using System.Collections.Generic;
using UnityEngine;

namespace CuidAR.Detection
{
    /// <summary>
    /// Display name and color for each class the model knows.
    /// The class order itself comes from the labels file exported with the model
    /// (Resources/Models/cuidar_labels.txt), so retraining with new classes needs no code change.
    /// </summary>
    public static class LeafClasses
    {
        public const string Healthy = "folha_saudavel";

        static readonly Dictionary<string, (string name, Color color)> k_Info = new()
        {
            { "folha_saudavel",  ("Healthy",            new Color32(30, 140, 70, 255)) },
            { "folha_amarelada", ("Yellowing leaf",     new Color32(215, 120, 20, 255)) },
            { "mancha_fungica",  ("Leaf spot (fungus)", new Color32(200, 40, 40, 255)) },
            { "oidio",           ("Powdery mildew",     new Color32(42, 100, 180, 255)) },
            { "praga",           ("Pest",               new Color32(139, 90, 43, 255)) },
        };

        public static string DisplayName(string label) =>
            k_Info.TryGetValue(label, out var info) ? info.name : label;

        public static Color ColorOf(string label) =>
            k_Info.TryGetValue(label, out var info) ? info.color : Color.gray;

        public static bool IsProblem(string label) => label != Healthy;
    }
}
