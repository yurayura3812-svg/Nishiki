using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KoiPond;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 池のえさやり。メニューの EsaButton を水色にしてモードにし、
/// 画面タップで白い餌を落とす。レイアウトは触らない。
/// </summary>
public class FeedController : MonoBehaviour
{
    public static FeedController Instance { get; private set; }
    public static bool IsFeeding { get; private set; }
    public static float WaterY { get; private set; } = 1.25f;

    static readonly Color ActiveFill = new Color(0.706f, 0.855f, 0.957f, 1f);
    static readonly List<FeedPellet> Pellets = new List<FeedPellet>();

    Image esaImage;
    Button esaButton;
    Color esaNormal = Color.white;
    int ignoreClickFrame;
    Material pelletMat;
    float waterY = 1.25f;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        var sim = FindFirstObjectByType<RippleSimulator>();
        if (sim != null) waterY = sim.waterY;
        WaterY = waterY;
        HookEsaButton();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (IsFeeding) IsFeeding = false;
        if (pelletMat != null) Destroy(pelletMat);
    }

    void HookEsaButton()
    {
        foreach (var btn in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (btn == null || btn.name != "EsaButton") continue;
            esaButton = btn;
            esaImage = btn.GetComponent<Image>();
            if (esaImage != null) esaNormal = esaImage.color;
            btn.onClick.AddListener(Toggle);
            break;
        }
    }

    void Toggle()
    {
        IsFeeding = !IsFeeding;
        ignoreClickFrame = Time.frameCount;
        if (esaImage != null)
            esaImage.color = IsFeeding ? ActiveFill : esaNormal;
        if (esaButton != null)
            esaButton.transition = IsFeeding ? Selectable.Transition.None : Selectable.Transition.ColorTint;
    }

    void Update()
    {
        Prune();
        if (!IsFeeding) return;
        if (Time.frameCount <= ignoreClickFrame) return;
        if (!Pressed(out Vector2 screen)) return;
        if (OverUI(screen)) return;
        Drop(screen);
    }

    static bool Pressed(out Vector2 screen)
    {
        screen = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screen = Mouse.current.position.ReadValue();
            return true;
        }
        var pad = Touchscreen.current;
        if (pad != null)
        {
            var touches = pad.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (!t.press.wasPressedThisFrame) continue;
                screen = t.position.ReadValue();
                return true;
            }
        }
#endif
        return false;
    }

    static bool OverUI(Vector2 screen)
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = screen };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, hits);
        return hits.Count > 0;
    }

    void Drop(Vector2 screen)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
        var plane = new Plane(Vector3.up, new Vector3(0f, waterY, 0f));
        if (!plane.Raycast(ray, out float enter)) return;

        Vector3 pos = ray.GetPoint(enter);
        int count = Random.Range(14, 22);
        for (int i = 0; i < count; i++)
        {
            if (Pellets.Count >= 64) break;
            Vector2 scatter = Random.insideUnitCircle * 0.28f;
            float depth = Random.Range(0.22f, 0.45f);
            SpawnPellet(pos + new Vector3(scatter.x, -depth, scatter.y));
        }

        var sim = FindFirstObjectByType<RippleSimulator>();
        if (sim != null) sim.AddImpulseWorld(pos, 0.22f, 0.18f);
    }

    void SpawnPellet(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "FeedPellet";
        go.transform.SetParent(transform, true);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * Random.Range(0.016f, 0.024f);
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            if (pelletMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                pelletMat = sh != null ? new Material(sh) : null;
                if (pelletMat != null)
                {
                    if (pelletMat.HasProperty("_BaseColor"))
                        pelletMat.SetColor("_BaseColor", Color.white);
                    if (pelletMat.HasProperty("_Color"))
                        pelletMat.SetColor("_Color", Color.white);
                }
            }
            if (pelletMat != null) rend.sharedMaterial = pelletMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        Pellets.Add(go.AddComponent<FeedPellet>());
    }

    static void Prune()
    {
        for (int i = Pellets.Count - 1; i >= 0; i--)
        {
            if (Pellets[i] == null) Pellets.RemoveAt(i);
        }
    }

    public static bool TryAttract(Transform koi, out Vector3 cluster, out float surfaceY, out float dist, out bool milling)
    {
        cluster = Vector3.zero;
        surfaceY = 1.25f;
        dist = 0f;
        milling = false;
        Prune();
        if (Pellets.Count == 0 || koi == null) return false;

        FeedPellet nearest = null;
        float best = float.MaxValue;
        Vector3 kp = koi.position;
        for (int i = 0; i < Pellets.Count; i++)
        {
            var p = Pellets[i];
            if (p == null) continue;
            float sq = XzSq(kp, p.transform.position);
            if (sq < best)
            {
                best = sq;
                nearest = p;
            }
        }
        if (nearest == null) return false;

        Vector3 sum = nearest.transform.position;
        int n = 1;
        for (int i = 0; i < Pellets.Count; i++)
        {
            var p = Pellets[i];
            if (p == null || p == nearest) continue;
            if (XzSq(p.transform.position, nearest.transform.position) > 0.35f * 0.35f) continue;
            sum += p.transform.position;
            n++;
        }

        cluster = sum / n;
        surfaceY = cluster.y;
        Vector3 delta = cluster - kp;
        delta.y = 0f;
        dist = delta.magnitude;
        milling = dist < 1.15f;
        return true;
    }

    public static Vector3 GatherSlot(Transform koi, Vector3 cluster)
    {
        var kois = GameObject.FindGameObjectsWithTag("Koi");
        int n = 0;
        int index = 0;
        int selfId = koi.GetInstanceID();
        for (int i = 0; i < kois.Length; i++)
        {
            var go = kois[i];
            if (go == null) continue;
            if (XzSq(go.transform.position, cluster) > 2.4f * 2.4f) continue;
            int id = go.GetInstanceID();
            if (id < selfId) index++;
            n++;
        }
        if (n < 1) n = 1;
        float radius = Mathf.Clamp(0.24f + n * 0.035f, 0.26f, 0.4f);
        float ang = (index / (float)n) * Mathf.PI * 2f;
        return cluster + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
    }

    public static FeedPellet ClosestPellet(Vector3 pos, float radius)
    {
        Prune();
        FeedPellet best = null;
        float bestSq = radius * radius;
        for (int i = 0; i < Pellets.Count; i++)
        {
            var p = Pellets[i];
            if (p == null) continue;
            float sq = XzSq(pos, p.transform.position);
            if (sq < bestSq)
            {
                bestSq = sq;
                best = p;
            }
        }
        return best;
    }

    static float XzSq(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }
}

public class FeedPellet : MonoBehaviour
{
    public float bites = 3f;
    float bob;

    void Awake()
    {
        bob = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        transform.position += Vector3.up * (Mathf.Sin(Time.time * 1.4f + bob) * 0.003f);
    }

    public bool Consume()
    {
        bites -= 1f;
        transform.localScale *= 0.9f;
        if (bites > 0f) return true;
        Destroy(gameObject);
        return true;
    }
}
