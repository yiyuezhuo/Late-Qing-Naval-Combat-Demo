using System;
using MathNet.Numerics.Distributions;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;
using Acornima.Ast;


namespace NavalCombatCore
{
    public enum HitPenDetType
    {
        PenetrateWithDetonate, // Class A
        PassThrough, // Class B
        NoPenetration // Class C
    }

    public enum RamType
    {
        None,
        Ram, // equipped with ram
        TrueRamShip // 
    }

    /// <summary>
    /// SK5 Table lookup and helpers
    /// </summary>
    public static class RuleChart
    {
        // Chart J1 - Hit Location - Warships
        public static float[,] broadAspectLocationWeightTable = new float[,]
        {// Short   Medium  Long/Extreme
            {2,     12,     25}, // 1H DECK
            {1,     3,      6},  // 2H TURRET
            {2,     4,      8},  // 3H SUPERSTR
            {4,     3,      3},  // 4V CON
            {26,    18,     16}, // 5V MAIN BELT
            {9,     8,      7},  // 6V BELT ENDS
            {19,    17,     11}, // 7V BARBETTE
            {17,    16,     11}, // 8V TURRET
            {19,    17,     12}, // 9V SUPERSTR
            {1,     1,      1}   // INEFFECTIVE
        };

        public static float[,] narrowAspectLocationWeightTable = new float[,]
        {// Short   Medium  Long/Extreme
            {4,     20,     34}, // 1H DECK
            {2,     3,      7},  // 2H TURRET
            {3,     9,      14}, // 3H SUPERSTR
            {6,     4,      3},  // 4V CON
            {7,     5,      5},  // 5V MAIN BELT
            {4,     3,      3},  // 6V BELT ENDS
            {28,    16,     6},  // 7V BARBETTE
            {23,    19,     13}, // 8V TURRET
            {22,    20,     14}, // 9V SUPERSTR
            {1,     1,      1}   // INEFFECTIVE
        };

        public static double[] GetLocationWeights(TargetAspect targetAspect, RangeBand rangeBand)
        {
            var table = targetAspect switch
            {
                TargetAspect.Broad => broadAspectLocationWeightTable,
                TargetAspect.Narrow => narrowAspectLocationWeightTable,
                _ => broadAspectLocationWeightTable
            };
            var colIdx = Math.Min(table.GetLength(1) - 1, (int)rangeBand);
            var rows = table.GetLength(0);
            var weights = new double[rows];
            for (int rowIdx = 0; rowIdx < table.GetLength(0); rowIdx++)
                weights[rowIdx] = table[rowIdx, colIdx];
            return weights;
        }

        public static ArmorLocation RollArmorLocation(TargetAspect targetAspect, RangeBand rangeBand)
        {
            var idx = Categorical.Sample(GetLocationWeights(targetAspect, rangeBand));
            return (ArmorLocation)idx;
        }


        // Chart J6 - Close Range Fire Control
        public static float[,] closeRangeFireControlTable = new float[,]
        {//     0-2000      2001-4500 (distance yards)
         //Broad  Narrow  Broad   Narrow
            {15,    12,     12,     10},
            {13,    9,      10,     8},
            {11,    8,      9,      7},
            { 9,    7,      8,      6},
            { 8,    6,      7,      5}
        };
        public static float[] closeRangeFireControlTableRows = new float[] { 9, 18, 27, 36, float.MaxValue };
        public static float GetCloseRangeFireControlScore(float distanceYards, float speedKnots, TargetAspect targetAspect)
        {
            var colIdx = (distanceYards > 2000 ? 2 : 0) + (targetAspect == TargetAspect.Broad ? 0 : 1);
            var rowIdx = closeRangeFireControlTableRows.Select((s, i) => (s, i)).Where(si => speedKnots <= si.s).First().i;
            return closeRangeFireControlTable[rowIdx, colIdx];
        }

        // Chart I1 - Surface Gunfire combat resolution (first row, other row is direct derivation from Binomial Distribution)
        public static float GetHitProbP100(float fireControlScore)
        {
            // Shell=1 row
            // 0.25~1% for 0-3 (+0.25% / FCS)
            // 1%-9% for 3-19 (+0.5% / FCS)
            // 9%-20% for 19-30 (+1% / FCS)
            fireControlScore = Math.Clamp(fireControlScore, 0, 30);
            if (fireControlScore <= 3)
                return 0.25f + fireControlScore * 0.25f;
            if (fireControlScore <= 19)
                return 1 + (fireControlScore - 3) * 0.5f;
            return 9 + (fireControlScore - 19) * 1f;
        }

        public class SimpleTable<TRow, TCol, TCell>
        {
            public TRow[] rows;
            public TCol[] cols;
            public TCell[,] cells;

            public static SimpleTable<TRow, TCol, TCell> FromCSV(string text, Func<string, TRow> rowExtractor, Func<string, TCol> colExtractor, Func<string, TCell> cellExtractor)
            {
                var lines = text.Split("\n").Select(s => s.Trim().Split(",").ToList()).ToList();
                var cols = lines[0].Skip(1).Select(colExtractor).ToArray();
                var rows = lines.Skip(1).Select(line => rowExtractor(line[0])).ToArray();

                var nrows = lines.Count - 1;
                var ncols = lines[0].Count - 1;
                var cells = new TCell[nrows, ncols];
                for (var i = 0; i < nrows; i++)
                {
                    for (var j = 0; j < ncols; j++)
                    {
                        cells[i, j] = cellExtractor(lines[i + 1][j + 1]);
                    }
                }
                return new SimpleTable<TRow, TCol, TCell>()
                {
                    rows = rows,
                    cols = cols,
                    cells = cells
                };
            }
        }

