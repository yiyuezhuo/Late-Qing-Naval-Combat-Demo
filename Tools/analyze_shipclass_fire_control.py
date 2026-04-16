#!/usr/bin/env python
"""Analyze inferred relationships in ShipClass battery fire-control tables.

The script treats fire-control values as external data and tries to recover
their likely generating structure from ShipClasses.xml.
"""

from __future__ import annotations

import argparse
import math
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

try:
    import numpy as np
    import pandas as pd
    import statsmodels.formula.api as smf
    from sklearn.compose import ColumnTransformer
    from sklearn.ensemble import RandomForestRegressor
    from sklearn.inspection import permutation_importance
    from sklearn.metrics import mean_absolute_error, mean_squared_error, r2_score
    from sklearn.model_selection import train_test_split
    from sklearn.pipeline import Pipeline
    from sklearn.preprocessing import OneHotEncoder
except ImportError as exc:
    print(
        "Missing Python analysis dependency. Install with:\n"
        "  python -m pip install --user numpy pandas statsmodels scikit-learn",
        file=sys.stderr,
    )
    raise SystemExit(2) from exc


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SHIPCLASSES = REPO_ROOT / "Assets" / "StreamingAssets" / "Scenarios" / "ShipClasses.xml"
DEFAULT_OUTPUT_DIR = REPO_ROOT / "Tools" / "shipclass_fire_control_analysis"

CODE_ORDER = [
    "Z",
    "Y",
    "X",
    "W",
    "V",
    "U",
    "T",
    "S",
    "R",
    "Q",
    "P",
    "N",
    "M",
    "L",
    "K",
    "J",
    "H",
    "G",
    "F",
    "E",
    "D",
    "C",
    "B",
    "A",
]
CODE_RANK = {code: idx for idx, code in enumerate(CODE_ORDER)}
RANGE_BAND_RANK = {"Short": 0, "Medium": 1, "Long": 2, "Extreme": 3}
FIRE_CONTROL_CELLS = [
    ("Short", "Broad", "shortBroad"),
    ("Short", "Narrow", "shortNarrow"),
    ("Medium", "Broad", "mediumBroad"),
    ("Medium", "Narrow", "mediumNarrow"),
    ("Long", "Broad", "longBroad"),
    ("Long", "Narrow", "longNarrow"),
    ("Extreme", "Broad", "extremeBroad"),
    ("Extreme", "Narrow", "extremeNarrow"),
]


def parse_float(text: str | None) -> float:
    if text is None or text == "":
        return 0.0
    return float(text)


def read_xml_root(path: Path) -> ET.Element:
    data = path.read_bytes()
    # Some repo XML declares utf-16 while the bytes are UTF-8. ElementTree then
    # trusts the declaration and fails, so normalize only the declaration copy
    # used for analysis. This does not write back to the scenario file.
    if data.startswith(b"<?xml") and b'encoding="utf-16"' in data[:128]:
        data = data.replace(b'encoding="utf-16"', b'encoding="utf-8"', 1)
    return ET.fromstring(data)


def family_name(name: str) -> str:
    value = name.strip()
    value = re.sub(r"\s+", " ", value)
    value = re.sub(r"\b(Mk|Mark|Type|No\.)\s*[\w.-]+", "", value, flags=re.IGNORECASE)
    value = re.sub(r"\b\d{4}\b", "", value)
    return value.strip(" -_/") or name


