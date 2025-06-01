
using UnityEngine;

public class PortraitViewerFlagController : MonoBehaviour, IColliderRootProvider
{
    public GameObject root;
    public GameObject GetRoot() => root;
}