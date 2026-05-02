#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KoiPond.EditorTools
{
    /// <summary>
    /// Foam Noise / Caustics / Water Normal Map をエディタ内で生成するツール。
    /// メニュー: Tools > KoiPond > Generate Water Textures
    /// </summary>
    public class NoiseTextureGenerator : EditorWindow
    {
        enum OutputType { FoamNoise, CausticsProxy, WaterNormalMap }

        OutputType _output = OutputType.WaterNormalMap;
        int _resolution = 512;
        float _scale = 6f;
        float _contrast = 1.2f;
        float _normalStrength = 1.5f;
        bool _invert = false;
        string _fileName = "T_WaterNormal";

        [MenuItem("Tools/KoiPond/Generate Water Textures")]
        static void Open() => GetWindow<NoiseTextureGenerator>("Water Texture Generator");

        void OnGUI()
        {
            EditorGUILayout.LabelField("水面シェーダー用テクスチャ生成", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _output = (OutputType)EditorGUILayout.EnumPopup("Output Type", _output);
            _resolution = EditorGUILayout.IntPopup("Resolution",
                _resolution,
                new[] { "256", "512", "1024" },
                new[] { 256, 512, 1024 });

            EditorGUILayout.Space();
            switch (_output)
            {
                case OutputType.FoamNoise:
                    if (string.IsNullOrEmpty(_fileName) || _fileName.StartsWith("T_Water") || _fileName.StartsWith("T_Caustics"))
                        _fileName = "T_FoamNoise";
                    _scale    = EditorGUILayout.Slider("Scale (細かさ)", _scale, 2f, 32f);
                    _contrast = EditorGUILayout.Slider("Contrast", _contrast, 0.5f, 3f);
                    _invert   = EditorGUILayout.Toggle("Invert", _invert);
                    EditorGUILayout.HelpBox("おすすめ: Scale 8 / Contrast 1.2", MessageType.Info);
                    break;
                case OutputType.CausticsProxy:
                    if (string.IsNullOrEmpty(_fileName) || _fileName.StartsWith("T_Water") || _fileName.StartsWith("T_Foam"))
                        _fileName = "T_Caustics_Proxy";
                    _scale    = EditorGUILayout.Slider("Scale (細かさ)", _scale, 2f, 32f);
                    _contrast = EditorGUILayout.Slider("Contrast", _contrast, 0.5f, 3f);
                    _invert   = EditorGUILayout.Toggle("Invert", _invert);
                    EditorGUILayout.HelpBox("おすすめ: Scale 6 / Contrast 2.0 / Invert ON", MessageType.Info);
                    break;
                case OutputType.WaterNormalMap:
                    if (string.IsNullOrEmpty(_fileName) || _fileName.StartsWith("T_Foam") || _fileName.StartsWith("T_Caustics"))
                        _fileName = "T_WaterNormal";
                    _scale          = EditorGUILayout.Slider("Wave Scale", _scale, 2f, 16f);
                    _normalStrength = EditorGUILayout.Slider("Normal Strength", _normalStrength, 0.3f, 4f);
                    EditorGUILayout.HelpBox("おすすめ: Scale 6 / Normal Strength 1.5\n2枚生成して Normal Map A/B に使うと見栄え向上(File Name を変えて Generate を 2 回)", MessageType.Info);
                    break;
            }

            _fileName = EditorGUILayout.TextField("File Name", _fileName);

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate & Save", GUILayout.Height(36)))
            {
                Generate();
            }
        }

        void Generate()
        {
            int res = _resolution;
            string folder = "Assets/Textures/Water";
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            string path = $"{folder}/{_fileName}.png";

            byte[] png;
            bool isNormal = _output == OutputType.WaterNormalMap;

            if (isNormal)
            {
                // 異なる Normal Map を 2 枚作りたい場合のために、ファイル名のハッシュで seed を変える
                int seed = _fileName.GetHashCode();
                png = GenerateWaterNormalMap(res, _scale, _normalStrength, seed);
            }
            else
            {
                png = GenerateGrayscaleNoise(res, _output, _scale, _contrast, _invert);
            }

            File.WriteAllBytes(path, png);
            AssetDatabase.Refresh();

            // Auto-set import settings
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                if (isNormal)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.mipmapEnabled = true;
                }
                else
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.mipmapEnabled = true;
                }
                importer.SaveAndReimport();
            }

            Debug.Log($"[KoiPond] Saved {_output} to {path}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ============================================================
        //   Water Normal Map (multi-octave seamless noise -> normals)
        // ============================================================
        static byte[] GenerateWaterNormalMap(int res, float scale, float strength, int seed)
        {
            // ファイル名から決まるオフセットでバリエーションを出す
            float seedOffsetX = ((seed & 0xFFFF) / 65535f) * 100f;
            float seedOffsetY = (((seed >> 16) & 0xFFFF) / 65535f) * 100f;

            // 1) 高さフィールド（4 オクターブのシームレス Perlin の合成）
            float[,] h = new float[res, res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / res;
                    float v = (float)y / res;
                    float val = 0f;
                    float amp = 1f;
                    float ampSum = 0f;
                    float s = scale;
                    for (int o = 0; o < 4; o++)
                    {
                        val += SeamlessPerlin(u, v, s, seedOffsetX + o * 17f, seedOffsetY + o * 13f) * amp;
                        ampSum += amp;
                        amp *= 0.5f;
                        s *= 2f;
                    }
                    h[x, y] = val / ampSum;
                }
            }

            // 2) 中心差分で法線を計算（境界はラップして継ぎ目なし）
            var tex = new Texture2D(res, res, TextureFormat.RGB24, true, true);
            var pixels = new Color32[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int xl = (x - 1 + res) % res;
                    int xr = (x + 1) % res;
                    int yd = (y - 1 + res) % res;
                    int yu = (y + 1) % res;

                    float dx = (h[xr, y] - h[xl, y]) * strength * res * 0.005f;
                    float dy = (h[x, yu] - h[x, yd]) * strength * res * 0.005f;

                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;

                    byte r = (byte)((n.x * 0.5f + 0.5f) * 255f);
                    byte g = (byte)((n.y * 0.5f + 0.5f) * 255f);
                    byte b = (byte)((n.z * 0.5f + 0.5f) * 255f);
                    pixels[y * res + x] = new Color32(r, g, b, 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            DestroyImmediate(tex);
            return bytes;
        }

        // ============================================================
        //   Grayscale noise (Foam / Caustics)
        // ============================================================
        static byte[] GenerateGrayscaleNoise(int res, OutputType type, float scale, float contrast, bool invert)
        {
            var tex = new Texture2D(res, res, TextureFormat.R8, true, true);
            var pixels = new Color32[res * res];

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / res;
                    float v = (float)y / res;
                    float n = 0f;

                    if (type == OutputType.FoamNoise)
                        n = SeamlessVoronoi(u, v, scale, false);
                    else
                        n = SeamlessVoronoi(u, v, scale, true);

                    n = Mathf.Clamp01((n - 0.5f) * contrast + 0.5f);
                    if (invert) n = 1f - n;

                    byte g = (byte)(n * 255f);
                    pixels[y * res + x] = new Color32(g, g, g, 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            DestroyImmediate(tex);
            return bytes;
        }

        // ============================================================
        //   Seamless noise primitives
        // ============================================================
        static float SeamlessVoronoi(float u, float v, float scale, bool worley)
        {
            float fx = u * scale;
            float fy = v * scale;
            int   ix = Mathf.FloorToInt(fx);
            int   iy = Mathf.FloorToInt(fy);
            float dx = fx - ix;
            float dy = fy - iy;
            int s = Mathf.RoundToInt(scale);
            if (s < 1) s = 1;

            float minD = 10f;
            float secondD = 10f;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int cx = ((ix + ox) % s + s) % s;
                int cy = ((iy + oy) % s + s) % s;
                Vector2 p = Hash2(cx, cy);
                float ddx = (ox + p.x) - dx;
                float ddy = (oy + p.y) - dy;
                float d = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                if (d < minD) { secondD = minD; minD = d; }
                else if (d < secondD) { secondD = d; }
            }
            return worley ? Mathf.Clamp01(secondD - minD) : Mathf.Clamp01(1f - minD);
        }

        static Vector2 Hash2(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            float a = ((h & 0xFFFF) / 65535f);
            float b = (((h >> 16) & 0xFFFF) / 65535f);
            return new Vector2(a, b);
        }

        // タイル可能な Perlin Noise（双線形ブレンドで境界の継ぎ目を消す）
        static float SeamlessPerlin(float u, float v, float scale, float offX, float offY)
        {
            float n00 = Mathf.PerlinNoise(u * scale + offX,        v * scale + offY);
            float n10 = Mathf.PerlinNoise((u - 1f) * scale + offX, v * scale + offY);
            float n01 = Mathf.PerlinNoise(u * scale + offX,        (v - 1f) * scale + offY);
            float n11 = Mathf.PerlinNoise((u - 1f) * scale + offX, (v - 1f) * scale + offY);

            float a = Mathf.Lerp(n00, n10, u);
            float b = Mathf.Lerp(n01, n11, u);
            return Mathf.Lerp(a, b, v);
        }
    }
}
#endif
