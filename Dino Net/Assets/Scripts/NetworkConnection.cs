using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// A finished link in the network: a glowing strand laid along the road between two dino
    /// nodes. Created only once the child actually reaches a node, so the strand always means
    /// "this connection has been established" rather than "go this way".
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class NetworkConnection : MonoBehaviour
    {
        [SerializeField]
        float m_Width = 0.55f;

        [SerializeField, Tooltip("How far above the ground the strand sits, to avoid z-fighting with the road.")]
        float m_GroundOffset = 0.06f;

        [SerializeField, Tooltip("Brightness it flashes to as the connection is made.")]
        float m_ConnectIntensity = 3.5f;

        [SerializeField, Tooltip("Brightness it settles at once established.")]
        float m_RestIntensity = 1.6f;

        [SerializeField]
        float m_SettleSeconds = 1.2f;

        static readonly int k_Intensity = Shader.PropertyToID("_Intensity");

        LineRenderer m_Line;
        MaterialPropertyBlock m_Block;

        /// <summary>Lays the strand flat along <paramref name="path"/> and flashes it on.</summary>
        public void Build(IReadOnlyList<Vector3> path, Material material)
        {
            m_Line = GetComponent<LineRenderer>();
            m_Block = new MaterialPropertyBlock();

            // Flat on the ground rather than turning to face the camera.
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            m_Line.alignment = LineAlignment.TransformZ;
            m_Line.useWorldSpace = true;
            m_Line.widthMultiplier = m_Width;
            m_Line.numCapVertices = 4;
            m_Line.numCornerVertices = 4;
            m_Line.textureMode = LineTextureMode.Tile;
            m_Line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Line.receiveShadows = false;

            if (material != null)
                m_Line.sharedMaterial = material;

            // Flattened to the floor: the path may start at the podium's orb anchor, which sits
            // well above the ground, and a strand that lifts into the air stops reading as a road.
            m_Line.positionCount = path.Count;
            for (var i = 0; i < path.Count; i++)
                m_Line.SetPosition(i, new Vector3(path[i].x, m_GroundOffset, path[i].z));

            StartCoroutine(SettleRoutine());
        }

        IEnumerator SettleRoutine()
        {
            var elapsed = 0f;
            while (elapsed < m_SettleSeconds)
            {
                elapsed += Time.deltaTime;
                SetIntensity(Mathf.Lerp(m_ConnectIntensity, m_RestIntensity, elapsed / m_SettleSeconds));
                yield return null;
            }

            SetIntensity(m_RestIntensity);
        }

        void SetIntensity(float value)
        {
            m_Line.GetPropertyBlock(m_Block);
            m_Block.SetFloat(k_Intensity, value);
            m_Line.SetPropertyBlock(m_Block);
        }
    }
}
