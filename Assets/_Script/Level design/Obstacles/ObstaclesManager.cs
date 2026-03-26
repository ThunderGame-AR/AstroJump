using UnityEngine;
using NaughtyAttributes;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class ObstaclesManager : MonoBehaviour
{
    public enum MovementMode { MovementRange, InfiniteMovement }
    public enum RotationMode { RotationRange, InfiniteRotation }

    [System.Flags]
    public enum AxisFlags { X = 1, Y = 2, Z = 4 }

    [Header("Core Settings")]
    public bool takesPlayerBackToSpawnPoint = true;

    [HideIf("takesPlayerBackToSpawnPoint")]
    public bool carriesPlayer = false;

    [Header("Knockback")]
    [SerializeField][Min(0f)] private float hitForce = 80f;
    [SerializeField][Min(0f)] private float hitCooldownInSeconds = 2f;
    private float _lastHitTime;

    private bool ShowPhysicsSection => !takesPlayerBackToSpawnPoint && !carriesPlayer;

    [Header("Life Settings")]
    public bool lifeCycle = false;
    [ShowIf("lifeCycle")] public bool startsAlive = true;
    [ShowIf("lifeCycle")][Min(0f)] public float aliveDurationInSeconds = 5f;
    [ShowIf("lifeCycle")][Min(0f)] public float deadDurationInSeconds = 2.5f;

    [Header("Movement Settings")]
    public bool movementCycle = false;
    [ShowIf("movementCycle")] public MovementMode movementMode;

    [ShowIf(EConditionOperator.And, "movementCycle", "IsRangeMove")]
    [SerializeField] private Vector3 startPosition;
    [ShowIf(EConditionOperator.And, "movementCycle", "IsRangeMove")]
    [SerializeField] private Vector3 endPosition;

    [ShowIf(EConditionOperator.And, "movementCycle", "IsInfiniteMove")]
    public AxisFlags movementAxes = AxisFlags.Y;
    [ShowIf(EConditionOperator.And, "movementCycle", "IsInfiniteMove")]
    [SerializeField] private Vector3 movementDirection = Vector3.up;

    [ShowIf("movementCycle")]
    [SerializeField][Min(0f)] private float movementSpeed = 5f;

    [ShowIf(EConditionOperator.And, "movementCycle", "IsRangeMove")]
    [SerializeField] private bool repeatMovementLoop = true;
    [ShowIf(EConditionOperator.And, "movementCycle", "IsRangeMove")]
    [SerializeField][Min(0f)] private float waitTimeInSecondsToRepeatMovementLoop = 0f;

    private bool IsRangeMove => movementMode == MovementMode.MovementRange;
    private bool IsInfiniteMove => movementMode == MovementMode.InfiniteMovement;

    [ShowIf(EConditionOperator.And, "movementCycle", "IsRangeMove")]
    [Button("Set Current as Start Position")]
    private void SetS() => startPosition = transform.localPosition;
    [ShowIf(EConditionOperator.And, "movementCycle", "IsRangeMove")]
    [Button("Set Current as End Position")]
    private void SetE() => endPosition = transform.localPosition;

    [Header("Rotation Settings")]
    public bool rotationCycle = false;
    [ShowIf("rotationCycle")] public RotationMode rotationMode;

    [ShowIf(EConditionOperator.And, "rotationCycle", "IsRangeRot")]
    [SerializeField] private Vector3 startRotation;
    [ShowIf(EConditionOperator.And, "rotationCycle", "IsRangeRot")]
    [SerializeField] private Vector3 endRotation;

    [ShowIf(EConditionOperator.And, "rotationCycle", "IsInfiniteRot")]
    public AxisFlags rotationAxes = AxisFlags.Y;
    [ShowIf(EConditionOperator.And, "rotationCycle", "IsInfiniteRot")]
    [SerializeField] private Vector3 rotationDirection = Vector3.up;

    [ShowIf("rotationCycle")]
    [SerializeField][Min(0f)] private float rotationSpeed = 100f;

    [ShowIf(EConditionOperator.And, "rotationCycle", "IsRangeRot")]
    [SerializeField] private bool repeatRotationLoop = true;
    [ShowIf(EConditionOperator.And, "rotationCycle", "IsRangeRot")]
    [SerializeField][Min(0f)] private float waitTimeInSecondsToRepeatRotationLoop = 0f;

    private bool IsRangeRot => rotationMode == RotationMode.RotationRange;
    private bool IsInfiniteRot => rotationMode == RotationMode.InfiniteRotation;

    [ShowIf(EConditionOperator.And, "rotationCycle", "IsRangeRot")]
    [Button("Set Current as Start Rotation")]
    private void SetSR() => startRotation = transform.localEulerAngles;
    [ShowIf(EConditionOperator.And, "rotationCycle", "IsRangeRot")]
    [Button("Set Current as End Rotation")]
    private void SetER() => endRotation = transform.localEulerAngles;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip spawnSound;
    [SerializeField] private AudioClip despawnSound;
    [SerializeField] private AudioClip pushedSound;

    private Rigidbody _rb;
    private bool _movingToEnd = true;
    private bool _rotatingToEnd = true;
    private bool _isWaitingMove = false;
    private bool _isWaitingRotation = false;

    private Vector3 _lastPosition;
    private Vector3 _currentVelocity;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        if (movementCycle && IsRangeMove) transform.localPosition = startPosition;
        if (rotationCycle && IsRangeRot) transform.localRotation = Quaternion.Euler(startRotation);
        _lastPosition = _rb.position;
        if (lifeCycle) StartCoroutine(LifeCycleRoutine());
    }

    public Vector3 GetCurrentVelocity()
    {
        if (!carriesPlayer) return Vector3.zero;
        return _currentVelocity;
    }

    private IEnumerator LifeCycleRoutine()
    {
        bool currentlyAlive = startsAlive;
        while (true)
        {
            ToggleObjectState(currentlyAlive);

            AudioClip clipToPlay = currentlyAlive ? spawnSound : despawnSound;
            if (clipToPlay != null)
            {
                AudioSource.PlayClipAtPoint(clipToPlay, transform.position, 1f);
            }

            yield return new WaitForSeconds(currentlyAlive ? aliveDurationInSeconds : deadDurationInSeconds);
            currentlyAlive = !currentlyAlive;
        }
    }

    private void ToggleObjectState(bool state)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = state;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = state;
    }

    private void FixedUpdate()
    {
        _currentVelocity = (_rb.position - _lastPosition) / Time.fixedDeltaTime;
        _lastPosition = _rb.position;

        if (movementCycle && !_isWaitingMove) HandleMovement();
        if (rotationCycle && !_isWaitingRotation) HandleRotation();
    }

    private void HandleMovement()
    {
        if (IsRangeMove)
        {
            Vector3 target = _movingToEnd ? endPosition : startPosition;
            Vector3 targetWorld = transform.parent != null ? transform.parent.TransformPoint(target) : target;
            _rb.MovePosition(Vector3.MoveTowards(_rb.position, targetWorld, movementSpeed * Time.fixedDeltaTime));
            if (Vector3.Distance(_rb.position, targetWorld) < 0.001f && repeatMovementLoop) StartCoroutine(WaitMoveRoutine());
        }
        else
        {
            Vector3 moveDelta = Vector3.zero;
            if ((movementAxes & AxisFlags.X) != 0) moveDelta.x = movementDirection.x;
            if ((movementAxes & AxisFlags.Y) != 0) moveDelta.y = movementDirection.y;
            if ((movementAxes & AxisFlags.Z) != 0) moveDelta.z = movementDirection.z;
            _rb.MovePosition(_rb.position + transform.TransformDirection(moveDelta) * movementSpeed * Time.fixedDeltaTime);
        }
    }

    private void HandleRotation()
    {
        if (IsRangeRot)
        {
            Quaternion targetQuat = Quaternion.Euler(_rotatingToEnd ? endRotation : startRotation);
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, targetQuat, rotationSpeed * Time.fixedDeltaTime));
            if (Quaternion.Angle(_rb.rotation, targetQuat) < 0.1f && repeatRotationLoop) StartCoroutine(WaitRotationRoutine());
        }
        else
        {
            Vector3 rotDir = Vector3.zero;
            if ((rotationAxes & AxisFlags.X) != 0) rotDir.x = rotationDirection.x;
            if ((rotationAxes & AxisFlags.Y) != 0) rotDir.y = rotationDirection.y;
            if ((rotationAxes & AxisFlags.Z) != 0) rotDir.z = rotationDirection.z;
            Quaternion deltaRot = Quaternion.Euler(rotDir * rotationSpeed * Time.fixedDeltaTime);
            _rb.MoveRotation(deltaRot * _rb.rotation);
        }
    }

    private IEnumerator WaitMoveRoutine()
    {
        _isWaitingMove = true;
        yield return new WaitForSeconds(waitTimeInSecondsToRepeatMovementLoop);
        _movingToEnd = !_movingToEnd; _isWaitingMove = false;
    }

    private IEnumerator WaitRotationRoutine()
    {
        _isWaitingRotation = true;
        yield return new WaitForSeconds(waitTimeInSecondsToRepeatRotationLoop);
        _rotatingToEnd = !_rotatingToEnd; _isWaitingRotation = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) HandleImpact(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && !carriesPlayer) HandleImpact(other.gameObject);
    }

    private void HandleImpact(GameObject playerObj)
    {
        CharacterController cc = playerObj.GetComponent<CharacterController>();
        if (cc != null && !cc.enabled) return;

        if (Time.time < _lastHitTime + hitCooldownInSeconds) return;
        PersonController pc = playerObj.GetComponentInParent<PersonController>() ?? playerObj.GetComponent<PersonController>();
        if (pc == null) return;

        bool hasPickupShield = PickUpManager.Instance != null && PickUpManager.Instance.IsShieldActive();

        bool hasAreaShield = pc.currentActiveArea != null && pc.currentActiveArea.IsAreaActive() && pc.currentActiveArea.givesShieldInTheArea;

        bool isProtected = hasPickupShield || hasAreaShield;

        if (takesPlayerBackToSpawnPoint && !isProtected)
        {
            pc.Respawn();
        }
        else if (ShowPhysicsSection || isProtected)
        {
            if (pushedSound != null)
            {
                AudioSource.PlayClipAtPoint(pushedSound, transform.position, 1f);
            }

            _lastHitTime = Time.time;
            Vector3 centerToPlayer = (playerObj.transform.position - transform.position);
            centerToPlayer.y = 0;
            Vector3 finalHitDir = (centerToPlayer.normalized + _currentVelocity.normalized).normalized;
            finalHitDir.y = 0.2f;
            pc.AddImpact(finalHitDir, hitForce);
        }
    }
}