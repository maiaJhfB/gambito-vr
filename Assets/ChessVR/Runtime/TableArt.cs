using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChessVR
{
    internal sealed class TableArt : IDisposable
    {
        private readonly Shader shader;
        private readonly List<Object> assets = new();
        private readonly Mesh box;
        private readonly Font font;

        internal TableArt(Shader shader)
        {
            this.shader = shader;
            box = CreateBox();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        internal static Color Tint(uint rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        }

        internal Material Surface(string name, uint rgb, float gloss = 0.25f, float metallic = 0f,
            float emission = 0f)
        {
            Color color = Tint(rgb);
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_Color", color);
            material.SetFloat("_Glossiness", gloss);
            material.SetFloat("_Metallic", metallic);
            material.SetColor("_EmissionColor", color * emission);
            assets.Add(material);
            return material;
        }

        internal static Transform Node(Transform parent, string name, Vector3 position)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = position;
            return node;
        }

        internal MeshRenderer Block(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            MeshRenderer renderer = Shape(parent, name, Vector3.zero, box, material);
            renderer.transform.localPosition = center;
            renderer.transform.localScale = size;
            return renderer;
        }

        internal static MeshRenderer Shape(Transform parent, string name, Vector3 position, Mesh mesh,
            Material material)
        {
            Transform node = Node(parent, name, position);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        internal static RectTransform Canvas(Transform parent, string name, Vector3 position)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            var canvas = node.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = node.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localPosition = position;
            rect.sizeDelta = new Vector2(1.3f, .55f);
            return rect;
        }

        internal Text Label(Transform parent, string name, string value, Vector3 position, float size,
            Color color, TextAnchor anchor = TextAnchor.MiddleCenter, float width = 1.1f)
        {
            const int glyphPixels = 96;
            var node = new GameObject(name, typeof(RectTransform));
            var rect = node.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(anchor == TextAnchor.MiddleLeft ? 0 : anchor == TextAnchor.MiddleRight ? 1 : .5f, .5f);
            float scale = size / glyphPixels;
            rect.sizeDelta = new Vector2(width / scale, glyphPixels * 1.7f);
            rect.localScale = Vector3.one * scale;
            rect.localPosition = position;

            var text = node.AddComponent<Text>();
            text.font = font;
            text.fontSize = glyphPixels;
            text.text = value;
            text.alignment = anchor;
            text.color = color;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        internal Mesh Lathe(string name, params Vector2[] profile)
        {
            const int segments = 32;
            var vertices = new Vector3[segments * profile.Length];
            var triangles = new List<int>(segments * (profile.Length - 1) * 6);
            for (int ring = 0; ring < profile.Length; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment * (Mathf.PI * 2 / segments);
                    float radius = profile[ring].x;
                    int current = ring * segments + segment;
                    vertices[current] = new Vector3(Mathf.Cos(angle) * radius, profile[ring].y,
                        Mathf.Sin(angle) * radius);
                    if (ring == profile.Length - 1)
                        continue;

                    int next = ring * segments + (segment + 1) % segments;
                    if (radius > 0)
                        triangles.AddRange(new[] { current, current + segments, next });
                    if (profile[ring + 1].x > 0)
                        triangles.AddRange(new[] { next, current + segments, next + segments });
                }
            }
            return Mesh(name, vertices, triangles.ToArray());
        }

        internal Mesh Prism(string name, float halfDepth, params Vector2[] outline)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            // The convex outline is counter-clockwise in its local XY plane.
            for (int i = 1; i < outline.Length - 1; i++)
            {
                Face(vertices, triangles, OnPlane(outline[0], halfDepth), OnPlane(outline[i], halfDepth),
                    OnPlane(outline[i + 1], halfDepth));
                Face(vertices, triangles, OnPlane(outline[0], -halfDepth), OnPlane(outline[i + 1], -halfDepth),
                    OnPlane(outline[i], -halfDepth));
            }
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                Quad(vertices, triangles, OnPlane(a, halfDepth), OnPlane(a, -halfDepth),
                    OnPlane(b, -halfDepth), OnPlane(b, halfDepth));
            }
            return Mesh(name, vertices.ToArray(), triangles.ToArray());
        }

        private static Vector3 OnPlane(Vector2 point, float z) => new(point.x, point.y, z);

        private Mesh CreateBox()
        {
            var vertices = new List<Vector3>(24);
            var triangles = new List<int>(36);
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 normal in normals)
            {
                Vector3 axis = Mathf.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up;
                Vector3 up = axis * 0.5f;
                Vector3 right = Vector3.Cross(up, normal);
                Vector3 center = normal * 0.5f;
                Quad(vertices, triangles, center - right - up, center + right - up,
                    center + right + up, center - right + up);
            }
            return Mesh("Caixa unitária", vertices.ToArray(), triangles.ToArray());
        }

        private Mesh Mesh(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            assets.Add(mesh);
            return mesh;
        }

        private static void Face(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }

        private static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c,
            Vector3 d)
        {
            int first = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
        }

        public void Dispose()
        {
            foreach (Object asset in assets)
                Object.Destroy(asset);
            assets.Clear();
        }
    }
}
