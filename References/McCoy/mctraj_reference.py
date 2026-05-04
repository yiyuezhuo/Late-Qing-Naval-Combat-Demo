"""Reference Python port of McCoy's MCTRAJ BASIC listing.

This file is based on the transcribed listing in
MCTRAJ_book_pages_183_186.bas. It intentionally keeps the program shape close
to the BASIC source: range is the independent variable, the drag model is a
Mach-CD table, and the corrector is iterated until the velocity change is below
E1.

The port is meant for reading and comparison, not as the calculator's production
exterior-ballistics implementation.
"""

from __future__ import annotations

from dataclasses import dataclass
import json
import math
from pathlib import Path
from typing import Iterable


GRAVITY_FTPS2 = 32.174
HEUN_EPSILON = 0.00001
MINUTES_PER_RADIAN = 3437.74677
MPH_TO_FPS = 22.0 / 15.0


@dataclass(frozen=True)
class AtmosphereCoefficients:
    rh1: float
    rh2: float
    tk1: float
    tk2: float
    pir: float
    vv1: float


ARMY_STANDARD_METRO = AtmosphereCoefficients(
    rh1=-0.00003158,
    rh2=0.0,
    tk1=-0.000006015,
    tk2=0.0,
    pir=0.0002048757,
    vv1=49.19,
)

ICAO_STANDARD_ATMOSPHERE = AtmosphereCoefficients(
    rh1=-0.00002926,
    rh2=-0.0000000001,
    tk1=-0.000006858,
    tk2=-0.000000000002776,
    pir=0.000208551,
    vv1=49.0223,
)


@dataclass(frozen=True)
class DragTable:
    name: str
    mach: tuple[float, ...]
    cd: tuple[float, ...]

    def __post_init__(self) -> None:
        if len(self.mach) != len(self.cd):
            raise ValueError("mach and cd must have the same length")
        if len(self.mach) < 2:
            raise ValueError("MCTRAJ interpolation requires at least two table rows")
        for left, right in zip(self.mach, self.mach[1:]):
            if right <= left:
                raise ValueError("mach table must be strictly increasing")

    def interpolate(self, mach_number: float) -> float:
        """Line 4010 subroutine: linear interpolation in the drag table."""

        for index in range(len(self.mach) - 1):
            if mach_number < self.mach[index + 1]:
                slope = (
                    (self.cd[index + 1] - self.cd[index])
                    / (self.mach[index + 1] - self.mach[index])
                )
                return self.cd[index] + slope * (mach_number - self.mach[index])
        raise ValueError("TRAJECTORY CANNOT REACH THE SPECIFIED MAXIMUM RANGE")


@dataclass(frozen=True)
class MctrajCase:
    drag_table: DragTable
    projectile_id: str
    muzzle_velocity_fps: float
    ballistic_coefficient_lb_in2: float
    sight_height_inches: float
    elevation_minutes: float
    density_ratio: float
    temperature_f: float
    range_print_interval: float
    range_terminate: float
    range_wind_mph: float = 0.0
    crosswind_mph: float = 0.0
    match_range: float = 0.0
    match_height_inches: float = 0.0
    use_yards: bool = True
    atmosphere: AtmosphereCoefficients = ARMY_STANDARD_METRO
    distance_integration_step: float = 1.0


@dataclass(frozen=True)
class TrajectoryPoint:
    range_value: float
    height_inches: float
    deflection_inches: float
    velocity_fps: float
    time_seconds: float
    vx_fps: float
    vy_fps: float
    vz_fps: float


@dataclass(frozen=True)
class MctrajResult:
    final_elevation_minutes: float
    points: tuple[TrajectoryPoint, ...]
    final_range_value: float
    final_height_feet: float
    final_deflection_feet: float
    final_time_seconds: float
    final_velocity_fps: float


@dataclass
class _State:
    range_feet: float
    print_range: float
    height_feet: float
    deflection_feet: float
    time_seconds: float
    vx_fps: float
    vy_fps: float
    vz_fps: float


@dataclass(frozen=True)
class _IntegrationResult:
    points: tuple[TrajectoryPoint, ...]
    state: _State
    total_velocity_fps: float


