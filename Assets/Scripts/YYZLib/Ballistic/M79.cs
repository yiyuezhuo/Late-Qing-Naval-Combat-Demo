using System;
using System.Collections.Generic;

namespace YYZ.Ballistic
{
    public static class M79PenetrationMode
    {
        public const string NoseFirst = "nose-first";
        public const string BaseFirst = "base-first";
        public const string NoCompletePenetration = "no-complete-penetration";
    }

    public sealed class M79Input
    {
        public double ProjectileDiameter { get; set; } = 3;

        public double ProjectileWeight { get; set; } = 15;

        public double PlateThickness { get; set; } = 3;

        public double PlateQuality { get; set; } = 1;

        public double Obliquity { get; set; }

        public double StrikingVelocity { get; set; } = 1500;

        public double Elongation { get; set; } = 25;
    }

    public sealed class M79Result
    {
        public double TSlashD { get; set; }

        public double ProjectileDensity { get; set; }

        public double ScaleFactor { get; set; }

        public double GreenFunction { get; set; }

        public double MPrime { get; set; }

        public double MObliquity { get; set; }

        public double NavyBallisticLimit { get; set; }

        public double NavyBallisticLimitRounded { get; set; }

        public double NoseFirstNbl { get; set; }

        public double NoseFirstNblRounded { get; set; }

        public double EnergyDensity { get; set; }

        public double NormalEnergyDensity { get; set; }

        public double NblRatio { get; set; }

        public double VsNblRatio { get; set; }

        public string PenetrationMode { get; set; } = M79PenetrationMode.NoseFirst;

        public double EnergyNbl { get; set; }

        public double? ExitAngle { get; set; }

        public double? DeflectionAngle { get; set; }

        public double? RemainingVelocity { get; set; }

        public List<string> LegacyReport { get; set; } = new List<string>();
    }

    public sealed class M79ScanRow
    {
        public double Thickness { get; set; }

        public M79Result Result { get; set; } = new M79Result();
    }

    public sealed class M79RangeInput
    {
        public double ProjectileDiameter { get; set; }

        public double ProjectileWeight { get; set; }

        public double MinPlateThickness { get; set; }

        public double MaxPlateThickness { get; set; }

        public double ThicknessStep { get; set; }

        public double PlateQuality { get; set; }

        public double Elongation { get; set; }

        public double Obliquity { get; set; }
    }

    public sealed class M79LegacyRangeRow
    {
        public double Thickness { get; set; }

        public double TSlashD { get; set; }

        public double Nbl { get; set; }

        public double Energy { get; set; }

        public double NormalEnergy { get; set; }

        public double NoseFirstNbl { get; set; }

        public double NoseFirstEnergy { get; set; }

        public double NoseFirstNormalEnergy { get; set; }
    }

    public static class M79
    {
        const double Rad45 = Math.PI / 4.0;

        sealed class RangeRow
        {
            public double Max;
            public double Nc;
            public double TCoefficient;
            public double Ja;
            public double Jb;
            public double Jc;
        }

        sealed class ExitAngles
        {
            public double ExitAngle;
            public double DeflectionAngle;
        }

        static readonly RangeRow[] Ranges =
        {
            new RangeRow { Max = 0.01156, Nc = 335.25392, TCoefficient = 0.4336513, Ja = 0, Jb = 0, Jc = 0 },
            new RangeRow { Max = 0.05, Nc = 516.85756, TCoefficient = 0.5306597, Ja = -0.02, Jb = 4682.6223, Jc = 54.131113 },
            new RangeRow { Max = 0.075, Nc = 902.41425, TCoefficient = 0.7166931, Ja = -0.01, Jb = 7200, Jc = 360 },
            new RangeRow { Max = 0.1, Nc = 1290.90181, TCoefficient = 0.8549115, Ja = 0, Jb = 0, Jc = 0 },
            new RangeRow { Max = 0.2549, Nc = 1687.6956, TCoefficient = 0.9713125, Ja = 0, Jb = 0, Jc = 0 },
            new RangeRow { Max = 0.47931, Nc = 1361.41055, TCoefficient = 0.8141355, Ja = 0.02, Jb = 802.10329, Jc = 204.45613 },
            new RangeRow { Max = 1, Nc = 1179.94178, TCoefficient = 0.619609, Ja = 0, Jb = 0, Jc = 0 },
            new RangeRow { Max = 2, Nc = 1179.94178, TCoefficient = 0.6387357, Ja = 0, Jb = 0, Jc = 0 },
            new RangeRow { Max = 3, Nc = 1173.77728, TCoefficient = 0.6462927, Ja = 0, Jb = 0, Jc = 0 },
            new RangeRow { Max = 4, Nc = 1201.73299, TCoefficient = 0.6248678, Ja = 0, Jb = 0, Jc = 0 },
            new RangeRow { Max = 6, Nc = 1227.31234, TCoefficient = 0.609576, Ja = 0, Jb = 0, Jc = 0 },
        };

