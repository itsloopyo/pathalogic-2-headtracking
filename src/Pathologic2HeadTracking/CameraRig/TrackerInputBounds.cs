namespace Pathologic2HeadTracking.CameraRig
{
    /// <summary>
    /// The range a tracker sample has to fall in before the pipeline sees it.
    ///
    /// The receiver rejects anything that is not finite as a float, but the socket
    /// takes datagrams from any host on the network, and a finite value near
    /// float.MaxValue overflows the first subtraction or multiply it meets: the
    /// interpolator's segment delta, or the zoom factor. The smoothing stages then
    /// lerp toward that infinity, hold a NaN, and the view stays broken for the rest
    /// of the session.
    ///
    /// The bounds are far outside anything a real head produces, so a sample from a
    /// working tracker passes through bit for bit: the OpenTrack protocol's angles
    /// are +-180 degrees, and the lean limits downstream cut a position at half a
    /// metre long before 10 m.
    /// </summary>
    public static class TrackerInputBounds
    {
        public const float MaxAngleDegrees = 180f;
        public const float MaxPositionMeters = 10f;

        /// <summary>Clamps into [-limit, limit], and reports whether it had to.</summary>
        public static float Clamp(float value, float limit, ref bool clamped)
        {
            if (value > limit) { clamped = true; return limit; }
            if (value < -limit) { clamped = true; return -limit; }
            return value;
        }
    }
}