        // Chart J4 Raw
        public static string penetrationTableHighExplosiveShellsText = @",26.5,26,25.5,25,24.5,24,23.5,23,22.5,22,21.5,21,20.5,20,19.5,19,18.5,18,17.5,17,16.5,16,15.5,15,14.5,14,13.5,13,12.5,12,11.5,11,10.5,10,9.5,9,8.5,8,7.5,7,6.5,6,5.5,5,4.5,4,3.5,3,2.5,2,1.5,1,0.5,0
18,3.6,3.5,3.5,3.4,3.4,3.3,3.3,3.2,3.2,3.2,3.1,3.1,3.1,3,3,3,3,2.9,2.9,2.9,2.9,2.9,2.9,2.9,2.9,2.9,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8,2.8
16,4,3.8,3.7,3.6,3.5,3.4,3.3,3.3,3.2,3.1,3.1,3,3,2.9,2.9,2.8,2.8,2.8,2.7,2.7,2.7,2.6,2.6,2.6,2.6,2.6,2.6,2.6,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5,2.5
15,4.4,4.2,4.1,3.9,3.8,3.7,3.5,3.4,3.3,3.2,3.1,3.1,3,2.9,2.9,2.8,2.7,2.7,2.7,2.6,2.6,2.6,2.5,2.5,2.5,2.5,2.4,2.4,2.4,2.4,2.4,2.4,2.4,2.4,2.4,2.4,2.4,2.3,2.3,2.3,2.3,2.3,2.3,2.3,2.3,2.3,2.3,2.3,2.3,2.3,2.3,2.3,2.3,2.3
14,5.1,4.9,4.7,4.4,4.3,4.1,3.9,3.7,3.6,3.5,3.3,3.2,3.1,3,2.9,2.8,2.8,2.7,2.6,2.6,2.5,2.5,2.4,2.4,2.4,2.4,2.3,2.3,2.3,2.3,2.3,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2,2.2
13.5,5.7,5.4,5.1,4.9,4.6,4.4,4.2,4,3.8,3.7,3.5,3.4,3.2,3.1,3,2.9,2.8,2.7,2.7,2.6,2.5,2.5,2.4,2.4,2.3,2.3,2.3,2.3,2.2,2.2,2.2,2.2,2.2,2.2,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2.1
13,6.4,6,5.7,5.4,5.1,4.8,4.6,4.3,4.1,3.9,3.7,3.6,3.4,3.3,3.1,3,2.9,2.8,2.7,2.6,2.5,2.5,2.4,2.4,2.3,2.3,2.2,2.2,2.2,2.2,2.1,2.1,2.1,2.1,2.1,2.1,2.1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2
12,8.5,7.9,7.4,7,6.5,6.1,5.7,5.4,5,4.7,4.5,4.2,4,3.7,3.5,3.4,3.2,3,2.9,2.8,2.7,2.6,2.5,2.4,2.3,2.2,2.2,2.1,2.1,2.1,2,2,2,2,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9,1.9
11,,,,,,8.5,7.9,7.3,6.8,6.3,5.8,5.4,5,4.7,4.4,4.1,3.8,3.6,3.4,3.2,3,2.8,2.7,2.5,2.4,2.3,2.2,2.1,2.1,2,2,1.9,1.9,1.8,1.8,1.8,1.8,1.8,1.8,1.7,1.7,1.7,1.7,1.7,1.7,1.7,1.7,1.7,1.7,1.7,1.7,1.7,1.7,1.7
10,,,,,,,,,,,,7.8,7.2,6.6,6.1,5.6,5.1,4.7,4.3,4,3.7,3.4,3.2,2.9,2.8,2.6,2.4,2.3,2.2,2.1,2,1.9,1.8,1.8,1.7,1.7,1.7,1.6,1.6,1.6,1.6,1.6,1.6,1.6,1.6,1.6,1.6,1.6,1.6,1.6,1.6,1.6,1.6,1.6
9.2,,,,,,,,,,,,,,,,,6.9,6.3,5.7,5.2,4.7,4.3,3.9,3.6,3.3,3,2.8,2.6,2.4,2.2,2.1,2,1.9,1.8,1.7,1.7,1.6,1.6,1.5,1.5,1.5,1.5,1.5,1.5,1.4,1.4,1.4,1.4,1.4,1.4,1.4,1.4,1.4,1.4
8,,,,,,,,,,,,,,,,,,,,,,,6.3,5.6,5,4.5,4,3.6,3.2,2.9,2.6,2.3,2.1,2,1.8,1.7,1.6,1.5,1.4,1.4,1.4,1.3,1.3,1.3,1.3,1.3,1.3,1.3,1.2,1.2,1.2,1.2,1.2,1.2
7.5,,,,,,,,,,,,,,,,,,,,,,,,,,5.9,5.2,4.5,4,3.5,3.1,2.8,2.5,2.2,2,1.8,1.7,1.6,1.5,1.4,1.3,1.3,1.2,1.2,1.2,1.2,1.2,1.2,1.2,1.2,1.2,1.2,1.2,1.2
6,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,4.8,4,3.4,2.9,2.4,2.1,1.8,1.6,1.4,1.2,1.1,1.1,1,1,1,0.9,0.9,0.9,0.9,0.9,0.9,0.9
5.5,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,4.2,3.5,2.9,2.4,2,1.7,1.5,1.3,1.1,1,1,0.9,0.9,0.9,0.9,0.9,0.9,0.9,0.9,0.9
5,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,3.9,3.2,2.6,2.1,1.7,1.4,1.2,1.1,1,0.9,0.8,0.8,0.8,0.8,0.8,0.8,0.8,0.8
4,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,3.2,2.4,1.9,1.5,1.2,1,0.8,0.7,0.7,0.6,0.6,0.6,0.6,0.6,0.6
3,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,2.5,1.7,1.2,0.9,0.7,0.6,0.5,0.5,0.5,0.5,0.5";

        public static SimpleTable<float, float, float> penetrationTableHighExplosiveShellsTable = SimpleTable<float, float, float>.FromCSV(
            penetrationTableHighExplosiveShellsText,
            float.Parse, float.Parse, x => x == "" ? -1 : float.Parse(x)
        );

        public static float ResolvePenetrationHEPenetration(float boreInch, float apPenetrationInch)
        {
            var t = penetrationTableHighExplosiveShellsTable;
            var row = t.rows.Select((threshold, index) => (threshold, index)).FirstOrDefault(r => boreInch >= r.threshold);
            var col = t.cols.Select((threshold, index) => (threshold, index)).FirstOrDefault(r => apPenetrationInch >= r.threshold);
            var cell = t.cells[row.index, col.index];
            if (cell != -1)
                return cell;
            for (var j = col.index + 1; j < t.cols.Length; j++)
            {
                if (t.cells[row.index, j] != -1)
                    return t.cells[row.index, j];
            }
            return 0;
        }

        // K1 - First Part
        public static Dictionary<(AmmunitionType, AmmunitionType), float> adjustPenetrationTable = new()
        {
            {(AmmunitionType.ArmorPiercing, AmmunitionType.SemiArmorPiercing), 0.75f},
            {(AmmunitionType.ArmorPiercing, AmmunitionType.Common), 0.5f},
            {(AmmunitionType.SemiArmorPiercing, AmmunitionType.Common), 0.66f},
        };