        static readonly double[] MpLtBite =
        {
            1, 0.999, 0.998, 0.995, 0.991, 0.986, 0.981, 0.974, 0.966, 0.958,
            0.948, 0.937, 0.925, 0.912, 0.901, 0.889, 0.881, 0.879, 0.885,
        };

        static readonly double[,] MpTable =
        {
            { 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885, 0.885 },
            { 0.918, 0.916, 0.912, 0.906, 0.902, 0.901, 0.905, 0.909, 0.912, 0.914, 0.914, 0.914, 0.914, 0.914, 0.914, 0.914, 0.914, 0.914, 0.914 },
            { 0.968, 0.96, 0.949, 0.932, 0.922, 0.921, 0.928, 0.936, 0.944, 0.951, 0.956, 0.96, 0.961, 0.961, 0.961, 0.961, 0.961, 0.961, 0.961 },
            { 1.05, 1.028, 1, 0.97, 0.958, 0.952, 0.96, 0.968, 0.982, 0.995, 1.005, 1.015, 1.021, 1.023, 1.023, 1.023, 1.023, 1.023, 1.023 },
            { 1.153, 1.111, 1.057, 1.008, 0.992, 0.987, 0.993, 1.006, 1.024, 1.042, 1.062, 1.076, 1.088, 1.094, 1.098, 1.098, 1.098, 1.098, 1.098 },
            { 1.272, 1.203, 1.12, 1.053, 1.027, 1.021, 1.031, 1.049, 1.072, 1.094, 1.12, 1.14, 1.154, 1.165, 1.172, 1.179, 1.182, 1.184, 1.184 },
            { 1.376, 1.29, 1.178, 1.092, 1.058, 1.052, 1.067, 1.089, 1.116, 1.142, 1.172, 1.197, 1.215, 1.231, 1.241, 1.25, 1.257, 1.26, 1.261 },
            { 1.441, 1.354, 1.213, 1.119, 1.083, 1.079, 1.095, 1.119, 1.147, 1.178, 1.211, 1.24, 1.262, 1.28, 1.294, 1.302, 1.31, 1.315, 1.316 },
            { 1.5, 1.4, 1.232, 1.134, 1.099, 1.094, 1.111, 1.139, 1.165, 1.194, 1.232, 1.262, 1.291, 1.313, 1.326, 1.339, 1.345, 1.351, 1.353 },
            { 1.519, 1.414, 1.232, 1.13, 1.098, 1.092, 1.109, 1.138, 1.165, 1.195, 1.232, 1.265, 1.295, 1.32, 1.337, 1.35, 1.36, 1.365, 1.37 },
            { 1.498, 1.386, 1.2, 1.109, 1.075, 1.07, 1.087, 1.114, 1.14, 1.171, 1.209, 1.24, 1.27, 1.293, 1.311, 1.325, 1.337, 1.344, 1.349 },
            { 1.448, 1.326, 1.157, 1.064, 1.029, 1.02, 1.037, 1.064, 1.091, 1.119, 1.156, 1.189, 1.217, 1.24, 1.257, 1.268, 1.28, 1.287, 1.292 },
            { 1.36, 1.255, 1.087, 1.004, 0.968, 0.961, 0.977, 1, 1.026, 1.055, 1.087, 1.112, 1.149, 1.17, 1.19, 1.202, 1.212, 1.218, 1.221 },
            { 1.26, 1.167, 1.013, 0.932, 0.897, 0.891, 0.906, 0.929, 0.957, 0.98, 1.012, 1.04, 1.066, 1.088, 1.109, 1.121, 1.132, 1.14, 1.145 },
            { 1.153, 1.071, 0.933, 0.858, 0.826, 0.82, 0.837, 0.853, 0.881, 0.908, 0.933, 0.961, 0.982, 1.007, 1.025, 1.039, 1.049, 1.055, 1.06 },
        };

        static readonly double[] MpNf = { 1.519, 1.414, 1.232, 1.134, 1.099, 1.094 };

        public static M79Input DefaultInput()
        {
            return new M79Input();
        }

