using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.UI.Image;
using UnityEngine.InputSystem.XR;

[RequireComponent(typeof(CharacterController))]
public class PersonController : MonoBehaviour
{
    [Header("Movement")]
    [Min(1f)] public float movementSpeed = 5f;

    [Header("View")]
    [SerializeField] Transform cameraRoot;
    [SerializeField][Range(5f, 100f)] float mouseSensitivity = 20f;
    [SerializeField][Range(0f, 180f)] float maxLookUp = 60f;
    [SerializeField][Range(-180f, 0f)] float maxLookDown = -30f;

    [Header("Jump & Gravity")]
    [Min(1f)] public float jumpHeight = 3f;
    public float gravity = -9.81f;

    [Header("Ground Check")]
    [SerializeField] Transform groundCheck;
    [SerializeField][Min(0.1f)] float groundDistance = 0.4f;
    [SerializeField] LayerMask groundMask;

    [Header("Visual Rotation & Flip")]
    [SerializeField] Transform characterModel;
    [SerializeField][Range(10f, 90f)] float modelRotationSpeed = 30f;
    [SerializeField][Range(-2880f, 2880f)] float flipSpeed = 720f;
    [SerializeField][Min(1f)] float landCheckDistance = 1.25f;

    [Header("Respawn Settings")]
    [SerializeField] Transform spawnPoint;

    [Header("References")]
    [SerializeField] private GamePause gamePauseScript;
    [SerializeField] private EndGame endGameScript;
    [SerializeField] private PickUpManager pickUpManager;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource walkAudioSource;
    [SerializeField] private AudioClip walkLoopSound;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landingSound;
    [SerializeField] private AudioClip playerRespawnSound;

    [Header("VR Settings")]
    [SerializeField] private Camera xrCamera;

    CharacterController controller;
    PlayerInputActions inputActions;
    public static System.Action OnPlayerRespawn;
    Animator animator;

    private Vector3 impactVelocity = Vector3.zero;
    private Vector3 activePlatformVelocity = Vector3.zero;
    private float damping = 8f;

    Vector2 moveInput;
    Vector2 lookInput;
    [HideInInspector] public float verticalVelocity;
    float xRotation;
    private float _rotationYBeforeJump;

    [HideInInspector] public bool isGrounded;
    [HideInInspector] public int numberTimesPlayerRespawned;
    [HideInInspector] public EffectArea currentActiveArea;

    bool isActuallyJumping;
    bool isRotatingInAir;
    bool readyToPlayLandingSoundOnce = false;

