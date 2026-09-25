using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace DinoNet
{
    /// <summary>
    /// Small helpers for building world-space panels in code, so the new features don't need
    /// hand-placed UI in every level scene. Icons are drawn procedurally: the default TextMeshPro
    /// font has no star or heart glyphs.
    /// </summary>
    public static class RuntimeUi
    {
        static Sprite s_Star;
        static Sprite s_Heart;
        static Sprite s_Dot;
        static Sprite s_Square;

        public static Sprite Star => s_Star != null ? s_Star : (s_Star = MakeSprite(96, InStar));
        public static Sprite Heart => s_Heart != null ? s_Heart : (s_Heart = MakeSprite(96, InHeart));
        public static Sprite Dot => s_Dot != null ? s_Dot : (s_Dot = MakeSprite(64, (x, y) => x * x + y * y <= 0.9f));
        public static Sprite Square => s_Square != null ? s_Square : (s_Square = MakeSprite(8, (x, y) => true));

        static bool InStar(float x, float y)
        {
            var angle = Mathf.Atan2(x, y);
            var sector = Mathf.PI * 2f / 5f;
            var t = Mathf.Repeat(angle, sector);
            var d = Mathf.Abs(t - sector * 0.5f) / (sector * 0.5f);
            var boundary = Mathf.Lerp(0.42f, 0.98f, d);
            return Mathf.Sqrt(x * x + y * y) <= boundary;
        }

        static bool InHeart(float x, float y)
        {
            x *= 1.2f;
            y = y * 1.2f + 0.1f;
            var a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }

        static Sprite MakeSprite(int size, Func<float, float, bool> inside)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var covered = 0;
                    for (var sy = 0; sy < 3; sy++)
                    {
                        for (var sx = 0; sx < 3; sx++)
                        {
                            var x = ((px + (sx + 0.5f) / 3f) / size) * 2f - 1f;
                            var y = ((py + (sy + 0.5f) / 3f) / size) * 2f - 1f;
                            if (inside(x, y))
                                covered++;
                        }
                    }

                    pixels[py * size + px] = new Color32(255, 255, 255, (byte)(covered * 255 / 9));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // ---------------------------------------------------------------- building blocks

        public static Canvas WorldCanvas(string name, Vector2 pixels, float scale, Transform parent, bool interactive)
        {
            var go = new GameObject(name, typeof(Canvas));
            if (parent != null)
                go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = pixels;
            rt.localScale = Vector3.one * scale;
            if (interactive)
                go.AddComponent<TrackedDeviceGraphicRaycaster>();

            return canvas;
        }

        public static Image Panel(Transform parent, string name, Color color, Vector2 anchoredPosition, Vector2 size, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            Vector2 anchoredPosition, Vector2 sizeDelta, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            return tmp;
        }

        public static Button MakeButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size,
            Color background, Color textColor, UnityAction onClick, float fontSize = 40f)
        {
            var go = new GameObject(label + " Button", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = background;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            if (onClick != null)
                button.onClick.AddListener(onClick);

            Text(go.transform, "Label", label, fontSize, textColor, Vector2.zero, size, TextAlignmentOptions.Center);
            return button;
        }

        /// <summary>Faces a world-space object toward the headset (keeps it upright).</summary>
        public static void FaceHeadset(Transform t)
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            var dir = t.position - cam.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                t.rotation = Quaternion.LookRotation(dir);
        }

        // ---------------------------------------------------------------- toasts

        sealed class ToastRunner : MonoBehaviour
        {
        }

        static ToastRunner s_Runner;
        static int s_ActiveToasts;

        /// <summary>A short message floating in front of the player, e.g. "Badge earned: Safe Surfer".</summary>
        public static void Toast(string text, Color tint, float seconds = 3.5f, Sprite icon = null)
        {
            if (Camera.main == null)
                return;

            if (s_Runner == null)
            {
                var host = new GameObject("Toasts");
                UnityEngine.Object.DontDestroyOnLoad(host);
                s_Runner = host.AddComponent<ToastRunner>();
            }

            s_Runner.StartCoroutine(ToastRoutine(text, tint, seconds, icon));
        }

        static IEnumerator ToastRoutine(string text, Color tint, float seconds, Sprite icon)
        {
            var slot = s_ActiveToasts++;
            var cam = Camera.main;
            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            var anchor = new GameObject("Toast");
            anchor.transform.position = cam.transform.position + forward * 1.9f + Vector3.up * (0.42f - slot * 0.26f);
            anchor.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            var canvas = WorldCanvas("Toast Canvas", new Vector2(900f, 200f), 0.0013f, anchor.transform, false);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            Panel(canvas.transform, "Background", new Color(0.05f, 0.1f, 0.16f, 0.9f), Vector2.zero, new Vector2(900f, 200f));
            Panel(canvas.transform, "Accent", tint, new Vector2(-440f, 0f), new Vector2(14f, 200f));
            var textOffset = 0f;
            if (icon != null)
            {
                Panel(canvas.transform, "Icon", tint, new Vector2(-350f, 0f), new Vector2(110f, 110f), icon);
                textOffset = 70f;
            }

            Text(canvas.transform, "Text", text, 54f, Color.white, new Vector2(textOffset, 0f), new Vector2(760f - textOffset, 180f));

            var t = 0f;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = t / 0.25f;
                yield return null;
            }

            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(seconds);

            t = 0.4f;
            while (t > 0f)
            {
                t -= Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(t / 0.4f);
                yield return null;
            }

            s_ActiveToasts = Mathf.Max(0, s_ActiveToasts - 1);
            UnityEngine.Object.Destroy(anchor);
        }
    }
}
