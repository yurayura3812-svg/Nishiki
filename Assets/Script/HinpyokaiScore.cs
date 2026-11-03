using UnityEngine;

public class HinpyokaiScore
{
    public int pattern;
    public int body;
    public int gloss;
    public int total;

    /// <summary>
    /// 暫定配点。艶データがまだ無いので 70 を仮置きする。
    /// 模様は量とディテール、体型は bellyLimit から出す。
    /// </summary>
    public static HinpyokaiScore From(KoiPatternData data)
    {
        var s = new HinpyokaiScore();
        if (data == null)
        {
            s.pattern = 50;
            s.body = 50;
            s.gloss = 70;
        }
        else
        {
            float amount = (data.sub1Amount + data.sub2Amount) * 0.5f;
            float detail = (data.sub1Detail + data.sub2Detail) * 0.5f;
            s.pattern = ClampScore(50f + amount * 30f + detail * 20f);
            s.body = ClampScore(82f + data.bellyLimit * 40f);
            s.gloss = 70;
        }
        s.total = Mathf.Clamp(Mathf.RoundToInt(s.pattern * 0.4f + s.body * 0.3f + s.gloss * 0.3f), 1, 100);
        return s;
    }

    static int ClampScore(float v)
    {
        return Mathf.Clamp(Mathf.RoundToInt(v), 1, 100);
    }
}
