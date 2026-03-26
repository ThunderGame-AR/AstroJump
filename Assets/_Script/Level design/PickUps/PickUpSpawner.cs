using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;

public enum SpawnerEndMode { None, Reset, Destroy }

public class PickUpSpawner : MonoBehaviour
{
    [Header("General Settings")]
    [SerializeField] private List<GameObject> poolPickUps = new List<GameObject>();
    [Min(1f)] public float activateAfterSeconds = 10f;
    public bool spawnImmediatelyAfterWaiting = true;
    [SerializeField][Min(1f)] private float cooldownTimeInSeconds = 20f;
    public bool activateAtRespawn = false;
    [ShowIf("activateAtRespawn")][Min(1)] public int respawnThresholdForActivation = 1;

    [Header("Cycle Settings")]
    public SpawnerEndMode endMode = SpawnerEndMode.None;

    [HideIf("endMode", SpawnerEndMode.None)]
    [Min(0)] public int collectedPickUps = 3;

    [HideIf("endMode", SpawnerEndMode.None)]
    public bool startCountingAtRespawn = false;

    [ShowIf(EConditionOperator.And, "Condition_HasEndMode", "startCountingAtRespawn")]
    [Min(1)] public int respawnThreshold = 1;

    [HideIf("endMode", SpawnerEndMode.None)]
    public bool destroyPickUpOnEnd = false;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip spawnSound;
    [SerializeField] private AudioClip spawnerDestroySound;

    private Dictionary<int, List<GameObject>> _spawnerPools = new Dictionary<int, List<GameObject>>();
    private GameObject _currentInstance;
    private bool _isActive = false;
    private bool _isCooldown = false;
    private bool _isWaitingInitial = false;
    private bool _hasProcessedPickUp = false;

    private float _timer = 0f;
    private int _collectedInThisCycle = 0;

    private float _maxAlpha = 0.5f, _minAlpha = 0.05f, _offAlpha = 0.025f;
    private Renderer _tecaRenderer;
    private MaterialPropertyBlock _propBlock;
    private PersonController _personController;

    private bool Condition_HasEndMode => endMode != SpawnerEndMode.None;

    private void Awake()
    {
        _tecaRenderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();
        _personController = Object.FindFirstObjectByType<PersonController>();
        UpdateTecaAlpha(_offAlpha);
        StartInitialWait();
    }

    private void OnEnable() => PersonController.OnPlayerRespawn += HandleRespawn;
    private void OnDisable() => PersonController.OnPlayerRespawn -= HandleRespawn;

    private void StartInitialWait()
    {
        _isWaitingInitial = true;
        _timer = activateAfterSeconds;
        _isActive = false;
        _isCooldown = false;
        _collectedInThisCycle = 0;
    }

    private void Update()
    {
        if (_isWaitingInitial)
        {
            if (activateAtRespawn && _personController != null)
            {
                if (_personController.numberTimesPlayerRespawned < respawnThresholdForActivation) return;
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0)
            {
                _isWaitingInitial = false;
                ActivateSpawner();
            }
            return;
        }

        if (!_isActive) return;

        if (!_isCooldown && (_currentInstance == null || !_currentInstance.activeSelf))
        {
            if (!_hasProcessedPickUp) ProcessCollection();
        }

        if (_isCooldown)
        {
            _timer -= Time.deltaTime;
            UpdateTecaAlpha(Mathf.Lerp(_minAlpha, _maxAlpha, 1f - (_timer / cooldownTimeInSeconds)));
            if (_timer <= 0) Spawn();
        }
    }

    private void ActivateSpawner()
    {
        _isActive = true;
        _collectedInThisCycle = 0;
        _hasProcessedPickUp = false;
        if (spawnImmediatelyAfterWaiting) Spawn();
        else StartCooldown();
    }

    private void ProcessCollection()
    {
        _hasProcessedPickUp = true;
        _currentInstance = null;

        if (endMode != SpawnerEndMode.None)
        {
            bool canIncrementAndCheck = true;

            if (startCountingAtRespawn && _personController != null)
            {
                if (_personController.numberTimesPlayerRespawned < respawnThreshold)
                {
                    canIncrementAndCheck = false;
                }
            }

            if (canIncrementAndCheck)
            {
                _collectedInThisCycle++;
                if (_collectedInThisCycle >= collectedPickUps)
                {
                    ExecuteEndLogic();
                    return;
                }
            }
        }

        StartCooldown();
    }

    private void ExecuteEndLogic()
    {
        if (destroyPickUpOnEnd && _currentInstance != null)
        {
            _currentInstance.SetActive(false);
        }

        if (endMode == SpawnerEndMode.Destroy)
        {
            if (spawnerDestroySound != null)
                AudioSource.PlayClipAtPoint(spawnerDestroySound, transform.position);
            Destroy(gameObject);
        }
        else
        {
            UpdateTecaAlpha(_offAlpha);
            StartInitialWait();
        }
    }

    private void HandleRespawn()
    {
        if (endMode != SpawnerEndMode.None && startCountingAtRespawn && _isActive)
        {
            if (_personController.numberTimesPlayerRespawned >= respawnThreshold && _collectedInThisCycle >= collectedPickUps)
            {
                ExecuteEndLogic();
            }
        }
    }

    private void Spawn()
    {
        bool isBusy = SceneChanger.Instance != null && SceneChanger.Instance.IsBusy;
        bool isPaused = (GamePause.Instance != null && GamePause.Instance.paused);

        if (isBusy || isPaused) return;

        if (poolPickUps.Count == 0 || !_isActive) return;
        _isCooldown = false;
        _hasProcessedPickUp = false;

        int index = Random.Range(0, poolPickUps.Count);
        GameObject prefab = poolPickUps[index];
        int prefabID = prefab.GetInstanceID();

        if (!_spawnerPools.ContainsKey(prefabID))
        {
            _spawnerPools[prefabID] = new List<GameObject>();
        }

        _currentInstance = null;
        foreach (GameObject obj in _spawnerPools[prefabID])
        {
            if (obj != null && !obj.activeSelf)
            {
                _currentInstance = obj;
                _currentInstance.transform.position = transform.position + new Vector3(0, -0.125f, 0);
                _currentInstance.transform.rotation = Quaternion.identity;
                _currentInstance.SetActive(true);
                break;
            }
        }

        if (_currentInstance == null)
        {
            _currentInstance = Instantiate(prefab, transform.position + new Vector3(0, -0.125f, 0), Quaternion.identity);
            _spawnerPools[prefabID].Add(_currentInstance);
        }

        if (audioSource != null && spawnSound != null)
            audioSource.PlayOneShot(spawnSound);

        UpdateTecaAlpha(_maxAlpha);
    }

    private void StartCooldown()
    {
        _isCooldown = true;
        _timer = cooldownTimeInSeconds;
        UpdateTecaAlpha(_minAlpha);
    }

    private void UpdateTecaAlpha(float alpha)
    {
        if (_tecaRenderer == null) return;
        _tecaRenderer.GetPropertyBlock(_propBlock);
        Color c = _tecaRenderer.sharedMaterial.color;
        c.a = alpha;
        _propBlock.SetColor("_BaseColor", c);
        _tecaRenderer.SetPropertyBlock(_propBlock);
    }
}