using UnityEngine;

public class GazeColorChange : MonoBehaviour
{
    Renderer rend;

    public Color normalColor = Color.white;
    public Color gazeColor = Color.red;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        rend.material.color = normalColor;
    }

    public void OnGazeEnter()
    {
        rend.material.color = gazeColor;
    }

    public void OnGazeExit()
    {
        rend.material.color = normalColor;
    }

void Update()
{
    Ray ray = new Ray(Camera.main.transform.position,
                      Camera.main.transform.forward);

    Debug.DrawRay(ray.origin, ray.direction * 100, Color.red);

    RaycastHit hit;

    if (Physics.Raycast(ray, out hit, 100))
    {
        // Debug.Log("Hit: " + hit.collider.name);
    }
}
}