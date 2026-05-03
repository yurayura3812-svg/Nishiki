using System.Collections.Generic;
using UnityEngine;

namespace KoiPond
{
    /// <summary>
    /// 波紋シミュレーションマネージャ。
    /// ・RenderTexture を 2 枚使った ping-pong で離散波動方程式を毎フレーム伝播。
    /// ・鯉などから AddImpulse() を呼ぶと、その位置の高さフィールドに加算され波が広がる。
    /// ・水面シェーダー (KoiPond/Water) に _RippleTex / _RippleAreaCenter / _RippleAreaSize を供給。
    ///
    /// 配置: 池の中央（XZ）に置き、areaSize に池サイズを設定する。
    /// </summary>
    [ExecuteAlways]
    public class RippleSimulator : MonoBehaviour
    {
        [Header("Simulation Area (world XZ)")]
        [Tooltip("シミュレーション領域の一辺の長さ（ワールド単位）。池より少し大きめに設定。")]
        public float areaSize = 20f;

        [Tooltip("RTの解像度。256~1024。高いほど鮮明だが負荷増。")]
        public int resolution = 512;

        [Header("Sim Parameters")]
        [Range(0.9f, 0.999f)] public float damping = 0.985f;
        [Range(0.1f, 0.5f)]   public float speed   = 0.35f;
        [Tooltip("1秒あたりのシミュレーションステップ数。固定で安定化。")]
        public int simStepsPerSecond = 60;

        [Header("References")]
        public Material rippleMaterial;          // KoiPond/RippleSim をアサイン
        public Material waterMaterial;           // KoiPond/Water をアサイン（_RippleTex 自動セット）

        [Header("Debug")]
        public bool clickToSplash = false;       // エディタ確認用
        public float clickStrength = 0.6f;
        public float clickRadiusUV = 0.02f;
        [Tooltip("Click To Splash 用の水面 Y 座標。RippleSimulator の位置と水面の高さが違う場合に使う")]
        public float waterY = 0f;

        // --- ランタイム ---
        RenderTexture _rtA, _rtB;
        bool _readFromA = true;
        float _accum;

        // 1フレーム内に貯めるインパルス（最大4個まで一度にディスパッチ）
        struct Impulse { public Vector2 uv; public float radiusUV; public float strength; }
        readonly List<Impulse> _pendingImpulses = new List<Impulse>(16);

        // Material property IDs
        static readonly int ID_RippleTex          = Shader.PropertyToID("_RippleTex");
        static readonly int ID_RippleAreaCenter   = Shader.PropertyToID("_RippleAreaCenter");
        static readonly int ID_RippleAreaSize     = Shader.PropertyToID("_RippleAreaSize");
        static readonly int ID_Damping            = Shader.PropertyToID("_Damping");
        static readonly int ID_Speed              = Shader.PropertyToID("_Speed");
        static readonly int ID_TexelSize          = Shader.PropertyToID("_TexelSize");
        static readonly int ID_ImpulsePos         = Shader.PropertyToID("_ImpulsePos");
        static readonly int ID_ImpulsePos1        = Shader.PropertyToID("_ImpulsePos1");
        static readonly int ID_ImpulsePos2        = Shader.PropertyToID("_ImpulsePos2");
        static readonly int ID_ImpulsePos3        = Shader.PropertyToID("_ImpulsePos3");

        void OnEnable()  { Allocate(); PushAreaToWater(); }
        void OnDisable() { Release(); }
        void OnValidate(){ if (Application.isPlaying || !isActiveAndEnabled) return; Allocate(); PushAreaToWater(); }

