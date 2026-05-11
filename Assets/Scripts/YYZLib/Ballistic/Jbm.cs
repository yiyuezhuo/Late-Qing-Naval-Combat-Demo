using System;
using System.Collections.Generic;

namespace YYZ.Ballistic
{
    public class JbmProjectileGeometryInput
    {
        public double ReferenceDiameterMm { get; set; } = 7.82;

        public double TotalLengthCalibers { get; set; } = 4.25;

        public double NoseLengthCalibers { get; set; } = 2.0;

        public double TangentRadiusRatio { get; set; } = 0.5;

        public double BoattailLengthCalibers { get; set; } = 0.7;

        public double BaseDiameterCalibers { get; set; } = 0.82;

        public double MeplatDiameterCalibers { get; set; } = 0.12;

        public string ProjectileId { get; set; } = "Example projectile";
    }

    public sealed class McDragInput : JbmProjectileGeometryInput
    {
        public double RotatingBandDiameterCalibers { get; set; } = 1.0;

        public double CenterOfGravityCalibers { get; set; } = 2.1;

        public JbmBoundaryLayer BoundaryLayer { get; set; } = JbmBoundaryLayer.LaminarTurbulent;
    }

    public sealed class McDragRow
    {
        public double Mach { get; set; }

        public double Cd0 { get; set; }

        public double Cdh { get; set; }

        public double Cdsf { get; set; }

        public double Cdbnd { get; set; }

        public double Cdbt { get; set; }

        public double Cdb { get; set; }

        public double PbOverPinf { get; set; }
    }

    public sealed class McDragResult
    {
        public List<McDragRow> Rows { get; set; } = new List<McDragRow>();

        public List<string> Warnings { get; set; } = new List<string>();

        public List<string> LegacyReport { get; set; } = new List<string>();
    }

    public sealed class McGyroInput : JbmProjectileGeometryInput
    {
        public double ProjectileDensityGramsPerCc { get; set; } = 10.9;

        public double RiflingTwistCalibersPerTurn { get; set; } = 30;
    }

    public sealed class McGyroRow
    {
        public double Mach { get; set; }

        public double StabilityFactor { get; set; }

        public double TwistForSg15 { get; set; }
    }

    public sealed class McGyroResult
    {
        public List<McGyroRow> Rows { get; set; } = new List<McGyroRow>();

        public List<string> LegacyReport { get; set; } = new List<string>();
    }

    public sealed class IntLiftInput : JbmProjectileGeometryInput
    {
        public double CenterOfGravityCalibers { get; set; } = 2.1;
    }

    public sealed class IntLiftRow
    {
        public double Mach { get; set; }

        public double Cla { get; set; }

        public double Cma { get; set; }

        public double Cda2 { get; set; }
    }

    public sealed class IntLiftResult
    {
        public List<IntLiftRow> Rows { get; set; } = new List<IntLiftRow>();

        public List<string> Warnings { get; set; } = new List<string>();

        public List<string> LegacyReport { get; set; } = new List<string>();
    }

    public static class Jbm
    {
        static readonly double[] McDragMaches =
        {
            0.5, 0.6, 0.7, 0.8, 0.85, 0.9, 0.925, 0.95, 0.975, 1.0, 1.1, 1.2, 1.3,
            1.4, 1.5, 1.6, 1.7, 1.8, 2.0, 2.2, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0,
        };

        static readonly double[] McGyroMaches =
        {
            0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 1.0, 1.1, 1.2, 1.3, 1.4, 1.5,
            1.6, 1.7, 1.8, 2.0, 2.2, 2.5, 3.0, 3.5, 4.0, 5.0,
        };

        // INTLIFT Mach table for CLA, CMA, and CDA2 output.
        static readonly double[] IntLiftMaches =
        {
            0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 1.0, 1.1, 1.2, 1.4, 1.6, 1.8,
            2.0, 2.2, 2.5, 3.0,
        };

        public static McDragInput DefaultMcDragInput()
        {
            return new McDragInput();
        }

        public static McGyroInput DefaultMcGyroInput()
        {
            return new McGyroInput();
        }

        public static IntLiftInput DefaultIntLiftInput()
        {
            return new IntLiftInput();
        }

