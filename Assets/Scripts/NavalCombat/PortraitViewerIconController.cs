
using UnityEngine;

public class PortraitViewerIconController : MonoBehaviour, IColliderRootProvider
{
    public GameObject root;
    public GameObject GetRoot() => root;
}