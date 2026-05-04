
The calculator has two connected models: an exterior ballistic model that finds the shell's flight path, impact velocity, and angle of fall, and a terminal ballistic model that turns the impact state into vertical and horizontal armor penetration.

# Exterior ballistic

Exterior ballistic follows the point-mass trajectory formulation described in Robert L. McCoy's [Modern Exterior Ballistics](https://www.mori.bz.it/Balistica/Mc%20Coy%20Modern%20Exterior%20Ballistic.pdf), Chapter 8, especially the standard-atmosphere discussion on pages `165`-`168` and the MCTRAJ Q-BASIC reference program on pages `183`-`186`.

Some NAAB-like adaptation (like the drag coef) is added.

## McCoy/MCTRAJ point-mass basis

MCTRAJ treats the shell as a point mass with three velocity components. It tracks downrange distance, height, lateral deflection, time, and the velocity components `vx`, `vy`, and `vz`. It can also include a range wind and a crosswind. In the original program, gun elevation is entered in minutes of angle, and the initial state is:

```
theta = elevationMinutes / 3437.74677
vx = muzzleVelocityFeetPerSecond * cos(theta)
vy = muzzleVelocityFeetPerSecond * sin(theta)
vz = 0
range = 0
height = -sightHeightInches / 12
deflection = 0
time = 0
```

MCTRAJ provides two atmosphere coefficient sets: Army Standard Metro and ICAO Standard Atmosphere. The calculator uses the Army Standard Metro branch, whose visible MCTRAJ constants are:

```
RH1 = -0.00003158
RH2 = 0
TK1 = -0.000006015
TK2 = 0
PIR = 0.0002048757
VV1 = 49.19
```

For a height `h` in feet, MCTRAJ computes local air temperature, sound speed, and density scaling from those coefficients:

```
temperatureF = (inputTemperatureF + 459.67) * exp((TK1 + TK2 * h) * h) - 459.67
soundFeetPerSecond = VV1 * sqrt(temperatureF + 459.67)
densityScale = exp((RH1 + RH2 * h) * h)
```

The "drag function" in MCTRAJ is an input table of Mach number versus drag coefficient `CD`, not a hard-coded `G1` or `G7` curve. The program linearly interpolates that table:

```
CD = CD_i + (CD_{i+1} - CD_i) / (Mach_{i+1} - Mach_i) * (Mach - Mach_i)
```

For each range step, the program computes relative air speed against wind, converts it to Mach number, reads `CD`, and scales drag by ballistic coefficient `BC`:

```
relativeSpeed = sqrt((vx - rangeWind)^2 + vy^2 + (vz - crosswind)^2)
mach = relativeSpeed / soundFeetPerSecond
dragFactor = PIR * densityRatioInput * CD * relativeSpeed * densityScale / BC
```

The trajectory is integrated against downrange distance, not directly against time. MCTRAJ uses a second-order Heun predictor-corrector method: it first predicts the next velocity with an Euler step, then repeatedly corrects the result until the relative velocity change is below `0.00001`.

This basis answers one question: for a given muzzle velocity, elevation, Mach-CD drag table, ballistic coefficient, atmosphere, and wind, what trajectory does the shell follow? MCTRAJ can also adjust elevation until the trajectory passes through a requested match range and match height.

## NAAB-like adaptations in this calculator

The calculator keeps the McCoy/MCTRAJ point-mass structure, but adapts it for NAAB-style projectile data and this project's penetration-table fitting.

- **2D reduction**: the calculator uses MCTRAJ's vertical-plane special case, tracking `x`, `y`, `vx`, `vy`, and time while omitting crosswind, lateral deflection, range wind, and sight-line height.
- **Atmosphere and gravity**: the calculator uses the Army Standard Metro-style constants from MCTRAJ rather than exposing the ICAO branch; unlike MCTRAJ's fixed `G = 32.174`, it computes gravity from Earth radius and Earth gravitational parameter.
- **Drag function**: the selected drag function (`G1`, `G2`, `G5`, `G6`, `G7`, `G8`, `G9`, `GS`, or `GL`) supplies `cdRef` from embedded drag tables, which function as named versions of MCTRAJ's user-entered Mach-CD table.
- **Numerical integration**: MCTRAJ's default `DINT = 1` yard corresponds to `D3 = 3` feet, matching the calculator's default `dxFeet = 3`; the calculator uses one predictor-corrector pass per step rather than MCTRAJ's iterative Heun correction loop.

