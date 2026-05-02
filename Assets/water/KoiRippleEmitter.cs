using UnityEngine;

namespace KoiPond
{
    /// <summary>
    /// 鯉に貼り付けるコンポーネント。鯉の動きに応じて RippleSimulator にインパルスを送る。
    /// ・速度ベースの押し退け（船首波）
    /// ・尾びれの振り（Tail bone があれば）による交互パルス
    /// ・水面ぎりぎりや跳ね上げ時の強い波紋
    ///
    /// 配置: 鯉の Root に AddComponent。
    ///
    /// 鯉がプレハブの場合、Inspector で Simulator を設定する代わりに、
    /// 起動時に自動的にシーン内の RippleSimulator を見つけて参照を設定する。
    /// </summary>
    [DisallowMultipleComponent]
    public class KoiRippleEmitter : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("プレハブで使う場合は空のままでOK。Start時に自動でシーン内の RippleSimulator を探して使う。")]
        public RippleSimulator simulator;
        [Tooltip("尾びれの根本ボーン（任意）。指定すると尾の振りに応じた波を出す。")]
        public Transform tailBone;
        [Tooltip("頭の Transform（任意）。船首波の発生位置に使う。未指定なら自身の位置。")]
        public Transform headPoint;

        [Header("Water Plane")]
        [Tooltip("水面の Y 座標（ワールド）。これより下は水中扱いで波紋は弱め。")]
        public float waterY = 0f;
        [Tooltip("水面からの距離がこの値以下なら波紋を出す。")]
        public float emitMaxDepth = 0.4f;

        [Header("Bow Wave (速度連動)")]
        [Tooltip("速度がこのしきい値以上で船首波が出る。")]
        public float minSpeedForBow = 0.3f;
        public float bowStrengthMul = 0.04f;
        public float bowRadius = 0.35f;

        [Header("Tail Wake (尾びれ連動)")]
        public float tailStrengthMul = 0.08f;
        public float tailRadius = 0.25f;
        [Tooltip("尾の位置変化を検知する平滑化の強さ")]
        public float tailVelDamping = 8f;

        [Header("Splash (跳ね上げ)")]
        public float splashYThreshold = 0.05f;   // 水面より上に出た時
        public float splashStrength = 0.9f;
        public float splashRadius = 0.5f;
        public float splashCooldown = 0.4f;

        [Header("Body Displacement (常時の押し退け)")]
        [Tooltip("常時、体の中央にわずかな凹み/膨らみを作る（鯉のシルエット周りの水面の盛り上がり）")]
        public bool emitBodyDisplacement = true;
        public float bodyStrength = -0.015f; // マイナスで凹み
        public float bodyRadius = 0.4f;

        // --- ランタイム ---
        Vector3 _prevPos;
        Vector3 _prevTailLocal;
        Vector3 _smoothedTailVel;
        bool    _wasAboveWater;
        float   _splashCooldownLeft;
        int     _warmupFramesLeft; // 起動直後の不安定な数フレームをスキップ

        void Reset()
        {
            // Inspector で AddComponent した瞬間に呼ばれる。シーン上にあれば自動アサイン。
            simulator = FindFirstObjectByType<RippleSimulator>();
        }

        void Start()
        {
            // simulator が未設定（プレハブ等）なら、シーン内から探す
            if (simulator == null)
            {
                simulator = FindFirstObjectByType<RippleSimulator>();
                if (simulator == null)
                {
                    Debug.LogWarning(
                        $"[KoiRippleEmitter] {name}: シーン内に RippleSimulator が見つかりません。" +
                        "波紋は発生しません。", this);
                }
            }

            _prevPos = transform.position;
            if (tailBone != null) _prevTailLocal = tailBone.localPosition;
            // 最初の数フレームはインパルスを出さない（Animator や 物理の初期化で速度が暴れるため）
            _warmupFramesLeft = 5;
            _wasAboveWater = transform.position.y > waterY + splashYThreshold;
        }

        void LateUpdate()
        {
            if (simulator == null) return;

            float dt = Time.deltaTime;
            if (dt < 1e-5f) return;

            Vector3 pos = transform.position;

            // ウォームアップ中は位置だけ追跡してインパルスは出さない
            if (_warmupFramesLeft > 0)
            {
                _warmupFramesLeft--;
                _prevPos = pos;
                if (tailBone != null) _prevTailLocal = tailBone.localPosition;
                _wasAboveWater = pos.y > waterY + splashYThreshold;
                return;
            }

            Vector3 vel = (pos - _prevPos) / dt;
            _prevPos = pos;

            // 速度が異常な場合（テレポート等）はインパルスを出さない
            const float kMaxReasonableSpeed = 30f; // 30 m/s 以上は怪しい
            if (vel.magnitude > kMaxReasonableSpeed)
                vel = Vector3.zero;

            float depthBelowSurface = waterY - pos.y; // +なら水中
            bool nearSurface = depthBelowSurface > -0.05f && depthBelowSurface < emitMaxDepth;

            // ---- Splash (水面を出入り) ----
            bool aboveWater = pos.y > waterY + splashYThreshold;
            _splashCooldownLeft -= dt;
            if (aboveWater != _wasAboveWater && _splashCooldownLeft <= 0f)
            {
                Vector3 splashPos = new Vector3(pos.x, waterY, pos.z);
                simulator.AddImpulseWorld(splashPos, splashRadius, splashStrength);
                _splashCooldownLeft = splashCooldown;
            }
            _wasAboveWater = aboveWater;

            if (!nearSurface) return; // 深く潜ってる時は以下スキップ

            // ---- Bow wave (頭の前方に速度に応じた波) ----
            Vector3 horizVel = new Vector3(vel.x, 0, vel.z);
            float speed = horizVel.magnitude;
            if (speed > minSpeedForBow)
            {
                Vector3 head = headPoint ? headPoint.position : pos;
                Vector3 bowPos = new Vector3(head.x, waterY, head.z);
                float surfaceFalloff = 1f - Mathf.Clamp01(Mathf.Abs(depthBelowSurface) / emitMaxDepth);
                float strength = (speed - minSpeedForBow) * bowStrengthMul * surfaceFalloff;
                simulator.AddImpulseWorld(bowPos, bowRadius, strength);
            }

            // ---- Tail wake (尾びれの振り) ----
            if (tailBone != null)
            {
                Vector3 tailLocal = tailBone.localPosition;
                Vector3 tailLocalVel = (tailLocal - _prevTailLocal) / dt;
                _prevTailLocal = tailLocal;
                _smoothedTailVel = Vector3.Lerp(_smoothedTailVel, tailLocalVel, 1f - Mathf.Exp(-tailVelDamping * dt));

                // 横方向（local X）成分の振りを検知
                float swing = Mathf.Abs(_smoothedTailVel.x);
                if (swing > 0.05f)
                {
                    Vector3 tailWS = tailBone.position;
                    Vector3 tailPos = new Vector3(tailWS.x, waterY, tailWS.z);
                    float surfaceFalloff = 1f - Mathf.Clamp01(Mathf.Abs(depthBelowSurface) / emitMaxDepth);
                    // 振りの符号で凸凹交互に
                    float sign = Mathf.Sign(_smoothedTailVel.x);
                    simulator.AddImpulseWorld(tailPos, tailRadius, sign * swing * tailStrengthMul * surfaceFalloff);
                }
            }

            // ---- Body displacement (常時) ----
            if (emitBodyDisplacement)
            {
                float surfaceFalloff = 1f - Mathf.Clamp01(Mathf.Abs(depthBelowSurface) / emitMaxDepth);
                Vector3 bodyPos = new Vector3(pos.x, waterY, pos.z);
                // dt依存：常時注入するので非常に弱く
                simulator.AddImpulseWorld(bodyPos, bodyRadius, bodyStrength * surfaceFalloff * dt * 60f);
            }
        }
    }
}
