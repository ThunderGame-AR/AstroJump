using UnityEngine;
using NaughtyAttributes;

public class SpawnPoint_PickUp : MonoBehaviour
{
    [SerializeField] private Transform spawnPointTransform;

    [Header("Destruction Settings")]
    public bool destroyPickUp = false;

    [ShowIf("destroyPickUp")]
    [AllowNesting]
    [Min(0f)] public float destroyAfterSeconds = 0f;

    [ShowIf("destroyPickUp")]
    [AllowNesting]
    public bool destroyAtRespawn = true;

    [ShowIf(EConditionOperator.And, "destroyPickUp", "destroyAtRespawn")]
    [AllowNesting]
    [Min(1)] public int respawnThreshold = 1;

    private PersonController _personController;
    private PickUpManager _manager;
    private Vector3 _startPosition;

    private void Start()
    {
        _startPosition = transform.position;
        _personController = Object.FindFirstObjectByType<PersonController>();
        _manager = Object.FindFirstObjectByType<PickUpManager>();

        if (destroyPickUp && transform.position.y > 0)
        {
            if (!destroyAtRespawn)
            {
                Invoke(nameof(DisablePickup), destroyAfterSeconds);
            }
            else
            {
                PersonController.OnPlayerRespawn += StartRespawnTimer;
            }
        }
    }

    private void StartRespawnTimer()
    {
        if (_personController != null && _personController.numberTimesPlayerRespawned >= respawnThreshold)
        {
            if (!IsInvoking(nameof(DisablePickup)))
            {
                Invoke(nameof(DisablePickup), destroyAfterSeconds);
            }
        }
    }

    private void DisablePickup()
    {
        if (gameObject.activeSelf && _manager != null) _manager.PlayDestructionSound();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        PersonController.OnPlayerRespawn -= StartRespawnTimer;
    }

    private void Update()
    {
        if (_manager == null) return;
        transform.Rotate(Vector3.up, _manager.rotationSpeed * Time.deltaTime);
        float bounce = Mathf.Abs(Mathf.Sin(Time.time * _manager.bobbingSpeed)) * _manager.bobbingAmount;
        transform.position = _startPosition + new Vector3(0, bounce, 0);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && spawnPointTransform != null)
        {
            spawnPointTransform.position = _startPosition - new Vector3(0, 2f, 0);
            spawnPointTransform.rotation = transform.rotation;

            if (_manager != null)
            {
                _manager.SetNewCheckpoint(spawnPointTransform.position);
                _manager.PlayCollectSound(true);
            }

            PersonController.OnPlayerRespawn -= StartRespawnTimer;
            gameObject.SetActive(false);
        }
    }
}