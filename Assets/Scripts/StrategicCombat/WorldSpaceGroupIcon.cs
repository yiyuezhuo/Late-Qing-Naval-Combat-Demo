using StrategicCombatCore;
using TMPro;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UI;

public interface IWorldSpaceGroupIconDataSource
{
    string sizeStr { get; }
    Sprite typeIconSprite { get; }
    string typeIconPath { get; }
    Color countryColor { get; }
    string bottomLabelText { get; }
    float timelinessOpacity { get; }
}

public interface ILayableWorldSpaceGroupIconDataSource : IWorldSpaceGroupIconDataSource
{
    SideState side { get; }
    Cell cell { get; }
    float stackPriority { get; set; }
}

public class WorldSpaceGroupIconDatasourcePlaceholder : IWorldSpaceGroupIconDataSource
{
    [CreateProperty]
    public string sizeStr { get; }

    [CreateProperty]
    public Sprite typeIconSprite { get; }

    [CreateProperty]
    public string typeIconPath => null;

    [CreateProperty]
    public Color countryColor { get; }

    [CreateProperty]
    public string bottomLabelText { get; }

    [CreateProperty]
    public float timelinessOpacity => 1f;
}

public class WorldSpaceGroupIcon : MonoBehaviour
{
    public ILayableWorldSpaceGroupIconDataSource currentDataSource;

    [SerializeField] Image backgroundImage;
    [SerializeField] Image topHighlightImage;
    [SerializeField] Image bottomShadeImage;
    [SerializeField] Image iconPlateImage;
    [SerializeField] Image typeIconImage;
    [SerializeField] TMP_Text sizeText;
    [SerializeField] TMP_Text bottomText;

    string lastIconPath;

    static Sprite defaultPanelSprite;
    static TMP_FontAsset defaultFontAsset;

    void Awake()
    {
        EnsureSharedAssets();
        ApplyDefaults();
    }

    public void SetDataSource(ILayableWorldSpaceGroupIconDataSource group)
    {
        currentDataSource = group;
        EnsureSharedAssets();
        ApplyDefaults();

        sizeText.text = group.sizeStr;
        bottomText.text = group.bottomLabelText;

        var countryColor = group.countryColor;
        backgroundImage.color = countryColor;
        SetImageAlpha(topHighlightImage, group.timelinessOpacity * 0.1f);
        SetImageAlpha(bottomShadeImage, group.timelinessOpacity * 0.08f);
        SetImageAlpha(backgroundImage, group.timelinessOpacity);
        SetImageAlpha(iconPlateImage, group.timelinessOpacity * 0.96f);
        SetImageAlpha(typeIconImage, group.timelinessOpacity);
        SetTextAlpha(sizeText, group.timelinessOpacity);
        SetTextAlpha(bottomText, group.timelinessOpacity);

        ApplyTypeIcon(group);
    }

    void ApplyDefaults()
    {
        if (backgroundImage == null || topHighlightImage == null || bottomShadeImage == null ||
            iconPlateImage == null || typeIconImage == null || sizeText == null || bottomText == null)
        {
            Debug.LogError($"WorldSpaceGroupIcon prefab references are incomplete on {name}.", this);
            enabled = false;
            return;
        }

        backgroundImage.sprite ??= defaultPanelSprite;
        topHighlightImage.sprite ??= defaultPanelSprite;
        bottomShadeImage.sprite ??= defaultPanelSprite;
        iconPlateImage.sprite ??= defaultPanelSprite;

        sizeText.font ??= defaultFontAsset;
        bottomText.font ??= defaultFontAsset;
    }

    void ApplyTypeIcon(IWorldSpaceGroupIconDataSource group)
    {
        lastIconPath = group.typeIconPath;
        typeIconImage.sprite = group.typeIconSprite;
        typeIconImage.enabled = typeIconImage.sprite != null;

        if (string.IsNullOrEmpty(lastIconPath))
        {
            return;
        }

        UnityWebRequestImageReader.Instance.RequestIfNotRequestedYetOtherwiseExecuteDirectly(new ImageFetchTask
        {
            path = lastIconPath,
            spriteCallbacks =
            {
                sprite =>
                {
                    if (currentDataSource == group && lastIconPath == group.typeIconPath)
                    {
                        typeIconImage.sprite = sprite;
                        typeIconImage.enabled = sprite != null;
                    }
                }
            }
        });
    }

    static void SetImageAlpha(Image image, float alpha)
    {
        var color = image.color;
        color.a = alpha;
        image.color = color;
    }

    static void SetTextAlpha(TMP_Text text, float alpha)
    {
        var color = text.color;
        color.a = alpha;
        text.color = color;
    }

    static void EnsureSharedAssets()
    {
        if (defaultPanelSprite == null)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            defaultPanelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        if (defaultFontAsset == null)
        {
            defaultFontAsset = TMP_Settings.defaultFontAsset;
            if (defaultFontAsset == null)
            {
                defaultFontAsset = Resources.Load<TMP_FontAsset>("LiberationSans SDF");
            }
        }
    }
}
