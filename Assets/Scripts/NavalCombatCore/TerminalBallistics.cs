using System;

namespace NavalCombatCore
{
    public enum TerminalBallisticsFormulaPreset
    {
        DeMarreNickelSteel,
        KruppAllPurpose,
        Custom
    }

    public sealed class TerminalBallisticsFormulaParameters
    {
        public TerminalBallisticsFormulaPreset preset = TerminalBallisticsFormulaPreset.DeMarreNickelSteel;
        public string name = "De Marre Nickel-Steel";
        public float numericalConstant = 0.00005021f;
        public float projectileDiameterExponent = 0.07144f;
        public float energyDensityExponent = 0.71429f;
        public float coefficient = 1f;
        public float obliquityCosineExponent = 3f;

        public TerminalBallisticsFormulaParameters Clone()
        {
            return new TerminalBallisticsFormulaParameters
            {
                preset = preset,
                name = name,
                numericalConstant = numericalConstant,
                projectileDiameterExponent = projectileDiameterExponent,
                energyDensityExponent = energyDensityExponent,
                coefficient = coefficient,
                obliquityCosineExponent = obliquityCosineExponent
            };
        }

        public static TerminalBallisticsFormulaParameters ForPreset(TerminalBallisticsFormulaPreset preset)
        {
            return preset switch
            {
                TerminalBallisticsFormulaPreset.KruppAllPurpose => new TerminalBallisticsFormulaParameters
                {
                    preset = preset,
                    name = "Krupp All-Purpose",
                    numericalConstant = 0.30386f,
                    projectileDiameterExponent = 0.25f,
                    energyDensityExponent = 0.625f,
                    coefficient = 655f,
                    obliquityCosineExponent = 0f
                },
                TerminalBallisticsFormulaPreset.Custom => new TerminalBallisticsFormulaParameters
                {
                    preset = preset,
                    name = "Custom",
                    numericalConstant = 0.00005021f,
                    projectileDiameterExponent = 0.07144f,
                    energyDensityExponent = 0.71429f,
                    coefficient = 1f,
                    obliquityCosineExponent = 3f
                },
                _ => new TerminalBallisticsFormulaParameters()
            };
        }
    }

    public sealed class TerminalBallisticsInput
    {
        public float projectileMassKg = 386f;
        public float projectileDiameterInches = 12f;
        public float impactVelocityMetersPerSecond = 500f;
        public float angleOfFallDeg = 10f;
        public TerminalBallisticsFormulaParameters formulaParameters = TerminalBallisticsFormulaParameters.ForPreset(TerminalBallisticsFormulaPreset.DeMarreNickelSteel);
    }

    public sealed class TerminalBallisticsResult
    {
        public bool success;
        public string failureReason;
        public float impactVelocityMetersPerSecond;
        public float angleOfFallDeg;
        public float verticalObliquityDeg;
        public float horizontalObliquityDeg;
        public float verticalPenetrationInches;
        public float horizontalPenetrationInches;
    }

    public static class TerminalBallisticsSolver
    {
        const float FeetPerMeter = 3.2808399f;
        const float PoundsPerKilogram = 2.20462262f;
        const float MillimetersPerInch = 25.4f;

        public static TerminalBallisticsResult Solve(TerminalBallisticsInput input)
        {
            var validationError = Validate(input);
            if (validationError != null)
            {
                return new TerminalBallisticsResult
                {
                    success = false,
                    failureReason = validationError,
                    impactVelocityMetersPerSecond = input?.impactVelocityMetersPerSecond ?? 0f,
                    angleOfFallDeg = input?.angleOfFallDeg ?? 0f
                };
            }

            var verticalObliquity = input.angleOfFallDeg;
            var horizontalObliquity = 90f - input.angleOfFallDeg;

            return new TerminalBallisticsResult
            {
                success = true,
                impactVelocityMetersPerSecond = input.impactVelocityMetersPerSecond,
                angleOfFallDeg = input.angleOfFallDeg,
                verticalObliquityDeg = verticalObliquity,
                horizontalObliquityDeg = horizontalObliquity,
                verticalPenetrationInches = CalculatePenetrationInches(input, verticalObliquity),
                horizontalPenetrationInches = CalculatePenetrationInches(input, horizontalObliquity)
            };
        }