### **Drag Coefficient adjustment**:

The effective ballistic coefficient is normally `BC`. If **Drag Coefficient** is non-zero, the calculator changes `BC` with range while the shell is supersonic:

`BCeff = max(0.01, BC + dragCoefficientAdjust * rangeTerm / 600000)`.

Before the shell first reaches `Mach <= 1`, `rangeTerm` is the current range. If a later state has already recorded a `Mach <= 1` crossing and then returns to `Mach > 1`, the solver uses:

`rangeTerm = abs(firstMachLeOneRangeFeet + firstMachGtOneAfterThatRangeFeet - currentRangeFeet)`.

With this adaptation, the drag slope becomes:

`dragSlope = 0.0002048757 * densityRatio * cdRef * speed / BCeff`.

For a ground-impact run, the solver continues until the shell crosses `y = 0`, `time > 300`, the horizontal velocity collapses, or the simulation range limit is reached. The impact point is linearly interpolated between the last positive-height state and the first non-positive-height state.

The exterior result passed to the terminal model is:

```
rangeYards = impactXFeet / 3
timeOfFlightSeconds = impactTime
impactVelocityFeetPerSecond = sqrt(impactVx^2 + impactVy^2)
angleOfFallDeg = max(atan2(-impactVy, impactVx) * 180 / pi, 0)
```

# Terminal ballistic

The terminal-ballistic model treats armor penetration as a ballistic-limit problem. For a candidate plate thickness, it estimates the striking velocity needed for complete penetration; then it inverts that relation to report the maximum plate thickness that the shell can defeat at the computed impact velocity.

## Okun homogeneous-armor basis

The reference model is Nathan Okun's homogeneous-armor work, mainly [M79APCLC](https://www.navweaps.com/index_nathan/M79apdoc.php).

The main geometry term is relative thickness. `D` is projectile diameter, `T` is plate thickness, and `T / D` says whether the plate is thin or thick relative to the shell. This matters because Okun's model changes coefficient sets across different `T / D` regions. Projectile weight enters through `W / D^3`, where `W` is complete projectile weight. This term keeps the formula sensitive to whether a projectile is heavy or light for its caliber.

For a candidate plate thickness `T`, the model selects coefficient tables by `T / D`. These tables provide `A`, `B`, and the parameters for a local shape correction `J(T / D)`. In compact form, the normal-impact Navy Ballistic Limit can be written as:

`NBL = A * J(T / D) * diameterScale(D) * (QA * T / D)^B / sqrt(W / D^3)`.

Here `QA` is the armor quality factor. The `J(T / D)` term is Okun's local Green's-function correction for the thickness curve:

`J(T / D) = 1 + JA * max(sin((JB * T / D - JC) * pi / 180), 0)`.

`JA`, `JB`, and `JC` are selected from the same `T / D` coefficient table as `A` and `B`. Most thickness regions use `J = 1`; where it is active, it bends the local curve above or below the plain power-law result. The diameter scale follows Okun's caliber-scaling term:

`diameterScale = sqrt(max(1e-9, 1 - 0.04 * ln(D / 3)))`.

Percent elongation `PE` is a separate armor-material input. In the Okun reference model it only changes the result when `PE < 25` and `D > 8`. Written as a reusable multiplier:

`elongationFactor = 1 - (1 - sqrt(PE / 25)) * (D - 8) / 8`.

At full ductility or for smaller projectiles, this multiplier is `1`.

Obliquity then adjusts the ballistic limit. Obliquity is measured from the armor normal: `0` degrees is a square hit, and higher values are more glancing. Okun's M79 model interpolates an obliquity reference value `M'`; below `45` degrees this depends only on obliquity, while at `45` degrees and above it also depends on `T / D`. The final obliquity multiplier is:

`obliquityMultiplier = M' / cos(obliquity)`.