        void Allocate()
        {
            Release();
            var desc = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.ARGBHalf, 0)
            {
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false,
            };
            _rtA = new RenderTexture(desc) { name = "RippleA", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _rtB = new RenderTexture(desc) { name = "RippleB", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _rtA.Create(); _rtB.Create();
            ClearRT(_rtA); ClearRT(_rtB);
        }

        void Release()
        {
            if (_rtA) { _rtA.Release(); DestroyImmediate(_rtA); _rtA = null; }
            if (_rtB) { _rtB.Release(); DestroyImmediate(_rtB); _rtB = null; }
        }

        static void ClearRT(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            // R=0(高さ0), G=0(前フレーム高さ0), B=0.5(勾配X中立), A=0.5(勾配Z中立)
            // 勾配は -1..1 を 0..1 にパックしているので 0.5 が中立値。
            // 0 でクリアすると初期フレームで -1 として読まれて「ボインッ」と暴れる。
            GL.Clear(true, true, new Color(0f, 0f, 0.5f, 0.5f));
            RenderTexture.active = prev;
        }

        void PushAreaToWater()
        {
            if (waterMaterial == null) return;
            var c = transform.position;
            waterMaterial.SetVector(ID_RippleAreaCenter, new Vector4(c.x, c.y, c.z, 0));
            waterMaterial.SetFloat(ID_RippleAreaSize, areaSize);
        }

        /// <summary>
        /// 鯉などから呼ぶ：ワールド座標 worldPos の位置に半径 worldRadius、強度 strength の波紋インパルスを追加。
        /// strength は + で水を盛り上げる、- で凹ませる（押し退け表現）。
        /// </summary>
        public void AddImpulseWorld(Vector3 worldPos, float worldRadius, float strength)
        {
            // ワールド -> エリア相対 -> UV 0..1
            Vector3 local = worldPos - transform.position;
            float u = local.x / areaSize + 0.5f;
            float v = local.z / areaSize + 0.5f;
            if (u < 0 || u > 1 || v < 0 || v > 1) return;

            float radiusUV = Mathf.Max(0.001f, worldRadius / areaSize);
            _pendingImpulses.Add(new Impulse { uv = new Vector2(u, v), radiusUV = radiusUV, strength = strength });
        }

        void Update()
        {
            if (waterMaterial == null || rippleMaterial == null || _rtA == null) return;

            PushAreaToWater();

            // 固定タイムステップでステップ数を決定
            _accum += Time.deltaTime;
            float dt = 1f / simStepsPerSecond;
            int steps = Mathf.Clamp(Mathf.FloorToInt(_accum / dt), 0, 4);
            _accum -= steps * dt;

            // デバッグ用クリック
            if (clickToSplash && Input.GetMouseButtonDown(0))
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Ray r = cam.ScreenPointToRay(Input.mousePosition);
                    // 水面 Y にあるプレーンとレイを交差させる
                    Plane plane = new Plane(Vector3.up, new Vector3(0, waterY, 0));
                    if (plane.Raycast(r, out float enter))
                    {
                        AddImpulseWorld(r.GetPoint(enter), areaSize * clickRadiusUV, clickStrength);
                    }
                }
            }

            for (int i = 0; i < steps; i++)
            {
                StepOnce();
            }

            // 水シェーダーに最新の表面状態を渡す
            waterMaterial.SetTexture(ID_RippleTex, _readFromA ? _rtA : _rtB);
        }

        void StepOnce()
        {
            RenderTexture src = _readFromA ? _rtA : _rtB;
            RenderTexture dst = _readFromA ? _rtB : _rtA;

            // --- Pass 0: 伝播 ---
            rippleMaterial.SetFloat(ID_Damping, damping);
            rippleMaterial.SetFloat(ID_Speed,   speed);
            rippleMaterial.SetFloat(ID_TexelSize, 1f / resolution);
            Graphics.Blit(src, dst, rippleMaterial, 0);

            // --- Pass 1: インパルス加算（dst に直接加算する） ---
            // 注意: Graphics.Blit(dst, dst, ...) は src と dst が同じになり警告が出るため、
            //       RenderTexture.active 経由で直接描画する。
            if (_pendingImpulses.Count > 0)
            {
                int n = _pendingImpulses.Count;
                Vector4 P(int idx) => idx < n
                    ? new Vector4(_pendingImpulses[idx].uv.x, _pendingImpulses[idx].uv.y,
                                  _pendingImpulses[idx].radiusUV, _pendingImpulses[idx].strength)
                    : Vector4.zero;

                // 4個ずつ消化
                int consumed = 0;
                var prevActive = RenderTexture.active;
                RenderTexture.active = dst;
                while (consumed < n)
                {
                    rippleMaterial.SetVector(ID_ImpulsePos,  P(consumed + 0));
                    rippleMaterial.SetVector(ID_ImpulsePos1, P(consumed + 1));
                    rippleMaterial.SetVector(ID_ImpulsePos2, P(consumed + 2));
                    rippleMaterial.SetVector(ID_ImpulsePos3, P(consumed + 3));

                    // フルスクリーンクワッドで Pass 1 を直接描画（ColorMask R + Blend One One で加算される）
                    rippleMaterial.SetPass(1);
                    GL.PushMatrix();
                    GL.LoadOrtho();
                    GL.Begin(GL.QUADS);
                    GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
                    GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
                    GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
                    GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
                    GL.End();
                    GL.PopMatrix();

                    consumed += 4;
                }
                RenderTexture.active = prevActive;
                _pendingImpulses.Clear();
            }

            _readFromA = !_readFromA;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireCube(transform.position, new Vector3(areaSize, 0.01f, areaSize));
        }
    }
}