        public static McDragResult CalculateMcDrag(McDragInput rawInput)
        {
            // MCDRAG, BY R. L. MCCOY.
            // DEC. 1974 DRAG COEFFICIENT ESTIMATE FOR AN AXISYMMETRIC PROJECTILE.
            var input = NormalizeMcDrag(rawInput ?? new McDragInput());
            var rows = new List<McDragRow>();

            foreach (var mach in McDragMaches)
            {
                // Skin-friction setup: Reynolds number, laminar/turbulent coefficients,
                // and wetted areas for nose plus afterbody.
                var t1 = (1 - input.MeplatDiameterCalibers) / input.NoseLengthCalibers;
                var m2 = mach * mach;
                var reynolds = 23296.3 * mach * input.TotalLengthCalibers * input.ReferenceDiameterMm;
                var logRe = 0.4343 * Math.Log(reynolds);
                var c7 = (1.328 / Math.Sqrt(reynolds)) * Math.Pow(1 + 0.12 * m2, -0.12);
                var c8 = (0.455 / Math.Pow(logRe, 2.58)) * Math.Pow(1 + 0.21 * m2, -0.32);
                var d5 = 1 + (0.333 + 0.02 / Math.Pow(input.NoseLengthCalibers, 2)) * input.TangentRadiusRatio;
                var s1 = 1.5708 * input.NoseLengthCalibers * d5 * (1 + 1 / (8 * Math.Pow(input.NoseLengthCalibers, 2)));
                var s2 = 3.1416 * (input.TotalLengthCalibers - input.NoseLengthCalibers);
                var s3 = s1 + s2;
                double c9;
                double c10;
                if (input.BoundaryLayer == JbmBoundaryLayer.LaminarLaminar)
                {
                    c9 = 1.2732 * s3 * c7;
                    c10 = c9;
                }
                else if (input.BoundaryLayer == JbmBoundaryLayer.LaminarTurbulent)
                {
                    c9 = 1.2732 * s3 * c7;
                    c10 = 1.2732 * s3 * c8;
                }
                else
                {
                    c9 = 1.2732 * s3 * c8;
                    c10 = c9;
                }

                var cdsf = (c9 * s1 + c10 * s2) / s3;
                // Meplat/head pressure contribution used by the transonic and supersonic head drag terms.
                var c15 = (m2 - 1) / (2.4 * m2);
                var p5 = mach <= 1
                    ? Math.Pow(1 + 0.2 * m2, 3.5)
                    : Math.Pow(1.2 * m2, 3.5) * Math.Pow(6 / (7 * m2 - 1), 2.5);
                var c16 = (1.122 * (p5 - 1) * Math.Pow(input.MeplatDiameterCalibers, 2)) / m2;
                double c18;
                if (mach <= 0.91)
                {
                    c18 = 0;
                }
                else if (mach >= 1.41)
                {
                    c18 = 0.85 * c16;
                }
                else
                {
                    c18 = (0.254 + 2.88 * c15) * c16;
                }

                // Base pressure ratio and base drag.
                var p2 = mach < 1
                    ? 1 / (1 + 0.1875 * m2 + 0.0531 * m2 * m2)
                    : 1 / (1 + 0.2477 * m2 + 0.0345 * m2 * m2);
                var p4 = (1 + 0.09000001 * m2 * (1 - Math.Exp(input.NoseLengthCalibers - input.TotalLengthCalibers))) *
                    (1 + 0.25 * m2 * (1 - input.BaseDiameterCalibers));
                var pbOverPinf = Math.Max(p2 * p4, 0);
                var cdb = (1.4286 * (1 - pbOverPinf) * Math.Pow(input.BaseDiameterCalibers, 2)) / m2;
                // Rotating band drag.
                var cdbnd = mach < 0.95
                    ? Math.Pow(mach, 12.5) * (input.RotatingBandDiameterCalibers - 1)
                    : (0.21 + 0.28 / m2) * (input.RotatingBandDiameterCalibers - 1);

                double cdh;
                double cdbt;
                if (mach <= 1)
                {
                    // Subsonic and transonic boattail drag and head drag.
                    if (input.BoattailLengthCalibers <= 0 || mach <= 0.85)
                    {
                        cdbt = 0;
                    }
                    else
                    {
                        var t2 = (1 - input.BaseDiameterCalibers) / (2 * input.BoattailLengthCalibers);
                        var t3 = 2 * t2 * t2 + t2 * t2 * t2;
                        var e1 = Math.Exp(-2 * input.BoattailLengthCalibers);
                        var b4 = 1 - e1 + 2 * t2 * (e1 * (input.BoattailLengthCalibers + 0.5) - 0.5);
                        cdbt = 2 * t3 * b4 * (1 / (0.564 + 1250 * c15 * c15));
                    }

                    var x2 = Math.Pow(1 + 0.552 * Math.Pow(t1, 0.8), -0.5);
                    var c17 = mach <= x2 ? 0 : 0.368 * Math.Pow(t1, 1.8) + 1.6 * t1 * c15;
                    cdh = c17 + c18;
                }
                else
                {
                    // Supersonic head drag and boattail drag.
                    var b2 = m2 - 1;
                    var b = Math.Sqrt(b2);
                    var z = b;
                    var s4 = 1 + 0.368 * Math.Pow(t1, 1.85);
                    if (mach < s4)
                    {
                        z = Math.Sqrt(s4 * s4 - 1);
                    }

                    var c11 = 0.7156 - 0.5313 * input.TangentRadiusRatio + 0.595 * input.TangentRadiusRatio * input.TangentRadiusRatio;
                    var c12 = 0.0796 + 0.0779 * input.TangentRadiusRatio;
                    var c13 = 1.587 + 0.049 * input.TangentRadiusRatio;
                    var c14 = 0.1122 + 0.1658 * input.TangentRadiusRatio;
                    cdh = (c11 - c12 * t1 * t1) * (1 / (z * z)) * Math.Pow(t1 * z, c13 + c14 * t1) + c18;
                    if (input.BoattailLengthCalibers <= 0)
                    {
                        cdbt = 0;
                    }
                    else if (mach <= 1.1)
                    {
                        var t2 = (1 - input.BaseDiameterCalibers) / (2 * input.BoattailLengthCalibers);
                        var t3 = 2 * t2 * t2 + t2 * t2 * t2;
                        var e1 = Math.Exp(-2 * input.BoattailLengthCalibers);
                        var b4 = 1 - e1 + 2 * t2 * (e1 * (input.BoattailLengthCalibers + 0.5) - 0.5);
                        cdbt = 2 * t3 * b4 * (1.774 - 9.3 * c15);
                    }
                    else
                    {
                        var t2 = (1 - input.BaseDiameterCalibers) / (2 * input.BoattailLengthCalibers);
                        var b3 = 0.85 / b;
                        var a12 = (5 * t1) / (6 * b) + Math.Pow(0.5 * t1, 2) - (0.7435 / m2) * Math.Pow(t1 * mach, 1.6);
                        var a11 = (1 - (0.6 * input.TangentRadiusRatio) / mach) * a12;
                        var e2 = Math.Exp((-1.1952 / mach) * (input.TotalLengthCalibers - input.NoseLengthCalibers - input.BoattailLengthCalibers));
                        var x3 = ((2.4 * m2 * m2 - 4 * b2) * t2 * t2) / (2 * b2 * b2);
                        var a1 = a11 * e2 - x3 + (2 * t2) / b;
                        var r5 = 1 / b3;
                        var e3 = Math.Exp(-b3 * input.BoattailLengthCalibers);
                        var a2 = 1 - e3 + 2 * t2 * (e3 * (input.BoattailLengthCalibers + r5) - r5);
                        cdbt = 4 * a1 * t2 * a2 * r5;
                    }
                }

                rows.Add(new McDragRow
                {
                    Mach = mach,
                    Cd0 = cdh + cdsf + cdbnd + cdbt + cdb,
                    Cdh = cdh,
                    Cdsf = cdsf,
                    Cdbnd = cdbnd,
                    Cdbt = cdbt,
                    Cdb = cdb,
                    PbOverPinf = pbOverPinf,
                });
            }

            var warnings = GetMcDragWarnings(input);
            return new McDragResult
            {
                Rows = rows,
                Warnings = warnings,
                LegacyReport = RenderMcDragLegacyReport(input, rows, warnings),
            };
        }

