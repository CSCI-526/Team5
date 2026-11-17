# Level3 / Level4 bounded squad camera

Open `Scenes/Level3GridBattle.unity` and enter Play Mode. The existing perspective camera smoothly zooms in by approximately 1.6x and follows the squad's simulated anchor. Rotation, battle movement, map content and the authored camera settings are preserved.

The initial camera frustum is projected onto the route's ground plane. The entire new viewport footprint must remain within this original quadrilateral. Near an edge the camera stops panning rather than revealing additional space; the squad can move away from the centre of the screen there. The constraint also applies during unit/squad inspection and changes of viewport aspect ratio.

During siege ram route planning the original overview is restored so the support start, gate and route remain accessible. The camera stays still during planning; on completion or cancellation it resumes the zoomed squad follow. The existing inspection controller keeps control while selecting a unit or formation, and the follow resumes after its restoration completes. Normal follow updates before health-bar projection.

Select `Battle Camera (Perspective 16x9)` and adjust `Level3 Squad Camera`:

- **Zoom Factor**: default 1.6; larger values show a closer view.
- **Follow Sharpness**: default 6; larger values follow more quickly.
- Disable the component to restore the authored overview.

The runtime-only camera component is attached only to Level3 and Level4. Other scenes and the authored camera Transform/Camera values are unchanged. `Editor/Level3CameraBuilder.cs` can reattach it through `Team5 > Rebuild > Level 3 > Enable bounded squad camera`.

Unity 6000.3.22f1 compilation and 4/4 Play Mode tests passed. Camera coverage includes actual squad movement, zoom, all four boundary directions, a wider viewport, inspection/restore, planning overview and return to follow; existing Level3 and Scene1 siege-ram flows also pass. Tests disable enemy range only in memory to isolate camera behaviour. This does not validate level difficulty or a complete playthrough. Rendered previews and results are in `Logs/Level3FollowCamera/` (`StartZoom.png`, `FollowSquad.png`, `PlanningOverview.png`, `FinalPlayMode.xml`). No desktop GUI automation was used.

## Level4

`Scenes/Level4GridBattle.unity` uses the same 1.6x zoom and smooth follow, constrained by its own authored camera overview. The source component retains its historical `Level3SquadCamera` name; scenes opt in by attaching and binding it. Level4 map, route, roster, enemy settings and camera Transform/FOV are preserved. No siege-ram system is added to Level4. Reattach via `Team5 > Rebuild > Level 4 > Enable bounded squad camera`. Verification artifacts are written to `Logs/Level4FollowCamera/`.

Level4 integration verified in Unity 6000.3.22f1: both Level3 and Level4 camera Play Mode tests passed (`Logs/Level4FollowCamera/PlayMode.xml`); the final saved Level4 scene also passed its follow/zoom/bounds/inspection regression (`FinalLevel4.xml`). Source-scene record comparison confirms that only the camera component attachment and one new component record were added. Rendered Level4 previews: `StartZoom.png` and `FollowSquad.png`.