def run_mctraj(case: MctrajCase) -> MctrajResult:
    """Run one MCTRAJ case.

    If ``case.match_range`` is non-zero, this follows the BASIC listing's
    elevation-match loop and then runs the final printable trajectory.
    """

    unit_to_feet = 3.0 if case.use_yards else 1.0 / 0.3048
    d3 = case.distance_integration_step * unit_to_feet
    stop_range = case.range_terminate
    elevation = case.elevation_minutes

    if case.match_range != 0.0:
        target_height_feet = case.match_height_inches / 12.0
        history: list[tuple[float, float]] = []

        for _ in range(20):
            trial = _integrate(case, elevation, case.match_range, d3, collect_points=False)
            height_error = abs(trial.state.height_feet - target_height_feet)
            if height_error < 0.00001:
                break

            history.append((elevation, trial.state.height_feet))
            if len(history) <= 2:
                elevation = elevation + 0.2
                continue

            prev_elevation, prev_height = history[-2]
            curr_elevation, curr_height = history[-1]
            if prev_height == curr_height:
                raise RuntimeError("ELEVATION ANGLE ITERATION DID NOT CONVERGE")
            elevation = curr_elevation + (
                (target_height_feet - curr_height)
                * (prev_elevation - curr_elevation)
                / (prev_height - curr_height)
            )
        else:
            raise RuntimeError("ELEVATION ANGLE ITERATION DID NOT CONVERGE")

    final = _integrate(case, elevation, stop_range, d3, collect_points=True)
    return MctrajResult(
        final_elevation_minutes=elevation,
        points=final.points,
        final_range_value=final.state.print_range,
        final_height_feet=final.state.height_feet,
        final_deflection_feet=final.state.deflection_feet,
        final_time_seconds=final.state.time_seconds,
        final_velocity_fps=final.total_velocity_fps,
    )


def _integrate(
    case: MctrajCase,
    elevation_minutes: float,
    stop_range: float,
    d3: float,
    collect_points: bool,
) -> _IntegrationResult:
    atmosphere = case.atmosphere
    wind_range = case.range_wind_mph * MPH_TO_FPS
    wind_cross = case.crosswind_mph * MPH_TO_FPS

    state = _State(
        range_feet=0.0,
        print_range=0.0,
        height_feet=-case.sight_height_inches / 12.0,
        deflection_feet=0.0,
        time_seconds=0.0,
        vx_fps=case.muzzle_velocity_fps * math.cos(elevation_minutes / MINUTES_PER_RADIAN),
        vy_fps=case.muzzle_velocity_fps * math.sin(elevation_minutes / MINUTES_PER_RADIAN),
        vz_fps=0.0,
    )

    points: list[TrajectoryPoint] = []
    if collect_points:
        points.append(
            _trajectory_point(
                state,
                case.muzzle_velocity_fps,
            )
        )

    c3 = (atmosphere.pir * case.density_ratio) / case.ballistic_coefficient_lb_in2
    next_print_range = case.range_print_interval
    total_velocity = case.muzzle_velocity_fps

    while True:
        relative_speed = _relative_speed(state, wind_range, wind_cross)
        c1 = case.drag_table.interpolate(
            relative_speed / _speed_of_sound(atmosphere, case.temperature_f, state.height_feet)
        )

        c4 = (
            c3
            * c1
            * relative_speed
            * math.exp((atmosphere.rh1 + atmosphere.rh2 * state.height_feet) * state.height_feet)
            / state.vx_fps
        )
        a1 = c4 * (state.vx_fps - wind_range)
        a2 = c4 * state.vy_fps - GRAVITY_FTPS2 / state.vx_fps
        a3 = c4 * (state.vz_fps - wind_cross)

        next_range_feet = state.range_feet + d3
        next_print = state.print_range + case.distance_integration_step
        vx_pred = state.vx_fps + a1 * d3
        vy_pred = state.vy_fps + a2 * d3
        vz_pred = state.vz_fps + a3 * d3
        pred_speed = _relative_speed_components(vx_pred, vy_pred, vz_pred, wind_range, wind_cross)

        while True:
            previous_pred_speed = pred_speed
            c2 = case.drag_table.interpolate(
                pred_speed / _speed_of_sound(atmosphere, case.temperature_f, state.height_feet)
            )
            c5 = (
                c3
                * c2
                * pred_speed
                * math.exp((atmosphere.rh1 + atmosphere.rh2 * state.height_feet) * state.height_feet)
                / vx_pred
            )
            a4 = c5 * (vx_pred - wind_range)
            a5 = c5 * vy_pred - GRAVITY_FTPS2 / vx_pred
            a6 = c5 * (vz_pred - wind_cross)
            vx_corr = state.vx_fps + 0.5 * (a1 + a4) * d3
            vy_corr = state.vy_fps + 0.5 * (a2 + a5) * d3
            vz_corr = state.vz_fps + 0.5 * (a3 + a6) * d3
            pred_speed = _relative_speed_components(
                vx_corr,
                vy_corr,
                vz_corr,
                wind_range,
                wind_cross,
            )
            if abs((pred_speed - previous_pred_speed) / pred_speed) <= HEUN_EPSILON:
                break
            vx_pred, vy_pred, vz_pred = vx_corr, vy_corr, vz_corr

        next_height = state.height_feet + ((state.vy_fps + vy_corr) / (state.vx_fps + vx_corr)) * d3
        next_deflection = (
            state.deflection_feet
            + ((state.vz_fps + vz_corr) / (state.vx_fps + vx_corr)) * d3
        )
        next_time = state.time_seconds + (2.0 * d3) / (state.vx_fps + vx_corr)
        total_velocity = math.sqrt(vx_corr * vx_corr + vy_corr * vy_corr + vz_corr * vz_corr)

        state = _State(
            range_feet=next_range_feet,
            print_range=next_print,
            height_feet=next_height,
            deflection_feet=next_deflection,
            time_seconds=next_time,
            vx_fps=vx_corr,
            vy_fps=vy_corr,
            vz_fps=vz_corr,
        )

        if collect_points and state.print_range >= next_print_range:
            points.append(_trajectory_point(state, total_velocity))
            next_print_range += case.range_print_interval

        if state.print_range >= stop_range:
            return _IntegrationResult(tuple(points), state, total_velocity)


