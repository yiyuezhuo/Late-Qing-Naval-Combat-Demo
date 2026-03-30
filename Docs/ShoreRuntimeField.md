# Shore Runtime Field Format

## Purpose

`*_shoreRuntime.bytes` stores a precomputed coastal avoidance field for naval ROI navigation.

The file is used by runtime CPU-side obstacle avoidance. It is not intended for shader sampling.

Inside the ROI elevation path, ships can use this field to:

- estimate distance from land in pixel units
- read a gradient that points away from land
- steer smoothly along coastlines instead of relying only on binary collision checks

Outside the ROI elevation path, the game falls back to the legacy elevation-based obstacle avoidance logic.

## Geographic Scope

The file itself does not define the active ROI bounds.

Runtime code must take the geographic ROI bounds from the active `ElevationProvider` configuration:

- `roiLongitudeDeg0`
- `roiLongitudeDeg1`
- `roiLatitudeDeg0`
- `roiLatitudeDeg1`
- `useROI`

The file name may include a human-readable ROI hint such as `105_146_15_55`, but runtime logic must not depend on the file name for the authoritative bounds.

## Binary Encoding

The file is written with `.NET BinaryWriter` and read with `.NET BinaryReader`.

This implies:

- little-endian numeric encoding
- `BinaryWriter.Write(string)` / `BinaryReader.ReadString()` for the magic header

## Header Layout

The header is written in this exact order:

1. `string magic`
2. `int32 width`
3. `int32 height`
4. `float32 landThreshold`
5. `float32 maxDistance`

Current magic value:

- `SFD1`

## Pixel Payload Layout

After the header, the file stores one record per pixel in row-major order.

Row-major means:

- all pixels of row `0` from `x=0` to `x=width-1`
- then all pixels of row `1`
- and so on

Each pixel record is written in this exact order:

1. `uint16 distance`
2. `int8 gradX`
3. `int8 gradY`

So each pixel costs 4 bytes total.

## Decode Rules

### Distance

`distance` is stored as a normalized unsigned 16-bit value.

Decode formula:

```text
distancePixels = encodedDistance / 65535.0 * maxDistance
```

Interpretation:

- `distancePixels == 0` means land
- `distancePixels > 0` means water
- the unit is pixel distance in the ROI raster, not meters and not nautical miles

### Gradient

`gradX` and `gradY` are stored as signed bytes.

Decode formula:

```text
gradientX = encodedGradX / 127.0
gradientY = encodedGradY / 127.0
```

Interpretation:

- the gradient points away from land
- `X` follows the ROI texture horizontal axis, which maps to longitude
- `Y` follows the ROI texture vertical axis, which maps to latitude

## Runtime Sampling Contract

Runtime code should only use the field when all of the following are true:

- the active elevation provider is using ROI elevation for the queried position
- the `.bytes` file loaded successfully
- the field dimensions match the active ROI height texture dimensions

Recommended sampling behavior:

- map latitude/longitude into ROI pixel coordinates using the runtime `ElevationProvider`
- sample the field bilinearly for smooth steering
- treat missing or invalid field data as a reason to fall back to legacy avoidance

## Legacy Fallback

This field supplements runtime avoidance; it does not replace the legacy code path globally.

When the field is unavailable, invalid, disabled in preferences, or not applicable to the current position, the game should continue using the old obstacle avoidance behavior based on elevation collision checks.
