using UnityEngine;

public class RippleManager : MonoBehaviour
{
    public Material simulationMaterial; 
    public RenderTexture rtA; 
    public RenderTexture rtB; 
    
    private bool isPrevA = true;

    void Update()
    {
        if (simulationMaterial == null || rtA == null || rtB == null) return;

        if (isPrevA)
        {
            simulationMaterial.SetTexture("_PrevTex", rtA); 
            Graphics.Blit(rtA, rtB, simulationMaterial);
            Shader.SetGlobalTexture("_TrailTex", rtB);
            isPrevA = false;
        }
        else
        {
            simulationMaterial.SetTexture("_PrevTex", rtB);
            Graphics.Blit(rtB, rtA, simulationMaterial);
            Shader.SetGlobalTexture("_TrailTex", rtA);
            isPrevA = true;
        }
    }
}