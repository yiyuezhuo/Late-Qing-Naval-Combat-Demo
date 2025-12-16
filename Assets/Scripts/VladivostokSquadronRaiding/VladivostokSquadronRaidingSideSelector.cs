using CoreUtils;
using UnityEngine;
using Unity.Properties;

public class VladivostokSquadronRaidingSideSelector
{
    public class SideInfo
    {
        public string leaderObjectId;
        public GlobalString groupName;
        public Country country;

        [CreateProperty]
        public Texture2D leaderPortrait => EntityManager.Instance.Get<Leader>(leaderObjectId)?.portraitReference?.texture2d;

        [CreateProperty]
        public Texture2D countryFlag => UnityWebRequestImageReader.Instance.FetchTexture2D(Utils.GetCountryPath(country));

        [CreateProperty]
        public string leaderLabelName => EntityManager.Instance.Get<Leader>(leaderObjectId)?.name?.mergedName;

        [CreateProperty]
        public string groupNameLabelName => groupName?.mergedName;
    }

    public SideInfo japanese = new()
    {
        leaderObjectId = "43bfbde8-70e0-4d42-8122-3dc082557429", // Kamimura Hikonojō (上村彦之丞)
        // TODO: Add a dedicated editor?
        groupName = new()
        {
            english="2nd Fleet",
            japanese="第2艦隊",
            chineseSimplified="第2舰队",
        },
        country = Country.Japan,
    };

    public SideInfo russia = new()
    {
        leaderObjectId = "a572b659-f9f6-4417-a9ab-fa7bbb7f6b81", // Karl Jessen 
        groupName = new()
        {
            english="Vladivostok Squadron",
            japanese="浦塩艦隊",
            chineseSimplified="海参崴分队",
        },
        country = Country.Russia
    };
}
