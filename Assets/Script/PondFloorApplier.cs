using UnityEngine;

public class PondFloorApplier : MonoBehaviour
{
    [SerializeField] Renderer floorRenderer;

    void Start()
    {
        if (floorRenderer == null)
        {
            var plane = GameObject.Find("Plane");
            if (plane != null) floorRenderer = plane.GetComponent<Renderer>();
        }

        PondCustomStore.ApplyFloor(floorRenderer);
    }
}