The reference model therefore answers one question: for this shell, armor quality, plate thickness, and obliquity, what striking velocity is needed for complete penetration? Penetration thickness is the inverse question: given an impact velocity, find the greatest `T` whose ballistic limit can still be reached.

## NAAB-like adaptations in this calculator

The calculator try to emulate Steven Lorenz's [Naval Armor and Ballistics program (NAAB)](http://www.panzer-war.com/Naab/NAaB.html)'s homogeneous armor calculation, so that I can compare game data with NAAB's projectile data. In current stage, the result is close to NAAB's result but not identical.

### **plateQuality** correction:

Conceptually, the calculator evaluates the Okun NBL with neutral `QA = 1`, then replaces Okun's direct QA effect with a `plateQualityModifier`.

```
plateQuality = clamp(armorQuality * HardnessProfile(235) / HardnessProfile(BHN), 0.5, 1.1)
plateQualityModifier = (max(plateQuality, 0.01) * td)^B / td^B
baseNBL = NBL * plateQualityModifier
```

Here, NBL means the Okun NBL evaluated with QA = 1, using the calculator's clamped inputs.

### **windscreenAddend** correction:

A windscreen uses:

`windscreenPercent = 100 * windscreenWeightPounds / totalWeightPounds`.

The addend is skipped when `windscreenPercent <= 0.1`, or when the thin-plate suppression rule applies. Otherwise, the calculator chooses `windscreenMultiplier` from the projectile's normal or high-obliquity windscreen multiplier fields, depending on the current obliquity. It then reads `windscreenTableValue` from the embedded windscreen table by `td` and obliquity. The windscreen contribution is:

`windscreenAddend = (windscreenPercent / 5.1) * windscreenMultiplier * windscreenTableValue`.

### **capAddend** correction:

Hard and medium AP caps use a separate cap addend. Other cap types are kept as projectile metadata, but this terminal formula applies cap addends only to hard and medium caps.

`apCapPercent = 100 * apCapWeightPounds / totalWeightPounds`.

| Cap type   |          `capRatio` | Low-obliquity cutoff |
| ---------- | ------------------: | -------------------: |
| Hard cap   | `apCapPercent / 20` |         `50` degrees |
| Medium cap | `apCapPercent / 10` |         `65` degrees |

The `td` threshold is rounded to `0.001`:

| Obliquity condition |                         Threshold |
| ------------------- | --------------------------------: |
| `obliquity > 65`    |                            `0.42` |
| `obliquity > 55`    | `0.44 - 0.002 * (obliquity - 55)` |
| otherwise           |  `0.66 - 0.18 * (obliquity / 45)` |

If `td = clamp(T / D, 0.001, 5.99999)` is above that threshold and obliquity is below the cap type's cutoff, the cap addend is:

`capAddend = capTableValue * (1 + 0.6 * (capRatio - 1))`.

If `td` is not above the threshold and `40 < obliquity < 75`, the shared mid-obliquity cap addend is:

`capAddend = sharedCapTableValue * capRatio`.

### **trueNBL** and penetration **T**:

After these additions, the calculator's complete ballistic limit is:

`trueNBL = baseNBL * (1 + windscreenAddend + capAddend)`.

`trueNBL` is actually a function of plate thickness `T` and `obliquity`: `trueNBL(T, obliquity)`. 
So, given the equation `impactVelocity = trueNBL(T, obliquity)`, we can inversely solve for `T` as `T = Penetration(impactVelocity, obliquity)`. 
In the calculator, the solution is found by approximate search rather than analytically.

### **postScale** correction:

The `T` is post-scaled by shell quality and extreme obliquity:

`postScale = extremeScale * clamp(effectiveShellQuality, 0.2, 1.2)`.

For `80 <= obliquity < 90`, `extremeScale = (cos(obliquity) / cos(80))^1.1`; otherwise it is `1`.

The calculator reports both vertical and horizontal penetration from the same impact state:

`sideObliquity = armorInclinedDeg + angleOfFallDeg`.

`deckObliquity = armorInclinedDeg + max(90 - angleOfFallDeg, 0)`.

`verticalPenetration = Penetration(impactVelocity, sideObliquity) * postScale`.

`horizontalPenetration = Penetration(impactVelocity, deckObliquity) * postScale`.
