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
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
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

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;
		private bool _isCrouching;
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

		private const float _threshold = 0.01f;
		private static readonly int _animIDSpeed = Animator.StringToHash("Speed");
		private static readonly int _animIDIsGrounded = Animator.StringToHash("IsGrounded");
		private static readonly int _animIDIsCrouching = Animator.StringToHash("IsCrouching");
		private static readonly int _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");

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
			// get a reference to our main camera
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
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif
			if (_animator == null)
			{
				_animator = GetComponentInChildren<Animator>(true);
			}

			_standingHeight = _controller.height;
			_standingCenter = _controller.center;
			_standingBottom = _standingCenter.y - (_standingHeight * 0.5f);

			if (CinemachineCameraTarget != null)
			{
				_standingCameraTargetLocalPosition = CinemachineCameraTarget.transform.localPosition;
				_crouchingCameraTargetLocalPosition = _standingCameraTargetLocalPosition - new Vector3(0.0f, CrouchCameraOffset, 0.0f);
			}

			if (CeilingLayers.value == 0)
			{
				CeilingLayers = ~0;
			}

			// reset our timeouts on start
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

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			// if there is an input
			if (_input.look.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
				
				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
		}

		private void Move()
		{
			// set target speed based on move speed, sprint speed and if sprint is pressed
			float targetSpeed = _isCrouching ? CrouchSpeed : (_input.sprint ? SprintSpeed : MoveSpeed);

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (_input.move != Vector2.zero)
			{
				// move
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			// move the player
			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
		}

		private void HandleCrouch()
		{
			UpdateCrouchState();
			ApplyCrouchTransition();
		}

		private void UpdateCrouchState()
		{
			if (_input.crouch)
			{
				_isCrouching = true;
				return;
			}

			if (_isCrouching && CanStandUp())
			{
				_isCrouching = false;
			}
		}

		private void ApplyCrouchTransition()
		{
			float targetHeight = _isCrouching ? CrouchHeight : _standingHeight;
			float nextHeight = Mathf.MoveTowards(_controller.height, targetHeight, CrouchTransitionSpeed * Time.deltaTime);
			_controller.height = nextHeight;
			_controller.center = GetCenterForHeight(nextHeight);

			if (CinemachineCameraTarget != null)
			{
				Vector3 targetCameraPosition = _isCrouching ? _crouchingCameraTargetLocalPosition : _standingCameraTargetLocalPosition;
				CinemachineCameraTarget.transform.localPosition = Vector3.MoveTowards(
					CinemachineCameraTarget.transform.localPosition,
					targetCameraPosition,
					CrouchTransitionSpeed * Time.deltaTime);
			}
		}

		private Vector3 GetCenterForHeight(float height)
		{
			return new Vector3(_standingCenter.x, _standingBottom + (height * 0.5f), _standingCenter.z);
		}

		private bool CanStandUp()
		{
			float radius = Mathf.Max(
				0.01f,
				(_controller.radius - _controller.skinWidth) *
				Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)));
			float halfHeight = Mathf.Max((_standingHeight * 0.5f) - _controller.radius + CeilingCheckBuffer, 0.0f);
			Vector3 bottom = transform.TransformPoint(_standingCenter + (Vector3.down * halfHeight));
			Vector3 top = transform.TransformPoint(_standingCenter + (Vector3.up * halfHeight));
			return !Physics.CheckCapsule(bottom, top, radius, GetCeilingLayerMask(), QueryTriggerInteraction.Ignore);
		}

		private int GetCeilingLayerMask()
		{
			return CeilingLayers.value & ~(1 << gameObject.layer);
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Jump
				if (_input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
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

			float horizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
			float normalizedSpeed = SprintSpeed > 0.0f ? Mathf.Clamp01(horizontalSpeed / SprintSpeed) : 0.0f;
			float motionSpeed = 0.0f;

			if (!Grounded)
			{
				motionSpeed = Mathf.Max(1.0f, MoveSpeed > 0.0f ? horizontalSpeed / MoveSpeed : 1.0f);
			}
			else if (horizontalSpeed > _threshold)
			{
				motionSpeed = MoveSpeed > 0.0f ? horizontalSpeed / MoveSpeed : 1.0f;
			}

			_animator.SetFloat(_animIDSpeed, normalizedSpeed);
			_animator.SetBool(_animIDIsGrounded, Grounded);
			_animator.SetBool(_animIDIsCrouching, _isCrouching);
			_animator.SetFloat(_animIDMotionSpeed, motionSpeed);
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}
	}
}
