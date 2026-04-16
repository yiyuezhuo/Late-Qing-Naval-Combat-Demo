# ShipClass Penetration Table Analysis Notes

This note records the current working model and tool usage for analyzing
`ShipClasses.xml` penetration and rate-of-fire data. It is intended to mirror
`ShipClass_FireControl_Analysis_Notes.md` for future continuation.

## Scope

Primary data source:

```powershell
Assets/StreamingAssets/Scenarios/ShipClasses.xml
```

The analysis focuses on `BatteryRecord.penetrationTableRecords` and the
relationship between:

- `distanceYards`
- `rateOfFire`
- `rangeBand`
- `horizontalPenetrationInchs`
- `verticalPenetrationInchs`
- Battery-level fields such as `shellSizeInch`, `shellWeightPounds`,
  `rangeYards`, `damageRating`, and `maxRateOfFireShootPerMin`

In code, `PenetrationTableRecord.rateOfFire` is rounds per 2 minutes, not rounds
per minute. `BatteryRecord.maxRateOfFireShootPerMin` is a separate per-minute
field, but the runtime firing cadence uses the table row's `rateOfFire`.

## Tool Script

### `Tools/analyze_shipclass_penetration.py`

Run:

```powershell
python Tools/analyze_shipclass_penetration.py
```

Do not write CSV outputs:

```powershell
python Tools/analyze_shipclass_penetration.py --no-write
```

Default CSV output directory:

```powershell
Tools/shipclass_penetration_analysis/
```

Main outputs:

- `expanded_penetration_rows.csv`
- `monotonic_summary.csv`
- `rate_of_fire_model_summary.csv`
- `rate_of_fire_distance_caps.csv`
- `penetration_model_summary.csv`
- `range_band_by_distance.csv`

## Current Dataset Shape

Current extraction:

```text
penetration rows = 1042
battery ids      = 186
unique ship+battery labels = 183
```

The difference between battery ids and unique labels is caused by repeated
ship/battery labels with separate `BatteryRecord` entries.

Distance rows are mostly standard 2000-yard increments:

```text
2000, 4000, 6000, 8000, 10000, 12000, 15000, 18000, 21000
```

Monotonicity checks:

```text
rateOfFire nonincreasing             186 / 186
horizontal penetration nondecreasing 177 / 186
vertical penetration nonincreasing   185 / 186
```

This strongly supports an externally generated rounded-table interpretation, but
there are still outliers or hand-edited rows.

## Current Findings

### 1. Rate of Fire Looks Like a Distance-Capped Model

The tutorial script includes a useful textual clue: table ROF is sometimes
limited by inherent gun cycling speed and sometimes by range because corrected
fire must observe previous shot results.

The best current compact model is:

```text
observed_rateOfFire =
  round_0.1(min(first_row_latent_rateOfFire, distance_correction_cap))
```

Fit:

```text
n          = 1042
exact      = 0.5432
within_0.1 = 0.6488
MAE        = 0.1594
RMSE       = 0.3157
max_abs    = 2.2
```

Fitted distance caps, in rounds per 2 minutes:

```text
2000 yd   8.173
4000 yd   5.876
6000 yd   4.571
8000 yd   3.924
10000 yd  3.176
12000 yd  2.942
15000 yd  2.650
18000 yd  2.287
21000 yd  1.661
```

Using `2 * maxRateOfFireShootPerMin` instead of the first table row as the
latent inherent ROF is worse:

```text
exact      = 0.4770
within_0.1 = 0.5633
MAE        = 0.2029
RMSE       = 0.3733
```

Interpretation: `maxRateOfFireShootPerMin` is related to the table but not clean
enough to be treated as the actual generator. The first table row is currently a
better inferred latent value.

Large ROF residual clusters include:

```text
12cm/25 Krupp C/78
4.2''/20 9pdr M1879
4.75''/22 40-pdr 1.32-Ton BLR
```

These may have family-specific slow-loading or low-velocity behavior not
captured by the global distance cap.

### 2. Vertical Penetration Is Well Explained By Physical Fields Plus Distance

Best compact physical model currently tested:

```text
log(vertical_penetration) ~
  log(shell_size_inch)
+ log(shell_weight_pounds)
+ log(range_yards)
+ maxRateOfFireShootPerMin
+ distance_kyd
+ distance_kyd^2
+ log(shell_size_inch):distance_kyd
+ log(range_yards):distance_kyd
```

Predictions are exponentiated and rounded to 0.1 inch.

Fit:

```text
R2(log)    = 0.9478
AIC        = -1050.3
n          = 1042
exact      = 0.1219
within_0.1 = 0.2956
MAE        = 0.4161 in
RMSE       = 0.5975 in
max_abs    = 3.0 in
```

Coefficients:

