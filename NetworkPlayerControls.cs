using System.Collections.Generic;
using System.Linq;
using TouchControlsKit;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
internal class NetworkPlayerControls : NetworkBehaviour
{
    [SerializeField] internal TCKTouchpad touchpad;
    [SerializeField] private FixedJoystick joystick;
    [SerializeField] private CharacterController controller;
    private static OrbitCamera orbitCamera;
    private static bool isRunning;
    private static bool canControl;
    private static float playerVelocityY;
    private static Vector3 moveDirection;
    private static float horizontal, vertical, speed;
    private int tick = 0;
    private float tickRate = 1f / 60f;
    private float tickDeltaTime = 0;
    private const int buffer = 1024;
    private InputState[] _inputStates = new InputState[buffer];
    private TransformState[] _transformStates = new TransformState[buffer];
    internal NetworkVariable<TransformState> currentServerTransformState = new NetworkVariable<TransformState>();
    internal TransformState previousTransformState;
    private int _lastProcessedTick = 0;
    internal static NetworkPlayerControls singleton;

    private void OnServerStateChanged(TransformState previousvalue, TransformState serverState)
    {
        if(!IsLocalPlayer) return;

        if (previousTransformState == null)
        {
            previousTransformState = serverState;
        }

        TransformState calculatedState = _transformStates.FirstOrDefault(localState => localState.Tick == serverState.Tick);

        if (calculatedState.Position != serverState.Position)
        {
            TeleportPlayer(serverState);
            IEnumerable<InputState> inputs = _inputStates.Where(input => input != null && input.Tick > serverState.Tick).OrderBy(input => input.Tick);
            inputs = from input in inputs orderby input.Tick select input;
            
            foreach (InputState inputState in inputs)
            {
                Move(inputState.MovementInput, isRunning);

                TransformState newTransformState = new TransformState()
                {
                    Tick = inputState.Tick,
                    Position = transform.position,
                    Rotation = transform.rotation,
                    HasStartedMoving = true
                };

                for (int i = 0; i < _transformStates.Length; i++)
                {
                    if (_transformStates[i].Tick == inputState.Tick)
                    {
                        _transformStates[i] = newTransformState;
                        break;
                    }
                }
            }
        }
    }

    private void OnEnable()
    {
        currentServerTransformState.OnValueChanged += OnServerStateChanged;
    }

    private void TeleportPlayer(TransformState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        Physics.SyncTransforms();

        for (int i = 0; i < _transformStates.Length; i++)
        {
            if (_transformStates[i].Tick == state.Tick)
            {
                _transformStates[i] = state;
                break;
            }
        }
    }

    private void ProcessLocalPlayerMovement(Vector3 _direction)
    {
        tickDeltaTime += Time.deltaTime;

        if (tickDeltaTime > tickRate)
        {
            int bufferIndex = tick % buffer;

            if (!IsServer)
            {
                MovePlayerWithServerTickServerRpc(tick, _direction, isRunning);
                Move(_direction, isRunning);
                SaveState(_direction, isRunning, bufferIndex);
            }

            else

            {
                Move(_direction, isRunning);
                TransformState state = new TransformState()
                {
                    Tick = tick,
                    Position = transform.position,
                    Rotation = transform.rotation,
                    HasStartedMoving = true
                };
                SaveState(_direction, isRunning, bufferIndex);
                previousTransformState = currentServerTransformState.Value;
                currentServerTransformState.Value = state;
            }

            tickDeltaTime -= tickRate;
            tick++;
        }
    }

    [ServerRpc]
    private void MovePlayerWithServerTickServerRpc(int tick, Vector3 moveDirection, bool _isRunning)
    {
        _lastProcessedTick = tick;
        Move(moveDirection, _isRunning);
        transform.forward = moveDirection;
        
        TransformState transformState = new()
        {
            Tick = tick,
            Position = transform.position,
            Rotation = transform.rotation,
            HasStartedMoving = true
        };

        previousTransformState = currentServerTransformState.Value;
        currentServerTransformState.Value = transformState;
    }

    private void SimulateOtherPlayers()
    {
        tickDeltaTime += Time.deltaTime;

        if (tickDeltaTime > tickRate)
        {
            if (currentServerTransformState.Value != null)
            {
                if (currentServerTransformState.Value.HasStartedMoving)
                {
                    transform.position = currentServerTransformState.Value.Position;
                    transform.rotation = currentServerTransformState.Value.Rotation;
                }
            }

            tickDeltaTime -= tickRate;
            tick++;
        }
    }

    private void SaveState(Vector2 movementInput, bool _isRunning, int bufferIndex)
    {
        InputState inputState = new InputState()
        {
            Tick = tick,
            MovementInput = movementInput,
            Running = _isRunning
        };

        TransformState transformState = new TransformState()
        {
            Tick = tick,
            Position = transform.position,
            Rotation = transform.rotation,
            HasStartedMoving = true
        };

        _inputStates[bufferIndex] = inputState;
        _transformStates[bufferIndex] = transformState;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            singleton = this;
            orbitCamera = OrbitCamera.singleton;
            orbitCamera.SetupCam(transform, touchpad);
            PlayerSpeedManagerServer._singleton.playerCanControl += CanControlFunction;
            canControl = PlayerSpeedManagerServer._singleton.isControlling;
        }
    }

    private void CanControlFunction(bool value)
    {
        canControl = value;
        orbitCamera.InitializeCursor(value);
    }


    private void Update()
    {
        if (IsClient && IsLocalPlayer)
        {
            if (!canControl) return;

            horizontal = !Application.isMobilePlatform ? Input.GetAxis("Horizontal") : joystick.Horizontal;
            vertical = !Application.isMobilePlatform ? Input.GetAxis("Vertical") : joystick.Vertical;
            moveDirection = (orbitCamera.transform.forward * vertical + orbitCamera.transform.right * horizontal).normalized;
            isRunning = Input.GetKey(KeyCode.LeftShift);
            playerVelocityY += PlayerSpeedManagerServer._singleton.GetServerGravity() * tickRate;
        
            if (controller.isGrounded && playerVelocityY < 0)
            {
                playerVelocityY = 0;
            }

            moveDirection.y = playerVelocityY;

            if (moveDirection != Vector3.zero)
            {
                //transform.forward = moveDirection;
                ProcessLocalPlayerMovement(moveDirection);
            }

            if (Input.GetButton("Jump"))
            {
                JumpServerRpc();
            }
        }

        else
        {
            SimulateOtherPlayers();
        }
    }

    private void Move(Vector3 direction, bool _isRunning)
    {
        speed = _isRunning ? PlayerSpeedManagerServer._singleton.GetServerRunSpeed() : PlayerSpeedManagerServer._singleton.GetServerWalkSpeed();

        transform.forward = moveDirection;
        controller.Move(direction * speed * tickRate);
    }

    private void PerformJump()
    {
        playerVelocityY = PlayerSpeedManagerServer._singleton.GetServerJumpHeight();
    }

    [ServerRpc]
    private void JumpServerRpc(ServerRpcParams rpcParams = default)
    {
        if (controller.isGrounded && PlayerSpeedManagerServer._singleton.GetCanJumpBool())
        {
            PerformJump();
            JumpClientRpc(OwnerClientId);
        }
    }

    [ClientRpc]
    private void JumpClientRpc(ulong clientId, ClientRpcParams rpcParams = default)
    {
        rpcParams.Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } };

        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            PerformJump();
        }
    }
}