        public static float GetAdjustedPenetrationByType(AmmunitionType baseAmmoType, float basePenetationInch, float boreInch, AmmunitionType currentAmmoType)
        {
            if (baseAmmoType == currentAmmoType)
                return basePenetationInch;
            if (currentAmmoType == AmmunitionType.HighExplosive)
            {
                return ResolvePenetrationHEPenetration(boreInch, basePenetationInch);
            }
            if (adjustPenetrationTable.TryGetValue((baseAmmoType, currentAmmoType), out var percent))
            {
                return basePenetationInch * percent;
            }
            return basePenetationInch;
        }



        public static Dictionary<ArmorLocation, ArmorLocationAngleType> armorLocationToAngleType = new()
        {
            {ArmorLocation.Deck, ArmorLocationAngleType.Horizontal},
            {ArmorLocation.TurretHorizontal, ArmorLocationAngleType.Horizontal},
            {ArmorLocation.SuperStructureHorizontal, ArmorLocationAngleType.Horizontal},
            {ArmorLocation.ConningTower, ArmorLocationAngleType.Vertical},
            {ArmorLocation.MainBelt, ArmorLocationAngleType.Vertical},
            {ArmorLocation.BeltEnd, ArmorLocationAngleType.Vertical},
            {ArmorLocation.Barbette, ArmorLocationAngleType.Vertical},
            {ArmorLocation.TurretVertical, ArmorLocationAngleType.Vertical},
            {ArmorLocation.SuperStructureVertical, ArmorLocationAngleType.Vertical},
        };

        // K2 - Pass-Through Check  - Armored & K3 - Penetration Results - Unarmored
        public class AmmoDetonateProbRecord
        {
            public float armored;
            public float unarmored;
        }
        public static Dictionary<AmmunitionType, AmmoDetonateProbRecord> ammoDetonateProbMap = new()
        {
            {AmmunitionType.ArmorPiercing, new() {armored = 0.3f, unarmored = 0.2f}},
            {AmmunitionType.SemiArmorPiercing, new() {armored = 0.5f, unarmored = 0.4f}},
            {AmmunitionType.Common, new() {armored = 0.7f, unarmored = 0.7f}},
            {AmmunitionType.HighExplosive, new() {armored = 0.9f, unarmored = 0.9f}},
        };

        public static HitPenDetType ResolveHitPenDetType(float effPenInch, float effArmorInch, AmmunitionType ammoType)
        {
            var unarmored = effArmorInch <= 0.5f;
            if (!unarmored)
            {
                if (effPenInch < effArmorInch)
                    return HitPenDetType.NoPenetration;
                else if (effPenInch >= effArmorInch && effPenInch < effArmorInch * 2)
                    return HitPenDetType.PenetrateWithDetonate;
            }
            var _detonateProb = ammoDetonateProbMap[ammoType];
            var detonateProb = unarmored ? _detonateProb.unarmored : _detonateProb.armored;
            if (RandomUtils.rand.NextDouble() <= detonateProb)
                return HitPenDetType.PenetrateWithDetonate;
            return HitPenDetType.PassThrough;
        }

        // K4 - Shell Damage Factors
        public static string shellDamageFactorsCsvText = @"Damage Factor,A - AP - 1,A - AP - 2,A - AP -3,A - AP - 4,A - SAP - 1,A - SAP - 2,A - SAP - 3,A - COM - 1,A - COM - 2,A - HE - 1,A - HE - 2,Class A - DE,B - All,Class B - DE,C - AP,C - SAP,C - COM,C - HE,Class C - DE
5,7,15,18,28,16,19,29,20,21,23,25,38,10,13,9,8,8,7,0
6,10,20,20,30,20,20,30,20,30,30,30,40,10,14,11,10,9,8,0
7,10,20,20,40,20,30,40,30,30,30,40,42,15,15,10,10,10,10,0
8,10,20,30,40,30,30,50,30,30,40,40,44,15,15,15,15,10,10,0
9,15,30,30,50,30,30,50,40,40,40,50,46,20,16,15,15,15,10,0
10,15,30,40,60,30,40,60,40,40,50,50,47,20,17,20,15,15,15,0
11,15,30,40,60,40,40,60,40,50,50,60,49,20,17,20,20,15,15,0
12,20,40,40,70,40,50,70,50,50,50,60,50,25,17,20,20,20,15,0
13,20,40,50,70,40,50,70,50,50,60,70,51,25,18,25,20,20,15,0
14,20,40,50,80,50,50,80,50,60,60,70,52,30,18,25,20,20,20,0
15,25,50,50,80,50,60,90,60,60,70,80,53,30,19,25,25,25,20,0
16,25,50,60,90,50,60,90,60,70,70,80,54,30,19,30,25,25,20,8
17,25,50,60,90,60,60,100,70,70,80,90,55,35,19,30,25,25,25,8
18,25,50,60,100,60,70,100,70,80,80,90,56,35,20,30,30,25,25,8
19,30,60,70,100,60,70,110,70,80,90,100,57,40,20,35,30,30,25,9
20,30,60,70,110,70,80,120,80,80,90,100,58,40,20,35,30,30,25,9
22,35,70,80,120,70,80,130,90,90,100,110,60,45,21,40,35,35,30,9
24,35,70,80,130,80,90,140,90,100,110,120,61,50,21,40,40,35,30,9
26,40,80,90,140,80,100,150,100,110,120,130,63,50,22,45,40,40,35,9
28,40,80,100,150,90,110,160,110,120,130,140,64,55,22,50,45,40,40,10
30,45,90,110,170,100,110,170,120,130,140,150,66,60,23,55,50,45,40,10
32,50,100,110,180,100,120,180,120,130,140,160,67,65,23,55,50,50,45,10
34,50,100,120,190,110,130,200,130,140,150,170,68,70,24,60,55,50,45,10
36,55,110,130,200,120,140,210,140,150,160,180,69,70,24,65,60,55,50,10
38,55,110,130,210,120,140,220,150,160,170,190,70,75,25,65,60,55,50,11
40,60,120,140,220,130,150,230,160,170,180,200,71,80,25,70,65,60,55,11
42,65,130,150,230,140,160,240,160,180,190,210,73,85,25,75,65,65,60,11
44,65,130,150,240,140,170,250,170,180,200,220,74,90,26,75,70,65,60,11
46,70,140,160,250,150,170,260,180,190,210,230,75,90,26,80,75,70,60,11
48,70,140,170,260,160,180,280,190,200,220,240,76,95,26,85,75,70,65,11
50,75,150,180,280,160,190,290,200,210,230,250,76,100,27,90,80,75,70,11
52,80,160,180,290,170,200,300,200,220,230,260,77,105,27,90,85,80,70,12
54,80,160,190,300,180,200,310,210,230,240,270,78,110,27,95,85,80,75,12
56,85,170,200,310,180,210,320,220,240,250,280,79,110,28,100,90,85,75,12
58,85,170,200,320,190,220,330,230,240,260,290,80,115,28,100,95,85,80,12
60,90,180,210,330,200,230,350,230,250,270,300,81,120,28,105,95,90,80,12
62,95,190,220,340,200,230,360,240,260,280,310,82,125,29,110,100,95,85,12
64,95,190,220,350,210,240,370,250,270,290,320,82,130,29,110,100,95,85,12
66,100,200,230,360,210,250,380,260,280,300,330,83,135,29,115,105,100,90,12
68,100,200,240,370,220,260,390,270,290,310,340,84,140,29,120,110,100,90,15
70,105,210,250,390,230,260,400,270,290,320,350,85,145,30,125,110,105,95,15
72,110,220,250,400,230,270,410,280,300,320,360,86,150,30,125,115,110,95,15
74,110,220,260,410,240,280,430,290,310,330,370,86,155,30,130,120,110,100,15
76,115,230,270,420,250,290,440,300,320,340,380,87,160,30,135,120,115,105,15
78,115,230,270,430,250,290,450,300,330,350,390,87,165,31,135,125,115,105,15
80,120,240,280,440,260,300,460,310,340,360,400,88,170,31,140,130,120,110,18
82,125,250,290,450,270,310,470,320,340,370,410,89,175,31,145,130,125,110,18
84,125,250,290,460,270,320,480,330,350,380,420,89,180,31,145,135,125,115,18";

