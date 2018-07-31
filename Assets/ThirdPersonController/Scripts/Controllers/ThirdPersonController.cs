using UnityEngine;
using UnityEngine.UI;

namespace CoverShooter
{
    /// <summary>
    /// Takes player input (usually from ThirdPersonInput) and translates that to commands to CharacterMotor.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(Actor))]
    public class ThirdPersonController : MonoBehaviour
    {
        /// <summary>
        /// Is the character actively reacting to camera direction changes.
        /// </summary>
        public bool IsActivelyFacing
        {
            get { return _isActivelyFacing; }
        }

        /// <summary>
        /// Is the character using zoom.
        /// </summary>
        public bool IsZooming
        {
            get
            {
                return ZoomInput &&
                       _motor.IsAlive &&
                       (((!_motor.IsInCover || _motor.IsReloadingAndNotAiming) && _motor.IsInCameraAimableState) ||
                        (_motor.IsInCover && !_motor.IsReloadingAndNotAiming && _motor.IsInAimableState));
            }
        }

        /// <summary>
        /// Is the character using zoom.
        /// </summary>
        public bool IsScoping
        {
            get { return ScopeInput && IsZooming && CouldScope && _motor.IsWeaponScopeReady; }
        }

        /// <summary>
        /// Can a scope be displayed right now.
        /// </summary>
        public bool CouldScope
        {
            get
            {
                var gun = _motor.ActiveWeapon.Gun;
                return gun != null && gun.Scope != null;
            }
        }

        /// <summary>
        /// Determines if the character takes cover automatically instead of waiting for player input.
        /// </summary>
        [Tooltip("Determines if the character takes cover automatically instead of waiting for player input.")]
        public bool AutoTakeCover = true;

        /// <summary>
        /// Time in seconds after a potential cover has been detected when the character automatically enters it.
        /// </summary>
        [Tooltip("Time in seconds after a potential cover has been detected when the character automatically enters it.")]
        public float CoverEnterDelay = 0.1f;

        /// <summary>
        /// Is the character always aiming in camera direction when not in cover.
        /// </summary>
        [Tooltip("Is the character always aiming in camera direction when not in cover.")]
        public bool AlwaysAim = false;

        /// <summary>
        /// Should the character aim when walking, if turned off it will only aim when zooming in.
        /// </summary>
        [Tooltip("Should the character aim when walking, if turned off it will only aim when zooming in.")]
        public bool AimWhenWalking = false;

        /// <summary>
        /// Should the character start crouching near closing in to a low cover.
        /// </summary>
        [Tooltip("Should the character start crouching near closing in to a low cover.")]
        public bool CrouchNearCovers = true;

        /// <summary>
        /// Will the character turn immediatelly when aiming.
        /// </summary>
        [Tooltip("Will the character turn immediatelly when aiming.")]
        public bool ImmediateTurns = true;

        /// <summary>
        /// How long to continue aiming after no longer needed.
        /// </summary>
        [Tooltip("How long to continue aiming after no longer needed.")]
        public float AimSustain = 0.4f;

        /// <summary>
        /// Time in seconds to keep the gun down when starting to move.
        /// </summary>
        [Tooltip("Time in seconds to keep the gun down when starting to move.")]
        public float NoAimSustain = 0.14f;

        /// <summary>
        /// Can the player roll into a cover.
        /// </summary>
        [Tooltip("Can the player roll into a cover.")]
        public bool TakeCoverWhenRolling = true;

        /// <summary>
        /// Degrees to add when aiming a grenade vertically.
        /// </summary>
        [Tooltip("Degrees to add when aiming a grenade vertically.")]
        public float ThrowAngleOffset = 30;

        /// <summary>
        /// How high can the player throw the grenade.
        /// </summary>
        [Tooltip("How high can the player throw the grenade.")]
        public float MaxThrowAngle = 45;

        /// <summary>
        /// Time in seconds to wait after landing before AlwaysAim activates again.
        /// </summary>
        [Tooltip("Time in seconds to wait after landing before AlwaysAim activates again.")]
        public float PostLandAimDelay = 0.4f;

        /// <summary>
        /// Time in seconds to wait before lifting an arm when running with a pistol.
        /// </summary>
        [Tooltip("Time in seconds to wait before lifting an arm when running with a pistol.")]
        public float ArmLiftDelay = 1.5f;

        /// <summary>
        /// Prefab to instantiate to display grenade explosion preview.
        /// </summary>
        [Tooltip("Prefab to instantiate to display grenade explosion preview.")]
        public GameObject ExplosionPreview;

