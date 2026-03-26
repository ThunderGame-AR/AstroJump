using UnityEngine;

public class CannonBullet : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioClip destructionSound;

    private float _speed;
    private float _lifetime;
    private float _timer;
    private Transform _playerTransform;
    private float shieldRadius = 3.5f;
    private float verticalOffset = 3f;
    private Vector3 _direction;
    private Vector3 _startPos;
    private TrailRenderer _trail;

    public void Setup(float speed, float lifetime)
    {
        _speed = speed;
        _lifetime = lifetime;
        _timer = 0;

        _direction = transform.forward;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _playerTransform = p.transform;

        if (_trail != null)
        {
            _trail.Clear();
        }

        _startPos = transform.position;
    }

    void Awake()
    {
        _trail = GetComponent<TrailRenderer>();
    }

    void Update()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.isPaused) return;

        transform.position += _direction * _speed * Time.deltaTime;

        if (IsPlayerShielded())
        {
            if (_playerTransform != null)
            {
                Vector3 shieldCenter = _playerTransform.position + Vector3.up * verticalOffset;
                float distanceToShield = Vector3.Distance(transform.position, shieldCenter);

                if (distanceToShield <= shieldRadius)
                {
                    PlayImpactSound(destructionSound);

                    gameObject.SetActive(false);
                    return;
                }
            }
        }

        _timer += Time.deltaTime;
        if (_timer >= _lifetime)
        {
            PlayImpactSound(destructionSound);

            gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;
        if (other.CompareTag("Cannon") || other.name.Contains("Cannon")) return;

        if (other.GetComponentInParent<ObstaclesManager>() != null || other.GetComponent<ObstaclesManager>() != null)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            if (IsPlayerShielded())
            {
                PlayImpactSound(destructionSound);

                gameObject.SetActive(false);
                return;
            }

            PersonController pc = other.GetComponentInParent<PersonController>() ?? other.GetComponent<PersonController>();
            if (pc != null) pc.Respawn();

            gameObject.SetActive(false);
        }
        else
        {
            PlayImpactSound(destructionSound);

            gameObject.SetActive(false);
        }
    }

    private void PlayImpactSound(AudioClip clip)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
        }
    }

    private bool IsPlayerShielded()
    {
        if (PickUpManager.Instance == null) return false;

        bool shieldFromPickup = PickUpManager.Instance.IsShieldActive();

        bool shieldFromArea = false;
        PersonController pc = _playerTransform.GetComponent<PersonController>();
        if (pc != null && pc.currentActiveArea != null && pc.currentActiveArea.IsAreaActive())
        {
            shieldFromArea = pc.currentActiveArea.givesShieldInTheArea;
        }

        return shieldFromPickup || shieldFromArea;
    }
}