        public static SimpleTable<float, string, float> shellDamageFactorsTable = SimpleTable<float, string, float>.FromCSV(shellDamageFactorsCsvText,
            float.Parse, s => s, float.Parse
        );

        public class ShellDamageResult
        {
            [XmlAttribute]
            public float damagePoint;

            // [XmlAttribute]
            // public bool causeDamageEffect;

            [XmlAttribute]
            public float damageEffectProb;

            public override string ToString()
            {
                return $"(DP={damagePoint}, Prob of DE={damageEffectProb})";
            }
        }

        static Dictionary<AmmunitionType, (int, double[])> classAHitWeights = new()
        {
            { AmmunitionType.ArmorPiercing, (0, new double[] { 5, 30, 45, 25 } )},
            { AmmunitionType.SemiArmorPiercing, (4, new double[] { 25, 50, 25 } )},
            { AmmunitionType.Common, (7, new double[] { 50, 50 } )},
            { AmmunitionType.HighExplosive, (9, new double[] { 25, 75 } )}
        };

        public static ShellDamageResult ResolveShellDamageResult(float damageFactor, HitPenDetType hitPenDetType, AmmunitionType ammoType)
        {
            var row = shellDamageFactorsTable.rows.Select((value, index) => (value, index)).Last(r => damageFactor >= r.value);
            var damagePoint = 0f;
            // var causeDamageEffect = false;
            double damageEffectProb = 0;
            switch (hitPenDetType)
            {
                case HitPenDetType.PenetrateWithDetonate:
                    var (baseOffset, subOffsetWeights) = classAHitWeights[ammoType];
                    var colIdx = baseOffset + Categorical.Sample(subOffsetWeights);

                    damagePoint = shellDamageFactorsTable.cells[row.index, colIdx];
                    damageEffectProb = shellDamageFactorsTable.cells[row.index, 11] * 0.01;
                    // causeDamageEffect = RandomUtils.rand.NextDouble() < damageEffectProb;
                    break;
                case HitPenDetType.PassThrough:
                    damagePoint = shellDamageFactorsTable.cells[row.index, 12];
                    damageEffectProb = shellDamageFactorsTable.cells[row.index, 13] * 0.01;
                    // causeDamageEffect = RandomUtils.rand.NextDouble() < damageEffectProb;
                    break;
                case HitPenDetType.NoPenetration:
                    damagePoint = shellDamageFactorsTable.cells[row.index, 14 + (int)ammoType];
                    damageEffectProb = shellDamageFactorsTable.cells[row.index, 18] * 0.01;
                    // causeDamageEffect = RandomUtils.rand.NextDouble() < damageEffectProb;
                    break;
            }

            return new()
            {
                damagePoint = damagePoint,
                // causeDamageEffect = causeDamageEffect,
                damageEffectProb = (float)damageEffectProb
            };
        }


        // W2 - RF Battery Damage
        public static string rapidFiringBatteryDamageTableCsvText = @"RF Battery Rating,01-25,26-75,76-00
2,3,4,5
3,4,6,7
4,5,8,9
5,6,9,11
6,8,11,13
7,9,13,15
8,10,15,18
9,11,17,20
10,13,19,22
11,14,21,24
12,15,23,26
13,16,24,28
14,18,26,31
15,19,28,33
16,20,30,35
17,21,32,37
18,23,34,39
19,24,36,42
20,25,38,44
21,26,39,46
22,28,41,48
23,29,43,50
24,30,45,53
25,31,47,55";

        public static SimpleTable<float, string, float> rapidFiringBatteryDamageTable = SimpleTable<float, string, float>.FromCSV(
            rapidFiringBatteryDamageTableCsvText,
            float.Parse, x => x, float.Parse
        );

        public static float RollRapidFireBatteryDamage(float rapidFiringBatteryRating)
        {
            var rowIdx = rapidFiringBatteryDamageTable.rows.Select((r, i) => (r, i)).LastOrDefault(ri => rapidFiringBatteryRating >= ri.r).i;
            var colIdx = Categorical.Sample(new double[] { 25, 50, 25 });
            return rapidFiringBatteryDamageTable.cells[rowIdx, colIdx];
        }