def extract_rows(path: Path) -> pd.DataFrame:
    root = read_xml_root(path)
    rows: list[dict[str, object]] = []

    for ship_class in root.findall("ShipClass"):
        ship_name = (ship_class.findtext("name/english") or "").strip()
        ship_type = (ship_class.findtext("type") or "").strip()
        country = (ship_class.findtext("country") or "").strip()

        for battery in ship_class.findall("batteryRecords/BatteryRecord"):
            code = (battery.findtext("fireControlType/code") or "").strip()
            if code not in CODE_RANK:
                continue

            battery_name = (battery.findtext("name/english") or "").strip()
            range_yards = parse_float(battery.findtext("rangeYards"))
            shell_size = parse_float(battery.findtext("shellSizeInch"))
            rate_of_fire = parse_float(battery.findtext("maxRateOfFireShootPerMin"))
            damage_rating = parse_float(battery.findtext("damageRating"))
            fire_control_positions = parse_float(battery.findtext("fireControlPositions"))

            for record in battery.findall("fireControlTableRecords/FireControlTableRecord"):
                speed = parse_float(record.findtext("speedThresholdKnot"))
                for range_band, aspect, tag in FIRE_CONTROL_CELLS:
                    rows.append(
                        {
                            "ship_name": ship_name,
                            "ship_type": ship_type,
                            "country": country,
                            "battery_name": battery_name,
                            "battery_family": family_name(battery_name),
                            "code": code,
                            "code_rank": CODE_RANK[code],
                            "range_yards": range_yards,
                            "log_range_yards": math.log(max(range_yards, 1.0)),
                            "shell_size_inch": shell_size,
                            "log_shell_size_inch": math.log(max(shell_size, 0.1)),
                            "rate_of_fire": rate_of_fire,
                            "log_rate_of_fire": math.log(max(rate_of_fire, 0.01)),
                            "damage_rating": damage_rating,
                            "log_damage_rating": math.log(max(damage_rating, 0.1)),
                            "fire_control_positions": fire_control_positions,
                            "speed_threshold_knot": speed,
                            "range_band": range_band,
                            "range_band_rank": RANGE_BAND_RANK[range_band],
                            "aspect": aspect,
                            "aspect_narrow": 1 if aspect == "Narrow" else 0,
                            "fire_control_value": parse_float(record.findtext(tag)),
                        }
                    )

    df = pd.DataFrame(rows)
    if df.empty:
        raise ValueError(f"No fire-control rows were extracted from {path}")
    return df


def rmse(actual: pd.Series, predicted: pd.Series) -> float:
    return math.sqrt(mean_squared_error(actual, predicted))


def summarize_model(name: str, model, df: pd.DataFrame) -> dict[str, object]:
    predicted = model.predict(df)
    return {
        "model": name,
        "n": len(df),
        "parameters": int(model.df_model) + 1,
        "r2": float(model.rsquared),
        "adj_r2": float(model.rsquared_adj),
        "rmse": rmse(df["fire_control_value"], predicted),
        "mae": mean_absolute_error(df["fire_control_value"], predicted),
    }


def fit_ols_models(df: pd.DataFrame) -> tuple[pd.DataFrame, dict[str, object]]:
    formulas = {
        "numeric_rank": (
            "fire_control_value ~ code_rank + range_band_rank + aspect_narrow "
            "+ speed_threshold_knot + log_range_yards"
        ),
        "categorical_no_range": (
            "fire_control_value ~ C(code) + C(range_band) + C(aspect) "
            "+ C(speed_threshold_knot)"
        ),
        "categorical_with_log_range": (
            "fire_control_value ~ C(code) + C(range_band) + C(aspect) "
            "+ C(speed_threshold_knot) + log_range_yards"
        ),
        "compact_interactions": (
            "fire_control_value ~ C(code) + C(range_band) + C(aspect) "
            "+ C(speed_threshold_knot) + log_range_yards "
            "+ code_rank:range_band_rank + code_rank:aspect_narrow "
            "+ range_band_rank:aspect_narrow + speed_threshold_knot:range_band_rank "
            "+ log_range_yards:range_band_rank"
        ),
        "plus_weapon_terms": (
            "fire_control_value ~ C(code) + C(range_band) + C(aspect) "
            "+ C(speed_threshold_knot) + log_range_yards "
            "+ code_rank:range_band_rank + code_rank:aspect_narrow "
            "+ range_band_rank:aspect_narrow + speed_threshold_knot:range_band_rank "
            "+ log_range_yards:range_band_rank "
            "+ log_shell_size_inch + log_rate_of_fire + log_damage_rating"
        ),
    }

    models = {name: smf.ols(formula=formula, data=df).fit() for name, formula in formulas.items()}
    summaries = pd.DataFrame(summarize_model(name, model, df) for name, model in models.items())
    return summaries, models


