# ShipClass Fire-Control Analysis Notes

This note records the current working model and tool usage for analyzing
`ShipClasses.xml` fire-control data. It is intended for future agents that need
to continue the analysis without rediscovering the same context.

## Scope

Primary data source:

```powershell
Assets/StreamingAssets/Scenarios/ShipClasses.xml
```

The analysis focuses on `BatteryRecord.fireControlTableRecords` and the
relationship between:

- SK5 `code` / `FireControlSystem` components
- the left-top fire-control table cell: lowest `speedThresholdKnot` (`9`) +
  `Short/Broad`
- the rest of the fire-control table cells
- weapon/ship context such as shell size, displacement, and range

In code, SK5 `code` is a shorthand for these `FireControlSystem` components:

- `gunSight`
- `fireControlInstrument`
- `rangeFinder`
- `directorControl`
- `stabilization`
- `powerRemoteControl`

For the current dataset, only these four vary meaningfully:

- `gunSight`
- `fireControlInstrument`
- `rangeFinder`
- `directorControl`

`stabilization` is always `Manual`, and `powerRemoteControl` is always `None`
in the analyzed sample.

## Python Dependencies

The tools use Python analysis packages installed in the user environment:

```powershell
python -m pip install --user numpy pandas scipy statsmodels scikit-learn
```

If the dependency install times out, check whether packages were still installed:

```powershell
python -m pip show numpy pandas scipy statsmodels scikit-learn
```

## Tool Scripts

### `Tools/analyze_shipclass_fire_control.py`

General-purpose regression/exploration tool. It expands each fire-control table
cell into one row:

```text
ship + battery + code + speed + rangeBand + aspect -> fireControlValue
```

Run:

```powershell
python Tools/analyze_shipclass_fire_control.py
```

Optional random forest feature importance:

```powershell
python Tools/analyze_shipclass_fire_control.py --random-forest
```

Do not write CSV outputs:

```powershell
python Tools/analyze_shipclass_fire_control.py --no-write --top 20
```

Default CSV output directory:

```powershell
Tools/shipclass_fire_control_analysis/
```

Main outputs:

- `expanded_fire_control_rows.csv`
- `model_summary.csv`
- `group_mean_residuals.csv`
- `top_group_mean_residuals.csv`
- `random_forest_importance.csv` if `--random-forest` is used

### `Tools/fit_fire_control_latent_multipliers.py`

Fits the internal table-generation model:

```text
observed_cell =
  round_half_up(left_top * aspect_factor * range_band_factor * speed_factor)
```

Run:

```powershell
python Tools/fit_fire_control_latent_multipliers.py
```

Useful options:

```powershell
python Tools/fit_fire_control_latent_multipliers.py --top 50
python Tools/fit_fire_control_latent_multipliers.py --metric sse
python Tools/fit_fire_control_latent_multipliers.py --no-write
```

Main outputs:

- `latent_multiplier_cell_results.csv`
- `latent_multiplier_factors.csv`
- `latent_multiplier_summary_by_aspect.csv`
- `latent_multiplier_summary_by_range_band.csv`
- `latent_multiplier_summary_by_speed.csv`
- `latent_multiplier_summary_by_code.csv`
- `latent_multiplier_top_residuals.csv`

### `Tools/detect_fire_control_code_mismatch.py`

Detects likely `code`/table mismatches using a two-stage model:

1. compress each battery table into `latent_left`
2. fit a latent true-code EM mixture where observed `code` may be noisy

Run:

```powershell
python Tools/detect_fire_control_code_mismatch.py
```

Sensitivity test with fixed label-error rate:

```powershell
python Tools/detect_fire_control_code_mismatch.py --fixed-epsilon 0.10 --no-write --top 30
```

Main outputs:

- `code_mismatch_latent_by_battery.csv`
- `code_mismatch_model_parameters.csv`
- `code_mismatch_confusion_matrix.csv`
- `code_mismatch_candidates.csv`

## Current Findings

### 1. Table Internals Are Highly Structured

The fire-control table cells are not independent. A compact multiplicative
latent model explains them very well:

```text
observed_cell =
  round_half_up(left_top * aspect_factor * range_band_factor * speed_factor)
```

Current fitted factors:

```text
Aspect:
Broad  = 1.0000
Narrow = 0.6005

Range band:
Short   = 1.0000
Medium  = 0.6010
Long    = 0.4165
Extreme = 0.3567

Speed:
9kt  = 1.0000
18kt = 0.6710
27kt = 0.5265
36kt = 0.4393
45kt = 0.3758
```

Global fit quality using observed left-top values:

```text
cells=7440
batteries=186
exact=0.9155
MAE=0.0845
RMSE=0.2908
max_abs_error=1
within_one=1.0000
```

This supports the interpretation that most table cells are rounded values from
an underlying continuous table model.

### 2. Use `latent_left`, Not Raw Left-Top, When Possible

For each battery, `latent_left` can be inferred from the whole fire-control
table. Regressing `latent_left` on `Code` is cleaner than regressing the raw
observed left-top value:

```text
observed_left ~ C(code):
R2    = 0.7094
AdjR2 = 0.6963
RMSE  = 1.2385

latent_left ~ C(code):
R2    = 0.7668
AdjR2 = 0.7563
RMSE  = 1.0719
```

This suggests the table internals contain useful signal and the observed code or
left-top cell can be noisy.

### 3. Strong Code/Table Mismatch Candidates

