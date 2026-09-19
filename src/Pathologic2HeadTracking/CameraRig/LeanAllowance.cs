using CameraUnlock.Core.Math;

namespace Pathologic2HeadTracking.CameraRig
{
    /// <summary>
    /// The lean clamp's arithmetic, with no engine in it. <see cref="LeanClamp"/> is
    /// the sweep and this is what it does with the answer, split apart so the policy
    /// half compiles into the test project - core keeps the same split in C++, where
    /// the query arrives as a callback.
    /// </summary>
    public static class LeanAllowance
    {
        /// <summary>
        /// Floors the 1/cos overreach. A lean sliding along a wall approaches the
        /// surface at a grazing angle where 1/cos diverges; 0.25 caps the trace at
        /// four times the standoff rather than sending it across the map.
        /// </summary>
        public const float MinApproachCosine = 0.25f;

        /// <summary>
        /// The allowance never reaches zero, and that is what keeps the clamp
        /// steerable. The controller recovers the lean the head is asking for by
        /// undoing the scale it applied, so an allowance of exactly zero applies a
        /// zero offset, a zero offset carries no direction, and the sweep is stuck
        /// re-testing the direction that blocked it - the lean is then refused in
        /// every direction including straight back out of the obstruction.
        ///
        /// The residue this leaves is 2% of the request toward the surface, under
        /// 11mm at the widest lean the shipped limits allow. For that to newly cull a
        /// surface, the clean eye would have to already sit within 11mm of the near
        /// clip distance from it, which is inside the standoff the clamp is trying to
        /// hold and where the game itself has already put the eye. So the floor never
        /// culls geometry that was being drawn.
        /// </summary>
        public const float Minimum = 0.02f;

        /// <summary>
        /// How far off a surface the eye is held. Geometry nearer the eye than the
        /// near plane is culled, so a wall held at less than the near distance is
        /// still not drawn and the player still sees through it. The standoff has to
        /// clear it with room to spare.
        /// </summary>
        public static float Standoff(float margin, float nearClipPlane)
        {
            float floor = nearClipPlane * 1.5f;
            return margin > floor ? margin : floor;
        }

        /// <summary>
        /// How far the trace has to reach. A ray that stops where the lean stops
        /// cannot see the surface the lean is about to come to rest against, so it
        /// overreaches by the standoff measured along the worst-case approach.
        /// </summary>
        public static float TraceDistance(float requested, float standoff)
        {
            return requested + standoff / MinApproachCosine;
        }

        /// <summary>
        /// Fraction of the requested lean that fits in front of a surface the sweep
        /// found. <paramref name="approachCosine"/> is the dot of the lean direction
        /// against the surface normal, so a lean meeting a wall at an angle is still
        /// held the full standoff off its face.
        /// </summary>
        public static float Target(float hitDistance, float requested, float approachCosine,
            float standoff)
        {
            float approach = approachCosine > MinApproachCosine ? approachCosine : MinApproachCosine;
            float allowed = hitDistance - standoff / approach;
            float fraction = allowed / requested;
            if (fraction < Minimum) return Minimum;
            return fraction > 1f ? 1f : fraction;
        }

        /// <summary>
        /// Moves the allowance toward the target. Tightening is instant and only the
        /// release is smoothed: easing into a smaller allowance leaves the eye inside
        /// the geometry for the duration of the ease, which is the whole bug. Easing
        /// back out stops the view popping when a doorframe clears between two frames.
        /// </summary>
        public static float Advance(float current, float target, float releaseSmoothing,
            float deltaTime)
        {
            if (target < current) return target;

            float advanced = SmoothingUtils.Smooth(current, target, releaseSmoothing, deltaTime);

            // Snapped at the end of the release. An exponential only ever approaches
            // its target, and in float32 the step underflows against the ulp of 1.0
            // long before it arrives: at the shipped 0.9 the allowance stalls around
            // 0.99999964 and stays there. Everything that asks "is the lean being cut"
            // reads that as yes for the rest of the session, so one brush against a
            // doorframe leaves the log reporting contact on every frame of an empty
            // field and the settings being rewritten every frame to no effect.
            return target - advanced < Settled ? target : advanced;
        }

        // Closer than this to the target and the remaining travel is not a lean
        // anyone can see: 1e-4 of a 0.4m request is 40 micrometres.
        private const float Settled = 1e-4f;
    }
}