def group_mean_model(df: pd.DataFrame) -> tuple[dict[str, float], pd.DataFrame]:
    group_cols = ["code", "speed_threshold_knot", "range_band", "aspect"]
    means = (
        df.groupby(group_cols, dropna=False)["fire_control_value"]
        .mean()
        .rename("group_mean_fire_control_value")
        .reset_index()
    )
    residuals = df.merge(means, on=group_cols, how="left")
    residuals["group_mean_residual"] = (
        residuals["fire_control_value"] - residuals["group_mean_fire_control_value"]
    )
    y = residuals["fire_control_value"]
    predicted = residuals["group_mean_fire_control_value"]
    stats = {
        "groups": float(len(means)),
        "r2": float(r2_score(y, predicted)),
        "rmse": rmse(y, predicted),
        "mae": float(mean_absolute_error(y, predicted)),
    }
    return stats, residuals


def residual_range_model(residuals: pd.DataFrame):
    return smf.ols("group_mean_residual ~ log_range_yards", data=residuals).fit()


def random_forest_importance(df: pd.DataFrame, seed: int) -> pd.DataFrame:
    feature_cols = [
        "code",
        "range_band",
        "aspect",
        "speed_threshold_knot",
        "log_range_yards",
        "log_shell_size_inch",
        "log_rate_of_fire",
        "log_damage_rating",
        "fire_control_positions",
        "ship_type",
        "country",
    ]
    categorical_cols = ["code", "range_band", "aspect", "ship_type", "country"]
    numeric_cols = [col for col in feature_cols if col not in categorical_cols]

    x_train, x_test, y_train, y_test = train_test_split(
        df[feature_cols],
        df["fire_control_value"],
        test_size=0.25,
        random_state=seed,
    )
    encoder_kwargs = {"handle_unknown": "ignore"}
    try:
        encoder = OneHotEncoder(sparse_output=False, **encoder_kwargs)
    except TypeError:
        encoder = OneHotEncoder(sparse=False, **encoder_kwargs)

    pipeline = Pipeline(
        [
            (
                "preprocess",
                ColumnTransformer(
                    [
                        ("categorical", encoder, categorical_cols),
                        ("numeric", "passthrough", numeric_cols),
                    ]
                ),
            ),
            (
                "model",
                RandomForestRegressor(
                    n_estimators=300,
                    random_state=seed,
                    min_samples_leaf=3,
                    n_jobs=1,
                ),
            ),
        ]
    )
    pipeline.fit(x_train, y_train)
    predicted = pipeline.predict(x_test)
    base = {
        "feature": "__model_score__",
        "importance_mean": r2_score(y_test, predicted),
        "importance_std": 0.0,
    }
    perm = permutation_importance(
        pipeline,
        x_test,
        y_test,
        n_repeats=12,
        random_state=seed,
        scoring="r2",
        n_jobs=1,
    )
    rows = [base]
    for col, mean, std in zip(feature_cols, perm.importances_mean, perm.importances_std):
        rows.append(
            {
                "feature": col,
                "importance_mean": float(mean),
                "importance_std": float(std),
            }
        )
    return pd.DataFrame(rows).sort_values("importance_mean", ascending=False)


def write_outputs(
    output_dir: Path,
    expanded: pd.DataFrame,
    model_summary: pd.DataFrame,
    residuals: pd.DataFrame,
    top_residuals: pd.DataFrame,
    rf_importance: pd.DataFrame | None,
) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    expanded.to_csv(output_dir / "expanded_fire_control_rows.csv", index=False, encoding="utf-8-sig")
    model_summary.to_csv(output_dir / "model_summary.csv", index=False, encoding="utf-8-sig")
    residuals.to_csv(output_dir / "group_mean_residuals.csv", index=False, encoding="utf-8-sig")
    top_residuals.to_csv(output_dir / "top_group_mean_residuals.csv", index=False, encoding="utf-8-sig")
    if rf_importance is not None:
        rf_importance.to_csv(output_dir / "random_forest_importance.csv", index=False, encoding="utf-8-sig")


