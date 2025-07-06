using UnityEngine;

public class RootGiver : MonoBehaviour, IColliderRootProvider
{
    public GameObject root;
    public GameObject GetRoot() => root;
}