def _speed_of_sound(
    atmosphere: AtmosphereCoefficients,
    temperature_f: float,
    height_feet: float,
) -> float:
    local_temperature_f = (
        (temperature_f + 459.67)
        * math.exp((atmosphere.tk1 + atmosphere.tk2 * height_feet) * height_feet)
        - 459.67
    )
    return atmosphere.vv1 * math.sqrt(local_temperature_f + 459.67)


def _relative_speed(state: _State, wind_range_fps: float, wind_cross_fps: float) -> float:
    return _relative_speed_components(
        state.vx_fps,
        state.vy_fps,
        state.vz_fps,
        wind_range_fps,
        wind_cross_fps,
    )


def _relative_speed_components(
    vx_fps: float,
    vy_fps: float,
    vz_fps: float,
    wind_range_fps: float,
    wind_cross_fps: float,
) -> float:
    return math.sqrt(
        (vx_fps - wind_range_fps) ** 2
        + vy_fps**2
        + (vz_fps - wind_cross_fps) ** 2
    )


def _trajectory_point(state: _State, velocity_fps: float) -> TrajectoryPoint:
    return TrajectoryPoint(
        range_value=state.print_range,
        height_inches=12.0 * state.height_feet,
        deflection_inches=12.0 * state.deflection_feet,
        velocity_fps=velocity_fps,
        time_seconds=state.time_seconds,
        vx_fps=state.vx_fps,
        vy_fps=state.vy_fps,
        vz_fps=state.vz_fps,
    )


def load_case_json(path: str | Path) -> MctrajCase:
    """Load a case from JSON for quick experiments.

    Expected shape:

    {
      "drag_table": {"name": "example", "mach": [0.5, 1.0], "cd": [0.3, 0.4]},
      "projectile_id": "...",
      "muzzle_velocity_fps": 2230,
      ...
    }
    """

    data = json.loads(Path(path).read_text(encoding="utf-8"))
    atmosphere_name = data.pop("atmosphere", "army")
    atmosphere = (
        ICAO_STANDARD_ATMOSPHERE
        if str(atmosphere_name).lower() in {"icao", "i"}
        else ARMY_STANDARD_METRO
    )
    drag_data = data.pop("drag_table")
    drag_table = DragTable(
        name=drag_data["name"],
        mach=tuple(float(value) for value in drag_data["mach"]),
        cd=tuple(float(value) for value in drag_data["cd"]),
    )
    return MctrajCase(drag_table=drag_table, atmosphere=atmosphere, **data)


def rows_for_print(points: Iterable[TrajectoryPoint]) -> list[dict[str, float]]:
    return [
        {
            "range": point.range_value,
            "height_in": point.height_inches,
            "deflection_in": point.deflection_inches,
            "velocity_fps": point.velocity_fps,
            "time_s": point.time_seconds,
            "vx_fps": point.vx_fps,
            "vy_fps": point.vy_fps,
            "vz_fps": point.vz_fps,
        }
        for point in points
    ]


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Run the McCoy MCTRAJ reference port.")
    parser.add_argument("case_json", help="JSON file describing an MCTRAJ case")
    args = parser.parse_args()

    result = run_mctraj(load_case_json(args.case_json))
    print(f"final_elevation_minutes={result.final_elevation_minutes:.6f}")
    print(json.dumps(rows_for_print(result.points), indent=2))
