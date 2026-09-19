# Changelog

## [0.0.0] - 2026-09-17

### Added
- Initial release. 6DOF head tracking for Pathologic 2 over the OpenTrack UDP
  protocol: your head moves the view while the mouse or controller keeps aiming.
- Added decoupled look and aim: weapons and interactions follow the mouse or
  controller, not the head-tracked view.
- Added positional lean, peek and duck alongside yaw, pitch and roll.
- Added an interaction prompt that follows your aim, so it sits on whatever the
  aim line points at rather than under wherever your head is looking.
