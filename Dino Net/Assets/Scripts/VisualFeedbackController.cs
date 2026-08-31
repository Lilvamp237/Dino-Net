using System.Collections;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Attach to an end-user node's dino to play its "happy" reaction and power-up VFX when a
    /// packet is successfully delivered to it.
    /// </summary>
    public class VisualFeedbackController : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional. If set, 'Success' trigger is fired on delivery.")]
        Animator m_Animator;

        [SerializeField, Tooltip("Optional. Activated briefly on delivery (e.g. a power-up particle effect).")]
        GameObject m_PowerUpVfx;

        [SerializeField, Tooltip("Optional. Shown briefly on delivery (e.g. a 'Task Complete!' icon/callout).")]
        GameObject m_TaskCompleteUi;

        [SerializeField]
        float m_FeedbackDuration = 2f;

        static readonly int k_SuccessTrigger = Animator.StringToHash("Success");

        public void PlayDeliverySuccess()
        {
            if (m_Animator != null)
                m_Animator.SetTrigger(k_SuccessTrigger);

            if (m_PowerUpVfx != null)
                StartCoroutine(ShowThenHide(m_PowerUpVfx));

            if (m_TaskCompleteUi != null)
                StartCoroutine(ShowThenHide(m_TaskCompleteUi));
        }

        IEnumerator ShowThenHide(GameObject target)
        {
            target.SetActive(true);
            yield return new WaitForSeconds(m_FeedbackDuration);
            target.SetActive(false);
        }
    }
}
