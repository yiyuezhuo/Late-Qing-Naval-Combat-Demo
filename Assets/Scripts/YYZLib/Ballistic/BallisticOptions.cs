namespace YYZ.Ballistic
{
    public enum McCoyRangeUnit
    {
        Yards,
        Meters
    }

    public enum McCoyAtmosphere
    {
        StandardMetro,
        Icao
    }

    public enum M79PenetrationMode
    {
        NoseFirst,
        BaseFirst,
        NoCompletePenetration
    }

    public enum FacehardCapType
    {
        None,
        Hood,
        Soft,
        Hard,
        ThinHard
    }

    public enum FacehardNoseSchema
    {
        Standard,
        JapaneseCapHead
    }

    public enum FacehardNoseCondition
    {
        Intact,
        WindscreenRemoved,
        CapHeadRemoved,
        AllRemoved
    }

    public enum FacehardPenetrationType
    {
        NoCaliberSizeHole = 0,
        EffectiveComplete = 1,
        Complete = 2,
        HoledDamagedProjectileRejected = 3,
        PartialByLowerBodyPieces = 4,
        PartialByPlugOrFragments = 5,
        ShatteredAgainstPlate = 6
    }

    public enum FacehardPenetrationFlag
    {
        None,
        Holing,
        Complete
    }

    public enum FacehardShatterType
    {
        None,
        NoseOnly,
        Complete
    }

    public enum FacehardStatus
    {
        NoHole,
        Holed,
        Complete,
        EffectiveComplete
    }

    public enum JbmBoundaryLayer
    {
        LaminarLaminar,
        LaminarTurbulent,
        TurbulentTurbulent
    }

    public static class BallisticOptions
    {
        public static string ToLegacyCode(McCoyRangeUnit value)
        {
            return value == McCoyRangeUnit.Yards ? "yards" : "meters";
        }

        public static string ToLegacyCode(McCoyAtmosphere value)
        {
            return value == McCoyAtmosphere.Icao ? "icao" : "standard";
        }

        public static string ToLegacyCode(M79PenetrationMode value)
        {
            return value switch
            {
                M79PenetrationMode.BaseFirst => "base-first",
                M79PenetrationMode.NoCompletePenetration => "no-complete-penetration",
                _ => "nose-first",
            };
        }

        public static string ToLegacyCode(FacehardCapType value)
        {
            return value switch
            {
                FacehardCapType.Hood => "hood",
                FacehardCapType.Soft => "soft",
                FacehardCapType.Hard => "hard",
                FacehardCapType.ThinHard => "thin-hard",
                _ => "none",
            };
        }

        public static string ToLegacyCode(FacehardNoseSchema value)
        {
            return value == FacehardNoseSchema.JapaneseCapHead ? "japanese-cap-head" : "standard";
        }

        public static string ToLegacyCode(FacehardNoseCondition value)
        {
            return value switch
            {
                FacehardNoseCondition.WindscreenRemoved => "windscreen-removed",
                FacehardNoseCondition.CapHeadRemoved => "caphead-removed",
                FacehardNoseCondition.AllRemoved => "all-removed",
                _ => "intact",
            };
        }

        public static string ToLegacyCode(FacehardPenetrationFlag value)
        {
            return value switch
            {
                FacehardPenetrationFlag.Holing => "holing",
                FacehardPenetrationFlag.Complete => "complete",
                _ => "none",
            };
        }

        public static string ToLegacyCode(FacehardShatterType value)
        {
            return value switch
            {
                FacehardShatterType.NoseOnly => "nose-only",
                FacehardShatterType.Complete => "complete",
                _ => "none",
            };
        }

        public static string ToLegacyCode(FacehardStatus value)
        {
            return value switch
            {
                FacehardStatus.Holed => "holed",
                FacehardStatus.Complete => "complete",
                FacehardStatus.EffectiveComplete => "effective-complete",
                _ => "no-hole",
            };
        }

        public static string ToLegacyCode(JbmBoundaryLayer value)
        {
            return value switch
            {
                JbmBoundaryLayer.LaminarLaminar => "L/L",
                JbmBoundaryLayer.TurbulentTurbulent => "T/T",
                _ => "L/T",
            };
        }
    }
}
