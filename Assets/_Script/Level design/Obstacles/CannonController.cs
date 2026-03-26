using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

[RequireComponent(typeof(Rigidbody))]
public class CannonController : MonoBehaviour
{
    [Header("Detection & Vision")]
    [Min(15f)] public float detectionRange = 20f;
    public LayerMask visionMask = -1;
    [Range(0f, 4f)] public float targetVerticalOffset = 4f;

    [Header("Rotation Settings")]
    [SerializeField] private Transform barrelPivot;
    [Range(30f, 180f)] public float degreesPerSecond = 45f;
    [Range(-120f, 0f)] public float minVerticalAngle = -90f;
    [Range(0f, 120f)] public float maxVerticalAngle = 0f;

    [Header("Shooting Settings")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    [Min(0.5f)] public float fireRateInSeconds = 1.5f;
    [Min(10f)] public float bulletSpeed = 20f;
    [Min(1f)] public float bulletLifetime = 3f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource cannonAudioSource;
    [SerializeField] private AudioClip fireSound;

    private List<GameObject> _bulletPool = new List<GameObject>();
    private Transform _player;
    private Rigidbody _rb;
    private int poolSize = 10;
    private float _nextFireTime;
    private bool _hasLineOfSight;
    private bool _isAimingAtPlayer;
    private bool _targetWithinLimits;

    private Quaternion _initialBaseRotation;
    private Quaternion _initialBarrelRotation;

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;

        _initialBaseRotation = transform.rotation;
        if (barrelPivot != null) _initialBarrelRotation = barrelPivot.localRotation;

        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(bulletPrefab);
            Physics.SyncTransforms();
            obj.transform.parent = null;
            obj.SetActive(false);
            _bulletPool.Add(obj);
        }

        if (cannonAudioSource != null)
        {
            if (cannonAudioSource.GetComponent<SpatialAudioPausable>() == null)
                cannonAudioSource.gameObject.AddComponent<SpatialAudioPausable>();
        }
    }

    private void OnValidate()
    {
        if (cannonAudioSource != null)
        {
            cannonAudioSource.maxDistance = detectionRange;
            cannonAudioSource.minDistance = detectionRange * 0.1f;

            cannonAudioSource.spatialBlend = 1f;
            cannonAudioSource.rolloffMode = AudioRolloffMode.Linear;
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update()
    {
        if (_player == null) return;
        if (AudioManager.Instance != null && AudioManager.Instance.isPaused) return;

        float distance = Vector3.Distance(transform.position, _player.position);

        if (distance <= detectionRange) CheckLogic();
        else _hasLineOfSight = false;

        Quaternion targetBase, targetBarrel;

        if (_hasLineOfSight && _targetWithinLimits)
        {
            targetBase = GetTargetBaseRotation();
            targetBarrel = GetTargetBarrelRotation();

            if (_isAimingAtPlayer) HandleShooting();
        }
        else
        {
            targetBase = _initialBaseRotation;
            targetBarrel = _initialBarrelRotation;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetBase, degreesPerSecond * Time.deltaTime);

        if (barrelPivot != null)
        {
            barrelPivot.localRotation = Quaternion.RotateTowards(barrelPivot.localRotation, targetBarrel, degreesPerSecond * Time.deltaTime);
        }
    }

    void CheckLogic()
    {
        Vector3 targetPos = _player.position + Vector3.up * targetVerticalOffset;
        Vector3 directionToPlayer = (targetPos - firePoint.position).normalized;

        if (Physics.Raycast(firePoint.position, directionToPlayer, out RaycastHit hit, detectionRange, visionMask, QueryTriggerInteraction.Ignore))
        {
            _hasLineOfSight = hit.collider.CompareTag("Player");
        }
        else _hasLineOfSight = false;

        if (barrelPivot != null)
        {
            Vector3 directionToTarget = (targetPos - barrelPivot.position).normalized;
            Quaternion lookRot = Quaternion.LookRotation(directionToTarget);
            float angleX = lookRot.eulerAngles.x;
            if (angleX > 180) angleX -= 360;
            _targetWithinLimits = (angleX >= minVerticalAngle && angleX <= maxVerticalAngle);
        }

        _isAimingAtPlayer = Vector3.Angle(firePoint.forward, directionToPlayer) < 8f;
    }

    Quaternion GetTargetBaseRotation()
    {
        Vector3 dir = _player.position - transform.position;
        dir.y = 0;
        return (dir.magnitude > 0.1f) ? Quaternion.LookRotation(dir) : transform.rotation;
    }

    Quaternion GetTargetBarrelRotation()
    {
        Vector3 targetPos = _player.position + Vector3.up * targetVerticalOffset;
        Vector3 dir = (targetPos - barrelPivot.position).normalized;
        Quaternion lookRot = Quaternion.LookRotation(dir);
        float angleX = lookRot.eulerAngles.x;
        if (angleX > 180) angleX -= 360;
        float clampedX = Mathf.Clamp(angleX, minVerticalAngle, maxVerticalAngle);
        return Quaternion.Euler(clampedX, 0, 0);
    }

    void HandleShooting()
    {
        if (Time.time >= _nextFireTime)
        {
            Fire();
            _nextFireTime = Time.time + fireRateInSeconds;
        }
    }

    void Fire()
    {
        foreach (GameObject proj in _bulletPool)
        {
            if (!proj.activeSelf)
            {
                if (cannonAudioSource != null && fireSound != null) cannonAudioSource.PlayOneShot(fireSound);

                proj.transform.position = firePoint.position;
                proj.transform.rotation = firePoint.rotation;
                proj.SetActive(true);

                Collider bulletCollider = proj.GetComponent<Collider>();
                Collider[] cannonColliders = GetComponentsInChildren<Collider>();
                foreach (var c in cannonColliders)
                    if (bulletCollider != null && c != null) Physics.IgnoreCollision(bulletCollider, c);

                proj.GetComponent<CannonBullet>()?.Setup(bulletSpeed, bulletLifetime);
                return;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        #if UNITY_EDITOR
        Handles.color = Color.red;
        Handles.DrawWireDisc(transform.position, Vector3.up, detectionRange);
        Handles.DrawWireDisc(transform.position, Vector3.right, detectionRange);
        Handles.DrawWireDisc(transform.position, Vector3.forward, detectionRange);
        #endif

        Gizmos.color = new Color(1, 0, 0, 0.1f);
        Gizmos.DrawSphere(transform.position, detectionRange);
    }
}