The EM model estimates a default code/table mismatch rate around:

```text
epsilon ~= 0.0416
```

Strong EM reassignments:

```text
Ting Yuen / 15cm RKL/35 C/80
observed Y -> likely Z

Itsukushima / 32cm/38 M1887
observed Z -> likely Y

Matsushima / 32cm/38 M1887
observed Z -> likely Y

Takao / 15cm RKL/25 C/75
observed Z -> likely Y
```

When only these four strong reassignments are excluded:

```text
latent_left ~ C(code):
R2    0.7668 -> 0.8125
AdjR2 0.7563 -> 0.8039
RMSE  1.0719 -> 0.9599
```

Do not automatically discard all review candidates from
`code_mismatch_candidates.csv`; the default candidate list is intentionally broad
and includes likely family/model residuals, not just true mismatches.

### 4. Best Current Cross-Sectional Model

After excluding the four strong EM-reassigned samples, the current recommended
model uses fire-control components plus raw shell size and raw displacement:

```text
latent_left ~
  C(gun_sight, reference='Basic')
+ C(fire_control_instrument, reference='None')
+ C(range_finder, reference='None')
+ C(director_control, reference='None')
+ C(stabilization, reference='Manual')
+ C(power_remote_control, reference='None')
+ shell_size_inch
+ displacement_1000_tons
```

Fit:

```text
n      = 182
R2     = 0.8615
AdjR2  = 0.8567
RMSE   = 0.8252
MAE    = 0.6236
AIC    = 460.55
BIC    = 482.98
```

Coefficients:

```text
Intercept                         4.7316
Telescope sight                  +1.8941
Basic fire-control instrument    +2.1650
Optical rangefinder              +1.5337
Follow-the-pointer director      +1.9988
shell_size_inch                  +0.2128 per inch
displacement_1000_tons           -0.0893 per 1000 tons
```

Use enum-first baselines, not alphabetic baselines. The meaningful references
are:

```text
gunSight: Basic
fireControlInstrument: None
rangeFinder: None
directorControl: None
stabilization: Manual
powerRemoteControl: None
```

Raw `shell_size_inch` and raw `displacement_1000_tons` fit better than log forms
in this dataset.

### 5. Range Has Little Stable Independent Explanatory Power

Adding `range_yards`/`log_range_yards` generally does not improve the preferred
models after controlling for code/components and shell size. It often worsens
BIC and has non-significant p-values.

Interpretation: observed range correlation is likely caused by correlation with
weapon era/size/code rather than range being a direct generator of the
fire-control value.

### 6. Same-Ship Variant Comparisons

Some ship classes have multiple period variants, usually marked with a year in
parentheses. Strip the trailing parenthesized suffix to group variants:

```text
Yoshino
Yoshino (1901)
```

Same ship + same battery comparisons show:

- code/component upgrades usually correspond to latent increases
- the direction of component coefficients is confirmed
- the upgrade values are not strictly additive

Important examples:

```text
Akitsushima 15cm/45:
Y -> X
latent 8.920 -> 10.731
delta +1.811
change: rangeFinder None -> Optical

Fuji 30cm:
T -> S
latent 10.180 -> 11.966
delta +1.785
change: rangeFinder None -> Optical

Peresviet 254mm:
W -> Q
latent 10.731 -> 13.815
delta +3.084
change: Basic -> Telescope and None -> Optical rangefinder
```

Suspicious variant comparisons:

```text
Yoshino 15cm/45:
code X unchanged
latent 8.920 -> 10.731
possible table changed but code not synchronized

Yoshino 4.7''/40:
Z -> Y
latent unchanged at 7.146
possible code changed but table not synchronized

Akitsushima 4.7''/40:
Z -> Y
latent unchanged at 7.146
possible code changed but table not synchronized
```

Multiple simultaneous component upgrades are not well modeled by simple additive
coefficients. For example:

```text
Z -> S on Itsukushima/Matsushima 32cm:
observed latent delta ~= +3.045
linear component model predicts ~= +5.593
```

This suggests diminishing returns, ceiling effects, or SK5 table-level
nonlinearity. Treat component coefficients as average marginal effects, not
fixed upgrade values.

## Common Pitfalls

- Do not assume `rangeYards` directly generates fire-control values. It has weak
  independent explanatory value after controls.
- Do not use alphabetic category ordering for fire-control component baselines.
  Use enum-first references.
- Do not interpret all residuals as code/table mismatches. Some residuals are
  likely weapon-family or historical-data effects.
- Do not automatically delete all `code_mismatch_candidates.csv` rows. Only the
  EM-reassigned rows are high-confidence automatic exclusions.
- Be careful with XML encoding. Some repo XML declares `utf-16` while bytes are
  effectively UTF-8. The tools normalize the declaration for analysis only and
  do not write back to scenario XML.

## Suggested Next Steps

1. Extend `detect_fire_control_code_mismatch.py` to optionally refit the
   cross-sectional component model after excluding high-confidence mismatches.
2. Add explicit same-ship variant comparison output as a CSV.
3. Try nonlinear component models:
   - component interaction terms
   - code-combination categorical effects
   - saturation/diminishing-return terms
4. Add weapon-family terms after normalizing battery names.
5. Manually review:
   - `Yoshino 15cm/45 EOC/VSM`
   - `Yoshino 4.7''/40 QF Type 41`
   - `Akitsushima 4.7''/40 QF Type 41`
   - the four strong EM reassignments listed above

