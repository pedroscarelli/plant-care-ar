using System.Collections.Generic;
using UnityEngine;

namespace CuidAR.Watering
{
    /// <summary>
    /// Small helpers to build the 3D content in code, so the project needs no model files.
    ///
    /// Two things matter on the phone build:
    /// - Materials are copies of Resources/CuidAR/CuidARLit.mat. Referencing a real material asset is what makes
    ///   Unity include the URP Lit shader in the build (without it everything renders magenta).
    /// - Meshes are generated here instead of using GameObject.CreatePrimitive, which also adds physics colliders
    ///   that are stripped from builds that don't use physics ("class 'CapsuleCollider' doesn't exist").
    ///   They have the same sizes as Unity's primitives (cylinder: radius 0.5, height 2; sphere: radius 0.5; cube: 1).
    /// </summary>
    static class ArtKit
    {
        static Material s_Base;
        static readonly Dictionary<PrimitiveType, Mesh> s_Meshes = new();

        static Material BaseMaterial
        {
            get
            {
                if (s_Base == null)
                {
                    s_Base = Resources.Load<Material>("CuidAR/CuidARLit");
                    if (s_Base == null)
                    {
                        Debug.LogError("[CuidAR] Resources/CuidAR/CuidARLit.mat not found; falling back to Shader.Find.");
                        s_Base = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    }
                }
                return s_Base;
            }
        }

        public static Mesh SphereMesh => MeshFor(PrimitiveType.Sphere);

        public static Material Lit(Color color, float smoothness = 0.3f)
        {
            var material = new Material(BaseMaterial) { name = "CuidAR " + ColorUtility.ToHtmlStringRGB(color) };
            SetColor(material, color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        public static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        public static Transform Node(string name, Transform parent, Vector3 localPosition = default)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = localPosition;
            return node;
        }

        public static Transform Shape(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = MeshFor(type);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = localScale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        // ---------- procedural meshes ----------

        static Mesh MeshFor(PrimitiveType type)
        {
            if (s_Meshes.TryGetValue(type, out var mesh) && mesh != null)
                return mesh;
            mesh = type switch
            {
                PrimitiveType.Cylinder => Cylinder(32),
                PrimitiveType.Cube => Cube(),
                _ => Sphere(24, 16),
            };
            s_Meshes[type] = mesh;
            return mesh;
        }

        static Mesh Sphere(int longitude, int latitude)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int lat = 0; lat <= latitude; lat++)
            {
                float theta = Mathf.PI * lat / latitude;
                for (int lon = 0; lon <= longitude; lon++)
                {
                    float phi = 2f * Mathf.PI * lon / longitude;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    normals.Add(n);
                    vertices.Add(n * 0.5f);
                }
            }
            int row = longitude + 1;
            for (int lat = 0; lat < latitude; lat++)
            {
                for (int lon = 0; lon < longitude; lon++)
                {
                    int a = lat * row + lon, b = a + row;
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            }
            return Build("CuidAR Sphere", vertices, normals, triangles);
        }

        static Mesh Cylinder(int segments)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            // Side.
            for (int i = 0; i <= segments; i++)
            {
                float a = 2f * Mathf.PI * i / segments;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(n * 0.5f + Vector3.down);
                vertices.Add(n * 0.5f + Vector3.up);
                normals.Add(n);
                normals.Add(n);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
            // Caps.
            foreach (float y in new[] { 1f, -1f })
            {
                int center = vertices.Count;
                var normal = new Vector3(0f, y, 0f);
                vertices.Add(normal);
                normals.Add(normal);
                for (int i = 0; i <= segments; i++)
                {
                    float a = 2f * Mathf.PI * i / segments;
                    vertices.Add(new Vector3(Mathf.Cos(a) * 0.5f, y, Mathf.Sin(a) * 0.5f));
                    normals.Add(normal);
                }
                for (int i = 0; i < segments; i++)
                {
                    int v = center + 1 + i;
                    if (y > 0f)
                        triangles.AddRange(new[] { center, v + 1, v });
                    else
                        triangles.AddRange(new[] { center, v, v + 1 });
                }
            }
            return Build("CuidAR Cylinder", vertices, normals, triangles);
        }

        static Mesh Cube()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            var faces = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            foreach (var n in faces)
            {
                var u = Vector3.Cross(n, Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up);
                var v = Vector3.Cross(u, n);
                int start = vertices.Count;
                foreach (var corner in new[] { -u - v, u - v, u + v, -u + v })
                {
                    vertices.Add((n + corner) * 0.5f);
                    normals.Add(n);
                }
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            return Build("CuidAR Cube", vertices, normals, triangles);
        }

        static Mesh Build(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
