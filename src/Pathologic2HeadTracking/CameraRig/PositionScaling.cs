using CameraUnlock.Core.Data;

namespace Pathologic2HeadTracking.CameraRig
{
    /// <summary>
    /// Uniform scaling of a position settings block, which is how the lean clamp
    /// hands its allowance to the position processor.
    ///
    /// Scaling the sensitivities and the limits by the same fraction scales the
    /// offset uniformly, because clamp(raw * s * f, +-L * f) == f * clamp(raw * s,
    /// +-L). Only the magnitudes scale: the smoothing pair and the inversion flags
    /// are conversions rather than distances and are carried across untouched.
    ///
    /// That identity is exact at rest and approximate in motion. The processor
    /// smooths BETWEEN its two clamps and only its settings are scaled, not its
    /// filter state, so on the frames right after the allowance tightens the lagging
    /// vector is clamped against the newly shrunk per-axis box and the surviving
    /// offset can point somewhere the request did not. It resolves inside the
    /// smoothing time constant, which is 23.5ms at the shipped RemoteSmoothing, and
    /// it cannot breach the box: every component is inside the new limits by the
    /// time it is applied. An allowance of exactly zero is exact either way, since
    /// clamping to +-0 has nothing to lag.
    /// </summary>
    public static class PositionScaling
    {
        public static PositionSettings Scale(PositionSettings settings, float scale)
        {
            return new PositionSettings(
                settings.SensitivityX * scale,
                settings.SensitivityY * scale,
                settings.SensitivityZ * scale,
                settings.LimitX * scale,
                settings.LimitY * scale,
                settings.LimitYDown * scale,
                settings.LimitZ * scale,
                settings.LimitZBack * scale,
                settings.LocalSmoothing,
                settings.RemoteSmoothing,
                settings.InvertX,
                settings.InvertY,
                settings.InvertZ);
        }
    }
}
