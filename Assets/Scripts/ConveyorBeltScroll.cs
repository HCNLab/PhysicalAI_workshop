using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ConveyorBeltScroll : MonoBehaviour
{
    public float scrollSpeed = 0.5f;
    
    [Header("Control")]
    public bool isRunning = true;  // Sync with ConveyorBeltMover
    
    private Renderer rend;
    private Vector2 offset = Vector2.zero;

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        if (!isRunning) return;  // Stop texture scroll when belt stops
        
        offset.x += scrollSpeed * Time.deltaTime;
        rend.material.SetTextureOffset("_BaseMap", offset);
    }
}