        // T5 - Torpedo Damage Table
        public static string torpedoDamageTableCsvText = @",A - Con,A - Mag,B - Con,B - Mag,C - Con,C - Mag,D - Con,D - Mag,E - Con,E - Mag,F - Con,F - Mag,G - Con,G - Mag,H - Con,H - Mag,I - Con
8,1650,2250,1485,2025,1320,1800,1155,1575,1005,1365,855,1170,727,990,640,792,590
7,1485,2025,1337,1823,1188,1620,1040,1418,904,1229,770,1053,654,891,575,713,525
8,1375,1875,1238,1688,1100,1500,963,1313,837,1138,713,975,606,825,533,660,485
10,1265,1725,1139,1553,1012,1380,886,1208,770,1047,656,897,557,759,490,607,445
10,1210,1650,1089,1485,968,1320,847,1155,737,1001,627,858,533,726,468,581,425
12,1155,1575,1040,1418,924,1260,809,1103,703,956,599,819,509,693,448,554,405
10,1100,1500,990,1350,880,1200,770,1050,670,910,570,780,485,660,427,528,390
10,1045,1425,941,1283,836,1140,732,998,636,865,542,741,460,627,405,502,370
8,990,1350,891,1215,792,1080,693,945,603,819,513,702,436,594,384,475,350
9,935,1275,842,1148,748,1020,655,893,569,774,485,663,412,561,362,449,330
8,825,1125,743,1013,660,900,578,788,502,683,428,585,363,495,320,396,295";

        public static SimpleTable<float, string, float> torpedoDamageTable = SimpleTable<float, string, float>.FromCSV(
            torpedoDamageTableCsvText,
            float.Parse, x => x, float.Parse
        );

        public static float RollTorpedoDamage(TorpedoDamageClass damageClass, TorpedoPistolType pistolType)
        {
            var colIdx = Math.Min(torpedoDamageTable.cols.Length - 1, (int)damageClass * 2 + (int)pistolType);
            var rowIdx = Categorical.Sample(torpedoDamageTable.rows.Select(x => (double)x).ToArray());
            return torpedoDamageTable.cells[rowIdx, colIdx];
        }

        public static float[,] armorAdjustmentTable = new float[,]
        {
            { 0,   1.4f, 2.5f, 3.5f, 4.5f, 5.5f, 6.5f, 8.0f, 9.5f, 11.0f, 12.5f, 14.0f},
            { 25,   50,   75,   100,  125,  150,  200,  250,  300,   350,   400,   450}
        };

        public static float GetArmorAdjustment(float armorEffInch)
        {
            var colIdx = Enumerable.Range(0, armorAdjustmentTable.GetLength(1)).Where(col => armorAdjustmentTable[0, col] <= armorEffInch).Last();
            return armorAdjustmentTable[1, colIdx];
        }

        // N1 - Fighting Shipbpard Fires
        public static float[,] fightingShipboardFireTableNoDamageControlApplied = new float[,]
        {
            {3, -20},
            {12, -10},
            {50, 0},
            {85, 10},
            {95, 30},
            {float.MaxValue, 40}
        };

        public static float[,] fightingShipboardFireTableDamageControlApplied = new float[,]
        {
            // 5 is not handled here
            { 15, -30},
            { 45, -20},
            { 75, -10},
            { 90, 0},
            {float.MaxValue, +10}
        };

        public static float ResolveFightingShipBoardFiresDelta(float severity, bool damageControlApplied, float d100Offset = 0)
        {
            var d100 = RandomUtils.D100F() + d100Offset;
            if (damageControlApplied && d100 < 5)
                return 0;
            var table = damageControlApplied ? fightingShipboardFireTableDamageControlApplied : fightingShipboardFireTableNoDamageControlApplied;
            var r = Enumerable.Range(0, table.GetLength(0)).First(r => d100 <= table[r, 0]);
            return Math.Clamp(severity + table[r, 1], 0, 100);
        }

        // N2 - Shipboard Fire Damage Effects
        public static float[,] shipboardFireDamageEffectsTable = new float[,]
        {
            {50, 20},
            {60, 25},
            {70, 30},
            {80, 35},
            {90, 40},
            {float.MaxValue, 50}
        };

        public static bool ResolveShipboardFireDamageEffect(float severity)
        {
            var t = shipboardFireDamageEffectsTable;
            var r = Enumerable.Range(0, t.GetLength(0)).First(r => severity <= t[r, 0]);
            var probPercent = t[r, 1];
            return RandomUtils.D100F() <= probPercent;
        }

        // L1 - Damage Determination - Warships 1880 to 1905
        public static string damageDeterminationTableWarships1880to1905CsvText = @"Roll,Deck (1),Turret (2/8),Superst (3/9),Con (4),Belt M. (5),Belt E (6),Barbette (7),General (G),Fires (F),Torpedo (T)
2,100,100,104,107,100,120,100,*601,501,100
2,100,100,104,107,101,121,100,*601,501,100
2,100,100,104,107,102,121,101,*602,501,101
2,101,101,107,108,106,123,101,*602,501,102
2,101,101,107,108,116,123,101,603,502,112
2,101,101,107,112,116,125,102,603,502,112
2,104,101,108,112,117,125,102,603,502,117
2,104,103,108,112,117,126,102,603,502,117
2,104,103,108,124,117,126,102,*604,503,120
2,106,103,113,124,118,127,103,*604,503,121
2,106,103,113,124,118,130,103,*604,503,122
2,110,104,124,133,118,133,103,*604,503,123
2,110,104,124,133,120,134,103,*604,506,123
2,110,104,127,140,120,134,105,605,506,123
2,113,105,127,140,120,134,105,605,506,123
2,113,105,128,140,121,135,105,605,506,125
2,114,106,128,140,123,135,107,*606,507,125
2,114,106,129,141,123,135,108,*606,507,126
2,116,107,129,141,123,147,109,*606,507,126
2,116,108,129,143,123,149,109,607,507,126
2,116,109,130,143,123,151,109,607,507,126
2,117,109,130,143,124,151,110,607,508,133
2,117,109,132,143,125,152,110,608,508,133
2,117,110,132,144,125,154,110,608,509,134
2,118,110,140,144,125,154,111,608,509,134
2,118,110,140,144,126,154,111,608,509,135
2,118,110,141,145,126,155,112,*609,510,                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  
2,120,111,141,145,126,155,112,*609,510,147
2,120,111,143,145,126,157,114,*609,510,152
2,120,111,143,146,146,167,114,*609,511,152
2,121,111,143,146,150,167,114,610,511,154
2,121,112,144,147,151,168,115,610,511,167
2,125,112,145,147,152,168,115,610,512,167
2,125,113,145,147,152,169,115,611,512,168
2,125,113,148,153,154,169,115,611,513,168
2,125,113,148,153,156,170,146,612,513,169
2,127,114,148,153,167,171,146,612,514,169
2,128,114,155,156,168,171,147,612,514,170
2,130,115,155,156,168,171,147,612,514,171
2,133,115,156,156,169,172,149,*613,515,172
2,133,115,156,157,170,172,149,*613,515,173
2,146,115,157,158,172,173,153,614,515,173
2,149,132,158,158,172,173,153,614,516,176
2,150,147,158,159,173,173,170,*615,516,176
2,152,147,159,160,176,175,170,*615,516,181
2,152,149,160,160,177,180,170,616,516,181
2,153,149,160,160,179,180,176,616,517,182
2,153,160,163,163,180,182,176,617,517,182
2,166,176,166,163,182,183,182,617,517,182
2,174,176,178,176,183,183,182,617,517,183";

