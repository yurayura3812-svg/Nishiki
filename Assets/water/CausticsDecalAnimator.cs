using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KoiPond
{
    /// <summary>
    /// Decal Projector の Transform を時間で動かしてCausticsをアニメーションさせる。
    ///
    /// 注意: URP の Decal Projector は、ランタイムでマテリアルの Texture Offset を変更しても
    /// 描画に反映されない仕様がある。なので、プロジェクター自体の位置を動かすことで
    /// 投影位置をずらし、結果的にCausticsを流して見せる。
    ///
    /// 加えてサイン波で揺らぎを入れ、水面の波で光が歪む見た目を再現する。
    ///
    /// 使い方:
    ///   1. シーンに Decal Projector を作る
    ///   2. その Decal Projector の GameObject にこのスクリプトを AddComponent
    /// </summary>
    [RequireComponent(typeof(DecalProjector))]
    [ExecuteAlways]
    public class CausticsDecalAnimator : MonoBehaviour
    {
        [Header("Scroll (XZ平面でのスクロール速度 m/s)")]
        public Vector2 scrollSpeed = new Vector2(0.3f, 0.2f);

        [Header("Wobble (うねり)")]
        [Tooltip("プロジェクターを揺らす振幅（メートル）")]
        [Range(0f, 2f)] public float wobbleStrength = 0.4f;
        [Tooltip("揺らぎの周波数")]
        [Range(0.1f, 5f)] public float wobbleFrequency = 1.2f;
        [Tooltip("揺らぎの速さ")]
        [Range(0f, 5f)] public float wobbleSpeed = 1.5f;

        DecalProjector _projector;
        Vector3 _basePosition;
        Vector3 _accumulatedScroll;
        float _wobbleTime;

        void OnEnable()
        {
            _projector = GetComponent<DecalProjector>();
            _basePosition = transform.position;
        }

        void OnDisable()
        {
            // 元の位置に戻す
            if (_projector != null)
                transform.position = _basePosition;
        }

        void Update()
        {
            if (_projector == null) return;

            // エディタ上では動かさない（Play中だけ動かす）。
            // [ExecuteAlways] にしているのは OnEnable/OnDisable をエディタ操作で走らせるため。
            if (!Application.isPlaying) return;

            float dt = Time.deltaTime;

            // スクロール量を蓄積
            _accumulatedScroll.x += scrollSpeed.x * dt;
            _accumulatedScroll.z += scrollSpeed.y * dt;

            // サイン波のうねり
            _wobbleTime += dt * wobbleSpeed;
            float wobbleX = Mathf.Sin(_wobbleTime * wobbleFrequency) * wobbleStrength;
            float wobbleZ = Mathf.Cos(_wobbleTime * wobbleFrequency * 1.3f) * wobbleStrength;

            // ベース位置 + スクロール + 揺らぎ
            transform.position = _basePosition
                + new Vector3(_accumulatedScroll.x + wobbleX, 0f, _accumulatedScroll.z + wobbleZ);
        }
    }
}