        public static McGyroResult CalculateMcGyro(McGyroInput rawInput)
        {
            // MCGYRO, APRIL 1986, BY R. L. MCCOY.
            // ESTIMATE OF GYROSCOPIC STABILITY FACTOR (SG) FOR A UNIFORM DENSITY PROJECTILE.
            // THE STANDARD DEVIATION OF THE SG ESTIMATE IS 5 PERCENT AT SUBSONIC AND
            // SUPERSONIC SPEEDS, AND 10 PERCENT AT TRANSONIC SPEEDS.
            var input = NormalizeMcGyro(rawInput ?? new McGyroInput());
            var s1 = Math.Sqrt(Math.Max(1 - input.MeplatDiameterCalibers, 0));
            var s2 = 1 - input.BaseDiameterCalibers * input.BaseDiameterCalibers;
            var rows = new List<McGyroRow>();
            foreach (var mach in McGyroMaches)
            {
                var m2 = mach * mach;
                double g1;
                if (mach <= 0.95)
                {
                    g1 = 20.082 + 3.726 * (mach / Math.Sqrt(Math.Max(1 - m2, 1e-12)));
                }
                else if (mach >= 1.1)
                {
                    g1 = 35.079 - 24.066 * (Math.Sqrt(m2 - 1) / mach);
                }
                else
                {
                    g1 = 71.73001 - 42.433 * mach;
                }

                var a1 = g1 * s2;
                double a;
                double b;
                // Subsonic and supersonic coefficient branches for SG and N15.
                if (mach < 1)
                {
                    var b1 = Math.Sqrt(Math.Max(1 - m2, 0));
                    b = 0.82112 + 0.36971 * b1;
                    a = 34.779 + (24.091 + (8.977 - 12.804 * input.TangentRadiusRatio + 8.38 * Math.Pow(input.TangentRadiusRatio, 2)) * input.NoseLengthCalibers) * s1 * b1 - a1;
                }
                else
                {
                    var b1 = Math.Sqrt(m2 - 1) / mach;
                    b = 1.0528 + 0.23379 * b1 - 0.004884 * (mach - 1);
                    a = 58.873 + (8.115 + (14.15 - 15.348 * input.TangentRadiusRatio + 7.216 * Math.Pow(input.TangentRadiusRatio, 2)) * input.NoseLengthCalibers) * s1 * b1 * b1 - a1;
                }

                var twistForSg15 = (a * Math.Sqrt(input.ProjectileDensityGramsPerCc)) / Math.Pow(input.TotalLengthCalibers, b);
                // Rifling twist rate required to give SG=1.5 is the slowest acceptable twist rate.
                var stabilityFactor = 1.5 * Math.Pow(twistForSg15 / input.RiflingTwistCalibersPerTurn, 2);
                rows.Add(new McGyroRow { Mach = mach, StabilityFactor = stabilityFactor, TwistForSg15 = twistForSg15 });
            }

            return new McGyroResult
            {
                Rows = rows,
                LegacyReport = RenderMcGyroLegacyReport(input, rows),
            };
        }

