using UnityEngine;
using KoiPond;

/// <summary>
/// 池カスタムで選んだ浮かべるセットを水面に出す。なしなら何も出さない。
/// </summary>
public class LotusLeafSpawner : MonoBehaviour
{
    [SerializeField] int count = 4;
    [SerializeField] float minSize = 0.62f;
    [SerializeField] float maxSize = 0.82f;

    static readonly Vector4 WaveA = new Vector4(1f, 0.3f, 0.04f, 3f);
    static readonly Vector4 WaveB = new Vector4(0.4f, 0.9f, 0.03f, 2f);
    static readonly Vector4 WaveC = new Vector4(-0.7f, 0.5f, 0.02f, 1.2f);
    const float WaveSpeed = 1.5f;

    RippleSimulator sim;
    Transform[] leaves;
    Vector3[] rest;
    float[] yaw;
    float[] spin;
    float[] sizes;
    Vector2[] vel;
    Material leafMat;
    float halfW = 1.5f;
    float halfH = 1.0f;
    bool pinToEdge = true;
    float edgeBand = 0.92f;

    void Start()
    {
        sim = FindFirstObjectByType<RippleSimulator>();
        Spawn();
    }

    public void SetVisible(bool visible)
    {
        if (leaves == null) return;
        for (int i = 0; i < leaves.Length; i++)
        {
            if (leaves[i] != null) leaves[i].gameObject.SetActive(visible);
        }
    }

    void Spawn()
    {
        var src = Resources.Load<Material>("PondLeaf");
        if (src != null)
            leafMat = new Material(src);
        else
        {
            var shader = Shader.Find("KoiPond/Leaf");
            if (shader == null) return;
            leafMat = new Material(shader);
        }
        leafMat.SetFloat("_Cutoff", 0.25f);

        var set = PondCustomStore.FindFloat(PondCustomStore.Load().floatId);
        if (set == null || set.textures == null || set.textures.Length == 0)
            return;
        var textures = set.textures;
        float scale = SizeScale(set.id);
        pinToEdge = !IsAhiru(set.id);
        edgeBand = 0.92f;
        int spawnCount;
        if (IsAhiru(set.id))
            spawnCount = 1;
        else
        {
            spawnCount = Mathf.RoundToInt(count / Mathf.Max(0.2f, scale));
            if (set.id == "sakura") spawnCount = Mathf.Max(spawnCount, 40);
            if (IsKoyo(set.id)) spawnCount = Mathf.Max(spawnCount, 20);
            spawnCount = Mathf.Clamp(spawnCount, 3, 48);
        }

        float waterY = sim != null ? sim.waterY : 1.25f;
        var cam = Camera.main;
        if (cam != null)
        {
            float dist = Mathf.Abs(cam.transform.position.y - waterY);
            halfH = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            halfW = halfH * cam.aspect;
        }

        leaves = new Transform[spawnCount];
        rest = new Vector3[spawnCount];
        yaw = new float[spawnCount];
        spin = new float[spawnCount];
        sizes = new float[spawnCount];
        vel = new Vector2[spawnCount];

        for (int i = 0; i < spawnCount; i++)
        {
            float ang = (i + 0.5f) / spawnCount * Mathf.PI * 2f + 0.2f;
            float u = 1.08f;
            if (set.id == "sakura") u = 0.98f + (i % 4) * 0.07f;
            else if (IsKoyo(set.id)) u = 1.02f + (i % 3) * 0.08f;
            else if (IsAhiru(set.id))
            {
                ang = -0.55f;
                u = 0.42f;
            }
            float x = Mathf.Cos(ang) * halfW * u;
            float z = Mathf.Sin(ang) * halfH * u;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "LotusLeaf";
            go.layer = 6;
            Object.Destroy(go.GetComponent<Collider>());
            var rend = go.GetComponent<Renderer>();
            var mat = new Material(leafMat);
            var tex = textures[i % textures.Length];
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            float size = Mathf.Lerp(minSize, maxSize, (i * 0.53f) % 1f) * scale;
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(size, size, size);

            rest[i] = new Vector3(x, waterY, z);
            yaw[i] = IsAhiru(set.id) ? 20f : i * 47f;
            spin[i] = IsAhiru(set.id) ? 0.6f : (i % 2 == 0 ? 4f : -5f);
            sizes[i] = size;
            leaves[i] = go.transform;
        }
    }