        public static M79Result Calculate(M79Input rawInput)
        {
            var input = ClampInput(rawInput ?? new M79Input());
            var tSlashD = input.PlateThickness / input.ProjectileDiameter;
            var projectileDensity = input.ProjectileWeight / Math.Pow(input.ProjectileDiameter, 3);
            var scaleFactor = Math.Sqrt(1 - 0.04 * Math.Log(input.ProjectileDiameter / 3));
            var obRad = input.Obliquity * BallisticMath.Deg;
            var cosOb = Math.Cos(obRad);
            var range = SelectRange(tSlashD);
            var green = GreenFunction(range, tSlashD);
            var mPrime = MPrimeFor(input, tSlashD, cosOb);
            var mObliquity = mPrime / cosOb;
            var navyBl =
                (range.Nc * green * mObliquity * scaleFactor * Math.Pow(input.PlateQuality * tSlashD, range.TCoefficient)) /
                Math.Sqrt(projectileDensity);
            if (input.Elongation < 25 && input.ProjectileDiameter > 8)
            {
                navyBl *= 1 - (1 - Math.Sqrt(input.Elongation / 25)) * ((input.ProjectileDiameter - 8) / 8);
            }

            var navyRounded = BallisticText.RoundHalfUp(navyBl);
            var noseFirst = NoseFirstNbl(tSlashD, input.Obliquity, mPrime, navyBl);
            var noseFirstRounded = BallisticText.RoundHalfUp(noseFirst);
            var vsRounded = BallisticText.RoundHalfUp(input.StrikingVelocity);
            var energyDensity = (0.5 * (input.ProjectileWeight / 32.186) * navyBl * navyBl) /
                (Math.PI * input.PlateThickness * Math.Pow(input.ProjectileDiameter / 2, 2));
            var normalEnergyDensity = energyDensity * cosOb * cosOb;

            var mode = M79PenetrationMode.NoseFirst;
            if (vsRounded < navyRounded)
            {
                mode = M79PenetrationMode.NoCompletePenetration;
            }
            else if (input.Obliquity > 45 && noseFirstRounded != navyRounded && vsRounded < noseFirstRounded)
            {
                mode = M79PenetrationMode.BaseFirst;
            }

            var energyNbl = navyBl;
            if (mode == M79PenetrationMode.NoseFirst && input.Obliquity > 45 && noseFirstRounded != navyRounded)
            {
                energyNbl = EnergyNblForHighOb(input.Obliquity, obRad, cosOb, navyBl);
            }

            double? exit = null;
            double? deflection = null;
            double? remaining = null;
            if (mode != M79PenetrationMode.NoCompletePenetration)
            {
                var angles = ExitAngle(input, energyNbl, mode == M79PenetrationMode.BaseFirst);
                exit = angles.ExitAngle;
                deflection = angles.DeflectionAngle;
                var velocityDelta = input.StrikingVelocity * input.StrikingVelocity - energyNbl * energyNbl;
                remaining = velocityDelta > 0 ? Math.Cos((deflection ?? 0) * BallisticMath.Deg) * Math.Sqrt(velocityDelta) : 0;
            }

            var result = new M79Result
            {
                TSlashD = tSlashD,
                ProjectileDensity = projectileDensity,
                ScaleFactor = scaleFactor,
                GreenFunction = green,
                MPrime = mPrime,
                MObliquity = mObliquity,
                NavyBallisticLimit = navyBl,
                NavyBallisticLimitRounded = navyRounded,
                NoseFirstNbl = noseFirst,
                NoseFirstNblRounded = noseFirstRounded,
                EnergyDensity = energyDensity,
                NormalEnergyDensity = normalEnergyDensity,
                NblRatio = noseFirstRounded / navyRounded,
                VsNblRatio = vsRounded / navyRounded,
                PenetrationMode = mode,
                EnergyNbl = energyNbl,
                ExitAngle = exit,
                DeflectionAngle = deflection,
                RemainingVelocity = remaining,
            };
            result.LegacyReport = RenderLegacyReport(input, result);
            return result;
        }

