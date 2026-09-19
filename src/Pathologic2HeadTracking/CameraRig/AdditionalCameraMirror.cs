using System.Collections.Generic;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Unity.Tracking;
using Pathologic2HeadTracking.Game;
using UnityEngine;

namespace Pathologic2HeadTracking.CameraRig
{
    /// <summary>
    /// Gives the game camera rig's other cameras the same head basis as the main one.
    ///
    /// Pathologic 2 draws one frame out of several cameras hanging off the same rig.
    /// The survey on a loaded world lists, besides "[Camera] Main": "[Camera] Sky"
    /// (depth -2, near 500, culling mask layer Sky alone) and "[Camera] Fog Portals"
    /// (depth -3, rendering to a texture the main pass samples). The tracking
    /// controller writes the main camera only, so without this the world turns with
    /// the head while the sky and the fog mask stay bolted to where the game pointed
    /// the camera - which reads as geometry going wrong at the edges of the frame
    /// rather than as a camera fault.
    ///
    /// The test for "does this camera render the player's view" is membership of the
    /// rig plus a perspective projection. "[Camera] UI" hangs off the same rig and is
    /// orthographic, and a head basis has no meaning for a screen-space overlay.
    /// Neither test names a camera, so a patch that adds a pass gets the basis too.
    ///
    /// The basis is written through the same ViewMatrixModifier call the controller
    /// makes, with the same pose and the same yaw mode, so there is one composition
    /// and two call sites rather than two compositions that can disagree. The
    /// rotation is relative to each camera's own clean transform, which is what makes
    /// them all land on the same orientation without any of them knowing about the
    /// others.
    ///
    /// The one thing this does not follow is the controller's 0.3s fade out, whose
    /// intermediate values it does not publish: these cameras hold the last full head
    /// rotation for that fade and then reset with everything else. It is 0.3s of a
    /// transition out of gameplay, on passes with no parallax in them.
    /// </summary>
    public sealed class AdditionalCameraMirror
    {
        private readonly ViewMatrixTrackingController _controller;
        private readonly List<Camera> _applied = new List<Camera>();

        public AdditionalCameraMirror(ViewMatrixTrackingController controller)
        {
            _controller = controller;
        }

        /// <summary>
        /// Subscribes straight to Camera.onPreCull rather than through
        /// RenderPipelineHelper. That helper keeps a single global slot per hook with
        /// no ownership token, and the tracking controller already holds it - adding a
        /// second callback there throws, and removing one would silently unhook the
        /// controller. Pathologic 2 is Unity 2018.4 on the built-in pipeline, so the
        /// legacy event is the only one that fires anyway.
        /// </summary>
        public void Enable()
        {
            Camera.onPreCull += OnPreCull;
        }

        public void Disable()
        {
            Camera.onPreCull -= OnPreCull;
            ResetApplied();
        }

        private void OnPreCull(Camera cam)
        {
            if (cam == null || cam.orthographic) return;

            Camera main = _controller.MainCamera;
            if (main == null || cam == main) return;

            // Ahead of the rig lookup, which is two reflected reads per camera per
            // frame and only matters while there is a basis to hand on.
            if (!_controller.IsApplyingTracking)
            {
                ResetApplied();
                return;
            }

            Transform rig = GameReflection.AimTransform;
            if (rig == null || !cam.transform.IsChildOf(rig)) return;

            if (!_applied.Contains(cam)) _applied.Add(cam);

            Vec3 position = _controller.LastTrackingPosition;
            var offset = new Vector3(position.X, position.Y, position.Z);

            if (_controller.WorldSpaceYaw)
            {
                ViewMatrixModifier.ApplyHeadRotationDecomposed(cam,
                    _controller.LastTrackingYaw, _controller.LastTrackingPitch,
                    _controller.LastTrackingRoll, offset);
            }
            else
            {
                ViewMatrixModifier.ApplyHeadRotation(cam,
                    _controller.LastTrackingYaw, _controller.LastTrackingPitch,
                    _controller.LastTrackingRoll, offset);
            }
        }

        /// <summary>
        /// worldToCameraMatrix is a sticky override: once written, Unity stops
        /// recomputing it from the transform. Without this reset these passes keep the
        /// last head rotation for the rest of the session.
        /// </summary>
        private void ResetApplied()
        {
            if (_applied.Count == 0) return;

            for (int i = 0; i < _applied.Count; i++)
                if (_applied[i] != null) _applied[i].ResetWorldToCameraMatrix();
            _applied.Clear();
        }
    }
}