        /// <summary>
        /// Prefab to instantiate to display grenade path preview.
        /// </summary>
        [Tooltip("Prefab to instantiate to display grenade path preview.")]
        public GameObject PathPreview;

        /// <summary>
        /// Scope object and component that's enabled and maintained when using scope.
        /// </summary>
        [Tooltip("Scope object and component that's enabled and maintained when using scope.")]
        public Image Scope;

        /// <summary>
        /// Sets the controller to start or stop firing.
        /// </summary>
        [HideInInspector]
        public bool FireInput;

        /// <summary>
        /// Sets the controller to start and stop zooming.
        /// </summary>
        [HideInInspector]
        public bool ZoomInput;

        /// <summary>
        /// Sets the controller to start and stop using scope.
        /// </summary>
        [HideInInspector]
        public bool ScopeInput;

        /// <summary>
        /// Sets the position the controller is rotated at.
        /// </summary>
        [HideInInspector]
        public Vector3 BodyTargetInput;

        /// <summary>
        /// Sets the position the controller is aiming at.
        /// </summary>
        [HideInInspector]
        public Vector3 AimTargetInput;

        /// <summary>
        /// Sets the horizontal angle for aiming a grenade.
        /// </summary>
        [HideInInspector]
        public float GrenadeHorizontalAngleInput;

        /// <summary>
        /// Sets the vertical angle for aiming a grenade.
        /// </summary>
        [HideInInspector]
        public float GrenadeVerticalAngleInput;

        /// <summary>
        /// Sets the movement for the controller.
        /// </summary>
        [HideInInspector]
        public CharacterMovement MovementInput;

        /// <summary>
        /// Will the Update function be called manually by some other component (most likely ThirdPersonInput).
        /// </summary>
        [HideInInspector]
        public bool WaitForUpdateCall;

        private CharacterMotor _motor;

        private GameObject _explosionPreview;
        private GameObject _pathPreview;

        private bool _isSprinting;
        private bool _isAiming;
        private bool _isActivelyFacing;

        private float _noAimSustain;
        private float _aimSustain;
        private float _postSprintNoAutoAim;

        private Vector3[] _grenadePath = new Vector3[128];
        private int _grenadePathLength;
        private bool _hasGrenadePath;
        private bool _wantsToThrowGrenade;

        private bool _wantsToAim;

        private bool _startedRollingInCover;
        private bool _wasRolling;

        private bool _wasZooming;
        private bool _wasScoping;

        private float _coverDelayWait = 0;
        private float _postLandWait = 0;
        private bool _hasLanded = true;

        private bool _wasInCover;

        private float _armLiftTimer;
        private float _armLiftRetain;

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
        }

        /// <summary>
        /// Set the grenade throw mode.
        /// </summary>
        public void InputThrowGrenade()
        {
            _wantsToThrowGrenade = true;
        }

        /// <summary>
        /// Make the character aim in the following frame.
        /// </summary>
        public void InputAim()
        {
            _wantsToAim = true;
        }

        private void OnEnable()
        {
            if (_motor != null && AlwaysAim && !_motor.ActiveWeapon.IsNull)
                _motor.InputImmediateAim();
        }

        private void LateUpdate()
        {
            if (!WaitForUpdateCall)
                ManualUpdate();
        }

