using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DinoNet
{
    /// <summary>
    /// Big friendly "GO" button on the starting podium. Grabbing/poking/ray-selecting it kicks
    /// off the delivery quest, then it locks so it can't be re-triggered mid-run.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class StartButton : MonoBehaviour
    {
        [SerializeField, Tooltip("Cap that visually depresses when pressed.")]
        Transform m_ButtonCap;

        [SerializeField]
        float m_PressDepth = 0.04f;

        [SerializeField]
        AudioSource m_AudioSource;

        [SerializeField]
        AudioClip m_PressClip;

        public event Action Pressed;

        XRSimpleInteractable m_Interactable;
        Vector3 m_CapRestPosition;
        bool m_Locked;

        void Awake()
        {
            m_Interactable = GetComponent<XRSimpleInteractable>();

            if (m_ButtonCap != null)
                m_CapRestPosition = m_ButtonCap.localPosition;
        }

        void OnEnable()
        {
            m_Interactable.selectEntered.AddListener(OnSelectEntered);
            m_Interactable.hoverEntered.AddListener(OnHoverEntered);
        }

        void OnDisable()
        {
            m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
            m_Interactable.hoverEntered.RemoveListener(OnHoverEntered);
        }

        void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (!m_Locked && m_ButtonCap != null)
                m_ButtonCap.localPosition = m_CapRestPosition - Vector3.up * (m_PressDepth * 0.4f);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (m_Locked)
                return;

            m_Locked = true;

            if (m_AudioSource != null && m_PressClip != null)
                m_AudioSource.PlayOneShot(m_PressClip);

            StartCoroutine(PressAnimation());
            Pressed?.Invoke();
        }

        public void Unlock() => m_Locked = false;

        IEnumerator PressAnimation()
        {
            if (m_ButtonCap == null)
                yield break;

            m_ButtonCap.localPosition = m_CapRestPosition - Vector3.up * m_PressDepth;
            yield return new WaitForSeconds(0.25f);
            m_ButtonCap.localPosition = m_CapRestPosition;
        }
    }
}
