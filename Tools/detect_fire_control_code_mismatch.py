#!/usr/bin/env python
"""Detect likely ShipClass fire-control code/table mismatches.

This script assumes a two-stage model:

1. A battery fire-control table is internally consistent and can be compressed
   into a latent left-top value using the fitted table multipliers.
2. The observed SK5 code may be a noisy label for that latent value. A latent
   true-code mixture model is then fitted with EM.

The output is intended for data review, not automatic correction.
"""

from __future__ import annotations

import argparse
import math
from pathlib import Path
import sys

try:
    import numpy as np
    import pandas as pd
except ImportError as exc:
    print(
        "Missing Python analysis dependency. Install with:\n"
        "  python -m pip install --user numpy pandas",
        file=sys.stderr,
    )
    raise SystemExit(2) from exc

import analyze_shipclass_fire_control as ship_fc
import fit_fire_control_latent_multipliers as latent_model


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SHIPCLASSES = REPO_ROOT / "Assets" / "StreamingAssets" / "Scenarios" / "ShipClasses.xml"
DEFAULT_OUTPUT_DIR = REPO_ROOT / "Tools" / "shipclass_fire_control_analysis"
DEFAULT_FACTORS = {
    "aspect": {"Broad": 1.0, "Narrow": 0.6005},
    "range_band": {"Short": 1.0, "Medium": 0.6010, "Long": 0.4165, "Extreme": 0.3567},
    "speed": {9.0: 1.0, 18.0: 0.6710, 27.0: 0.5265, 36.0: 0.4393, 45.0: 0.3758},
}


def normal_pdf(values: np.ndarray, means: np.ndarray, sigma: float) -> np.ndarray:
    z = (values[:, None] - means[None, :]) / sigma
    return np.exp(-0.5 * z * z) / (sigma * math.sqrt(2.0 * math.pi))


def fixed_factor_for_row(row: pd.Series) -> float:
    return (
        DEFAULT_FACTORS["aspect"][row["aspect"]]
        * DEFAULT_FACTORS["range_band"][row["range_band"]]
        * DEFAULT_FACTORS["speed"][float(row["speed_threshold_knot"])]
    )


def estimate_latent_left(cells: pd.DataFrame) -> pd.DataFrame:
    cells = cells.copy()
    cells["cell_factor"] = cells.apply(fixed_factor_for_row, axis=1)

    rows: list[dict[str, object]] = []
    for battery_id, group in cells.groupby("battery_id", sort=False):
        factor = group["cell_factor"].to_numpy(dtype=float)
        observed = group["fire_control_value"].to_numpy(dtype=float)
        latent_ls = float(np.dot(factor, observed) / np.dot(factor, factor))

        left_top = float(group["left_top_value"].iloc[0])
        rounded_latent = math.floor(latent_ls + 0.5)
        prediction = np.floor(latent_ls * factor + 0.5)
        residual = prediction - observed

        rows.append(
            {
                "battery_id": int(battery_id),
                "ship_name": group["ship_name"].iloc[0],
                "ship_type": group["ship_type"].iloc[0],
                "country": group["country"].iloc[0],
                "battery_name": group["battery_name"].iloc[0],
                "observed_code": group["code"].iloc[0],
                "observed_left": left_top,
                "latent_left": latent_ls,
                "latent_minus_observed": latent_ls - left_top,
                "rounded_latent": rounded_latent,
                "rounded_delta": rounded_latent - left_top,
                "internal_exact": float(np.mean(residual == 0)),
                "internal_mae": float(np.mean(np.abs(residual))),
                "internal_max_abs": float(np.max(np.abs(residual))),
                "range_yards": float(group["range_yards"].iloc[0]),
                "shell_size_inch": float(group["shell_size_inch"].iloc[0]),
                "rate_of_fire": float(group["rate_of_fire"].iloc[0]),
                "damage_rating": float(group["damage_rating"].iloc[0]),
            }
        )

    return pd.DataFrame(rows)


def initialize_parameters(latents: pd.DataFrame, codes: list[str]) -> tuple[np.ndarray, float, np.ndarray]:
    x = latents["latent_left"].to_numpy(dtype=float)
    obs = latents["observed_code"].to_numpy()
    global_mean = float(np.mean(x))
    means = []
    for code in codes:
        subset = x[obs == code]
        means.append(float(np.mean(subset)) if len(subset) else global_mean)
    means_array = np.array(means, dtype=float)
    sigma = float(np.sqrt(np.mean((x - np.array([means[codes.index(code)] for code in obs])) ** 2)))
    sigma = max(sigma, 0.35)
    pi = np.array([(obs == code).mean() for code in codes], dtype=float)
    pi = np.maximum(pi, 1e-6)
    pi /= pi.sum()
    return means_array, sigma, pi


