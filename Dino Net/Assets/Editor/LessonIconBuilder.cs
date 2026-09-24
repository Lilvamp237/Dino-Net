using System.IO;
using UnityEditor;
using UnityEngine;

namespace DinoNetEditor
{
    /// <summary>
    /// Draws the lesson icon set as sprite assets. The project's TextMesh Pro font has no emoji
    /// fallback, so a padlock written as a character would render as an empty box in the headset.
    /// These are plain white shapes instead, tinted by whatever uses them.
    /// </summary>
    public static class LessonIconBuilder
    {
        const string k_Folder = "Assets/UI/Icons/";
        const int k_Size = 128;

        /// <summary>Shape test in a normalised 0..1 square, y pointing up.</summary>
        delegate bool Shape(Vector2 p);

        [MenuItem("DinoNet/Rebuild Lesson Icons")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(k_Folder);

            Write("Icon_Lock", Lock());
            Write("Icon_Warn", Warn());
            Write("Icon_Get", Tray(true));
            Write("Icon_Post", Tray(false));
            Write("Icon_Key", Key());
            Write("Icon_Shield", Shield());
            Write("Icon_Bulb", Bulb());
            Write("Icon_Dino", Dino());
            Write("Icon_Server", Server());
            Write("Icon_Node", Node());
            Write("Icon_Arrow", Arrow());
            Write("Icon_Packet", Packet());

            AssetDatabase.Refresh();
            Debug.Log("[DinoNet] Lesson icons rebuilt into " + k_Folder);
        }

        /// <summary>Loads an icon, building the whole set first if it isn't there yet.</summary>
        public static Sprite Load(string iconName)
        {
            var path = k_Folder + iconName + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                BuildAll();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            return sprite;
        }

        // ------------------------------------------------------------------ shapes

        static Shape Circle(Vector2 c, float r) => p => (p - c).sqrMagnitude <= r * r;

        static Shape Ring(Vector2 c, float outer, float inner) => p =>
        {
            var d = (p - c).sqrMagnitude;
            return d <= outer * outer && d >= inner * inner;
        };

        static Shape Rect(Vector2 c, Vector2 half) => p =>
            Mathf.Abs(p.x - c.x) <= half.x && Mathf.Abs(p.y - c.y) <= half.y;

        static Shape RoundRect(Vector2 c, Vector2 half, float radius)
        {
            var inner = new Vector2(Mathf.Max(0f, half.x - radius), Mathf.Max(0f, half.y - radius));
            return p =>
            {
                var d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y));
                if (d.x > half.x || d.y > half.y)
                    return false;

