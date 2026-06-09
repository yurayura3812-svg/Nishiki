using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KoiPond
{
    /// <summary>
    /// 水面屈折用カメラのセットアップ。
    ///
    /// 仕組み:
    ///   - メインカメラの子に「屈折用カメラ」を自動生成
    ///   - 屈折用カメラはメインカメラより少し広い視野で撮影
    ///   - 撮影結果を RenderTexture に書き、グローバル Shader プロパティ _RefractionTex で
    ///     水面シェーダーに渡す
    ///   - メインカメラの設定（FOV や Orthographic Size）を毎フレーム同期
    ///
    /// 配置:
    ///   1. Main Camera にこのスクリプトを AddComponent
    ///   2. Inspector で WaterMaterial に M_KoiPondWater をアサイン（任意）
    ///   3. View Expansion はデフォルト 1.25 で OK（メインの 25% 広く撮る）
    ///
    /// 注意:
    ///   - 屈折用カメラは「水面（_WaterLayer）を含まないレイヤー」だけ撮影する
    ///     ので、必ず水面メッシュを別レイヤーに置くこと（デフォルト "Water" レイヤー）
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public class RefractionCameraSetup : MonoBehaviour
    {
        [Header("Refraction RT")]
        [Tooltip("RT の解像度倍率（1.0 = 画面と同じ解像度）")]
        [Range(0.25f, 1.0f)] public float resolutionScale = 0.75f;

        [Tooltip("メインカメラより何倍広く撮影するか。1.25 = 25% 広く撮る")]
        [Range(1.0f, 2.0f)] public float viewExpansion = 1.25f;

        [Header("Layers")]
        [Tooltip("屈折カメラに撮影させない（=水面に映してもおかしくないものから除外する）レイヤー。" +
                 "通常は Water レイヤー(=水面メッシュが置かれているレイヤー)を指定")]
        public LayerMask excludeFromRefraction = 0;

        [Header("Optional")]
        [Tooltip("水面マテリアル。指定するとその _RefractionTex に直接 RT をセットする")]
        public Material waterMaterial;

        // --- ランタイム ---
        Camera _mainCam;
        Camera _refractionCam;
        RenderTexture _rt;
        Vector2Int _lastResolution;

        static readonly int ID_RefractionTex = Shader.PropertyToID("_RefractionTex");
        static readonly int ID_RefractionTexParams = Shader.PropertyToID("_RefractionTexParams");

        void OnEnable()
        {
            _mainCam = GetComponent<Camera>();
            EnsureRefractionCamera();
        }

        void OnDisable()
        {
            if (_refractionCam != null)
            {
                if (Application.isPlaying) Destroy(_refractionCam.gameObject);
                else DestroyImmediate(_refractionCam.gameObject);
                _refractionCam = null;
            }
            ReleaseRT();
        }

        void EnsureRefractionCamera()
        {
            if (_refractionCam != null) return;

            var go = new GameObject("~RefractionCamera");
            go.hideFlags = HideFlags.DontSave; // シーン保存に含めない
            go.transform.SetParent(transform, false);

            _refractionCam = go.AddComponent<Camera>();
            _refractionCam.enabled = false; // 手動で Render() するので自動描画は OFF

            // URP 用カメラデータ
            var addData = go.AddComponent<UniversalAdditionalCameraData>();
            addData.renderType = CameraRenderType.Base;
            addData.renderPostProcessing = false; // 屈折用なのでポスプロは不要
            addData.requiresColorOption = CameraOverrideOption.Off;
            addData.requiresDepthOption = CameraOverrideOption.Off;
        }

        void EnsureRT(int width, int height)
        {
            if (_rt != null && _lastResolution.x == width && _lastResolution.y == height)
                return;

            ReleaseRT();
            _rt = new RenderTexture(width, height, 16, RenderTextureFormat.DefaultHDR)
            {
                name = "WaterRefractionRT",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false,
            };
            _rt.Create();
            _lastResolution = new Vector2Int(width, height);
        }

        void ReleaseRT()
        {
            if (_rt != null)
            {
                _rt.Release();
                if (Application.isPlaying) Destroy(_rt);
                else DestroyImmediate(_rt);
                _rt = null;
            }
        }

        void LateUpdate()
        {
            if (_mainCam == null) _mainCam = GetComponent<Camera>();
            if (_mainCam == null) return;
            if (_refractionCam == null) EnsureRefractionCamera();

            // ---- 解像度を決定 ----
            int w = Mathf.Max(64, Mathf.RoundToInt(_mainCam.pixelWidth  * resolutionScale));
            int h = Mathf.Max(64, Mathf.RoundToInt(_mainCam.pixelHeight * resolutionScale));
            EnsureRT(w, h);

            // ---- メインカメラの設定をコピー（広めに） ----
            _refractionCam.transform.localPosition = Vector3.zero;
            _refractionCam.transform.localRotation = Quaternion.identity;

            _refractionCam.clearFlags        = _mainCam.clearFlags;
            _refractionCam.backgroundColor   = _mainCam.backgroundColor;
            _refractionCam.nearClipPlane     = _mainCam.nearClipPlane;
            _refractionCam.farClipPlane      = _mainCam.farClipPlane;
            _refractionCam.orthographic      = _mainCam.orthographic;
            _refractionCam.cullingMask       = _mainCam.cullingMask & ~excludeFromRefraction.value;

            // Aspect ratio を明示的に同期 (Play 切り替え時にメインカメラの aspect が変わって
            //  屈折カメラと食い違うと、画面の片側だけ屈折が破綻するため)
            _refractionCam.aspect = _mainCam.aspect;

            if (_mainCam.orthographic)
            {
                _refractionCam.orthographicSize = _mainCam.orthographicSize * viewExpansion;
            }
            else
            {
                // Perspective: FOV を広げる
                float halfFovRad = _mainCam.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float halfTan = Mathf.Tan(halfFovRad) * viewExpansion;
                _refractionCam.fieldOfView = Mathf.Atan(halfTan) * 2f * Mathf.Rad2Deg;
            }

            // ---- 撮影 ----
            _refractionCam.targetTexture = _rt;
            _refractionCam.Render();

            // ---- グローバル Shader プロパティとマテリアルにセット ----
            // メインカメラの視野中心が RT のどこに来るかを示すパラメータも一緒に渡す。
            // 視野倍率が viewExpansion なので、メイン視野は RT の中央 (1/viewExpansion) の領域に映る。
            // x = メイン視野が RT 内で占める割合(0..1)、y も同じ、zw 予備
            float scale = 1.0f / viewExpansion;
            Vector4 texParams = new Vector4(scale, scale, viewExpansion, viewExpansion);

            Shader.SetGlobalTexture(ID_RefractionTex, _rt);
            Shader.SetGlobalVector(ID_RefractionTexParams, texParams);
            if (waterMaterial != null)
            {
                waterMaterial.SetTexture(ID_RefractionTex, _rt);
                waterMaterial.SetVector(ID_RefractionTexParams, texParams);
            }
        }
    }
}