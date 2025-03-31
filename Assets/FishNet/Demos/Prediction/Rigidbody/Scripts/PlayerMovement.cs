using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

public struct ReplicateData : IReplicateData
{
    public OneTimeInput OneTimeInput;

    public bool IsGrounded;
    public bool OnSlope;
    public Vector3 SlopeHitNormal;
    public Vector2 MovementNorm;
    public bool IsRunning;

    public ReplicateData(
        OneTimeInput oneTimeInput,
        Vector2 movementNorm,
        bool onSlope,
        Vector3 slopeHitNormal
    )
        : this()
    {
        OneTimeInput = oneTimeInput;
        MovementNorm = movementNorm;

        OnSlope = onSlope;
        SlopeHitNormal = slopeHitNormal;
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
    private float _jumpForce = 120f;

    [SerializeField]
    private float _moveSpeed = 0.8f;

    [SerializeField]
    private float _runSpeed = 12f;

    [SerializeField]
    private float _playerHeight = 3f;

    public PredictionRigidbody PredictionRigidbody = new();

    public OneTimeInput oneTimeInputs = new();

    int ignoreGroundCheckLayers;

    private float maxSlopeAngle = 45f;
    private RaycastHit _slopeHit;

    private void Awake()
    {
        PredictionRigidbody.Initialize(GetComponent<Rigidbody>());

        ignoreGroundCheckLayers = ~LayerMask.GetMask("Player");
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

    public override void OnStartNetwork()
    {
        //Rigidbodies need tick and postTick.
        base.SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
    }

    protected override void TimeManager_OnTick()
    {
        RunInputs(CreateReplicateData());
        CreateReconcile();
    }

    //protected override void TimeManager_OnPostTick()
    //{
    //    CreateReconcile();
    //}

    private void FixedUpdate()
    {
        GroundCheck();
    }

    private void Update()
    {
        HandleJump();
    }

    private ReplicateData CreateReplicateData()
    {
        if (!base.IsOwner)
            return default;

        Vector2 movementNorm = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        bool onSlope = SlopeCheck();

        ReplicateData md = new ReplicateData(
            oneTimeInputs,
            movementNorm,
            onSlope,
            _slopeHit.normal
        );
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

        Vector3 forces;
        float currentMaxSpeed = data.IsRunning ? 1.3f : 0.8f;
        if (data.OnSlope)
        {
            //Debug.Log("ON SLOPE");
            forces =
                GetSlopeMoveDirection(movementDirection.normalized, data.SlopeHitNormal)
                * currentMaxSpeed;
        }
        else
        {
            forces = movementDirection.normalized * currentMaxSpeed;
        }

        if (!data.IsGrounded)
        {
            PredictionRigidbody.AddForce(Physics.gravity * 20f);
        }

        PredictionRigidbody.AddForce(forces, ForceMode.VelocityChange);
        PredictionRigidbody.AddForce(Physics.gravity * 5f);
        if (data.OneTimeInput.ExternalForce != Vector3.zero)
        {
            PredictionRigidbody.AddForce(data.OneTimeInput.ExternalForce, ForceMode.Impulse);
        }
        Debug.Log($"_isGrounded 123aac {data.IsGrounded}");
        Debug.Log($"data.OneTimeInput.Jump 123aac {data.OneTimeInput.Jump}");
        if (data.OneTimeInput.Jump && GroundCheck())
        {
            //Vector3 jmpFrc = (transform.up + movementDirection.normalized) * _jumpForce;
            Vector3 jmpFrc = (transform.up + movementDirection.normalized) * 120f;
            //PredictionRigidbody.Velocity(Vector3.zero);
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

    RaycastHit[] raycastHits = new RaycastHit[10];

    bool GroundCheck()
    {
        int numberOfHits = Physics.SphereCastNonAlloc(
            PredictionRigidbody.Rigidbody.position,
            0.5f,
            transform.up * -1,
            raycastHits,
            1.8f,
            ignoreGroundCheckLayers
        );
        for (int i = 0; i < numberOfHits; i++)
        {
            if (raycastHits[i].transform.root == transform)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool SlopeCheck()
    {
        if (
            Physics.Raycast(
                PredictionRigidbody.Rigidbody.position,
                Vector3.down,
                out _slopeHit,
                3f * 0.5f + 0.6f,
                ignoreGroundCheckLayers
            )
        )
        {
            float angle = Vector3.Angle(Vector3.up, _slopeHit.normal);

            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }

    private Vector3 GetSlopeMoveDirection(Vector3 moveDirection, Vector3 slopeHitNormal)
    {
        return Vector3.ProjectOnPlane(moveDirection, slopeHitNormal).normalized;
    }
}