        public static IntLiftResult CalculateIntLift(IntLiftInput rawInput)
        {
            // PROGRAM INTLIFT.BAS, EDITED FOR TANDY 1000 HX (JULY 1990).
            // THIS PROGRAM IS A MODIFIED VERSION OF M. A. MORRIS' RARDLIFT CODE,
            // WITH CORRECTIONS FOR SMALL ARMS BULLETS AND MEDIUM CALIBER CANNON PROJECTILES.
            rawInput = rawInput ?? new IntLiftInput();
            var normalized = NormalizeGeometry(rawInput);
            var input = new IntLiftInput
            {
                ReferenceDiameterMm = normalized.ReferenceDiameterMm,
                TotalLengthCalibers = normalized.TotalLengthCalibers,
                NoseLengthCalibers = normalized.NoseLengthCalibers,
                TangentRadiusRatio = normalized.TangentRadiusRatio,
                BoattailLengthCalibers = normalized.BoattailLengthCalibers,
                BaseDiameterCalibers = normalized.BaseDiameterCalibers,
                MeplatDiameterCalibers = normalized.MeplatDiameterCalibers,
                ProjectileId = normalized.ProjectileId,
                CenterOfGravityCalibers = BallisticMath.IsFinite(rawInput.CenterOfGravityCalibers)
                    ? rawInput.CenterOfGravityCalibers
                    : normalized.TotalLengthCalibers / 2,
            };
            var warnings = new List<string>();
            if (input.TotalLengthCalibers > 7)
            {
                warnings.Add("PROJECTILE TOO LONG FOR ACCURATE ESTIMATES.");
            }

            if (input.NoseLengthCalibers < 1.5)
            {
                warnings.Add("NOSE LENGTH TOO SHORT FOR ACCURATE ESTIMATES.");
            }

            // CONVERT MCDRAG UNITS TO PROGRAM UNITS.
            var d = input.ReferenceDiameterMm / 1000;
            var l3 = input.TotalLengthCalibers - input.NoseLengthCalibers;
            var l1 = input.NoseLengthCalibers;
            var l2 = input.BoattailLengthCalibers;
            var f1 = input.BaseDiameterCalibers * d;
            var f2 = input.MeplatDiameterCalibers * d;
            var g1 = input.CenterOfGravityCalibers;
            var noseType = input.TangentRadiusRatio < 0.1 ? 3 : input.TangentRadiusRatio > 0.8 ? 2 : 1;
            // Set nose shape parameters and shift the center of gravity to the program nose datum.
            var l10 = l1;
            var t1 = (l10 * l10 + Math.Pow((1 - f2 / d) / 2, 2)) / (1 - f2 / d);
            if (noseType != 3)
            {
                l1 = Math.Sqrt(Math.Max(t1 - 0.25, 1e-12));
                if (noseType == 1)
                {
                    var l11 = (d / (d - f2)) * l10;
                    l1 = (l1 + l11) / 2;
                }
            }
            else
            {
                l1 = (d / (d - f2)) * l10;
            }

            var f3 = l1 - l10;
            g1 += f3;
            var d1 = f1 / d;
            var rows = new List<IntLiftRow>();

            foreach (var mach in IntLiftMaches)
            {
                var b = Math.Sqrt(Math.Abs(mach * mach - 1));
                var b1 = Math.Sqrt(Math.Abs(mach * mach - 0.9025));
                double c1;
                // CALCULATION OF BODY LIFT WITH NO BOATTAIL.
                if (mach > 1.19)
                {
                    c1 = 1.974 + (0.921 * b) / l1;
                }
                else
                {
                    var a = mach * mach / l1;
                    if (a > 0.4)
                    {
                        c1 = (0.12 * a - 0.064) * l3 - 0.227 * a + 2.2865;
                    }
                    else if (a > 0.35)
                    {
                        c1 = (0.104 - 0.3 * a) * l3 + 0.573 * a + 1.9665;
                    }
                    else if (a > 0.25)
                    {
                        c1 = (0.43 * a - 0.1515) * l3 + 2.202 - 0.1 * a;
                    }
                    else
                    {
                        c1 = 0.856 * a - 0.044 * l3 + 1.963;
                    }
                }

                // SUBSONIC LIFT LOSS DUE TO THE PRESENCE OF A BOATTAIL.
                // SUPERSONIC BOATTAIL LIFT LOSS.
                double c2;
                if (mach < 0.951 && l2 >= 0.48)
                {
                    c2 = b1 * (3.115 + 15.083 * l2 * l2 - 21.106 * l2);
                    c2 = c2 + 71.14601 * l2 - 47.3 * l2 * l2 - 18.303;
                    c2 = c2 * Math.Pow(d1, 0.75) * (1 - Math.Pow(d1, 0.75));
                }
                else if (b1 != 0 && l2 / b1 <= 3)
                {
                    c2 = (1 - d1 * d1) * (2 - Math.Pow(3 - l2 / b1, 3.2439) / 17.649);
                }
                else
                {
                    c2 = (1 - d1 * d1) * 2;
                }

                if (mach >= 0.951 && mach <= 2)
                {
                    if (mach <= 1.4)
                    {
                        c2 *= 0.5 * mach + 0.41;
                    }
                    else
                    {
                        c2 *= 1.3662 - 0.1833 * mach;
                    }
                }

                if (mach >= 1.01)
                {
                    var l4 = 0.34 + 0.25 / b;
                    if (l2 >= l4)
                    {
                        c2 = c2 * (1 - Math.Pow(1 - (1 - d1) * l4 / l2, 2)) / (1 - d1 * d1);
                    }
                }

                // TOTAL LIFT.
                var cla = c1 - c2;
                double c4;
                if (mach >= 1.01)
                {
                    if (b >= 1)
                    {
                        c4 = (0.82 - 0.15 * b) * l1 + 1.7 * b + 0.3;
                    }
                    else if (b >= 0.6)
                    {
                        c4 = (2.045 - 1.375 * b) * l1 + 4.575 * b - 2.575;
                    }
                    else
                    {
                        c4 = (0.166 * b + 1.12) * l1 + 0.05 * b + 0.14;
                    }
                }
                else if (b > 0.65)
                {
                    c4 = (1.304 - 0.846 * b) * l1 + 0.5 * b + 0.945;
                }
                else if (b > 0.3)
                {
                    c4 = (1.304 - 0.846 * b) * l1 + 2.286 * b - 0.216;
                }
                else
                {
                    c4 = (1.12 - 0.233 * b) * l1 + 1.1 * b + 0.14;
                }

                if (noseType == 3)
                {
                    c4 = (0.667 * c4) / 0.557;
                }
                else if (noseType == 2)
                {
                    c4 = (0.456 * c4) / 0.557;
                }

                // BOATTAIL LIFT CENTER FROM NOSE.
                var x1 = (mach == 1 ? l2 / 2 : (0.66 - (0.041 * l2) / b) * l2) + l1 + l3 - l2;
                var c5 = x1 * c2;
                var c6 = c4 - c5;
                // OVERALL LIFT CENTER AFT OF NOSE.
                var x2 = c6 / cla;
                var x3 = g1 - x2;
                var cma = cla * x3;
                var u7 = mach < 0.951 ? 0.73 + 0.163 * mach : mach == 1 ? 0.84 : 0.82;
                cma *= u7;

                var a1 = 1 / (l1 / 2 + l3 - l2 + (l2 * (1 + d1)) / 2);
                // CALCULATION OF YAW DRAG.
                double cda2;
                if (mach > 1.3)
                {
                    cda2 = 9.825 - 3.95 * mach + (0.1458 * mach - 0.1594) * cla * cla / a1;
                }
                else if (mach > 1)
                {
                    cda2 = 9.467 * mach - 7.617 + (0.606 - 0.443 * mach) * cla * cla / a1;
                }
                else if (mach > 0.8)
                {
                    cda2 = 1.85 + (0.3825 * mach - 0.2195) * cla * cla / a1;
                }
                else
                {
                    cda2 = 1.476 + 0.467 * mach + 0.08649999 * cla * cla / a1;
                }

                if (mach <= 2 && mach > 1.25)
                {
                    cda2 *= 0.133 * mach + 0.784;
                }
                else if (mach <= 1.25 && mach >= 1)
                {
                    cda2 *= 1.2 - 0.2 * mach;
                }
                else if (mach > 2)
                {
                    cda2 *= 1.41 - 0.18 * mach;
                }

                if (mach <= 0.95 && mach > 0.9)
                {
                    cda2 *= 2 * mach - 0.9;
                }
                else if (mach <= 0.9 && mach >= 0.8)
                {
                    cda2 *= 1.8 - mach;
                }

                cda2 *= 1.33;

                double u1;
                if (mach <= 1)
                {
                    u1 = 0.96 + 0.038 * mach;
                }
                else if (mach <= 1.11)
                {
                    u1 = 1;
                }
                else
                {
                    u1 = 1.09 - 0.057 * mach;
                }

                // Final empirical correction to CLA.
                if (l3 < 1.5)
                {
                    u1 -= 0.12 * Math.Pow(1.5 - l3, 2);
                }

                var u2 = mach <= 0.95
                    ? (0.22 * mach * mach) / Math.Sqrt(Math.Max(1 - mach * mach, 1e-12))
                    : 0.431 / mach - 0.1;
                var u3 = u1 + u2 * Math.Pow(l2 / d1, 2);
                cla *= u3;

                rows.Add(new IntLiftRow { Mach = mach, Cla = cla, Cma = cma, Cda2 = cda2 });
            }

            return new IntLiftResult
            {
                Rows = rows,
                Warnings = warnings,
                LegacyReport = RenderIntLiftLegacyReport(input, rows, warnings),
            };
        }