        /// <summary>
        /// Update the controller after some other component has updated.
        /// </summary>
        public void ManualUpdate()
        {
            var isAimingBecauseAlways = AlwaysAim && !_isSprinting;
            var hasLanded = !_motor.IsJumping && !_motor.IsFalling;
            var gun = _motor.ActiveWeapon.Gun;

            if (hasLanded)
            {
                if (_postLandWait >= 0)
                    _postLandWait -= Time.deltaTime;
            }
            else
                _postLandWait = PostLandAimDelay;

            if (isAimingBecauseAlways)
            {
                if (!hasLanded || _postLandWait > float.Epsilon)
                    isAimingBecauseAlways = false;
            }

            _hasLanded = hasLanded;
            _isActivelyFacing = isAimingBecauseAlways;

            if (isAimingBecauseAlways)
                _motor.StopAimingWhenEnteringCover();

            updateGrenadeAimAndPreview();

            CharacterMovement movement;
            var isMoving = updateMovement(out movement);

            if (_motor.IsRolling && !_wasRolling)
                _startedRollingInCover = _wasInCover;

            _wasInCover = _motor.IsInCover;

            if (AutoTakeCover)
                _motor.InputImmediateCoverSearch();

            if (((AutoTakeCover && _coverDelayWait >= CoverEnterDelay) || ((_startedRollingInCover || TakeCoverWhenRolling) && _motor.IsRolling)) && _motor.PotentialCover != null)
                _motor.InputTakeCover();

            if (AutoTakeCover && CrouchNearCovers)
                _motor.InputCrouchNearCover();

            if (_motor.PotentialCover != null)
                _coverDelayWait += Time.deltaTime;
            else
                _coverDelayWait = 0;

            if (_motor.IsInProcess && !_motor.CanMoveInProcess)
                _armLiftTimer = 0;
            else if (_motor.IsInCover)
            {
                if (!_motor.IsInTallCover)
                    _armLiftTimer = ArmLiftDelay;
                else
                    _armLiftTimer = 0;
            }
            else
            {
                if ((movement.IsSlowedDown || movement.Magnitude > 0.6f) && movement.Magnitude < 1.1f && isMoving && !_motor.IsClimbingOrVaulting)
                {
                    _armLiftTimer += Time.deltaTime;
                    _armLiftRetain = 0.1f;
                }
                else
                {
                    if (_armLiftRetain > float.Epsilon)
                        _armLiftRetain -= Time.deltaTime;
                    else
                        _armLiftTimer = Mathf.Clamp01(_armLiftTimer - Time.deltaTime);
                }
            }

            if (_armLiftTimer >= ArmLiftDelay - float.Epsilon)
                _motor.InputArmLift();

            if (_motor.HasGrenadeInHand)
            {
                if (_hasGrenadePath && _wantsToThrowGrenade)
                {
                    _wantsToThrowGrenade = false;
                    _isActivelyFacing = true;
                    _motor.SetAimTarget(BodyTargetInput);
                    _motor.InputThrowGrenade(_grenadePath, _grenadePathLength, _motor.Grenade.Step);
                }

                FireInput = false;
                ZoomInput = false;
            }
            else
            {
                if (_motor.IsWeaponReady && FireInput)
                {
                    if (gun != null && gun.LoadedBulletsLeft <= 0)
                        _motor.InputReload();
                    else
                        _motor.InputFire();

                    _armLiftTimer = ArmLiftDelay;
                    _isActivelyFacing = true;
                    _isSprinting = false;
                }

                if (_motor.IsWeaponScopeReady && ZoomInput)
                {
                    _motor.InputAim();
                    _motor.InputZoom();

                    if (ScopeInput)
                        _motor.InputScope();

                    _isActivelyFacing = true;
                    _isSprinting = false;
                }
            }

            if (_isSprinting)
            {
                _isAiming = false;
                _isActivelyFacing = false;
                FireInput = false;
                ZoomInput = false;
                ScopeInput = false;
            }

            if (_isAiming && _aimSustain >= 0)
                _aimSustain -= Time.deltaTime;

            if (_noAimSustain >= 0)
                _noAimSustain -= Time.deltaTime;

            if (!FireInput && !ZoomInput)
            {
                if (_postSprintNoAutoAim >= 0)
                    _postSprintNoAutoAim -= Time.deltaTime;
            }
            else
            {
                _postSprintNoAutoAim = 0;
                _noAimSustain = 0;
            }

            if ((!_isSprinting && _isActivelyFacing && _postSprintNoAutoAim <= float.Epsilon) ||
                _wantsToAim ||
                 FireInput ||
                 ZoomInput)
            {
                _isAiming = true;
                _aimSustain = AimSustain;
            }
            else if (!_isAiming)
                _noAimSustain = NoAimSustain;

            if (!isAimingBecauseAlways)
                if (_aimSustain <= float.Epsilon || _noAimSustain > float.Epsilon)
                    _isAiming = false;

            if (_isAiming && gun != null)
            {
                if (_motor.IsInCover)
                    _motor.InputAimWhenLeavingCover();
                else
                    _motor.InputAim();
            }

            if (_isActivelyFacing && ImmediateTurns)
                _motor.InputPossibleImmediateTurn();

            if (!_motor.IsAiming && _isSprinting && isMoving)
            {
                var vector = BodyTargetInput - transform.position;
                vector.y = 0;
                var distance = vector.magnitude;

                var target = transform.position + MovementInput.Direction * distance;
                _motor.SetBodyTarget(target);
                _motor.SetAimTarget(target);
            }
            else if (_isActivelyFacing || _motor.IsAiming || _motor.IsInCover || isMoving)
            {
                _motor.SetBodyTarget(BodyTargetInput);
                _motor.SetAimTarget(AimTargetInput);
            }

            if (ZoomInput && !_wasZooming)
                SendMessage("OnZoom", SendMessageOptions.DontRequireReceiver);
            else if (!ZoomInput && _wasZooming)
                SendMessage("OnUnzoom", SendMessageOptions.DontRequireReceiver);

            if (ScopeInput && !_wasScoping)
                SendMessage("OnScope", SendMessageOptions.DontRequireReceiver);
            else if (!ScopeInput && _wasScoping)
                SendMessage("OnUnscope", SendMessageOptions.DontRequireReceiver);

            _wasZooming = ZoomInput;
            _wasScoping = ScopeInput;
            _wasRolling = _motor.IsRolling;
            _wantsToAim = false;

            if (Scope != null)
            {
                if (Scope.gameObject.activeSelf != IsScoping)
                {
                    Scope.gameObject.SetActive(IsScoping);

                    if (Scope.gameObject.activeSelf && gun != null)
                        Scope.sprite = gun.Scope;
                }
            }
        }

