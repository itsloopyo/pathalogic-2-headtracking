namespace Pathologic2HeadTracking.CameraRig
{
    /// <summary>
    /// Keeps head tracking's effect on the picture the same size whatever the game
    /// does with its field of view.
    ///
    /// Pathologic 2 renders at the player's chosen FOV plus GameCamera.AdditionalFov,
    /// which its blueprint FovEffectNode drives during scripted effects, and replaces
    /// both outright with a cutscene FOV. A narrow FOV magnifies everything in the
    /// frame, head tracking included: the head still turns ten degrees and the camera
    /// still turns ten degrees, and the picture simply moves further, by the ratio
    /// between the two fields of view. A player does not read that as a zoom - they
    /// read it as the mod's sensitivity jumping.
    ///
    /// This is an engine-boundary conversion in the same family as the axis signs,
    /// not a sensitivity knob and not an aim-down-sights setting. It is exactly 1.0
    /// whenever the game is at its own un-zoomed FOV, so ordinary play is untouched,
    /// and nothing about it is user-configurable.
    ///
    /// Mirrors cameraunlock-core's C++ cameraunlock/camera/zoom_compensation.h,
    /// which has no C# counterpart yet.
    /// </summary>
    public static class ZoomCompensation
    {
        private const float DegToRad = (float)(System.Math.PI / 180.0);

        /// <summary>
        /// The factor a translation - a lean - scales by, from the FOV being
        /// rendered now and the game's un-zoomed one.
        ///
        /// Both angles must be the same axis. That is the whole of the difficulty
        /// elsewhere, and the reason it is not a difficulty here: GameCamera.ApplyFov
        /// assigns GraphicsGameSettings.FieldOfView straight into Camera.fieldOfView,
        /// so the base and the live value are the same vertical angle by construction
        /// rather than by assumption. The check that says so is that this returns 1.0
        /// in ordinary gameplay.
        /// </summary>
        public static float Factor(float fovNowDeg, float fovBaseDeg)
        {
            if (!IsUsableFov(fovNowDeg) || !IsUsableFov(fovBaseDeg))
                return 1f;

            float tanNow = (float)System.Math.Tan(fovNowDeg * 0.5f * DegToRad);
            float tanBase = (float)System.Math.Tan(fovBaseDeg * 0.5f * DegToRad);
            if (tanBase <= 0f || tanNow <= 0f)
                return 1f;

            return tanNow / tanBase;
        }

        /// <summary>
        /// An angle in degrees, rescaled so it displaces the image by as much as the
        /// original angle did at the base FOV.
        ///
        /// The tangent round trip is what makes that exact rather than approximate:
        /// image displacement goes as tan(angle) / tan(fov/2), so holding the ratio
        /// fixed means tan(out) = tan(in) * factor. At the angles a neck reaches it
        /// is close to a plain multiply, and it stays honest at the large ones.
        /// </summary>
        public static float ScaleAngle(float angleDeg, float factor)
        {
            if (factor <= 0f || factor == 1f) return angleDeg;
            if (angleDeg <= -89.9f || angleDeg >= 89.9f) return angleDeg;

            return (float)System.Math.Atan(
                System.Math.Tan(angleDeg * DegToRad) * factor) / DegToRad;
        }

        private static bool IsUsableFov(float fovDeg)
        {
            // Straight off a game object, so this is the boundary check. A mod that
            // cannot read a live FOV applies no compensation rather than a guessed one.
            return !float.IsNaN(fovDeg) && !float.IsInfinity(fovDeg)
                   && fovDeg > 1f && fovDeg < 179f;
        }
    }
}