        static JbmProjectileGeometryInput NormalizeGeometry(JbmProjectileGeometryInput input)
        {
            // Normalize free-form UI input before feeding the McCoy estimate equations.
            return new JbmProjectileGeometryInput
            {
                ReferenceDiameterMm = BallisticMath.Positive(input.ReferenceDiameterMm, 0.001),
                TotalLengthCalibers = BallisticMath.Positive(input.TotalLengthCalibers, 0.001),
                NoseLengthCalibers = BallisticMath.Positive(input.NoseLengthCalibers, 0.001),
                TangentRadiusRatio = BallisticMath.IsFinite(input.TangentRadiusRatio) ? input.TangentRadiusRatio : 0,
                BoattailLengthCalibers = BallisticMath.NonNegative(input.BoattailLengthCalibers),
                BaseDiameterCalibers = BallisticMath.Positive(input.BaseDiameterCalibers, 0.001),
                MeplatDiameterCalibers = Math.Min(Math.Max(BallisticMath.IsFinite(input.MeplatDiameterCalibers) ? input.MeplatDiameterCalibers : 0, 0), 0.999999),
                ProjectileId = string.IsNullOrEmpty(input.ProjectileId) ? "Projectile" : input.ProjectileId,
            };
        }

        static McDragInput NormalizeMcDrag(McDragInput rawInput)
        {
            var geometry = NormalizeGeometry(rawInput);
            return new McDragInput
            {
                ReferenceDiameterMm = geometry.ReferenceDiameterMm,
                TotalLengthCalibers = geometry.TotalLengthCalibers,
                NoseLengthCalibers = geometry.NoseLengthCalibers,
                TangentRadiusRatio = geometry.TangentRadiusRatio,
                BoattailLengthCalibers = geometry.BoattailLengthCalibers,
                BaseDiameterCalibers = geometry.BaseDiameterCalibers,
                MeplatDiameterCalibers = geometry.MeplatDiameterCalibers,
                ProjectileId = geometry.ProjectileId,
                RotatingBandDiameterCalibers = BallisticMath.Positive(rawInput.RotatingBandDiameterCalibers, 1),
                CenterOfGravityCalibers = BallisticMath.IsFinite(rawInput.CenterOfGravityCalibers) ? rawInput.CenterOfGravityCalibers : 0,
                BoundaryLayer = rawInput.BoundaryLayer,
            };
        }