    void LateUpdate()
    {
        if (leaves == null) return;
        float waterY = sim != null ? sim.waterY : 1.25f;
        float t = Time.time;
        float dt = Time.deltaTime;
        var current = new Vector2(1f, 0.3f).normalized * 0.012f;
        float waveT = t * 0.45f;

        for (int i = 0; i < leaves.Length; i++)
        {
            if (leaves[i] == null) continue;

            var drift = current;
            drift.x += Mathf.Sin(t * 0.12f + i * 1.7f) * 0.006f;
            drift.y += Mathf.Cos(t * 0.1f + i * 1.1f) * 0.005f;

            var p = rest[i];
            var push = sim != null ? sim.SampleRipplePush(p) : Vector2.zero;
            vel[i] += push * 2.4f * dt;
            vel[i] *= Mathf.Exp(-1.6f * dt);
            p.x += (drift.x + vel[i].x) * dt;
            p.z += (drift.y + vel[i].y) * dt;

            float limX = halfW * 1.15f;
            float limZ = halfH * 1.15f;
            p.x = Mathf.Clamp(p.x, -limX, limX);
            p.z = Mathf.Clamp(p.z, -limZ, limZ);
            var ring = new Vector2(limX > 0.001f ? p.x / limX : 0f, limZ > 0.001f ? p.z / limZ : 0f);
            float r = ring.magnitude;
            if (pinToEdge && r < edgeBand && r > 0.001f)
            {
                ring = ring.normalized * edgeBand;
                p.x = ring.x * limX;
                p.z = ring.y * limZ;
            }
            rest[i] = p;

            var wave = WaveOffset(p, waveT);
            var n = Vector3.Slerp(Vector3.up, SampleNormal(p, waveT, waterY), 0.08f);
            float y = waterY + wave.y * 0.35f + Ripple(p) + sizes[i] * 0.2f + 0.02f;
            yaw[i] += (spin[i] * 0.08f + drift.x * 2f + vel[i].x * 8f) * dt;
            leaves[i].position = new Vector3(p.x + wave.x, y, p.z + wave.z);
            leaves[i].rotation = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(-90f, yaw[i], 0f);
        }
    }

    static bool IsAhiru(string id)
    {
        return id == "ahiru" || id == "duck";
    }

    static bool IsKoyo(string id)
    {
        return id == "koyo" || id == "ityo" || id == "ichou" || id == "icho";
    }

    static float SizeScale(string id)
    {
        if (id == "sakura") return 0.22f;
        if (IsKoyo(id)) return 0.48f;
        if (IsAhiru(id)) return 0.85f;
        return 1f;
    }

    Vector3 WaveOffset(Vector3 p, float t)
    {
        return Gerstner(WaveA, p, t) + Gerstner(WaveB, p, t) + Gerstner(WaveC, p, t);
    }

    float Ripple(Vector3 p)
    {
        return sim != null ? sim.SampleRippleHeight(p) * 0.12f : 0f;
    }

    float SampleHeight(Vector3 p, float t, float waterY)
    {
        return waterY + WaveOffset(p, t).y + Ripple(p) + 0.02f;
    }

    Vector3 SampleNormal(Vector3 p, float t, float waterY)
    {
        const float e = 0.08f;
        float h = SampleHeight(p, t, waterY);
        float hx = SampleHeight(p + new Vector3(e, 0f, 0f), t, waterY);
        float hz = SampleHeight(p + new Vector3(0f, 0f, e), t, waterY);
        var n = Vector3.Cross(new Vector3(0f, hz - h, e), new Vector3(e, hx - h, 0f));
        if (n.sqrMagnitude < 0.0001f) return Vector3.up;
        return n.normalized;
    }

    static Vector3 Gerstner(Vector4 wave, Vector3 p, float t)
    {
        float steepness = wave.z;
        float wavelength = Mathf.Max(wave.w, 0.001f);
        float k = 2f * Mathf.PI / wavelength;
        float c = Mathf.Sqrt(9.8f / k);
        var d = new Vector2(wave.x, wave.y);
        if (d.sqrMagnitude > 0.0001f) d.Normalize();
        float f = k * (d.x * p.x + d.y * p.z - c * t * WaveSpeed);
        float a = steepness / k;
        float cosF = Mathf.Cos(f);
        return new Vector3(d.x * a * cosF, a * Mathf.Sin(f), d.y * a * cosF);
    }
}