def print_table(title: str, df: pd.DataFrame, columns: list[str] | None = None) -> None:
    print(f"\n{title}")
    print("-" * len(title))
    view = df if columns is None else df[columns]
    print(view.to_string(index=False))


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Infer ShipClass fire-control data relationships from ShipClasses.xml.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--shipclasses", type=Path, default=DEFAULT_SHIPCLASSES)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR)
    parser.add_argument("--no-write", action="store_true", help="Print analysis only; do not write CSV outputs.")
    parser.add_argument("--top", type=int, default=25, help="Number of largest residual rows to print/write.")
    parser.add_argument("--random-forest", action="store_true", help="Also run a random forest permutation-importance model.")
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()

    df = extract_rows(args.shipclasses)
    model_summary, ols_models = fit_ols_models(df)
    group_stats, residuals = group_mean_model(df)
    range_residual_model = residual_range_model(residuals)

    top_residuals = residuals.assign(
        abs_group_mean_residual=residuals["group_mean_residual"].abs()
    ).sort_values("abs_group_mean_residual", ascending=False).head(args.top)

    rf_importance = random_forest_importance(df, args.seed) if args.random_forest else None

    print(f"Source: {args.shipclasses}")
    print(
        f"Extracted {len(df)} fire-control cells from "
        f"{df[['ship_name', 'battery_name']].drop_duplicates().shape[0]} batteries."
    )

    by_code = (
        df.groupby("code")
        .agg(
            cells=("fire_control_value", "size"),
            batteries=("battery_name", "nunique"),
            avg_range_yards=("range_yards", "mean"),
            avg_fire_control_value=("fire_control_value", "mean"),
        )
        .reset_index()
        .sort_values("code", key=lambda s: s.map(CODE_RANK))
    )
    print_table(
        "By SK5 Code",
        by_code.round({"avg_range_yards": 0, "avg_fire_control_value": 3}),
    )

    print_table(
        "OLS Model Comparison",
        model_summary.round({"r2": 4, "adj_r2": 4, "rmse": 3, "mae": 3}),
    )

    print("\nCode+Speed+RangeBand+Aspect Group-Mean Model")
    print("---------------------------------------------")
    print(
        f"groups={int(group_stats['groups'])} "
        f"R2={group_stats['r2']:.4f} "
        f"RMSE={group_stats['rmse']:.3f} "
        f"MAE={group_stats['mae']:.3f}"
    )

    print("\nResidual Range Test After Group Means")
    print("-------------------------------------")
    print(
        f"R2={range_residual_model.rsquared:.4f} "
        f"coef(log_range_yards)={range_residual_model.params['log_range_yards']:.4f} "
        f"p={range_residual_model.pvalues['log_range_yards']:.4g}"
    )

    print_table(
        f"Top {args.top} Residuals vs Group Mean",
        top_residuals[
            [
                "group_mean_residual",
                "group_mean_fire_control_value",
                "fire_control_value",
                "code",
                "speed_threshold_knot",
                "range_band",
                "aspect",
                "range_yards",
                "shell_size_inch",
                "ship_name",
                "battery_name",
            ]
        ].round(
            {
                "group_mean_residual": 3,
                "group_mean_fire_control_value": 3,
                "range_yards": 0,
                "shell_size_inch": 2,
            }
        ),
    )

    if rf_importance is not None:
        print_table(
            "Random Forest Permutation Importance",
            rf_importance.round({"importance_mean": 4, "importance_std": 4}),
        )

    if not args.no_write:
        write_outputs(args.output_dir, df, model_summary, residuals, top_residuals, rf_importance)
        print(f"\nWrote CSV outputs to: {args.output_dir}")

    # Keep this available for ad-hoc interactive work through --no-write.
    _ = ols_models
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
