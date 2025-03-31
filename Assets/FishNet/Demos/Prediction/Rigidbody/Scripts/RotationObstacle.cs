using FishNet.Component.Prediction;
using FishNet.Object;
using UnityEngine;

public class RotationObstacle : NetworkBehaviour
{
    private NetworkCollision _networkCollision;

    [SerializeField]
    private Vector3 rotationAmount;

    private void Awake()
    {
        _networkCollision = GetComponent<NetworkCollision>();
        _networkCollision.OnEnter += NetworkCollisionEnter;
    }

    private void FixedUpdate()
    {
        if (IsServerInitialized)
        {
            ServerUpdateRotation();
        }
    }

    private void OnDestroy()
    {
        if (_networkCollision != null)
        {
            _networkCollision.OnEnter -= NetworkCollisionEnter;
        }
    }

    private void ServerUpdateRotation()
    {
        float delta = Time.deltaTime;

        Quaternion rotationX = Quaternion.AngleAxis(rotationAmount.x * delta, Vector3.right);
        Quaternion rotationY = Quaternion.AngleAxis(rotationAmount.y * delta, Vector3.up);
        Quaternion rotationZ = Quaternion.AngleAxis(rotationAmount.z * delta, Vector3.forward);

        transform.rotation = rotationX * rotationY * rotationZ * transform.rotation;
    }

    private void NetworkCollisionEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement>(out var rbPlayer))
        {
            Debug.Log("ENTER COLLISION 123aac");

            Vector3 dir = (rbPlayer.gameObject.transform.position - transform.position).normalized;
            rbPlayer.PredictionRigidbody.AddForce(dir * 150f, ForceMode.Impulse);
        }
    }
}
