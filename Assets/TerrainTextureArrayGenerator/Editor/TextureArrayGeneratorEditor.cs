using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(TextureArrayGenerator))]
public class TextureArrayGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        TextureArrayGenerator generator = (TextureArrayGenerator)target;

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Texture Array"))
        {
            Generate(false);
        }

        if (GUILayout.Button("Generate Texture Array (1 Px Sample)"))
        {
            Generate(true);
        }

        if (GUILayout.Button("List Terrain Names"))
        {
            // foreach (var tex in generator.textures)
            // {
            //     Debug.Log(tex.name);
            // }
            var names = generator.textures.Select(tex => tex.name).ToList();
            Debug.Log(string.Join(",", names));
        }
    }

    void Generate(bool sample)
    {
        TextureArrayGenerator generator = (TextureArrayGenerator)target;

        string defaultName = "NewTextureArray.asset";
        string defaultFolder = "Assets";

        string path = EditorUtility.SaveFilePanel(
            "Save Texture2DArray",
            defaultFolder,
            defaultName,
            "asset");

        if (string.IsNullOrEmpty(path))
        {
            Debug.Log("Texture2DArray save cancelled");
            return;
        }

        if (!path.StartsWith(Application.dataPath))
        {
            Debug.LogError("Texture2DArray must be saved in the Assets folder");
            return;
        }

        string relativePath = "Assets" + path.Substring(Application.dataPath.Length);

        var textureArray = sample ? generator.GenerateTextureArray1PixelSample(): generator.GenerateTextureArray();

        AssetDatabase.CreateAsset(textureArray, relativePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Texture2DArray saved at: " + relativePath);

        EditorGUIUtility.PingObject(textureArray);
    }
}
