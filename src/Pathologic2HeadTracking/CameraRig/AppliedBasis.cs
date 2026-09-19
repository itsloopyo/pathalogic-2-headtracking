using UnityEngine;

namespace Pathologic2HeadTracking.CameraRig
{
    /// <summary>
    /// Which way the tracked camera actually moved, relative to where the game
    /// pointed it, read off the view matrix that was written rather than
    /// re-derived from the pose.
    ///
    /// Every one of these is a sign the doctrine says cannot be reasoned out and
    /// has to be measured. Putting them in the log as numbers means a direction
    /// fault is settled by reading one line, instead of by asking somebody to
    /// describe which way a view moved.
    /// </summary>
    public struct AppliedBasis
    {
        /// <summary>Positive when the view turned to the player's right.</summary>
        public float TurnedRight;

        /// <summary>Positive when the view turned upward.</summary>
        public float TurnedUp;

        /// <summary>Positive when the top of the view tilted to the right.</summary>
        public float TiltedRight;

        /// <summary>Positive when the render eye moved to the player's right.</summary>
        public float LeanedRight;

        /// <summary>Positive when the render eye moved up.</summary>
        public float LeanedUp;

        /// <summary>Positive when the render eye moved forward.</summary>
        public float LeanedForward;

        /// <summary>
        /// Reads the basis out of the camera's current view matrix. Valid only
        /// after the render hook has written it, so from OnGUI rather than from
        /// LateUpdate.
        ///
        /// The view matrix rows ARE the camera's world-space axes: row 0 is right,
        /// row 1 is up, row 2 is backward, because Unity's view space looks down
        /// -z. The eye is recovered as -R-transpose times the translation column,
        /// which is exact for an orthonormal rotation.
        /// </summary>
        public static AppliedBasis Read(Camera cam)
        {
            Matrix4x4 view = cam.worldToCameraMatrix;

            Vector3 trackedRight = new Vector3(view.m00, view.m01, view.m02);
            Vector3 trackedUp = new Vector3(view.m10, view.m11, view.m12);
            Vector3 trackedForward = -new Vector3(view.m20, view.m21, view.m22);

            Vector3 t = new Vector3(view.m03, view.m13, view.m23);
            Vector3 trackedEye = -(trackedRight * t.x + trackedUp * t.y
                                   - trackedForward * t.z);

            Transform clean = cam.transform;
            Vector3 eyeDelta = trackedEye - clean.position;

            AppliedBasis basis;
            basis.TurnedRight = Vector3.Dot(trackedForward, clean.right);
            basis.TurnedUp = Vector3.Dot(trackedForward, clean.up);
            basis.TiltedRight = Vector3.Dot(trackedUp, clean.right);
            basis.LeanedRight = Vector3.Dot(eyeDelta, clean.right);
            basis.LeanedUp = Vector3.Dot(eyeDelta, clean.up);
            basis.LeanedForward = Vector3.Dot(eyeDelta, clean.forward);
            return basis;
        }
    }
}