        public static SimpleTable<double, string, string> damageDeterminationTableWarships1880to1905 = SimpleTable<double, string, string>.FromCSV(damageDeterminationTableWarships1880to1905CsvText,
            double.Parse, x => x, x => x
        );

        public static string ResolveDamageEffectId(DamageEffectCause cause)
        {
            var colIdx = Math.Min((int)cause, damageDeterminationTableWarships1880to1905.cols.Length - 1);
            var rowIdx = Categorical.Sample(damageDeterminationTableWarships1880to1905.rows);
            return damageDeterminationTableWarships1880to1905.cells[rowIdx, colIdx];
        }

        // public class DamageTierPercentProbRecord
        // {
        //     public float percent;
        //     public float damagePointProb;
        // }

        public static float[,] damageTierPercentProbRecords = new float[,]
        {
            {0, 0.0f, 0f},
            {1, 0.1f, 70}, // Tier 1, cross to tier 1 (10%) from tier 0 will have 70% chance to get a general DE
            {2, 0.2f, 80}, // // Tier 2, cross to tier 2 (20%) from tier 1 will have 80% chance to get a general DE
            {3, 0.3f, 75},
            {4, 0.4f, 85},
            {5, 0.5f, 80},
            {6, 0.59f, 90},
            {7, 0.68f, 85},
            {8, 0.77f, 95},
            {9, 0.86f, 90},
            {10, 0.95f, 100},
            {10, 1.01f, 100}
        };

        public static int GetDamageTier(float p)
        {
            var t = damageTierPercentProbRecords;
            var r = Enumerable.Range(0, t.GetLength(0)).Last(r => p >= t[r, 1]);
            return (int)t[r, 0];
        }

        // M4 Morale Check
        public static float[,] moraleCheckTable = new float[,]
        {// Tier    -1,  0 or +1,   +2,  +3
            {0,     0,     0,       0,   0},
            {7,     12,    8,       4,   4}, // Tier 7, for crew rating -1~3 percentage to abandon ship
            {8,     24,    16,      10,  4},
            {9,     36,    24,      15,  8},
            {10,    48,    32,      20,  15}
        };


        // Crossing Tier Damage Effects, "instant sunk" and morale checks  
        public static void ResolveCrossingDamageTierDamageEffects(float p1, float p2, int crewRating, out int damageEffectTier, out int damageEffectCount, out bool sinking, out bool abandonShip)
        {
            var t = damageTierPercentProbRecords;
            var r1 = Enumerable.Range(0, t.GetLength(0)).Last(r => p1 >= t[r, 1]);
            var r2 = Enumerable.Range(0, t.GetLength(0)).Last(r => p2 >= t[r, 1]);
            damageEffectTier = (int)t[r2, 0];

            damageEffectCount = 0;
            abandonShip = false;
            for (int r = r1 + 1; r <= r2; r++)
            {
                if (RandomUtils.D100F() <= t[r, 2])
                {
                    damageEffectCount++;
                }

                var row = Enumerable.Range(0, moraleCheckTable.GetLength(0)).FirstOrDefault(row => r == moraleCheckTable[row, 0]);
                if (row > 0)
                {
                    var col = 1;
                    if (crewRating >= 3)
                        col = 4;
                    else if (crewRating >= 2)
                        col = 3;
                    else if (crewRating >= 0)
                        col = 2;
                    var moraleCheckPercentage = moraleCheckTable[row, col];
                    if (RandomUtils.D100F() <= moraleCheckPercentage)
                    {
                        abandonShip = true;
                    }
                }
            }

            sinking = r2 - r1 >= 8;
        }

        public static string rammingTargetSpeedFactorTableCsvText = @"SPEED (kts),20,30,40,50,60,70,80,90,100,110,120,130,140,150,160
2,2,2,2,2,2,2,2,2,-2,-2,-2,-2,-2,-2,-2
4,4,4,4,4,4,2,2,2,-2,-2,-2,-4,-4,-4,-4
6,6,6,6,4,4,4,2,2,-2,-4,-4,-4,-6,-6,-6
8,8,8,8,6,6,4,2,2,-2,-4,-4,-6,-8,-8,-8
10,10,10,8,8,6,4,2,2,-2,-4,-6,-8,-8,-10,-10
12,12,12,10,8,8,6,4,2,-4,-6,-6,-8,-10,-12,-12
14,14,14,12,10,8,6,4,2,-4,-6,-8,-10,-12,-14,-14
16,16,14,14,12,10,6,4,2,-4,-6,-8,-12,-14,-14,-16
18,18,16,14,12,10,8,4,2,-4,-8,-10,-12,-14,-16,-18
20,20,18,16,14,12,8,4,2,-4,-8,-10,-14,-16,-18,-20
22,22,20,18,16,12,8,4,2,-4,-8,-12,-16,-18,-20,-22
24,24,22,20,16,14,10,6,2,-6,-10,-12,-16,-20,-22,-24
26,26,24,20,18,14,10,6,2,-6,-10,-14,-18,-20,-24,-26
28,28,26,22,18,16,10,6,2,-6,-10,-14,-18,-22,-26,-28
30,30,26,24,20,16,12,6,2,-6,-12,-16,-20,-24,-26,-30
32,32,28,26,22,18,12,6,2,-6,-12,-16,-22,-26,-28,-32";