        private bool updateMovement(out CharacterMovement movementOutput)
        {
            var movement = MovementInput;

            var wasSprinting = _isSprinting;
            _isSprinting = false;

            var isMoving = false;

            if (movement.IsMoving)
            {
                isMoving = true;

                if (AimWhenWalking)
                    _isActivelyFacing = true;

                if (movement.Magnitude > 1.1f && !_motor.IsInCover)
                    _isSprinting = true;

                if (!_isSprinting && wasSprinting)
                    _postSprintNoAutoAim = 0.0f;
            }
            else if (wasSprinting && AimWhenWalking)
                _postSprintNoAutoAim = 0.3f;

            if (movement.Direction.sqrMagnitude > float.Epsilon && !_isSprinting && _motor.IsInCover)
            {
                Vector3 local;

                if (_motor.IsInCover)
                    local = Util.HorizontalVector(Util.HorizontalAngle(movement.Direction) - transform.eulerAngles.y);
                else
                    local = Util.HorizontalVector(Util.HorizontalAngle(movement.Direction) - Util.HorizontalAngle(BodyTargetInput - transform.position));

                movement.Direction = Quaternion.Euler(0, transform.eulerAngles.y, 0) * local;
            }

            _motor.InputMovement(movement);
            movementOutput = movement;

            return isMoving;
        }

        private void updateGrenadeAimAndPreview()
        {
            if (_motor.IsAlive && _motor.IsReadyToThrowGrenade && _motor.CurrentGrenade != null)
            {
                GrenadeDescription desc;
                desc.Gravity = _motor.Grenade.Gravity;
                desc.Duration = _motor.PotentialGrenade.Timer;
                desc.Bounciness = _motor.PotentialGrenade.Bounciness;

                var verticalAngle = Mathf.Min(GrenadeVerticalAngleInput + ThrowAngleOffset, MaxThrowAngle);

                var velocity = _motor.Grenade.MaxVelocity;

                if (verticalAngle < 45)
                    velocity *= Mathf.Clamp01((verticalAngle + 15) / 45f);

                _grenadePathLength = GrenadePath.Calculate(GrenadePath.Origin(_motor, GrenadeHorizontalAngleInput),
                                                           GrenadeHorizontalAngleInput,
                                                           verticalAngle,
                                                           velocity,
                                                           desc,
                                                           _grenadePath,
                                                           _motor.Grenade.Step);
                _hasGrenadePath = true;

                if (_explosionPreview == null && ExplosionPreview != null)
                {
                    _explosionPreview = GameObject.Instantiate(ExplosionPreview);
                    _explosionPreview.transform.SetParent(null);
                    _explosionPreview.SetActive(true);
                }

                if (_explosionPreview != null)
                {
                    _explosionPreview.transform.localScale = Vector3.one * _motor.PotentialGrenade.ExplosionRadius * 2;
                    _explosionPreview.transform.position = _grenadePath[_grenadePathLength - 1];
                }

                if (_pathPreview == null && PathPreview != null)
                {
                    _pathPreview = GameObject.Instantiate(PathPreview);
                    _pathPreview.transform.SetParent(null);
                    _pathPreview.SetActive(true);
                }

                if (_pathPreview != null)
                {
                    _pathPreview.transform.position = _grenadePath[0];

                    var path = _pathPreview.GetComponent<PathPreview>();

                    if (path != null)
                    {
                        path.Points = _grenadePath;
                        path.PointCount = _grenadePathLength;
                    }
                }
            }
            else
            {
                if (_explosionPreview != null)
                {
                    GameObject.Destroy(_explosionPreview);
                    _explosionPreview = null;
                }

                if (_pathPreview != null)
                {
                    GameObject.Destroy(_pathPreview);
                    _pathPreview = null;
                }

                _hasGrenadePath = false;
            }
        }
    }
}