        static McGyroInput NormalizeMcGyro(McGyroInput rawInput)
        {
            var geometry = NormalizeGeometry(rawInput);
            return new McGyroInput
            {
                ReferenceDiameterMm = geometry.ReferenceDiameterMm,
                TotalLengthCalibers = geometry.TotalLengthCalibers,
                NoseLengthCalibers = geometry.NoseLengthCalibers,
                TangentRadiusRatio = geometry.TangentRadiusRatio,
                BoattailLengthCalibers = geometry.BoattailLengthCalibers,
                BaseDiameterCalibers = geometry.BaseDiameterCalibers,
                MeplatDiameterCalibers = geometry.MeplatDiameterCalibers,
                ProjectileId = geometry.ProjectileId,
                ProjectileDensityGramsPerCc = BallisticMath.Positive(rawInput.ProjectileDensityGramsPerCc, 0.001),
                RiflingTwistCalibersPerTurn = BallisticMath.Positive(rawInput.RiflingTwistCalibersPerTurn, 0.001),
            };
        }

        static List<string> GetMcDragWarnings(McDragInput input)
        {
            // Original warning branches from MCDRAG input validation.
            var warnings = new List<string>();
            if (input.NoseLengthCalibers < 1)
            {
                warnings.Add("NOSE TOO SHORT. CDH IS TOO HIGH AT TRANSONIC AND SUPERSONIC SPEEDS.");
            }

            if (input.MeplatDiameterCalibers > 0.5)
            {
                warnings.Add("NOSE TOO BLUNT. CDH IS TOO HIGH AT TRANSONIC AND SUPERSONIC SPEEDS.");
            }

            if (input.BoattailLengthCalibers >= 1.5)
            {
                warnings.Add("BOATTAIL TOO LONG. CDBT AND CDB MAY BE INCORRECT.");
            }

            if (input.BaseDiameterCalibers < 0.65)
            {
                warnings.Add("BOATTAIL TOO STEEP. CDBT AND CDB MAY BE INCORRECT.");
            }
            else if (input.BaseDiameterCalibers > 1.35)
            {
                warnings.Add("CONICAL FLARE TAIL TOO STEEP. CDBT AND CDB MAY BE INCORRECT.");
            }

            return warnings;
        }

