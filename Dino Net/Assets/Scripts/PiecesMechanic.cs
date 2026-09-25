using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DinoNet
{
    /// <summary>
    /// Teaches that big messages travel in pieces (packets). A small "picture" board in the
    /// corner of the child's view is made of one tile per dinosaur on the route. Every time the
    /// packet reaches a dinosaur another piece arrives; when the last one lands the pieces join
    /// up into the whole picture - the same way a real network reassembles a message.
    /// </summary>
    public class PiecesMechanic : MonoBehaviour
    {
        static readonly Color[] k_Palette =
        {
            new Color(1f, 0.55f, 0.4f), new Color(1f, 0.82f, 0.3f), new Color(0.5f, 0.9f, 0.5f),
            new Color(0.4f, 0.8f, 1f), new Color(0.7f, 0.6f, 1f), new Color(1f, 0.6f, 0.85f), new Color(0.6f, 1f, 0.9f),
        };

        DinoQuestManager m_Quest;
        Image[] m_Tiles;
        Image m_Whole;
        GameObject m_Board;

        void Start()
        {
            m_Quest = FindFirstObjectByType<DinoQuestManager>();
            var cam = Camera.main;
            if (m_Quest == null || cam == null || m_Quest.RouteCount == 0)
            {
                enabled = false;
                return;
            }

            var count = m_Quest.RouteCount;
            var width = 120f + count * 92f;
            var canvas = RuntimeUi.WorldCanvas("Message Pieces", new Vector2(width, 190f), 0.0011f, cam.transform, false);
            canvas.transform.localPosition = new Vector3(0.52f, -0.3f, 1.5f);
            canvas.transform.localRotation = Quaternion.identity;
            m_Board = canvas.gameObject;

            RuntimeUi.Panel(canvas.transform, "Background", new Color(0.04f, 0.1f, 0.16f, 0.85f), Vector2.zero, new Vector2(width, 190f));
            RuntimeUi.Text(canvas.transform, "Title", "MESSAGE PIECES", 32f, new Color(0.8f, 0.92f, 1f), new Vector2(0f, 68f), new Vector2(width - 20f, 44f));

            m_Tiles = new Image[count];
            for (var i = 0; i < count; i++)
            {
                var x = (i - (count - 1) * 0.5f) * 88f;
                m_Tiles[i] = RuntimeUi.Panel(canvas.transform, "Piece " + (i + 1), new Color(1f, 1f, 1f, 0.12f), new Vector2(x, -14f), new Vector2(76f, 76f), RuntimeUi.Square);
            }

            m_Whole = RuntimeUi.Panel(canvas.transform, "Whole", new Color(1f, 0.85f, 0.3f, 0f), new Vector2(0f, -14f), new Vector2(110f, 110f), RuntimeUi.Star);

            m_Quest.NodeConnected += OnNode;
            m_Quest.QuestCompleted += OnComplete;
        }

        void OnDestroy()
        {
            if (m_Quest != null)
            {
                m_Quest.NodeConnected -= OnNode;
                m_Quest.QuestCompleted -= OnComplete;
            }
        }

        void OnNode(QuestNode node)
        {
            var index = m_Quest.ConnectedCount - 1;
            if (m_Tiles == null || index < 0 || index >= m_Tiles.Length)
                return;

            StartCoroutine(Land(m_Tiles[index], k_Palette[index % k_Palette.Length]));
            if (index == 0)
                RuntimeUi.Toast("Piece 1 arrived! Big messages travel in small pieces.", new Color(0.55f, 0.85f, 1f), 3.5f);
        }

        void OnComplete()
        {
            StartCoroutine(Join());
        }

        static IEnumerator Land(Image tile, Color color)
        {
            var t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                var k = t / 0.4f;
                tile.color = Color.Lerp(new Color(1f, 1f, 1f, 0.12f), color, k);
                tile.transform.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.3f);
                yield return null;
            }

            tile.transform.localScale = Vector3.one;
        }

        IEnumerator Join()
        {
            yield return new WaitForSeconds(0.9f);
            Sfx.Play2D("sparkle", 0.8f);
            RuntimeUi.Toast("All the pieces arrived and joined up - that's a whole message!", new Color(1f, 0.9f, 0.4f), 5f);
            VoiceOver.Speak("All the pieces arrived and joined up. That's a whole message!");

            var t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                foreach (var tile in m_Tiles)
                {
                    var c = tile.color;
                    c.a = Mathf.Lerp(c.a, 0f, t);
                    tile.color = c;
                }

                m_Whole.color = new Color(1f, 0.85f, 0.3f, t);
                m_Whole.transform.localScale = Vector3.one * (0.6f + t * 0.5f);
                yield return null;
            }
        }
    }
}
