using System.Collections.Generic;
using UnityEngine;



[CreateAssetMenu(fileName = "TextureArrayGenerator", menuName = "Scriptable Objects/TextureArrayGenerator")]
public class TextureArrayGenerator : ScriptableObject
{
    public List<Texture2D> textures = new();

    // Generate the Texture2DArray from the texture list
    public Texture2DArray GenerateTextureArray()
    {
        if (textures == null || textures.Count == 0)
        {
            Debug.LogWarning("No textures to generate Texture2DArray", this);
            return null;
        }

        // Verify all textures have the same dimensions
        int width = textures[0].width;
        int height = textures[0].height;

        foreach (var tex in textures)
        {
            if (tex.width != width || tex.height != height)
            {
                Debug.LogError($"All textures must have the same dimensions. Found {tex.width}x{tex.height} but expected {width}x{height}", this);
                return null;
            }
        }

        int slices = textures.Count;
        TextureFormat format = TextureFormat.RGBA32;
        // TextureFormat format = TextureFormat.RGB24;
        bool mipChain = false;

        // Create the texture array and apply the parameters
        Texture2DArray textureArray = new Texture2DArray(width, height, slices, format, mipChain);


        // Copy each texture into the array
        for (int i = 0; i < textures.Count; i++)
        {
            Debug.Log($"Texture readable: {textures[i].isReadable}");
            Graphics.CopyTexture(textures[i], 0, 0, textureArray, i, 0);
        }

        textureArray.Apply(true);

        Debug.Log($"Successfully generated Texture2DArray with {textures.Count} textures", this);

        return textureArray;
    }

    public Texture2DArray GenerateTextureArray1PixelSample()
    {
        if (textures == null || textures.Count == 0)
        {
            Debug.LogWarning("No textures to generate Texture2DArray", this);
            return null;
        }

        // Verify all textures have the same dimensions
        int width = textures[0].width;
        int height = textures[0].height;

        foreach (var tex in textures)
        {
            if (tex.width != width || tex.height != height)
            {
                Debug.LogError($"All textures must have the same dimensions. Found {tex.width}x{tex.height} but expected {width}x{height}", this);
                return null;
            }
        }

        int slices = textures.Count;
        TextureFormat format = TextureFormat.RGBA32;
        bool mipChain = false;

        // Create the texture array and apply the parameters
        Texture2DArray textureArray = new Texture2DArray(1, 1, slices, format, mipChain);


        // Copy each texture into the array
        for (int i = 0; i < textures.Count; i++)
        {
            Debug.Log($"Texture readable: {textures[i].isReadable}");

            Texture2D newTexture = new Texture2D(1, 1);
            newTexture.SetPixel(0, 0, textures[i].GetPixel(width / 2, height / 2));
            newTexture.Apply();

            Graphics.CopyTexture(newTexture, 0, 0, textureArray, i, 0);
        }

        textureArray.Apply(true);

        Debug.Log($"Successfully generated Texture2DArray with {textures.Count} textures", this);

        return textureArray;
    }

}
