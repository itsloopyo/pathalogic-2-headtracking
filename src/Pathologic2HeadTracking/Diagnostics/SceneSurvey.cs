using System.Text;
using BepInEx.Logging;
using Pathologic2HeadTracking.Aim;
using Pathologic2HeadTracking.Game;
using UnityEngine;

namespace Pathologic2HeadTracking.Diagnostics
{
    /// <summary>
    /// One-shot survey of the things the mod reaches into, written when a world first
    /// comes up: every enabled camera and what it draws, the physics layer table the
    /// allow-lists are checked against, and the HUD element the interaction prompt is
    /// moved through.
    ///
    /// Only the camera set is here rather than left to be worked out from a screenshot.
    /// A second camera drawing world geometry would have to be given the head basis
    /// too, and the symptom of missing one - part of the frame not turning with the
    /// rest - is easy to mistake for a culling fault.
    /// </summary>
    public sealed class SceneSurvey
    {
        private readonly ManualLogSource _logger;
        private bool _written;

        public SceneSurvey(ManualLogSource logger)
        {
            _logger = logger;
        }

        /// <summary>Re-arms the survey, so a fresh world writes a fresh one.</summary>
        public void Rearm()
        {
            _written = false;
        }

        public void WriteOnce()
        {
            if (_written || !GameReflection.Resolved) return;

            Camera main = GameReflection.MainCamera;
            if (main == null) return;

            _written = true;

            _logger.LogInfo("SURVEY main camera: " + Describe(main));

            Camera[] additional = GameReflection.AdditionalCameras;
            if (additional == null)
            {
                _logger.LogInfo("SURVEY GameCamera.additionalCameras is unreadable on this build.");
            }
            else
            {
                for (int i = 0; i < additional.Length; i++)
                    _logger.LogInfo("SURVEY additional camera " + i + ": " + Describe(additional[i]));
            }

            Camera[] all = Camera.allCameras;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == main) continue;
                _logger.LogInfo("SURVEY enabled camera: " + Describe(all[i]));
            }

            Transform aim = GameReflection.AimTransform;
            _logger.LogInfo("SURVEY aim transform: "
                            + (aim == null ? "null"
                                : aim.name + " sameObjectAsCamera=" + (aim == main.transform)
                                  + " parents=" + DescribeParents(aim)
                                  + " mainIsInRig=" + main.transform.IsChildOf(aim)));

            _logger.LogInfo("SURVEY physics layers: " + LayerMaskResolver.DescribeAllLayers());

            GameObject prompt = GameReflection.InteractableWindowObject;
            _logger.LogInfo("SURVEY interaction prompt: "
                            + (prompt == null ? "not available" : DescribePrompt(prompt)));
        }

        private static string Describe(Camera cam)
        {
            if (cam == null) return "null";

            var sb = new StringBuilder();
            sb.Append(cam.name)
              .Append(" enabled=").Append(cam.isActiveAndEnabled)
              .Append(" ortho=").Append(cam.orthographic)
              .Append(" pos=").Append(cam.transform.position)
              .Append(" parents=").Append(DescribeParents(cam.transform))
              .Append(" depth=").Append(cam.depth.ToString("F1"))
              .Append(" clear=").Append(cam.clearFlags)
              .Append(" fov=").Append(cam.fieldOfView.ToString("F1"))
              .Append(" near=").Append(cam.nearClipPlane.ToString("F3"))
              .Append(" far=").Append(cam.farClipPlane.ToString("F1"))
              .Append(" mask=0x").Append(cam.cullingMask.ToString("X8"))
              .Append(" layers[").Append(LayerMaskResolver.Describe(cam.cullingMask)).Append(']')
              .Append(" rt=").Append(cam.targetTexture != null);
            return sb.ToString();
        }

        /// <summary>
        /// The transform's parent chain, root last. The mirror's test for "does this
        /// camera render the player's view" is membership of the aim transform's
        /// subtree, so what is in that subtree has to be readable from the log rather
        /// than assumed.
        /// </summary>
        private static string DescribeParents(Transform transform)
        {
            var sb = new StringBuilder();
            for (Transform t = transform.parent; t != null; t = t.parent)
            {
                if (sb.Length > 0) sb.Append('<');
                sb.Append(t.name);
            }
            return sb.Length == 0 ? "(root)" : sb.ToString();
        }

        private static string DescribePrompt(GameObject prompt)
        {
            var rect = prompt.GetComponent<RectTransform>();
            var canvas = prompt.GetComponentInParent<Canvas>();

            var sb = new StringBuilder();
            sb.Append(prompt.name);
            if (rect == null)
            {
                sb.Append(" (no RectTransform - the prompt cannot be moved)");
                return sb.ToString();
            }

            sb.Append(" anchored=").Append(rect.anchoredPosition)
              .Append(" anchors=").Append(rect.anchorMin).Append("..").Append(rect.anchorMax)
              .Append(" size=").Append(rect.sizeDelta);
            if (canvas == null)
            {
                sb.Append(" (no parent Canvas)");
                return sb.ToString();
            }

            var canvasRect = canvas.GetComponent<RectTransform>();
            sb.Append(" canvas=").Append(canvas.name)
              .Append(" mode=").Append(canvas.renderMode)
              .Append(" scale=").Append(canvas.scaleFactor.ToString("F3"));
            if (canvasRect != null)
                sb.Append(" canvasRect=").Append(canvasRect.rect.width.ToString("F0"))
                  .Append('x').Append(canvasRect.rect.height.ToString("F0"));
            return sb.ToString();
        }
    }
}
