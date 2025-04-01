using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using GameKit.Dependencies.Utilities;
using UnityEngine;

public struct ReplicateData : IReplicateData
{
    public OneTimeInput OneTimeInput;

    public Vector2 MovementNorm;

    public ReplicateData(OneTimeInput oneTimeInput, Vector2 movementNorm)
        : this()
    {
        OneTimeInput = oneTimeInput;
        MovementNorm = movementNorm;
    }

    private uint _tick;

    public void Dispose() { }

    public uint GetTick() => _tick;

    public void SetTick(uint value) => _tick = value;
}

public struct ReconcileData : IReconcileData
{
    //PredictionRigidbody is used to synchronize rigidbody states
    //and forces. This could be done manually but the PredictionRigidbody
    //type makes this process considerably easier. Velocities, kinematic state,
    //transform properties, pending velocities and more are automatically
    //handled with PredictionRigidbody.
    public PredictionRigidbody PredictionRigidbody;

    public ReconcileData(PredictionRigidbody pr)
        : this()
    {
        PredictionRigidbody = pr;
    }

    private uint _tick;

    public void Dispose() { }

    public uint GetTick() => _tick;

    public void SetTick(uint value) => _tick = value;
}

public struct OneTimeInput
{
    public bool Jump;
    public Vector3 ExternalForce;

    /// <summary>
    /// Unset inputs.
    /// </summary>
    public void ResetState()
    {
        Jump = false;
        ExternalForce = Vector3.zero;
    }

    public void ResetExternalForce()
    {
        ExternalForce = Vector3.zero;
    }
}

public class PlayerMovement : TickNetworkBehaviour
{
    [SerializeField]
    private float _jumpForce = 150f;

    [SerializeField]
    private float _moveSpeed = 0.8f;

    [SerializeField]
    private float _runSpeed = 12f;

    [SerializeField]
    private float _playerHeight = 3f;

    public PredictionRigidbody PredictionRigidbody;

    public OneTimeInput oneTimeInputs = new();

    private void Awake()
    {
        PredictionRigidbody = ObjectCaches<PredictionRigidbody>.Retrieve();
        PredictionRigidbody.Initialize(GetComponent<Rigidbody>());
    }

    private void OnDestroy()
    {
        ObjectCaches<PredictionRigidbody>.StoreAndDefault(ref PredictionRigidbody);
    }

    public override void OnStartNetwork()
    {
        base.TimeManager.OnTick += TimeManager_OnTick;
        base.TimeManager.OnPostTick += TimeManager_OnPostTick;
    }

    public override void OnStopNetwork()
    {
        base.TimeManager.OnTick -= TimeManager_OnTick;
        base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
    }

    private void TimeManager_OnTick()
    {
        RunInputs(CreateReplicateData());
    }

    private void TimeManager_OnPostTick()
    {
        CreateReconcile();
    }

    private void Update()
    {
        HandleJump();
    }

    private void HandleJump()
    {
        if (base.IsOwner)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                oneTimeInputs.Jump = true;
            }
        }
    }

    private ReplicateData CreateReplicateData()
    {
        if (!base.IsOwner)
            return default;

        Vector2 movementNorm = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        ReplicateData md = new ReplicateData(oneTimeInputs, movementNorm);
        oneTimeInputs.ResetState();

        return md;
    }

    [Replicate]
    private void RunInputs(
        ReplicateData data,
        ReplicateState state = ReplicateState.Invalid,
        Channel channel = Channel.Unreliable
    )
    {
        Vector3 movementDirection = new Vector3(data.MovementNorm.x, 0f, data.MovementNorm.y);

        Vector3 forces = movementDirection.normalized * 1.3f;

        PredictionRigidbody.AddForce(forces, ForceMode.VelocityChange);
        PredictionRigidbody.AddForce(Physics.gravity * 7f);
        if (data.OneTimeInput.Jump)
        {
            Vector3 jmpFrc = (transform.up + movementDirection.normalized) * 150f;

            PredictionRigidbody.AddForce(jmpFrc, ForceMode.Impulse);
        }
        PredictionRigidbody.Simulate();
    }

    public override void CreateReconcile()
    {
        ReconcileData rd = new ReconcileData(PredictionRigidbody);

        ReconcileState(rd);
    }

    [Reconcile]
    private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
    {
        PredictionRigidbody.Reconcile(data.PredictionRigidbody);
    }
}