        static string Validate(TerminalBallisticsInput input)
        {
            if (input == null)
                return "Input is missing.";
            if (!IsFinitePositive(input.projectileMassKg))
                return "Projectile mass must be greater than 0.";
            if (!IsFinitePositive(input.projectileDiameterInches))
                return "Projectile diameter must be greater than 0.";
            if (!IsFinitePositive(input.impactVelocityMetersPerSecond))
                return "Impact velocity must be greater than 0.";
            if (!float.IsFinite(input.angleOfFallDeg) || input.angleOfFallDeg < 0f || input.angleOfFallDeg > 90f)
                return "Angle of fall must be between 0 and 90 degrees.";

            var parameters = input.formulaParameters;
            if (parameters == null)
                return "Formula parameters are missing.";
            if (!IsFinitePositive(parameters.numericalConstant))
                return "Formula constant must be greater than 0.";
            if (!float.IsFinite(parameters.projectileDiameterExponent))
                return "Projectile diameter exponent must be finite.";
            if (!IsFinitePositive(parameters.energyDensityExponent))
                return "Energy density exponent must be greater than 0.";
            if (!IsFinitePositive(parameters.coefficient))
                return "Formula coefficient must be greater than 0.";
            if (!float.IsFinite(parameters.obliquityCosineExponent) || parameters.obliquityCosineExponent < 0f)
                return "Obliquity cosine exponent must be 0 or greater.";
            return null;
        }

        static float CalculatePenetrationInches(TerminalBallisticsInput input, float obliquityDeg)
        {
            var parameters = input.formulaParameters;
            var diameterInches = input.projectileDiameterInches;
            var weightPounds = KilogramsToPounds(input.projectileMassKg);
            var velocityFeetPerSecond = MetersPerSecondToFeetPerSecond(input.impactVelocityMetersPerSecond);

            var energyDensity = weightPounds / Math.Pow(diameterInches, 3d);
            var velocityRatio = velocityFeetPerSecond / parameters.coefficient;
            var obliquityMultiplier = GetObliquityMultiplier(obliquityDeg, parameters.obliquityCosineExponent);
            var innerTerm = energyDensity * velocityRatio * velocityRatio * obliquityMultiplier;
            if (innerTerm <= 0d)
                return 0f;

            var thicknessOverDiameter =
                parameters.numericalConstant *
                Math.Pow(diameterInches, parameters.projectileDiameterExponent) *
                Math.Pow(innerTerm, parameters.energyDensityExponent);
            return (float)(thicknessOverDiameter * diameterInches);
        }

        static double GetObliquityMultiplier(float obliquityDeg, float cosineExponent)
        {
            if (cosineExponent <= 0f)
                return 1d;

            var radians = Math.Clamp(obliquityDeg, 0f, 90f) * Math.PI / 180d;
            var cosine = Math.Max(0d, Math.Cos(radians));
            return Math.Pow(cosine, cosineExponent);
        }

        static bool IsFinitePositive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        public static float MetersPerSecondToFeetPerSecond(float metersPerSecond) => metersPerSecond * FeetPerMeter;
        public static float FeetPerSecondToMetersPerSecond(float feetPerSecond) => feetPerSecond / FeetPerMeter;
        public static float KilogramsToPounds(float kilograms) => kilograms * PoundsPerKilogram;
        public static float PoundsToKilograms(float pounds) => pounds / PoundsPerKilogram;
        public static float InchesToMillimeters(float inches) => inches * MillimetersPerInch;
        public static float MillimetersToInches(float millimeters) => millimeters / MillimetersPerInch;
    }
}