        public static List<M79ScanRow> Scan(M79Input input, double? maxThickness = null, int steps = 80)
        {
            var source = input ?? new M79Input();
            var minThickness = source.ProjectileDiameter * 0.001;
            var max = Math.Max(maxThickness ?? source.ProjectileDiameter * 6, minThickness);
            var rows = new List<M79ScanRow>();
            for (var index = 0; index <= steps; index += 1)
            {
                var thickness = minThickness + ((max - minThickness) * index) / steps;
                rows.Add(new M79ScanRow
                {
                    Thickness = thickness,
                    Result = Calculate(new M79Input
                    {
                        ProjectileDiameter = source.ProjectileDiameter,
                        ProjectileWeight = source.ProjectileWeight,
                        PlateThickness = thickness,
                        PlateQuality = source.PlateQuality,
                        Obliquity = source.Obliquity,
                        StrikingVelocity = source.StrikingVelocity,
                        Elongation = source.Elongation,
                    }),
                });
            }

            return rows;
        }

        public static List<M79LegacyRangeRow> ScanLegacyRange(M79RangeInput input)
        {
            var rows = new List<M79LegacyRangeRow>();
            var minThickness = Math.Max(input.MinPlateThickness, input.ProjectileDiameter * 0.001);
            var maxThickness = Math.Min(Math.Max(input.MaxPlateThickness, minThickness), input.ProjectileDiameter * 5.99999);
            var step = input.ThicknessStep <= 0 ? 0 : Math.Max(input.ThicknessStep, 0.0001);
            for (double thickness = minThickness, guard = 0; guard < 10000; guard += 1)
            {
                var result = Calculate(new M79Input
                {
                    ProjectileDiameter = input.ProjectileDiameter,
                    ProjectileWeight = input.ProjectileWeight,
                    PlateThickness = thickness,
                    PlateQuality = input.PlateQuality,
                    Obliquity = input.Obliquity,
                    StrikingVelocity = 3500,
                    Elongation = input.Elongation,
                });
                var noseFirstEnergy = (0.5 * (input.ProjectileWeight / 32.185) * result.NoseFirstNbl * result.NoseFirstNbl) /
                    (Math.PI * thickness * Math.Pow(input.ProjectileDiameter / 2, 2));
                var cosObSquared = Math.Pow(Math.Cos(input.Obliquity * BallisticMath.Deg), 2);
                rows.Add(new M79LegacyRangeRow
                {
                    Thickness = thickness,
                    TSlashD = result.TSlashD,
                    Nbl = result.NavyBallisticLimitRounded,
                    Energy = BallisticText.RoundHalfUp(result.EnergyDensity),
                    NormalEnergy = BallisticText.RoundHalfUp(result.NormalEnergyDensity),
                    NoseFirstNbl = result.NoseFirstNblRounded,
                    NoseFirstEnergy = BallisticText.RoundHalfUp(noseFirstEnergy),
                    NoseFirstNormalEnergy = BallisticText.RoundHalfUp(noseFirstEnergy * cosObSquared),
                });
                if (step == 0 || Math.Abs(thickness - maxThickness) < 0.000001)
                {
                    break;
                }

                var next = thickness + step;
                thickness = next > maxThickness ? maxThickness : next;
            }

            return rows;
        }