def transition_matrix(
    observed_indices: np.ndarray,
    pi: np.ndarray,
    epsilon: float,
) -> np.ndarray:
    n = len(observed_indices)
    k = len(pi)
    transition = np.zeros((n, k), dtype=float)
    for row, obs_idx in enumerate(observed_indices):
        off_pi = pi.copy()
        off_pi[obs_idx] = 0.0
        off_sum = off_pi.sum()
        if off_sum <= 0:
            off_pi[:] = 1.0 / (k - 1)
            off_pi[obs_idx] = 0.0
        else:
            off_pi /= off_sum
        transition[row, :] = epsilon * off_pi
        transition[row, obs_idx] = 1.0 - epsilon
    return transition


def run_em(
    latents: pd.DataFrame,
    codes: list[str],
    epsilon_init: float,
    fixed_epsilon: float | None,
    max_iter: int,
    tol: float,
    min_sigma: float,
) -> dict[str, object]:
    x = latents["latent_left"].to_numpy(dtype=float)
    obs_indices = np.array([codes.index(code) for code in latents["observed_code"]], dtype=int)
    means, sigma, pi = initialize_parameters(latents, codes)
    epsilon = epsilon_init
    log_likelihood = -np.inf

    for iteration in range(max_iter):
        transition = transition_matrix(obs_indices, pi, epsilon)
        likelihood = normal_pdf(x, means, sigma)
        weighted = transition * likelihood
        denom = np.maximum(weighted.sum(axis=1, keepdims=True), 1e-300)
        posterior = weighted / denom

        weights = posterior.sum(axis=0)
        means = (posterior * x[:, None]).sum(axis=0) / np.maximum(weights, 1e-12)
        sigma = math.sqrt(
            float((posterior * (x[:, None] - means[None, :]) ** 2).sum() / max(len(x), 1))
        )
        sigma = max(sigma, min_sigma)
        pi = np.maximum(weights / len(x), 1e-6)
        pi /= pi.sum()
        if fixed_epsilon is None:
            epsilon = float(np.mean(1.0 - posterior[np.arange(len(x)), obs_indices]))
            epsilon = min(max(epsilon, 1e-4), 0.49)
        else:
            epsilon = fixed_epsilon

        new_log_likelihood = float(np.sum(np.log(np.maximum(weighted.sum(axis=1), 1e-300))))
        if abs(new_log_likelihood - log_likelihood) < tol:
            log_likelihood = new_log_likelihood
            break
        log_likelihood = new_log_likelihood

    transition = transition_matrix(obs_indices, pi, epsilon)
    likelihood = normal_pdf(x, means, sigma)
    weighted = transition * likelihood
    posterior = weighted / np.maximum(weighted.sum(axis=1, keepdims=True), 1e-300)

    return {
        "means": means,
        "sigma": sigma,
        "pi": pi,
        "epsilon": epsilon,
        "posterior": posterior,
        "log_likelihood": log_likelihood,
        "iterations": iteration + 1,
    }


def build_outputs(latents: pd.DataFrame, codes: list[str], em: dict[str, object]) -> tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame]:
    means = em["means"]
    sigma = float(em["sigma"])
    pi = em["pi"]
    posterior = em["posterior"]
    obs_indices = np.array([codes.index(code) for code in latents["observed_code"]], dtype=int)
    best_indices = posterior.argmax(axis=1)

    result = latents.copy()
    result["best_true_code"] = [codes[idx] for idx in best_indices]
    result["p_observed_code"] = posterior[np.arange(len(result)), obs_indices]
    result["p_best_true_code"] = posterior[np.arange(len(result)), best_indices]
    result["observed_code_mu"] = means[obs_indices]
    result["best_code_mu"] = means[best_indices]
    result["observed_code_z"] = (result["latent_left"].to_numpy(dtype=float) - means[obs_indices]) / sigma
    result["best_code_z"] = (result["latent_left"].to_numpy(dtype=float) - means[best_indices]) / sigma
    observed_likelihood = normal_pdf(result["latent_left"].to_numpy(dtype=float), means, sigma)[
        np.arange(len(result)), obs_indices
    ]
    best_likelihood = normal_pdf(result["latent_left"].to_numpy(dtype=float), means, sigma)[
        np.arange(len(result)), best_indices
    ]
    result["log_likelihood_gain_best_vs_observed"] = np.log(
        np.maximum(best_likelihood, 1e-300) / np.maximum(observed_likelihood, 1e-300)
    )
    result["is_reassigned_by_em"] = result["best_true_code"] != result["observed_code"]
    result["review_score"] = (
        (1.0 - result["p_observed_code"])
        + result["log_likelihood_gain_best_vs_observed"].clip(lower=0) / 4.0
        + result["rounded_delta"].abs() / 2.0
    )

    params = pd.DataFrame(
        {
            "code": codes,
            "mean_latent_left": means,
            "true_code_prior": pi,
            "observed_count": [int((latents["observed_code"] == code).sum()) for code in codes],
        }
    )
    params["sigma_common"] = sigma
    params["epsilon"] = float(em["epsilon"])
    params["log_likelihood"] = float(em["log_likelihood"])
    params["iterations"] = int(em["iterations"])

    confusion = pd.DataFrame(posterior, columns=codes)
    confusion.insert(0, "observed_code", latents["observed_code"].to_numpy())
    confusion = confusion.groupby("observed_code", dropna=False)[codes].sum().reset_index()
    return result, params, confusion


