using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace YYZ.Ballistic
{
    // Literal Facehard 6.9 translation track.
    //
    // This file intentionally keeps BASIC-era names and state. The goal is to build
    // an executable legacy kernel first, then refactor only after its behavior is
    // locked down. Line references point at References/Facehard69/*.BAS.
    public sealed class Facehard69LegacyInput
    {
        public double ARMOR;
        public double Q;
        public double QDAM;
        public double UB;
        public double CARTWL;
        public double CMPND;
        public double SOFTSHAT;
        public double THNCHL;
        public double THKTHN;
        public double TA;
        public double TEFF;
        public double D;
        public double WT;
        public double WB;
        public double OB;
        public double? VS;
        public double? WTSAVE;
        public double? CAPHD;
        public double? CAPHDRMV;
        public double? WCHWT;
        public double? WWT;
        public string noseCoveringState = "intact";
        public double? CURV;
        public double? WD;
        public double? CMT;
        public double? MTLBACK;
        public double? NBK;
        public double? BTP;
        public double? QBK;
        public double? NATN;
        public double? PRJTL;
        public double? SHAT;
        public double? BEND;
        public double? CARDONALD;
        public double? CRITAGL;
        public double? SHATRES;
        public double? BRAAK;
        public double? BRAIK;
        public double? APCAP;
        public double? LTCASE;
        public double? NSDAMAGL;
        public double? PLIM;
        public double? PDAM;
        public double? ALD;
        public double? BLD;
        public double? CLD;
        public double? AED;
        public double? BED;
        public double? CED;
    }

    public sealed class Facehard69LegacyState
    {
        // COMMON SHARED, FH69MAIN.BAS:80-99. Only the first converted slice is typed
        // here so the state can grow without forcing unrelated refactors.
        public double ARMOR, Q, QDAM, UB, UBCALC, CARTWL, CMPND, SOFTSHAT, THNCHL, THKTHN;
        public double TA, TEFF, D, WT, WB, OB, VS, OBRAD, SC, MO, MSHAT, VXP;
        public double VDFSTD, VDFUSED, VDFSTDWW1, VDFSTDWW2, VDFHARVEY, VDFBND, VDFBRK;
        public double BEND, CARDONALD, CRITAGL, SHATRES, BRAAK, BRAIK, APCAP, LTCASE, NSDAMAGL;
        public double PLIM, PDAM, ALD, BLD, CLD, AED, BED, CED, CART, THIN, TRUTHIN;
        public double MAXDIFF, SNCSMAX, OB45, EXMIN, SHAT, THVAL, THSPD, VLEXREV, VHEXREV;
        public double NATION, PROJ, WTSAVE, HARD, CAPHDLOSS, TD, TP, TPCAL, SOFTQPMAX;
        public double PPLUS, PLM, PDM, PNL, PNI, PNLPR, PNLSHAT, PSHMAX, POLMOD, POIMOD, LCMOD;
        public double CRTAPR, OBCRIT, SHATMULT, PENCONST, VLND, VLTRU, VHTRU, VHND, VHSHAT, VHSHATMAX;
        public double VLSHAT, VLSHATMAX, VITRU, VSCRIT, SHATVDF, VDFUSEDPR, SHATVDFPR, NSSHAT;
        public double CURV, WD, CMT, MTLBACK, NBK, BTP, QBK, PENFLG, VRAT, VRATVEL, VRATMIN;
        public double OB45CALC, VHDAM, VLDAM, VLMT, VHOL, EX, EXNBL, EXRAD, OBDF, TMPOBDF;
        public double CRTGD, VSCHECK, HF, BKEFF, RNDPLUGWT, NORMPLUGWT, DELTAPLUGWT, TOTPLUGWT;
        public double VNPLUG, VDPLUG, VR, VRPR, VRSHATNS, PENTP, VTOTAL, CAPHD, CAPHDRMV, WCHWT, WWT;
        public double MINSHVEL, SHATPRT, TOTHNFLG, MINEV, MINEV1, MINEV2, MINEV3, MINEV4, MINEV5;
        public double NOTEFLAG, NSFLG, CRITVEL, NOSEVEL, NVRFLAG, NSTEST, NSTESTV, NSVEL;
        public double DEN, CRVFLAG, CRVRL, BRK, BDYDM, NSBRK, NDAP;

        public string noseCoveringState = "intact";
        public string EFFPRINT1 = "", EFFPRINT2 = "", EFFVEL = "", PAND = "", HBLTONBL = "";
        public string NOTE1 = "", NOTE2 = "", NOTE3 = "", NOTE4 = "", NOTE5 = "";
        public string BDYDM1 = "", BDYDM2 = "", BDYDM3 = "", BDYDM4 = "", BDYDM5 = "", NSBRK1 = "";
        public string PEN1 = "", PEN2 = "", PEN3 = "", REMVEL = "Proj Remaining Velocity: ", RVU = "";
        public string BSNS1 = "", BSNS2 = "", BSNS3 = "", ONEPC = "", DPLG = "";
        public string WBL1 = "", WBL2 = "", WBL3 = "", WBL4 = "", FLAKE = "";
        public string N1 = "", N2 = "", N3 = "", N4 = "", H1 = "", H2 = "", H3 = "", H4 = "";
        public string RESNOTE = "", NBL1 = "", NBL2 = "", NBL3 = "", NBL4 = "", NBL5 = "", NBL6 = "", NBL7 = "";
        public string HBL1 = "", HBL2 = "", HBL3 = "", HBL4 = "", HBL5 = "", HBL6 = "", HBL7 = "", HBL8 = "", HBL9 = "";
        public string VELLTRU = "", VELLSHAT = "", VELLSHATMAX = "", VELLND = "";
        public string VELHTRU = "", VELHSHAT = "", VELHSHATMAX = "", VELHND = "";

        public double[] M = Array.Empty<double>();
        public double[] MS = Array.Empty<double>();
        public List<string> REPORT = new List<string>();
        public List<string> SECOND_PAGE_REPORT = new List<string>();
        public List<string> PROCESS_REPORT = new List<string>();
    }

    public sealed class Facehard69LegacyImpactOptions
    {
        public bool? softCapWorksInZomr;
    }

    public sealed class Facehard69LegacyRunOptions
    {
        public bool resolveArmorInfo = true;
        public bool renderReports = true;
    }

    public static class Facehard69Legacy
    {
        static readonly Dictionary<string, FieldInfo> StateFields = BuildFieldCache();

        public static int QbInt(double value)
        {
            return (int)Math.Floor(value);
        }

        public static double QbSqr(double value)
        {
            return Math.Sqrt(Math.Max(0d, value));
        }

        public static Facehard69LegacyState CreateState(Facehard69LegacyInput input)
        {
            return new Facehard69LegacyState
            {
                ARMOR = input.ARMOR,
                Q = input.Q,
                QDAM = input.QDAM,
                UB = input.UB,
                UBCALC = input.UB,
                CARTWL = input.CARTWL,
                CMPND = input.CMPND,
                SOFTSHAT = input.SOFTSHAT,
                THNCHL = input.THNCHL,
                THKTHN = input.THKTHN,
                TA = input.TA,
                TEFF = input.TEFF,
                D = input.D,
                WT = input.WT,
                WB = input.WB,
                OB = input.OB,
                VS = input.VS ?? 0,
                OBRAD = input.OB / 57.29578,
                M = new[] { 1d, 1.045d, 1.09d, 1.135d, 1.18d, 1.235d, 1.31d, 1.4d, 1.53d, 1.695d, 1.9d, 2.3d, 3.2d, 4.9d, 8d, 15d },
                MS = new[] { 1d, 1.002d, 1.0078d, 1.0176d, 1.0314d, 1.0495d, 1.0722d, 1.0994d, 1.1317d, 1.1672d, 1.2018d, 1.2377d, 1.2782d, 1.3236d, 1.3715d, 1.429d, 1.51d, 1.6036d },
                VXP = 1d / 1.21d,
                VDFSTDWW1 = 0.1256839d,
                VDFSTDWW2 = 0.09d,
                VDFHARVEY = 0.13d,
                VDFBRK = 0.02d,
                BEND = input.BEND ?? 0,
                CARDONALD = input.CARDONALD ?? 0,
                CRITAGL = input.CRITAGL ?? 0,
                SHATRES = input.SHATRES ?? 0,
                BRAAK = input.BRAAK ?? input.BRAIK ?? 0,
                BRAIK = input.BRAIK ?? 0,
                APCAP = input.APCAP ?? 0,
                LTCASE = input.LTCASE ?? 0,
                NSDAMAGL = input.NSDAMAGL ?? 0,
                PLIM = input.PLIM ?? 1,
                PDAM = input.PDAM ?? 1,
                ALD = input.ALD ?? 0,
                BLD = input.BLD ?? 0,
                CLD = input.CLD ?? 0,
                AED = input.AED ?? -1,
                BED = input.BED ?? -1,
                CED = input.CED ?? -1,
                CART = input.CARTWL,
                EXMIN = 0,
                SHAT = input.SHAT ?? 0,
                NATION = input.NATN ?? 0,
                PROJ = input.PRJTL ?? 0,
                WTSAVE = input.WTSAVE ?? input.WT,
                HARD = input.APCAP ?? 0,
                TD = input.TA * input.QDAM,
                TPCAL = input.TA / input.D,
                SOFTQPMAX = 1,
                PLM = 1,
                PDM = 1,
                PNL = 1,
                PNI = 1,
                PNLPR = 1,
                PNLSHAT = 1,
                PSHMAX = 1,
                POLMOD = 1,
                POIMOD = 1,
                OBCRIT = -1,
                SHATMULT = 1,
                CURV = input.CURV ?? 0,
                WD = input.WD ?? 0,
                CMT = input.CMT ?? 0,
                MTLBACK = input.MTLBACK ?? 0,
                NBK = input.NBK ?? 0,
                BTP = input.BTP ?? 0,
                QBK = input.QBK ?? 0,
                PENFLG = 2,
                VNPLUG = -1,
                VDPLUG = -1,
                VR = -1,
                VRPR = -1,
                VRSHATNS = -1,
                VTOTAL = -1,
                CAPHD = input.CAPHD ?? 0,
                CAPHDRMV = input.CAPHDRMV ?? 0,
                WCHWT = input.WCHWT ?? 0,
                WWT = input.WWT ?? 0,
                noseCoveringState = input.noseCoveringState ?? "intact"
            };
        }

        public static void ArmorBackSetup(Facehard69LegacyState s)
        {
            // FH69MAIN.BAS:216-256 backing/effective thickness setup.
            // PEN EFFECTIVE THICKNESS W/O BACKING.
            // PROJ DAMAGE EFFECTIVE THICKNESS.
            // CURVED-PLATE RULE DEFINED: CURVED FH PLATES ELIMINATE BODY DAMAGE TO STEEL PROJ AT
            // OB>45 DEG IF HBL <= VEL < NBL. IF SHATR OCCURS, RESULTS IN NOSE-ONLY SHATR INSTEAD OF
            // USUAL COMPLETE SHATR.
            // ENTER BACKING DATA. THEY ONLY INCREASE RESISTANCE, NOT PROJ DAMAGE.
            // ALL METAL BACKING PLATES ASSUMED IDENTICAL (USUAL DESIGN).
            s.TP = s.TA * s.Q;
            s.TD = s.TA * s.QDAM;
            if (s.MTLBACK == 0)
            {
                s.BKEFF = 0;
                s.BTP = 0;
                s.NBK = 0;
                s.QBK = 0;
            }
            else
            {
                if (s.NBK == 0) s.NBK = 1;
                if (s.BTP > 0) s.QBK = 0.5d + s.BTP / 10d;
                if (s.QBK == 0) s.QBK = 1;
                // DE MARRE SPACED ARMOR/2.
                s.BKEFF = 0.5d * Math.Pow(s.NBK * Math.Pow(s.MTLBACK * s.QBK / s.NBK, 1.4d), 0.714286d);
            }
            // TOTAL EFFECTIVE THICKNESS OF PLATE + BACKING.
            s.TEFF = s.TP + s.WD + s.CMT + s.BKEFF;
            s.TPCAL = s.TP / s.D;
        }

        public static void NoseCoverSetup(Facehard69LegacyState s)
        {
            // FH69MAIN.BAS:275-390, non-interactive nose-covering loss logic.
            // BEGIN NOSE COVERINGS LOST PRIOR TO FACE-HARDENED ARMOR IMPACT LOGIC.
            // ONLY POST-WWI JAP "DIVING" TYPE 88/91/1 AP PROJ HAD A REMOVABLE NOSE-TIP CALLED
            // A "CAP HEAD" (FLAT END UNDER IT). LOSS OF WINDSCREEN ALWAYS CAUSED LOSS OF
            // CAP HEAD, WHICH WAS HELD ON ONLY BY NOTCHED WINDSCREEN THREADS.
            // CAP HEAD WAS AP CAP TIP IN LARGER, CAPPED JAP TYPE 88/91/1 PROJ, SO LOSING
            // CAP HEAD KEPT MOST OF AP CAP.
            // UNCAPPED JAP TYPE 91 AP W/O CAP HEAD & WINDSCREEN REVERTS TO PRE-WWI UNCAPPED
            // AP & COMMON PROJ (DEFAULT #3).
            // "HOOD" IS THIN SOFT-AP-CAP-LIKE NOSE COVERING FOR SCREWING ON WINDSCREEN.
            // IT ACTS AS A LOW-GRADE SOFT AP CAP. A HOOD WILL ACT AS AN SOFT AP CAP,
            // BUT IF IMPACT IS BELOW THE NBL, THE PROJECTILE NOSE BREAKS UP NO MATTER WHAT.
            // INIT DEFAULT VALUES FOR POSSIBLE CHANGE.
            s.HARD = s.APCAP;
            s.WT = s.WTSAVE;

            if (s.noseCoveringState == "all-removed")
            {
                // DISCARD ALL NOSE COVERINGS; SET NO AP CAP FLAG.
                s.WT = s.WB;
                s.HARD = 0;
            }
            else if (s.CAPHD > 0 && (s.noseCoveringState == "caphead-removed" || s.noseCoveringState == "windscreen-removed"))
            {
                // JAP TYPE 88/91 AP PROJ LOGIC.
                s.CAPHDRMV = 1;
                if (s.CAPHD == 1)
                {
                    // UNCAPPED TYPE 91 AP PROJ (LOSES MOST OF NOSE; MUST ALSO CHANGE PROJ PEN/DAM PARAMETERS).
                    s.WT = s.WB;
                }
                else
                {
                    var wtDiff = s.WT - s.WB;
                    if (s.WCHWT >= wtDiff) s.WCHWT = 0;
                    s.WT -= s.WCHWT;
                }
            }
            else if (s.CAPHD == 0)
            {
                if (s.APCAP <= 0 && s.WT > s.WB && s.noseCoveringState == "all-removed")
                {
                    s.WT = s.WB;
                    s.HARD = 0;
                }
                if (s.WT > s.WB && s.noseCoveringState == "windscreen-removed")
                {
                    // WINDSCREEN LOSS ONLY LOGIC (MINOR WEIGHT LOSS EFFECT).
                    var wDiff = s.WT - s.WB;
                    if (s.WWT > wDiff || (s.APCAP != 0 && s.WWT == wDiff)) s.WWT = 0;
                    s.WT -= s.WWT;
                }
            }

            if (s.WT == s.WB)
            {
                // LOSS OF AP CAP ASSUMES LOSS OF ALL PROJ NOSE COVERINGS.
                // WHEN UNCAPPED TYPE 91 AP PROJ LOSES ITS CAP HEAD, SWITCH TO PRE-WWI UNCAPPED AP & COMMON PROJ PARAMETERS.
                if (s.CAPHD == 1 && s.PROJ == 10)
                {
                    Set(s, ("SHATRES", 1), ("APCAP", 0), ("NSDAMAGL", 5), ("PLIM", 0.795), ("PDAM", 0.7),
                        ("ALD", 0.000143), ("BLD", 2.249), ("CLD", 0.00267), ("CRITAGL", 0), ("BRAAK", 2),
                        ("BRAIK", 2), ("LTCASE", 0), ("AED", 0.000247), ("BED", 2.129), ("CED", 0.00172),
                        ("BEND", 0), ("CARDONALD", 0));
                }
                s.HARD = 0;
            }
            else
            {
                s.HARD = s.APCAP;
            }
        }

        static void SyncBraik(Facehard69LegacyState s)
        {
            s.BRAIK = s.BRAAK;
        }

        public static void AllProjData(Facehard69LegacyState s, int NATN, int PRJTL)
        {
            // ALLPROJDATA, FH69SBM1.BAS:47-453.
            s.NATION = NATN;
            s.PROJ = PRJTL;
            s.BEND = 0;
            s.CARDONALD = 0;

            void DEFCMN3() => Set(s, ("ALD", 0.000143), ("BLD", 2.249), ("CLD", 0.00267), ("CRITAGL", 0), ("BRAAK", 1), ("CARDONALD", 0), ("BEND", 0));
            void DEFCMN2() { Set(s, ("AED", 0.000247), ("BED", 2.129), ("CED", 0.00172)); DEFCMN3(); }
            void DEFAULT1() { Set(s, ("PLIM", 0.6), ("PDAM", 0.5), ("SHATRES", 2), ("APCAP", 0), ("NSDAMAGL", 5), ("LTCASE", 1), ("AED", -1), ("BED", -1), ("CED", -1)); DEFCMN3(); }
            void DEFAULT2() { Set(s, ("PLIM", 0.6), ("PDAM", 0.5), ("SHATRES", 2), ("APCAP", 1), ("NSDAMAGL", 5), ("LTCASE", 1)); DEFCMN2(); }
            void DEFCMN1() => Set(s, ("PLIM", 0.795), ("PDAM", 0.7), ("ALD", 0.0004301), ("BLD", 1.845), ("CLD", 0.0027), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222), ("CRITAGL", 0), ("CARDONALD", 0), ("BRAAK", 2), ("SHATRES", 1), ("NSDAMAGL", 15));
            void DEFAULT3() { Set(s, ("LTCASE", 1), ("APCAP", 0), ("BEND", 0)); DEFCMN1(); }
            void DEFAULT4() { Set(s, ("LTCASE", 1), ("APCAP", 1), ("BEND", 0)); DEFCMN1(); }
            void DEFAULT5() { Set(s, ("PLIM", 0.86), ("PDAM", 0.768), ("LTCASE", 0), ("SHATRES", 1), ("APCAP", -1), ("NSDAMAGL", 15)); DEFCMN2(); }
            void DEFAULT6() { Set(s, ("PLIM", 0.86), ("PDAM", 0.768), ("LTCASE", 0), ("SHATRES", 1), ("APCAP", 3), ("NSDAMAGL", 15)); DEFCMN2(); }
            void DEFAULT7() { Set(s, ("PLIM", 0.795), ("PDAM", 0.7), ("SHATRES", 1), ("APCAP", 3), ("NSDAMAGL", 15), ("LTCASE", 0)); DEFCMN2(); }
            void UCMN2() => Set(s, ("ALD", 0), ("BLD", 0), ("CLD", 0), ("AED", -1), ("BED", -1), ("CED", -1), ("BEND", 0), ("PDAM", -1));
            void UCMN() { Set(s, ("CARDONALD", 0)); UCMN2(); }
            void UCMN1() { Set(s, ("CARDONALD", 2)); UCMN2(); }
            void UCMN3() => Set(s, ("ALD", 0), ("BLD", 0), ("CLD", 0), ("AED", -1), ("BED", -1), ("CED", -1), ("BEND", 2), ("PDAM", -1), ("CARDONALD", 0));
            void BCMN1() => Set(s, ("ALD", 0.0004301), ("BLD", 1.845), ("CLD", 0.0027), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222), ("CRITAGL", 0), ("NSDAMAGL", 15), ("BEND", 0), ("SHATRES", 1));
            void BCMN2() => Set(s, ("CARDONALD", 0), ("BEND", 0), ("SHATRES", 0), ("APCAP", 2), ("LTCASE", 0), ("BRAAK", 0), ("NSDAMAGL", 20));
            void BCMN3() => Set(s, ("AED", -1), ("BED", -1), ("CED", -1), ("BEND", 1), ("BRAAK", 0), ("NSDAMAGL", 25));
            void BPR3() { Set(s, ("APCAP", 0), ("PLIM", 0.728), ("PDAM", 0.5), ("LTCASE", 2), ("BRAAK", 2), ("CARDONALD", 0)); BCMN1(); }
            void BPR4() { Set(s, ("APCAP", 1), ("PLIM", 0.728), ("PDAM", 0.5), ("LTCASE", 2), ("BRAAK", 2), ("CARDONALD", 0)); BCMN1(); }
            void BPR6() { Set(s, ("APCAP", 1), ("PLIM", 0.985), ("PDAM", 0.985), ("LTCASE", 0), ("BRAAK", 1), ("CARDONALD", 0)); BCMN1(); }
            void BPR7() { Set(s, ("APCAP", 1), ("PLIM", 1), ("PDAM", 1), ("LTCASE", 0), ("BRAAK", 0), ("CARDONALD", 2)); BCMN1(); }
            void BPR8() { Set(s, ("PLIM", 1), ("PDAM", 1), ("CRITAGL", 0), ("ALD", 0.0004301), ("BLD", 1.845), ("CLD", 0.0027), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222)); BCMN2(); }
            void BPR9() { Set(s, ("PLIM", 1.02), ("PDAM", 1.02), ("CRITAGL", 0), ("ALD", 0.000454554), ("BLD", 2.08917437), ("CLD", 0.00514125), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222)); BCMN2(); }
            void GCMN1() => Set(s, ("ALD", 0.000143), ("BLD", 2.249), ("CLD", 0.00267), ("AED", 0.000247), ("BED", 2.129), ("CED", 0.00172), ("CRITAGL", 0), ("NSDAMAGL", 15), ("CARDONALD", 0), ("BEND", 0));
            void GCMN3() => Set(s, ("ALD", 0.00006891), ("BLD", 2.26), ("CLD", 0.00333), ("AED", 0.0000971), ("BED", 2.283), ("CED", 0.0035), ("CRITAGL", 0), ("BRAAK", 1), ("SHATRES", 0), ("APCAP", 2), ("LTCASE", 0), ("CARDONALD", 0), ("BEND", 0));
            void FCMN() => Set(s, ("BRAAK", 1), ("SHATRES", 0), ("APCAP", 2), ("AED", -1), ("BED", -1), ("CED", -1), ("NSDAMAGL", 20), ("PLIM", 1), ("PDAM", 1), ("CARDONALD", 0), ("BEND", 0));
            void JCMN2() => Set(s, ("ALD", 0.0004301), ("BLD", 1.845), ("CLD", 0.0027), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222), ("SHATRES", 1), ("CRITAGL", 0), ("NSDAMAGL", 15), ("CARDONALD", 0), ("BEND", 0));
            void JCMN3() => Set(s, ("CARDONALD", 0), ("BEND", 0), ("SHATRES", 0), ("APCAP", 2), ("CRITAGL", 15), ("BRAAK", 1), ("NSDAMAGL", 20), ("LTCASE", 0));
            void JCMN4() => Set(s, ("ALD", 0.00336), ("BLD", 1.418), ("CLD", 0.0091701), ("BRAAK", 1), ("APCAP", 2), ("PLIM", 0.945), ("CARDONALD", 0), ("BEND", 0));
            void AHCMN1() => Set(s, ("PLIM", 0.728), ("PDAM", 0.5), ("LTCASE", 2), ("ALD", 0.0004301), ("BLD", 1.845), ("CLD", 0.0027), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222), ("CRITAGL", 0), ("BRAAK", 2), ("NSDAMAGL", 15), ("CARDONALD", 0), ("BEND", 0), ("SHATRES", 1));
            void RCMN1() => Set(s, ("CARDONALD", 0), ("BEND", 0), ("LTCASE", 0), ("BRAAK", 0), ("NSDAMAGL", 20));
            void RCMN2() => Set(s, ("ALD", 0.0004301), ("BLD", 1.845), ("CLD", 0.0027), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222), ("CRITAGL", 0), ("NSDAMAGL", 15), ("BEND", 0));

            if (NATN == 1)
            {
                switch (PRJTL)
                {
                    case 1: DEFAULT1(); return;
                    case 2: DEFAULT2(); return;
                    case 3: DEFAULT3(); return;
                    case 4: DEFAULT4(); return;
                    case 5: Set(s, ("CRITAGL", 5), ("BRAAK", 1), ("SHATRES", 0), ("APCAP", 0), ("NSDAMAGL", 15), ("PLIM", 0.795), ("LTCASE", 0)); UCMN3(); return;
                    case 6: Set(s, ("CRITAGL", 5), ("BRAAK", 1), ("SHATRES", 0), ("APCAP", 1), ("NSDAMAGL", 15), ("PLIM", 0.795), ("LTCASE", 0)); UCMN3(); return;
                    case 7: Set(s, ("PLIM", 0.748), ("PDAM", 0.6), ("LTCASE", 2), ("CRITAGL", 0), ("BRAAK", 2), ("SHATRES", 1), ("APCAP", 0), ("NSDAMAGL", 15), ("CARDONALD", 0), ("BEND", 0), ("ALD", 0.0004301), ("BLD", 1.845), ("CLD", 0.0027), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222)); return;
                    case 8: Set(s, ("CRITAGL", 5), ("BRAAK", 1), ("SHATRES", 0), ("APCAP", 1), ("NSDAMAGL", 15), ("PLIM", 0.89), ("LTCASE", 0)); UCMN1(); return;
                    case 9: Set(s, ("CRITAGL", 5), ("BRAAK", 0), ("SHATRES", 0), ("APCAP", 1), ("NSDAMAGL", 15), ("PLIM", 1), ("LTCASE", 0)); UCMN1(); return;
                    case 10: Set(s, ("CRITAGL", 10), ("BRAAK", 0), ("SHATRES", 0), ("APCAP", 2), ("NSDAMAGL", 15), ("PLIM", 0.94), ("LTCASE", 0)); UCMN(); return;
                    case 11: Set(s, ("CRITAGL", 20), ("BRAAK", 0), ("SHATRES", 0), ("APCAP", 2), ("NSDAMAGL", 20), ("PLIM", 0.94), ("LTCASE", 0)); UCMN1(); return;
                    case 12: Set(s, ("CRITAGL", 10), ("BRAAK", 0), ("SHATRES", 1), ("APCAP", 0), ("NSDAMAGL", 15), ("PLIM", 0.85), ("LTCASE", 1)); UCMN1(); return;
                    case 13: Set(s, ("CRITAGL", 15), ("BRAAK", 0), ("SHATRES", 1), ("APCAP", -1), ("NSDAMAGL", 15), ("PLIM", 0.9), ("LTCASE", 0)); UCMN1(); return;
                    case 14: Set(s, ("CRITAGL", 20), ("BRAAK", 0), ("SHATRES", 0), ("APCAP", -1), ("NSDAMAGL", 20), ("PLIM", 0.95), ("LTCASE", 0)); UCMN(); return;
                    case 15: Set(s, ("CRITAGL", 15), ("BRAAK", 0), ("SHATRES", 1), ("APCAP", 2), ("NSDAMAGL", 15), ("PLIM", 0.9), ("LTCASE", 0)); UCMN(); return;
                    case 16: Set(s, ("CRITAGL", 15), ("BRAAK", 0), ("SHATRES", 0), ("APCAP", 2), ("NSDAMAGL", 15), ("PLIM", 0.94), ("LTCASE", 0)); UCMN(); return;
                    case 17: Set(s, ("CRITAGL", 20), ("BRAAK", 0), ("SHATRES", 0), ("APCAP", 2), ("NSDAMAGL", 15), ("PLIM", 0.96), ("LTCASE", 0)); UCMN(); return;
                    case 18: Set(s, ("CRITAGL", 20), ("BRAAK", 0), ("SHATRES", 0), ("APCAP", 2), ("NSDAMAGL", 20), ("PLIM", 0.9), ("LTCASE", 0)); UCMN(); return;
                    case 19: Set(s, ("CRITAGL", 25), ("BRAAK", 0), ("SHATRES", 0), ("APCAP", 2), ("NSDAMAGL", 25), ("PLIM", 0.94), ("LTCASE", 0)); UCMN(); return;
                    case 20: Set(s, ("CRITAGL", 30), ("BRAAK", 0), ("SHATRES", 0), ("APCAP", 2), ("NSDAMAGL", 30), ("PLIM", 1), ("LTCASE", 0)); UCMN(); return;
                }
            }
            else if (NATN == 2)
            {
                switch (PRJTL)
                {
                    case 1: DEFAULT1(); return;
                    case 2: DEFAULT3(); return;
                    case 3: BPR3(); return;
                    case 4: BPR4(); return;
                    case 5: DEFAULT4(); return;
                    case 6: BPR6(); return;
                    case 7: BPR7(); return;
                    case 8: BPR8(); return;
                    case 9: BPR9(); return;
                    case 10: Set(s, ("PLIM", 1.02), ("PDAM", 1.02), ("CRITAGL", 15), ("ALD", 0), ("BLD", 0), ("CLD", 0), ("AED", -1), ("BED", -1), ("CED", -1)); BCMN2(); return;
                    case 11: Set(s, ("SHATRES", 1), ("APCAP", -1), ("PLIM", 0.9), ("PDAM", 0.9), ("ALD", 0.000184977), ("BLD", 2.46), ("CLD", 0.02549452), ("CRITAGL", -33), ("CARDONALD", 0), ("LTCASE", 1)); BCMN3(); return;
                    case 12: Set(s, ("SHATRES", 0), ("APCAP", 2), ("PLIM", 0.93), ("PDAM", 0.93), ("ALD", 0.000000122), ("BLD", 3.84), ("CLD", 0.001118), ("CRITAGL", -33), ("CARDONALD", 0), ("LTCASE", 0)); BCMN3(); return;
                    case 13: Set(s, ("PLIM", 1), ("PDAM", 0.99), ("CRITAGL", 15), ("ALD", 0.0004301), ("BLD", 1.845), ("CLD", 0.0027), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222)); BCMN2(); return;
                    case 14: Set(s, ("SHATRES", 0), ("APCAP", 2), ("PLIM", 1.06), ("PDAM", 1.06), ("ALD", 0.000232273), ("BLD", 2.00692), ("CLD", 0.00096671), ("CRITAGL", -41), ("CARDONALD", 0), ("LTCASE", 0)); BCMN3(); return;
                    case 15: Set(s, ("SHATRES", 0), ("APCAP", 2), ("PLIM", 1.01), ("PDAM", 1.01), ("ALD", 0.000184977), ("BLD", 2.46), ("CLD", 0.02549452), ("CRITAGL", -38), ("CARDONALD", 0), ("LTCASE", 0)); BCMN3(); return;
                    case 16: Set(s, ("SHATRES", 0), ("APCAP", 2), ("PLIM", 1.05), ("PDAM", 1.05), ("ALD", 0.000184977), ("BLD", 2.46), ("CLD", 0.02549452), ("CRITAGL", -38), ("CARDONALD", 0), ("LTCASE", 0)); BCMN3(); return;
                    case 17: Set(s, ("SHATRES", 0), ("APCAP", 2), ("PLIM", 1.05), ("PDAM", 1.05), ("ALD", 0.000184977), ("BLD", 2.46), ("CLD", 0.02549452), ("CRITAGL", -38), ("CARDONALD", 1), ("LTCASE", 0)); BCMN3(); return;
                }
            }
            else if (NATN == 3)
            {
                switch (PRJTL)
                {
                    case 1: DEFAULT1(); return;
                    case 2: Set(s, ("SHATRES", 0), ("APCAP", 0), ("BRAAK", 2), ("PLIM", 0.794), ("PDAM", 0.754), ("LTCASE", 1)); GCMN1(); return;
                    case 3: Set(s, ("SHATRES", 1), ("APCAP", 0), ("BRAAK", 2), ("PLIM", 0.75), ("PDAM", 0.65), ("LTCASE", 1)); GCMN1(); return;
                    case 4: Set(s, ("SHATRES", 0), ("APCAP", 1), ("BRAAK", 2), ("PLIM", 0.794), ("PDAM", 0.754), ("LTCASE", 1)); GCMN1(); return;
                    case 5: Set(s, ("SHATRES", 0), ("APCAP", 3), ("BRAAK", 1), ("PLIM", 0.794), ("PDAM", 0.754), ("LTCASE", 0)); GCMN1(); return;
                    case 6: DEFAULT5(); return;
                    case 7: DEFAULT6(); return;
                    case 8: Set(s, ("NSDAMAGL", 10), ("PLIM", 0.759), ("PDAM", 0.709), ("APCAP", 2), ("ALD", 0.0000243), ("BLD", 2.477), ("CLD", 0.00307), ("AED", 0.000247), ("BED", 2.129), ("CED", 0.00172), ("CRITAGL", 0), ("BRAAK", 1), ("SHATRES", 0), ("CARDONALD", 0), ("BEND", 0), ("LTCASE", 1)); return;
                    case 9: Set(s, ("NSDAMAGL", 10), ("PLIM", 0.794), ("PDAM", 0.754), ("APCAP", 2), ("ALD", 0.000143), ("BLD", 2.249), ("CLD", 0.00267), ("AED", 0.000247), ("BED", 2.129), ("CED", 0.00172), ("CRITAGL", 0), ("BRAAK", 1), ("SHATRES", 0), ("CARDONALD", 0), ("LTCASE", 0), ("BEND", 0)); return;
                    case 10: Set(s, ("NSDAMAGL", 25), ("PLIM", 0.99), ("PDAM", 0.972)); GCMN3(); return;
                    case 11: Set(s, ("NSDAMAGL", 25), ("PLIM", 0.979), ("PDAM", 0.926)); GCMN3(); return;
                    case 12: Set(s, ("NSDAMAGL", 25), ("PLIM", 0.988), ("PDAM", 0.977)); GCMN3(); return;
                    case 13: Set(s, ("NSDAMAGL", 25), ("PLIM", 0.929), ("PDAM", 0.881)); GCMN3(); return;
                    case 14: Set(s, ("NSDAMAGL", 25), ("PLIM", 0.86), ("PDAM", 0.8)); GCMN3(); return;
                    case 15: Set(s, ("NSDAMAGL", 25), ("PLIM", 0.9), ("PDAM", 0.86)); GCMN3(); return;
                }
            }
            else if (NATN == 4)
            {
                switch (PRJTL)
                {
                    case 1: DEFAULT1(); return;
                    case 2: DEFAULT2(); return;
                    case 3: DEFAULT3(); return;
                    case 4: DEFAULT4(); return;
                    case 5: DEFAULT7(); return;
                    case 6: DEFAULT5(); return;
                    case 7: Set(s, ("ALD", 0.00335), ("BLD", 2.13), ("CLD", 0.08701), ("CRITAGL", 15), ("LTCASE", 0)); FCMN(); return;
                    case 8: Set(s, ("ALD", 0.00336), ("BLD", 1.418), ("CLD", 0.0091701), ("CRITAGL", 20), ("LTCASE", 0)); FCMN(); return;
                    case 9: Set(s, ("ALD", 0), ("BLD", 0), ("CLD", 0), ("AED", -1), ("BED", -1), ("CED", -1), ("CRITAGL", 30), ("BRAAK", 0), ("SHATRES", 0), ("APCAP", 2), ("LTCASE", 0), ("NSDAMAGL", 30), ("PLIM", 1), ("PDAM", 1), ("CARDONALD", 0), ("BEND", 0)); return;
                }
            }
            else if (NATN == 5)
            {
                switch (PRJTL)
                {
                    case 1: DEFAULT1(); return;
                    case 2: DEFAULT3(); return;
                    case 3: DEFAULT4(); return;
                    case 4: BPR3(); return;
                    case 5: BPR4(); return;
                    case 6: BPR6(); return;
                    case 7: BPR7(); return;
                    case 8: BPR8(); return;
                    case 9: BPR9(); return;
                    case 10: DEFAULT5(); return;
                    case 11: DEFAULT6(); return;
                    case 12: Set(s, ("PLIM", 1.02), ("PDAM", 1.02), ("ALD", 0.0081984), ("BLD", 1.119507), ("CLD", 0.005032), ("AED", -1), ("BED", -1), ("CED", -1), ("CARDONALD", 0), ("BEND", 0), ("SHATRES", 0), ("APCAP", 2), ("LTCASE", 0), ("CRITAGL", 25), ("BRAAK", 0), ("NSDAMAGL", 20)); return;
                }
            }
            else if (NATN == 6)
            {
                switch (PRJTL)
                {
                    case 1: DEFAULT1(); return;
                    case 2: DEFAULT3(); return;
                    case 3: DEFAULT4(); return;
                    case 4: Set(s, ("APCAP", 0), ("BRAAK", 2), ("LTCASE", 2), ("PLIM", 0.728), ("PDAM", 0.5)); JCMN2(); return;
                    case 5: Set(s, ("APCAP", 1), ("BRAAK", 2), ("LTCASE", 2), ("PLIM", 0.728), ("PDAM", 0.5)); JCMN2(); return;
                    case 6: Set(s, ("APCAP", 1), ("BRAAK", 1), ("LTCASE", 0), ("PLIM", 0.985), ("PDAM", 0.985)); JCMN2(); return;
                    case 7: Set(s, ("PLIM", 1.02), ("PDAM", 1.01), ("ALD", 0.0081984), ("BLD", 1.119507), ("CLD", 0.005032), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222)); JCMN3(); return;
                    case 8: Set(s, ("PLIM", 1.02), ("PDAM", 1.01), ("ALD", 0.0081984), ("BLD", 1.119507), ("CLD", 0.005032), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222)); JCMN3(); return;
                    case 9: Set(s, ("AED", 0.00104), ("BED", 1.773), ("CED", 0.00823), ("LTCASE", 0), ("CRITAGL", 0), ("SHATRES", 0), ("NSDAMAGL", 15), ("PDAM", 0.85)); JCMN4(); return;
                    case 10: Set(s, ("AED", -1), ("BED", -1), ("CED", -1), ("CRITAGL", 15), ("SHATRES", 1), ("NSDAMAGL", 20), ("PDAM", 0.945), ("LTCASE", 1)); JCMN4(); return;
                }
            }
            else if (NATN == 7)
            {
                switch (PRJTL)
                {
                    case 1: DEFAULT1(); return;
                    case 2: DEFAULT2(); return;
                    case 3: DEFAULT3(); return;
                    case 4: DEFAULT4(); return;
                    case 5: Set(s, ("APCAP", 0)); AHCMN1(); return;
                    case 6: DEFAULT6(); return;
                    case 7: Set(s, ("APCAP", 3)); AHCMN1(); return;
                    case 8: Set(s, ("PLIM", 0.83), ("PDAM", 0.78), ("LTCASE", 0), ("ALD", 0.000143), ("BLD", 2.24), ("CLD", 0.00267), ("AED", 0.000247), ("BED", 2.129), ("CED", 0.00172), ("APCAP", 3), ("CRITAGL", 0), ("NSDAMAGL", 15), ("BRAAK", 1), ("CARDONALD", 0), ("BEND", 0), ("SHATRES", 0)); return;
                }
            }
            else if (NATN == 8)
            {
                switch (PRJTL)
                {
                    case 1: DEFAULT1(); return;
                    case 2: DEFAULT2(); return;
                    case 3: DEFAULT3(); return;
                    case 4: DEFAULT4(); return;
                    case 5: Set(s, ("PLIM", 1.02), ("PDAM", 1.02), ("CRITAGL", 0), ("ALD", 0.000454554), ("BLD", 2.08917437), ("CLD", 0.00514125), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222), ("APCAP", 0), ("SHATRES", 1)); RCMN1(); return;
                    case 6: Set(s, ("PLIM", 1.02), ("PDAM", 1.02), ("CRITAGL", 0), ("ALD", 0.000454554), ("BLD", 2.08917437), ("CLD", 0.00514125), ("AED", 0.03322), ("BED", 1.172), ("CED", 0.02222), ("APCAP", 3), ("SHATRES", 0)); RCMN1(); return;
                    case 7: Set(s, ("APCAP", 0), ("PLIM", 0.728), ("PDAM", 0.5), ("LTCASE", 2), ("BRAAK", 1), ("CARDONALD", 0), ("SHATRES", 1)); RCMN2(); return;
                    case 8: Set(s, ("APCAP", 3), ("PLIM", 0.728), ("PDAM", 0.5), ("LTCASE", 2), ("BRAAK", 1), ("CARDONALD", 0), ("SHATRES", 0)); RCMN2(); return;
                }
            }

            throw new ArgumentException(Invariant($"Unsupported Facehard69 projectile selection NATN={NATN} PRJTL={PRJTL}"));
        }

        public static void FaceCalc(Facehard69LegacyState s)
        {
            // FACECALC, FH69SBM2.BAS:257-292.
            // CALCULATE VARIABLE BACK LAYER THICKNESS (UB) FOR GRUSON CHILLED CAST IRON,
            // HARVEY ARMOR (BOTH TYPES), & WWII ITALIAN TERNI CEMENTED ARMORS.
            if (s.ARMOR == 1)
            {
                // GRUSON ARMOR.
                if (s.TA <= 15.75) s.UBCALC = 45;
                // THICKEST PLATE KNOWN (84CM) W/MAX BACK LAYER THICKNESS.
                else if (s.TA >= 33.07) s.UBCALC = 67;
                // LINEAR INCREASE FROM 45% TO 67% UNAFFECTED BACK ASSUMED.
                // MATCH INTERNAL VALUE TO DISPLAYED VALUE (ROUND DOWN TO WHOLE NUMBER).
                else s.UBCALC = QbInt(45 + 22 * (s.TA - 15.75) / 17.32);
            }
            else if (s.ARMOR == 3 || s.ARMOR == 4)
            {
                // HARVEYIZED STEEL (3 = MILD STEEL & 4 = NICKEL-STEEL).
                // FIXED 1-1.5" FACE LAYER THICKNESS (USE AVERAGE).
                s.UBCALC = QbInt(100 * (1 - 1.25 / s.TA));
            }
            else
            {
                // TERNI ARMOR.
                if (s.TA < 6.2205) s.UBCALC = 50;
                else if (s.TA > 10.433) s.UBCALC = 70;
                else
                {
                    // SHAPE OF FACE THICKNESS CURVE BETWEEN 13CM & 28CM NOT KNOWN. ASSUME SIMPLEST CURVE THAT DOES NOT CAUSE PROBLEMS.
                    // ALL INTERMEDIATE PLATES (NEARLY CONSTANT FACE THICKNESS).
                    var face = 3.1102 + 0.004682 * (s.TA - 6.2205);
                    s.UBCALC = QbInt(100 * (1 - face / s.TA));
                }
            }
        }

        public static void ArmorInfo(Facehard69LegacyState s)
        {
            // ARMORINFO, FH69SBM1.BAS:459-560.
            // PARAMETERS FOR SELECTED ARMOR TYPE:
            // "CARTWL"  --IF 1, BRITTLE PLATE ALWAYS THROWS LARGE DISK ("CARTWHEEL" OR "BACK SPALL") FROM BACK; IF 2, HAPPENS ONLY AT HIGH OBLIQUITY.
            // "CMPND"   --COMPOUND (STEEL-FACED WROUGHT IRON) ARMOR (FACE TOO SOFT TO SHATR MOST STEEL PROJ).
            // "SOFTSHAT"--EXTRA-TOUGH ARMOR THAT, IF 1, ALWAYS SHATRS SOFT-CAPPED PROJ (MOST POST-WWI ARMOR) OR, IF 2, SAME FOR WEAKER SOFT-CAPPED PROJ ("CARDONALD" < 2).
            // "THKTHN"  --FLAG FOR BOUNDARY OF THICK- & THIN-PLATE CALCULATIONS.
            // "THNCHL"  --VERY THIN FACE LAYER W/REDUCED BREAKAGE ABILITY (HARVEY & BETHLEHEM THIN CHILL).
            // "Q"       --PLATE'S RELATIVE STEEL QUALITY BASED ON TYPICAL WWII ARMOR AS 1.00 STANDARD (LARGER=BETTER).
            // "QDAM"    --PLATE'S RELATIVE PROJ DAMAGE ABILITY (ONLY RARELY DIFFERENT FROM "Q").
            // "UB"      --AVERAGE THICKNESS OF PLATE'S UNHARDENED BACK LAYER (THINNER MEANS MORE SCALING EFFECTS).
            s.Q = 1; s.UB = 65; s.CARTWL = 0; s.CMPND = 0; s.SOFTSHAT = 0; s.THKTHN = 0; s.THNCHL = 0;
            // FOR HARVEY ARMOR ONLY: PLATE QUALITY INCREASES AS THICKNESS DECREASES; BELOW 8" THIS RATE IS FASTER.
            var qvelhThick = -0.027917 * s.TA + 1.2525;
            var qvelhThin = (-0.035 * s.TA + 1.28) * qvelhThick;
            var qvelhMult = s.TA < 8 ? qvelhThin : qvelhThick;
            switch ((int)s.ARMOR)
            {
                case 1: s.Q = 0.7; s.CARTWL = 1; FaceCalc(s); s.UB = s.UBCALC; s.QDAM = s.Q; break;
                case 2: s.UB = 70; s.Q = 0.75; s.QDAM = 0.6; s.CMPND = 1; break;
                case 3: s.Q = 0.78 * 0.982 * Math.Pow(qvelhMult, 1d / 1.21d); s.QDAM = 0.86 * s.Q; FaceCalc(s); s.UB = s.UBCALC; s.THNCHL = s.UB > 75 ? 1 : 0; break;
                case 4: s.Q = 0.805 * 0.982 * Math.Pow(qvelhMult, 1d / 1.21d); s.QDAM = 0.86 * s.Q; FaceCalc(s); s.UB = s.UBCALC; s.THNCHL = s.UB > 75 ? 1 : 0; break;
                case 5: s.Q = 0.828; s.QDAM = s.Q; break;
                case 6: s.UB = 59; s.Q = 0.9; s.THKTHN = 1; s.QDAM = s.Q; break;
                case 7: s.UB = 59; s.Q = 0.96; s.SOFTSHAT = 1; s.THKTHN = 1; s.QDAM = s.Q; break;
                case 8: s.Q = 0.947; s.SOFTSHAT = 1; s.THKTHN = 1; s.QDAM = s.Q; break;
                case 9: s.Q = 0.85; s.SOFTSHAT = 2; s.THKTHN = 2; s.QDAM = s.Q; break;
                case 10: s.Q = 0.9; s.SOFTSHAT = 2; s.THKTHN = 2; s.QDAM = s.Q; break;
                case 11: s.UB = 70; s.Q = 0.928; s.SOFTSHAT = 1; s.THKTHN = 1; s.QDAM = s.Q; break;
                case 12: s.Q = 0.98; FaceCalc(s); s.UB = s.UBCALC; s.SOFTSHAT = 1; s.THKTHN = 1; s.QDAM = s.Q; break;
                case 13: s.Q = 0.839; s.QDAM = s.Q; break;
                case 14: s.UB = 18; s.Q = 0.881; s.CARTWL = 1; s.SOFTSHAT = 1; s.QDAM = s.Q; break;
                case 15: s.UB = 85; s.Q = 0.889; s.QDAM = 0.85; s.THNCHL = 1; break;
                case 16: s.Q = 0.889; s.QDAM = s.Q; break;
                case 17: s.UB = 45; s.SOFTSHAT = 1; s.THKTHN = 1; s.QDAM = s.Q; break;
                case 18: s.Q = 1.025; s.UB = 45; s.SOFTSHAT = 1; s.THKTHN = 1; s.QDAM = s.Q; break;
                case 19: s.Q = 0.828; s.CARTWL = 2; s.QDAM = s.Q; break;
                case 20: s.Q = 0.85; s.QDAM = s.Q; break;
                case 21: s.Q = 0.9; s.QDAM = s.Q; break;
                case 22: s.SOFTSHAT = 1; s.THKTHN = 1; s.QDAM = s.Q; break;
                default: s.QDAM = s.Q; break;
            }
        }

        public static void ScaleFactor(Facehard69LegacyState s, double back)
        {
            // SCALEFACTOR, FH69SBM1.BAS:1579-1607.
            // SCALING FACTOR CONSTANTS BASED ON UNAFFECTED BACK PERCENTAGE OF ACTUAL PLATE THICKNESS.
            // THINNER BACK = LARGER SCALING EFFECTS FROM FACE & TRANSITION LAYER SHEARING & BRITTLE FRACTURE FAILURE.
            // CONSTANTS "AZ" & "BZ" FOR COMBINED FACE & TRANSITION LAYERS & "CZ" FOR SOFT BACK LAYER.
            // COMPUTE SCALING FACTOR TERM "SC" USING FORMULA: SC = (AZ * (D^BZ)) + CZ.
            double az, bz, cz;
            if (back > 90) { az = 0; bz = 1; cz = 79; }
            else if (back > 75) { az = 0.000000665; bz = 5.35; cz = 78.5; }
            else if (back > 67.5) { az = 0.00037; bz = 3.23; cz = 77.8; }
            else if (back > 62) { az = 0.003; bz = 2.75; cz = 77.7; }
            else if (back > 52) { az = 0.03; bz = 2.1; cz = 77; }
            else if (back > 30) { az = 1; bz = 1.25; cz = 67; }
            else { az = 10.57; bz = 0.80625; cz = 17.26; }
            s.SC = az * Math.Pow(s.D, bz) + cz;
        }

        public static void SetObMult(Facehard69LegacyState s)
        {
            // SETOBMULT, FH69SBM1.BAS:1657-1714.
            // PROJ OB MULTIPLIER FOR BOTH SHATRD & UNSHATRD PROJ FROM TABLE INTERPOLATION OR CALCULATION FORMULAE.
            // "INT1" IS M/MS-TABLE INDEX & "INT2" IS FRACTION OF 5-DEG STEP THAT OB IS ABOVE M/MS(INT1).
            if (s.OB < 70)
            {
                // FIRST, DO UNSHATRD PROJ MULTIPLIER; "MO" IS FOR ALL UNSHATRD PROJ, EXCEPT HBL WHEN "VHSHAT" < "VHTRU".
                // 3-POINT FORWARD-LOOKING INTERPOLATION FORMULA.
                var int1 = QbInt(s.OB / 5);
                var int2 = (s.OB - 5 * int1) / 5;
                var point5 = int1 > 11 ? 0 : 0.5;
                s.MO = s.M[int1] + int2 * (s.M[int1 + 1] - s.M[int1]) + point5 * int2 * (int2 - 1) * (s.M[int1 + 2] - 2 * s.M[int1 + 1] + s.M[int1]);
            }
            // OB = 70 DEG IS MAX FOR UNSHATRD COMPLETE PEN.
            else if (s.OB == 70) s.MO = 8;
            // ENSURE NO PEN OCCURS AT OB > 70 DEG IF NO SHATR (70.01-80 DEG).
            else s.MO = 100;

            // "MSHAT" IS FOR SHATRD PROJ (TWO VALUES DEPENDING ON "THIN" PLATE THICKNESS THRESHOLD).
            // THICK PLATE, USE FORMULAE FOR MSHAT.
            const double thkMax = 5.5264;
            var thick = s.OB > 75 ? 100 : s.OB == 75 ? thkMax : 1 / Math.Cos(1.061 * s.OBRAD);
            double thin;
            if (s.OB >= 80) thin = 1.51;
            else
            {
                // THIN PLATE, USE SPECIAL SHATR OB MULT TABLE (PLATE SHATRS, TOO!).
                var int1 = QbInt(s.OB / 5);
                var int2 = (s.OB - 5 * int1) / 5;
                thin = s.MS[int1] + int2 * (s.MS[int1 + 1] - s.MS[int1]) + 0.5 * int2 * (int2 - 1) * (s.MS[int1 + 2] - 2 * s.MS[int1 + 1] + s.MS[int1]);
            }

            var tpcal = s.TEFF / s.D;
            if (tpcal < s.THIN)
            {
                // STEP DOWN TO THE THIN VALUE IN TWO INTERMEDIATE STEPS FOR ALL "OB" UP TO 80 DEG.
                if (tpcal > s.TRUTHIN) s.MSHAT = thin + 0.625 * Math.Abs(thick - thin);
                else if (tpcal > s.TRUTHIN - 0.05) s.MSHAT = thin + 0.3 * Math.Abs(thick - thin);
                else s.MSHAT = thin;
            }
            else s.MSHAT = thick;
        }

        public static void ThinSelect(Facehard69LegacyState s)
        {
            // THINSELECT, FH69SBM2.BAS:1208-1229.
            // U.S. WWI-ERA TESTS SHOW BRITTLE FH ARMORS LOST STRENGTH IF <0.55-CAL EFFECTIVE THICKNESS,
            // REDUCING "MSHAT" FOR SUCH "THIN" ARMOR. LATER, TOUGHER PLATES ("THKTHN" FLAG = 1 OR 2)
            // ACT AS "THIN" PLATES IF <0.35-CAL ("THKTHN=1").
            s.THIN = 0.55;
            s.TRUTHIN = 0.45;
            if (s.THKTHN == 1) { s.THIN = 0.35; s.TRUTHIN = 0.25; }
            else if (s.THKTHN == 2) { s.THIN = 0.45; s.TRUTHIN = 0.35; }
        }

        public static void CalcVdf(Facehard69LegacyState s)
        {
            // CALCVDF, FH69SBM1.BAS:635-680. This only computes the common scalar state;
            // the full MODIFYVDF limit selection remains a later translation step.
            // COMPUTE VELOCITY HOLING DIFFERENTIAL TO USE IN THIS CASE.
            // BRITTLE WWI PLATES (ALL "THKTHN = 0" PLATES) USE "VDFSTDWW1";
            // ALL MORE MODERN, TOUGHER ("THKTHN > 0") PLATES USE SMALLER "VDFSTDWW2" DIFFERENCE.
            // BRITISH DEFORMABLE PROJECTILES ("BEND" = 1) VARY THIS FROM A NARROW GAP UP TO 22.5 DEG
            // THEN A RAPID INCREASE IN THE GAP UNTIL IT REACHES THE STANDARD PLATE GAP AT & ABOVE 45 DEG.
            if (s.THKTHN > 0) s.VDFSTD = s.VDFSTDWW2;
            else if (s.ARMOR == 3 || s.ARMOR == 4) s.VDFSTD = s.VDFHARVEY;
            else s.VDFSTD = s.VDFSTDWW1;
            s.VDFUSED = s.VDFSTD;

            if (s.BEND == 1 && s.CARDONALD == 0)
            {
                // BRITISH DEFORMABLE NON-CARDONALD PROJ.
                var obvdf = s.OB;
                if (obvdf < 22.5) obvdf = 22.5;
                if (obvdf > 45) obvdf = 45;
                var vdfVal = (90 / 57.29578) * (2 * ((obvdf - 22.5) / 22.5) + 1);
                const double dif1 = 0.08;
                const double dif2 = 0.01;
                // EQUAL TO "DIF2" IF OB <= 22.5 DEG; EQUAL TO "DIF1+DIF2" IF OB >= 45 DEG.
                s.VDFBND = s.VDFUSED / (dif1 + dif2) * (dif1 * ((1 - Math.Sin(vdfVal)) / 2) + dif2);
            }
            else s.VDFBND = 0;
        }

        public static void ProjQMods(Facehard69LegacyState s)
        {
            // PROJQMODS, FH69SBM1.BAS:1524-1550.
            // CALCULATE ALL PROJECTILE PENETRATION QUALITY FACTOR MODIFIERS.
            var lcDam = 0d;
            var tcal = s.TD / s.D;
            // LIGHTCASE BASE-FUZED PROJ LOSE PEN AT NORMAL WHEN EFFECTIVE DAMAGE-CAUSING PLATE THICKNESS > 0.67 CALIBER.
            if (s.LTCASE == 2 && tcal > 0.67) lcDam = 0.5;
            var oPrimeL = s.OB;
            var oPrimeD = s.OB;
            // LITTLE DATA AT OB > 60 DEG EXIST FOR PROJ USING THIS FORMULA, SO RESTRICT EFFECTS TO OB = 45 DEG AS WORST CASE.
            if (s.OB > 60) { oPrimeL = 60; oPrimeD = 60; }
            s.LCMOD = lcDam * (tcal - 0.67);
            s.POLMOD = 1 + s.CLD * oPrimeL - s.ALD * tcal * Math.Pow(oPrimeL, s.BLD) - s.LCMOD;
            if (s.POLMOD > 1) s.POLMOD = 1;
            if (s.POLMOD < 0.1) s.POLMOD = 0.1;
            if (s.AED < 0) s.POIMOD = s.POLMOD;
            else
            {
                // "AED" = -1 MEANS "POIMOD" FORMULA NOT USED BY THIS PROJ TYPE.
                // BENDING/COMPRESSION/BREAKAGE DAMAGE EFFECTS ON EFFECTIVE LIMIT, IF USED FOR THIS PROJ TYPE.
                s.POIMOD = 1 + s.CED * oPrimeD - s.AED * tcal * Math.Pow(oPrimeD, s.BED) - s.LCMOD;
                if (s.POIMOD > 1) s.POIMOD = 1;
                else if (s.POIMOD < 0.095) s.POIMOD = 0.095;
            }
        }

        public static void ShtrMultSelect(Facehard69LegacyState s)
        {
            // SHTRMULTSELECT, FH69SBM2.BAS:1124-1146.
            // SHATTER INCREASES HBL, WHICH ALSO INCREASES NBL.
            // SELECT WHICH HBL SHATTER MULTIPLIER TO USE.
            if (s.ARMOR == 3 || s.ARMOR == 4)
            {
                // NORMAL OB BONUS FOR THICK HARVEYIZED ARMORS.
                s.SHATMULT = s.TA >= 8 ? 1 : 1.25 - 0.03125 * s.TA;
            }
            // NORMAL OB BONUS FOR IMPROVED BRITISH WWI PLATES & REGULAR SOFT-CAPPED PROJ AT OB<=20 DEG.
            else if (s.SOFTSHAT == 2 && Math.Abs(s.HARD) == 1 && s.NSSHAT == 1 && s.OB <= 20) s.SHATMULT = 1.2;
            // NORMAL OB BONUS FOR BEST SOFTSHAT PLATES WHEN PROJ HAS COMPLETE SHATR.
            else if (s.SOFTSHAT == 1 && s.HARD < 3 && s.BEND == 0 && (s.NSSHAT == 0 || s.OB > 20)) s.SHATMULT = 1.4;
            // NORMAL OB BONUS FOR EVERY OTHER CASE.
            else s.SHATMULT = 1.3;
        }

        public static void ModifyVdf(Facehard69LegacyState s)
        {
            // MODIFYVDF, FH69SBM2.BAS:467-532.
            // CHANGE THE HBL-TO-NBL VELOCITY RANGE FOR SPECIAL CASES.
            // STANDARD WWI "15% THICKNESS DIFFERENCE RULE": PLATE'S HBL = NBL OF A PLATE 15% THINNER;
            // WWII PLATES ARE USUALLY MUCH TOUGHER & HAVE A NARROWER DIFFERENCE.
            // AGAINST BRITISH DEFORMING PROJ, GAP IS NARROW TO 22.5 DEG & THEN WIDENS UNTIL REGULAR GAP USED ABOVE 45 DEG.
            s.VDFUSEDPR = s.VDFUSED;
            s.SHATVDFPR = s.VDFUSED;
            if (s.ARMOR == 3 || s.ARMOR == 4)
            {
                // HARVEYIZED STEEL ARMOR.
                // FOR THINNER FACES, PUNCHING THROUGH THE FACE DOES NOT FORM A PLUG THAT PUNCHES A HOLE THRU THE REST OF THE PLATE.
                var vdfThin = s.TA >= 8 ? 0 : -0.023 * s.TA + 0.184;
                s.VHTRU = (1 - (s.VDFBRK + vdfThin)) * s.VLTRU;
                s.VHND = (1 - (s.VDFBRK + vdfThin)) * s.VLND;
                s.VHSHAT = QbInt((1 - (s.VDFBRK + vdfThin)) * s.VLSHAT);
                s.VHSHATMAX = QbInt((1 - (s.VDFBRK + vdfThin)) * s.VLSHATMAX);
                s.VDFUSEDPR = s.VDFBRK + vdfThin;
                if (s.VDFBRK + vdfThin < s.SHATVDF) s.SHATVDFPR = s.VDFBRK + vdfThin;
            }
            else
            {
                if (s.VDFBND == 0 && (s.BRAIK == 0 || s.LTCASE == 2))
                {
                    s.VHTRU = (1 - s.VDFSTD) * s.VLTRU;
                    s.VHND = (1 - s.VDFSTD) * s.VLND;
                }
                else if (s.VDFBND > 0)
                {
                    if (s.UB > 45 && s.UB < 70 && s.CART == 0 && s.SOFTSHAT == 1)
                    {
                        s.VHTRU = (1 - (s.VDFSTD + s.VDFBND) / 2) * s.VLTRU;
                        s.VHND = (1 - (s.VDFSTD + s.VDFBND) / 2) * s.VLND;
                        s.VDFUSEDPR = (s.VDFSTD + s.VDFBND) / 2;
                    }
                    else
                    {
                        s.VHTRU = (1 - s.VDFBND) * s.VLTRU;
                        s.VHND = (1 - s.VDFBND) * s.VLND;
                        s.VDFUSEDPR = s.VDFBND;
                    }
                }
                else
                {
                    s.VHTRU = (1 - s.VDFBRK) * s.VLTRU;
                    s.VHND = (1 - s.VDFBRK) * s.VLND;
                    s.VHSHAT = QbInt((1 - s.VDFBRK) * s.VLSHAT);
                    s.VHSHATMAX = QbInt((1 - s.VDFBRK) * s.VLSHATMAX);
                    s.VDFUSEDPR = s.VDFBRK;
                    if (s.VDFBRK < s.SHATVDF) s.SHATVDFPR = s.VDFBRK;
                }
            }
            s.VHTRU = QbInt(s.VHTRU);
            s.VHND = QbInt(s.VHND);
        }

        public static void CalcBl(Facehard69LegacyState s)
        {
            // CALCBL, FH69MAIN.BAS:588-850.
            // COMPUTE SCALE FACTOR "SC".
            ScaleFactor(s, s.UB);
            s.DEN = Math.Pow(s.WT / Math.Pow(s.D, 3), 0.2);
            s.TD = s.TA * s.QDAM;
            s.SOFTQPMAX = QbInt(1000 * (1 - (1.1 * ((s.WT - s.WB) / s.WT) - 0.0268)) + 0.5) / 1000d;
            if (s.SOFTQPMAX > 1) s.SOFTQPMAX = 1;
            s.PPLUS = 0;
            if (s.CMPND == 1)
            {
                // THIN CHILL, HARVEY, & COMPOUND ARMORS CAN CAUSE LESS STRONG PROJ DAMAGE,
                // SO IF THEY CAUSE DAMAGE, THE REDUCED "QP" THEY CAUSE CAN BE INCREASED BACK UP TO 1.0
                // OR "SOFTQPMAX" (SOFT CAP/HOOD) MAXIMUM VALUE.
                if (s.SHATRES == 2) s.PPLUS = 0;
                else if (s.BRAIK == 2 || s.BEND == 2) s.PPLUS = 0.1;
                else s.PPLUS = 0.2;
            }
            else if (s.THNCHL == 1)
            {
                if (s.SHATRES == 2 || s.BRAIK == 2 || s.BEND == 2) s.PPLUS = 0;
                else s.PPLUS = 0.1;
            }

            if (s.PLIM > 1 && s.APCAP != s.HARD) s.PLM = 1;
            else s.PLM = s.PLIM - s.CAPHDLOSS;
            if (s.PLM < 1)
            {
                s.PLM += s.PPLUS;
                var maxPlm = Math.Abs(s.HARD) == 1 ? s.SOFTQPMAX : 1;
                if (s.PLM > maxPlm) s.PLM = maxPlm;
            }
            if (s.PLIM > 1 && s.APCAP != s.HARD) s.PDM = s.PDAM * (1 - (s.PLIM - 1) / s.PLIM);
            else s.PDM = s.PDAM - s.CAPHDLOSS;
            if (s.PDM > 0 && s.PDM < 1) s.PDM += s.PPLUS;

            if (s.NATION == 1 && s.PROJ == 20)
            {
                var damChk = (s.TD / s.D) * s.OB;
                if (damChk > 32.175)
                {
                    s.PLM = 0.94; s.PDM = 0.94; s.CRITAGL = 25; s.NSDAMAGL = 25;
                }
            }

            s.PNL = s.PLM;
            s.PENCONST = 1.9822E-06 * s.D * s.SC * s.DEN;
            s.CRTAPR = s.CRITAGL <= 0 ? 0 : s.CRITAGL;
            if (s.CRITAGL > 0 && s.CMPND == 1 && s.SHATRES < 2) s.CRTAPR = s.CRITAGL + 10;
            if (s.CRITAGL > 0 && s.THNCHL == 1 && s.BRAIK != 2 && s.SHATRES != 2) s.CRTAPR = s.CRITAGL + 5;
            if (s.BEND == 1)
            {
                s.OBCRIT = Math.Abs(s.CRITAGL);
                if ((s.SOFTSHAT == 0 || s.CART > 0) && s.THNCHL == 0) s.OBCRIT += s.CMPND == 1 ? 12 : 6;
            }
            else s.OBCRIT = -1;

            SetObMult(s);
            ProjQMods(s);
            if ((s.PNL < 1 && s.HARD > 1) || s.HARD == 0) s.PNLPR = 1;
            else if (Math.Abs(s.HARD) == 1) s.PNLPR = s.SOFTQPMAX;
            else s.PNLPR = s.PNL;
            if (s.PNL > s.PNLPR) s.PNL = s.PNLPR;
            s.PNI = s.PDM;
            if (s.PNI < 0 || s.PNI > s.PNL) s.PNI = s.PNL;

            if (s.LTCASE > 0 || s.BRAIK > 0)
            {
                s.PNLSHAT = s.PNL;
                if (s.LTCASE == 2) s.PNLSHAT *= 1 - s.LCMOD;
                s.PSHMAX = Math.Abs(s.APCAP) == 1 && s.WT != s.WB ? s.SOFTQPMAX : 1;
                if (s.PNLSHAT < 0.095) s.PNLSHAT = 0.095;
                if (s.PNLSHAT > s.PSHMAX) s.PNLSHAT = s.PSHMAX;
            }
            else { s.PNLSHAT = 1; s.PSHMAX = 1; }
            if (s.BEND == 1 && s.PNLSHAT < 1)
            {
                if (s.OB >= 30) s.PNLSHAT = s.PSHMAX;
                else if (s.OB > 15 && s.OB < 30) s.PNLSHAT += (s.PSHMAX - s.PNLSHAT) * (s.OB - 15) / 15;
            }
            if (s.SHAT == 1) s.PNI = s.PNLSHAT;

            s.VLND = QbInt(Math.Pow(s.TEFF * s.MO / (s.PENCONST * s.PNLPR), s.VXP));
            CalcVdf(s);
            ShtrMultSelect(s);
            s.SHATVDF = s.OB <= 45 ? 1 / (1 - s.VDFSTD) : 1 / (1 - s.VDFSTD * Math.Pow(Math.Cos(2 * (s.OB - 45) / 57.29578), 8));
            s.VHSHATMAX = QbInt(Math.Pow(s.TEFF * s.SHATMULT * s.MSHAT / (s.PENCONST * s.PSHMAX), s.VXP));
            s.VLSHATMAX = QbInt(s.SHATVDF * s.VHSHATMAX);
            s.VHSHAT = QbInt(Math.Pow(s.TEFF * s.SHATMULT * s.MSHAT / (s.PENCONST * s.PNLSHAT), s.VXP));
            if (Math.Abs(s.VHSHAT - s.VHSHATMAX) <= 1) s.VHSHAT = s.VHSHATMAX;
            s.VLSHAT = QbInt(s.SHATVDF * s.VHSHAT);
            s.VLTRU = QbInt(Math.Pow(s.TEFF * s.MO / (s.PENCONST * s.PNL * s.POLMOD), s.VXP));
            if (Math.Abs(s.VLTRU - s.VLND) <= 1) s.VLTRU = s.VLND;
            ModifyVdf(s);
            if (s.PNL == s.PNI && s.CRITAGL != 0) s.VITRU = -1;
            else
            {
                s.VITRU = QbInt(Math.Pow(s.TEFF * s.MO / (s.PENCONST * s.PNI * s.POIMOD), s.VXP));
                if (s.VITRU < s.VLTRU) s.VITRU = s.VLTRU;
            }
            s.VSCRIT = QbInt(s.VHSHAT / (1 - 0.83 * s.VDFSTD));
        }

        public static void ImpactSetup(Facehard69LegacyState s, Facehard69LegacyImpactOptions options = null)
        {
            // IMPACTSETUP, FH69MAIN.BAS:853-879 and FH69SBM2.BAS:295-386.
            // DETERMINE PROJECTILE SHATTER, NOSE-ONLY SHATTER, AND OTHER INITIAL IMPACT FLAGS.
            var vs = s.VS;
            if (s.CAPHD == 2 && s.CAPHDRMV == 1)
            {
                // FOR OB<45 DEG ONLY.
                s.CAPHDLOSS = 0.045;
                if (s.OB >= 45) s.CAPHDLOSS *= Math.Pow(Math.Cos(2 * (s.OB - 45) / 57.29578), 2);
            }
            else s.CAPHDLOSS = 0;

            s.BRAIK = s.BRAAK;
            s.SHAT = 0;
            s.SHATPRT = 0;
            s.CART = s.CARTWL;
            s.TD = s.TA * s.QDAM;
            s.TOTHNFLG = (s.TD / s.D) / Math.Cos(s.OBRAD) < 0.25 ? 1 : 0;
            var skipShtr = 0;
            if ((s.SHATRES != 2 || (s.SHATRES == 2 && s.HARD == 1 && s.OB < 20)) && s.TOTHNFLG == 1)
            {
                if (s.CMPND == 1) skipShtr = 2;
                else { skipShtr = 1; s.SHATPRT = 1; }
            }
            else if (s.SHATRES != 2 && (s.ARMOR == 3 || s.ARMOR == 4))
            {
                if (s.BRAIK < 2 || s.BEND == 2) { skipShtr = 1; s.SHATPRT = 2; }
            }
            else if (s.CMPND == 1)
            {
                skipShtr = 2;
                if (s.SHATRES == 2)
                {
                    if (s.HARD == 0) { s.SHAT = 1; s.CART = 0; skipShtr = 1; }
                }
                else { skipShtr = 1; s.SHATPRT = 3; }
            }

            if (skipShtr != 1)
            {
                var shatSoft = s.SOFTSHAT == 1 || (s.SOFTSHAT == 2 && s.CARDONALD < 2) ? 1 : 0;
                var tooThick = s.TD / s.D >= 0.67 ? 1 : 0;
                if (s.HARD == 0 || s.HARD == -1) s.SHAT = 1;
                else if (s.HARD == 1)
                {
                    if (s.OB > 20 || shatSoft == 1) s.SHAT = 1;
                }
                else if (s.HARD == 3)
                {
                    if (shatSoft == 1 && tooThick == 1 && s.OB > 20) s.SHAT = 1;
                }
                else if (s.CAPHD == 1 && s.HARD == 2) s.SHAT = 1;

                if (s.SHAT == 1)
                {
                    if (s.HARD == 1 && ((s.SOFTSHAT == 2 && s.CARDONALD < 2) || s.SOFTSHAT == 1) && s.OB <= 20) s.NSSHAT = 1;
                    else if (s.HARD == -1 && s.SOFTSHAT == 0 && s.SHATRES < 2 && s.OB <= 20) s.NSSHAT = 4;
                }
            }

            if (s.OB > 15 && s.OB <= 20 && Math.Abs(s.HARD) == 1 && options != null && options.softCapWorksInZomr == false)
            {
                s.SHAT = 1; s.CART = 0; s.NSSHAT = 0;
            }

            s.MINSHVEL = 0;
            if (s.SHAT == 1 && s.NSSHAT == 0)
            {
                s.MINSHVEL = 1170 * Math.Cos(s.OBRAD);
                if (s.BRAIK == 0 && vs <= s.MINSHVEL) s.NSSHAT = 2;
            }
            s.CART = s.SHAT == 1 ? 0 : s.CARTWL;
            s.HF = s.SHAT == 0 && s.OB > 70 ? 1 : 0;
        }

        public static void DeflecCalc(Facehard69LegacyState s)
        {
            s.VRAT = s.VRATVEL / s.VRATMIN;
            if (Math.Abs(s.SNCSMAX) < 1e-12)
            {
                s.TMPOBDF = 0;
                s.OB45CALC = 0;
                return;
            }
            var tmpv = Math.Pow(s.VRAT, 2) - 1;
            var tmpvel = Math.Pow(s.VRAT, 2) + s.VRAT * QbSqr(tmpv);
            var tmpdf1 = s.SNCSMAX / tmpvel;
            var tmpdf2 = 1 - 4 * Math.Pow(tmpdf1, 2);
            var tanObdf = (1 - QbSqr(tmpdf2)) / (2 * tmpdf1);
            s.TMPOBDF = Math.Atan(tanObdf) * 57.29578;
            s.OB45CALC = s.OB - s.EXMIN > 45 ? ((s.OB - s.EXMIN) - 45) / 45 : 0;
            s.TMPOBDF *= 1 + s.OB45CALC;
        }

        public static void BlPlusEx(Facehard69LegacyState s)
        {
            // BLPLUSEX, FH69MAIN.BAS:883-1187. This preserves the numeric selection path;
            // print-marker strings are intentionally kept out of the first executable slice.
            // SELECT ALL LIMIT VEL THAT APPLY TO IMPACT ("!" IS FOR PEN & "#" IS FOR POST-IMPACT EFFECTS).
            // IF NO SHATR, "EX" LINEARLY INCREASES FROM 0 TO "EXMIN" AS VEL INCREASES FROM HBL TO NBL.
            // ALWAYS USE DESIGNATED LIMITS FOR THAT POST-IMPACT PROJ DAMAGE LEVEL.
            // IF VEL<HBL, NO LARGE HOLE MADE IN PLATE & "EX" IS UNDEFINED, SO EXIT "EX" LOGIC.
            // "EX" VEL RATIO FORMULA: "EX" -> OB (DEFLECTION = OB - "EX" = "OBDF" -> 0) AS VEL INCREASES.
            var vs = s.VS;
            s.N1 = s.N2 = s.N3 = s.N4 = s.H1 = s.H2 = s.H3 = s.H4 = "";
            s.PENFLG = 2; s.VRAT = 0; s.OB45 = 0; s.OB45CALC = 0; s.VHDAM = 0; s.VLDAM = 0;
            s.CRVFLAG = s.CURV == 1 && s.OB > 45 && s.SHATRES < 2 ? 1 : 0;
            s.EXMIN = s.OB <= 15 ? s.OB : 15;
            s.CRTGD = 0; s.VSCHECK = 0;
            if (s.SHAT == 1 && s.HARD != 2 && vs >= s.VHSHAT) s.EXMIN = 0;
            var maxDfDeg = Math.Min(s.OB - s.EXMIN, 45);
            var maxDf = maxDfDeg / 57.29578;
            s.SNCSMAX = Math.Sin(maxDf) * Math.Cos(maxDf);

            if (s.SHAT == 0 || (s.SHAT == 1 && s.HARD == 2))
            {
                if (s.SHAT == 1)
                {
                    s.VLMT = s.VLSHAT; s.VLDAM = s.VLSHAT; s.VHOL = s.VHTRU; s.VHDAM = s.VHTRU;
                    if (s.VHOL >= s.VHSHAT) { s.VHOL = s.VHSHAT; s.VHDAM = s.VHSHAT; }
                }
                else
                {
                    s.VLMT = s.VLTRU; s.VLDAM = s.VLTRU; s.VHOL = s.VHTRU; s.VHDAM = s.VHTRU;
                    if (s.BEND == 1 && s.CARDONALD == 0 && s.OB >= s.OBCRIT) s.VSCHECK = 9999;
                    else if (s.CARDONALD == 1 && s.OB >= s.OBCRIT) s.VSCHECK = s.VSCRIT;
                    else if (s.BEND == 1 && s.OB < s.OBCRIT) s.VSCHECK = 0;
                    else s.VSCHECK = -1;
                    if (s.VLTRU == s.VLND)
                    {
                        s.VLMT = s.VLND; s.VLDAM = s.VLND; s.VHOL = s.VHND; s.VHDAM = s.VHND;
                    }
                    else
                    {
                        var vsChkFlg = 0;
                        if (s.VSCHECK == -1 && s.VLTRU > s.VLND && s.VITRU >= 0 && vs >= s.VITRU) vsChkFlg = 1;
                        if (s.VSCHECK > 0 && vs >= s.VSCHECK) vsChkFlg = 2;
                        if (s.VSCHECK == 0) vsChkFlg = 3;
                        if (vsChkFlg != 0) { s.VLDAM = s.VLND; s.VHDAM = s.VHND; }
                    }
                }

                if (s.OB <= 45 && s.BEND == 0)
                {
                    if (s.VLMT >= s.VLSHAT)
                    {
                        if (s.VLDAM == s.VLND && s.VLSHAT == s.VLSHATMAX)
                        {
                            s.VLMT = s.VLSHATMAX; s.VLDAM = s.VLSHATMAX; s.VHOL = s.VHSHATMAX; s.VHDAM = s.VHSHATMAX;
                        }
                        else
                        {
                            s.VLMT = s.VLSHAT;
                            s.VLDAM = s.VLDAM == s.VLND ? s.VLSHATMAX : s.VLSHAT;
                            s.VHOL = s.VHSHAT;
                            s.VHDAM = s.VHDAM == s.VHND ? s.VHSHATMAX : s.VHSHAT;
                        }
                    }
                    else if (s.VHOL >= s.VHSHAT)
                    {
                        s.VHOL = s.VHDAM == s.VHND && s.VHSHAT == s.VHSHATMAX ? s.VHSHATMAX : s.VHSHAT;
                        if (s.VHDAM >= s.VHOL || s.VHDAM > s.VHSHATMAX) s.VHDAM = s.VHSHATMAX;
                    }
                }
                else if (s.VHOL >= s.VHSHAT)
                {
                    s.VHOL = s.VHDAM == s.VHND && s.VHSHAT == s.VHSHATMAX ? s.VHSHATMAX : s.VHSHAT;
                    if (s.VHDAM >= s.VHOL || s.VHDAM > s.VHSHATMAX) s.VHDAM = s.VHSHATMAX;
                }
            }
            else
            {
                s.VLMT = s.VLSHAT; s.VLDAM = s.VLSHAT; s.VHOL = s.VHSHAT; s.VHDAM = s.VHSHAT;
                if (vs >= s.VHOL && vs < s.VLMT) s.PENFLG = 1;
            }

            s.VRATMIN = s.VLMT;
            if (s.SHAT == 1 && s.HARD != 2 && vs >= s.VHSHAT) s.VRATMIN = s.VHOL;
            if (vs < s.VHOL)
            {
                s.VRAT = -1; s.PENFLG = 0; s.EX = -1; s.EXRAD = -1; s.OBDF = -1;
                s.OB45 = s.OB - s.EXMIN > 45 ? ((s.OB - s.EXMIN) - 45) / 45 : 0;
                UpdateLimitMarkers(s);
                return;
            }
            if (s.SHAT == 0 || (s.SHAT == 1 && s.HARD == 2))
            {
                if (vs >= s.VHOL && vs < s.VLMT)
                {
                    s.EXNBL = s.EXMIN; s.PENFLG = 1; s.EX = s.EXMIN * (vs - s.VHOL) / (s.VLMT - s.VHOL);
                    s.OB45 = s.OB - s.EXMIN > 45 ? ((s.OB - s.EXMIN) - 45) / 45 : 0;
                    s.EXRAD = s.EX / 57.29578; s.OBDF = s.OB - s.EX;
                    UpdateLimitMarkers(s);
                    return;
                }
            }
            else if (vs >= s.VHSHAT && vs < s.VLSHAT) s.PENFLG = 1;

            if (s.SHAT == 1 || s.OB > 15)
            {
                if (s.OB <= 0.005) { s.EX = 0; s.EXNBL = 0; }
                else
                {
                    s.VRATVEL = vs; DeflecCalc(s); s.OB45 = s.OB45CALC; s.EX = s.OB - s.TMPOBDF;
                    if (s.SHAT == 1) { s.VRATVEL = s.VLSHAT; DeflecCalc(s); s.EXNBL = s.OB - s.TMPOBDF; }
                    else s.EXNBL = s.EXMIN;
                }
            }
            else { s.EX = s.OB; s.EXNBL = s.OB; }
            s.EXRAD = s.EX / 57.29578;
            s.OBDF = s.OB - s.EX;
            UpdateLimitMarkers(s);
        }

        static void UpdateLimitMarkers(Facehard69LegacyState s)
        {
            s.N1 = s.N2 = s.N3 = s.N4 = s.H1 = s.H2 = s.H3 = s.H4 = "";
            ApplyMarker(s, SelectLimitLabel(s.VLMT, ("N3", s.VLSHATMAX), ("N2", s.VLSHAT), ("N4", s.VLND), ("N1", s.VLTRU)), "-!-");
            ApplyMarker(s, SelectLimitLabel(s.VLDAM, ("N3", s.VLSHATMAX), ("N2", s.VLSHAT), ("N4", s.VLND), ("N1", s.VLTRU)), "-#-");
            ApplyMarker(s, SelectLimitLabel(s.VHOL, ("H3", s.VHSHATMAX), ("H2", s.VHSHAT), ("H4", s.VHND), ("H1", s.VHTRU)), "-!-");
            ApplyMarker(s, SelectLimitLabel(s.VHDAM, ("H3", s.VHSHATMAX), ("H2", s.VHSHAT), ("H4", s.VHND), ("H1", s.VHTRU)), "-#-");
        }

        static string SelectLimitLabel(double value, params (string label, double candidate)[] labels)
        {
            foreach (var entry in labels)
            {
                if (Math.Abs(value - entry.candidate) < 0.5) return entry.label;
            }
            return labels[labels.Length - 1].label;
        }

        static void ApplyMarker(Facehard69LegacyState s, string label, string marker)
        {
            var field = StateFields[label];
            var current = (string)field.GetValue(s);
            if (string.IsNullOrEmpty(current)) field.SetValue(s, marker);
            else if (current != marker) field.SetValue(s, "-!#-");
        }

        public static void ShatrDam(Facehard69LegacyState s)
        {
            if ((s.CRVRL == 1 && s.BRAIK == 0) || (s.PENFLG == 2 && s.NSSHAT > 0 && s.NSSHAT != 2) || s.NSSHAT == 2)
            {
                s.NSBRK = 2; s.BRK = 0; s.BDYDM = 0;
            }
            else if (s.PENFLG < 2 && s.NSSHAT > 0)
            {
                s.NSBRK = 2; s.BRK = 2; s.BDYDM = 2;
            }
            else
            {
                s.NSBRK = 2; s.BRK = 1; s.BDYDM = 2;
            }
        }

        public static void NoseDam(Facehard69LegacyState s)
        {
            s.NDAP = s.NSDAMAGL;
            if (s.CMPND == 1) s.NDAP += 10;
            else if (s.ARMOR == 3 || s.ARMOR == 4)
            {
                if (s.TA >= 8) s.NDAP += 10;
                else if (s.THNCHL == 1) s.NDAP += 5 + 5 * ((s.TA - 5) / 3);
            }
            else if (s.THNCHL == 1) s.NDAP += 5;
            if (s.SOFTSHAT == 1) s.NDAP -= 10;
            else if (s.SOFTSHAT == 2) s.NDAP -= 5;
            if (s.NDAP < 5 && s.NSDAMAGL > 0) s.NDAP = 5;
            if (s.NDAP < 0) s.NDAP = 0;
        }

        public static void NoseBroke(Facehard69LegacyState s)
        {
            if (s.NSBRK != 0) return;
            if (s.BRAIK > 0)
            {
                if ((s.ARMOR == 3 || s.ARMOR == 4) && s.BEND == 2) s.NSBRK = 8;
                else s.NSBRK = 1;
            }
            if (s.NSBRK != 1)
            {
                if (s.HARD == -1 && s.SHATRES == 1 && s.CMPND == 0 && s.ARMOR != 3 && s.ARMOR != 4) s.NSBRK = 4;
                else if (Math.Abs(s.HARD) == 1 && s.SOFTSHAT == 1) s.NSBRK = 5;
                else if (Math.Abs(s.HARD) == 1 && s.SOFTSHAT == 2 && s.CARDONALD < 2) s.NSBRK = 5;
                else if (s.PENFLG == 0 && s.OB >= s.NDAP) s.NSBRK = 3;
                else if (s.PENFLG == 1 && s.OB - s.EX > s.NDAP) s.NSBRK = 3;
                else if (s.HARD > 1 && s.PENFLG == 0 && s.VLMT > s.VLND && s.CARDONALD == 0) s.NSBRK = 6;
                else if (s.OB > 45) s.NSBRK = 7;
            }
        }

        public static void DoCrtGood(Facehard69LegacyState s)
        {
            if (s.NSBRK > 0 || s.PENFLG == 2)
            {
                if (!(s.CRTAPR <= 0 || (s.CRTAPR > 0 && s.OB - s.EX >= s.CRTAPR) || (s.SHAT == 1 && s.HARD != 2)))
                {
                    s.CRTGD = 1; s.VHDAM = s.VHND; s.VLDAM = s.VLND;
                    if (s.VHDAM > s.VHSHATMAX) s.VHDAM = s.VHSHATMAX;
                    if (s.SHAT == 1) s.VLDAM = s.VLSHATMAX;
                }
            }
            else s.CRTGD = 1;
            if (s.PENFLG > 0 && s.CRTGD == 0) s.BRK = 4;
        }

        public static void BendLogic(Facehard69LegacyState s)
        {
            if (s.BEND == 1 && s.SHAT == 0)
            {
                if (s.OB >= s.OBCRIT)
                {
                    if (s.CARDONALD == 1) s.BRK = s.VS >= s.VSCRIT ? 0 : 7;
                    else s.BRK = 8;
                }
                else if ((s.BRAIK == 0 && s.PENFLG < 2) || s.PENFLG == 2) s.BRK = 0;
                else s.BRK = 9;
            }
        }

        public static void DamageCalc(Facehard69LegacyState s)
        {
            // DAMAGECALC, FH69MAIN.BAS:1199-1311.
            // SHATRD PROJ DAMAGE LOGIC.
            // UNSHATRD PROJ COMPLETE PEN NOSE & BODY DAM LOGIC.
            // ONLY COMPUTE NOSE DAMAGE USING CRIT NOSE (OB-EX) LOGIC IF VEL>=HBL ("PENFLG">0),
            // WHICH RELIEVES FORCE ON NOSE DUE TO INITIAL IMPACT, BUT ADDS TO TWISTING FORCES ON NOSE.
            // RICOCHET & HOLING-ONLY PEN LOGIC (SKIP IF COMPLETE PEN) W/O CURVED-PLATE RULE.
            var vs = s.VS;
            s.CRVRL = 0; s.BDYDM = 0; s.BRK = 0; s.NSBRK = 0;
            var obrk = s.CMPND == 1 ? 50 : 40;
            if (s.PENFLG == 1 && s.CRVFLAG == 1) s.CRVRL = 1;
            if (s.SHAT == 1) ShatrDam(s);
            else
            {
                NoseDam(s);
                if (s.PENFLG < 2) NoseBroke(s);
                else if (s.OB - s.EX > s.NDAP) s.NSBRK = 3;
                else if ((s.ARMOR == 3 || s.ARMOR == 4) && s.BEND == 2) s.NSBRK = 8;
            }
            if (s.VITRU == -1) { DoCrtGood(s); BendLogic(s); }
            else if (vs < s.VITRU && s.BRK == 0 && s.PENFLG == 2) s.BRK = 3;
            if (s.PENFLG < 2 && s.BRK == 0)
            {
                var shtrsFlg = s.SHATRES == 2 || (s.SHATRES < 2 && s.PENFLG == 0) ? 1 : 0;
                if (s.BRAIK > 0 && (s.CMPND == 0 || (s.CMPND == 1 && shtrsFlg == 1))) s.BRK = 5;
                else if (s.OB > obrk && (s.PENFLG == 0 || s.SHATRES > 0)) s.BRK = 6;
                else if (s.VITRU > s.VLMT) s.BRK = 3;
            }
            if (s.NSSHAT == 2) s.BRK = 0;
            else if (s.CRVRL == 1 && s.BRAIK == 0) { s.BRK = 0; s.NSSHAT = 3; }
            else if ((s.NSSHAT == 1 || s.NSSHAT == 4) && vs >= s.VLMT) s.BRK = 0;
            if (s.BRK > 0)
            {
                if (s.SHATRES == 2)
                {
                    if (s.NSBRK == 0) s.NSBRK = 1;
                    s.BDYDM = 2;
                }
                else if (s.BDYDM == 0) s.BDYDM = 1;
            }
        }

        public static void PlugCalc(Facehard69LegacyState s)
        {
            var vs = s.VS;
            double plugMult;
            if (s.SHAT == 1) plugMult = 1.5;
            else if (s.CART == 1) plugMult = 2;
            else if (s.CART == 2)
            {
                plugMult = 1 / Math.Cos(s.OBRAD);
                if (plugMult > 2) plugMult = 2;
            }
            else plugMult = 1;
            s.RNDPLUGWT = 0.011 * s.TA * (Math.Pow(s.TA, 2) + 4.5 * s.D * s.TA + 20.25 * Math.Pow(s.D, 2)) * plugMult;
            s.NORMPLUGWT = s.RNDPLUGWT;
            if (s.OB >= 45) s.NORMPLUGWT /= Math.Cos(2 * (s.OBRAD - 0.7853981));
            s.DELTAPLUGWT = s.RNDPLUGWT * (1 / Math.Cos(s.EXRAD) - 1);
            if (vs < s.VLMT && ((s.SHAT == 1 && s.OB >= 45) || s.SHAT == 0)) s.DELTAPLUGWT = 0;
            if (s.TPCAL < s.THIN && vs >= s.VLSHAT && s.VLTRU > s.VLSHAT)
            {
                var npwtpr = s.NORMPLUGWT / Math.Cos(s.OBRAD);
                if (s.TPCAL < s.THIN && s.TPCAL > s.TRUTHIN) npwtpr = (npwtpr + s.NORMPLUGWT) / 2;
                if (npwtpr < 2 * s.NORMPLUGWT) s.NORMPLUGWT *= 2;
                else s.NORMPLUGWT = npwtpr;
            }
            s.TOTPLUGWT = s.NORMPLUGWT + s.DELTAPLUGWT;
        }

        // PLUGWTS bridges to the plug-weight calculation used by remaining-velocity logic.
        public static void PlugWts(Facehard69LegacyState s) => PlugCalc(s);

        public static void PenVrvPlgCalc(Facehard69LegacyState s)
        {
            // PENVRVPLGCALC, FH69SBM2.BAS:795-894.
            var vs = s.VS;
            if (s.HF == 1 || vs < s.VLMT)
            {
                if (s.OB < 45)
                {
                    var unshtOrHardHead = s.SHAT == 0 || (s.SHAT == 1 && s.HARD == 2 && vs < s.VHSHAT && s.VHOL < s.VHSHAT);
                    if (unshtOrHardHead)
                    {
                        if (s.BRK != 0) { s.PENTP = 5; s.VR = -1; s.VDPLUG = s.VRSHATNS; }
                        else
                        {
                            s.VR = -1; s.TOTPLUGWT = s.NORMPLUGWT; s.DELTAPLUGWT = 0;
                            if (s.NSBRK > 0) { s.PENTP = 3; s.VDPLUG = 0; }
                            else { s.PENTP = 2; s.VDPLUG = -1; }
                        }
                    }
                    else if (s.BRK != 0) { s.PENTP = 5; s.VR = -1; s.VDPLUG = s.VRSHATNS; }
                    else
                    {
                        s.VR = -1; s.TOTPLUGWT = s.NORMPLUGWT; s.DELTAPLUGWT = 0;
                        if (s.NSBRK > 0) { s.PENTP = 3; s.VDPLUG = 0; }
                        else { s.PENTP = 2; s.VDPLUG = -1; }
                    }
                }
                else if (s.BRK > 0) { s.PENTP = 4; s.VDPLUG = s.VRSHATNS; s.VR = -1; }
                else
                {
                    s.VR = -1; s.TOTPLUGWT = s.NORMPLUGWT; s.DELTAPLUGWT = 0;
                    if (s.NSBRK > 0) { s.PENTP = 3; s.VDPLUG = -1; }
                    else { s.PENTP = 2; s.VDPLUG = -1; }
                }
            }
            else
            {
                s.PENTP = 6;
                if (s.SHAT == 1)
                {
                    s.VR = s.VRPR; s.VDPLUG = s.VRSHATNS; if (s.VDPLUG < s.VR) s.VDPLUG = s.VR;
                }
                else if (s.OB < 45 || (s.OB >= 45 && s.BDYDM == 0))
                {
                    s.VR = s.VRPR; s.VDPLUG = s.VR;
                }
                else
                {
                    s.VR = s.VRPR; s.VDPLUG = s.VRSHATNS; if (s.VDPLUG < s.VR) s.VDPLUG = s.VR;
                }
            }
            s.VNPLUG = QbInt(s.VNPLUG);
            s.VDPLUG = QbInt(s.VDPLUG);
            s.VR = QbInt(s.VR);
        }

        public static void FinalResults(Facehard69LegacyState s)
        {
            // FINALRESULTS, FH69MAIN.BAS:1331-1441.
            // NEXT LINES GIVE EFFECTS IF NO HOLE MADE COMPLETELY THRU PLATE.
            // "PENTP=0" MEANS THAT ONLY SHOCK EFFECTS OCCUR BEHIND PLATE UNLESS MODIFIED
            // TO "PENTP = 1" FOR SPLINTERS BEING KNOCKED FROM PLATE BACK.
            // REMAINING VEL CALC NEEDS TO REMOVE NORMAL PLUG ENERGY & ENERGY LOST TEARING THRU PLATE BETWEEN HBL & NBL.
            var vs = s.VS;
            if (vs < s.VHOL)
            {
                // NO PEN THRU PLATE ACHIEVED (EXCEPT FOR SMALL POSSIBLE BACK-SPALLING & A SMALL HOLE IF SHATR NEAR HBL).
                s.PENTP = 0;
                if (s.BKEFF <= 0 && s.CART > 0) s.PENTP = 1;
                s.TOTPLUGWT = s.NORMPLUGWT = s.DELTAPLUGWT = 0;
                s.VR = s.VNPLUG = s.VDPLUG = -1;
                s.EX = s.EXRAD = s.OBDF = -1;
                s.HF = -1;
                s.VRPR = -1;
                return;
            }
            var keTotal = 0.5 * s.WB * Math.Pow(vs, 2);
            var kePunch = 0.5 * s.WB * Math.Pow(s.VHDAM, 2);
            var v1 = QbSqr((keTotal - kePunch) / (0.5 * s.WB));
            s.VNPLUG = QbSqr(s.WB / (s.WB + s.NORMPLUGWT)) * v1 * Math.Cos(s.OBRAD);
            var vspr = s.VNPLUG / Math.Cos(s.OBRAD);
            var kevspr = 0.5 * s.WB * Math.Pow(vspr, 2);
            var keobmnsex = 0.5 * s.WB * Math.Pow(vspr, 2) * (Math.Pow(Math.Sin(s.OBRAD), 2) - Math.Pow(Math.Sin(s.EXRAD), 2));
            var v2 = QbSqr((kevspr - keobmnsex) / (0.5 * s.WB));
            double bfract;
            if (s.BRK > 0) bfract = 0.5;
            else if (s.NSBRK > 0) bfract = 0.667;
            else bfract = 1;
            s.VRSHATNS = v2 * QbSqr(s.WB / (s.WB + (1 - bfract) * s.DELTAPLUGWT));
            if (vs < s.VLMT) s.VRPR = -1;
            else
            {
                var vdfCalc = s.SHAT == 1 ? s.SHATVDFPR : s.VDFUSEDPR;
                var topVel = 1 / (1 - vdfCalc) * s.VHDAM;
                if (s.VLDAM < topVel) topVel = s.VLDAM;
                var kenbl = 0.5 * s.WB * Math.Pow(topVel, 2);
                var v1nbl = QbSqr((kenbl - kePunch) / (0.5 * s.WB));
                var vnplugnbl = QbSqr(s.WB / (s.WB + s.NORMPLUGWT)) * v1nbl * Math.Cos(s.OBRAD);
                var vnblpr = vnplugnbl / Math.Cos(s.OBRAD);
                var kenblpr = 0.5 * s.WB * Math.Pow(vnblpr, 2);
                var kenome = 0.5 * s.WB * Math.Pow(vnblpr, 2) * (Math.Pow(Math.Sin(s.OBRAD), 2) - Math.Pow(Math.Sin(s.EXRAD), 2));
                var v2nbl = QbSqr((kenblpr - kenome) / (0.5 * s.WB));
                s.VRPR = QbSqr((s.WB / (s.WB + bfract * s.DELTAPLUGWT)) * (Math.Pow(v2, 2) - Math.Pow(v2nbl, 2)));
            }
            PenVrvPlgCalc(s);
            s.VTOTAL = s.VR >= 0 && s.VDPLUG >= 0 ? (s.VR + s.VDPLUG) / 2 : Math.Max(s.VR, s.VDPLUG);
        }

        public static void EffVelInit(Facehard69LegacyState s)
        {
            s.MINEV = 0; s.NOTEFLAG = 0; s.NSFLG = 0; s.CRITVEL = 0; s.NOSEVEL = 0; s.NVRFLAG = 0;
            s.MINEV1 = 0; s.MINEV2 = 0; s.MINEV3 = 0; s.MINEV4 = 0; s.MINEV5 = 0; s.NSTEST = 0; s.NSTESTV = 0;
            s.MAXDIFF = s.SHAT == 1 ? 0 : s.OB > 15 ? 15 : s.OB;
        }

        public static void ApCriticalV(Facehard69LegacyState s)
        {
            s.THVAL = s.CRTAPR; s.VLEXREV = s.VLMT; s.VHEXREV = s.VHOL; ThresholdCalc(s); s.CRITVEL = s.THSPD;
        }

        public static void NsCriticalV(Facehard69LegacyState s)
        {
            s.THVAL = s.NDAP; s.VLEXREV = s.VLMT; s.VHEXREV = s.VHOL; ThresholdCalc(s);
            var nsv = s.THSPD;
            if (s.NSTEST == 1) s.NSTESTV = s.VHOL >= nsv ? s.VHOL : nsv;
            else s.NSVEL = s.VHOL >= nsv ? s.VHOL : nsv;
        }

        public static void MinEvShatCalc(Facehard69LegacyState s)
        {
            if (s.NSSHAT == 0 || (s.NSSHAT > 0 && s.LTCASE > 1)) { s.MINEV1 = -1; s.NOTEFLAG = 0; }
            else if (s.NSSHAT == 1) { s.MINEV1 = s.VLMT; s.NOTEFLAG = 1; }
            else if (s.NSSHAT == 2) { s.MINEV1 = s.VHOL; s.NOTEFLAG = 0; }
            else if (s.NSSHAT == 3) { s.MINEV1 = -1; s.NOTEFLAG = 0; }
            else if (s.NSSHAT == 4) { s.MINEV1 = s.VLMT; s.NOTEFLAG = 1; }
        }

        public static void SetMinEv(Facehard69LegacyState s)
        {
            if (s.MINEV1 >= 0 && s.MINEV < s.MINEV1) s.MINEV = s.MINEV1;
            if (s.MINEV < s.MINEV2) s.MINEV = s.MINEV2;
            if (s.MINEV3 > 0 && s.MINEV < s.MINEV3) s.MINEV = s.MINEV3;
            if (s.MINEV < s.MINEV4) s.MINEV = s.MINEV4;
            if (s.MINEV < s.MINEV5) s.MINEV = s.MINEV5;
        }

        public static void SetupSecPg(Facehard69LegacyState s)
        {
            // SETUPSECPG numeric portion, FH69MAIN.BAS:1922-2050.
            // CALCULATE "EFFECTIVE" LIMIT "MINEV" FOR THIS IMPACT FROM ALL DATA.
            LmtStrings(s);
            EffVelInit(s);
            var obrk = s.CMPND == 1 ? 50 : 40;
            if (s.SHAT == 1) MinEvShatCalc(s);
            else
            {
                if (s.LTCASE > 0)
                {
                    if (s.LTCASE == 2)
                    {
                        NsCriticalV(s);
                        var mtmp = QbInt(s.NSVEL);
                        s.MINEV2 = mtmp == s.NSVEL ? mtmp : mtmp + 1;
                    }
                    else s.MINEV2 = s.VLMT;
                }
                else s.MINEV2 = 0;
                if (s.VITRU == -1 && s.CRTAPR > 0)
                {
                    ApCriticalV(s);
                    var mtmpcr = QbInt(s.CRITVEL);
                    if (mtmpcr < s.CRITVEL) mtmpcr++;
                    s.NSTEST = 1;
                    NsCriticalV(s);
                    var mtmpns = QbInt(s.NSTESTV);
                    if (mtmpns < s.NSTESTV) mtmpns++;
                    double mtmp;
                    if (mtmpns >= s.VLMT) mtmp = mtmpcr;
                    else if (mtmpns < s.VLMT && mtmpcr <= mtmpns) mtmp = mtmpcr;
                    else if (mtmpns < s.VLMT && mtmpcr > mtmpns && mtmpcr < s.VLMT) mtmp = mtmpns;
                    else mtmp = mtmpcr;
                    s.MINEV3 = mtmp;
                    if (s.VLMT == s.VLND && s.BRAIK == 0 && s.MINEV3 < s.VLMT) s.MINEV3 = s.VHOL;
                }
                else if (s.VITRU > -1)
                {
                    s.MINEV3 = s.VITRU;
                    if (s.VITRU == s.VLND && s.BRAIK == 0) s.MINEV3 = s.VHOL;
                }
                else if (s.BEND == 1 && s.OB >= s.OBCRIT) s.MINEV3 = s.CARDONALD == 1 ? s.VSCRIT : -1;
                else s.MINEV3 = 0;
                if (s.OB > obrk) s.MINEV4 = s.CRVFLAG == 0 ? s.VLMT : 0;
                if (s.BRAIK > 0)
                {
                    if (s.CMPND == 1 && s.SHATRES < 2) s.MINEV5 = s.VHOL;
                    else s.MINEV5 = s.VLMT;
                }
                else s.MINEV5 = 0;
            }
            SetMinEv(s);
            if (s.MINEV > 4000) s.MINEV = 4001;
            s.VELLTRU = FormatVelocity69(s.VLTRU);
            s.VELLSHAT = FormatVelocity69(s.VLSHAT);
            s.VELLSHATMAX = FormatVelocity69(s.VLSHATMAX);
            s.VELLND = FormatVelocity69(s.VLND);
            s.VELHTRU = FormatVelocity69(s.VHTRU);
            s.VELHSHAT = FormatVelocity69(s.VHSHAT);
            s.VELHSHATMAX = FormatVelocity69(s.VHSHATMAX);
            s.VELHND = FormatVelocity69(s.VHND);
        }

        public static string FormatVelocity69(double value)
        {
            if (value > 4000) return ">4000 ft/sec";
            return Invariant($"{value} ft/sec");
        }

        public static void LmtStrings(Facehard69LegacyState s)
        {
            s.RESNOTE = " BL USED FOR PENETRATION MARKED BY '-!-'/BL USED FOR POST-IMPACT LOGIC BY '-#-'";
            s.NBL1 = "N1> Navy BL w/o shatter, but all other damage & given AP cap =";
            s.NBL2 = "N2> Navy BL w/  shatter  and all other damage & given AP cap =";
            s.NBL3 = "(Worst (maximum) NBL at low OB; replaces N1 when OB <= 45 deg and N2 < N1)";
            s.NBL4 = "N3> Navy BL w/  shatter, but no  other damage & given AP cap =";
            s.NBL5 = "(Replaces N2 (& N1/N4 if N2 does) @ NBL or VS if no    shatter-changing damage)";
            s.NBL6 = "N4> Navy BL if unshattered/undeformed body and given AP cap  =";
            s.NBL7 = "(Best (minimum) NBL; replaces N1 @ NBL or VS if no penetration-changing damage)";
            s.HBL1 = "H1> Holing BL without shatter using given AP cap             =";
            s.HBL2 = "(Unshattered HBL if < H2 and non-shatter damage reduces penetration @ HBL)";
            s.HBL3 = "(Includes Japanese uncapped Type 91 AP projectile w/cap head in place)";
            s.HBL4 = "H2> Holing BL with shatter (AP cap, if any, did not work)    =";
            s.HBL5 = "(Replaces H1 if H2 <= H1 (smaller hole) or other damage adds to shatter @ HBL)";
            s.HBL6 = "H3> Holing BL with shatter, but no other kind of damage      =";
            s.HBL7 = "(Best high-OB HBL; replaces H2 @ HBL or VS if no     shatter-changing damage)";
            s.HBL8 = "H4> Holing BL if unshattered/undeformed body & given AP cap  =";
            s.HBL9 = "(Best low -OB HBL; replaces H1 @ HBL or VS if no penetration-changing damage)";
        }

        public static void DamageSetup(Facehard69LegacyState s)
        {
            // DAMAGESETUP, FH69SBM2.BAS:94-216, translated to structured state.
            // SET UP TO PRINT PROJECTILE DAMAGE MESSAGES.
            // CURVED-PLATE RULE APPLIED; REGULAR CURVED-PLATE RULE FORCES NOSE-ONLY SHATR
            // OR LOWERS EFFECTIVE LIMIT TO HBL DEPENDING ON THE SELECTED MINIMUM EFFECTIVE VELOCITY.
            if (s.NSBRK == 8)
            {
                s.EFFPRINT1 = " HOLING BL (TOUGH PROJ VS HARVEY ARMOR)";
                return;
            }
            if (s.CRVFLAG == 1 && (s.MINEV <= 0 || s.MINEV >= s.VHOL))
            {
                if (s.MINEV == 0 || s.MINEV1 == -1 || s.MINEV3 == -1) { s.MINEV = 0; s.NOTEFLAG = 2; }
                else if (s.MINEV > s.VHOL && s.MINEV <= s.VLMT) s.NOTEFLAG = 3;
                else if (s.MINEV > s.VLMT) s.NOTEFLAG = 4;
            }
            var gotoSoftShatRule = false;
            if (s.LTCASE > 0)
            {
                if (s.SHAT == 1)
                {
                    s.EFFPRINT1 = " NEVER (NOSE SHATTER REACHES CAVITY)";
                    gotoSoftShatRule = true;
                }
                if (!gotoSoftShatRule && s.MINEV2 > 0 && s.MINEV == s.MINEV2)
                {
                    s.NSFLG = 1;
                    s.NOTEFLAG = 0;
                }
            }
            if (!gotoSoftShatRule)
            {
                if (s.NOTEFLAG >= 2)
                {
                    if (s.NOTEFLAG == 4)
                    {
                        s.EFFVEL = FormatVelocity69(s.MINEV);
                        if (s.VHOL <= 4000) s.PAND = " *AND*";
                    }
                    if ((s.NOTEFLAG == 2 || s.NOTEFLAG == 4) && s.VHOL <= 4000)
                    {
                        s.HBLTONBL = " BETWEEN HOLING BL & NAVY BL (SEE BELOW)";
                        ApplyCurvedPlateNote(s);
                        return;
                    }
                }
                if (s.NOTEFLAG == 3) s.MINEV = s.VHOL;
                else if (s.MINEV == 0 && s.MINEV1 != -1 && s.MINEV3 != -1)
                {
                    s.EFFPRINT1 = " USUALLY EFFECTIVE (CAVITY IMMUNE TO NOSE DAMAGE)";
                    gotoSoftShatRule = true;
                }
            }
            if (!gotoSoftShatRule)
            {
                if (s.MINEV == 0 && s.MINEV1 == -1) { s.EFFPRINT1 = " NEVER (COMPLETE SHATTER)"; return; }
                if (s.MINEV3 == -1) { s.NVRFLAG = 1; s.EFFPRINT1 = " RARELY EFFECTIVE.  EXCEEDS BREAKAGE ANGLE (DEGREES):"; return; }
                if (s.HARD == -1 && s.SHATRES < 2 && s.MINEV <= s.VLMT)
                {
                    s.EFFPRINT1 = " NAVY BL"; s.NVRFLAG = 0; s.NSFLG = 1;
                }
                else if (s.MINEV != s.VLMT && s.MINEV > s.VHOL) s.EFFVEL = FormatVelocity69(s.MINEV);
                else if (s.MINEV == s.VLMT) s.EFFPRINT1 = " NAVY BL";
                else
                {
                    s.EFFPRINT1 = " HOLING BL";
                    if (s.NOTEFLAG == 3)
                    {
                        s.EFFPRINT2 = " (DUE TO CURVED PLATE) (SEE BELOW)";
                        ApplyCurvedPlateNote(s);
                        return;
                    }
                }
                if (s.NVRFLAG == 0 && s.NSFLG == 1) s.EFFPRINT2 = " (NOSE DAMAGE REACHES CAVITY)";
            }
            ApplySoftShatNote(s);
        }

        static void ApplyCurvedPlateNote(Facehard69LegacyState s)
        {
            s.NOTE1 = "SPECIAL CURVED-PLATE RULE FOR BODY DAMAGE:";
            s.NOTE2 = " Strong steel projectiles frequently remain effective against curved plates at";
            s.NOTE3 = " over 45 degrees obliquity if Striking Velocity is between Holing BL & Navy BL.";
            if (s.SHAT == 1) s.NOTE4 = " Complete shatter of this projectile will occur otherwise.";
            else if (s.BEND == 1 && s.OB >= s.OBCRIT && s.CARDONALD == 0) s.NOTE4 = " Complete breakup of this projectile will occur otherwise.";
            else if (s.MINEV == s.VHOL) s.NOTE4 = " Other effects keep projectile effective above Navy BL.";
        }

        static void ApplySoftShatNote(Facehard69LegacyState s)
        {
            if (s.NOTEFLAG == 1)
            {
                s.NOTE1 = s.HARD == 1 ? "SPECIAL SOFT CAP & EXTRA-TOUGH PLATE NOSE-ONLY SHATTER RULE:" : "SPECIAL HOOD & NOT-EXTRA-TOUGH PLATE NOSE-ONLY SHATTER RULE:";
                s.NOTE2 = "Nose-only shatter if Complete Penetration @ OB<=15deg, sometimes if 15<OB<20deg.";
                s.NOTE3 = "Complete shatter of this projectile against this armor will occur otherwise.";
            }
        }

        public static string NoseDamageWord(Facehard69LegacyState s)
        {
            if (s.NSBRK == 8) return "deformed";
            if (s.NSBRK == 2) return "shattered";
            if (s.NSBRK > 0) return "broken";
            return "intact";
        }

        public static void NsBdyDamPrnt(Facehard69LegacyState s)
        {
            s.BDYDM1 = s.BDYDM2 = s.BDYDM3 = s.BDYDM4 = s.BDYDM5 = s.NSBRK1 = "";
            var nd = NoseDamageWord(s);
            const string bd1 = " about ";
            var noPrnt = 0;
            if (s.BDYDM == 0)
            {
                if (s.PENFLG == 2)
                {
                    if (s.NSBRK == 8)
                    {
                        s.BDYDM1 = "Projectile nose upset and deformed, but little lower body damage from impact.";
                        s.BDYDM2 = " It is still 'effective' and intact, other than losing nose coverings.";
                    }
                    else if (s.NSBRK > 0 && s.NSBRK < 8)
                    {
                        s.BDYDM1 = Invariant($"Projectile nose {nd} by impact, but lower body undamaged.");
                        s.BDYDM2 = " It is only 'effective' if this nose damage does not reach explosive cavity.";
                        if (nd != "deformed") s.BDYDM3 = Invariant($" Nose loses{bd1}33% body weight and what's left is much weaker.");
                    }
                    else
                    {
                        s.BDYDM1 = "Neither projectile nose nor lower body damaged by impact.";
                        s.BDYDM2 = " It is still 'effective' and intact other than losing nose coverings.";
                    }
                }
                else
                {
                    if (s.NSBRK == 8) s.BDYDM1 = "Projectile nose upset & deformed; usually only insignificant lower body damage.";
                    else if (s.NSBRK > 0 && s.NSBRK < 8)
                    {
                        s.BDYDM1 = Invariant($"Projectile nose {nd} so 50% chance of major internal lower body damage.");
                        if (nd != "deformed") s.BDYDM3 = Invariant($" Nose loses{bd1}33% body weight and what's left is much weaker.");
                    }
                    else s.BDYDM1 = "Projectile nose intact so usually insignificant lower body damage occurs.";
                    s.BDYDM2 = " It is still 'effective' unless lower body damage is major internal damage or if";
                    s.BDYDM4 = "  nose damage reaches explosive cavity, then projectile will not be effective.";
                    s.BDYDM5 = " 'Major internal'=base plug/fuze broken, deep cracks or base pieces snapped off.";
                }
            }
            else if (s.BDYDM == 1)
            {
                if (s.NSBRK == 0) s.BDYDM1 = "Projectile nose undamaged, but lower body damaged by impact.";
                else if (s.NSBRK == 8) s.BDYDM1 = "Projectile nose upset and deformed and lower body damaged by impact.";
                else
                {
                    s.BDYDM1 = Invariant($"Projectile nose {nd} and lower body damaged by impact.");
                    if (nd != "deformed") s.BDYDM3 = Invariant($" Nose loses{bd1}33% body weight and what's left is much weaker.");
                }
                if ((s.SHATRES > 0 && s.CMPND == 0) || (s.SHATRES == 2 && s.CMPND == 1))
                {
                    s.BDYDM2 = " It is not 'effective' and lower body is always broken apart by impact.";
                    s.BDYDM3 = "";
                    noPrnt = 1;
                }
                else
                {
                    s.BDYDM2 = " It is not 'effective' and lower body always suffers major internal damage.";
                    s.BDYDM5 = " 'Major internal'=base plug/fuze broken, deep cracks or base pieces snapped off.";
                    noPrnt = 1;
                }
            }
            else if (s.BDYDM == 2)
            {
                s.BDYDM1 = Invariant($"Projectile nose {nd} and lower body damaged by impact.");
                s.BDYDM2 = " It is not 'effective' and lower body is always broken apart by impact.";
                noPrnt = 1;
            }
            if (s.NSBRK == 0 || s.NSBRK == 8) s.NSBRK1 = "If any, only projectile nose coverings stripped off. Nose still in one piece.";
            else if (s.NSBRK < 8 && noPrnt == 0)
            {
                if (s.LTCASE > 0) s.NSBRK1 = " Projectile's large explosive cavity is 'ineffective' due to major nose damage.";
                else s.NSBRK1 = " Projectile's small explosive cavity rarely affected by any nose breakage.";
            }
        }

        public static void PenPrint(Facehard69LegacyState s, double? pntp = null)
        {
            var p = pntp ?? s.PENTP;
            s.PEN1 = s.PEN2 = s.PEN3 = "";
            if (p == 0)
            {
                s.PEN1 = "NO HOLING OF PLATE: Only impact shock & plate distortion damage behind plate";
                s.PEN2 = " due to ductile plate &/or a plate backing layer (Projectile may be damaged)";
            }
            else if (p == 1)
            {
                s.PEN1 = "NO HOLING OF PLATE: Impact shock & plate distortion & thrown splinters cause";
                s.PEN2 = " damage behind this brittle plate w/o any backing (Projectile may be damaged)";
            }
            else if (p == 2)
            {
                s.PEN1 = "PLATE HOLED AND NORMAL PLUG PUNCHED OUT, BUT INTACT PROJECTILE REJECTED";
                s.PEN2 = " (No significant projectile damage suffered due to impact)";
                s.PEN3 = " Proj might imbed in plate near NBL. If so, filler may cause damage thru plate.";
            }
            else if (p == 3)
            {
                s.PEN1 = "PLATE HOLED AND NORMAL PLUG PUNCHED OUT, BUT DAMAGED PROJECTILE REJECTED";
                s.PEN2 = " (If nose breaks up at OB<45 deg, part of nose (<= 33% body weight) penetrates)";
                s.PEN3 = " Proj might imbed in plate near NBL. If so, filler may cause damage thru plate.";
            }
            else if (p == 4)
            {
                s.PEN1 = "PARTIAL PENETRATION: Broken-up lower body (= 50% body weight) penetrates, but";
                s.PEN2 = " projectile nose ricochets (Major damage behind plate if filler explodes/burns)";
            }
            else if (p == 5)
            {
                s.PEN1 = "PARTIAL PENETRATION: Broken-up nose & upper body (= 50% body weight) penetrate,";
                s.PEN2 = " but projectile lower body rejected (Filler rarely has any effect behind plate)";
                s.PEN3 = " Proj might imbed in plate near NBL. If so, filler may cause damage thru plate.";
            }
            else if (p == 6)
            {
                s.PEN1 = "COMPLETE PENETRATION ACHIEVED: If Exit Angle > 0, Delta Plug pieces ejected";
                s.PEN2 = " (If projectile is broken up, at least 80% of body weight exits plate back)";
            }
        }

        public static void ProjMotionPrnt(Facehard69LegacyState s)
        {
            s.WBL1 = s.WBL2 = s.WBL3 = s.WBL4 = "";
            const string wblg = "wobbling or tumbling ";
            const string dfl = "change in direction.";
            const string dmg = "damage.";
            if (s.OBDF > 45) { s.WBL1 = wblg; s.WBL2 = "due to extreme "; s.WBL3 = dfl; }
            else if (s.OBDF > 30 && s.NSBRK == 0 && s.BDYDM == 0) { s.WBL1 = "has est. 67% chance of wobbling due to "; s.WBL2 = dfl; }
            else if (s.OBDF > 15 && s.NSBRK == 0 && s.BDYDM == 0) { s.WBL1 = "has est. 33% chance of wobbling due to "; s.WBL2 = dfl; }
            else if (s.OBDF > 30 && (s.NSBRK > 0 || s.BDYDM > 0)) { s.WBL1 = wblg; s.WBL2 = "due to "; s.WBL3 = dmg; }
            else if (s.OBDF > 15 && (s.NSBRK > 0 || s.BDYDM > 0)) { s.WBL1 = "has est. 67% chance of "; s.WBL2 = wblg; s.WBL3 = "due to "; s.WBL4 = dmg; }
            else if (s.NSBRK > 0 || s.BDYDM > 0) { s.WBL1 = "has est. 33% chance of "; s.WBL2 = wblg; s.WBL3 = "due to "; s.WBL4 = dmg; }
            else s.WBL1 = "is almost always moving nose-first with little or no wobble.";
        }

        public static void SplntrPrnt(Facehard69LegacyState s)
        {
            s.FLAKE = "";
            if (s.NORMPLUGWT <= 0.05)
            {
                if (s.BKEFF == 0)
                {
                    if (s.CART > 0) s.FLAKE = "Many dangerous splinters thrown from plate back likely due to impact shock.";
                    else s.FLAKE = "Few dangerous splinters thrown from plate back due to impact shock.";
                }
                else s.FLAKE = "Backing material stops splinters thrown from plate back due to impact shock.";
            }
        }

        public static void RemVelPrnt(Facehard69LegacyState s)
        {
            var vs = s.VS;
            s.RVU = s.BSNS1 = s.BSNS2 = s.BSNS3 = s.ONEPC = s.DPLG = "";
            if (s.VDPLUG < 0)
            {
                s.RVU = "Projectile Remaining Velocity NOT DEFINED";
                return;
            }
            if (s.VDPLUG != s.VR)
            {
                var vtmp = (Math.Pow(s.VDPLUG, 2) + Math.Pow(s.VR, 2)) / 2;
                s.VTOTAL = QbInt(QbSqr(vtmp) + 0.5);
            }
            if (s.DELTAPLUGWT > 0.05) s.DPLG = "& Delta Plug ";
            if (s.SHAT == 0 || s.PENTP == 3)
            {
                if (s.OB < 45)
                {
                    if (s.PENTP == 2) s.BSNS3 = "No Part of Projectile completely penetrates plate.";
                    if (s.PENTP == 3)
                    {
                        if (s.SHAT == 1 || (s.SHAT == 0 && s.NSBRK > 0 && s.NSBRK < 8))
                        {
                            s.BSNS1 = "Nose Pieces ";
                            s.BSNS3 = "Projectile Body up to forward bourrelet fails to completely penetrate.";
                        }
                        else s.BSNS3 = "No Part of Projectile completely penetrates plate.";
                    }
                    if (s.PENTP == 5)
                    {
                        s.BSNS1 = s.NSBRK == 0 || s.NSBRK == 8 ? "Nose & Upper Body " : "Nose Pieces & Upper Body";
                        if (s.BDYDM < 2)
                        {
                            s.BSNS3 = "Projectile Lower Body fails to completely penetrate.";
                            s.ONEPC = "If Projectile Body not broken up, No Part of Projectile completely penetrates.";
                        }
                        else s.BSNS3 = "Projectile Lower Body Pieces fail to completely penetrate.";
                    }
                    if (s.PENTP == 6)
                    {
                        if (s.BDYDM == 1)
                        {
                            if (s.VDPLUG != s.VR)
                            {
                                s.BSNS1 = s.NSBRK == 0 || s.NSBRK == 8 ? "Nose & Upper Body " : "Nose Pieces & Upper Body ";
                                s.BSNS2 = "Lower Body ";
                                s.ONEPC = "If Proj not broken, it ";
                            }
                            else s.BSNS1 = "Entire Projectile ";
                        }
                        else if (s.BDYDM == 2)
                        {
                            if (s.VDPLUG != s.VR) { s.BSNS1 = "Nose & Upper Body Pieces "; s.BSNS2 = "Lower Body Pieces "; }
                            else s.BSNS1 = "All Projectile Pieces ";
                        }
                        else s.BSNS1 = "Entire Projectile ";
                    }
                }
                else
                {
                    if (s.PENTP == 2) s.BSNS3 = "No Part of Projectile completely penetrates plate.";
                    if (s.PENTP == 3) s.BSNS3 = "Entire Projectile ricochets off of plate.";
                    if (s.PENTP == 4)
                    {
                        if (s.NSBRK == 0 || s.NSBRK == 8) { s.BSNS1 = "Lower Body "; s.BSNS3 = "Projectile Nose & Upper Body ricochet off of plate."; }
                        else { s.BSNS1 = "Lower Body & Some Nose Pieces "; s.BSNS3 = "Most of Projectile Nose & Upper Body Pieces ricochet off of plate."; }
                        s.ONEPC = "If Projectile Body not broken up, All of Projectile ricochets off of plate.";
                    }
                    if (s.PENTP == 6)
                    {
                        if (s.BDYDM == 1)
                        {
                            if (s.VDPLUG != s.VR)
                            {
                                s.BSNS2 = s.NSBRK == 0 || s.NSBRK == 8 ? "Nose & Upper Body " : "Nose Pieces & Upper Body ";
                                s.BSNS1 = "Lower Body ";
                            }
                            else s.BSNS1 = "Entire Projectile ";
                            s.ONEPC = "If Proj not broken, it ";
                        }
                        else if (s.BDYDM == 2)
                        {
                            if (s.VDPLUG != s.VR) { s.BSNS1 = "Lower Body Pieces "; s.BSNS2 = "Nose & Upper Body Pieces "; }
                            else s.BSNS1 = "All Pieces ";
                        }
                        else s.BSNS1 = "Entire Projectile ";
                    }
                }
            }
            else if (s.OB < 45)
            {
                s.BSNS1 = "All Nose Pieces ";
                if (s.VR >= 0)
                {
                    if (s.VDPLUG != s.VR) s.BSNS2 = "Body Pieces ";
                    else s.BSNS1 = "All Pieces ";
                }
                else if (vs >= s.VHSHAT) s.BSNS3 = "Projectile Body does not completely penetrate.";
            }
            else
            {
                s.BSNS1 = "Body & Some Nose Pieces ";
                if (s.VR >= 0)
                {
                    if (s.VDPLUG != s.VR) s.BSNS2 = "Most Nose Pieces ";
                    else s.BSNS1 = "All Pieces ";
                }
                else s.BSNS3 = "Most Projectile Nose Pieces do not completely penetrate.";
            }
        }

        static double RoundTo(double value, double scale)
        {
            return QbInt(scale * value + 0.5) / scale;
        }

        static string BackingTypeText(Facehard69LegacyState s)
        {
            switch ((int)s.BTP)
            {
                case 1: return "Wrought Iron (Q = 0.6)";
                case 2: return "Mild (Medium) Steel thru WWI (Q = 0.7)";
                case 3: return "High Tensile Steel thru WWI, Nickel Steel, Post-WWI Mild Steel (Q = 0.8)";
                case 4: return "Post-WWI High Tensile Steel & British/Japanese Ducol (D) Steel (Q = 0.9)";
                case 5: return "All Special Treatment (homogeneous Krupp-armor grade) Steels (Q = 1.0)";
                default: return "";
            }
        }

        public static List<string> ImpactPrntLines(Facehard69LegacyState s)
        {
            var lines = new List<string>();
            lines.Add(Invariant($"Projectile Diameter (Caliber)   = {s.D} inches -- Nation = {s.NATION} & Type = {s.PROJ}"));
            lines.Add(Invariant($"Projectile Striking Velocity    = {s.VS} ft/sec"));
            var obPrnt = RoundTo(s.OB, 100);
            var exPrnt = RoundTo(s.EX, 100);
            lines.Add(Invariant($"Angles, degrees:  Obliquity     = {obPrnt} & Exit = {(s.EX >= 0 ? F(exPrnt) : " NOT DEFINED")}"));
            lines.Add("All projectile nose coverings intact and in place on impact.");
            lines.Add(Invariant($"Projectile Weights, pounds: Original={RoundTo(s.WT, 100)}*Impact={RoundTo(s.WT, 100)}*Body={RoundTo(s.WB, 100)}"));
            return lines;
        }

        public static List<string> RemVelPrntLines(Facehard69LegacyState s)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(s.RVU)) return new List<string> { s.RVU };
            string SlowOrVelocity(double value) => value >= 25 ? Invariant($"= {value} ft/sec") : "ARE VERY SLOW";
            if (!string.IsNullOrEmpty(s.BSNS1)) lines.Add(Invariant($"{s.REMVEL}{s.BSNS1}{s.DPLG}{SlowOrVelocity(s.VDPLUG)}"));
            if (!string.IsNullOrEmpty(s.BSNS2)) lines.Add(Invariant($"{s.REMVEL}{s.BSNS2}{SlowOrVelocity(s.VR)}"));
            if (!string.IsNullOrEmpty(s.ONEPC))
            {
                if (s.PENFLG == 2)
                {
                    var have = !string.IsNullOrEmpty(s.DPLG) ? "have" : "has";
                    var rv = s.VTOTAL >= 25 ? Invariant($" Remaining Velocity = {s.VTOTAL} ft/sec") : " VERY SLOW Remaining Velocity";
                    lines.Add(Invariant($"{s.ONEPC}{s.DPLG}{have}{rv}"));
                    if (!string.IsNullOrEmpty(s.BSNS3)) lines.Add(s.BSNS3);
                }
                else
                {
                    if (!string.IsNullOrEmpty(s.BSNS3)) lines.Add(s.BSNS3);
                    lines.Add(s.ONEPC);
                }
            }
            else if (!string.IsNullOrEmpty(s.BSNS3)) lines.Add(s.BSNS3);
            return lines;
        }

        public static List<string> RenderReport(Facehard69LegacyState s)
        {
            var lines = new List<string>();
            if (s.PENFLG == 0 && (s.MINEV > s.VHOL || s.NOTEFLAG == 3 || s.BRAIK > 0))
            {
                s.BRK = 10; s.BDYDM = 1; NsBdyDamPrnt(s);
            }
            AddIf(lines, s.PEN1, s.PEN2, s.PEN3);
            AddIf(lines, s.BDYDM1, s.BDYDM2, s.BDYDM4, s.BDYDM5, s.NSBRK1, s.BDYDM3);
            lines.Add(Invariant($"Plate Type = {s.ARMOR}"));
            lines.Add(Invariant($"Plate Thickness, inches: Actual = {RoundTo(s.TA, 100)} & Effective ('Q'+backing) = {RoundTo(s.TEFF, 100)}"));
            if (s.WD == 0 && s.CMT == 0 && s.MTLBACK == 0) lines.Add("Backing Thickness, inches: NONE USED.");
            else
            {
                var backingParts = new List<string>();
                if (s.WD > 0) backingParts.Add(Invariant($"WOOD = {100 * s.WD}"));
                if (s.CMT > 0) backingParts.Add(Invariant($"CEMENT = {25 * s.CMT}"));
                if (s.MTLBACK > 0) backingParts.Add(Invariant($"METAL = {s.MTLBACK} ({s.NBK} plate{(s.NBK > 1 ? "s" : "")})"));
                lines.Add(Invariant($"Backing Thickness, inches: {string.Join(" & ", backingParts)}"));
                var btype = BackingTypeText(s);
                if (!string.IsNullOrEmpty(btype)) lines.Add(Invariant($"*TYPE: {btype}"));
            }
            lines.AddRange(ImpactPrntLines(s));
            if (s.PENFLG >= 2) lines.Add(Invariant($"Projectile {s.WBL1}{s.WBL2}{s.WBL3}{s.WBL4}"));
            if (s.NORMPLUGWT <= 0.05) lines.Add("No plugs ejected from plate.");
            else lines.Add(Invariant($"Plug Weights, pounds: Normal = {RoundTo(s.NORMPLUGWT, 10)} & Delta = {RoundTo(s.DELTAPLUGWT, 10)}"));
            if (!string.IsNullOrEmpty(s.FLAKE)) lines.Add(s.FLAKE);
            if (s.NORMPLUGWT > 0.05) lines.Add(Invariant($"Normal Plug Velocity = {s.VNPLUG} ft/sec"));
            if (s.BDYDM > 0) lines.Add("IF PROJECTILE BODY BREAKS, ASSUME 50% OF BODY WEIGHT IN UPPER AND LOWER HALVES.");
            lines.AddRange(RemVelPrntLines(s));
            s.REPORT = lines;
            return lines;
        }

        public static List<string> RenderSecondPage(Facehard69LegacyState s)
        {
            var lines = new List<string>();
            lines.Add("CALCULATED HOLING, NAVY, AND EFFECTIVE BALLISTIC LIMITS");
            if (!string.IsNullOrEmpty(s.RESNOTE)) lines.Add(s.RESNOTE);
            lines.Add(Invariant($"{s.NBL1} {s.VELLTRU} {s.N1}"));
            lines.Add(Invariant($"{s.NBL2} {s.VELLSHAT} {s.N2}"));
            lines.Add(s.NBL3);
            lines.Add(Invariant($"{s.NBL4} {s.VELLSHATMAX} {s.N3}"));
            lines.Add(s.NBL5);
            lines.Add(Invariant($"{s.NBL6} {s.VELLND} {s.N4}"));
            lines.Add(s.NBL7);
            lines.Add(Invariant($"{s.HBL1} {s.VELHTRU} {s.H1}"));
            lines.Add(s.HBL2);
            lines.Add(s.HBL3);
            lines.Add(Invariant($"{s.HBL4} {s.VELHSHAT} {s.H2}"));
            lines.Add(s.HBL5);
            lines.Add(Invariant($"{s.HBL6} {s.VELHSHATMAX} {s.H3}"));
            lines.Add(s.HBL7);
            lines.Add(Invariant($"{s.HBL8} {s.VELHND} {s.H4}"));
            lines.Add(s.HBL9);
            var effective = new List<string> { "Effective BL =" };
            if (!string.IsNullOrEmpty(s.EFFVEL)) effective.Add(s.EFFVEL);
            if (!string.IsNullOrEmpty(s.PAND)) effective.Add(s.PAND);
            if (!string.IsNullOrEmpty(s.HBLTONBL)) effective.Add(s.HBLTONBL);
            else effective.Add(Invariant($"{s.EFFPRINT1}{s.EFFPRINT2}{(s.NVRFLAG == 1 ? $" {s.OBCRIT}" : "")}").Trim());
            lines.Add(string.Join(" ", effective.FindAll(item => !string.IsNullOrEmpty(item))));
            AddIf(lines, s.NOTE1, s.NOTE2, s.NOTE3, s.NOTE4);
            if (!string.IsNullOrEmpty(s.NOTE5) && (s.BEND == 1 || (s.BEND == 0 && s.MINEV >= s.VHOL))) lines.Add(s.NOTE5);
            s.SECOND_PAGE_REPORT = lines;
            return lines;
        }

        public static List<string> RenderProcessReport(Facehard69LegacyState s)
        {
            var lines = new List<string>();
            var penFlagText = s.PENFLG == 0 ? "below HBL: no caliber-size hole" : s.PENFLG == 1 ? "between HBL and NBL: holing/partial result" : "at or above NBL: complete-penetration energy path considered";
            var shatterText = s.SHAT == 1 ? "Projectile shatter path is active for this impact." : "Projectile shatter path is not active for this impact.";
            var capText = s.HARD == 0 ? "No AP cap/hood effect remains at impact." :
                s.HARD == -1 ? "A hood is treated as the nose covering at impact." :
                s.HARD == 1 ? "A soft AP cap is treated as present at impact." :
                s.HARD == 2 ? "A hard AP cap/cap-head effect is treated as present at impact." :
                "A tough/thin AP cap is treated as present at impact.";
            lines.Add("FACEHARD69 PROCESS EXPLANATION");
            lines.Add(Invariant($"1. Armor preset {s.ARMOR} resolves to Q={s.Q}, QDAM={s.QDAM}, UB={s.UB}; effective plate thickness TEFF={RoundTo(s.TEFF, 100)} in."));
            if (s.WD == 0 && s.CMT == 0 && s.MTLBACK == 0) lines.Add("2. Backing setup: no wood/cement/metal backing contributes to effective thickness.");
            else lines.Add(Invariant($"2. Backing setup: wood={s.WD} in, cement={s.CMT} in, metal={s.MTLBACK} in; BKEFF={RoundTo(s.BKEFF, 1000)}."));
            lines.Add(Invariant($"3. Projectile preset Nation={s.NATION}, Type={s.PROJ} resolves to PLIM={s.PLIM}, PDAM={s.PDAM}, APCAP={s.APCAP}, SHATRES={s.SHATRES}, BRAAK={s.BRAAK}, LTCASE={s.LTCASE}."));
            lines.Add(Invariant($"4. Nose covering setup: {capText} Impact weight WT={s.WT} lb; body weight WB={s.WB} lb."));
            lines.Add(Invariant($"5. Obliquity/scaling setup: OB={s.OB} deg, D={s.D} in, SC={RoundTo(s.SC, 1000)}, MO={RoundTo(s.MO, 1000)}, MSHAT={RoundTo(s.MSHAT, 1000)}, VDFUSED={RoundTo(s.VDFUSED, 100000)}."));
            lines.Add(Invariant($"6. Ballistic limits before selection: N1/VLTRU={s.VLTRU}, N2/VLSHAT={s.VLSHAT}, N3/VLSHATMAX={s.VLSHATMAX}, N4/VLND={s.VLND}; H1/VHTRU={s.VHTRU}, H2/VHSHAT={s.VHSHAT}, H3/VHSHATMAX={s.VHSHATMAX}, H4/VHND={s.VHND}."));
            lines.Add(Invariant($"7. Selected limits for this shot: VHOL={s.VHOL} ({string.Join("/", SelectedLimitNames(s, "H"))}), VLMT={s.VLMT} ({string.Join("/", SelectedLimitNames(s, "N"))}). VS={s.VS} is {penFlagText}."));
            lines.Add(Invariant($"8. Exit/deflection setup: EX={RoundTo(s.EX, 100)} deg, OBDF={RoundTo(s.OBDF, 100)} deg, EXNBL={RoundTo(s.EXNBL, 100)} deg."));
            lines.Add(Invariant($"9. Damage logic: {shatterText} BRK={s.BRK}, NSBRK={s.NSBRK}, BDYDM={s.BDYDM}, NSSHAT={s.NSSHAT}."));
            lines.Add(Invariant($"10. Final penetration type PENTP={s.PENTP}: {PenetrationTypeSummary(s.PENTP)}"));
            lines.Add(Invariant($"11. Plug/remaining velocity: normal plug={RoundTo(s.NORMPLUGWT, 10)} lb at {s.VNPLUG} ft/sec; delta plug={RoundTo(s.DELTAPLUGWT, 10)} lb; projectile/base VR={s.VR} ft/sec; damaged/nose pieces VDPLUG={s.VDPLUG} ft/sec."));
            lines.Add(Invariant($"12. Effective BL: MINEV={s.MINEV}{(!string.IsNullOrEmpty(s.EFFVEL) ? $" ({s.EFFVEL})" : "")}{(!string.IsNullOrEmpty(s.EFFPRINT1) ? $" {s.EFFPRINT1}{s.EFFPRINT2}" : "")}."));
            s.PROCESS_REPORT = lines;
            return lines;
        }

        public static void ThresholdCalc(Facehard69LegacyState s)
        {
            // THRESHOLDCALC, FH69SBM2.BAS:1232-1277.
            // COMPUTE MINIMUM EFFECTIVE-FILLER OR NO-NOSE-DAMAGE VELOCITY THRESHOLD VALUE.
            // ALWAYS USE DAMAGE/NO-DAMAGE BOUNDARY FOR "EBL" & USE ACTUAL "VLEX/VHEX" VALUES FOR NOSE DAMAGE CALC.
            // "THVAL" IS PROJ CRITICAL DEFLECTION ANGLE CALCULATED PREVIOUSLY (NOSE OR BODY, AS SELECTED).
            // WE MUST FIND THE STRIKING VEL THAT GIVES AN "EX" FOR GIVEN "OB" SUCH THAT "THVAL" IS EXACTLY REACHED.
            s.THSPD = 0;
            if (s.THVAL <= s.OB)
            {
                var exth = s.OB - s.THVAL;
                if (s.SHAT == 1 || (exth > 15 && s.OB > 15))
                {
                    // COMPUTE "THSPD" USING INVERSE OF "EX" VEL RATIO FORMULA IF > "VLEXREV".
                    var temp0 = s.THVAL / 57.29578;
                    var temp1 = temp0 / (1 + s.OB45);
                    var temp2 = Math.Sin(temp1) * Math.Cos(temp1);
                    var temp3 = s.SNCSMAX / temp2;
                    var temp4 = Math.Max(0, 2 * temp3 - 1);
                    s.THSPD = s.VLEXREV * temp3 / QbSqr(temp4);
                    if (s.THSPD < s.VLEXREV) s.THSPD = s.VLEXREV;
                }
                else
                {
                    // LINEAR INCREASE IN "EX" WITH VEL FROM HBL TO NBL.
                    var thvusd = s.OB <= 15 ? s.THVAL : 15 - exth;
                    s.THSPD = s.VLEXREV - thvusd / s.MAXDIFF * (s.VLEXREV - s.VHEXREV);
                }
                var thchk = Math.Abs(s.THSPD - s.VLEXREV);
                // ROUND "THSPD" TO EXACTLY EQUAL "VLMT" OR "VHOL" IF SO ROUNDED ON DISPLAY.
                if (thchk < 0.5) s.THSPD = s.VLEXREV;
                thchk = Math.Abs(s.THSPD - s.VHEXREV);
                if (thchk < 0.5) s.THSPD = s.VHEXREV;
            }
        }

        static Facehard69LegacyState RunSetupAndLimits(Facehard69LegacyInput input, Facehard69LegacyRunOptions options)
        {
            var state = CreateState(input);
            if (options == null || options.resolveArmorInfo) ArmorInfo(state);
            if (input.NATN.HasValue && input.PRJTL.HasValue) AllProjData(state, (int)input.NATN.Value, (int)input.PRJTL.Value);
            NoseCoverSetup(state);
            state.CART = state.CARTWL;
            ArmorBackSetup(state);
            ThinSelect(state);
            ImpactSetup(state);
            CalcBl(state);
            BlPlusEx(state);
            return state;
        }

        public static Facehard69LegacyState RunLimitSlice(Facehard69LegacyInput input, Facehard69LegacyRunOptions options = null)
        {
            return RunSetupAndLimits(input, options);
        }

        public static Facehard69LegacyState RunSlice(Facehard69LegacyInput input, Facehard69LegacyRunOptions options = null)
        {
            var state = RunSetupAndLimits(input, options);
            DamageCalc(state);
            PlugWts(state);
            FinalResults(state);
            SetupSecPg(state);
            DamageSetup(state);
            NsBdyDamPrnt(state);
            PenPrint(state);
            ProjMotionPrnt(state);
            SplntrPrnt(state);
            RemVelPrnt(state);
            if (options == null || options.renderReports)
            {
                RenderReport(state);
                RenderSecondPage(state);
                RenderProcessReport(state);
            }
            return state;
        }

        static List<string> SelectedLimitNames(Facehard69LegacyState s, string kind)
        {
            var keys = kind == "N" ? new[] { ("N1", s.N1), ("N2", s.N2), ("N3", s.N3), ("N4", s.N4) } : new[] { ("H1", s.H1), ("H2", s.H2), ("H3", s.H3), ("H4", s.H4) };
            var result = new List<string>();
            foreach (var entry in keys)
            {
                if (!string.IsNullOrEmpty(entry.Item2) && entry.Item2.Contains("!")) result.Add(entry.Item1);
            }
            if (result.Count == 0) result.Add("unmarked");
            return result;
        }

        static string PenetrationTypeSummary(double pentp)
        {
            if (pentp == 0) return "plate is not holed.";
            if (pentp == 1) return "cartwheel/ductile shock result without normal holing.";
            if (pentp == 2) return "plate is holed, but projectile is rejected or ricochets.";
            if (pentp == 3) return "plate is holed; only nose pieces or limited damaged portions may pass.";
            if (pentp == 4) return "partial penetration by lower body pieces.";
            if (pentp == 5) return "partial penetration by nose/upper body pieces.";
            if (pentp == 6) return "complete penetration.";
            return "unclassified penetration state.";
        }

        static void AddIf(List<string> lines, params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrEmpty(value)) lines.Add(value);
            }
        }

        static void Set(Facehard69LegacyState s, params (string name, double value)[] values)
        {
            foreach (var item in values)
            {
                if (!StateFields.TryGetValue(item.name, out var field))
                    throw new InvalidOperationException(Invariant($"Unknown Facehard69 state field {item.name}."));
                field.SetValue(s, item.value);
            }
            SyncBraik(s);
        }

        static Dictionary<string, FieldInfo> BuildFieldCache()
        {
            var result = new Dictionary<string, FieldInfo>();
            foreach (var field in typeof(Facehard69LegacyState).GetFields(BindingFlags.Instance | BindingFlags.Public))
                result[field.Name] = field;
            return result;
        }

        static string F(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        static string Invariant(FormattableString formattable)
        {
            return formattable.ToString(CultureInfo.InvariantCulture);
        }
    }
}