        static List<string> RenderMcDragLegacyReport(McDragInput input, List<McDragRow> rows, List<string> warnings)
        {
            // PRINT MCDRAG OUTPUT.
            var lines = new List<string>();
            lines.Add("MCDRAG, DECEMBER 1974, R. L. MCCOY");
            lines.Add("");
            lines.Add($"PROJECTILE IDENTIFICATION: {input.ProjectileId}");
            lines.Add("");
            lines.Add("REF. DIA.  TOTAL LENGTH  NOSE LENGTH  RT/R  BOATTAIL LENGTH  BASE DIA.  MEPLAT DIA.  BAND DIA.  XCG  BOUND. LAYER");
            lines.Add($"{BallisticText.ToJsString(input.ReferenceDiameterMm)} {BallisticText.ToJsString(input.TotalLengthCalibers)} {BallisticText.ToJsString(input.NoseLengthCalibers)} {BallisticText.ToJsString(input.TangentRadiusRatio)} {BallisticText.ToJsString(input.BoattailLengthCalibers)} {BallisticText.ToJsString(input.BaseDiameterCalibers)} {BallisticText.ToJsString(input.MeplatDiameterCalibers)} {BallisticText.ToJsString(input.RotatingBandDiameterCalibers)} {BallisticText.ToJsString(input.CenterOfGravityCalibers)} {BallisticOptions.ToLegacyCode(input.BoundaryLayer)}");
            lines.Add("");
            lines.Add("M      CD0    CDH    CDSF   CDBND  CDBT   CDB    PB/PINF");
            foreach (var row in rows)
            {
                lines.Add($"{BallisticText.Fixed(row.Mach, 3).PadLeft(5)} {BallisticText.Fixed(row.Cd0, 3).PadLeft(6)} {BallisticText.Fixed(row.Cdh, 3).PadLeft(6)} {BallisticText.Fixed(row.Cdsf, 3).PadLeft(6)} {BallisticText.Fixed(row.Cdbnd, 3).PadLeft(6)} {BallisticText.Fixed(row.Cdbt, 3).PadLeft(6)} {BallisticText.Fixed(row.Cdb, 3).PadLeft(6)} {BallisticText.Fixed(row.PbOverPinf, 3).PadLeft(8)}");
            }

            if (warnings.Count > 0)
            {
                lines.Add("");
                lines.AddRange(warnings);
            }

            return lines;
        }