        public static SimpleTable<float, float, float> rammingTargetSpeedFactorTable = SimpleTable<float, float, float>.FromCSV(
            rammingTargetSpeedFactorTableCsvText,
            float.Parse, float.Parse, float.Parse
        );

        public static string rammingRamSpeedFactorTableCsvText = @"SPEED (kts),20,30,40,50,60,70,80,90,100,110,120,130,140,150,160
4,2,2,4,4,4,4,4,4,4,4,4,4,4,2,2
6,4,4,4,6,6,6,6,6,6,6,6,6,4,4,4
8,4,4,6,8,8,8,8,8,8,8,8,8,6,4,4
10,4,6,8,8,10,10,10,10,10,10,10,8,8,6,4
12,6,6,8,10,12,12,12,12,12,12,12,10,8,6,6
14,6,8,10,12,14,14,14,14,14,14,14,12,10,8,6
16,6,8,12,14,14,16,16,16,16,16,14,14,12,8,6
18,8,10,12,14,16,18,18,18,18,18,16,14,12,10,8
20,8,10,14,16,18,20,20,20,20,20,18,16,14,10,8
22,8,12,16,18,20,22,22,22,22,22,20,18,16,12,8
24,10,12,16,20,22,24,24,24,24,24,22,20,16,12,10
26,10,14,18,20,24,26,26,26,26,26,24,20,18,14,10
28,10,14,18,22,26,28,28,28,28,28,26,22,18,14,10
30,12,16,20,24,26,30,30,30,30,30,26,24,20,16,12
32,12,16,22,26,28,32,32,32,32,32,28,26,22,16,12";

        public static SimpleTable<float, float, float> rammingRamSpeedFactorTable = SimpleTable<float, float, float>.FromCSV(
            rammingRamSpeedFactorTableCsvText,
            float.Parse, float.Parse, float.Parse
        );

        public static string rammingDamageFactorTableCsvText = @"FACTOR,20,30,40,50,60,70,80,90,100,110,120,130,140,150,160
2,0.02,0.03,0.04,0.05,0.05,0.06,0.06,0.06,0.06,0.06,0.05,0.05,0.04,0.03,0.02
4,0.04,0.06,0.07,0.09,0.1,0.1,0.11,0.11,0.11,0.1,0.1,0.09,0.07,0.06,0.04
6,0.05,0.08,0.1,0.12,0.14,0.15,0.16,0.16,0.16,0.15,0.14,0.12,0.1,0.08,0.05
8,0.07,0.1,0.13,0.15,0.17,0.19,0.2,0.2,0.2,0.19,0.17,0.15,0.13,0.1,0.07
10,0.08,0.12,0.15,0.18,0.21,0.22,0.23,0.24,0.23,0.22,0.21,0.18,0.15,0.12,0.08
12,0.09,0.14,0.18,0.21,0.24,0.26,0.27,0.27,0.27,0.26,0.24,0.21,0.18,0.14,-1
14,0.1,0.15,0.2,0.23,0.26,0.29,0.3,0.3,0.3,0.29,0.26,0.23,0.2,0.15,-1
16,0.11,0.17,0.21,0.26,0.29,0.31,0.33,0.33,0.33,0.31,0.29,0.26,0.21,-1,-1
18,0.12,0.18,0.23,0.28,0.31,0.34,0.35,0.36,0.35,0.34,0.31,0.28,0.23,-1,-1
20,0.13,0.19,0.25,0.29,0.33,0.36,0.38,0.38,0.38,0.36,0.33,0.29,0.25,-1,-1
22,0.14,0.2,0.26,0.31,0.35,0.38,0.4,0.41,0.4,0.38,0.35,0.31,-1,-1,-1
24,0.15,0.21,0.28,0.33,0.37,0.4,0.42,0.43,0.42,0.4,0.37,0.33,-1,-1,-1
26,0.15,0.22,0.29,0.34,0.39,0.42,0.44,0.45,0.44,0.42,0.39,-1,-1,-1,-1
28,0.16,0.23,0.3,0.36,0.4,0.44,0.46,0.47,0.46,0.44,-1,-1,-1,-1,-1
30,0.17,0.24,0.31,0.37,0.42,0.45,0.48,0.48,0.48,0.45,-1,-1,-1,-1,-1
32,0.17,0.25,0.32,0.38,0.43,0.47,0.49,0.5,-1,-1,-1,-1,-1,-1,-1
34,0.18,0.26,0.33,0.39,0.45,0.48,0.51,0.52,-1,-1,-1,-1,-1,-1,-1
36,0.18,0.26,0.34,0.41,0.46,0.5,0.52,-1,-1,-1,-1,-1,-1,-1,-1
38,0.19,0.27,0.35,0.42,0.47,0.51,0.53,-1,-1,-1,-1,-1,-1,-1,-1
40,0.19,0.28,0.36,0.43,0.48,0.52,-1,-1,-1,-1,-1,-1,-1,-1,-1
42,0.19,0.28,0.36,0.43,0.49,0.53,-1,-1,-1,-1,-1,-1,-1,-1,-1
44,0.2,0.29,0.37,0.44,0.5,0.54,-1,-1,-1,-1,-1,-1,-1,-1,-1
46,-1,-1,0.38,0.45,0.51,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1
48,-1,-1,0.39,0.46,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1";

        public static SimpleTable<float, float, float> rammingDamageFactorTable = SimpleTable<float, float, float>.FromCSV(
            rammingDamageFactorTableCsvText,
            float.Parse, float.Parse, float.Parse
        );

        public class RamResolutionResult
        {
            public float inflictToRammerDamagePoint;
            public float inflictToTargetDamagePoint;
        }

        public class RammingResolutionParameter
        {
            public float rammerDamagePoint;
            public float impactAngleDeg;
            public RamType ramType;
            public float targetArmorActualInch;
            public float targetSpeedKnots;
            public float rammerSpeedKnots;
            public float targetBuiltYear;
            public bool isTargetSubmarine;
            public bool isTargetNonWarship;
            public bool isRammerNonWarship;
            public bool isRammerUnarmored;

            // X4 - ramming - target adjustement (left)
            public float[,] rammingTargetAdjustmentsTable = new float[,]
            {//Armor  adjustments
                {0,      0},
                {0.1f,  -1},
                {1,     -2},
                {3,     -3},
                {5,     -4},
                {8,     -5},
                {11,    -6},
                {15,    -7},
            };