        public static List<string> RenderLegacyReport(M79Input input, M79Result result)
        {
            var lines = new List<string>();
            var vsRounded = BallisticText.RoundHalfUp(input.StrikingVelocity);
            var obRounded = BallisticText.RoundHalfUp(10 * input.Obliquity) / 10;
            lines.Add("NAVY BALLISTIC LIMIT, ENERGY, EXIT ANGLE, & REMAINING VELOCITY FOR PROJECTILES");
            lines.Add("LIKE WWII U.S. ARMY 3-INCH M79 AP SHOT (TANGENT OGIVE W/1.667-CALIBER RADIUS)");
            lines.Add("VS WWII U.S. NAVY CLASS 'B' ARMOR OR S.T.S. OF 115,000 P.S.I. TENSILE STRENGTH");
            lines.Add("");
            lines.Add($"Projectile Diameter (Inches) = {BallisticText.ToJsString(input.ProjectileDiameter)}");
            lines.Add($"Projectile Total Weight (Pounds) = {BallisticText.ToJsString(input.ProjectileWeight)}");
            lines.Add($"Plate Thickness (Inches) = {BallisticText.ToJsString(input.PlateThickness)}");
            lines.Add($"PLATE THICKNESS IN CALIBERS = {BallisticText.ToJsString(result.TSlashD)}");
            lines.Add($"Plate Quality Factor = {BallisticText.ToJsString(input.PlateQuality)}");
            lines.Add($"Obliquity (Degrees) = {BallisticText.ToJsString(input.Obliquity)}");
            lines.Add($"Striking Velocity (Feet/Second) = {BallisticText.ToJsString(input.StrikingVelocity)}");
            lines.Add($"Percent Elongation of Armor Metal Used = {BallisticText.ToJsString(input.Elongation)}");
            lines.Add("");
            lines.Add($"Navy Ballistic Limit = {BallisticText.ToJsString(result.NavyBallisticLimitRounded)} feet/second");
            lines.Add($"Energy/Unit Hole Volume      = {BallisticText.ToJsString(BallisticText.RoundHalfUp(result.EnergyDensity))} ft-lbs/in^3 (Actual Energy Density)");
            lines.Add($"Energy Density X COS(OB)^2   = {BallisticText.ToJsString(BallisticText.RoundHalfUp(result.NormalEnergyDensity))} ft-lbs/in^3 (Normal Energy Density)");
            if (result.NoseFirstNblRounded == result.NavyBallisticLimitRounded)
            {
                lines.Add("Navy B.L. (Nose-First Penetration) = NAVY B.L. (Always Nose-First)");
            }
            else
            {
                lines.Add($"Navy B.L. (Nose-First Penetration) = {BallisticText.ToJsString(result.NoseFirstNblRounded)} feet/second");
            }

            lines.Add($"NBL(N.F.)/TRUE NBL RATIO = {BallisticText.ToJsString(result.NblRatio)} --- VS/TRUE NBL RATIO = {BallisticText.ToJsString(result.VsNblRatio)}");
            if (input.Obliquity <= 45)
            {
                lines.Add("ALL PENETRATIONS NOSE-FIRST AT OB <= 45 SO VR = EX = 0 & ENERGY NBL = TRUE NBL.");
            }
            else if (result.PenetrationMode == M79PenetrationMode.BaseFirst)
            {
                lines.Add("BASE-FIRST PENETRATION AT OB > 45 SO VR = EX = 0 & ENERGY NBL = TRUE NBL.");
            }
            else
            {
                lines.Add("NOSE-FIRST PENETRATION AT OB > 45 SO VR > 0 & EX > 0 & ENERGY NBL < TRUE NBL.");
                lines.Add($"ENERGY NBL = {BallisticText.ToJsString(BallisticText.RoundHalfUp(result.EnergyNbl))} feet/second");
            }

            lines.Add("");
            lines.Add($"Striking Velocity (VS) = {BallisticText.ToJsString(vsRounded)} {(vsRounded <= 1 ? "foot/second" : "feet/second")}");
            lines.Add($"Obliquity Angle   (OB) = {BallisticText.ToJsString(obRounded)} {(obRounded <= 1 ? "degree" : "degrees")}");
            if (result.PenetrationMode == M79PenetrationMode.NoCompletePenetration)
            {
                lines.Add("No complete penetration.  EX & VR are UNDEFINED.");
            }
            else
            {
                lines.Add(result.PenetrationMode == M79PenetrationMode.BaseFirst
                    ? "Base-First Penetration occurred at given Striking Velocity."
                    : "Nose-First Penetration occurred at given Striking Velocity.");
                lines.Add($"Exit Angle (EX) (assumed to be in same plane as OB) = {BallisticText.ToJsString(BallisticText.RoundHalfUp(10 * (result.ExitAngle ?? 0)) / 10)} degrees");
                lines.Add($"Deflection Angle (OB - EX) = {BallisticText.ToJsString(BallisticText.RoundHalfUp(10 * (result.DeflectionAngle ?? 0)) / 10)} degrees");
                lines.Add($"Remaining Velocity (VR) = {BallisticText.ToJsString(BallisticText.RoundHalfUp(result.RemainingVelocity ?? 0))} feet/second");
            }

            return lines;
        }

        static M79Input ClampInput(M79Input input)
        {
            var projectileDiameter = Math.Max(input.ProjectileDiameter, 0.001);
            var minPlate = projectileDiameter * 0.001;
            var maxPlate = projectileDiameter * 5.99999;
            var obliquity = Math.Min(Math.Max(input.Obliquity, 0), 79.9999);
            return new M79Input
            {
                ProjectileDiameter = projectileDiameter,
                ProjectileWeight = Math.Max(input.ProjectileWeight, 0.001),
                PlateThickness = Math.Min(Math.Max(input.PlateThickness, minPlate), maxPlate),
                PlateQuality = Math.Max(input.PlateQuality, 0.001),
                Obliquity = obliquity,
                StrikingVelocity = Math.Min(Math.Max(input.StrikingVelocity, 1), 3500),
                Elongation = Math.Max(input.Elongation, 10),
            };
        }