def print_table(title: str, df: pd.DataFrame, max_rows: int | None = None) -> None:
    print(f"\n{title}")
    print("-" * len(title))
    view = df.head(max_rows) if max_rows else df
    print(view.to_string(index=False))


def write_outputs(output_dir: Path, result: pd.DataFrame, params: pd.DataFrame, confusion: pd.DataFrame, candidates: pd.DataFrame) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    result.to_csv(output_dir / "code_mismatch_latent_by_battery.csv", index=False, encoding="utf-8-sig")
    params.to_csv(output_dir / "code_mismatch_model_parameters.csv", index=False, encoding="utf-8-sig")
    confusion.to_csv(output_dir / "code_mismatch_confusion_matrix.csv", index=False, encoding="utf-8-sig")
    candidates.to_csv(output_dir / "code_mismatch_candidates.csv", index=False, encoding="utf-8-sig")


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Detect likely fire-control code/table mismatches with a latent true-code EM model.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--shipclasses", type=Path, default=DEFAULT_SHIPCLASSES)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR)
    parser.add_argument("--epsilon-init", type=float, default=0.08)
    parser.add_argument("--fixed-epsilon", type=float, default=None, help="Fix label error rate instead of estimating it.")
    parser.add_argument("--max-iter", type=int, default=500)
    parser.add_argument("--tol", type=float, default=1e-8)
    parser.add_argument("--min-sigma", type=float, default=0.35)
    parser.add_argument("--top", type=int, default=30)
    parser.add_argument("--candidate-p-observed", type=float, default=0.50)
    parser.add_argument("--candidate-gain", type=float, default=1.0)
    parser.add_argument("--no-write", action="store_true")
    args = parser.parse_args()

    cells = latent_model.extract_cells(args.shipclasses)
    latents = estimate_latent_left(cells)
    codes = sorted(latents["observed_code"].dropna().unique(), key=lambda code: ship_fc.CODE_RANK.get(code, 999))
    em = run_em(
        latents,
        codes,
        epsilon_init=args.epsilon_init,
        fixed_epsilon=args.fixed_epsilon,
        max_iter=args.max_iter,
        tol=args.tol,
        min_sigma=args.min_sigma,
    )
    result, params, confusion = build_outputs(latents, codes, em)

    candidate_mask = (
        (result["is_reassigned_by_em"])
        | (result["p_observed_code"] < args.candidate_p_observed)
        | (result["log_likelihood_gain_best_vs_observed"] > args.candidate_gain)
        | (result["rounded_delta"].abs() >= 1)
    )
    candidates = result[candidate_mask].sort_values(
        ["review_score", "log_likelihood_gain_best_vs_observed", "p_observed_code"],
        ascending=[False, False, True],
    )

    print(f"Source: {args.shipclasses}")
    print(
        f"Fitted {len(latents)} batteries. "
        f"epsilon={em['epsilon']:.4f} sigma={em['sigma']:.4f} "
        f"log_likelihood={em['log_likelihood']:.2f} iterations={em['iterations']}"
    )
    print_table(
        "Code Parameters",
        params[
            [
                "code",
                "observed_count",
                "mean_latent_left",
                "true_code_prior",
                "sigma_common",
                "epsilon",
            ]
        ].round(4),
    )

    reassigned = int(result["is_reassigned_by_em"].sum())
    low_observed = int((result["p_observed_code"] < args.candidate_p_observed).sum())
    print(
        f"\nReassigned by EM: {reassigned}. "
        f"p(observed_code)<{args.candidate_p_observed}: {low_observed}. "
        f"Review candidates: {len(candidates)}."
    )

    print_table(
        f"Top {args.top} Review Candidates",
        candidates[
            [
                "review_score",
                "observed_code",
                "best_true_code",
                "p_observed_code",
                "p_best_true_code",
                "log_likelihood_gain_best_vs_observed",
                "latent_left",
                "observed_left",
                "rounded_delta",
                "observed_code_mu",
                "best_code_mu",
                "observed_code_z",
                "ship_name",
                "battery_name",
            ]
        ].round(
            {
                "review_score": 3,
                "p_observed_code": 3,
                "p_best_true_code": 3,
                "log_likelihood_gain_best_vs_observed": 3,
                "latent_left": 3,
                "observed_code_mu": 3,
                "best_code_mu": 3,
                "observed_code_z": 3,
            }
        ),
        max_rows=args.top,
    )

    print_table("Expected True-Code Counts By Observed Code", confusion.round(3))

    if not args.no_write:
        write_outputs(args.output_dir, result, params, confusion, candidates)
        print(f"\nWrote CSV outputs to: {args.output_dir}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
