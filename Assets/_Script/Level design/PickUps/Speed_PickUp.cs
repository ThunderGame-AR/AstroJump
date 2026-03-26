using UnityEngine;
using NaughtyAttributes;

public class Speed_PickUp : MonoBehaviour
{
    [Min(1f)] public float durationInSeconds = 15f;
    [Min(0f)] public float redThresholdInSeconds = 5f;
    [Min(0.2f)] public float speedMultiplier = 2.5f;

    [Header("Respawn Settings")]
    public bool keepPickUpsEffectsOnRespawn = false;

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
    private Vector3 _startPos;

    private void Start()
    {
        _startPos = transform.position;
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
        transform.position = _startPos + new Vector3(0, bounce, 0);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && _manager != null)
        {
            _manager.ActivateSpeed(durationInSeconds, redThresholdInSeconds, speedMultiplier, keepPickUpsEffectsOnRespawn);
            _manager.PlayCollectSound(false);
            PersonController.OnPlayerRespawn -= StartRespawnTimer;
            gameObject.SetActive(false);
        }
    }
}