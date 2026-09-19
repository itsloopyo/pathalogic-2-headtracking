namespace Pathologic2HeadTracking.Game
{
    /// <summary>
    /// Engine.Source.Services.CameraServices.CameraKindEnum, mirrored so the mod can
    /// name the state it is in without holding a reference to the game's assembly.
    /// The member ORDER is the contract - the value is read out of CameraService's
    /// backing field as an int - so nothing here may be reordered or inserted into.
    /// </summary>
    public enum CameraKind
    {
        Unknown,
        Fly,
        FirstPerson_Controlling,
        FirstPerson_Tracking,
        FirstPerson_Controlling_Fight,
        Cutscene,
        Cutscene_Cinemachine,
        Ragdoll,
        Dialog,
        Crazy,
        Trade,
        FirstPerson_Controlling2
    }

    public enum GameState
    {
        /// <summary>No world up: the title screen, a profile, a save slot, a level load.</summary>
        Loading,

        /// <summary>Free-look gameplay with the HUD up and the mouse captured.</summary>
        Gameplay,

        /// <summary>Inventory, map, mind map, trade, lock picking, sleep, the pause menu.</summary>
        Menu,

        /// <summary>A scripted sequence, a dialogue rig or a ragdoll death owns the camera.</summary>
        Cutscene
    }

    public static class GameStateRules
    {
        /// <summary>
        /// Gameplay only. A cutscene, a dialogue and a death are all animating the
        /// camera to a composition of their own, and a head that moves it off that
        /// composition is the player fighting the director.
        /// </summary>
        public static bool AllowsTracking(GameState state)
        {
            return state == GameState.Gameplay;
        }

        /// <summary>
        /// The two kinds that mean the player is driving the camera themselves. The
        /// game's own PlayerUtility.IsPlayerCanControlling tests this same pair.
        /// </summary>
        public static bool IsPlayerControlled(CameraKind kind)
        {
            return kind == CameraKind.FirstPerson_Controlling
                   || kind == CameraKind.FirstPerson_Controlling2;
        }
    }
}
