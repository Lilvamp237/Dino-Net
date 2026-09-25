using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DinoNet
{
    /// <summary>
    /// The sandbox: a free-play mode where the child builds their own network and then finds out,
    /// playfully, where it is weak.
    ///
    /// Build: tap a glowing pad to place a dinosaur, tap two dinosaurs to join them with a vine
    /// (tap the same two again to cut it). Send: a message travels the shortest path from Home to
    /// Friend. Then three gentle "problems" show up, each with a fix the child controls:
    ///   1. No shield  - a sneaky raptor peeks at the message.   Fix: turn the HTTPS shield on.
    ///   2. Weak password - the sneaky raptor guesses the door.  Fix: switch to a strong password.
    ///   3. One road only - Rocky the T-Rex sits on the only route. Fix: build a second route.
    /// Fixing all three earns the "Network Builder" progress.
    /// </summary>
    public class SandboxController : MonoBehaviour
    {
        [SerializeField] GameObject m_TemplateStego;
        [SerializeField] GameObject m_TemplatePara;
        [SerializeField] GameObject m_TemplateTrike;
        [SerializeField] GameObject m_TemplateRaptor;
        [SerializeField] GameObject m_TemplateRex;

        static readonly Vector3 k_HomePos = new Vector3(-8f, 0f, 4f);
        static readonly Vector3 k_FriendPos = new Vector3(8f, 0f, 4f);

        static readonly Vector3[] k_PadPositions =
        {
            new Vector3(-4f, 0f, 0f), new Vector3(-4f, 0f, 8f), new Vector3(0f, 0f, 4f),
            new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 8f), new Vector3(0f, 0f, -3f),
        };

        static readonly Color k_Idle = new Color(0.25f, 0.8f, 1f);
        static readonly Color k_Chosen = new Color(1f, 0.85f, 0.25f);
        static readonly Color k_HomeColor = new Color(0.4f, 1f, 0.55f);
        static readonly Color k_FriendColor = new Color(0.8f, 0.6f, 1f);

        class Node
        {
            public string name;
            public GameObject go;
            public Vector3 position;
            public bool isHome;
            public bool isFriend;
            public int pad = -1;
        }

        class Edge
        {
            public Node a;
            public Node b;
            public LineRenderer line;
        }

        readonly List<Node> m_Nodes = new List<Node>();
        readonly List<Edge> m_Edges = new List<Edge>();
        readonly List<GameObject> m_Pads = new List<GameObject>();
        readonly Stack<System.Action> m_Undo = new Stack<System.Action>();

        Node m_Home;
        Node m_Friend;
        Node m_Selected;
        Material m_VineMaterial;

        bool m_Shield;
        bool m_StrongPassword;
        bool m_Sending;
        readonly HashSet<string> m_FixedThisSession = new HashSet<string>();

        TMP_Text m_Hint;
        TMP_Text m_Log;
        TMP_Text m_ShieldLabel;
        TMP_Text m_PasswordLabel;
        Image m_ShieldDot;
        Image m_PasswordDot;
        Image m_BackupDot;
        GameObject m_Panel;
        GameObject m_Door;
        TMP_Text m_DoorText;

        // ---------------------------------------------------------------- setup

        void Start()
        {
            m_VineMaterial = MechanicsUtil.GlowMaterial(new Color(0.35f, 1f, 0.9f));

            m_Home = SpawnNode("HOME", k_HomePos, m_TemplateStego, true, false);
            m_Friend = SpawnNode("FRIEND", k_FriendPos, m_TemplateTrike, false, true);
            for (var i = 0; i < k_PadPositions.Length; i++)
                m_Pads.Add(BuildPad(i, k_PadPositions[i]));

            BuildDoor();
            BuildPanel();
            SetHint("Tap a glowing pad to place a dino. Tap two dinos to join them with a vine.");
            SessionTelemetry.Log("sandbox_open", 0);
        }

        GameObject BuildPad(int index, Vector3 position)
        {
            var pad = new GameObject("Build Pad " + (index + 1));
            pad.transform.position = position;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(pad.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            disc.transform.localScale = new Vector3(2.6f, 0.03f, 2.6f);
            disc.GetComponent<Renderer>().sharedMaterial = MechanicsUtil.GlowMaterial(new Color(0.35f, 0.95f, 0.6f));

            var tag = MechanicsUtil.Tag("+ DINO", new Color(0.7f, 1f, 0.8f), position + Vector3.up * 1.4f, 320f, 56f);
            tag.transform.SetParent(pad.transform, true);
            tag.name = "Pad Tag";

            var collider = pad.AddComponent<SphereCollider>();
            collider.radius = 1.4f;
            collider.center = new Vector3(0f, 0.6f, 0f);
            var interactable = pad.AddComponent<XRSimpleInteractable>();
            var captured = index;
            interactable.selectEntered.AddListener(_ => OnPadSelected(captured));
            return pad;
        }

        Node SpawnNode(string label, Vector3 position, GameObject template, bool home, bool friend)
        {
            var node = new Node { name = label, position = position, isHome = home, isFriend = friend };
            if (template != null)
            {
                node.go = Instantiate(template);
                node.go.name = label;
                node.go.SetActive(true);
                node.go.transform.position = new Vector3(position.x, template.transform.position.y, position.z);
                var toCentre = new Vector3(0f, 0f, 4f) - position;
                toCentre.y = 0f;
                if (toCentre.sqrMagnitude > 0.01f)
                    node.go.transform.rotation = Quaternion.LookRotation(toCentre.normalized, Vector3.up);
            }
            else
            {
                node.go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                node.go.name = label;
                node.go.transform.position = position + Vector3.up;
            }

            var tag = MechanicsUtil.Tag(label, home ? k_HomeColor : friend ? k_FriendColor : new Color(0.8f, 0.95f, 1f), position + Vector3.up * 3.3f, 360f, 56f);
            tag.transform.SetParent(node.go.transform, true);

            var interactable = node.go.GetComponent<XRSimpleInteractable>();
            if (interactable == null)
            {
                if (node.go.GetComponent<Collider>() == null)
                {
                    var box = node.go.AddComponent<CapsuleCollider>();
                    box.height = 3f;
                    box.radius = 1f;
                    box.center = Vector3.up * 1.5f;
                }

                interactable = node.go.AddComponent<XRSimpleInteractable>();
            }

            var captured = node;
            interactable.selectEntered.AddListener(_ => SelectNode(captured));
            m_Nodes.Add(node);
            Colour(node, home ? k_HomeColor : friend ? k_FriendColor : k_Idle);
            return node;
        }

        // ---------------------------------------------------------------- building

        /// <summary>Called by a pad's interactable. Public so tests can drive the sandbox without a headset.</summary>
        public void OnPadSelected(int index)
        {
            if (m_Sending || index < 0 || index >= m_Pads.Count)
                return;

            foreach (var n in m_Nodes)
            {
                if (n.pad == index)
                {
                    SelectNode(n);
                    return;
                }
            }

            var relays = 0;
            foreach (var n in m_Nodes)
            {
                if (!n.isHome && !n.isFriend)
                    relays++;
            }

            var template = relays % 2 == 0 ? m_TemplatePara : m_TemplateStego;
            var node = SpawnNode("DINO " + (relays + 1), k_PadPositions[index], template, false, false);
            node.pad = index;
            var padTag = m_Pads[index].transform.Find("Pad Tag");
            if (padTag != null)
                padTag.gameObject.SetActive(false);

            Sfx.Play2D("ui_pop", 0.8f);
            m_Undo.Push(() => RemoveRelay(node));
            SessionTelemetry.Log("sandbox_build", 0, "nodes", m_Nodes.Count, "edges", m_Edges.Count, "action", "place");
            SetHint("Nice! Tap two dinos to join them. Connect Home to Friend, then press Send.");
        }

        /// <summary>Selects a node by its label (e.g. "HOME", "FRIEND", "DINO 1"). Lets tests drive the sandbox without a headset.</summary>
        public void SelectByName(string nodeName)
        {
            foreach (var n in m_Nodes)
            {
                if (n.name == nodeName)
                {
                    SelectNode(n);
                    return;
                }
            }
        }

        public bool ShieldOn => m_Shield;

        public bool StrongPasswordOn => m_StrongPassword;

        public bool IsSending => m_Sending;

        public int NodeCount => m_Nodes.Count;

        public int EdgeCount => m_Edges.Count;

        void SelectNode(Node node)
        {
            if (m_Sending)
                return;

            if (m_Selected == null)
            {
                m_Selected = node;
                Colour(node, k_Chosen);
                Sfx.Play2D("ui_click", 0.8f);
                SetHint("Now tap another dino to join them with a vine.");
                return;
            }

            if (m_Selected == node)
            {
                Colour(node, BaseColour(node));
                m_Selected = null;
                return;
            }

            var first = m_Selected;
            Colour(first, BaseColour(first));
            m_Selected = null;

            var existing = FindEdge(first, node);
            if (existing != null)
            {
                RemoveEdge(existing);
                m_Undo.Push(() => AddEdge(first, node));
                Sfx.Play2D("lost_whoosh", 0.4f, 1.4f);
                SetHint("Vine cut. Tap two dinos to join them again.");
            }
            else
            {
                AddEdge(first, node);
                m_Undo.Push(() => RemoveEdge(FindEdge(first, node)));
                Sfx.Play2D("ack_ding", 0.6f, 1.2f);
                SetHint(IsConnected(m_Home, m_Friend, null, null) ? "Home and Friend are connected! Press Send message." : "Keep joining dinos until Home reaches Friend.");
            }

            SessionTelemetry.Log("sandbox_build", 0, "nodes", m_Nodes.Count, "edges", m_Edges.Count, "action", existing != null ? "cut" : "link");
        }

        Edge AddEdge(Node a, Node b)
        {
            var go = new GameObject("Vine " + a.name + "-" + b.name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = m_VineMaterial;
            line.widthMultiplier = 0.18f;
            line.numCapVertices = 4;
            line.useWorldSpace = true;
            var start = a.position + Vector3.up * 2f;
            var end = b.position + Vector3.up * 2f;
            const int segments = 18;
            line.positionCount = segments + 1;
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var p = Vector3.Lerp(start, end, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * 0.9f;
                line.SetPosition(i, p);
            }

            line.startColor = line.endColor = new Color(0.4f, 1f, 0.9f);
            var edge = new Edge { a = a, b = b, line = line };
            m_Edges.Add(edge);
            return edge;
        }

        void RemoveEdge(Edge edge)
        {
            if (edge == null)
                return;

            m_Edges.Remove(edge);
            if (edge.line != null)
                Destroy(edge.line.gameObject);
        }

        void RemoveRelay(Node node)
        {
            if (node == null || node.isHome || node.isFriend)
                return;

            for (var i = m_Edges.Count - 1; i >= 0; i--)
            {
                if (m_Edges[i].a == node || m_Edges[i].b == node)
                    RemoveEdge(m_Edges[i]);
            }

            if (node.pad >= 0)
            {
                var padTag = m_Pads[node.pad].transform.Find("Pad Tag");
                if (padTag != null)
                    padTag.gameObject.SetActive(true);
            }

            m_Nodes.Remove(node);
            if (m_Selected == node)
                m_Selected = null;

            Destroy(node.go);
        }

        Edge FindEdge(Node a, Node b)
        {
            foreach (var e in m_Edges)
            {
                if ((e.a == a && e.b == b) || (e.a == b && e.b == a))
                    return e;
            }

            return null;
        }

        static Node Other(Edge e, Node n) => e.a == n ? e.b : e.a;

        // ---------------------------------------------------------------- graph

        /// <summary>Shortest path (fewest vines) from one node to another, optionally avoiding a node or a vine.</summary>
        List<Node> FindPath(Node from, Node to, Node avoidNode, Edge avoidEdge)
        {
            var came = new Dictionary<Node, Node>();
            var queue = new Queue<Node>();
            queue.Enqueue(from);
            came[from] = null;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == to)
                {
                    var path = new List<Node>();
                    for (var n = to; n != null; n = came[n])
                        path.Add(n);
                    path.Reverse();
                    return path;
                }

                foreach (var e in m_Edges)
                {
                    if (e == avoidEdge || (e.a != current && e.b != current))
                        continue;

                    var next = Other(e, current);
                    if (next == avoidNode || came.ContainsKey(next))
                        continue;

                    came[next] = current;
                    queue.Enqueue(next);
                }
            }

            return null;
        }

        bool IsConnected(Node a, Node b, Node avoidNode, Edge avoidEdge) => FindPath(a, b, avoidNode, avoidEdge) != null;

        // ---------------------------------------------------------------- sending

        public void Send()
        {
            if (!m_Sending)
                StartCoroutine(SendRoutine());
        }

        IEnumerator SendRoutine()
        {
            m_Sending = true;
            var path = FindPath(m_Home, m_Friend, null, null);
            SessionTelemetry.Log("sandbox_send", 0, "connected", path != null, "shield", m_Shield, "strongPassword", m_StrongPassword,
                "nodes", m_Nodes.Count, "edges", m_Edges.Count);

            if (path == null)
            {
                Log("No road from Home to Friend yet! Join the dinos with vines.");
                Sfx.Play2D("lost_whoosh", 0.5f);
                VoiceOver.Speak("There is no road from Home to Friend yet. Join the dinosaurs with vines.");
                m_Sending = false;
                yield break;
            }

            // The message: readable without the shield, scrambled with it.
            var packet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(packet.GetComponent<Collider>());
            packet.transform.localScale = Vector3.one * 0.5f;
            packet.GetComponent<Renderer>().sharedMaterial = MechanicsUtil.GlowMaterial(m_Shield ? new Color(0.8f, 0.6f, 1f) : new Color(1f, 0.85f, 0.3f));
            var label = MechanicsUtil.Tag(m_Shield ? "#@%&  (locked)" : "HELLO FRIEND!", m_Shield ? new Color(0.85f, 0.7f, 1f) : new Color(1f, 0.95f, 0.6f), Vector3.zero, 520f, 56f);
            label.transform.SetParent(packet.transform, false);
            label.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            label.transform.localScale = Vector3.one * 2f;

            GameObject sneaky = null;
            for (var i = 0; i < path.Count; i++)
            {
                var target = path[i].position + Vector3.up * 2f;
                if (i == 0)
                    packet.transform.position = target;

                while (Vector3.Distance(packet.transform.position, target) > 0.05f)
                {
                    packet.transform.position = Vector3.MoveTowards(packet.transform.position, target, 4.2f * Time.deltaTime);
                    yield return null;
                }

                Sfx.PlayAt("ui_pop", target, 0.5f, 1f + i * 0.1f);

                // Halfway along, the sneaky raptor tries to peek.
                if (i == path.Count / 2 && path.Count > 1 && sneaky == null)
                {
                    sneaky = SpawnSneaky(path[i].position);
                    yield return new WaitForSeconds(0.6f);
                    if (m_Shield)
                    {
                        Log("Sneaky Raptor: \"Grr! I can't read it... #@%&!\"  The HTTPS shield works!");
                        VoiceOver.Speak("Sneaky Raptor can't read it. The shield works!");
                        RegisterFix("shield");
                    }
                    else
                    {
                        Log("Oops! Sneaky Raptor peeked: \"HELLO FRIEND!\"  Turn ON the HTTPS shield to scramble it.");
                        VoiceOver.Speak("Oops! Sneaky Raptor peeked at your message. Turn on the H T T P S shield to scramble it.");
                        Sfx.Play2D("warning_pulse", 0.5f);
                        SessionTelemetry.Log("sandbox_vulnerability", 0, "kind", "eavesdrop");
                    }

                    yield return new WaitForSeconds(1.8f);
                }
            }

            Sfx.Play2D("ack_ding", 0.9f);
            Destroy(packet);

            // The door to Friend's house is guarded by a password.
            yield return new WaitForSeconds(0.4f);
            if (m_StrongPassword)
            {
                Log("Sneaky Raptor tried to guess the password... and gave up. \"It would take a million years!\"");
                VoiceOver.Speak("Sneaky Raptor tried to guess the password and gave up. A strong password is very hard to guess.");
                RegisterFix("password");
            }
            else
            {
                Log("Uh-oh! Sneaky Raptor guessed the password \"1234\" in one try. Switch to a STRONG password!");
                VoiceOver.Speak("Uh oh! Sneaky Raptor guessed the password in one try. Switch to a strong password.");
                Sfx.Play2D("warning_pulse", 0.5f);
                SessionTelemetry.Log("sandbox_vulnerability", 0, "kind", "weak_password");
            }

            yield return new WaitForSeconds(2.2f);
            if (sneaky != null)
                Destroy(sneaky);

            // Resilience: what if one dinosaur (or the only vine) had a nap?
            yield return StartCoroutine(ResilienceCheck(path));

            CheckPerfect();
            m_Sending = false;
        }

        IEnumerator ResilienceCheck(List<Node> path)
        {
            Node weakNode = null;
            Edge weakEdge = null;

            // A relay that the whole network depends on?
            for (var i = 1; i < path.Count - 1 && weakNode == null; i++)
            {
                if (!IsConnected(m_Home, m_Friend, path[i], null))
                    weakNode = path[i];
            }

            // Or, with no relays, the single vine between Home and Friend.
            if (weakNode == null && path.Count == 2)
            {
                var only = FindEdge(path[0], path[1]);
                if (!IsConnected(m_Home, m_Friend, null, only))
                    weakEdge = only;
            }

            if (weakNode == null && weakEdge == null && path.Count > 2)
            {
                // Every relay can be bypassed - but only a real second route counts, so check the vines too.
                for (var i = 0; i < path.Count - 1 && weakEdge == null; i++)
                {
                    var edge = FindEdge(path[i], path[i + 1]);
                    if (!IsConnected(m_Home, m_Friend, null, edge))
                        weakEdge = edge;
                }
            }

            if (weakNode != null || weakEdge != null)
            {
                var spot = weakNode != null ? weakNode.position : (weakEdge.a.position + weakEdge.b.position) * 0.5f;
                var rex = SpawnRex(spot);
                Log("Rocky the T-Rex sat down on the only road! " + (weakNode != null ? "Nothing can get past " + weakNode.name : "The single vine is blocked") + ". Build a SECOND route!");
                VoiceOver.Speak("Rocky the T rex sat down on the only road! Build a second route so messages can go around him.");
                Sfx.PlayAt("roar", spot, 0.5f, 1.2f);
                SessionTelemetry.Log("sandbox_vulnerability", 0, "kind", "single_point_of_failure");
                SetBackupDot(false);
                yield return new WaitForSeconds(3.2f);
                if (rex != null)
                    Destroy(rex);
            }
            else
            {
                Log("Rocky the T-Rex sat on a road, but the message just went around him. Backup routes work!");
                VoiceOver.Speak("Rocky the T rex sat on a road, but the message just went around him. Backup routes work!");
                RegisterFix("backup");
                SetBackupDot(true);
                yield return new WaitForSeconds(2.4f);
            }
        }

        void RegisterFix(string fix)
        {
            if (!m_FixedThisSession.Add(fix))
                return;

            ProgressStore.RecordSandboxFix();
            SessionTelemetry.Log("sandbox_fix", 0, "fix", fix);
            Sfx.Play2D("sparkle", 0.7f);
            RuntimeUi.Toast(fix == "shield" ? "Fixed: HTTPS shield!" : fix == "password" ? "Fixed: strong password!" : "Fixed: backup route!", new Color(0.5f, 1f, 0.7f), 3f, RuntimeUi.Star);
            if (fix == "shield") SetDot(m_ShieldDot, true);
            if (fix == "password") SetDot(m_PasswordDot, true);
        }

        void CheckPerfect()
        {
            if (m_FixedThisSession.Contains("shield") && m_FixedThisSession.Contains("password") && m_FixedThisSession.Contains("backup"))
            {
                Log("PERFECT NETWORK! Safe, strong and full of backup roads. You're a real network builder!");
                VoiceOver.Speak("Perfect network! Safe, strong and full of backup roads. You are a real network builder!");
                Sfx.Play2D("power_up", 0.9f);
                RuntimeUi.Toast("Perfect network!", new Color(1f, 0.9f, 0.4f), 5f, RuntimeUi.Star);
                foreach (var n in m_Nodes)
                    StartCoroutine(Glow(n.position + Vector3.up * 2.6f));
            }
        }

        IEnumerator Glow(Vector3 position)
        {
            var go = new GameObject("Network Light");
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.9f, 0.5f);
            light.range = 9f;
            var t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                light.intensity = Mathf.Lerp(0f, 4f, t / 1.2f);
                yield return null;
            }
        }

        GameObject SpawnSneaky(Vector3 near)
        {
            var go = Spawn(m_TemplateRaptor, near + new Vector3(1.8f, 0f, -1.4f));
            if (go != null)
            {
                var tag = MechanicsUtil.Tag("SNEAKY RAPTOR", new Color(1f, 0.6f, 0.5f), go.transform.position + Vector3.up * 2.4f, 480f, 54f);
                tag.transform.SetParent(go.transform, true);
                Sfx.PlayAt("grunt_small", go.transform.position, 0.6f, 1.4f);
            }

            return go;
        }

        GameObject SpawnRex(Vector3 at)
        {
            var go = Spawn(m_TemplateRex, at + new Vector3(0f, 0f, -1.6f));
            if (go != null)
            {
                var tag = MechanicsUtil.Tag("ROCKY THE T-REX", new Color(1f, 0.7f, 0.4f), go.transform.position + Vector3.up * 3.2f, 560f, 54f);
                tag.transform.SetParent(go.transform, true);
            }

            return go;
        }

        GameObject Spawn(GameObject template, Vector3 position)
        {
            if (template == null)
                return null;

            var go = Instantiate(template);
            go.SetActive(true);
            go.transform.position = new Vector3(position.x, template.transform.position.y, position.z);
            var toCentre = new Vector3(0f, 0f, 4f) - position;
            toCentre.y = 0f;
            if (toCentre.sqrMagnitude > 0.01f)
                go.transform.rotation = Quaternion.LookRotation(toCentre.normalized, Vector3.up);
            return go;
        }

        // ---------------------------------------------------------------- panel

        void BuildDoor()
        {
            m_Door = new GameObject("Friend's Door");
            m_Door.transform.position = k_FriendPos + new Vector3(2.4f, 0f, 0f);
            MechanicsUtil.Box("Door", m_Door.transform, m_Door.transform.position + Vector3.up * 1.2f, new Vector3(0.3f, 2.4f, 1.6f), MechanicsUtil.GlowMaterial(new Color(0.55f, 0.36f, 0.2f)), false);
            var tag = MechanicsUtil.Tag("PASSWORD: 1234", new Color(1f, 0.7f, 0.5f), m_Door.transform.position + Vector3.up * 3f, 560f, 54f);
            tag.transform.SetParent(m_Door.transform, true);
            m_DoorText = tag.GetComponentInChildren<TMP_Text>();
        }

        void BuildPanel()
        {
            var anchor = new GameObject("Build Tablet");
            var canvas = RuntimeUi.WorldCanvas("Build Tablet Canvas", new Vector2(1200f, 760f), 0.0014f, anchor.transform, true);
            canvas.transform.localPosition = new Vector3(0f, 0f, 1.9f);
            RuntimeUi.Panel(canvas.transform, "Background", new Color(0.05f, 0.15f, 0.25f, 0.94f), Vector2.zero, new Vector2(1200f, 760f));

            RuntimeUi.Text(canvas.transform, "Title", "Build your own network!", 56f, Color.white, new Vector2(0f, 325f), new Vector2(1100f, 70f));
            m_Hint = RuntimeUi.Text(canvas.transform, "Hint", string.Empty, 34f, new Color(0.75f, 0.92f, 1f), new Vector2(0f, 250f), new Vector2(1120f, 90f));
            m_Log = RuntimeUi.Text(canvas.transform, "Log", "Press Send to see what happens...", 36f, new Color(1f, 0.92f, 0.6f), new Vector2(0f, 120f), new Vector2(1120f, 130f));

            // Three fix checkmarks.
            m_ShieldDot = RuntimeUi.Panel(canvas.transform, "ShieldDot", new Color(1f, 1f, 1f, 0.2f), new Vector2(-400f, 25f), new Vector2(40f, 40f), RuntimeUi.Star);
            RuntimeUi.Text(canvas.transform, "ShieldFix", "HTTPS shield", 30f, Color.white, new Vector2(-260f, 25f), new Vector2(240f, 44f), TextAlignmentOptions.Left);
            m_PasswordDot = RuntimeUi.Panel(canvas.transform, "PasswordDot", new Color(1f, 1f, 1f, 0.2f), new Vector2(-40f, 25f), new Vector2(40f, 40f), RuntimeUi.Star);
            RuntimeUi.Text(canvas.transform, "PasswordFix", "Strong password", 30f, Color.white, new Vector2(120f, 25f), new Vector2(280f, 44f), TextAlignmentOptions.Left);
            m_BackupDot = RuntimeUi.Panel(canvas.transform, "BackupDot", new Color(1f, 1f, 1f, 0.2f), new Vector2(330f, 25f), new Vector2(40f, 40f), RuntimeUi.Star);
            RuntimeUi.Text(canvas.transform, "BackupFix", "Backup route", 30f, Color.white, new Vector2(470f, 25f), new Vector2(240f, 44f), TextAlignmentOptions.Left);

            RuntimeUi.MakeButton(canvas.transform, "Send message", new Vector2(-390f, -70f), new Vector2(360f, 96f), new Color(0.3f, 0.8f, 0.5f), Color.white, Send, 40f);
            var shield = RuntimeUi.MakeButton(canvas.transform, "Shield: OFF", new Vector2(0f, -70f), new Vector2(360f, 96f), new Color(0.35f, 0.5f, 0.85f), Color.white, ToggleShield, 40f);
            m_ShieldLabel = shield.GetComponentInChildren<TMP_Text>();
            var password = RuntimeUi.MakeButton(canvas.transform, "Password: WEAK", new Vector2(390f, -70f), new Vector2(360f, 96f), new Color(0.75f, 0.5f, 0.3f), Color.white, TogglePassword, 36f);
            m_PasswordLabel = password.GetComponentInChildren<TMP_Text>();

            RuntimeUi.MakeButton(canvas.transform, "Undo", new Vector2(-390f, -190f), new Vector2(360f, 84f), new Color(1f, 1f, 1f, 0.9f), new Color(0.1f, 0.12f, 0.14f), Undo, 38f);
            RuntimeUi.MakeButton(canvas.transform, "Clear all", new Vector2(0f, -190f), new Vector2(360f, 84f), new Color(1f, 1f, 1f, 0.9f), new Color(0.1f, 0.12f, 0.14f), ClearAll, 38f);
            RuntimeUi.MakeButton(canvas.transform, "Back to menu", new Vector2(390f, -190f), new Vector2(360f, 84f), new Color(1f, 1f, 1f, 0.9f), new Color(0.1f, 0.12f, 0.14f), GameFlow.LoadMainMenu, 36f);

            RuntimeUi.Text(canvas.transform, "Tip", "Try it: send once as it is, then fix what goes wrong!", 30f, new Color(0.65f, 0.85f, 0.95f), new Vector2(0f, -290f), new Vector2(1100f, 50f));

            m_Panel = anchor;
            PanelAnchor.PlaceInFront(anchor, -0.15f);
        }

        void Update()
        {
            if (m_Panel != null && PanelAnchor.HasDrifted(m_Panel, 75f, 6f))
                PanelAnchor.PlaceInFront(m_Panel, -0.15f);
        }

        public void ToggleShield()
        {
            m_Shield = !m_Shield;
            if (m_ShieldLabel != null)
                m_ShieldLabel.text = "Shield: " + (m_Shield ? "ON" : "OFF");

            Sfx.Play2D(m_Shield ? "shield_up" : "ui_click", 0.8f);
        }

        public void TogglePassword()
        {
            m_StrongPassword = !m_StrongPassword;
            if (m_PasswordLabel != null)
                m_PasswordLabel.text = "Password: " + (m_StrongPassword ? "STRONG" : "WEAK");

            if (m_DoorText != null)
                m_DoorText.text = m_StrongPassword ? "PASSWORD: Purple-Volcano-7" : "PASSWORD: 1234";

            Sfx.Play2D("ui_click", 0.8f);
        }

        public void Undo()
        {
            if (m_Sending || m_Undo.Count == 0)
                return;

            m_Undo.Pop().Invoke();
            Sfx.Play2D("ui_click", 0.6f, 0.8f);
        }

        public void ClearAll()
        {
            if (m_Sending)
                return;

            for (var i = m_Nodes.Count - 1; i >= 0; i--)
                RemoveRelay(m_Nodes[i]);

            for (var i = m_Edges.Count - 1; i >= 0; i--)
                RemoveEdge(m_Edges[i]);

            m_Undo.Clear();
            m_Selected = null;
            SetHint("Fresh start! Tap a glowing pad to place a dino.");
        }

        // ---------------------------------------------------------------- small helpers

        void Log(string text)
        {
            if (m_Log != null)
                m_Log.text = text;
        }

        void SetHint(string text)
        {
            if (m_Hint != null)
                m_Hint.text = text;
        }

        static void SetDot(Image dot, bool on)
        {
            if (dot != null)
                dot.color = on ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 1f, 1f, 0.2f);
        }

        void SetBackupDot(bool on) => SetDot(m_BackupDot, on);

        static Color BaseColour(Node n) => n.isHome ? k_HomeColor : n.isFriend ? k_FriendColor : k_Idle;

        static void Colour(Node node, Color colour)
        {
            if (node == null || node.go == null)
                return;

            var ring = node.go.transform.Find("NodeRing");
            if (ring == null)
                return;

            var renderer = ring.GetComponent<Renderer>();
            if (renderer == null)
                return;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", colour);
            block.SetColor("_EmissionColor", colour * 1.6f);
            renderer.SetPropertyBlock(block);
        }
    }
}