            public RamResolutionResult Resolve()
            {
                var x1 = rammingTargetSpeedFactorTable;
                var x1Row = Enumerable.Range(0, x1.rows.Length).Where(r => targetSpeedKnots <= x1.rows[r]).DefaultIfEmpty(x1.rows.Length - 1).First();
                var x1Col = Enumerable.Range(0, x1.cols.Length).Where(c => impactAngleDeg <= x1.cols[c]).DefaultIfEmpty(x1.cols.Length - 1).First();
                var damageFactor1 = x1.cells[x1Row, x1Col];

                var x2 = rammingRamSpeedFactorTable;
                var x2Row = Enumerable.Range(0, x2.rows.Length).Where(r => rammerSpeedKnots <= x2.rows[r]).DefaultIfEmpty(x2.rows.Length - 1).First();
                var x2Col = Enumerable.Range(0, x2.cols.Length).Where(c => impactAngleDeg <= x2.cols[c]).DefaultIfEmpty(x2.cols.Length - 1).First();
                var damageFactor2 = x2.cells[x2Row, x2Col];

                var damageFactor = damageFactor1 + damageFactor2;

                var x4 = rammingTargetAdjustmentsTable;
                var x4Row = Enumerable.Range(0, x4.GetLength(0)).Where(r => x4[r, 0] <= targetArmorActualInch).LastOrDefault();
                var targetArmorAdjustment = (int)x4[x4Row, 1];

                var targetShipTypeAdjustment = 0;
                if (isTargetSubmarine)
                    targetShipTypeAdjustment = 1;
                else if (isTargetNonWarship)
                    targetShipTypeAdjustment = 3;
                else if (targetBuiltYear <= 1905)
                    targetShipTypeAdjustment = 2;
                else if (targetBuiltYear <= 1924)
                    targetShipTypeAdjustment = 1;

                var ramModifier = ramType switch
                {
                    RamType.Ram => 5,
                    RamType.TrueRamShip => 5, // True Ram Ship can reduce damage inflicted to itself but don't inclict more damage to target.
                    _ => 0
                };

                var rammerShipTypeAdjustment = isRammerNonWarship ? -3 : 0;

                var x3 = rammingDamageFactorTable;
                var x3Col = Enumerable.Range(0, x3.cols.Length).Where(c => impactAngleDeg <= x3.cols[c]).DefaultIfEmpty(x3.cols.Length - 1).First();

                var x3Row = Enumerable.Range(0, x3.rows.Length).Where(r => damageFactor <= x3.rows[r]).DefaultIfEmpty(x3.rows.Length - 1).First();
                x3Row += targetArmorAdjustment + targetShipTypeAdjustment + ramModifier + rammerShipTypeAdjustment;
                x3Row = Math.Clamp(x3Row, 0, x3.rows.Length - 1);
                var x3Value = x3.cells[x3Row, x3Col];
                if (x3Value == -1)
                {
                    x3Row = Enumerable.Range(0, x3.rows.Length).Where(r => x3.cells[r, x3Col] != -1).Last();
                    x3Value = x3.cells[x3Row, x3Col];
                }

                var inflictToTargetDamagePoint = rammerDamagePoint * x3Value;
                var rammerMod = 0;

                if (isTargetNonWarship)
                    rammerMod -= 3;
                else if (targetArmorActualInch <= 0.5)
                    rammerMod -= 1;
                else if (targetArmorActualInch <= 2)
                    rammerMod += 1;
                else if (targetArmorActualInch <= 6)
                    rammerMod += 2;
                else if (targetArmorActualInch <= 12)
                    rammerMod += 3;
                else
                    rammerMod += 4;

                if (ramType == RamType.None && isRammerNonWarship)
                    rammerMod -= 3;
                else if (ramType == RamType.Ram)
                    rammerMod -= 5;
                else if (ramType == RamType.TrueRamShip)
                    rammerMod -= 6;

                var x3Row2 = x3Row + rammerMod;
                x3Row2 = Math.Clamp(x3Row2, 0, x3.rows.Length - 1);
                var x3Value2 = x3.cells[x3Row2, x3Col];
                if (x3Value2 == -1)
                {
                    x3Row2 = Enumerable.Range(0, x3.rows.Length).Where(r => x3.cells[r, x3Col] != -1).Last();
                    x3Value2 = x3.cells[x3Row2, x3Col];
                }
                var inflictToRammerDamagePoint = inflictToTargetDamagePoint * x3Value2;

                return new()
                {
                    inflictToTargetDamagePoint = inflictToTargetDamagePoint,
                    inflictToRammerDamagePoint = inflictToRammerDamagePoint
                };
            }
        }
        
        // Chart C4
        public static string seaStateGunneryReductionTableCsvText = @"DP of ship,4,5,6,7,8,9,10
100,-2,-4,-6,-100,-100,-100,-100
200,-1,-3,-4,-5,-7,-100,-100
300,-1,-2,-3,-5,-6,-100,-100
400,0,-2,-3,-4,-5,-9,-100
500,0,-2,-3,-4,-5,-7,-100
600,0,-1,-2,-3,-4,-6,-100
700,0,-1,-2,-3,-4,-5,-100
800,0,-1,-2,-3,-3,-5,-100
1000,0,-1,-2,-2,-3,-5,-100
1200,0,-1,-2,-2,-3,-4,-9
1500,0,-1,-1,-2,-2,-4,-8
1900,0,-1,-1,-2,-2,-3,-7
2500,0,0,-1,-2,-2,-3,-7
3500,0,0,-1,-1,-2,-2,-6
5000,0,0,-1,-1,-2,-2,-6";

        public static SimpleTable<float, float, float> seaStateGunneryReductionTable = SimpleTable<float, float, float>.FromCSV(
            seaStateGunneryReductionTableCsvText,
            float.Parse, float.Parse, float.Parse
        );

        public static float ResolveSeaStateOffset(float displacementTons, int seaState, out bool blocked)
        {
            blocked = false;
            if (seaState < 4)
                return 0;
            var c4 = seaStateGunneryReductionTable;
            var row = Enumerable.Range(0, c4.rows.Length).DefaultIfEmpty(c4.rows.Length - 1).First(r => displacementTons <= c4.rows[r]);
            var col = Enumerable.Range(0, c4.cols.Length).LastOrDefault(c => c4.cols[c] <= seaState);
            var offset = seaStateGunneryReductionTable.cells[row, col];
            blocked = offset == -100;
            return offset;
        }
    }
}