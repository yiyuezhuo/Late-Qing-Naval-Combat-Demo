using System;
using UnityEngine.UIElements;
using UnityEngine;
using Unity.Properties;
using StrategicCombatCore;
using CoreUtils;
using YYZ;

public class StrategicScenarioStateEditor
{
    public void OnCreated(object sender, VisualElement root)
    {
        root.Q<Button>("ImportAreaSystemButton").clicked += () =>
        {
            Debug.Log("ImportAreaSystemButton clicked");

            IOManager.Instance.LoadTextFile(ImportAreaSystem, "xml");
        };

        root.Q<Button>("ExportAreaSystemButton").clicked += () =>
        {
            Debug.Log("ExportAreaSystemButton clicked");

            ExportAreaSystem();
        };

        RefreshDescriptionPreview(root);

        root.Q<Button>("SetDescriptionButton").clicked += () =>
        {
            scenarioState.globalDescription ??= new GlobalString();
            DialogRoot.Instance.PopupGlobalStringMarkdownEditorDialog(
                scenarioState.globalDescription,
                "Description",
                () => RefreshDescriptionPreview(root));
        };
    }

    void ImportAreaSystem(string text)
    {
        var areaSystem = XmlUtils.FromXML<AreaSystem>(text);
        StrategicGameState.Instance.scenarioState.areaSystem = areaSystem;
    }

    void ExportAreaSystem()
    {
        var text = XmlUtils.ToXML(StrategicGameState.Instance.scenarioState.areaSystem);
        IOManager.Instance.SaveTextFile(text, "Area System", "xml");
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        
    }

    [CreateProperty]
    public StrategicScenarioState scenarioState => StrategicGameState.Instance.scenarioState;

    void RefreshDescriptionPreview(VisualElement root)
    {
        root.Q<MarkdownRenderer>("DescriptionMarkdownRenderer")
            ?.SetMarkdownWithoutNotify(scenarioState.globalDescription?.shortName ?? string.Empty);
    }
}
