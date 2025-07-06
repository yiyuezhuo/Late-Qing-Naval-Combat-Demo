using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

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

            var textureArray = generator.GenerateTextureArray();
            
            AssetDatabase.CreateAsset(textureArray, relativePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("Texture2DArray saved at: " + relativePath);
            
            EditorGUIUtility.PingObject(textureArray);
        }
                
        // EditorGUILayout.Space();
        // EditorGUILayout.LabelField("Texture Array Status", EditorStyles.boldLabel);
        // EditorGUILayout.LabelField($"Texture Count: {generator.TextureCount}");
        // EditorGUILayout.ObjectField("Generated Array", generator.TextureArray, typeof(Texture2DArray), false);
    }
}
