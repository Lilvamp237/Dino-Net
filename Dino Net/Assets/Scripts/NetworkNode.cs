using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DinoNet
{
    public enum NodeType
    {
        Source,
        Hub,
        EndUser,
    }

    [RequireComponent(typeof(XRSimpleInteractable))]
    public class NetworkNode : MonoBehaviour
    {
        [SerializeField, Tooltip("Unique id for this node. Defaults to the GameObject name if left blank.")]
        string m_NodeId;

        [SerializeField]
        NodeType m_NodeType;

        [SerializeField, Tooltip("Point the vine/packet visuals connect to. Defaults to this transform if left blank.")]
        Transform m_ConnectionAnchor;

        public string NodeId => string.IsNullOrEmpty(m_NodeId) ? name : m_NodeId;
        public NodeType NodeType => m_NodeType;
        public Transform ConnectionAnchor => m_ConnectionAnchor != null ? m_ConnectionAnchor : transform;

        public event Action<NetworkNode> Selected;

        public static event Action<NetworkNode> AnyNodeSelected;

        XRSimpleInteractable m_Interactable;

        void Awake()
        {
            m_Interactable = GetComponent<XRSimpleInteractable>();
        }

        void OnEnable()
        {
            m_Interactable.selectEntered.AddListener(OnSelectEntered);
        }

        void OnDisable()
        {
            m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        void Start()
        {
            NetworkTopologyManager.Instance.RegisterNode(this);
        }

        void OnDestroy()
        {
            if (NetworkTopologyManager.Instance != null)
                NetworkTopologyManager.Instance.UnregisterNode(this);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            Selected?.Invoke(this);
            AnyNodeSelected?.Invoke(this);
        }
    }
}
