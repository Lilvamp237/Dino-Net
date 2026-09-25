using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>Shared helpers for the per-level mechanics.</summary>
    public static class MechanicsUtil
    {
        static Shader s_Unlit;

        static Shader Unlit
        {
            get
            {
                if (s_Unlit == null)
                    s_Unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                return s_Unlit;
            }
        }

        /// <summary>A flat, bright, unlit colour - reads clearly at dusk without needing lights.</summary>
        public static Material GlowMaterial(Color color)
        {
            var m = new Material(Unlit);
            m.SetColor("_BaseColor", color);
            m.color = color;
            return m;
        }

        /// <summary>A see-through unlit colour.</summary>
        public static Material TransparentMaterial(Color color)
        {
            var m = new Material(Unlit);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetColor("_BaseColor", color);
            m.color = color;
            return m;
        }

        public static Transform CreateAnchor(Vector3 position, string name = "Mechanic Anchor")
        {
            var go = new GameObject(name);
            go.transform.position = position;
            return go.transform;
        }

        /// <summary>
        /// Sends the packet back to hover at a spot, letting go of it first if a hand is holding it
        /// (otherwise the grab and the docking would fight over its position).
        /// </summary>
        public static void ReturnOrb(CarryableOrb orb, Vector3 groundPosition, float height)
        {
            if (orb == null)
                return;

            var grab = orb.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grab);

            var anchor = CreateAnchor(new Vector3(groundPosition.x, height, groundPosition.z), "Orb Return Anchor");
            orb.SetHomeAnchor(anchor);
            orb.Dock();
        }

        /// <summary>The point <paramref name="distance"/> metres along a polyline (clamped to its ends).</summary>
        public static Vector3 PointAlong(IReadOnlyList<Vector3> path, float distance, out Vector3 direction)
        {
            direction = Vector3.forward;
            if (path == null || path.Count == 0)
                return Vector3.zero;

            if (path.Count == 1)
                return path[0];

            var travelled = 0f;
            for (var i = 1; i < path.Count; i++)
            {
                var seg = path[i] - path[i - 1];
                seg.y = 0f;
                var len = seg.magnitude;
                if (len < 0.0001f)
                    continue;

                direction = seg / len;
                if (travelled + len >= distance)
                    return path[i - 1] + (path[i] - path[i - 1]) * ((distance - travelled) / len);

                travelled += len;
            }

            return path[path.Count - 1];
        }

        public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
                Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Floating text that always faces the player.</summary>
        public static GameObject Tag(string text, Color color, Vector3 position, float width = 620f, float fontSize = 64f, float scale = 0.0035f)
        {
            var canvas = RuntimeUi.WorldCanvas("Tag", new Vector2(width, 120f), scale, null, false);
            canvas.transform.position = position;
            canvas.gameObject.AddComponent<Billboard>();
            RuntimeUi.Panel(canvas.transform, "Background", new Color(0.05f, 0.08f, 0.14f, 0.82f), Vector2.zero, new Vector2(width, 120f));
            RuntimeUi.Text(canvas.transform, "Text", text, fontSize, color, Vector2.zero, new Vector2(width - 20f, 110f));
            return canvas.gameObject;
        }

        /// <summary>Junction path between two world points along the road network, or a straight line if there is none.</summary>
        public static List<Vector3> RoadPath(RoadNetwork roads, Vector3 from, Vector3 to)
        {
            var points = new List<Vector3>();
            if (roads != null)
            {
                var a = roads.NearestJunction(from);
                var b = roads.NearestJunction(to);
                if (roads.TryGetPath(a, b, points) && points.Count > 1)
                    return points;
            }

            points.Clear();
            points.Add(from);
            points.Add(to);
            return points;
        }
    }
}