        static List<string> RenderMcGyroLegacyReport(McGyroInput input, List<McGyroRow> rows)
        {
            // Print McGyro Output.
            var lines = new List<string>();
            lines.Add("MCGYRO, APRIL 1986, R. L. MCCOY.");
            lines.Add("");
            lines.Add($"PROJECTILE IDENTIFICATION: {input.ProjectileId}");
            lines.Add("");
            lines.Add("DREF  LT  LN  RT/R");
            lines.Add($"{BallisticText.ToJsString(input.ReferenceDiameterMm)} {BallisticText.ToJsString(input.TotalLengthCalibers)} {BallisticText.ToJsString(input.NoseLengthCalibers)} {BallisticText.ToJsString(input.TangentRadiusRatio)}");
            lines.Add("");
            lines.Add("LBT  DB  DM  RHOB  TWIST");
            lines.Add($"{BallisticText.ToJsString(input.BoattailLengthCalibers)} {BallisticText.ToJsString(input.BaseDiameterCalibers)} {BallisticText.ToJsString(input.MeplatDiameterCalibers)} {BallisticText.ToJsString(input.ProjectileDensityGramsPerCc)} {BallisticText.ToJsString(input.RiflingTwistCalibersPerTurn)}");
            lines.Add("");
            lines.Add("MACH NO.      SG       N (SG=1.5)");
            foreach (var row in rows)
            {
                lines.Add($"{BallisticText.Fixed(row.Mach, 2).PadLeft(6)} {BallisticText.Fixed(row.StabilityFactor, 2).PadLeft(10)} {BallisticText.Fixed(row.TwistForSg15, 2).PadLeft(12)}");
            }

            return lines;
        }

        static List<string> RenderIntLiftLegacyReport(IntLiftInput input, List<IntLiftRow> rows, List<string> warnings)
        {
            // PRINT INTLIFT OUTPUT.
            var lines = new List<string>();
            lines.Add("* * * INTERIM PROGRAM FOR CLA, CMA, AND CDA2 * * *");
            lines.Add("");
            lines.Add($"PROJECTILE IDENTIFICATION: {input.ProjectileId}");
            lines.Add("");
            lines.Add("DREF  LT  LN  RT/R");
            lines.Add($"{BallisticText.ToJsString(input.ReferenceDiameterMm)} {BallisticText.ToJsString(input.TotalLengthCalibers)} {BallisticText.ToJsString(input.NoseLengthCalibers)} {BallisticText.ToJsString(input.TangentRadiusRatio)}");
            lines.Add("");
            lines.Add("LBT  DB  DM  CGN");
            lines.Add($"{BallisticText.ToJsString(input.BoattailLengthCalibers)} {BallisticText.ToJsString(input.BaseDiameterCalibers)} {BallisticText.ToJsString(input.MeplatDiameterCalibers)} {BallisticText.ToJsString(input.CenterOfGravityCalibers)}");
            lines.Add("");
            lines.Add("MACH      CLA      CMA      CDA2");
            foreach (var row in rows)
            {
                lines.Add($"{BallisticText.Fixed(row.Mach, 2).PadLeft(6)} {BallisticText.Fixed(row.Cla, 2).PadLeft(8)} {BallisticText.Fixed(row.Cma, 2).PadLeft(8)} {BallisticText.Fixed(row.Cda2, 2).PadLeft(8)}");
            }

            if (warnings.Count > 0)
            {
                lines.Add("");
                lines.AddRange(warnings);
            }

            return lines;
        }
    }
}
