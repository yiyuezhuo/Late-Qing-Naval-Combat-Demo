# Shore Field Assets

## Purpose

The ROI shore field is a precomputed coastal avoidance dataset derived from the ROI elevation texture.

It is used in two places:

- CPU-side ROI obstacle avoidance
- optional in-game visualization on the earth shader

Outside the ROI elevation path, the game still falls back to the legacy elevation-based obstacle avoidance logic.

## Runtime Assets

The runtime representation is a packed texture plus a metadata JSON sidecar:

- `*_shoreField.png`
- `*_shoreField.json`

Example:

- `roi_105_146_15_55_uint16_9840x9600_shoreField.png`
- `roi_105_146_15_55_uint16_9840x9600_shoreField.json`

The old `*_shoreRuntime.bytes` format is legacy and is no longer the primary runtime source.

## Geographic Scope

The file name may contain a human-readable ROI hint, but runtime code must not depend on the file name for authoritative bounds.

Runtime code must use the active `ElevationProvider` ROI configuration:

- `roiLongitudeDeg0`
- `roiLongitudeDeg1`
- `roiLatitudeDeg0`
- `roiLatitudeDeg1`
- `useROI`

## Packed Texture Layout

The packed PNG stores one texel per ROI pixel.

Current channel meaning:

- `R`: normalized distance-from-land
- `G`: gradient X encoded from `[-1, 1]` into `[0, 1]`
- `B`: gradient Y encoded from `[-1, 1]` into `[0, 1]`

`X` follows the ROI texture horizontal axis, which maps to longitude.

`Y` follows the ROI texture vertical axis, which maps to latitude.

## Metadata JSON

The JSON sidecar provides the decode scale and validation metadata.

Important fields:

- `width`
- `height`
- `landThreshold`
- `maxDistancePixels`
- `exportMode`

The runtime should treat `maxDistancePixels` as the authoritative distance decode scale.

## Decode Rules

### Distance

```text
distancePixels = R / 255.0 * maxDistancePixels
```

Interpretation:

- `distancePixels == 0` means land
- `distancePixels > 0` means water
- the unit is pixel distance in the ROI raster

### Gradient

```text
gradientX = G / 255.0 * 2.0 - 1.0
gradientY = B / 255.0 * 2.0 - 1.0
```

Interpretation:

- the gradient points away from land
- runtime code may renormalize after bilinear interpolation

## Runtime Sampling Contract

Runtime code should only use the field when all of the following are true:

- the active elevation provider is using ROI elevation for the queried position
- the packed shore-field texture loaded successfully
- the metadata JSON loaded successfully
- the packed field dimensions match the active ROI height texture dimensions

Recommended sampling behavior:

- map latitude/longitude into ROI pixel coordinates using the runtime `ElevationProvider`
- bilinearly sample the packed field
- fall back to legacy obstacle avoidance when the field is unavailable or invalid

## Visualization Contract

The same packed texture may also be passed into the earth shader.

Two optional preference-controlled overlays exist:

- distance-field display
- gradient-field display

When both display toggles are off, the shader should avoid sampling the shore-field texture so that the visualization feature does not add avoidable runtime cost.
