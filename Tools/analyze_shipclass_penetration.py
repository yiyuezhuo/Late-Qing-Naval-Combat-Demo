#!/usr/bin/env python
"""Analyze inferred models behind ShipClass penetration/ROF tables.

This is a companion to the fire-control analysis tools. It treats
BatteryRecord.penetrationTableRecords as rounded external data and tests compact
generating hypotheses for:

- rateOfFire
- verticalPenetrationInchs
- horizontalPenetrationInchs
- rangeBand assignment
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
    from scipy.optimize import minimize
    import statsmodels.formula.api as smf
except ImportError as exc:
    print(
        "Missing Python analysis dependency. Install with:\n"
        "  python -m pip install --user numpy pandas scipy statsmodels",
        file=sys.stderr,
    )
    raise SystemExit(2) from exc


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SHIPCLASSES = REPO_ROOT / "Assets" / "StreamingAssets" / "Scenarios" / "ShipClasses.xml"
DEFAULT_OUTPUT_DIR = REPO_ROOT / "Tools" / "shipclass_penetration_analysis"


def parse_float(text: str | None) -> float:
    if text is None or text == "":
        return 0.0
    return float(text)


def read_xml_root(path: Path) -> ET.Element:
    data = path.read_bytes()
    # Some repo XML declares utf-16 while the bytes are UTF-8. Normalize only
    # the analysis copy; never write this back to scenario XML.
    if data.startswith(b"<?xml") and b'encoding="utf-16"' in data[:128]:
        data = data.replace(b'encoding="utf-16"', b'encoding="utf-8"', 1)
    return ET.fromstring(data)


def short_name(element: ET.Element) -> str:
    return (element.findtext("name/english") or "").strip()


def parse_caliber_length(name: str) -> float:
    match = re.search(r"/(\d+(?:\.\d+)?)", name)
    return float(match.group(1)) if match else math.nan


def round_tenth(values):
    values = np.asarray(values, dtype=float)
    return np.floor(values * 10.0 + 0.5) / 10.0


def score(y, predicted) -> dict[str, float]:
    y = np.asarray(y, dtype=float)
    predicted = np.asarray(predicted, dtype=float)
    residual = predicted - y
    return {
        "n": float(len(y)),
        "exact": float(np.mean(np.isclose(residual, 0.0))),
        "within_0.1": float(np.mean(np.abs(residual) <= 0.100001)),
        "mae": float(np.mean(np.abs(residual))),
        "rmse": float(math.sqrt(np.mean(residual * residual))),
        "max_abs": float(np.max(np.abs(residual))),
    }


def extract_rows(path: Path) -> pd.DataFrame:
    root = read_xml_root(path)
    rows: list[dict[str, object]] = []
    battery_id = 0

    for ship_class in root.findall("ShipClass"):
        ship_name = short_name(ship_class)
        ship_type = (ship_class.findtext("type") or "").strip()
        country = (ship_class.findtext("country") or "").strip()

        for battery in ship_class.findall("batteryRecords/BatteryRecord"):
            records = battery.findall("penetrationTableRecords/PenetrationTableRecord")
            if not records:
                continue

            battery_name = short_name(battery)
            range_yards = parse_float(battery.findtext("rangeYards"))
            shell_size = parse_float(battery.findtext("shellSizeInch"))
            shell_weight = parse_float(battery.findtext("shellWeightPounds"))
            max_rof = parse_float(battery.findtext("maxRateOfFireShootPerMin"))
            damage_rating = parse_float(battery.findtext("damageRating"))
            caliber_length = parse_caliber_length(battery_name)

            for record in records:
                distance = parse_float(record.findtext("distanceYards"))
                rows.append(
                    {
                        "battery_id": battery_id,
                        "ship_name": ship_name,
                        "ship_type": ship_type,
                        "country": country,
                        "battery_name": battery_name,
                        "shell_size_inch": shell_size,
                        "shell_weight_pounds": shell_weight,
                        "max_rate_of_fire_shoot_per_min": max_rof,
                        "max_rate_of_fire_per_2_min": max_rof * 2.0,
                        "range_yards": range_yards,
                        "damage_rating": damage_rating,
                        "caliber_length": caliber_length,
                        "distance_yards": distance,
                        "distance_kyd": distance / 1000.0,
                        "rate_of_fire": parse_float(record.findtext("rateOfFire")),
                        "range_band": (record.findtext("rangeBand") or "").strip(),
                        "horizontal_penetration_inch": parse_float(
                            record.findtext("horizontalPenetrationInchs")
                        ),
                        "vertical_penetration_inch": parse_float(
                            record.findtext("verticalPenetrationInchs")
                        ),
                    }
                )

            battery_id += 1

    df = pd.DataFrame(rows)
    if df.empty:
        raise ValueError(f"No penetration rows were extracted from {path}")
    return df


def monotonic_summary(df: pd.DataFrame) -> pd.DataFrame:
    rows = []
    for _, group in df.sort_values("distance_yards").groupby("battery_id"):
        rows.append(
            {
                "battery_id": group["battery_id"].iloc[0],
                "ship_name": group["ship_name"].iloc[0],
                "battery_name": group["battery_name"].iloc[0],
                "rows": len(group),
                "rate_of_fire_nonincreasing": bool(
                    np.all(np.diff(group["rate_of_fire"].to_numpy()) <= 0)
                ),
                "horizontal_penetration_nondecreasing": bool(
                    np.all(np.diff(group["horizontal_penetration_inch"].to_numpy()) >= 0)
                ),
                "vertical_penetration_nonincreasing": bool(
                    np.all(np.diff(group["vertical_penetration_inch"].to_numpy()) <= 0)
                ),
            }
        )
    return pd.DataFrame(rows)


def fit_rate_of_fire_models(df: pd.DataFrame) -> tuple[pd.DataFrame, pd.DataFrame]:
    distances = sorted(df["distance_yards"].unique())
    distance_index = {distance: idx for idx, distance in enumerate(distances)}
    di = df["distance_yards"].map(distance_index).to_numpy(dtype=int)
    y = df["rate_of_fire"].to_numpy(dtype=float)

    def fit_cap_model(latent_values: np.ndarray, label: str) -> tuple[dict[str, float], pd.DataFrame]:
        init = [
            max(np.percentile(df.loc[df["distance_yards"] == distance, "rate_of_fire"], 90), 0.05)
            for distance in distances
        ]
        x0 = np.log(init)

        def predicted_from_x(x):
            caps = np.exp(x)
            return round_tenth(np.minimum(latent_values, caps[di]))

        def objective(x):
            caps = np.exp(x)
            monotonic_penalty = sum(
                max(0.0, caps[idx + 1] - caps[idx]) for idx in range(len(caps) - 1)
            )
            residual = predicted_from_x(x) - y
            return (
                float(np.mean(np.abs(residual)))
                + 0.05 * float(np.mean(residual * residual))
                + 10.0 * monotonic_penalty
            )

        result = minimize(
            objective,
            x0,
            method="Nelder-Mead",
            options={"maxiter": 20000, "xatol": 1e-9, "fatol": 1e-9},
        )
        caps = np.exp(result.x)
        predicted = predicted_from_x(result.x)
        stats = {"model": label, **score(y, predicted)}
        cap_df = pd.DataFrame(
            {
                "model": label,
                "distance_yards": distances,
                "cap_rate_of_fire_per_2_min": caps,
            }
        )
        return stats, cap_df

    first_row_latent = (
        df.sort_values("distance_yards")
        .groupby("battery_id")["rate_of_fire"]
        .first()
    )
    latent_from_first = df["battery_id"].map(first_row_latent).to_numpy(dtype=float)
    latent_from_max_rate = df["max_rate_of_fire_per_2_min"].to_numpy(dtype=float)

    stats_a, caps_a = fit_cap_model(latent_from_first, "first_row_latent_min_distance_cap")
    stats_b, caps_b = fit_cap_model(latent_from_max_rate, "max_rate_times_2_min_distance_cap")
    return pd.DataFrame([stats_a, stats_b]), pd.concat([caps_a, caps_b], ignore_index=True)


def fit_penetration_models(df: pd.DataFrame) -> tuple[pd.DataFrame, dict[str, object]]:
    formulas = {
        "physical_quadratic": (
            "log_value ~ log_shell_size + log_shell_weight + log_range_yards "
            "+ max_rate_of_fire_shoot_per_min + distance_kyd + distance_kyd_sq"
        ),
        "physical_relative_range": (
            "log_value ~ log_shell_size + log_shell_weight + log_range_yards "
            "+ max_rate_of_fire_shoot_per_min + relative_distance + relative_distance_sq"
        ),
        "physical_distance_interactions": (
            "log_value ~ log_shell_size + log_shell_weight + log_range_yards "
            "+ max_rate_of_fire_shoot_per_min + distance_kyd + distance_kyd_sq "
            "+ log_shell_size:distance_kyd + log_range_yards:distance_kyd"
        ),
        "damage_quadratic": (
            "log_value ~ np.log(damage_rating) + log_range_yards "
            "+ max_rate_of_fire_shoot_per_min + distance_kyd + distance_kyd_sq"
        ),
        "battery_fixed_effect_quadratic": "log_value ~ C(battery_id) + distance_kyd + distance_kyd_sq",
        "battery_fixed_effect_distance": "log_value ~ C(battery_id) + C(distance_yards)",
    }

    summary_rows = []
    fitted_models: dict[str, object] = {}
    for target, source_col in [
        ("vertical", "vertical_penetration_inch"),
        ("horizontal", "horizontal_penetration_inch"),
    ]:
        sub = df.loc[df[source_col] > 0].copy()
        sub["value"] = sub[source_col]
        sub["log_value"] = np.log(sub["value"])
        sub["log_shell_size"] = np.log(sub["shell_size_inch"].clip(lower=0.1))
        sub["log_shell_weight"] = np.log(sub["shell_weight_pounds"].clip(lower=0.1))
        sub["log_range_yards"] = np.log(sub["range_yards"].clip(lower=1.0))
        sub["distance_kyd_sq"] = sub["distance_kyd"] ** 2
        sub["relative_distance"] = sub["distance_yards"] / sub["range_yards"].clip(lower=1.0)
        sub["relative_distance_sq"] = sub["relative_distance"] ** 2

        for model_name, formula in formulas.items():
            model = smf.ols(formula, data=sub).fit()
            predicted = round_tenth(np.exp(model.predict(sub)))
            model_score = score(sub["value"], predicted)
            summary_rows.append(
                {
                    "target": target,
                    "model": model_name,
                    "r2_log": float(model.rsquared),
                    "aic": float(model.aic),
                    **model_score,
                }
            )
            fitted_models[f"{target}:{model_name}"] = model

    return pd.DataFrame(summary_rows), fitted_models


def range_band_summary(df: pd.DataFrame) -> pd.DataFrame:
    return (
        pd.crosstab(df["distance_yards"], df["range_band"])
        .reset_index()
        .rename_axis(None, axis=1)
    )


def write_outputs(
    output_dir: Path,
    rows: pd.DataFrame,
    monotonic: pd.DataFrame,
    rof_summary: pd.DataFrame,
    rof_caps: pd.DataFrame,
    penetration_summary: pd.DataFrame,
    bands: pd.DataFrame,
) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    rows.to_csv(output_dir / "expanded_penetration_rows.csv", index=False, encoding="utf-8-sig")
    monotonic.to_csv(output_dir / "monotonic_summary.csv", index=False, encoding="utf-8-sig")
    rof_summary.to_csv(output_dir / "rate_of_fire_model_summary.csv", index=False, encoding="utf-8-sig")
    rof_caps.to_csv(output_dir / "rate_of_fire_distance_caps.csv", index=False, encoding="utf-8-sig")
    penetration_summary.to_csv(
        output_dir / "penetration_model_summary.csv", index=False, encoding="utf-8-sig"
    )
    bands.to_csv(output_dir / "range_band_by_distance.csv", index=False, encoding="utf-8-sig")


def print_table(title: str, df: pd.DataFrame) -> None:
    print(f"\n{title}")
    print("-" * len(title))
    print(df.to_string(index=False))


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Infer ShipClass penetration-table generation models.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--shipclasses", type=Path, default=DEFAULT_SHIPCLASSES)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR)
    parser.add_argument("--no-write", action="store_true")
    args = parser.parse_args()

    rows = extract_rows(args.shipclasses)
    monotonic = monotonic_summary(rows)
    rof_summary, rof_caps = fit_rate_of_fire_models(rows)
    penetration_summary, fitted_models = fit_penetration_models(rows)
    bands = range_band_summary(rows)

    print(f"Source: {args.shipclasses}")
    print(
        f"Extracted {len(rows)} penetration rows from "
        f"{rows[['ship_name', 'battery_name']].drop_duplicates().shape[0]} batteries."
    )
    print(
        "Monotonic batteries: "
        f"ROF={monotonic['rate_of_fire_nonincreasing'].sum()}/{len(monotonic)} "
        f"Horizontal={monotonic['horizontal_penetration_nondecreasing'].sum()}/{len(monotonic)} "
        f"Vertical={monotonic['vertical_penetration_nonincreasing'].sum()}/{len(monotonic)}"
    )

    print_table(
        "Rate-of-Fire Model Summary",
        rof_summary.round({"exact": 4, "within_0.1": 4, "mae": 4, "rmse": 4, "max_abs": 3}),
    )
    print_table(
        "Rate-of-Fire Distance Caps",
        rof_caps.round({"cap_rate_of_fire_per_2_min": 3}),
    )
    print_table(
        "Penetration Model Summary",
        penetration_summary.round(
            {
                "r2_log": 4,
                "aic": 1,
                "exact": 4,
                "within_0.1": 4,
                "mae": 4,
                "rmse": 4,
                "max_abs": 3,
            }
        ),
    )

    for key in ["vertical:physical_distance_interactions", "horizontal:physical_relative_range"]:
        model = fitted_models[key]
        print(f"\nCoefficients: {key}")
        print("-" * (14 + len(key)))
        print(model.params.round(4).to_string())

    print_table("Range Band By Distance", bands)

    if not args.no_write:
        write_outputs(args.output_dir, rows, monotonic, rof_summary, rof_caps, penetration_summary, bands)
        print(f"\nWrote analysis CSVs to {args.output_dir}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
