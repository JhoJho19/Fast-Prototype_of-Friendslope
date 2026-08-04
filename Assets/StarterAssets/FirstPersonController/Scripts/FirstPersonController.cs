using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 4.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 6.0f;

        [Tooltip("Rotation speed of the character")]
        public float RotationSpeed = 1.0f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again")]
        public float JumpTimeout = 0.1f;

        [Tooltip("Time required to pass before entering the fall state")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check")]
        public float GroundedRadius = 0.5f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target used by the Cinemachine camera")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 90.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -90.0f;

        [Header("Crouch")]
        [SerializeField]
        [Tooltip("Move speed of the character while crouching")]
        private float CrouchSpeed = 2.0f;

        [SerializeField]
        [Tooltip("CharacterController height while crouching")]
        private float CrouchHeight = 1.25f;

        [SerializeField]
        [Tooltip("How quickly crouch transitions are applied")]
        private float CrouchTransitionSpeed = 10.0f;

        [SerializeField]
        [Tooltip("How far to lower the camera target while crouching")]
        private float CrouchCameraOffset = 0.5f;

        [SerializeField]
        [Tooltip("Layers checked before standing up")]
        private LayerMask CeilingLayers = ~0;

        [SerializeField]
        [Tooltip("Extra headroom required to stand up")]
        private float CeilingCheckBuffer = 0.05f;

        [Header("Animation")]
        [SerializeField]
        private Animator _animator;

        // Cinemachine
        private float _cinemachineTargetPitch;

        // Movement
        private float _speed;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private readonly float _terminalVelocity = 53.0f;

        // Timeouts
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // Crouch
        private bool _isCrouching;
        private bool _wasCrouchPressed;

        private float _standingHeight;
        private float _standingBottom;
        private Vector3 _standingCenter;
        private Vector3 _standingCameraTargetLocalPosition;
        private Vector3 _crouchingCameraTargetLocalPosition;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif

        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private readonly RaycastHit[] _groundHits = new RaycastHit[8];
        private readonly Collider[] _ceilingHits = new Collider[8];

        private const float Threshold = 0.01f;

        private static readonly int AnimIDSpeed =
            Animator.StringToHash("Speed");

        private static readonly int AnimIDIsGrounded =
            Animator.StringToHash("IsGrounded");

        private static readonly int AnimIDIsCrouching =
            Animator.StringToHash("IsCrouching");

        private static readonly int AnimIDMotionSpeed =
            Animator.StringToHash("MotionSpeed");

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();

#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
            Debug.LogError(
                "Starter Assets package is missing dependencies. " +
                "Use Tools/Starter Assets/Reinstall Dependencies.");
#endif

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }

            SaveStandingParameters();

            if (CeilingLayers.value == 0)
            {
                CeilingLayers = ~0;
            }

            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            GroundedCheck();
            HandleCrouch();
            JumpAndGravity();
            Move();
            UpdateAnimator();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        private void SaveStandingParameters()
        {
            _standingHeight = _controller.height;
            _standingCenter = _controller.center;

            _standingBottom =
                _standingCenter.y - _standingHeight * 0.5f;

            if (CinemachineCameraTarget == null)
            {
                return;
            }

            _standingCameraTargetLocalPosition =
                CinemachineCameraTarget.transform.localPosition;

            _crouchingCameraTargetLocalPosition =
                _standingCameraTargetLocalPosition -
                new Vector3(0.0f, CrouchCameraOffset, 0.0f);
        }

        private void GroundedCheck()
        {
            float scaleX = Mathf.Abs(transform.lossyScale.x);
            float scaleZ = Mathf.Abs(transform.lossyScale.z);

            float groundedRadius =
                GroundedRadius * Mathf.Max(scaleX, scaleZ);

            float castDistance =
                Mathf.Abs(GroundedOffset) +
                groundedRadius +
                _controller.skinWidth;

            Vector3 spherePosition = new Vector3(
                transform.position.x,
                transform.position.y - GroundedOffset,
                transform.position.z);

            Vector3 castOrigin =
                spherePosition + Vector3.up * castDistance;

            int hitCount = Physics.SphereCastNonAlloc(
                castOrigin,
                groundedRadius,
                Vector3.down,
                _groundHits,
                castDistance * 2.0f,
                GroundLayers,
                QueryTriggerInteraction.Ignore);

            float minimumGroundDot =
                Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad);

            Grounded = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundHits[i];

                if (hit.collider == null ||
                    IsSelfCollider(hit.collider))
                {
                    continue;
                }

                if (hit.normal.y < minimumGroundDot)
                {
                    continue;
                }

                Grounded = true;
                break;
            }
        }

        private void CameraRotation()
        {
            if (_input.look.sqrMagnitude < Threshold)
            {
                return;
            }

            float deltaTimeMultiplier =
                IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            _cinemachineTargetPitch +=
                _input.look.y *
                RotationSpeed *
                deltaTimeMultiplier;

            _rotationVelocity =
                _input.look.x *
                RotationSpeed *
                deltaTimeMultiplier;

            _cinemachineTargetPitch = ClampAngle(
                _cinemachineTargetPitch,
                BottomClamp,
                TopClamp);

            if (CinemachineCameraTarget != null)
            {
                CinemachineCameraTarget.transform.localRotation =
                    Quaternion.Euler(
                        _cinemachineTargetPitch,
                        0.0f,
                        0.0f);
            }

            transform.Rotate(Vector3.up * _rotationVelocity);
        }

        private void Move()
        {
            float targetSpeed;

            if (_isCrouching)
            {
                targetSpeed = CrouchSpeed;
            }
            else
            {
                targetSpeed =
                    _input.sprint ? SprintSpeed : MoveSpeed;
            }

            if (_input.move == Vector2.zero)
            {
                targetSpeed = 0.0f;
            }

            Vector3 horizontalVelocity = new Vector3(
                _controller.velocity.x,
                0.0f,
                _controller.velocity.z);

            float currentHorizontalSpeed =
                horizontalVelocity.magnitude;

            const float speedOffset = 0.1f;

            float inputMagnitude =
                _input.analogMovement
                    ? _input.move.magnitude
                    : 1.0f;

            bool speedIsDifferent =
                currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset;

            if (speedIsDifferent)
            {
                _speed = Mathf.Lerp(
                    currentHorizontalSpeed,
                    targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                _speed =
                    Mathf.Round(_speed * 1000.0f) / 1000.0f;
            }
            else
            {
                _speed = targetSpeed;
            }

            Vector3 inputDirection = new Vector3(
                _input.move.x,
                0.0f,
                _input.move.y).normalized;

            if (_input.move != Vector2.zero)
            {
                inputDirection =
                    transform.right * _input.move.x +
                    transform.forward * _input.move.y;
            }

            Vector3 horizontalMovement =
                inputDirection.normalized *
                (_speed * Time.deltaTime);

            Vector3 verticalMovement =
                Vector3.up *
                (_verticalVelocity * Time.deltaTime);

            _controller.Move(
                horizontalMovement + verticalMovement);
        }

        private void HandleCrouch()
        {
            UpdateCrouchState();
            ApplyCrouchTransition();
        }

        private void UpdateCrouchState()
        {
            _isCrouching = false;
            _wasCrouchPressed = false;
        }

        private void ApplyCrouchTransition()
        {
            float targetHeight =
                _isCrouching
                    ? CrouchHeight
                    : _standingHeight;

            float nextHeight = Mathf.MoveTowards(
                _controller.height,
                targetHeight,
                CrouchTransitionSpeed * Time.deltaTime);

            _controller.height = nextHeight;
            _controller.center = GetCenterForHeight(nextHeight);

            if (CinemachineCameraTarget == null)
            {
                return;
            }

            Vector3 targetCameraPosition =
                _isCrouching
                    ? _crouchingCameraTargetLocalPosition
                    : _standingCameraTargetLocalPosition;

            CinemachineCameraTarget.transform.localPosition =
                Vector3.MoveTowards(
                    CinemachineCameraTarget.transform.localPosition,
                    targetCameraPosition,
                    CrouchTransitionSpeed * Time.deltaTime);
        }

        private Vector3 GetCenterForHeight(float height)
        {
            return new Vector3(
                _standingCenter.x,
                _standingBottom + height * 0.5f,
                _standingCenter.z);
        }

        private bool CanStandUp()
        {
            float scaleX = Mathf.Abs(transform.lossyScale.x);
            float scaleZ = Mathf.Abs(transform.lossyScale.z);

            float radius = Mathf.Max(
                0.01f,
                (_controller.radius - _controller.skinWidth) *
                Mathf.Max(scaleX, scaleZ));

            Vector3 currentTop =
                GetTopHemisphereCenter(_controller.height);

            Vector3 standingTop =
                GetTopHemisphereCenter(_standingHeight) +
                transform.up * CeilingCheckBuffer;

            if ((standingTop - currentTop).sqrMagnitude <= Threshold)
            {
                return true;
            }

            int hitCount = Physics.OverlapCapsuleNonAlloc(
                currentTop,
                standingTop,
                radius,
                _ceilingHits,
                CeilingLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _ceilingHits[i];

                if (hit == null || IsSelfCollider(hit))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private bool IsSelfCollider(Collider collider)
        {
            return collider.transform.root == transform.root;
        }

        private Vector3 GetTopHemisphereCenter(float height)
        {
            Vector3 center = GetCenterForHeight(height);

            float hemisphereOffset = Mathf.Max(
                height * 0.5f - _controller.radius,
                0.0f);

            return transform.TransformPoint(
                center + Vector3.up * hemisphereOffset);
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2.0f;
                }

                if (_input.jump &&
                    _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(
                        JumpHeight * -2.0f * Gravity);
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }

                _input.jump = false;
            }

            // Gravity отрицательная, поэтому ограничиваем
            // скорость падения значением -_terminalVelocity.
            if (_verticalVelocity > -_terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private void UpdateAnimator()
        {
            if (_animator == null)
            {
                return;
            }

            Vector3 horizontalVelocity = new Vector3(
                _controller.velocity.x,
                0.0f,
                _controller.velocity.z);

            float horizontalSpeed =
                horizontalVelocity.magnitude;

            bool hasMovementInput =
                _input.move.sqrMagnitude > Threshold;

            if (Grounded && !hasMovementInput)
            {
                horizontalSpeed = 0.0f;
            }

            float normalizedSpeed =
                SprintSpeed > 0.0f
                    ? Mathf.Clamp01(
                        horizontalSpeed / SprintSpeed)
                    : 0.0f;

            float motionSpeed = 0.0f;

            if (!Grounded)
            {
                motionSpeed = Mathf.Max(
                    1.0f,
                    MoveSpeed > 0.0f
                        ? horizontalSpeed / MoveSpeed
                        : 1.0f);
            }
            else if (hasMovementInput &&
                     horizontalSpeed > Threshold)
            {
                float referenceSpeed =
                    _isCrouching
                        ? CrouchSpeed
                        : MoveSpeed;

                motionSpeed =
                    referenceSpeed > 0.0f
                        ? horizontalSpeed / referenceSpeed
                        : 1.0f;
            }

            _animator.SetFloat(
                AnimIDSpeed,
                normalizedSpeed);

            _animator.SetBool(
                AnimIDIsGrounded,
                Grounded);

            _animator.SetBool(
                AnimIDIsCrouching,
                _isCrouching);

            _animator.SetFloat(
                AnimIDMotionSpeed,
                motionSpeed);
        }

        private static float ClampAngle(
            float angle,
            float min,
            float max)
        {
            if (angle < -360.0f)
            {
                angle += 360.0f;
            }

            if (angle > 360.0f)
            {
                angle -= 360.0f;
            }

            return Mathf.Clamp(angle, min, max);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen =
                new Color(0.0f, 1.0f, 0.0f, 0.35f);

            Color transparentRed =
                new Color(1.0f, 0.0f, 0.0f, 0.35f);

            Gizmos.color =
                Grounded
                    ? transparentGreen
                    : transparentRed;

            Gizmos.DrawSphere(
                new Vector3(
                    transform.position.x,
                    transform.position.y - GroundedOffset,
                    transform.position.z),
                GroundedRadius);
        }
    }
}