```text
Intercept                        -7.1957
log_shell_size                   -0.5967
log_shell_weight                  0.7021
log_range_yards                   0.7334
max_rate_of_fire_shoot_per_min    0.0331
distance_kyd                      0.4023
distance_kyd_sq                   0.0068
log_shell_size:distance_kyd       0.0367
log_range_yards:distance_kyd     -0.0718
```

The negative effective slope with range emerges mainly through the interaction
with `log_range_yards`; the literal `distance_kyd` coefficient should not be
read in isolation.

Battery fixed-effect models are much stronger:

```text
log(vertical) ~ C(battery_id) + C(distance_yards)
R2(log)    = 0.9858
within_0.1 = 0.5393
MAE        = 0.2454 in
```

Interpretation: the table likely depends on hidden per-weapon ballistic
parameters not present in `ShipClasses.xml`, especially muzzle velocity and
projectile/armor quality. `shellWeightPounds`, `rangeYards`, and shell size are
good proxies but do not fully identify those hidden parameters.

### 3. Horizontal Penetration Is More Regular Than Vertical In Absolute Error

Best compact model by AIC among current tests:

```text
log(horizontal_penetration) ~
  log(shell_size_inch)
+ log(shell_weight_pounds)
+ log(range_yards)
+ maxRateOfFireShootPerMin
+ relative_distance
+ relative_distance^2
```

where:

```text
relative_distance = distanceYards / rangeYards
```

Fit:

```text
R2(log)    = 0.8827
AIC        = 427.6
n          = 1017
exact      = 0.3746
within_0.1 = 0.7198
MAE        = 0.1294 in
RMSE       = 0.2212 in
max_abs    = 1.3 in
```

Coefficients:

```text
Intercept                        -13.5807
log_shell_size                    -0.4045
log_shell_weight                   0.4925
log_range_yards                    1.0164
max_rate_of_fire_shoot_per_min    -0.0211
relative_distance                  3.8428
relative_distance_sq              -1.2766
```

Battery fixed effects improve the fit, but less dramatically than vertical
penetration:

```text
log(horizontal) ~ C(battery_id) + C(distance_yards)
R2(log)    = 0.9213
within_0.1 = 0.8073
MAE        = 0.0966 in
```

Interpretation: horizontal/deck penetration is mostly a smooth function of range
fraction and projectile weight, but special high-angle or low-velocity guns
still produce meaningful residuals.

### 4. Range Band Is Not A Fixed Distance Function

Current cross-tab:

```text
distance   Extreme  Long  Medium  Short
2000             0     0       5    181
4000             0     5      42    139
6000             4     5     134     43
8000             0    46     129      1
10000            0    87      57      0
12000            2    74      16      1
15000            3    54       0      1
18000            4     6       0      0
21000            3     0       0      0
```

Interpretation: `rangeBand` is probably assigned from each weapon's effective
range profile rather than a universal yard threshold. It should be modeled as a
classification problem using relative range and fire-control context if needed.

## Working Model

For generation or imputation, the best current interpretation is:

```text
rateOfFire:
  round_0.1(min(inherent_rate_per_2_min, distance_observation_cap))

vertical penetration:
  round_0.1(exp(f(shell weight, shell size, max range, hidden ballistic quality, distance)))

horizontal penetration:
  round_0.1(exp(g(shell weight, max range, relative distance, hidden ballistic quality)))

rangeBand:
  ordinal category from relative range/effective range, not fixed yard thresholds
```

The hidden ballistic-quality term is important. In practice, use a
`battery_id`/weapon-family latent term when backfilling or reviewing existing
records.

## Common Pitfalls

- Do not treat `maxRateOfFireShootPerMin` as the exact source of table ROF.
  `2 * maxRateOfFireShootPerMin` is only an approximation.
- Do not use a single global distance multiplier for vertical penetration. It
  underfits badly because different weapons have different curve shapes.
- Do not read `distanceYards` alone as determining `rangeBand`.
- Do not overinterpret exact-match rates. The data is rounded to 0.1 inch/ROF,
  but hidden weapon parameters and likely hand edits mean exact reconstruction
  from visible fields is not expected.
- Preserve XML encoding if later using these findings to edit scenario data.

## Suggested Next Steps

1. Add weapon-family normalization and fit family-level random/fixed effects.
2. Parse battery names for caliber length and gun type more robustly, then test
   whether caliber length explains residuals.
3. Fit `rangeBand` as an ordinal model using `relative_distance`, `rangeYards`,
   fire-control code, and weapon family.
4. Test a de Marre-style penetration formula explicitly if muzzle velocity or
   source ballistic tables can be recovered.
5. Investigate high-residual clusters:
   - `254mm/45 M1891`
   - `240mm/36 M1887`
   - `12cm/25 Krupp C/78`
   - `16''/18 RML Mk 1`
   - `20cm/45 No. 2 EOC`