                var q = new Vector2(Mathf.Max(0f, d.x - inner.x), Mathf.Max(0f, d.y - inner.y));
                return q.sqrMagnitude <= radius * radius;
            };
        }

        static Shape Tri(Vector2 a, Vector2 b, Vector2 c) => p =>
        {
            var d1 = Cross(b - a, p - a);
            var d2 = Cross(c - b, p - b);
            var d3 = Cross(a - c, p - c);
            return (d1 >= 0f && d2 >= 0f && d3 >= 0f) || (d1 <= 0f && d2 <= 0f && d3 <= 0f);
        };

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static Shape Or(params Shape[] shapes) => p =>
        {
            foreach (var s in shapes)
            {
                if (s(p))
                    return true;
            }

            return false;
        };

        static Shape Minus(Shape shape, params Shape[] holes) => p =>
        {
            if (!shape(p))
                return false;

            foreach (var h in holes)
            {
                if (h(p))
                    return false;
            }

            return true;
        };

        static Shape Rotate(Shape shape, Vector2 pivot, float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            return p =>
            {
                var d = p - pivot;
                return shape(pivot + new Vector2(d.x * cos + d.y * sin, -d.x * sin + d.y * cos));
            };
        }

        // ------------------------------------------------------------------ icons

        static Shape Lock()
        {
            var body = RoundRect(new Vector2(0.5f, 0.33f), new Vector2(0.30f, 0.23f), 0.07f);
            var keyhole = Or(Circle(new Vector2(0.5f, 0.36f), 0.058f),
                             Rect(new Vector2(0.5f, 0.27f), new Vector2(0.025f, 0.06f)));

            // Only the top half of the ring, so the shackle sits on top of the body.
            var shackle = Minus(Ring(new Vector2(0.5f, 0.60f), 0.195f, 0.125f),
                                Rect(new Vector2(0.5f, 0.30f), new Vector2(0.5f, 0.30f)));
            return Or(Minus(body, keyhole), shackle);
        }

        static Shape Warn()
        {
            var triangle = Tri(new Vector2(0.5f, 0.94f), new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.12f));
            var bang = Or(Rect(new Vector2(0.5f, 0.50f), new Vector2(0.055f, 0.17f)),
                          Circle(new Vector2(0.5f, 0.26f), 0.065f));
            return Minus(triangle, bang);
        }

        static Shape Tray(bool down)
        {
            var tray = Or(Rect(new Vector2(0.5f, 0.14f), new Vector2(0.36f, 0.06f)),
                          Rect(new Vector2(0.18f, 0.24f), new Vector2(0.06f, 0.16f)),
                          Rect(new Vector2(0.82f, 0.24f), new Vector2(0.06f, 0.16f)));

            if (down)
            {
                // Arriving: the arrow points down into the tray. "Give me information."
                return Or(tray,
                    Rect(new Vector2(0.5f, 0.74f), new Vector2(0.075f, 0.18f)),
                    Tri(new Vector2(0.5f, 0.38f), new Vector2(0.27f, 0.62f), new Vector2(0.73f, 0.62f)));
            }

            // Leaving: the arrow points up out of the tray. "Here is some information."
            return Or(tray,
                Rect(new Vector2(0.5f, 0.56f), new Vector2(0.075f, 0.18f)),
                Tri(new Vector2(0.5f, 0.94f), new Vector2(0.27f, 0.70f), new Vector2(0.73f, 0.70f)));
        }

        static Shape Key()
        {
            return Or(Ring(new Vector2(0.27f, 0.5f), 0.21f, 0.095f),
                      Rect(new Vector2(0.62f, 0.5f), new Vector2(0.23f, 0.055f)),
                      Rect(new Vector2(0.71f, 0.39f), new Vector2(0.045f, 0.075f)),
                      Rect(new Vector2(0.83f, 0.39f), new Vector2(0.045f, 0.075f)));
        }

        static Shape Shield()
        {
            // Described by its half-width at each height rather than assembled from a box and a
            // triangle: straight sides down to the waist, then a curved taper to the point, with
            // the top corners rounded off. A straight taper reads as a map pin instead.
            const float top = 0.93f;
            const float bottom = 0.06f;
            const float waist = 0.52f;
            const float halfWidth = 0.34f;
            const float corner = 0.10f;

            return p =>
            {
                if (p.y < bottom || p.y > top)
                    return false;

                float half;
                if (p.y >= waist)
                {
                    half = halfWidth;

                    // Round the two top corners.
                    var intoTop = p.y - (top - corner);
                    if (intoTop > 0f)
                        half -= corner - Mathf.Sqrt(Mathf.Max(0f, corner * corner - intoTop * intoTop));
                }
                else
                {
                    half = halfWidth * Mathf.Sqrt((p.y - bottom) / (waist - bottom));
                }

                return Mathf.Abs(p.x - 0.5f) <= half;
            };
        }

        static Shape Bulb()
        {
            return Or(Circle(new Vector2(0.5f, 0.63f), 0.27f),
                      Rect(new Vector2(0.5f, 0.32f), new Vector2(0.105f, 0.10f)),
                      Rect(new Vector2(0.5f, 0.18f), new Vector2(0.14f, 0.05f)),
                      Rect(new Vector2(0.5f, 0.08f), new Vector2(0.14f, 0.04f)));
        }

        static Shape Dino()
        {
            return Or(RoundRect(new Vector2(0.44f, 0.41f), new Vector2(0.25f, 0.15f), 0.13f),
                      Circle(new Vector2(0.74f, 0.63f), 0.14f),
                      Rect(new Vector2(0.67f, 0.52f), new Vector2(0.08f, 0.14f)),
                      Tri(new Vector2(0.22f, 0.47f), new Vector2(0.22f, 0.33f), new Vector2(0.02f, 0.28f)),
                      Rect(new Vector2(0.34f, 0.20f), new Vector2(0.06f, 0.12f)),
                      Rect(new Vector2(0.55f, 0.20f), new Vector2(0.06f, 0.12f)));
        }

        static Shape Server()
        {
            Shape Shelf(float y) => Minus(RoundRect(new Vector2(0.5f, y), new Vector2(0.34f, 0.08f), 0.035f),
                                          Circle(new Vector2(0.26f, y), 0.032f));
            return Or(Shelf(0.72f), Shelf(0.5f), Shelf(0.28f));
        }

        static Shape Node()
        {
            return Or(Ring(new Vector2(0.5f, 0.5f), 0.36f, 0.22f), Circle(new Vector2(0.5f, 0.5f), 0.12f));
        }

        static Shape Arrow()
        {
            return Or(Rect(new Vector2(0.36f, 0.5f), new Vector2(0.28f, 0.075f)),
                      Tri(new Vector2(0.94f, 0.5f), new Vector2(0.58f, 0.76f), new Vector2(0.58f, 0.24f)));
        }

        static Shape Packet()
        {
            return Rotate(RoundRect(new Vector2(0.5f, 0.5f), new Vector2(0.27f, 0.27f), 0.07f),
                          new Vector2(0.5f, 0.5f), 45f);
        }

        // ------------------------------------------------------------------ raster

        static void Write(string iconName, Shape shape)
        {
            const int samples = 4;   // 4x4 supersampling keeps the edges smooth at 128px
            var texture = new Texture2D(k_Size, k_Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[k_Size * k_Size];

            for (var y = 0; y < k_Size; y++)
            {
                for (var x = 0; x < k_Size; x++)
                {
                    var hits = 0;
                    for (var sy = 0; sy < samples; sy++)
                    {
                        for (var sx = 0; sx < samples; sx++)
                        {
                            var p = new Vector2(
                                (x + (sx + 0.5f) / samples) / k_Size,
                                (y + (sy + 0.5f) / samples) / k_Size);
                            if (shape(p))
                                hits++;
                        }
                    }

                    var alpha = (byte)Mathf.RoundToInt(255f * hits / (samples * samples));
                    pixels[y * k_Size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var path = k_Folder + iconName + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }
}