        static RangeRow SelectRange(double tSlashD)
        {
            foreach (var range in Ranges)
            {
                if (tSlashD <= range.Max)
                {
                    return range;
                }
            }

            return Ranges[Ranges.Length - 1];
        }

        static double GreenFunction(RangeRow range, double tSlashD)
        {
            if (range.Ja == 0)
            {
                return 1;
            }

            var jDeg = range.Jb * tSlashD - range.Jc;
            var jSin = Math.Max(Math.Sin(jDeg * BallisticMath.Deg), 0);
            return 1 + range.Ja * jSin;
        }

        static double MPrimeFor(M79Input input, double tSlashD, double cosOb)
        {
            if (input.Obliquity < 45)
            {
                var i1 = (int)Math.Floor(input.Obliquity / 2.5);
                var i2 = (input.Obliquity - 2.5 * i1) / 2.5;
                return MpLtBite[i1] + i2 * (MpLtBite[i1 + 1] - MpLtBite[i1]);
            }

            var c1 = (int)Math.Floor((input.Obliquity - 45) / 2.5);
            var c2 = (input.Obliquity - 45 - 2.5 * c1) / 2.5;
            var tSlashDPr = Math.Min(tSlashD, 0.899);
            var t1 = (int)Math.Floor(tSlashDPr / 0.05);
            var t2 = (tSlashDPr - 0.05 * t1) / 0.05;
            var mp0 = MpTable[c1, t1] + t2 * (MpTable[c1, t1 + 1] - MpTable[c1, t1]);
            var mp1 = MpTable[c1 + 1, t1] + t2 * (MpTable[c1 + 1, t1 + 1] - MpTable[c1 + 1, t1]);
            var value = mp0 + c2 * (mp1 - mp0);
            return BallisticMath.IsFinite(value) ? value : cosOb;
        }

        static double NoseFirstNbl(double tSlashD, double obliquity, double mPrime, double navyBl)
        {
            if (!((tSlashD > 0.1 && tSlashD < 0.25 && obliquity > 65) || (tSlashD <= 0.1 && obliquity > 67.5)))
            {
                return navyBl;
            }

            var tSlashDPr = Math.Min(tSlashD, 0.899);
            var t1 = (int)Math.Floor(tSlashDPr / 0.05);
            var t2 = (tSlashDPr - 0.05 * t1) / 0.05;
            var safeT1 = Math.Min(t1, MpNf.Length - 2);
            var mPrimeMax = MpNf[safeT1] + t2 * (MpNf[safeT1 + 1] - MpNf[safeT1]);
            var mPrimeNf = mPrimeMax - (mPrimeMax - mPrime) * (tSlashD / 0.25);
            return (mPrimeNf / mPrime) * navyBl;
        }

        static double EnergyNblForHighOb(double obliquity, double obRad, double cosOb, double navyBl)
        {
            var e1 = 1 + (obliquity - 45) / 45;
            var e2 = 2 * Math.Sin(obRad) * cosOb;
            var e3 = e1 / e2;
            var e4 = Math.Sqrt(2 * e3 - 1) / e3;
            return e4 * navyBl;
        }

        static ExitAngles ExitAngle(M79Input input, double energyNbl, bool baseFirst)
        {
            if (input.Obliquity <= 0.005)
            {
                return new ExitAngles { ExitAngle = 0, DeflectionAngle = input.Obliquity };
            }

            var obRad = input.Obliquity * BallisticMath.Deg;
            var vRatio = input.StrikingVelocity / energyNbl;
            if (vRatio < 1)
            {
                vRatio = 1;
            }

            var tmpV = vRatio * vRatio - 1;
            var tmpVel = vRatio * vRatio + vRatio * Math.Sqrt(tmpV);
            var trueSnCs = Math.Sin(obRad) * Math.Cos(obRad);
            var snCs = baseFirst ? Math.Sin(Rad45) * Math.Cos(Rad45) : trueSnCs;
            var tmpDf1 = snCs / tmpVel;
            var tmpDf2 = Math.Max(1 - 4 * tmpDf1 * tmpDf1, 0);
            var tanDf = (1 - Math.Sqrt(tmpDf2)) / (2 * tmpDf1);
            var deflectionAngle = Math.Atan(tanDf) / BallisticMath.Deg;
            if (snCs != trueSnCs)
            {
                deflectionAngle *= 1 + (input.Obliquity - 45) / 45;
            }

            return new ExitAngles
            {
                ExitAngle = input.Obliquity - deflectionAngle,
                DeflectionAngle = deflectionAngle,
            };
        }
    }
}
