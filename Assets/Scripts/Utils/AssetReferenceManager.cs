using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine;
using System;
using System.Collections.Generic;

public class AssetReferenceManager
{
    static AssetReferenceManager _instance = new();
    public static AssetReferenceManager Instance => _instance;

    public List<AssetReference> loadingAssetReferences = new();

    public void LoadTexture2D(AssetReference assetReference, Action<Texture2D> callback)
    {
        loadingAssetReferences.Add(assetReference);

        AsyncOperationHandle handle = assetReference.LoadAssetAsync<Texture2D>();
        handle.Completed += (AsyncOperationHandle obj) =>
        {
            if (obj.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"AssetReference {assetReference.RuntimeKey} Success to load.");
                callback(assetReference.Asset as Texture2D);

                loadingAssetReferences.Remove(assetReference);
            }
            else
            {
                Debug.LogError($"AssetReference {assetReference.RuntimeKey} failed to load.");
            }
        };

    }
}