    string respawnTag = "Respawn";

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        inputActions = new PlayerInputActions();
        
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += _ => moveInput = Vector2.zero;
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += _ => lookInput = Vector2.zero;
        inputActions.Player.Jump.performed += _ => TryJump();
        inputActions.Player.Recenter.performed += _ => RecenterView();
        numberTimesPlayerRespawned = 0;
    }

    void OnEnable() => inputActions.Enable();
    void OnDisable()
    {
        if (walkAudioSource != null) walkAudioSource.Stop();
        inputActions.Disable();
    }


    void Start()
    {
        Utilities.SetCursorLocked(gamePauseScript == null || !gamePauseScript.paused);
        if (SceneChanger.Instance != null)
        {
            SceneChanger.Instance.OnLoadingCompleted += Recenter;
        }

        Look();
    }

    void OnDestroy()
    {
        if (SceneChanger.Instance != null)
        {
            SceneChanger.Instance.OnLoadingCompleted -= Recenter;
        }
    }

    void Update()
    {
        if (impactVelocity.magnitude > 0.2f)
        {
            controller.Move(impactVelocity * Time.deltaTime);
            impactVelocity = Vector3.Lerp(impactVelocity, Vector3.zero, damping * Time.deltaTime);
        }

        if (IsPlayerMovementLocked()) return;

        CheckGroundStatus();
        Look();
        ApplyMovement();
        HandleAirLogic();
        UpdateAnimations();
    }

    void LateUpdate()
    {
        if (animator == null) return;
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if ((stateInfo.IsName("Clip for Rotation") || isRotatingInAir) && !isGrounded)
        {
            characterModel.Rotate(Vector3.right * flipSpeed * Time.deltaTime, Space.Self);
        }
    }

    public void RecenterView()
    {
        if (Time.deltaTime != 0) Recenter();
    }

    private void Recenter()
    {
        if (UnityEngine.XR.XRSettings.isDeviceActive)
        {
            Transform cameraOffset = xrCamera.transform.parent;
            if (cameraOffset != null)
            {
                float currentYaw = xrCamera.transform.localEulerAngles.y;
                cameraOffset.localRotation = Quaternion.Euler(0, -currentYaw, 0);
                Vector3 currentPos = xrCamera.transform.localPosition;
                cameraOffset.localPosition = new Vector3(-currentPos.x, 0, -currentPos.z);
            }
        }
        else
        {
            xRotation = 0f;
            cameraRoot.localRotation = Quaternion.identity;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("EffectArea"))
        {
            currentActiveArea = other.GetComponent<EffectArea>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("EffectArea"))
        {
            currentActiveArea = null;
        }
    }

    public bool IsPlayerMovementLocked()
    {
        bool isPaused = (gamePauseScript != null && gamePauseScript.paused);
        bool isOver = (endGameScript != null && endGameScript.isGameOver);
        return isPaused || isOver;
    }

    public void AddImpact(Vector3 direction, float force)
    {
        verticalVelocity = 0;
        impactVelocity = direction * force;
    }

    void ApplyMovement()
    {
        if (!(isActuallyJumping && isGrounded))
            verticalVelocity += gravity * Time.deltaTime;

        Vector3 camForward = cameraRoot.forward;
        Vector3 camRight = cameraRoot.right;

        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 playerMove = (camForward * moveInput.y + camRight * moveInput.x);

        if (moveInput.sqrMagnitude > 0.01f && !isRotatingInAir)
        {
            Quaternion targetRot = Quaternion.LookRotation(playerMove);
            characterModel.rotation = Quaternion.Slerp(characterModel.rotation, targetRot, modelRotationSpeed * Time.deltaTime);
        }

        float currentSpeed = movementSpeed;
        if (pickUpManager != null && pickUpManager.IsSpeedActive())
            currentSpeed = movementSpeed * pickUpManager.GetSpeedMultiplier();

        Vector3 finalMove = (playerMove * currentSpeed + Vector3.up * verticalVelocity + activePlatformVelocity) * Time.deltaTime;
        controller.Move(finalMove);

        if (isGrounded && activePlatformVelocity != Vector3.zero)
        {
            RaycastHit hit;
            if (Physics.Raycast(groundCheck.position, Vector3.down, out hit, 0.7f, groundMask))
            {
                if (hit.collider.GetComponentInParent<ObstaclesManager>() == null)
                {
                    activePlatformVelocity = Vector3.Lerp(activePlatformVelocity, Vector3.zero, 15f * Time.deltaTime);
                }
            }
        }
    }

    void CheckGroundStatus()
    {
        bool wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded)
        {
            if (!wasGrounded)
            {
                if (!isActuallyJumping && verticalVelocity < -2.1f && readyToPlayLandingSoundOnce)
                {
                    StartCoroutine(PlayLandingSoundDelayed(0.15f));
                    readyToPlayLandingSoundOnce = false;
                }
            }

            if (gravity < 0 && verticalVelocity < 0)
            {
                verticalVelocity = -2f;
                HandlePlatformVelocity();
            }
            else if (gravity > 0 && verticalVelocity > 0)
            {
                verticalVelocity = 2f;
            }

            if (!wasGrounded || isActuallyJumping || isRotatingInAir)
            {
                isActuallyJumping = isRotatingInAir = false;
                characterModel.localRotation = Quaternion.Euler(0, _rotationYBeforeJump, 0);
                animator?.SetBool("IsFalling", false);
                animator?.SetTrigger("JumpEnd");
            }
        }
        else
        {
            readyToPlayLandingSoundOnce = true;
        }
    }

    private IEnumerator PlayLandingSoundDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (isGrounded && landingSound != null)
        {
            AudioSource.PlayClipAtPoint(landingSound, transform.position, 0.7f);
        }
    }

    void HandlePlatformVelocity()
    {
        RaycastHit hit;
        if (Physics.Raycast(groundCheck.position, Vector3.down, out hit, 0.7f, groundMask))
        {
            ObstaclesManager plat = hit.collider.GetComponentInParent<ObstaclesManager>();
            if (plat != null && !isActuallyJumping)
            {
                activePlatformVelocity = plat.GetCurrentVelocity();
            }
        }
    }

    void Look()
    {
        float horizontalLook = lookInput.x * mouseSensitivity * Time.deltaTime;
        transform.Rotate(Vector3.up * horizontalLook);

        if (!UnityEngine.XR.XRSettings.isDeviceActive)
        {
            xRotation = Mathf.Clamp(xRotation - (lookInput.y * mouseSensitivity * Time.deltaTime), maxLookDown, maxLookUp);
            cameraRoot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }

    void TryJump()
    {
        if (isGrounded && !IsPlayerMovementLocked())
        {
            StopAllCoroutines();
            StartCoroutine(JumpSequence());
        }
    }

    IEnumerator JumpSequence()
    {
        isActuallyJumping = true;
        _rotationYBeforeJump = characterModel.localEulerAngles.y;
        animator?.SetTrigger("JumpTrigger");

        RaycastHit hit;
        if (Physics.Raycast(groundCheck.position, Vector3.down, out hit, 0.7f, groundMask))
        {
            ObstaclesManager plat = hit.collider.GetComponentInParent<ObstaclesManager>();
            if (plat != null) activePlatformVelocity = plat.GetCurrentVelocity();
        }

        yield return new WaitForSeconds(0.4f);

        if (jumpSound != null) AudioSource.PlayClipAtPoint(jumpSound, transform.position, 0.7f);

        float currentJump = jumpHeight;
        if (pickUpManager != null && pickUpManager.IsJumpActive())
            currentJump = jumpHeight * pickUpManager.GetJumpMultiplier();

        float jumpDirection = (gravity > 0) ? -1f : 1f;
        verticalVelocity = jumpDirection * Mathf.Sqrt(currentJump * -2f * -Mathf.Abs(gravity));
        yield return new WaitForSeconds(0.2f);
        isRotatingInAir = true;
    }

    void HandleAirLogic()
    {
        if (!isGrounded)
        {
            if (verticalVelocity < -1.0f && !isRotatingInAir)
            {
                isRotatingInAir = true;
                _rotationYBeforeJump = characterModel.localEulerAngles.y;
                animator?.SetBool("IsFalling", true);
            }

            if (isRotatingInAir && Physics.Raycast(transform.position, Vector3.down, landCheckDistance, groundMask))
            {
                animator?.SetBool("IsFalling", false);
                animator?.SetTrigger("JumpEnd");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(respawnTag)) Respawn();
    }

    public void Respawn()
    {
        if (currentActiveArea != null)
        {
            if (currentActiveArea.IsAreaActive())
            {
                currentActiveArea.ExitEffect();
            }

            currentActiveArea._isPlayerInside = false;
            currentActiveArea = null;
        }

        if (playerRespawnSound != null)
        {
            AudioSource.PlayClipAtPoint(playerRespawnSound, transform.position, 1f);
        }
        if (walkAudioSource != null) walkAudioSource.Stop();

        readyToPlayLandingSoundOnce = true;
        activePlatformVelocity = Vector3.zero;
        isActuallyJumping = isRotatingInAir = false;
        verticalVelocity = 0;

        StopAllCoroutines();

        if (pickUpManager != null) pickUpManager.ResetPickUpsOnRespawn();
        controller.enabled = false;

        if (spawnPoint)
        {
            transform.position = spawnPoint.position;
            Vector3 forwardDifference = spawnPoint.forward;
            forwardDifference.y = 0;
            transform.rotation = Quaternion.LookRotation(forwardDifference);
            XROrigin xrOrigin = GetComponentInParent<XROrigin>();
            if (xrOrigin != null)
            {
                xrOrigin.MatchOriginUpCameraForward(spawnPoint.forward, spawnPoint.up);
            }
        }

        characterModel.localRotation = Quaternion.identity;
        controller.enabled = true;
        moveInput = inputActions.Player.Move.ReadValue<Vector2>();
        animator?.Rebind();
        numberTimesPlayerRespawned++;
        OnPlayerRespawn?.Invoke();
    }

    void UpdateAnimations()
    {
        if (animator == null || walkAudioSource == null) return;

        if (IsPlayerMovementLocked())
        {
            if (walkAudioSource.isPlaying) walkAudioSource.Stop();
            animator.SetFloat("Speed", 0f);
            return;
        }

        float speed = moveInput.magnitude;
        animator.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
        animator.SetBool("IsGrounded", isGrounded);

        if (isGrounded && speed > 0.1f)
        {
            if (!walkAudioSource.isPlaying)
            {
                walkAudioSource.clip = walkLoopSound;
                walkAudioSource.loop = true;
                walkAudioSource.Play();
            }
        }
        else
        {
            if (walkAudioSource.isPlaying) walkAudioSource.Stop();
        }
    }
}