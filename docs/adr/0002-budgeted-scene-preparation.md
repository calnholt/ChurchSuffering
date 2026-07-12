# Budget scene preparation before activation

Scene transitions previously destroyed the active scene and synchronously published
`LoadSceneEvent` from one `TransitionDisplaySystem.Update` call. The performance
report recorded this scope at 268.58 ms, with additional cold-load spikes from
music and first-use rendering.

**Decision:** scene changes use a preparation identifier and explicit lifecycle:
request, prepare, deactivate, activate, complete. `SceneLoadingCoordinatorSystem`
warms a contextual manifest on covered transition frames with a 6 ms work budget.
The transition remains fully covered until required preparation reports ready.
Legacy `DeleteCachesEvent` and `LoadSceneEvent` remain as an activation bridge while
subscribers migrate to the explicit lifecycle events.

Large destination textures use preparation-scoped `ContentManager` instances.
Active and prepared scopes are pinned; unpinned scopes are evicted whole using a
256 MiB decoded-size LRU budget. Shared UI assets remain in the application content
cache. ECS teardown uses a bulk entity/index pass, and scene system bundles are
constructed once, then activated or deactivated instead of recreated.

Graphics and `ContentManager` access remain on the game thread. Any future
background preparation is restricted to immutable CPU-only data and must return a
main-thread finalization job.
