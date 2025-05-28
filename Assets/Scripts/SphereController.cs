using UnityEngine;

public class SphereController : MonoBehaviour
{
    public void Awake()
    {
        var diameter = Utils.r * 2f;
        transform.localScale = new Vector3(diameter, diameter, diameter);   
    }
}