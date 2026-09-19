using CameraUnlock.Core.Unity.UI;
using Pathologic2HeadTracking.Game;
using UnityEngine;

namespace Pathologic2HeadTracking.Aim
{
    /// <summary>
    /// Moves Pathologic 2's own centre-screen interaction prompt onto the point the
    /// aim ray stops at.
    ///
    /// That prompt is this game's reticle. PickingService casts from the camera
    /// transform and PlayerInteractableComponent shows the prompt for whatever it
    /// found, so the icon has always marked where the ray points - it simply sat at
    /// the centre of the screen because the ray did. Once the head moves the view off
    /// the aim, a prompt left at the centre points at whatever the head happens to be
    /// looking at, which is usually not the door the player is about to open.
    ///
    /// The doctrine's rule is to move the game's own reticle wherever one can be
    /// reached rather than draw a second one over it, and this one can: it is a plain
    /// uGUI RectTransform reached through IHudWindow.InteractableInterface.
    ///
    /// Driven from Canvas.willRenderCanvases, which is where uGUI expects a HUD
    /// element to be repositioned. See <see cref="ReticlePlacer.LastPlacement"/> for
    /// why the placement it consumes is the previous frame's.
    /// </summary>
    public sealed class StockPrompt
    {
        private readonly ReticlePlacer _placer;
        private readonly AnchoredOffsetCompensator _compensator;

        public StockPrompt(ReticlePlacer placer)
        {
            _placer = placer;
            _compensator = new AnchoredOffsetCompensator(() => GameReflection.InteractableWindowObject);
        }

        /// <summary>Subscribe once, from the plugin's Awake.</summary>
        public void Enable()
        {
            Canvas.willRenderCanvases += OnWillRenderCanvases;
        }

        public void Disable()
        {
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
            _compensator.Restore();
        }

        /// <summary>
        /// Puts the prompt back at the centre. Called when tracking stops and on every
        /// state change out of gameplay, so a prompt is never left parked off centre in
        /// a menu the player then reads.
        /// </summary>
        public void Restore()
        {
            _compensator.Restore();
        }

        private void OnWillRenderCanvases()
        {
            if (!_placer.ShouldMoveStockPrompt)
            {
                _compensator.Restore();
                return;
            }

            _compensator.ApplyNdcOffset(_placer.LastPlacement.NdcOffset);
        }
    }
}
