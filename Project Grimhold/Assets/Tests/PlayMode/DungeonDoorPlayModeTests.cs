#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using ProjectGrimhold.Gameplay.Visibility;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Scenario
{
    /// <summary>Exercises the real door prefab on State Authority without loading Gameplay.</summary>
    public sealed class DungeonDoorPlayModeTests
    {
        private const string DoorPrefabGuid = "e6dd992b08015f64db6dc289448875ce";
        private const float TransitionSeconds = 0.4f;
        private const float CrossingDistance = 4f;
        private static readonly Vector3 DoorPosition = new Vector3(20f, 20f, 0f);
        private static readonly EntityId InteractorId = new EntityId(900001);

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
        private NetworkRunner _runner;
        private ExtractionProgressSimulationDriver _driver;
        private NetworkDoor _door;
        private IInteractable _interactable;
        private Collider2D _blocker;
        private BoxCollider2D _character;
        private DungeonDoorTestInteractor _interactor;
        private Bounds _doorwayBounds;
        private ContactFilter2D _worldFilter;
        private PathfindingGrid _grid;
        private PathfindingGridConfig _gridConfig;
        private Tile _floorTile;
        private int _transitionDeadlineTick;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _blocker = null;
            _door = null;
            _interactable = null;
            _character = null;
            var holder = new GameObject(nameof(DungeonDoorPlayModeTests));
            holder.AddComponent<AudioListener>();
            _runner = holder.AddComponent<NetworkRunner>();
            holder.AddComponent<EntityRegistry>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            _driver = holder.AddComponent<ExtractionProgressSimulationDriver>();
            _worldFilter = new ContactFilter2D { useTriggers = false };
            _worldFilter.SetLayerMask(LayerMask.GetMask("WorldCollision"));

            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"dungeon-door-{Guid.NewGuid():N}",
                SceneManager = holder.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = holder.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.IsFaulted, Is.False, start.Exception?.ToString());
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
                if (_runner != null) Object.DestroyImmediate(_runner.gameObject);
            }

            if (_gridConfig != null) Object.DestroyImmediate(_gridConfig);
            if (_floorTile != null) Object.DestroyImmediate(_floorTile);
            _runner = null;
            _grid = null;
            _gridConfig = null;
            _floorTile = null;
        }

        [UnityTest]
        public IEnumerator Closed_BlocksCharacterProjectileAndVision_OffersOpenPrompt()
        {
            SpawnDoor();

            yield return Execute(() =>
            {
                AssertDoorState(DungeonDoorState.Closed, blocksPassage: true, "Abrir puerta");
                Assert.That(_interactable.CanInteract(Request()), Is.True);
                Assert.That(_door.TransitionTimer.IsRunning, Is.False);
            });
        }

        [UnityTest]
        public IEnumerator Opening_IgnoresRepeatedPresses_UntilTimedOpeningClearsAllOcclusion()
        {
            SpawnDoor();
            yield return BeginOpening();
            TickTimer deadline = _door.TransitionTimer;

            yield return Execute(() => AssertRepeatedPressesIgnored(DungeonDoorState.Opening, deadline));
            yield return WaitForStableState(DungeonDoorState.Open);

            AssertDoorState(DungeonDoorState.Open, blocksPassage: false, "Cerrar puerta");
            Assert.That(_interactable.CanInteract(Request()), Is.True);
            Assert.That(_door.TransitionTimer.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator Closing_RestoresBlockerImmediately_IgnoresRepeatedPresses_ThenOffersOpenPrompt()
        {
            SpawnDoor();
            yield return OpenDoor();
            TickTimer deadline = default;
            yield return Execute(() =>
            {
                Assert.That(_interactable.CanInteract(Request()), Is.True);
                AssertSucceeded(_interactable.Interact(Request()));
                AssertDoorState(DungeonDoorState.Closing, blocksPassage: true, prompt: null);
                AssertTransitionDuration();
                deadline = _door.TransitionTimer;
                AssertRepeatedPressesIgnored(DungeonDoorState.Closing, deadline);
            });
            yield return Execute(() => AssertRepeatedPressesIgnored(DungeonDoorState.Closing, deadline));
            yield return WaitForStableState(DungeonDoorState.Closed);

            AssertDoorState(DungeonDoorState.Closed, blocksPassage: true, "Abrir puerta");
            Assert.That(_interactable.CanInteract(Request()), Is.True);
            Assert.That(_door.TransitionTimer.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator Open_WithCharacterInDoorway_RejectsClosingWithoutMutation_UntilCharacterLeaves()
        {
            SpawnDoor();
            yield return OpenDoor();
            BoxCollider2D occupant = CreateCharacter("DoorwayOccupant", _doorwayBounds.center);
            Physics2D.SyncTransforms();
            Assert.That(occupant.isTrigger, Is.False);
            Assert.That(occupant.bounds.Intersects(_doorwayBounds), Is.True);

            yield return Execute(() =>
            {
                TickTimer before = _door.TransitionTimer;
                for (int press = 0; press < 3; press++)
                {
                    AssertRejected(_interactable.Interact(Request()));
                    AssertDoorState(DungeonDoorState.Open, blocksPassage: false, "Cerrar puerta");
                    Assert.That(_door.TransitionTimer, Is.EqualTo(before), "Rejected closing cannot start a timer.");
                    Assert.That(_door.TransitionTimer.IsRunning, Is.False);
                }
            });
            // Rejection must also survive a later authoritative tick, not just the call itself.
            yield return Execute(() => AssertDoorState(DungeonDoorState.Open, blocksPassage: false, "Cerrar puerta"));

            occupant.transform.position = DoorPosition + Vector3.right * 5f;
            Physics2D.SyncTransforms();
            Assert.That(occupant.bounds.Intersects(_doorwayBounds), Is.False);
            yield return Execute(() =>
            {
                Assert.That(_interactable.CanInteract(Request()), Is.True);
                AssertSucceeded(_interactable.Interact(Request()));
                AssertDoorState(DungeonDoorState.Closing, blocksPassage: true, prompt: null);
                AssertTransitionDuration();
            });
            yield return WaitForStableState(DungeonDoorState.Closed);
            AssertDoorState(DungeonDoorState.Closed, blocksPassage: true, "Abrir puerta");
            Assert.That(_door.TransitionTimer.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator BuiltGrid_SpawnedClosedDoorRefreshesInitiallyWalkableDoorwayNode()
        {
            BuildGrid();
            Vector2 doorwayPoint = DoorPosition + Vector3.up * 0.125f;
            AssertWalkableAt(doorwayPoint, expected: true);
            SpawnDoor();
            AssertDoorwayContainsNode(doorwayPoint);
            yield return Execute(() =>
            {
                AssertDoorState(DungeonDoorState.Closed, blocksPassage: true, "Abrir puerta");
                AssertWalkableAt(doorwayPoint, expected: false);
            });
        }

        [UnityTest]
        public IEnumerator BuiltGrid_DoorRefreshesDoorwayNode_OnOpenAndImmediateClosing()
        {
            BuildGrid();
            SpawnDoor();
            // Bootstrap may build after doors spawn. NetworkDoor already resolved this grid;
            // build from the synchronized closed collider, independently of spawn-time refresh.
            _grid.Build();
            Vector2 doorwayPoint = _doorwayBounds.center;
            AssertDoorwayContainsNode(doorwayPoint);
            AssertWalkableAt(doorwayPoint, expected: false);

            yield return OpenDoor();
            AssertWalkableAt(doorwayPoint, expected: true);
            yield return Execute(() =>
            {
                AssertSucceeded(_interactable.Interact(Request()));
                AssertDoorState(DungeonDoorState.Closing, blocksPassage: true, prompt: null);
                AssertTransitionDuration();
                AssertWalkableAt(doorwayPoint, expected: false);
            });
            yield return WaitForStableState(DungeonDoorState.Closed);
            AssertDoorState(DungeonDoorState.Closed, blocksPassage: true, "Abrir puerta");
            AssertWalkableAt(doorwayPoint, expected: false);
        }

        [UnityTest]
        public IEnumerator NorthInteractor_OpensSouth_KeepsDirectionThroughOpenAndClosing()
        {
            yield return AssertDirectionalCycle(1f, DungeonDoorOpenDirection.South);
        }

        [UnityTest]
        public IEnumerator SouthInteractor_OpensNorth_KeepsDirectionThroughOpenAndClosing()
        {
            yield return AssertDirectionalCycle(-1f, DungeonDoorOpenDirection.North);
        }

        [UnityTest]
        public IEnumerator NorthCharacterBody_StopsAtWallDepthWhenClosed_AndPassesWhenOpen()
        {
            SpawnDoor();
            BoxCollider2D northBody = CreateCharacter("NorthBody", DoorPosition + Vector3.up * 2f);
            northBody.offset = new Vector2(0f, 0.25f); // Player body spans root..root+0.5.
            Physics2D.SyncTransforms();
            int count = northBody.Cast(Vector2.down, _worldFilter, _hits, CrossingDistance);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(_hits[0].collider, Is.SameAs(_blocker));
            float stoppedBottom = northBody.bounds.min.y - _hits[0].distance;
            Assert.That(stoppedBottom, Is.InRange(DoorPosition.y + 1f, DoorPosition.y + 1.01f),
                "The Character body must stop at the north face of the adjacent wall depth.");
            Assert.That(_blocker.bounds.min.y, Is.EqualTo(DoorPosition.y).Within(0.001f));
            yield return OpenDoor();
            Assert.That(northBody.Cast(Vector2.down, _worldFilter, _hits, CrossingDistance), Is.Zero);
        }

        [UnityTest]
        public IEnumerator TransitionPresentation_RejoinsAtTimerProgress_AndCorrectsProxyDriftWithoutGameplayMutation()
        {
            SpawnDoor();
            yield return BeginOpening();
            yield return Execute(() => { });
            var presenter = _door.GetComponent<DungeonDoorPresenter>();
            Animator animator = _door.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null);
            TickTimer timer = _door.TransitionTimer;
            DungeonDoorState state = _door.State;
            DungeonDoorOpenDirection direction = _door.OpenDirection;
            // Re-adopting already replicated state models presentation's late join/restore path.
            presenter.Spawned();
            presenter.Render();
            animator.Update(0f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Door_Opening_North"), Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
                Is.EqualTo(_door.TransitionProgress).Within(0.02f));
            animator.Play("Door_Opening_North", 0, 0.99f);
            animator.Update(0f);
            presenter.Render();
            animator.Update(0f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
                Is.EqualTo(_door.TransitionProgress).Within(0.125f));
            Assert.That(_door.State, Is.EqualTo(state));
            Assert.That(_door.OpenDirection, Is.EqualTo(direction));
            Assert.That(_door.TransitionTimer, Is.EqualTo(timer));
            Assert.That(_blocker.enabled, Is.True);
            yield return WaitForStableState(DungeonDoorState.Open);
        }

        [UnityTest]
        public IEnumerator UnresolvedInteractor_RejectsOpeningWithoutMutation()
        {
            SpawnDoor();
            yield return Execute(() =>
            {
                var missing = new InteractionRequest(new EntityId(900002), _door.Id, _runner.Tick.Raw);
                TickTimer before = _door.TransitionTimer;
                DungeonDoorOpenDirection direction = _door.OpenDirection;
                for (int press = 0; press < 3; press++)
                {
                    InteractionResult result = _interactable.Interact(missing);
                    Assert.That(result.Success, Is.False);
                    Assert.That(result.IsConsumed, Is.False);
                    Assert.That(result.FailureReason, Is.EqualTo(InteractionFailureReason.InteractorUnavailable));
                    Assert.That(result.LootFailureReason, Is.EqualTo(LootTransferFailureReason.None));
                    AssertDoorState(DungeonDoorState.Closed, blocksPassage: true, "Abrir puerta");
                    Assert.That(_door.TransitionTimer, Is.EqualTo(before));
                    Assert.That(_door.OpenDirection, Is.EqualTo(direction));
                }
            });
            yield return Execute(() =>
            {
                AssertDoorState(DungeonDoorState.Closed, blocksPassage: true, "Abrir puerta");
                Assert.That(_door.TransitionTimer.IsRunning, Is.False);
                Assert.That(_door.OpenDirection, Is.EqualTo(DungeonDoorOpenDirection.North));
            });
        }

        private IEnumerator AssertDirectionalCycle(float side, DungeonDoorOpenDirection expected)
        {
            SpawnDoor();
            Assert.That(_door.OpenDirection, Is.EqualTo(DungeonDoorOpenDirection.North), "Fresh spawn default.");
            _interactor.transform.position = DoorPosition + Vector3.up * side;
            yield return BeginOpening();
            Assert.That(_door.OpenDirection, Is.EqualTo(expected));
            AssertAnimationState($"Door_Opening_{expected}");
            yield return Execute(() =>
            {
                AssertRepeatedPressesIgnored(DungeonDoorState.Opening, _door.TransitionTimer);
                Assert.That(_door.OpenDirection, Is.EqualTo(expected));
            });
            yield return WaitForStableState(DungeonDoorState.Open);
            Assert.That(_door.OpenDirection, Is.EqualTo(expected));
            AssertPresentation(expected, 0);
            // Closing from the opposite side must not choose a new direction.
            _interactor.transform.position = DoorPosition - Vector3.up * side;
            yield return Execute(() =>
            {
                AssertSucceeded(_interactable.Interact(Request()));
                AssertDoorState(DungeonDoorState.Closing, blocksPassage: true, prompt: null);
                AssertTransitionDuration();
                Assert.That(_door.OpenDirection, Is.EqualTo(expected));
                AssertRepeatedPressesIgnored(DungeonDoorState.Closing, _door.TransitionTimer);
            });
            AssertAnimationState($"Door_Closing_{expected}");
            yield return WaitForStableState(DungeonDoorState.Closed);
            Assert.That(_door.OpenDirection, Is.EqualTo(expected));
            AssertPresentation(expected, 3);
            // A new opening chooses again, rather than retaining the previous direction.
            yield return BeginOpening();
            Assert.That(_door.OpenDirection, Is.EqualTo(expected == DungeonDoorOpenDirection.North
                ? DungeonDoorOpenDirection.South : DungeonDoorOpenDirection.North));
        }

        private void AssertPresentation(DungeonDoorOpenDirection direction, int frame)
        {
            AssertAnimationState(frame == 3 ? "Door_Closed" : $"Door_Open_{direction}");
            string sheet = direction == DungeonDoorOpenDirection.South ? "Door-Open-South-Sheet" : "Door-Open-Sheet";
            if (frame == 3) sheet = "Door-Open-Sheet"; // One shared Closed idle.

            Assert.That(_door.GetComponentInChildren<SpriteRenderer>().sprite.name, Is.EqualTo($"{sheet}_{frame}"));
        }

        private void AssertAnimationState(string name)
        {
            _door.GetComponent<DungeonDoorPresenter>().Render();
            Animator animator = _door.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null, "The real prefab must use editable Animator sprite clips.");
            animator.Update(0f); // Evaluate the scheduled Play without advancing local presentation time.
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(name), Is.True, name);
        }

        private void SpawnDoor()
        {
            NetworkPrefabId id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(DoorPrefabGuid));
            Assert.That(id.IsValid, Is.True, "The real DungeonDoor prefab must be registered with Fusion.");
            NetworkObject prefab = _runner.Config.PrefabTable.Load(id, true);
            Assert.That(prefab, Is.Not.Null);
            NetworkObject spawned = _runner.Spawn(prefab, DoorPosition, Quaternion.identity);
            Assert.That(spawned, Is.Not.Null);
            _door = spawned.GetComponent<NetworkDoor>();
            Assert.That(_door, Is.Not.Null);
            Assert.That(_door.HasStateAuthority && _door.IsReady, Is.True);
            _interactable = _door;
            var actor = new GameObject("Interactor");
            actor.transform.SetParent(_runner.transform, false);
            actor.transform.position = DoorPosition - Vector3.up;
            _interactor = actor.AddComponent<DungeonDoorTestInteractor>();
            Assert.That(_runner.GetComponent<EntityRegistry>().TryRegisterEntity(InteractorId, _interactor, null), Is.True);

            foreach (Collider2D collider in spawned.GetComponentsInChildren<Collider2D>())
            {
                if (!collider.isTrigger && collider.gameObject.layer == LayerMask.NameToLayer("WorldCollision"))
                {
                    Assert.That(_blocker, Is.Null, "The prefab must have exactly one solid doorway blocker.");
                    _blocker = collider;
                }
            }
            Assert.That(_blocker, Is.Not.Null);
            Physics2D.SyncTransforms();
            _doorwayBounds = _blocker.bounds;
            _character = CreateCharacter("CrossingCharacter", (Vector2)_doorwayBounds.center - Vector2.up * 2f);
            Physics2D.SyncTransforms();
            Assert.That(Physics2D.GetIgnoreLayerCollision(_character.gameObject.layer, _blocker.gameObject.layer),
                Is.False, "Character and WorldCollision must physically collide.");
        }

        private IEnumerator BeginOpening()
        {
            yield return Execute(() =>
            {
                AssertDoorState(DungeonDoorState.Closed, blocksPassage: true, "Abrir puerta");
                Assert.That(_interactable.CanInteract(Request()), Is.True);
                AssertSucceeded(_interactable.Interact(Request()));
                AssertDoorState(DungeonDoorState.Opening, blocksPassage: true, prompt: null);
                AssertTransitionDuration();
                AssertRepeatedPressesIgnored(DungeonDoorState.Opening, _door.TransitionTimer);
            });
        }

        private IEnumerator OpenDoor()
        {
            yield return BeginOpening();
            yield return WaitForStableState(DungeonDoorState.Open);
            AssertDoorState(DungeonDoorState.Open, blocksPassage: false, "Cerrar puerta");
            Assert.That(_door.TransitionTimer.IsRunning, Is.False);
        }

        private void AssertRepeatedPressesIgnored(DungeonDoorState transition, TickTimer deadline)
        {
            for (int press = 0; press < 3; press++)
            {
                Assert.That(_interactable.CanInteract(Request()), Is.False);
                AssertRejected(_interactable.Interact(Request()));
                AssertDoorState(transition, blocksPassage: true, prompt: null);
                Assert.That(_door.TransitionTimer, Is.EqualTo(deadline), "Repeated presses must not restart the transition.");
            }
        }

        private void AssertDoorState(DungeonDoorState expected, bool blocksPassage, string prompt)
        {
            Assert.That(_door.State, Is.EqualTo(expected));
            Assert.That(_door.PromptText, Is.EqualTo(prompt));
            Assert.That(_blocker.enabled, Is.EqualTo(blocksPassage));
            Assert.That(_character.isTrigger, Is.False);
            Assert.That(_character.attachedRigidbody, Is.Not.Null);
            Physics2D.SyncTransforms();

            int movementHits = _character.Cast(Vector2.up, _worldFilter, _hits, CrossingDistance);
            Assert.That(movementHits > 0, Is.EqualTo(blocksPassage), "A solid Character body crosses only an open door.");
            if (blocksPassage)
            {
                Assert.That(_hits[0].collider, Is.SameAs(_blocker));
                Assert.That(_hits[0].distance, Is.GreaterThan(0f).And.LessThan(CrossingDistance));
            }

            Vector2 origin = _character.bounds.center;
            Vector2 target = origin + Vector2.up * CrossingDistance;
            int projectileHits = Physics2D.Linecast(origin, target, _worldFilter, _hits);
            Assert.That(projectileHits, Is.EqualTo(blocksPassage ? 1 : 0), "WorldCollision projectile linecast.");
            if (blocksPassage) Assert.That(_hits[0].collider, Is.SameAs(_blocker));

            var vision = new VisionQuery(origin, Vector2.up, 2f, VisionConeRules.CosineOfHalfAngle(90f),
                LayerMask.GetMask("WorldCollision"), _hits, _character.transform, VisibilityFadeTimings.Instant);
            Assert.That(vision.IsPointVisible(target, null, requiresCone: true), Is.EqualTo(!blocksPassage),
                "The room beyond the door must follow live vision occlusion.");
            float expectedVisionDistance = blocksPassage ? _doorwayBounds.min.y - origin.y : CrossingDistance;
            Assert.That(vision.CastOcclusion(Vector2.up, CrossingDistance), Is.EqualTo(expectedVisionDistance).Within(0.01f),
                "The fog ray must stop at the closed face or reveal the room beyond the open door.");
        }

        private void AssertTransitionDuration()
        {
            Assert.That(_door.TransitionTimer.IsRunning, Is.True);
            _transitionDeadlineTick = (int)_door.TransitionTimer.TargetTick;
            Assert.That(_door.TransitionTimer.RemainingTime(_runner),
                Is.EqualTo(TransitionSeconds).Within(_runner.DeltaTime + 0.001f));
        }

        private IEnumerator WaitForStableState(DungeonDoorState expected)
        {
            // Check on simulation ticks so slow Editor frames cannot hide an early completion.
            bool completed = false;
            float timeout = Time.realtimeSinceStartup + 10f;
            while (!completed && Time.realtimeSinceStartup < timeout)
            {
                yield return Execute(() =>
                {
                    if (_door.State != expected)
                    {
                        // The reusable driver can run before NetworkDoor on the deadline tick.
                        Assert.That(_runner.Tick.Raw, Is.LessThanOrEqualTo(_transitionDeadlineTick + 1),
                            "An expired transition must finish on the authoritative simulation tick.");
                        return;
                    }
                    Assert.That(_runner.Tick.Raw, Is.GreaterThanOrEqualTo(_transitionDeadlineTick),
                        "The door must not finish before its 0.4 second deadline.");
                    Assert.That(_door.TransitionTimer.IsRunning, Is.False);
                    completed = true;
                });
            }
            Assert.That(completed, Is.True, $"Door never reached {expected}.");
        }

        private InteractionRequest Request() => new InteractionRequest(InteractorId, _door.Id, _runner.Tick.Raw);

        private static void AssertSucceeded(InteractionResult result)
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsConsumed, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(InteractionFailureReason.None));
            Assert.That(result.LootFailureReason, Is.EqualTo(LootTransferFailureReason.None));
        }

        private static void AssertRejected(InteractionResult result)
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.IsConsumed, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(InteractionFailureReason.TargetUnavailable));
            Assert.That(result.LootFailureReason, Is.EqualTo(LootTransferFailureReason.None));
        }

        private BoxCollider2D CreateCharacter(string name, Vector2 position)
        {
            var body = new GameObject(name) { layer = LayerMask.NameToLayer("Character") };
            Assert.That(body.layer, Is.GreaterThanOrEqualTo(0));
            body.transform.SetParent(_runner.transform, false);
            body.transform.position = position;
            body.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            BoxCollider2D collider = body.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.5f, 0.5f);
            return collider;
        }

        private IEnumerator Execute(Action action)
        {
            _driver.ClearException();
            _driver.PendingAction = _ => action();
            float timeout = Time.realtimeSinceStartup + 10f;
            while (_driver.PendingAction != null && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(_driver.PendingAction, Is.Null, "No authoritative Fusion tick executed the operation.");
            Assert.That(_driver.LastException, Is.Null, _driver.LastException?.ToString());
        }

        private void BuildGrid()
        {
            _gridConfig = ScriptableObject.CreateInstance<PathfindingGridConfig>();
            SetField(_gridConfig, "_obstacleLayer", (LayerMask)LayerMask.GetMask("WorldCollision"));
            SetField(_gridConfig, "_agentRadius", 0.1f);
            var gridRoot = new GameObject("DoorNavigation", typeof(Grid));
            gridRoot.transform.SetParent(_runner.transform, false);
            gridRoot.transform.position = DoorPosition - new Vector3(2f, 2f, 0f);
            var floor = new GameObject("Floor", typeof(Tilemap));
            floor.transform.SetParent(gridRoot.transform, false);
            _floorTile = ScriptableObject.CreateInstance<Tile>();
            Tilemap tilemap = floor.GetComponent<Tilemap>();
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    tilemap.SetTile(new Vector3Int(x, y, 0), _floorTile);

            _grid = gridRoot.AddComponent<PathfindingGrid>();
            SetField(_grid, "_config", _gridConfig);
            Physics2D.SyncTransforms();
            _grid.Build();
            Assert.That(_grid.IsBuilt, Is.True);
        }

        private void AssertDoorwayContainsNode(Vector2 point)
        {
            Assert.That(_grid.TryGetNodeFromWorldPoint(point, out int x, out int y), Is.True);
            Assert.That(_doorwayBounds.Contains(_grid.GetWorldPosition(x, y)), Is.True,
                "The sampled node must actually be inside the prefab doorway.");
        }

        private void AssertWalkableAt(Vector2 point, bool expected)
        {
            Assert.That(_grid.TryGetNodeFromWorldPoint(point, out int x, out int y), Is.True);
            Assert.That(_grid.IsWalkable(x, y), Is.EqualTo(expected),
                "NetworkDoor must refresh the built grid itself; the test never calls RefreshRegion.");
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }

    public sealed class DungeonDoorTestInteractor : MonoBehaviour, IDamageable
    {
        public EntityId Id => new EntityId(900001);
        public bool CanReceiveDamage => false;
        public DamageResult ApplyDamage(in DamageRequest request) => default;
    }
}
#endif
