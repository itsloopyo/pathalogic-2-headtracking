using UnityEngine;

namespace Pathologic2HeadTracking.Aim
{
    /// <summary>
    /// Projects a world point into the frame that was actually drawn.
    ///
    /// The projection is basis-to-basis in the strictest available sense: it reads
    /// the camera's own worldToCameraMatrix and projectionMatrix rather than
    /// re-deriving a composition from yaw/pitch/roll. The tracking controller writes
    /// worldToCameraMatrix in Camera.onPreCull and the override is sticky, so by the
    /// time OnGUI runs - after the camera has rendered - the matrix still on the
    /// camera is the one this frame was drawn with. There is no second derivation
    /// that can disagree with the first, and no Euler formula to drift on combined
    /// poses.
    ///
    /// The same fact compensates any of the game's own HUD that projects a real
    /// world point through the camera, since Camera.WorldToScreenPoint reads the
    /// same override.
    /// </summary>
    public static class AimProjection
    {
        // A point approaching the plane of the camera projects to infinity. The guard
        // is on the magnitude, not just the sign - a reticle at 1e30 is a NaN on its
        // way to a vertex buffer.
        private const float ClipEpsilon = 1e-4f;

        /// <summary>
        /// Projects a world point to normalized device coordinates, +-1 at the edges of
        /// the drawn frustum. This is the resolution-independent form and the one a
        /// uGUI element is moved by: a canvas is sized in its own units, and dividing
        /// screen pixels by a scale factor is only equivalent for a screen-space
        /// overlay canvas that fills the window.
        /// </summary>
        public static bool TryProjectPointNdc(Camera cam, Vector3 worldPoint, out Vector2 ndc)
        {
            return Project(cam, new Vector4(worldPoint.x, worldPoint.y, worldPoint.z, 1f), out ndc);
        }

        /// <summary>
        /// Projects a world DIRECTION, which is where a point at infinity lands. Used
        /// on a definite no-hit: the aim has no surface to mark, so the reticle marks
        /// the direction instead. Never a substituted fixed distance.
        /// </summary>
        public static bool TryProjectDirectionNdc(Camera cam, Vector3 worldDirection, out Vector2 ndc)
        {
            return Project(cam, new Vector4(worldDirection.x, worldDirection.y, worldDirection.z, 0f), out ndc);
        }

        /// <summary>
        /// Normalized device coordinates to screen pixels, Unity convention (y up from
        /// the bottom). Screen rather than the camera's pixel rect: the reticle is
        /// placed in screen pixels, and Pathologic 2's game camera covers the whole
        /// window.
        /// </summary>
        public static Vector2 NdcToScreen(Vector2 ndc)
        {
            return new Vector2(
                (ndc.x * 0.5f + 0.5f) * Screen.width,
                (ndc.y * 0.5f + 0.5f) * Screen.height);
        }

        private static bool Project(Camera cam, Vector4 homogeneous, out Vector2 ndc)
        {
            ndc = Vector2.zero;
            if (cam == null) return false;

            Matrix4x4 viewProjection = cam.projectionMatrix * cam.worldToCameraMatrix;
            Vector4 clip = viewProjection * homogeneous;

            // Unity hands back the OpenGL-convention projection from this property
            // whatever the graphics API underneath is, and there clip.w == -z_view.
            // View space has the camera looking down -z, so w is positive in front.
            // Negated rather than written as "< ClipEpsilon", so a NaN fails the test
            // instead of passing it. A NaN w compares false against everything, and
            // the plain form let it through to become a NaN screen position, which is
            // the one outcome this guard exists to stop.
            if (!(clip.w >= ClipEpsilon)) return false;

            ndc = new Vector2(clip.x / clip.w, clip.y / clip.w);
            return true;
        }
    }
}
