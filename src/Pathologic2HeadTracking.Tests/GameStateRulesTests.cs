using Pathologic2HeadTracking.Game;
using Xunit;

namespace Pathologic2HeadTracking.Tests
{
    public class GameStateRulesTests
    {
        [Fact]
        public void TrackingAppliesInGameplayOnly()
        {
            Assert.True(GameStateRules.AllowsTracking(GameState.Gameplay));
            Assert.False(GameStateRules.AllowsTracking(GameState.Menu));
            Assert.False(GameStateRules.AllowsTracking(GameState.Loading));
            Assert.False(GameStateRules.AllowsTracking(GameState.Cutscene));
        }

        [Fact]
        public void OnlyTheTwoControllingKindsArePlayerDriven()
        {
            Assert.True(GameStateRules.IsPlayerControlled(CameraKind.FirstPerson_Controlling));
            Assert.True(GameStateRules.IsPlayerControlled(CameraKind.FirstPerson_Controlling2));

            Assert.False(GameStateRules.IsPlayerControlled(CameraKind.Unknown));
            Assert.False(GameStateRules.IsPlayerControlled(CameraKind.Fly));
            Assert.False(GameStateRules.IsPlayerControlled(CameraKind.FirstPerson_Tracking));
            Assert.False(GameStateRules.IsPlayerControlled(CameraKind.Cutscene));
            Assert.False(GameStateRules.IsPlayerControlled(CameraKind.Cutscene_Cinemachine));
            Assert.False(GameStateRules.IsPlayerControlled(CameraKind.Ragdoll));
            Assert.False(GameStateRules.IsPlayerControlled(CameraKind.Dialog));
            Assert.False(GameStateRules.IsPlayerControlled(CameraKind.Trade));
        }

        /// <summary>
        /// The enum is read out of CameraService's backing field as an int, so its
        /// member ORDER is the contract with the game rather than its member names.
        /// A reorder here would silently re-point every state test at the wrong
        /// controller, which is the sort of fault that only shows up as "tracking
        /// keeps running through dialogue".
        /// </summary>
        [Fact]
        public void CameraKindOrdinalsMatchTheGamesEnum()
        {
            Assert.Equal(0, (int)CameraKind.Unknown);
            Assert.Equal(1, (int)CameraKind.Fly);
            Assert.Equal(2, (int)CameraKind.FirstPerson_Controlling);
            Assert.Equal(3, (int)CameraKind.FirstPerson_Tracking);
            Assert.Equal(4, (int)CameraKind.FirstPerson_Controlling_Fight);
            Assert.Equal(5, (int)CameraKind.Cutscene);
            Assert.Equal(6, (int)CameraKind.Cutscene_Cinemachine);
            Assert.Equal(7, (int)CameraKind.Ragdoll);
            Assert.Equal(8, (int)CameraKind.Dialog);
            Assert.Equal(9, (int)CameraKind.Crazy);
            Assert.Equal(10, (int)CameraKind.Trade);
            Assert.Equal(11, (int)CameraKind.FirstPerson_Controlling2);
        }
    }
}
