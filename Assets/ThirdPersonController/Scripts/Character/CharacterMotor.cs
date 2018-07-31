using System;
using UnityEngine;

namespace CoverShooter
{
    /// <summary>
    /// Current cover offset state inside the character motor.
    /// </summary>
    public enum CoverOffsetState
    {
        None,
        Entering,
        Using,
        Exiting
    }

    /// <summary>
    /// State of the weapon in character's hands.
    /// </summary>
    public enum WeaponEquipState
    {
        unequipped,
        equipping,
        equipped,
        unequipping
    }

    /// <summary>
    /// Characters must have a Character Motor component attached. It manages the character, it’s movement, appearance and use of weapons. It handles gravity and therefore gravity should be turned off in the RigidBody component to avoid conflicts.
    /// There is an IK (inverse-kinematics) system that handles aiming and recoil. It can be configured manually but for ease of use there is a button to set it up automatically inside the CharacterMotor inspector. It is recommended to reduce the amount of bones used by IK on non-player characters for performance reasons.
    /// IK is calculated by adjusting bones until certain objects reach defined targets. Target objects must be part of the skeleton in order for changes to modify their transform. Character’s sight is usually defined by a marker object that is part of the head, bones are transformed until marker’s forward vector points towards the target. 
    /// Each character has a set of weapons in its disposal. To add a new weapon to the character you must create an object with a 3D model and a Gun component and attach it to a hand. Additionally, you can create a version of the weapon that is put into its holster. The motor automatically enables and disables weapon and holster objects.
    /// Characters can throw grenades from both hands. Left hand is used in some situations when the character is hiding behind a cover. Grenades are duplicated when thrown. 
    /// A character can be setup to have many hitboxes for various body parts. Setup is done inside Character Health component.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterMotor : MonoBehaviour
    {
        public const int FirstWeaponLayer = 0;
        public const int GrenadeLayer = 4;
        public const int GrenadeTopLayer = 5;

        #region Properties

        /// <summary>
        /// An object the motor is currently aiming at. Only objects with CharacterHealth are considered.
        /// </summary>
        public GameObject Target
        {
            get { return _target; }
        }

        /// <summary>
        /// Position the body is rotated at.
        /// </summary>
        public Vector3 BodyLookTarget
        {
            get { return _currentBodyTarget; }
        }

        /// <summary>
        /// Position the body is aiming at.
        /// </summary>
        public Vector3 AimTarget
        {
            get { return _aimTarget; }
        }

        /// <summary>
        /// Closest position on the cover to the character. Returns character position if no cover available.
        /// </summary>
        public Vector3 ClosestCoverPosition
        {
            get
            {
                if (_cover.In)
                {
                    var closest =  _cover.Main.ClosestPointTo(transform.position, _capsule.radius, _capsule.radius);
                    return closest - _cover.ForwardDirection * Vector3.Dot(-_cover.ForwardDirection, transform.position - closest);
                }
                else
                    return transform.position;
            }
        }

        /// <summary>
        /// Horizontal aim vector.
        /// </summary>
        public Vector3 AimForward
        {
            get
            {
                var vec = AimTarget - transform.position;
                vec.y = 0;

                return vec.normalized;
            }
        }

        /// <summary>
        /// Is the motor currently performing a custom action.
        /// </summary>
        public bool IsPerformingCustomAction
        {
            get { return _isPerformingCustomAction; }
        }

        /// <summary>
        /// Is the motor currently in a custom process.
        /// </summary>
        public bool IsInProcess
        {
            get { return _isInProcess; }
        }

        /// <summary>
        /// Is the character currently in a process and that process allows movement.
        /// </summary>
        public bool CanMoveInProcess
        {
            get { return _isInProcess && _process.CanMove; }
        }

        /// <summary>
        /// Is character currently climbing or vaulting.
        /// </summary>
        public bool IsClimbingOrVaulting
        {
            get { return _isClimbing; }
        }

        /// <summary>
        /// Is character currently vaulting.
        /// </summary>
        public bool IsVaulting
        {
            get { return _isClimbing && _isClimbingAVault; }
        }

        /// <summary>
        /// Y coordinate of the character position before the vault.
        /// </summary>
        public float VaultPosition
        {
            get { return _vaultPosition; }
        }

        /// <summary>
        /// Is the character currently in cover.
        /// </summary>
        public bool IsInCover
        {
            get { return _cover.In; }
        }

        /// <summary>
        /// Is the character currently crouching.
        /// </summary>
        public bool IsCrouching
        {
            get { return _isCrouching; }
        }

        /// <summary>
        /// Degrees in world space of direction the character is intended to face.
        /// </summary>
        public float BodyAngle
        {
            get { return _horizontalAngle; }
        }

        /// <summary>
        /// Is the character currently facing left in a cover.
        /// </summary>
        public bool IsStandingLeftInCover
        {
            get { return _cover.In && _cover.IsStandingLeft; }
        }

        /// <summary>
        /// Does the character have switched arms and wants to keep that for at least another frame.
        /// </summary>
        public bool WantsToMaintainMirror
        {
            get { return _keepZoomingAndPotentiallyReloading; }
        }

        /// <summary>
        /// Is the character currently in low cover.
        /// </summary>
        public bool IsInLowCover
        {
            get { return _cover.In && (_isCrouching || !_cover.IsTall); }
        }

        /// <summary>
        /// Is the character currently in tall cover.
        /// </summary>
        public bool IsInTallCover
        {
            get { return _cover.In && _cover.IsTall && !_isCrouching; }
        }

        /// <summary>
        /// Is the character currently in tall cover and crouching.
        /// </summary>
        public bool IsCrouchingInTallCover
        {
            get { return _isCrouching && _cover.In && _cover.IsTall; }
        }

        /// <summary>
        /// Is the character currently falling without control.
        /// </summary>
        public bool IsFalling
        {
            get { return _isFalling; }
        }

        /// <summary>
        /// Is the character currently jumping (false if it entered a long fall).
        /// </summary>
        public bool IsJumping
        {
            get { return _isJumping; }
        }

        /// <summary>
        /// Is the line between the player and target blocked by a close cover. 
        /// Used to determine if the player should stand up when aiming from low cover.
        /// </summary>
        public bool IsAimingThroughCoverPlane
        {
            get { return _isAimingThroughCoverPlane; }
        }

        /// <summary>
        /// Vertical shift in degrees of the weapon affected by recoil.
        /// </summary>
        public float VerticalRecoil
        {
            get { return _verticalRecoil; }
        }

        /// <summary>
        /// Horizontal shift in degrees of the weapon affected by recoil.
        /// </summary>
        public float HorizontalRecoil
        {
            get { return _horizontalRecoil; }
        }

        /// <summary>
        /// Weapon currently held or intended to be held in hands (EquippedWeapon is set to none in the middle of changing weapons whereas ActiveWeapon is not).
        /// </summary>
        public WeaponDescription ActiveWeapon
        {
            get { return _weaponEquipState == WeaponEquipState.unequipped ? (IsEquipped ? Weapon : new WeaponDescription()) : _equippedWeapon; }
        }

        /// <summary>
        /// Weapon currently held in hands.
        /// </summary>
        public WeaponDescription EquippedWeapon
        {
            get { return _weaponEquipState == WeaponEquipState.unequipped ? new WeaponDescription() : _equippedWeapon; }
        }

        /// <summary>
        /// State of the weapon in character's hands.
        /// </summary>
        public WeaponEquipState WeaponEquipState
        {
            get { return _weaponEquipState; }
        }

        /// <summary>
        /// Is currently switching to a different weapon or unequipping.
        /// </summary>
        public bool IsChangingWeapon
        {
            get
            {
                if (_weaponEquipState == WeaponEquipState.unequipped)
                    return _equippedWeapon != Weapon && IsEquipped;
                else if (_weaponEquipState == WeaponEquipState.equipped)
                    return false;
                else
                    return true;
            }
        }

        /// <summary>
        /// Is currently switching to a different weapon or has equipped in the last few frames.
        /// </summary>
        public bool IsChangingWeaponOrHasJustChanged
        {
            get
            {
                if (_weaponEquipState == WeaponEquipState.unequipped)
                    return _equippedWeapon != Weapon && IsEquipped;
                else if (_weaponEquipState == WeaponEquipState.equipped)
                    return _weaponGrabTimer > float.Epsilon;
                else
                    return true;
            }
        }

        /// <summary>
        /// Can the camera zoom in right now for aiming.
        /// </summary>
        public bool IsInCameraAimableState
        {
            get
            {
                if (Weapon.IsNull || !IsEquipped)
                    return false;

                if (HasGrenadeInHand || IsClimbingOrVaulting || _isFalling || _isJumping || IsRolling || IsPerformingCustomAction || (_isInProcess && !_process.CanAim))
                    return false;

                return true;
            }
        }

        /// <summary>
        /// Can the character possibly use scope when aiming.
        /// </summary>
        public bool IsInAimableState
        {
            get
            {
                if (Weapon.IsNull || !IsEquipped)
                    return false;

                if (IsChangingWeapon || HasGrenadeInHand || IsClimbingOrVaulting || _isFalling || _isJumping || IsRolling || IsPerformingCustomAction || (_isInProcess && !_process.CanAim))
                    return false;

                return true;
            }
        }

        /// <summary>
        /// Should the character use IK to put the left hand in the correct position.
        /// </summary>
        public bool IsLeftHandAimReady
        {
            get
            {
                var weapon = EquippedWeapon;

                if (weapon.IsNull)
                    return false;

                if (weapon.Type == WeaponType.Pistol && weapon.Shield != null)
                    return false;

                if (IsChangingWeapon || IsReloading || HasGrenadeInHand || IsClimbingOrVaulting || _isFalling || _isJumping || IsRolling || IsPerformingCustomAction || (_isInProcess && !_process.CanAim))
                    return false;

                return true;
            }
        }

        /// <summary>
        /// Is the motor aiming in on a weapon.
        /// </summary>
        public bool IsZooming
        {
            get { return IsAimingGun && (_wantsToZoom || _wantedToZoom); }
        }

        /// <summary>
        /// Is the motor using a scope on a weapon.
        /// </summary>
        public bool IsScoping
        {
            get { return IsAimingGun && (_wantsToScope || _wantedToScope) && EquippedWeapon.Gun != null && EquippedWeapon.Gun.Scope != null; }
        }

        /// <summary>
        /// Is the gun not being changed or reloaded.
        /// </summary>
        public bool IsWeaponReady
        {
            get
            {
                if (!IsInAimableState || _isLoadingMagazine || _isPumping)
                    return false;

                return true;
            }
        }

        /// <summary>
        /// Is the weapon currently in any reload animation.
        /// </summary>
        public bool IsReloading
        {
            get
            {
                return _isPumping || _postPumpDelay > float.Epsilon || _isLoadingBullet || _isLoadingMagazine;
            }
        }

        /// <summary>
        /// Is the weapon currently in any reload animation that prevents aiming.
        /// </summary>
        public bool IsReloadingAndNotAiming
        {
            get
            {
                return _isLoadingBullet || _isLoadingMagazine;
            }
        }

        /// <summary>
        /// Is the weapon usable for scoping right now.
        /// </summary>
        public bool IsWeaponScopeReady
        {
            get
            {
                return IsInAimableState && !_isLoadingBullet && !_isLoadingMagazine;
            }
        }

        /// <summary>
        /// Is the gun not being changed or reloaded.
        /// </summary>
        public bool IsGunReady
        {
            get
            {
                if (!IsWeaponReady)
                    return false;

                return EquippedWeapon.Gun != null;
            }
        }

        /// <summary>
        /// Is the current 'weapon' a gun and usable for scoping right now.
        /// </summary>
        public bool IsGunScopeReady
        {
            get
            {
                if (!IsWeaponScopeReady)
                    return false;

                return EquippedWeapon.Gun != null;
            }
        }

        /// <summary>
        /// Is the character currently rolling.
        /// </summary>
        public bool IsRolling
        {
            get { return _isRolling || _isIntendingToRoll; }
        }

        /// <summary>
        /// Is the character going to sprint. True when already sprinting.
        /// </summary>
        public bool IsGoingToSprint
        {
            get { return !_cover.In && _isGrounded && _inputMovement.Magnitude > 1.1f; }
        }

        /// <summary>
        /// Is the character currently sprinting.
        /// </summary>
        public bool IsSprinting
        {
            get { return _isSprinting; }
        }

        /// <summary>
        /// Is the motor currently loading a bullet.
        /// </summary>
        public bool IsLoadingBullet
        {
            get { return _isLoadingBullet; }
        }

        /// <summary>
        /// Is the motor currently loading a magazine.
        /// </summary>
        public bool IsLoadingMagazine
        {
            get { return _isLoadingMagazine; }
        }

        /// <summary>
        /// Is the motor currently pumping the weapon.
        /// </summary>
        public bool IsPumping
        {
            get { return _isPumping || _postPumpDelay > float.Epsilon; }
        }

        /// <summary>
        /// Is the character by a tall cover's left corner they can take a peek from.
        /// </summary>
        public bool IsByAnOpenLeftCorner
        {
            get
            {
                return _cover.In && _cover.Main.OpenLeft && (!_cover.HasLeftAdjacent || (_cover.Main.IsTall && !_cover.LeftAdjacent.IsTall)) && IsNearLeftCorner;
            }
        }


        /// <summary>
        /// Is the character by a tall cover's right corner they can take a peek from.
        /// </summary>
        public bool IsByAnOpenRightCorner
        {
            get
            {
                return _cover.In && _cover.Main.OpenRight && (!_cover.HasRightAdjacent || (_cover.Main.IsTall && !_cover.RightAdjacent.IsTall)) && IsNearRightCorner;
            }
        }


        /// <summary>
        /// Is the character currently in cover and standing near the left corner.
        /// </summary>
        public bool IsNearLeftCorner
        {
            get
            {
                if (!_cover.In)
                    return false;

                return _cover.In &&
                       _cover.HasLeftCorner &&
                       _cover.Main.IsByLeftCorner(transform.position - _coverOffset, CoverSettings.CornerAimTriggerDistance);
            }
        }

        /// <summary>
        /// Is the character currently in cover and standing near the right corner.
        /// </summary>
        public bool IsNearRightCorner
        {
            get
            {
                if (!_cover.In)
                    return false;

                return _cover.In &&
                       _cover.HasRightCorner &&
                       _cover.Main.IsByRightCorner(transform.position - _coverOffset, CoverSettings.CornerAimTriggerDistance);
            }
        }

        /// <summary>
        /// Is the currently transitioning between cover offsets
        /// </summary>
        public bool IsMovingToCoverOffset
        {
            get
            {
                if (!_cover.In)
                    return false;

                return _sideOffset == CoverOffsetState.Entering ||
                       _sideOffset == CoverOffsetState.Exiting ||
                       _backOffset == CoverOffsetState.Entering ||
                       _backOffset == CoverOffsetState.Exiting;
            }
        }

        /// <summary>
        /// Is the currently transitioning between cover offsets and the gun cannot be aimed
        /// </summary>
        public bool IsMovingToCoverOffsetAndCantAim
        {
            get
            {
                if (!IsMovingToCoverOffset)
                    return false;

                return !_canAimInThisOffsetAnimationOrIsAtTheEndOfIt;
            }
        }

        /// <summary>
        /// Returns the object of the current taken cover.
        /// </summary>
        public Cover Cover { get { return _cover.Main; } }

        /// <summary>
        /// Currently faced direction in cover. -1 for left, 1 for right.
        /// </summary>
        public int CoverDirection { get { return _cover.Direction; } }

        /// <summary>
        /// Returns the object of the cover left to the current cover.
        /// </summary>
        public Cover LeftCover { get { return _cover.LeftAdjacent; } }

        /// <summary>
        /// Returns the object of the cover right to the current cover.
        /// </summary>
        public Cover RightCover { get { return _cover.RightAdjacent; } }

        /// <summary>
        /// Origin from which various aiming decisions and blending is performed.
        /// </summary>
        public Vector3 AimOrigin
        {
            get
            {
                var flip = _ik.HasSwitchedHands ? -1f : 1f;
                var right = Vector3.Cross(Vector3.up, _bodyTarget - transform.position).normalized;

                return VirtualHead + right * _capsule.radius * flip * 0.75f;
            }
        }

        /// <summary>
        /// Origin from which various aiming decisions and blending is performed. Position is based on the current character position and no the virtual one.
        /// </summary>
        public Vector3 AimOriginWithCoverOffset
        {
            get
            {
                var flip = _ik.HasSwitchedHands ? -1f : 1f;
                var right = Vector3.Cross(Vector3.up, _bodyTarget - transform.position).normalized;
                var result = VirtualHead + right * _capsule.radius * flip * 0.75f;

                if (_cover.In)
                    result += _coverOffset;

                return result;
            }
        }

        /// <summary>
        /// Virtual position at which the head is considered to be located.
        /// </summary>
        public Vector3 VirtualHead
        {
            get
            {
                var position = transform.position + _capsule.height * Vector3.up * 0.75f;

                if (_cover.In)
                    position -= _coverOffset;

                return position;
            }
        }

        /// <summary>
        /// Position of the currently held gun where bullets would appear. 
        /// </summary>
        public Vector3 GunOrigin
        {
            get { return EquippedWeapon.Gun == null ? transform.position : EquippedWeapon.Gun.Origin; }
        }

        /// <summary>
        /// Direction of the gun affected by recoil.
        /// </summary>
        public Vector3 GunDirection
        {
            get { return EquippedWeapon.Gun == null ? transform.forward : EquippedWeapon.Gun.Direction; }
        }

        /// <summary>
        /// Position of the top of the capsule.
        /// </summary>
        public Vector3 Top
        {
            get { return transform.position + Vector3.up * _defaultCapsuleHeight; }
        }

        /// <summary>
        /// Is the character in a state where they want to aim.
        /// </summary>
        public bool WouldAim
        {
            get
            {
                if (_isThrowing || _isGrenadeTakenOut)
                    return true;

                return !Weapon.IsNull && IsEquipped;
            }
        }

        /// <summary>
        /// Is aiming a tool.
        /// </summary>
        public bool IsAimingTool
        {
            get
            {
                return _isUsingWeapon && EquippedWeapon.IsAnAimableTool(_isUsingWeaponAlternate);
            }
        }

        /// <summary>
        /// Was the motor aiming a gun during a previous frame.
        /// </summary>
        public bool WasAimingGun
        {
            get { return _wasAimingGun; }
        }

        /// <summary>
        /// Is aiming or intending to aim.
        /// </summary>
        public bool IsAimingGun
        {
            get
            {
                if (!_isFalling &&
                    !_isClimbing &&
                    !_isJumping &&
                    !_isWeaponBlocked &&
                    IsGunScopeReady &&
                    !HasGrenadeInHand)
                {
                    if (_coverAim.IsAiming || wantsToAim)
                        return true;
                    else if (!_cover.In && !EquippedWeapon.IsNull && (wantsToAim || _wantsToAimWhenLeavingCover))
                        return true;
                    else
                        return false;
                }
                else
                    return false;
            }
        }

        /// <summary>
        /// Is currently aiming a gun or grenade.
        /// </summary>
        public bool IsAiming
        {
            get { return _isThrowing || IsAimingGun || IsAimingTool; }
        }

        /// <summary>
        /// Is the motor currently either in or transitioning from or to side cover offset.
        /// </summary>
        public bool IsInAnySideOffset
        {
            get { return _sideOffset != CoverOffsetState.None; }
        }

        /// <summary>
        /// Is the motor currently fully in cover offset.
        /// </summary>
        public bool IsInCoverOffset
        {
            get { return _sideOffset == CoverOffsetState.Using || _backOffset == CoverOffsetState.Using; }
        }

        /// <summary>
        /// Is the character currently walking or standing.
        /// </summary>
        public bool IsWalkingOrStanding
        {
            get { return _currentMovement.Magnitude < 0.9f; }
        }

        /// <summary>
        /// Was there any intended movement in cover.
        /// </summary>
        public bool IsWalkingInCover
        {
            get { return _wasMovingInCover; }
        }

        /// <summary>
        /// Returns height when standing.
        /// </summary>
        public float StandingHeight
        {
            get { return _defaultCapsuleHeight; }
        }
        
        /// <summary>
        /// Returns current height of the capsule collider.
        /// </summary>
        public float CurrentHeight
        {
            get { return _capsule.height; }
        }

        /// <summary>
        /// Height the capsule is reaching for.
        /// </summary>
        public float TargetHeight
        {
            get
            {
                var targetHeight = _defaultCapsuleHeight;

                if (_isClimbing && _normalizedClimbTime < 0.5f)
                    targetHeight = _isClimbingAVault ? VaultSettings.CapsuleHeight : ClimbSettings.CapsuleHeight;
                else if (_isJumping && _jumpTimer < JumpSettings.HeightDuration)
                    targetHeight = JumpSettings.CapsuleHeight;
                else if (_isRolling && _rollTimer < RollSettings.HeightDuration)
                    targetHeight = RollSettings.CapsuleHeight;
                else if (_isCrouching)
                    targetHeight = CrouchHeight;
                else if (IsInLowCover)
                {
                    if (!IsAiming)
                        targetHeight = CoverSettings.LowCapsuleHeight;
                    else if (!_isAimingThroughCoverPlane)
                        targetHeight = CoverSettings.LowAimCapsuleHeight;
                }

                return targetHeight;
            }
        }

        /// <summary>
        /// Returns true if the character has a grenade in a hand
        /// </summary>
        public bool HasGrenadeInHand
        {
            get { return _isGrenadeTakenOut || _isThrowing || _isInGrenadeAnimation; }
        }

        /// <summary>
        /// Returns true if the character has a grenade in hand and ready to throw it.
        /// </summary>
        public bool IsReadyToThrowGrenade
        {
            get { return _isGrenadeTakenOut && !_isThrowing && !_hasThrown; }
        }

        /// <summary>
        /// Is the motor currently throwing or has just thrown a grenade.
        /// </summary>
        public bool IsThrowingGrenade
        {
            get { return _isThrowing || _hasThrown; }
        }

        /// <summary>
        /// Grenade that would be potentially displayed in a hand if thrown.
        /// </summary>
        public Grenade PotentialGrenade
        {
            get
            {
                GameObject obj = null;

                if (IsThrowingLeft)
                    obj = Grenade.Left;
                else
                    obj = Grenade.Right;

                if (obj != null)
                    return obj.GetComponent<Grenade>();
                else
                    return null;
            }
        }

        /// <summary>
        /// Returns currently displayed grenade object in a hand.
        /// </summary>
        public Grenade CurrentGrenade
        {
            get
            {
                GameObject obj = null;

                if (_rightGrenade != null && _rightGrenade.activeSelf)
                    obj = _rightGrenade;
                else if (_leftGrenade != null && _leftGrenade.activeSelf)
                    obj = _leftGrenade;
                else
                    return null;

                return obj.GetComponent<Grenade>();
            }
        }

        /// <summary>
        /// Returns true if in current situation grenades would be thrown with the left hand.
        /// </summary>
        public bool IsThrowingLeft
        {
            get
            {
                if (_isThrowing)
                    return _isGoingToThrowLeft;

                var shouldThrowLeft = _cover.In && _cover.IsStandingLeft;
                return (shouldThrowLeft && (Grenade.Left != null || _leftGrenade != null)) || (Grenade.Right == null && _rightGrenade == null);
            }
        }

        /// <summary>
        /// Returns cover object the character is closest to and able to take.
        /// </summary>
        public Cover PotentialCover
        {
            get { return _potentialCover; }
        }

        /// <summary>
        /// Is the character currently moving.
        /// </summary>
        public bool IsMoving
        {
            get { return _isMoving; }
        }

        /// <summary>
        /// Current movement speed.
        /// </summary>
        public float Movement
        {
            get { return _localMovement.magnitude; }
        }

        /// <summary>
        /// Accuracy error produced by movement.
        /// </summary>
        public float MovementError
        {
            get
            {
                var movement = Movement;

                if (movement < 0.5f)
                    return movement * 2 * AimSettings.WalkError;
                else if (movement < 1)
                    return Mathf.Lerp(AimSettings.WalkError, AimSettings.RunError, (movement - 0.5f) * 2);
                else if (movement < 2)
                    return Mathf.Lerp(AimSettings.RunError, AimSettings.SprintError, movement - 1);
                else
                    return AimSettings.SprintError;
            }
        }

        #endregion

        #region Public fields

        /// <summary>
        /// Controls wheter the character is in a state of death. Dead characters have no collisions and ignore any input.
        /// </summary>
        [Tooltip("Controls wheter the character is in a state of death.")]
        public bool IsAlive = true;

        /// <summary>
        /// Speed multiplier for the movement speed. Adjusts animations.
        /// </summary>
        [Tooltip("Speed multiplier for the movement speed. Adjusts animations.")]
        public float Speed = 1.0f;

        /// <summary>
        /// Multiplies movement speed without adjusting animations.
        /// </summary>
        [Tooltip("Multiplies movement speed without adjusting animations.")]
        public float MoveMultiplier = 1.0f;

        /// <summary>
        /// Toggles character's ability to run. Used by the CharacterStamina.
        /// </summary>
        [Tooltip("Toggles character's ability to run. Used by the CharacterStamina.")]
        public bool CanRun = true;

        /// <summary>
        /// Toggles character's ability to run. Used by the CharacterStamina.
        /// </summary>
        [Tooltip("Toggles character's ability to run. Used by the CharacterStamina.")]
        public bool CanSprint = true;

        /// <summary>
        /// Distance below feet to check for ground.
        /// </summary>
        [Tooltip("Distance below feet to check for ground.")]
        [Range(0, 1)]
        public float GroundThreshold = 0.3f;

        /// <summary>
        /// Minimal height to trigger state of falling. It’s ignored when jumping over gaps.
        /// </summary>
        [Tooltip("Minimal height to trigger state of falling. It’s ignored when jumping over gaps.")]
        [Range(0, 10)]
        public float FallThreshold = 2.0f;

        /// <summary>
        /// Movement to obstacles closer than this is ignored. 
        /// It is mainly used to prevent character running into walls.
        /// </summary>
        [Tooltip("Movement to obstacles closer than this is ignored.")]
        [Range(0, 2)]
        public float ObstacleDistance = 0.05f;

        /// <summary>
        /// Aiming in front of obstacles closer than this distance is ignored.
        /// </summary>
        [Tooltip("Aiming in front of obstacles closer than this distance is ignored.")]
        [Range(0, 5)]
        public float AimObstacleDistance = 0.4f;

        /// <summary>
        /// Gravity force applied to this character.
        /// </summary>
        [Tooltip("Gravity force applied to this character.")]
        public float Gravity = 10;

        /// <summary>
        /// Degrees recovered per second after a recoil.
        /// </summary>
        [Tooltip("Degrees recovered per second after a recoil.")]
        public float RecoilRecovery = 17;

        /// <summary>
        /// Sets the origin of bullet raycasts, either a camera or an end of a gun.
        /// </summary>
        [Tooltip("Sets the origin of bullet raycasts, either a camera or an end of a gun.")]
        public bool IsFiringFromCamera = true;

        /// <summary>
        /// Gun accuracy increase when zooming in. Multiplier gun error.
        /// </summary>
        [Tooltip("Gun accuracy increase when zooming in. Multiplier gun error.")]
        public float ZoomErrorMultiplier = 0.75f;

        /// <summary>
        /// Capsule height when crouching.
        /// </summary>
        [Tooltip("Capsule height when crouching.")]
        public float CrouchHeight = 1.5f;

        /// <summary>
        /// How long it takes for the animator to go from standing to full speed when issued a move command.
        /// </summary>
        [Tooltip("How long it takes for the animator to go from standing to full speed when issued a move command.")]
        public float AccelerationDamp = 1;

        /// <summary>
        /// How much the animator keeps moving after the character stops getting move commands.
        /// </summary>
        [Tooltip("How much the animator keeps moving after the character stops getting move commands.")]
        public float DeccelerationDamp = 3;

        /// <summary>
        /// Damage multiplier for weapons.
        /// </summary>
        [Tooltip("Damage multiplier for weapons.")]
        public float DamageMultiplier = 1;

        /// <summary>
        /// Should the character hold the weapon in their hands. Change is not immediate.
        /// </summary>
        [Tooltip("Should the character hold the weapon in their hands. Change is not immediate.")]
        public bool IsEquipped = true;

        /// <summary>
        /// Weapon description of the weapon the character is to equip.
        /// </summary>
        [Tooltip("Weapon description of the weapon the character is to equip.")]
        public WeaponDescription Weapon;

        /// <summary>
        /// Deprecated. Use Upgrade Weapon List button to update to the newer version of the asset.
        /// </summary>
        [Tooltip("Deprecated. Use Upgrade Weapon List button to update to the newer version of the asset.")]
        [Obsolete("Deprecated. Use Upgrade Weapon List button to update to the newer version of the asset.")]
        [HideInInspector]
        public WeaponDescription[] Weapons;

        /// <summary>
        /// Grenade settings.
        /// </summary>
        public GrenadeSettings Grenade = GrenadeSettings.Default();

        /// <summary>
        /// IK settings for the character.
        /// </summary>
        [Tooltip("IK settings for the character.")]
        public IKSettings IK = IKSettings.Default();

        /// <summary>
        /// Settings for cover behaviour.
        /// </summary>
        [Tooltip("Settings for cover behaviour.")]
        public CoverSettings CoverSettings = CoverSettings.Default();

        /// <summary>
        /// Settings for climbing.
        /// </summary>
        [Tooltip("Settings for climbing.")]
        public ClimbSettings ClimbSettings = ClimbSettings.Default();

        /// <summary>
        /// Settings for climbing.
        /// </summary>
        [Tooltip("Settings for climbing.")]
        public VaultSettings VaultSettings = VaultSettings.Default();

        /// <summary>
        /// Settings for jumping.
        /// </summary>
        [Tooltip("Settings for jumping.")]
        public JumpSettings JumpSettings = JumpSettings.Default();

        /// <summary>
        /// Settings for rolling.
        /// </summary>
        [Tooltip("Settings for rolling.")]
        public RollSettings RollSettings = RollSettings.Default();

        /// <summary>
        /// Settings for aiming.
        /// </summary>
        [Tooltip("Settings for aiming.")]
        public AimSettings AimSettings = AimSettings.Default();

        /// <summary>
        /// Settings for turning.
        /// </summary>
        [Tooltip("Settings for turning.")]
        public TurnSettings TurnSettings = TurnSettings.Default();

        /// <summary>
        /// Settings for camera pivot positions based on shoulders.
        /// </summary>
        [Tooltip("Settings for camera pivot positions based on shoulders.")]
        public ShoulderSettings ShoulderSettings = ShoulderSettings.Default();

        /// <summary>
        /// Settings for hit response IK.
        /// </summary>
        [Tooltip("Settings for hit response IK.")]
        public HitResponseSettings HitResponseSettings = HitResponseSettings.Default();

        #endregion

        #region Actions

        /// <summary>
        /// Executed when the normal standing height changes.
        /// </summary>
        public Action<float> StandingHeightChanged;

        /// <summary>
        /// Executed when the current height changes.
        /// </summary>
        public Action<float> CurrentHeightChanged;

        /// <summary>
        /// Executed when the weapon change starts.
        /// </summary>
        public Action WeaponChangeStarted;

        /// <summary>
        /// Executed when a weapon change stops and the character has a new weapon armed.
        /// </summary>
        public Action WeaponChanged;

        /// <summary>
        /// Executed after the character stars firing.
        /// </summary>
        public Action FireStarted;

        /// <summary>
        /// Executed after the character stops firing.
        /// </summary>
        public Action FireStopped;

        /// <summary>
        /// Executed when the weapon reload starts.
        /// </summary>
        public Action ReloadStarted;

        /// <summary>
        /// Executed after a weapon is fully laoded.
        /// </summary>
        public Action FullyLoaded;

        /// <summary>
        /// Executed after a bullet is loaded.
        /// </summary>
        public Action BulletLoaded;

        /// <summary>
        /// Executed after a weapon is pumped.
        /// </summary>
        public Action Pumped;

        /// <summary>
        /// Executed after a weapon successfully hit's something.
        /// </summary>
        public Action<Hit> SuccessfullyHit;

        /// <summary>
        /// Executed on character entering a cover. Invoked on any cover change as well.
        /// </summary>
        public Action<Cover> EnteredCover;

        /// <summary>
        /// Executed on character leaving a cover.
        /// </summary>
        public Action ExitedCover;

        /// <summary>
        /// Executed after the character changes from dead to alive.
        /// </summary>
        public Action Resurrected;

        /// <summary>
        /// Executed on character death.
        /// </summary>
        public Action Died;

        /// <summary>
        /// Executed after a tool was used.
        /// </summary>
        public Action UsedTool;

        /// <summary>
        /// Executed after an alternative mode of a tool was used.
        /// </summary>
        public Action UsedToolAlternate;

        /// <summary>
        /// Executed on any jump.
        /// </summary>
        public Action Jumped;

        /// <summary>
        /// Executed on every footstep.
        /// </summary>
        public Action Stepped;

        /// <summary>
        /// Executed on character landing after a fall.
        /// </summary>
        public Action Landed;

        /// <summary>
        /// Executed when the character starts zooming in.
        /// </summary>
        public Action Zoomed;

        /// <summary>
        /// Executed after the character stops zooming in.
        /// </summary>
        public Action Unzoomed;

        #endregion

        #region Private fields

        enum WalkMode
        {
            none,
            walk,
            run,
            sprint
        }

        private WalkMode _lastWalkMode;

        private bool _hasRegistered;

        private CapsuleCollider _capsule;
        private Rigidbody _body;
        private Animator _animator;
        private Visibility[] _visibility;
        private Actor _actor;

        private CharacterCamera _cameraToLateUpdate;

        private CharacterIK _ik;

        private CoverState _cover;
        private Cover _potentialCover;
        private Cover _lastNotifiedCover;

        private Renderer[] _renderers;
        private int _targetLayer;

        private Vector3 _lastKnownPosition;
        private float _previousCapsuleHeight;

        private bool _isMoving;
        private bool _isMovingAwayFromCover;
        private Vector3 _moveFromCoverPosition;
        private Vector3 _moveFromCoverDirection;

        private bool _isWeaponBlocked;

        private bool _hasEnteredOffsetAnimation;
        private bool _coverOffsetSideIsRight;
        private bool _canAimInThisOffsetAnimationOrIsAtTheEndOfIt;

        private bool _isGrenadeTakenOut = false;
        private bool _isThrowing = false;
        private bool _hasBeganThrowAnimation = false;
        private bool _isGoingToThrowLeft;
        private bool _hasThrown = false;
        private float _throwBodyAngle;
        private float _throwAngle;
        private Vector3 _throwTarget;
        private Vector3 _throwVelocity;
        private Vector3 _throwOrigin;
        private bool _isInGrenadeAnimation = false;

        private bool _hasCrouchCover;
        private Vector3 _crouchCoverPosition;

        private bool _wantsToRotateSmoothly;
        private bool _wantsToCrouchNearCovers;

        private bool _stopAimingWhenEnteringCover;

        private bool _isPerformingCustomAction;

        private bool _isInProcess;
        private CharacterProcess _process;

        private bool _isGrounded = true;
        private bool _wasGrounded;
        private bool _isFalling;

        private float _verticalRecoil;
        private float _horizontalRecoil;

        private bool _wasAimingGun;

        private bool _wantsToZoom;
        private bool _wantedToZoom;
        private bool _wantsToScope;
        private bool _wantedToScope;

        private bool _wantsToMirror;

        private bool _isAimingThroughCoverPlane;
        private float _coverPlaneAngle;
        private bool _hasCoverPlaneAngle;

        private bool _needToEnterCoverByWalkingToIt;

        private bool _immediateAim;
        private bool _immediateIdle;
        private float _aimTimer;

        private bool _hasAimTarget;
        private float _bodyTurnSpeed = 10;
        private Vector3 _bodyTarget;
        private Vector3 _aimTarget;
        private Vector3 _currentBodyTarget;

        private float _horizontalAngle;
        private float _verticalAngle;
        private float _horizontalAngleDiff;
        private bool _wouldTurnImmediately;

        private float _currentAnimatedAngle;
        private float _stepCursor;
        private float _currentStep;
        private float _minAnimatedStep = 20;
        private float _maxAnimatedStep = 90;
        private float _stepDuration = 0.15f;

        private bool _wantsToTakeCover;
        private bool _wantsToImmediatelyUpdatePotentialCover;
        private bool _wasMovingInCover;

        private CoverAimState _coverAim;

        private Vector3 _coverOffset;
        private Vector3 _coverOffsetSide;
        private Vector3 _coverOffsetBack;
        private Vector3 _coverOffsetSideTarget;
        private Vector3 _coverOffsetBackTarget;
        private CoverOffsetState _sideOffset;
        private CoverOffsetState _backOffset;
        private Vector3 _initialOffsetPosition;

        private WeaponDescription _equippedWeapon;
        private WeaponEquipState _weaponEquipState;
        private bool _isUnequippedButGoingToGrab;
        private float _weaponGrabTimer;

        private float _movementInput = 0;

        private float _coverTime = 0;
        private float _coverUpdateTimer = 0;

        private bool _isSprinting = false;
        private bool _wantsToSprint = false;
        private bool _useSprintingAnimation = false;
        private float _sprintAnimationOffDelay = 0;

        private bool _isClimbing = false;
        private Vector3 _climbDirection;
        private bool _hasBeganClimbing = false;
        private bool _isClimbingAVault = false;
        private Cover _wantsToClimbCover;
        private float _climbHeight = 0;
        private float _climbAngle = 0;
        private float _climbTime = 0;
        private Vector3 _climbOffset;
        private float _finalClimbHeight;
        private float _ignoreFallTimer = 0;
        private float _normalizedClimbTime = 0;
        private float _vaultPosition;

        private bool _isUsingWeapon = false;
        private bool _isUsingWeaponAlternate = false;
        private bool _keepUsingWeapon = false;
        private bool _wasWeaponUsed = false;

        private bool _isLoadingMagazine = false;
        private bool _isLoadingBullet = false;

        private bool _isPumping = false;
        private float _postPumpDelay;
        private bool _willPerformPump;
        private bool _willReloadAfterPump;
        private float _pumpWait;

        private bool _keepZoomingAndPotentiallyReloading;
        private bool _wasZoomingAndPotentiallyReloading;

        private bool _isJumping = false;
        private float _jumpAngle;
        private float _jumpForwardMultiplier;
        private bool _isIntendingToJump = false;
        private bool _wantsToJump = false;
        private float _nextJumpTimer = 0;
        private float _jumpLegTimer = 0;
        private float _jumpTimer = 0;
        private float _normalizedJumpTime = 0;

        private bool _isRolling = false;
        private bool _isIntendingToRoll = false;
        private float _rollAngle;
        private float _normalizedRollTime;
        private float _rollTimer = 0;

        private float _defaultCapsuleHeight = 2.0f;
        private float _defaultCapsuleCenter = 1.0f;

        private bool _isResurrecting;

        private CoverSearch _coverSearch = new CoverSearch();
        private CoverSearch _climbSearch = new CoverSearch();

        private Vector3 _localMovement = new Vector3(0, 0, 0);

        private float _postFireAimWait;

        private bool _isCrouching = false;
        private bool _wantsToCrouch = false;
        private CharacterMovement _inputMovement;
        private CharacterMovement _currentMovement;
        private bool _wantsToAim;
        private bool _wantsToAimWhenLeavingCover;
        private bool _wantsToFire;
        private bool _hasFireCondition;
        private int _fireConditionSide;
        private bool _dontChangeArmAimingJustYet = false;
        private bool _wantsToFaceInADirection;

        private int _lastFoot;

        private bool _needsToUpdateCoverBecauseOfDelay;

        private float _directionChangeDelay;

        private bool _wasAlive = true;

        private GameObject _target;

        private bool _isOnSlope = false;
        private Vector3 _groundNormal;
        private float _slope;

        private float _noMovementTimer;
        private float _groundTimer;
        private float _nogroundTimer;

        private bool _wasZooming;

        private GameObject _leftGrenade;
        private GameObject _rightGrenade;

        private bool _wantsToLiftArms;

        private static Vector3[] _grenadePath = new Vector3[64];

        #endregion

        #region Public methods

        /// <summary>
        /// Returns a potential climbable cover in given direction represented by a horizontal angle in world space.
        /// </summary>
        public Cover GetClimbambleInDirection(float angle)
        {
            return GetClimbableInDirection(Util.HorizontalVector(angle));
        }

        /// <summary>
        /// Returns a potential climbable cover in given direction.
        /// </summary>
        public Cover GetClimbableInDirection(Vector3 direction)
        {
            _climbSearch.Update(_cover,
                                transform.position,
                                _animator.GetBoneTransform(HumanBodyBones.Head).position,
                                0,
                                0,
                                CoverSettings.ClimbDistance,
                                _capsule.radius,
                                CoverSettings);

            return _climbSearch.FindClimbCoverInDirection(direction);
        }

        /// <summary>
        /// Sets the position for the character body to turn to.
        /// </summary>
        public void SetBodyTarget(Vector3 target, float speed = 8f)
        {
            _bodyTarget = target;

            if (!_hasAimTarget)
            {
                _aimTarget = _bodyTarget;
                _hasAimTarget = true;
            }

            _bodyTurnSpeed = speed;
            updateAngles();
        }

        /// <summary>
        /// Sets the position for the character to look and aim at.
        /// </summary>
        public void SetAimTarget(Vector3 target)
        {
            _aimTarget = target;
            _hasAimTarget = true;
        }

        #endregion

        #region Commands

        /// <summary>
        /// Suppossed to be called by the weapon use animation.
        /// </summary>
        public void OnUseWeapon()
        {
            if (!_isUsingWeapon)
                return;

            var weaponObject = EquippedWeapon.Item;
            _wasWeaponUsed = true;

            _isUsingWeapon = _keepUsingWeapon && EquippedWeapon.IsAContinuousTool(_isUsingWeaponAlternate);

            if (!_isUsingWeapon)
                endOfWeaponUse();
        }

        /// <summary>
        /// Supposed to be called by the grenade throw animation to release the grenade.
        /// </summary>
        public void OnThrow()
        {
            if (_isThrowing)
            {
                if (_isGoingToThrowLeft)
                {
                    throwGrenade(_leftGrenade);
                    _leftGrenade = null;
                }
                else
                {
                    throwGrenade(_rightGrenade);
                    _rightGrenade = null;
                }

                _isThrowing = false;
                _hasThrown = true;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Catch the end of an equip animation.
        /// </summary>
        public void OnEquip()
        {
            _weaponEquipState = WeaponEquipState.equipped;
            if (_equippedWeapon.Shield != null) _equippedWeapon.Shield.SetActive(true);

            SendMessage("OnWeaponChange", SendMessageOptions.DontRequireReceiver);
            if (WeaponChanged != null) WeaponChanged.Invoke();

            if (_equippedWeapon.Item != null && _isUsingWeapon)
            {
                if (_isUsingWeaponAlternate)
                    _equippedWeapon.Item.SendMessage("OnStartUsingAlternate", SendMessageOptions.DontRequireReceiver);
                else
                    _equippedWeapon.Item.SendMessage("OnStartUsing", SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// Catch the moment in the equip animation when the character has grabbed the weapon.
        /// </summary>
        public void OnGrabWeapon()
        {
            _equippedWeapon = Weapon;
            _weaponEquipState = WeaponEquipState.equipping;
            _isUnequippedButGoingToGrab = false;

            _weaponGrabTimer = 0.35f;

            if (_equippedWeapon.Item != null) _equippedWeapon.Item.SetActive(true);
            if (_equippedWeapon.Holster != null) _equippedWeapon.Holster.SetActive(false);

            if (_equippedWeapon.Gun != null)
                _equippedWeapon.Gun.CancelFire();
        }

        /// <summary>
        /// Catch the end of an unequip animation.
        /// </summary>
        public void OnUnequip()
        {
            _weaponEquipState = WeaponEquipState.unequipped;

            if (_equippedWeapon.Item != null) _equippedWeapon.Item.SetActive(false);
            if (_equippedWeapon.Holster != null) _equippedWeapon.Holster.SetActive(true);
            if (_equippedWeapon.Shield != null) _equippedWeapon.Shield.SetActive(false);
            _equippedWeapon = new WeaponDescription();
        }

        /// <summary>
        /// Catch the animation event at the end of the resurrection.
        /// </summary>
        public void OnResurrect()
        {
            IsAlive = true;
            _isResurrecting = false;
        }

        /// <summary>
        /// Catch a gun starting a series of bullets.
        /// </summary>
        public void OnStartGunFire()
        {
            if (FireStarted != null)
                FireStarted.Invoke();
        }

        /// <summary>
        /// Catch a gun stopping a series of bullets.
        /// </summary>
        public void OnStopGunFire()
        {
            if (FireStopped != null)
                FireStopped.Invoke();
        }

        /// <summary>
        /// Catch a successful bullet hit.
        /// </summary>
        public void OnSuccessfulHit(Hit hit)
        {
            if (SuccessfullyHit != null)
                SuccessfullyHit.Invoke(hit);
        }

        /// <summary>
        /// Catch custom input coming from the controller.
        /// </summary>
        public void OnCustomAction(string name)
        {
            if (_isPerformingCustomAction)
                return;

            _isPerformingCustomAction = true;
            _cover.Clear();
            clearPotentialCovers();

            _animator.SetTrigger(name);
        }

        /// <summary>
        /// Catch an end of a custom animation.
        /// </summary>
        public void OnFinishCustomAction()
        {
            _isPerformingCustomAction = false;
        }

        /// <summary>
        /// Sets IsAlive to false upon the character death.
        /// </summary>
        public void OnDead()
        {
            IsAlive = false;
        }

        /// <summary>
        /// Affects the character spine by a bullet hit.
        /// </summary>
        public void OnHit(Hit hit)
        {
            _ik.Hit(hit.Normal, HitResponseSettings.Strength, HitResponseSettings.Wait);
        }

        /// <summary>
        /// Catch the end of a bullet load animation.
        /// </summary>
        public void OnBulletLoad()
        {
            if (!_isLoadingBullet)
                return;

            _isLoadingBullet = false;
            var gun = EquippedWeapon.Gun;

            if (gun == null)
                return;

            if (!gun.LoadBullet())
                return;

            if (BulletLoaded != null)
                BulletLoaded.Invoke();

            if (gun.IsFullyLoaded)
            {
                if (FullyLoaded != null)
                    FullyLoaded.Invoke();

                if (gun.PumpAfterFinalLoad)
                    InputPump();
            }
            else if (gun.PumpAfterBulletLoad)
                InputPump();
            else
                loadBullet();
        }

        /// <summary>
        /// Catch the end of a magazine load animation or the load of a final bullet.
        /// </summary>
        public void OnMagazineLoad()
        {
            if (!_isLoadingMagazine)
                return;

            _isLoadingMagazine = false;
            var gun = EquippedWeapon.Gun;

            if (gun == null)
                return;

            if (gun.LoadMagazine())
                if (FullyLoaded != null)
                    FullyLoaded.Invoke();

            if (gun.PumpAfterFinalLoad)
                InputPump();
        }

        /// <summary>
        /// Catch the end of a weapon pump animation.
        /// </summary>
        public void OnPump()
        {
            if (!_isPumping)
                return;

            _postPumpDelay = 0.2f;
            _isPumping = false;
            if (Pumped != null) Pumped.Invoke();

            var gun = EquippedWeapon.Gun;

            if (gun == null)
                return;

            gun.SendMessage("OnPump", SendMessageOptions.DontRequireReceiver);

            if (!gun.IsFullyLoaded && !gun.ReloadWithMagazines && gun.PumpAfterBulletLoad)
            {
                _willReloadAfterPump = false;
                loadBullet();
            }
            else if (_willReloadAfterPump)
            {
                _willReloadAfterPump = false;
                InputReload();
            }
        }

        /// <summary>
        /// Catch the magazine eject event from a reload animation.
        /// </summary>
        public void OnEject()
        {
            var gun = EquippedWeapon.Gun;

            if (gun != null)
                gun.SendMessage("OnEject"); 
        }

        /// <summary>
        /// Catch the magazine rechamber event from a reload animation.
        /// </summary>
        public void OnRechamber()
        {
            var gun = EquippedWeapon.Gun;

            if (gun != null)
                gun.SendMessage("OnRechamber");
        }

        /// <summary>
        /// Execute the bullet load.
        /// </summary>
        private void loadBullet()
        {
            _isLoadingBullet = true;
            _animator.SetTrigger("LoadBullet");

            var gun = EquippedWeapon.Gun;

            if (gun != null)
                gun.SendMessage("OnBulletLoadStart");
        }

        #endregion

        #region Input

        /// <summary>
        /// Input gun recoil angles. Gun position offset is calculated from the given angles.
        /// </summary>
        public void InputRecoil(float vertical, float horizontal)
        {
            _verticalRecoil += vertical;
            _horizontalRecoil += horizontal;

            _verticalRecoil = Mathf.Clamp(_verticalRecoil, -30, 30);
            _horizontalRecoil = Mathf.Clamp(_horizontalRecoil, -30, 30);
        }

        /// <summary>
        /// Prevents the character from aiming at a tall cover wall.
        /// </summary>
        public void StopAimingWhenEnteringCover()
        {
            _stopAimingWhenEnteringCover = true;
        }

        /// <summary>
        /// Tells the motor to update the given camera after it's own LateUpdate finishes.
        /// </summary>
        public void AskForLateUpdate(CharacterCamera camera)
        {
            _cameraToLateUpdate = camera;
        }

        /// <summary>
        /// Should the character crouch near covers in the next frame.
        /// </summary>
        public void InputCrouchNearCover()
        {
            _wantsToCrouchNearCovers = true;
        }

        /// <summary>
        /// Smooth rotation only animates the legs, leaving upper body stable and not wobbling. Useful when the camera is zooming in.
        /// </summary>
        public void InputSmoothRotation()
        {
            _wantsToRotateSmoothly = true;
        }

        /// <summary>
        /// Tells the character to lift the weapon when running or standing in tall cover.
        /// </summary>
        public void InputArmLift()
        {
            _wantsToLiftArms = true;
        }

        /// <summary>
        /// Tells the character to switch arms holding the weapon. Used when aiming and standing in tall cover facing left.
        /// </summary>
        public void InputMirror()
        {
            _wantsToMirror = true;
        }

        /// <summary>
        /// Tells the character to resurrect and play the resurrection animation.
        /// </summary>
        public void InputResurrect()
        {
            if (!IsAlive && !_isResurrecting)
            {
                _isResurrecting = true;
                _animator.SetTrigger("Resurrect");
            }
        }

        /// <summary>
        /// Tells the character to start a process. An action is performed and the character returns back to the usual routine.
        /// </summary>
        public void InputProcess(CharacterProcess desc)
        {
            _isInProcess = true;
            _process = desc;

            if (desc.AnimationTrigger != null && desc.AnimationTrigger.Length > 0)
                _animator.SetTrigger(desc.AnimationTrigger);
        }

        /// <summary>
        /// Stops the current process.
        /// </summary>
        public void InputProcessEnd()
        {
            _isInProcess = false;
        }

        /// <summary>
        /// Set's the renderer layer for the next frame.
        /// </summary>        
        public void InputLayer(int value)
        {
            _targetLayer = value;
        }

        /// <summary>
        /// Tells the character to take a grenade in hands.
        /// </summary>
        public void InputTakeGrenade(GameObject grenadeOverride = null)
        {
            if (!_isThrowing && !_isGrenadeTakenOut)
            {
                recreateGrenades(grenadeOverride);

                _isGrenadeTakenOut = true;
                _animator.SetTrigger("TakeGrenade");
            }
        }

        /// <summary>
        /// Tells the character to put the grenade away.
        /// </summary>
        public void InputCancelGrenade()
        {
            _isGrenadeTakenOut = false;
        }

        /// <summary>
        /// Inputs a command to throw a grenade to the given target location.
        /// </summary>
        public void InputThrowGrenade(Vector3 target, GameObject grenadeOverride = null)
        {
            Grenade potentialGrenade;

            if (grenadeOverride != null)
                potentialGrenade = grenadeOverride.GetComponent<Grenade>();
            else
                potentialGrenade = PotentialGrenade;

            if (potentialGrenade == null)
                return;

            GrenadeDescription desc;
            desc.Gravity = Grenade.Gravity;
            desc.Duration = potentialGrenade.Timer;
            desc.Bounciness = potentialGrenade.Bounciness;

            var length = GrenadePath.Calculate(GrenadePath.Origin(this, Util.HorizontalAngle(target - transform.position)),
                                               target,
                                               Grenade.MaxVelocity,
                                               desc,
                                               _grenadePath,
                                               Grenade.Step);

            InputThrowGrenade(_grenadePath, length, Grenade.Step, grenadeOverride);
        }

        /// <summary>
        /// Calculates flight parameters given a path and launches the grenade.
        /// </summary>
        public void InputThrowGrenade(Vector3[] predictedPath, int predictedPathLength, float step, GameObject grenadeOverride = null)
        {
            if (predictedPathLength < 2)
                return;

            InputThrowGrenade(predictedPath[0], (predictedPath[1] - predictedPath[0]) / step, predictedPath[predictedPathLength - 1], grenadeOverride);
        }

        /// <summary>
        /// Tells the character to throw a grenade in the given path.
        /// </summary>
        public void InputThrowGrenade(Vector3 origin, Vector3 velocity, Vector3 target, GameObject grenadeOverride = null)
        {
            if (!_isThrowing)
            {
                _isGoingToThrowLeft = IsThrowingLeft;

                _wouldTurnImmediately = true;
                _isThrowing = true;
                _hasThrown = false;
                _isGrenadeTakenOut = false;
                _throwOrigin = origin;
                _throwVelocity = velocity;
                _throwTarget = target;
                _hasBeganThrowAnimation = false;

                _throwAngle = Util.HorizontalAngle(velocity);

                if (_cover.In && _cover.Main.IsFrontField(_throwAngle, 180))
                    _throwBodyAngle = _cover.FaceAngle(_isCrouching);
                else
                    _throwBodyAngle = _throwAngle;

                recreateGrenades(grenadeOverride);
            }
        }

        /// <summary>
        /// Inputs a command to roll in a specific direction.
        /// </summary>
        public void InputRoll(float angle)
        {
            if (_isRolling || _isIntendingToRoll)
                return;

            if (EquippedWeapon.IsHeavy)
                return;

            if (_cover.In)
            {
                if (_cover.Main.IsFrontField(angle, 160))
                    return;

                if (_cover.IsTall)
                {
                    _needToEnterCoverByWalkingToIt = true;
                    _cover.Clear();
                    _rollAngle = angle;
                }
                else
                    _rollAngle = angle;
            }
            else
                _rollAngle = angle;

            _isIntendingToRoll = true;
        }

        /// <summary>
        /// Inputs a command to jump.
        /// </summary>
        public void InputJump(float angle, float forwardMultiplier)
        {
            if (_isIntendingToJump || _isRolling)
                return;

            if (EquippedWeapon.IsHeavy)
                return;

            _jumpAngle = angle;
            _jumpForwardMultiplier = forwardMultiplier;
            _wantsToJump = true;
        }

        /// <summary>
        /// Inputs a command to climb or vault.
        /// </summary>
        public void InputClimbOrVault(Cover cover)
        {
            if (!EquippedWeapon.PreventClimbing)
                _wantsToClimbCover = cover;
        }

        /// <summary>
        /// Inputs a command to take cover.
        /// </summary>
        public void InputTakeCover()
        {
            _wantsToTakeCover = true;
        }

        /// <summary>
        /// Tells the motor to immediately update potential cover status.
        /// </summary>
        public void InputImmediateCoverSearch()
        {
            _wantsToImmediatelyUpdatePotentialCover = true;
        }

        /// <summary>
        /// Makes the motor ignore any cover in the following frame.
        /// </summary>
        public void InputLeaveCover()
        {
            _wantsToTakeCover = false;

            if (_cover.In)
                _cover.Clear();
        }

        /// <summary>
        /// Sets the character movement for the next update.
        /// </summary>
        public void InputMovement(CharacterMovement movement)
        {
            _inputMovement = movement;

            if (EquippedWeapon.IsHeavy && _inputMovement.Magnitude > 0.5f)
                _inputMovement.Magnitude = 0.5f;

            if (!CanSprint)
                _inputMovement.Magnitude = Mathf.Clamp(_inputMovement.Magnitude, 0, 1.0f);

            if (!CanRun)
                _inputMovement.Magnitude = Mathf.Clamp(_inputMovement.Magnitude, 0, 0.5f);

            _wantsToSprint = _inputMovement.IsSprinting;

            if (_wantsToSprint)
            {
                _useSprintingAnimation = true;
                _sprintAnimationOffDelay = 0.2f;
            }

            _isMoving = _inputMovement.IsMoving;
        }

        /// <summary>
        /// Sets the character to move forward during the next update.
        /// </summary>
        public void InputMoveForward(float strength = 1)
        {
            InputMovement(new CharacterMovement(Util.HorizontalVector(_horizontalAngle), 1));
        }

        /// <summary>
        /// Sets the character to move backwards during the next update.
        /// </summary>
        public void InputMoveBack(float strength = 1)
        {
            InputMovement(new CharacterMovement(Util.HorizontalVector(_horizontalAngle - 180), 1));
        }

        /// <summary>
        /// Sets the character to move left during the next update.
        /// </summary>
        public void InputMoveLeft(float strength = 1)
        {
            InputMovement(new CharacterMovement(Util.HorizontalVector(_horizontalAngle - 90), 1));
        }

        /// <summary>
        /// Sets the character to move right during the next update.
        /// </summary>
        public void InputMoveRight(float strength = 1)
        {
            InputMovement(new CharacterMovement(Util.HorizontalVector(_horizontalAngle + 90), 1));
        }

        /// <summary>
        /// Sets the character crouching state for the next update.
        /// </summary>
        public void InputCrouch()
        {
            _wantsToCrouch = true;
        }

        /// <summary>
        /// Sets the character to turn immediately if needed and allowed in the settings.
        /// </summary>
        public void InputPossibleImmediateTurn(bool value = true)
        {
            _wouldTurnImmediately = value;
        }

        /// <summary>
        /// Sets the character aim state for the next update.
        /// </summary>
        public void InputAim()
        {
            _wantsToAim = true;
        }

        /// <summary>
        /// Sets the character to avoid having a frame without aiming when leaving cover.
        /// </summary>
        public void InputAimWhenLeavingCover()
        {
            _wantsToAimWhenLeavingCover = true;
        }

        /// <summary>
        /// Sets the character up for zooming without a scope. Implicitly calls InputAim()
        /// </summary>
        public void InputZoom()
        {
            _wantsToAim = true;
            _wantsToZoom = true;
        }

        /// <summary>
        /// Sets the character up for zooming with a scope. 
        /// </summary>
        public void InputScope()
        {
            _wantsToAim = true;
            _wantsToZoom = true;
            _wantsToScope = true;
        }

        /// <summary>
        /// Sets the character to use the weapon as a tool in alternate mode.
        /// </summary>
        public void InputUseToolAlternate()
        {
            InputUseTool(true);
        }

        /// <summary>
        /// Sets the character to use the weapon as a tool.
        /// </summary>
        public void InputUseTool(bool isAlternate = false)
        {
            var weapon = EquippedWeapon;

            if (_isUsingWeapon)
            {
                _keepUsingWeapon = isAlternate == _isUsingWeaponAlternate;
                return;
            }

            if (weapon.IsNull)
                return;

            if (weapon.Gun != null)
            {
                InputFire();
                return;
            }

            if (_weaponEquipState != WeaponEquipState.equipping && _weaponEquipState != WeaponEquipState.equipped)
                return;

            _isUsingWeaponAlternate = isAlternate;
            _isUsingWeapon = true;
            _keepUsingWeapon = true;
            _wasWeaponUsed = false;

            if (weapon.Item != null)
            {
                if (_isUsingWeaponAlternate)
                    weapon.Item.SendMessage("OnStartUsingAlternate", SendMessageOptions.DontRequireReceiver);
                else
                    weapon.Item.SendMessage("OnStartUsing", SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// Sets the character state of firing for the next update.
        /// </summary>
        public void InputFire()
        {
            var weapon = EquippedWeapon;

            if (weapon.IsNull)
                return;

            if (weapon.Gun == null)
            {
                InputUseTool();
                return;
            }

            _wantsToFire = true;
            _hasFireCondition = false;
            InputAim();
        }

        /// <summary>
        /// Sets the character state of firing for the next update. Fires only if the target is not a friend.
        /// </summary>
        public void InputFireOnCondition(int ignoreSide)
        {
            var weapon = EquippedWeapon;

            if (weapon.IsNull)
                return;

            if (weapon.Gun == null)
            {
                InputUseTool();
                return;
            }

            _hasFireCondition = true;
            _fireConditionSide = ignoreSide;
            _wantsToFire = true;
            InputAim();
        }

        /// <summary>
        /// Attempts to start reloading a gun.
        /// </summary>
        public void InputReload()
        {
            var gun = EquippedWeapon.Gun;

            if (IsReloading || gun == null || !gun.CanLoad)
                return;

            if (_willPerformPump)
            {
                _willReloadAfterPump = true;
                return;
            }

            _willPerformPump = false;

            if (gun.ReloadWithMagazines || EquippedWeapon.Type != WeaponType.Rifle)
            {
                _isLoadingMagazine = true;
                _animator.SetTrigger("LoadMagazine");

                if (gun != null)
                    gun.SendMessage("OnMagazineLoadStart");
            }
            else
                loadBullet();
        }

        /// <summary>
        /// Attempts to start a weapon pump animation.
        /// </summary>
        public void InputPump(float delay = 0)
        {
            if (IsReloading || EquippedWeapon.Gun == null)
                return;

            if (delay <= float.Epsilon)
                performPump();
            else
            {
                _pumpWait = delay;
                _willPerformPump = true;
                _isPumping = false;
            }
        }

        /// <summary>
        /// Tells the character to face left relative to the cover.
        /// </summary>
        public void InputStandLeft()
        {
            _wantsToFaceInADirection = true;
            _cover.StandLeft();
        }

        /// <summary>
        /// Tells the character to face right relative to the cover.
        /// </summary>
        public void InputStandRight()
        {
            _wantsToFaceInADirection = true;
            _cover.StandRight();
        }

        /// <summary>
        /// Sets the character to enter aim animation immediately.
        /// </summary>
        public void InputImmediateAim()
        {
            InputAim();
            _immediateAim = true;
        }

        #endregion

        #region Behaviour

        private void OnEnable()
        {
            if (IsAlive)
            {
                _immediateIdle = true;

                Characters.Register(this);
                _hasRegistered = true;
            }
        }

        private void OnDisable()
        {
            Characters.Unregister(this);
            _hasRegistered = false;
        }

        private void Awake()
        {
            _capsule = GetComponent<CapsuleCollider>();
            _body = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            _actor = GetComponent<Actor>();

            _ik.Setup(this);

            _renderers = GetComponentsInChildren<Renderer>();
            _visibility = new Visibility[_renderers.Length];

            for (int i = 0; i < _visibility.Length; i++)
            {
                _visibility[i] = _renderers[i].GetComponent<Visibility>();

                if (_visibility[i] == null)
                    _visibility[i] = _renderers[i].gameObject.AddComponent<Visibility>();
            }

            _defaultCapsuleHeight = _capsule.height;
            _defaultCapsuleCenter = _capsule.center.y;
            _previousCapsuleHeight = _defaultCapsuleHeight;

            SetAimTarget(transform.position + transform.forward * 1000);

            if (Grenade.Left != null)
            {
                var collider = Grenade.Left.GetComponent<Collider>();

                if (collider != null)
                    Physics.IgnoreCollision(_capsule, collider, true);
            }

            if (Grenade.Right != null)
            {
                var collider = Grenade.Right.GetComponent<Collider>();

                if (collider != null)
                    Physics.IgnoreCollision(_capsule, collider, true);
            }

            SendMessage("OnStandingHeight", _defaultCapsuleHeight, SendMessageOptions.DontRequireReceiver);
            SendMessage("OnCurrentHeight", _defaultCapsuleHeight, SendMessageOptions.DontRequireReceiver);

            if (StandingHeightChanged != null) StandingHeightChanged.Invoke(_defaultCapsuleHeight);
            if (CurrentHeightChanged != null) CurrentHeightChanged.Invoke(_defaultCapsuleHeight);
        }

        private void LateUpdate()
        {
            _target = null;
            _isMoving = _currentMovement.IsMoving;

            if (IsAlive && !_hasRegistered)
            {
                _hasRegistered = true;
                Characters.Register(this);
            }
            else if (!IsAlive && _hasRegistered)
            {
                _hasRegistered = false;
                Characters.Unregister(this);
            }

            if (_weaponGrabTimer > float.Epsilon) _weaponGrabTimer -= Time.deltaTime;
            if (_postFireAimWait > float.Epsilon) _postFireAimWait -= Time.deltaTime;

            if (IsAlive)
            {
                var weapon = EquippedWeapon;

                if (weapon.Aiming == WeaponAiming.always)
                {
                    if (!_cover.In)
                        InputAim();
                    else
                        InputAimWhenLeavingCover();
                }
                else if (weapon.Aiming == WeaponAiming.alwaysImmediateTurn)
                {
                    if (!_cover.In)
                    {
                        InputAim();
                        InputPossibleImmediateTurn();
                    }
                    else
                        InputAimWhenLeavingCover();
                }

                _coverUpdateTimer += Time.deltaTime;
                _isCrouching = _wantsToCrouch && (!_cover.In || _cover.IsTall);

                if (_useSprintingAnimation)
                {
                    if (_sprintAnimationOffDelay > 0)
                        _sprintAnimationOffDelay -= Time.deltaTime;
                    else
                        _useSprintingAnimation = false;
                }

                if (_cover.In)
                    _coverTime += Time.deltaTime;
                else
                    _coverTime = 0;

                if (_postPumpDelay > float.Epsilon)
                    _postPumpDelay -= Time.deltaTime;

                if (_willPerformPump && !_isPumping)
                {
                    if (_pumpWait > float.Epsilon)
                    {
                        _pumpWait -= Time.deltaTime;

                        if (_pumpWait <= float.Epsilon)
                            performPump();
                    }
                }

                updateAngles();

                if (IsAlive && !_isClimbing)
                {
                    var force = new Vector3(0, Gravity, 0) * Time.deltaTime;

                    if (_noMovementTimer < 0.2f || !_isGrounded || _isOnSlope || _groundTimer < 0.2f)
                    {
                        if (_isOnSlope && _noMovementTimer > float.Epsilon && !_isJumping)
                            _body.velocity = Vector3.zero;
                        else if (_isGrounded && _jumpTimer < 0.1f && !_isOnSlope)
                            _body.velocity -= force * 2;
                        else
                            _body.velocity -= force;
                    }
                }

                if (EquippedWeapon.Gun != null && !IsInCover)
                {
                    var origin = VirtualHead;

                    _isWeaponBlocked = !Util.IsFree(gameObject,
                                                    origin,
                                                    (_aimTarget - origin).normalized,
                                                    IsZooming ? 0.75f : 1.2f,
                                                    false);
                }
                else
                    _isWeaponBlocked = false;

                updateWeapons();
                updateGrenade();
                updateSprinting();
                updateCoverPlane();

                if (_isInProcess && _process.LeaveCover)
                    clearPotentialCovers();

                if (_isPerformingCustomAction)
                    clearPotentialCovers();
                else if (_isClimbing)
                {
                    clearPotentialCovers();
                    updateClimb();
                }
                else
                    updateCommon();

                moveAndRotate(); 

                var isArmMirrored = false;

                if (IsInTallCover && !IsChangingWeapon)
                {
                    if (IsAiming)
                        isArmMirrored = _cover.IsStandingLeft;
                    else
                        isArmMirrored = _ik.HasSwitchedHands;
                }
                else if (_wantsToMirror)
                {
                    if ((IsAiming || _keepZoomingAndPotentiallyReloading || _wantsToZoom) && !IsChangingWeapon)
                        isArmMirrored = true;
                }

                if (isArmMirrored)
                {
                    _wasZoomingAndPotentiallyReloading = IsReloading;
                    _keepZoomingAndPotentiallyReloading = !_wantsToZoom || IsReloading;

                    mirror();
                }
                else
                    unmirror();

                if (!_wasZoomingAndPotentiallyReloading)
                    _keepZoomingAndPotentiallyReloading = false;

                if (!IsReloading && !_wantsToZoom)
                    _wasZoomingAndPotentiallyReloading = false;

                var anyVisibility = _visibility.Length == 0;

                for (int i = 0; i < _visibility.Length; i++)
                    if (_visibility[i].IsVisible)
                    {
                        anyVisibility = true;
                        break;
                    }

                if (anyVisibility || _targetLayer == Layers.Scope)
                    updateIK();

                if (EquippedWeapon.Gun != null)
                    EquippedWeapon.Gun.UpdateManually();

                if (Mathf.Abs(_movementInput) > float.Epsilon)
                    _noMovementTimer = 0;
                else if (_noMovementTimer < 1)
                    _noMovementTimer += Time.deltaTime;

                if (!_isGrounded)
                {
                    _groundTimer = 0;

                    if (_nogroundTimer < 1)
                        _nogroundTimer += Time.deltaTime;
                }
                else
                {
                    _nogroundTimer = 0;

                    if (_groundTimer < 1)
                        _groundTimer += Time.deltaTime;
                }

                if (_lastNotifiedCover != _cover.Main)
                {
                    _lastNotifiedCover = _cover.Main;

                    if (_lastNotifiedCover == null)
                    {
                        SendMessage("OnLeaveCover", SendMessageOptions.DontRequireReceiver);

                        if (ExitedCover != null)
                            ExitedCover.Invoke();
                    }
                    else
                    {
                        SendMessage("OnEnterCover", _lastNotifiedCover, SendMessageOptions.DontRequireReceiver);

                        if (EnteredCover != null)
                            EnteredCover.Invoke(_lastNotifiedCover);
                    }
                }
            }
            else
            {
                _isCrouching = false;
                _isWeaponBlocked = false;
                _body.velocity = Vector3.zero;
                updateGround();
            }

            updateCapsule();
            updateAnimator();

            _wantedToZoom = _wantsToZoom;
            _wantedToScope = _wantsToScope;

            _keepUsingWeapon = false;

            if (IsAiming)
                _aimTimer += Time.deltaTime;
            else
                _aimTimer = 0;

            foreach (var renderer in _renderers)
                if (renderer.gameObject.layer != _targetLayer)
                    renderer.gameObject.layer = _targetLayer;

            {
                var isZooming = IsZooming;

                if (isZooming && !_wasZooming)
                {
                    SendMessage("OnZoom", SendMessageOptions.DontRequireReceiver);
                    if (Zoomed != null) Zoomed.Invoke();
                }
                else if (!isZooming && _wasZooming)
                {
                    SendMessage("OnUnzoom", SendMessageOptions.DontRequireReceiver);
                    if (Unzoomed != null) Unzoomed.Invoke();
                }

                _wasZooming = isZooming;
            }

            var gun = EquippedWeapon.Gun;

            // Recoil recover
            if (gun != null)
            {
                var rate = gun.Recoil.RecoveryRate * RecoilRecovery * Time.deltaTime;

                if (_verticalRecoil > 0)
                {
                    if (_verticalRecoil > rate)
                        _verticalRecoil -= rate;
                    else
                        _verticalRecoil = 0;
                }
                else
                {
                    if (_verticalRecoil < -rate)
                        _verticalRecoil += rate;
                    else
                        _verticalRecoil = 0;
                }

                if (_horizontalRecoil > 0)
                {
                    if (_horizontalRecoil > rate)
                        _horizontalRecoil -= rate;
                    else
                        _horizontalRecoil = 0;
                }
                else
                {
                    if (_horizontalRecoil < -rate)
                        _horizontalRecoil += rate;
                    else
                        _horizontalRecoil = 0;
                }
            }
            else
            {
                _horizontalRecoil = 0;
                _verticalRecoil = 0;
            }

            _wasAimingGun = IsAimingGun;
            _targetLayer = Layers.Character;
            _wantsToAim = false;
            _wouldTurnImmediately = false;
            _wantsToAimWhenLeavingCover = false;
            _wantsToJump = false;
            _wantsToClimbCover = null;
            _wantsToTakeCover = false;
            _inputMovement = new CharacterMovement();
            _wantsToSprint = false;
            _wantsToCrouch = false;
            _wantsToFire = false;
            _wantsToFaceInADirection = false;
            _wantsToImmediatelyUpdatePotentialCover = false;
            _wantsToZoom = false;
            _wantsToScope = false;
            _wantsToRotateSmoothly = false;
            _wantsToLiftArms = false;
            _wantsToMirror = false;
            _wantsToCrouchNearCovers = false;
            _stopAimingWhenEnteringCover = false;

            {
                var isAlive = IsAlive;

                if (isAlive && !_wasAlive)
                    if (Resurrected != null) Resurrected.Invoke();

                if (!isAlive && _wasAlive)
                    if (Died != null) Died.Invoke();

                _wasAlive = isAlive;
            }

            if (_cameraToLateUpdate != null)
            {
                _cameraToLateUpdate.UpdateForCharacterMotor();
                _cameraToLateUpdate = null;
            }
        }

        #endregion

        #region Private methods

        private void updateCoverPlane()
        {
            var gun = EquippedWeapon.Gun;

            if (_cover.In && gun != null && !IsReloadingAndNotAiming)
            {
                var wasAimingThroughCoverPlane = _isAimingThroughCoverPlane;
                _isAimingThroughCoverPlane = false;

                var isAimingLowInLowCover = false;
                var isAimingHigh = false;

                {
                    var origin = AimOrigin;

                    var horizontalDirection = _bodyTarget - origin;
                    horizontalDirection.y = 0;
                    horizontalDirection.Normalize();

                    var coverTop = _cover.Main.Top - 0.1f;

                    if (coverTop < origin.y)
                        origin.y = coverTop;

                    if (_cover.Main.IsFrontField(_horizontalAngle, 180))
                    {
                        _isAimingThroughCoverPlane = Physics.RaycastNonAlloc(origin, horizontalDirection, Util.Hits, _capsule.radius * 4, 0x1, QueryTriggerInteraction.Ignore) > 0;

                        if (_isAimingThroughCoverPlane)
                        {
                            _hasCoverPlaneAngle = true;
                            _coverPlaneAngle = _horizontalAngle;
                        }
                    }

                    if (IsInLowCover)
                    {
                        if (_isAimingThroughCoverPlane || (gun.HasRaycastSetup && Vector3.Dot(_cover.Main.Forward, gun.RaycastOrigin - _cover.Main.transform.position - _coverOffset) > 0))
                        {
                            if ((_verticalAngle < -5 && wasAimingThroughCoverPlane) || (_verticalAngle < -8 && gun != null && gun.HasRaycastSetup && (gun.RaycastOrigin.y < _cover.Main.Top + 0.1f)))
                            {
                                isAimingHigh = true;
                                _isAimingThroughCoverPlane = true;
                                _hasCoverPlaneAngle = false;
                            }
                            else if (_cover.Main.IsFrontField(_horizontalAngle, 190 + Mathf.Clamp(_verticalAngle * 2, 0, 70)))
                            {
                                _isAimingThroughCoverPlane = true;

                                if (_backOffset == CoverOffsetState.Using || _backOffset == CoverOffsetState.Entering)
                                    isAimingLowInLowCover = _verticalAngle > 20;
                                else
                                    isAimingLowInLowCover = _verticalAngle > 25;

                                if (!isAimingLowInLowCover)
                                {
                                    _hasCoverPlaneAngle = true;
                                    _coverPlaneAngle = _horizontalAngle;
                                }
                            }
                        }
                    }
                }

                if (!_isAimingThroughCoverPlane)
                {
                    var delta = Mathf.DeltaAngle(_horizontalAngle, _coverPlaneAngle);

                    if (_hasCoverPlaneAngle && ((IsStandingLeftInCover && delta < 5) || (!IsStandingLeftInCover && delta > -5)) && !isAimingHigh && !isAimingLowInLowCover)
                        _isAimingThroughCoverPlane = true;
                    else
                        _hasCoverPlaneAngle = false;
                }

                if (_isAimingThroughCoverPlane && IsAimingGun)
                {
                    if (IsInLowCover)
                    {
                        if (isAimingLowInLowCover)
                            setCoverOffsets(false, true, Vector3.zero, -_cover.ForwardDirection * 0.4f);
                        else
                            setCoverOffsets(false, false, Vector3.zero, Vector3.zero);
                    }
                    else
                    {
                        const float sideBackOffset = 0.25f;

                        const float baseSideOffset = 0.25f;
                        const float maxSideOffset = 0.45f;
                        const float sideCornerAngle = 25f;

                        const float baseBackOffset = 0.4f;
                        const float maxBackOffset = 0.6f;

                        var shouldMoveBack = true;
                        var isCornerAiming = false;

                        var side = Vector3.zero;
                        var forward = Vector3.zero;

                        if (!_currentMovement.IsMoving)
                        {
                            float standBackDelta = IsZooming ? -10f : 0f;
                            const float adjacentCoverDelta = 30f;

                            if (IsStandingLeftInCover && 
                                IsByAnOpenLeftCorner && 
                                (_inputMovement.Magnitude < 0.5f || 
                                 _inputMovement.Direction.magnitude < 0.1f || 
                                 _cover.Main.IsLeft(Util.HorizontalAngle(_inputMovement.Direction), -5)))
                            {
                                if (!_cover.HasLeftAdjacent || Mathf.DeltaAngle(_cover.Main.Angle, _cover.LeftAdjacent.Angle) > -adjacentCoverDelta)
                                {
                                    var intensity = Mathf.Clamp01((sideCornerAngle - Mathf.DeltaAngle(_horizontalAngle, _cover.Main.Angle + sideCornerAngle)) / sideCornerAngle);

                                    side = -_cover.Main.Right * Mathf.Lerp(baseSideOffset, maxSideOffset, intensity);
                                    forward = -_cover.ForwardDirection * sideBackOffset;
                                    isCornerAiming = true;
                                }

                                if (!_cover.Main.IsLeft(_horizontalAngle, standBackDelta))
                                    shouldMoveBack = true;
                                else
                                    shouldMoveBack = !isCornerAiming;
                            }
                            else if (!IsStandingLeftInCover && 
                                     IsByAnOpenRightCorner && 
                                     (_inputMovement.Magnitude < 0.5f || 
                                      _inputMovement.Direction.magnitude < 0.1f || 
                                      _cover.Main.IsRight(Util.HorizontalAngle(_inputMovement.Direction), -5)))
                            {
                                if (!_cover.HasRightAdjacent || Mathf.DeltaAngle(_cover.Main.Angle, _cover.RightAdjacent.Angle) < adjacentCoverDelta)
                                {
                                    var intensity = Mathf.Clamp01((sideCornerAngle - Mathf.DeltaAngle(_cover.Main.Angle - sideCornerAngle, _horizontalAngle)) / sideCornerAngle);

                                    side = _cover.Main.Right * Mathf.Lerp(baseSideOffset, maxSideOffset, intensity);
                                    forward = -_cover.ForwardDirection * sideBackOffset;
                                    isCornerAiming = true;
                                }

                                if (!_cover.Main.IsRight(_horizontalAngle, standBackDelta))
                                    shouldMoveBack = true;
                                else
                                    shouldMoveBack = !isCornerAiming;
                            }
                        }

                        if (shouldMoveBack)
                        {
                            var intensity = Mathf.Clamp01((90f - Mathf.Abs(Mathf.DeltaAngle(_horizontalAngle, _cover.Main.Angle))) / 90f);
                            forward = -_cover.ForwardDirection * Mathf.Lerp(baseBackOffset, maxBackOffset, intensity);
                        }

                        setCoverOffsets(isCornerAiming, shouldMoveBack, side, forward);
                    }
                }
                else
                    setCoverOffsets(false, false, Vector3.zero, Vector3.zero);
            }
            else
            {
                _isAimingThroughCoverPlane = false;
                _hasCoverPlaneAngle = false;
                setCoverOffsets(false, false, Vector3.zero, Vector3.zero);
            }
        }

        private bool canTransitionToCoverOffset
        {
            get
            {
                var current = _animator.GetCurrentAnimatorStateInfo(0);

                if (current.IsName("Walking") || current.IsName("Cover"))
                    return true;
                else
                    return false;
            }
        }

        private void setCoverOffsets(bool side, bool back, Vector3 sideOffset, Vector3 backOffset)
        {
            if (_cover.Main == null)
            {
                _backOffset = CoverOffsetState.None;
                _sideOffset = CoverOffsetState.None;
                return;
            }

            if (IsMovingToCoverOffset || !canTransitionToCoverOffset)
            {
                if ((_backOffset == CoverOffsetState.Entering || _backOffset == CoverOffsetState.Using) && !back)
                {
                    _backOffset = CoverOffsetState.None;
                    _coverOffsetBack = backOffset;
                    _coverOffsetBackTarget = backOffset;
                }
                else if ((_backOffset == CoverOffsetState.Exiting || _backOffset == CoverOffsetState.None) && back)
                {
                    _backOffset = CoverOffsetState.Using;
                    _coverOffsetBack = backOffset;
                    _coverOffsetBackTarget = backOffset;
                }

                if ((_sideOffset == CoverOffsetState.Entering || _backOffset == CoverOffsetState.Using) && !side)
                {
                    _sideOffset = CoverOffsetState.None;
                    _coverOffsetSide = sideOffset;
                    _coverOffsetSideTarget = sideOffset;
                }
                else if ((_sideOffset == CoverOffsetState.Exiting || _sideOffset == CoverOffsetState.None) && side)
                {
                    _sideOffset = CoverOffsetState.Using;
                    _coverOffsetSide = sideOffset;
                    _coverOffsetSideTarget = sideOffset;
                }

                return;
            }

            if (IsMoving)
            {
                _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = true;
                _backOffset = back ? CoverOffsetState.Using : CoverOffsetState.None;
                _sideOffset = side ? CoverOffsetState.Using : CoverOffsetState.None;
                _coverOffsetBack = back ? backOffset : Vector3.zero;
                _coverOffsetSide = side ? sideOffset : Vector3.zero;
                _coverOffsetSideIsRight = _cover.IsStandingRight;
                _initialOffsetPosition = transform.position;
                return;
            }

            if (back && _backOffset == CoverOffsetState.None)
            {
                if (_sideOffset == CoverOffsetState.Using || IsInLowCover)
                {
                    _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = true;
                    _animator.SetInteger("CoverOffsetDirection", 1);
                    _animator.SetTrigger("StepBack");
                }
                else if (side)
                {
                    _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = false;
                    _animator.SetInteger("CoverOffsetDirection", 1);
                    _animator.SetTrigger("CornerAim");
                }
                else
                {
                    if (_coverAim.Step == AimStep.Aiming)
                    {
                        _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = true;
                        _animator.SetInteger("CoverOffsetDirection", 1);
                        _animator.SetTrigger("StepRight");
                    }
                    else
                    {
                        _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = false;
                        _animator.SetInteger("CoverOffsetDirection", 1);
                        _animator.SetTrigger("WallAim");
                    }
                }

                beginCoverOffsetTransition(side, back, sideOffset, backOffset);
            }
            else if (!back && _backOffset == CoverOffsetState.Using)
            {
                if (side || IsInLowCover)
                {
                    _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = true;
                    _animator.SetInteger("CoverOffsetDirection", -1);
                    _animator.SetTrigger("StepBack");
                }
                else
                {
                    if (_coverAim.Step == AimStep.Aiming && !_isAimingThroughCoverPlane)
                    {
                        _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = true;
                        _animator.SetInteger("CoverOffsetDirection", -1);
                        _animator.SetTrigger("StepRight");
                    }
                    else
                    {
                        _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = false;
                        _animator.SetInteger("CoverOffsetDirection", -1);
                        _animator.SetTrigger("WallAim");
                    }
                }

                beginCoverOffsetTransition(side, back, sideOffset, backOffset);
            }
            else if (side && _sideOffset == CoverOffsetState.None)
            {
                if (_wasAimingGun)
                {
                    _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = true;

                    if (_cover.Main.IsFrontField(_horizontalAngle, 90))
                    {
                        _animator.SetInteger("CoverOffsetDirection", 1);
                        _animator.SetTrigger("StepRight");
                    }
                    else
                    {
                        if (_coverOffsetSideIsRight)
                        {
                            if (_cover.Main.IsRight(_horizontalAngle))
                                _animator.SetInteger("CoverOffsetDirection", -1);
                            else
                                _animator.SetInteger("CoverOffsetDirection", 1);
                        }
                        else
                        {
                            if (_cover.Main.IsLeft(_horizontalAngle))
                                _animator.SetInteger("CoverOffsetDirection", -1);
                            else
                                _animator.SetInteger("CoverOffsetDirection", 1);
                        }

                        _animator.SetTrigger("StepBack");
                    }
                }
                else
                {
                    _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = false;
                    _animator.SetInteger("CoverOffsetDirection", 1);
                    _animator.SetTrigger("CornerAim");
                }

                beginCoverOffsetTransition(side, back, sideOffset, backOffset);
            }
            else if (!side && _sideOffset == CoverOffsetState.Using)
            {
                if (IsAiming)
                {
                    _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = true;

                    if (_cover.Main.IsFrontField(_horizontalAngle, 90))
                    {
                        _animator.SetInteger("CoverOffsetDirection", -1);
                        _animator.SetTrigger("StepRight");
                    }
                    else
                    {
                        if (_coverOffsetSideIsRight)
                        {
                            if (_cover.Main.IsRight(_horizontalAngle))
                                _animator.SetInteger("CoverOffsetDirection", 1);
                            else
                                _animator.SetInteger("CoverOffsetDirection", -1);
                        }
                        else
                        {
                            if (_cover.Main.IsLeft(_horizontalAngle))
                                _animator.SetInteger("CoverOffsetDirection", 1);
                            else
                                _animator.SetInteger("CoverOffsetDirection", -1);
                        }

                        _animator.SetTrigger("StepBack");
                    }
                }
                else
                {
                    _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = false;
                    _animator.SetInteger("CoverOffsetDirection", -1);
                    _animator.SetTrigger("CornerAim");
                }

                beginCoverOffsetTransition(side, back, sideOffset, backOffset);
            }
            else
            {
                if (_sideOffset == CoverOffsetState.Using && side)
                {
                    _coverOffsetSideTarget = sideOffset;
                    _coverOffsetSide = sideOffset;
                }

                if (_backOffset == CoverOffsetState.Using && back)
                {
                    _coverOffsetBackTarget = backOffset;
                    _coverOffsetBack = backOffset;
                }
            }
        }

        private void beginCoverOffsetTransition(bool side, bool back, Vector3 sideOffset, Vector3 forwardOffset)
        {
            _hasEnteredOffsetAnimation = false;
            _coverOffsetSideIsRight = _cover.IsStandingRight;
            _initialOffsetPosition = transform.position;

            if (side && _sideOffset == CoverOffsetState.None)
            {
                _sideOffset = CoverOffsetState.Entering;
                _coverOffsetSideTarget = sideOffset;
            }

            if (back && _backOffset == CoverOffsetState.None)
            {
                _backOffset = CoverOffsetState.Entering;
                _coverOffsetBackTarget = forwardOffset;
            }

            if (!side && _sideOffset == CoverOffsetState.Using) _sideOffset = CoverOffsetState.Exiting;
            if (!back && _backOffset == CoverOffsetState.Using) _backOffset = CoverOffsetState.Exiting;
        }

        private void updateAngles()
        {
            var vector = _bodyTarget - VirtualHead;
            _horizontalAngle = Util.HorizontalAngle(vector);
            _verticalAngle = Util.VerticalAngle(vector);
        }

        private void updateCapsule()
        {
            if (IsAlive)
            {
                if (_capsule.isTrigger)
                    _capsule.isTrigger = false;

                var off = _isClimbingAVault ? VaultSettings.CollisionOff : ClimbSettings.CollisionOff;
                var on = _isClimbingAVault ? VaultSettings.CollisionOn : ClimbSettings.CollisionOn;

                if (_isClimbing && _normalizedClimbTime >= off && _normalizedClimbTime < on && off < on)
                    _capsule.enabled = false;
                else
                {
                    if (!_capsule.enabled)
                        _groundTimer = 0;

                    _capsule.enabled = true;
                }

                _capsule.height = Util.Lerp(_capsule.height, TargetHeight, 10);
                _capsule.center = new Vector3(_capsule.center.x, _defaultCapsuleCenter - (_defaultCapsuleHeight - _capsule.height) * 0.5f, _capsule.center.z);

                if (_previousCapsuleHeight != _capsule.height)
                {
                    SendMessage("OnCurrentHeight", _capsule.height, SendMessageOptions.DontRequireReceiver);

                    if (CurrentHeightChanged != null)
                        CurrentHeightChanged.Invoke(_capsule.height);
                }
            }
            else
            {
                if (!_capsule.isTrigger)
                    _capsule.isTrigger = true;
            }
        }

        private void updateSprinting()
        {
            var state = _animator.GetCurrentAnimatorStateInfo(0);

            _isSprinting = state.IsName("Sprint") || state.IsName("Sprint Rifle");

            if (_isSprinting)
            {
                var next = _animator.GetNextAnimatorStateInfo(0);

                if (next.shortNameHash != 0 && !(next.IsName("Sprint") || next.IsName("Sprint Rifle")))
                    _isSprinting = false;
            }
        }

        private void updateRolling()
        {
            if (!_isRolling)
                _rollTimer = 0;

            if (EquippedWeapon.IsHeavy)
                _isIntendingToRoll = false;

            if (_isIntendingToRoll && !_isRolling && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, _rollAngle)) < 60)
            {
                _animator.SetTrigger("Roll");
                _isIntendingToRoll = false;
                _isRolling = true;
                _normalizedRollTime = 0;
            }
            else if (_isRolling)
            {
                var info = _animator.GetCurrentAnimatorStateInfo(0);
                _isRolling = info.IsName("Roll");

                if (_isRolling)
                    _normalizedRollTime = info.normalizedTime;
                else
                {
                    info = _animator.GetNextAnimatorStateInfo(0);
                    _isRolling = info.IsName("Roll");

                    if (_isRolling)
                        _normalizedRollTime = info.normalizedTime;
                }

                if (_rollTimer < 0.3f * Speed)
                    _isRolling = true;

                _rollTimer += Time.deltaTime;
            }
        }

        private void updateUse()
        {
            var weapon = EquippedWeapon;

            if (_isUsingWeapon && !_keepUsingWeapon && _wasWeaponUsed)
            {
                _isUsingWeapon = false;
                endOfWeaponUse();
            }

            if (!_isUsingWeapon)
                _coverAim.Leave();
            else if (weapon.IsAnAimableTool(_isUsingWeaponAlternate))
                _coverAim.CoverAim(_horizontalAngle);
        }

        private void hideGrenade(ref GameObject instantiated, GameObject grenade)
        {
            if (instantiated != null)
            {
                Destroy(instantiated);
                instantiated = null;
            }

            hideGrenade(grenade);
        }

        private void hideGrenade(GameObject grenade)
        {
            if (grenade != null && grenade.activeSelf)
                grenade.SetActive(false);
        }

        private void showGrenade(GameObject grenade)
        {
            if (grenade != null && !grenade.activeSelf)
                grenade.SetActive(true);
        }

        private void throwGrenade(GameObject grenade)
        {
            if (grenade == null)
                return;

            grenade.transform.SetParent(null, true);

            var body = grenade.GetComponent<Rigidbody>();
            if (body != null)
            {
                var forward = Util.HorizontalVector(_throwBodyAngle);

                body.isKinematic = false;
                body.velocity += (forward + Vector3.up).normalized * Grenade.MaxVelocity;
            }

            var component = grenade.GetComponent<Grenade>();
            if (component != null)
            {
                component.Activate(_actor);
                component.Fly(_throwOrigin, _throwVelocity, Grenade.Gravity);
            }
        }

        private GameObject cloneGrenade(GameObject grenade, GameObject location)
        {
            if (grenade == null)
                return null;

            var clone = GameObject.Instantiate(grenade);
            clone.transform.SetParent(location.transform.parent);
            clone.transform.position = location.transform.position;
            clone.transform.rotation = location.transform.rotation;
            clone.transform.localScale = location.transform.localScale;

            var collider = clone.GetComponent<Collider>();
            if (collider != null)
            {
                Physics.IgnoreCollision(_capsule, collider, true);
                Physics.IgnoreCollision(location.GetComponent<Collider>(), collider, true);
            }

            clone.SetActive(true);
            return clone;
        }

        private void recreateGrenades(GameObject grenadeOverride = null)
        {
            hideGrenade(ref _leftGrenade, Grenade.Left);
            hideGrenade(ref _rightGrenade, Grenade.Right);

            _leftGrenade = cloneGrenade(grenadeOverride != null ? grenadeOverride : Grenade.Left, Grenade.Left);
            _rightGrenade = cloneGrenade(grenadeOverride != null ? grenadeOverride : Grenade.Right, Grenade.Right);

            updateCloneGrenadeVisibility();
        }

        private void updateGrenade()
        {
            if (_isThrowing)
            {
                if (!_hasBeganThrowAnimation && (Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, _throwBodyAngle)) < 30))
                {
                    _hasBeganThrowAnimation = true;
                    _animator.SetTrigger("ThrowGrenade");
                }
            }
            else if (_isGrenadeTakenOut)
                updateCloneGrenadeVisibility();
            else
            {
                hideGrenade(ref _leftGrenade, Grenade.Left);
                hideGrenade(ref _rightGrenade, Grenade.Right);
            }

            _isInGrenadeAnimation = false;

            if (!_isInGrenadeAnimation)
            {
                var current = _animator.GetCurrentAnimatorStateInfo(GrenadeLayer);
                var next = _animator.GetNextAnimatorStateInfo(GrenadeLayer);

                if (!current.IsName("None") || !(next.shortNameHash == 0 || next.IsName("None")))
                    _isInGrenadeAnimation = true;
            }

            if (!_isInGrenadeAnimation)
            {
                var current = _animator.GetCurrentAnimatorStateInfo(GrenadeTopLayer);
                var next = _animator.GetNextAnimatorStateInfo(GrenadeTopLayer);

                if (!current.IsName("None") || !(next.shortNameHash == 0 || next.IsName("None")))
                    _isInGrenadeAnimation = true;
            }

            if (!_isInGrenadeAnimation && !_isThrowing)
            {
                if (_hasThrown)
                    updateCoverDirection(_throwBodyAngle);

                _hasThrown = false;
            }
        }

        private void performPump()
        {
            _isPumping = true;
            _willPerformPump = false;
            _animator.SetTrigger("Pump");

            var gun = EquippedWeapon.Gun;

            if (gun != null)
                gun.SendMessage("OnPumpStart");
        }

        private void updateCloneGrenadeVisibility()
        {
            if (IsThrowingLeft)
            {
                showGrenade(_leftGrenade);
                hideGrenade(_rightGrenade);
            }
            else
            {
                hideGrenade(_leftGrenade);
                showGrenade(_rightGrenade);
            }
        }

        private Vector3 lerpRelativePosition(Vector3 from, Vector3 to, float speed)
        {
            var current = from - transform.position;
            var next = to - transform.position;

            var currentLength = current.magnitude;
            var nextLength = next.magnitude;

            if (currentLength > float.Epsilon) current.Normalize();
            if (nextLength > float.Epsilon) next.Normalize();

            var vector = Util.Lerp(current, next, speed);
            var length = Util.Lerp(currentLength, nextLength, speed);

            return transform.position + vector * length;
        }

        private void moveAndRotate()
        {
            updateBodyAngleDiff();

            _currentBodyTarget = lerpRelativePosition(_currentBodyTarget, _bodyTarget, _bodyTurnSpeed);

            if (_cover.In)
                stickToCover();

            var animatorMovement = _animator.deltaPosition / Time.deltaTime;
            var animatorSpeed = animatorMovement.magnitude;

            if (!IsAlive)
            {
            }
            else if (IsMovingToCoverOffset)
            {
                var current = _animator.GetCurrentAnimatorStateInfo(0);
                var next = _animator.GetNextAnimatorStateInfo(0);

                var isAtEnd = false;

                var targetOffset = Vector3.zero;

                if (_sideOffset == CoverOffsetState.Entering || _sideOffset == CoverOffsetState.Using)
                    targetOffset += _coverOffsetSideTarget;

                if (_backOffset == CoverOffsetState.Entering || _backOffset == CoverOffsetState.Using)
                    targetOffset += _coverOffsetBackTarget;

                if (current.IsName("Walking") || current.IsName("Cover"))
                {
                    isAtEnd = _hasEnteredOffsetAnimation;

                    if (!isAtEnd)
                    {
                        var closest = getClosestCoverPosition();
                        transform.position = Vector3.Lerp(_initialOffsetPosition, closest + targetOffset, next.normalizedTime);
                    }
                }
                else
                {
                    var closest = getClosestCoverPosition();
                    transform.position = Vector3.Lerp(_initialOffsetPosition, closest + targetOffset, current.normalizedTime);

                    _hasEnteredOffsetAnimation = true;

                    if (!_canAimInThisOffsetAnimationOrIsAtTheEndOfIt)
                        _canAimInThisOffsetAnimationOrIsAtTheEndOfIt = current.normalizedTime > 0.5f;

                    if (current.normalizedTime > 0.75f)
                        isAtEnd = true;
                }

                if (!_canAimInThisOffsetAnimationOrIsAtTheEndOfIt || !IsAiming)
                    transform.Rotate(0, _animator.deltaRotation.eulerAngles.y, 0);
                else
                {
                    _horizontalAngleDiff = Mathf.DeltaAngle(transform.eulerAngles.y, _horizontalAngle);
                    turn(TurnSettings.StandingRotationSpeed);
                }

                if (isAtEnd)
                {
                    if (_sideOffset == CoverOffsetState.Entering)
                    {
                        _sideOffset = CoverOffsetState.Using;
                        _coverOffsetSide = _coverOffsetSideTarget;
                    }

                    if (_sideOffset == CoverOffsetState.Exiting)
                    {
                        _sideOffset = CoverOffsetState.None;
                        _coverOffsetSide = Vector3.zero;
                    }

                    if (_backOffset == CoverOffsetState.Entering)
                    {
                        _backOffset = CoverOffsetState.Using;
                        _coverOffsetBack = _coverOffsetBackTarget;
                    }

                    if (_backOffset == CoverOffsetState.Exiting)
                    {
                        _backOffset = CoverOffsetState.None;
                        _coverOffsetBack = Vector3.zero;
                    }

                    if (_sideOffset == CoverOffsetState.Using || _backOffset == CoverOffsetState.Using)
                        if (IsInTallCover)
                            _coverAim.ImmediateEnter();
                }
            }
            else if (_isClimbing)
            {
                var y = animatorMovement.y;
                animatorMovement *= (_isClimbingAVault ? VaultSettings.HorizontalScale : ClimbSettings.HorizontalScale);

                if (_isClimbingAVault)
                {
                    if (_normalizedClimbTime >= VaultSettings.PushOn && _normalizedClimbTime < VaultSettings.PushOff)
                        animatorMovement += _climbDirection * VaultSettings.Push;
                }
                else
                {
                    if (_normalizedClimbTime >= ClimbSettings.PushOn && _normalizedClimbTime < ClimbSettings.PushOff)
                        animatorMovement += _climbDirection * ClimbSettings.Push;
                }

                if (_isClimbingAVault)
                {
                    if (_normalizedClimbTime >= 0.5f && y < 0)
                        animatorMovement.y = _body.velocity.y - Gravity * Time.deltaTime;
                    else
                        animatorMovement.y = y * VaultSettings.VerticalScale;
                }
                else
                    animatorMovement.y = y * ClimbSettings.VerticalScale;

                _body.velocity = animatorMovement;

                const float turnStart = 0.3f;
                const float turnEnd = 0.9f;

                var scale = Mathf.Abs(Mathf.DeltaAngle(_climbAngle, _horizontalAngle)) / 180f;
                scale = Mathf.Clamp01((scale - 0.5f) * 2);

                var turnIntensity = (1 - scale * scale) * Mathf.Clamp01((_normalizedClimbTime - turnStart) / (turnEnd - turnStart));
                var angle = Mathf.LerpAngle(_climbAngle, _horizontalAngle, turnIntensity);

                transform.rotation = Util.Lerp(transform.rotation, Quaternion.Euler(0, angle, 0), 20);
            }
            else if (_isRolling || _isIntendingToRoll)
            {
                var speed = RollSettings.RotationSpeed;

                if (_isRolling && _normalizedRollTime > RollSettings.AnimationLock)
                    speed = TurnSettings.RunningRotationSpeed;

                turn(speed);

                if (_isRolling)
                {
                    animatorMovement.y = _body.velocity.y - Gravity * Time.deltaTime;

                    if (_potentialCover == null)
                        animatorMovement -= transform.right * Vector3.Dot(transform.right, animatorMovement);
                    else
                        animatorMovement -= _potentialCover.Forward * Vector3.Dot(_potentialCover.Forward, animatorMovement);

                    applyVelocityToTheGround(animatorMovement);
                }
            }
            else if ((_isJumping && _normalizedJumpTime < JumpSettings.AnimationLock) || _isIntendingToJump)
            {
                turn(JumpSettings.RotationSpeed);
            }
            else if (!_isGrounded)
            {
                turn(JumpSettings.RotationSpeed);
            }
            else
            {
                if (_isPerformingCustomAction ||
                     _isInProcess ||
                     (_wouldTurnImmediately && !isInAnimatedCover && (_aimTimer > 0.2f || !IsInTallCover)))
                {
                    transform.Rotate(0, _horizontalAngleDiff, 0);
                    _horizontalAngleDiff = 0;
                }
                else if (_isThrowing && !_hasThrown)
                    turn(TurnSettings.GrenadeRotationSpeed);
                else
                {
                    float manualSpeed;

                    if (_cover.In)
                        manualSpeed = IsInTallCover ? CoverSettings.TallRotationSpeed : CoverSettings.LowRotationSpeed;
                    else if (IsSprinting)
                        manualSpeed = TurnSettings.SprintingRotationSpeed;
                    else if (!EquippedWeapon.IsNull || _movementInput > 0.5f)
                        manualSpeed = TurnSettings.RunningRotationSpeed;
                    else
                        manualSpeed = TurnSettings.StandingRotationSpeed;

                    turn(manualSpeed);
                }

                if (_cover.In)
                {
                    if (animatorSpeed > float.Epsilon)
                    {
                        Vector3 direction;

                        if (Vector3.Dot(animatorMovement, _cover.Main.Left) > Vector3.Dot(animatorMovement, _cover.Main.Right))
                            direction = _cover.Main.Left;
                        else
                            direction = _cover.Main.Right;

                        applyVelocityToTheGround(MoveMultiplier * direction * Vector3.Dot(direction, animatorMovement) * Vector3.Dot(_currentMovement.Direction, animatorMovement / animatorSpeed));
                    }
                    else
                        _body.velocity = new Vector3(0, _body.velocity.y, 0);
                }
                else if (_movementInput > float.Epsilon)
                {
                    var speed = animatorMovement.magnitude;

                    if (speed > float.Epsilon)
                    {
                        var constraint = 1f;

                        var currentDirection = _currentMovement.Direction;
                        var magnitude = currentDirection.magnitude;

                        if (magnitude > float.Epsilon)
                            currentDirection /= magnitude;
                        else
                            constraint = 0;

                        var direction = Vector3.Lerp(animatorMovement / speed, currentDirection, constraint);

                        applyVelocityToTheGround(MoveMultiplier * direction * speed * _movementInput);
                    }
                }
                else
                    _body.velocity = Vector3.zero;
            }

            {
                var targetAngle = transform.eulerAngles.y + _horizontalAngleDiff;
                var fullDelta = Mathf.DeltaAngle(_currentAnimatedAngle, targetAngle);

                _stepCursor += Time.deltaTime / _stepDuration;

                float sign;

                if (fullDelta >= 0)
                    sign = 1;
                else
                    sign = -1;

                if (_currentStep * sign < 0)
                {
                    _currentStep = 0;
                    _stepCursor = 0;
                }

                var delta = fullDelta;

                if (delta * sign > _maxAnimatedStep)
                    delta = _maxAnimatedStep * sign;
                else if (delta * sign < _minAnimatedStep)
                {
                    _currentStep = 0;
                    _stepCursor = 0;
                    delta = 0;
                }

                if (_currentStep * sign < delta * sign)
                {
                    if (delta * sign > float.Epsilon)
                        _stepCursor *= _currentStep / delta;
                    else
                        _stepCursor = 0;

                    _currentStep = delta;
                }

                if (_stepCursor >= 1)
                {
                    _currentAnimatedAngle = targetAngle;
                    _stepCursor = 0;
                }
            }
        }

        private void applyVelocityToTheGround(Vector3 velocity)
        {
            velocity.y = 0;

            if (_isOnSlope && _isGrounded)
            {
                var right = Vector3.Cross(_groundNormal, Vector3.up);
                right.y = 0;

                if (right.sqrMagnitude > float.Epsilon)
                    right.Normalize();

                var result = Quaternion.AngleAxis(-_slope, right) * velocity;
                var speed = result.magnitude;

                if (speed > float.Epsilon)
                {
                    var angle = Mathf.Asin(result.y / speed) * Mathf.Rad2Deg;

                    const float start = 26f;
                    const float end = 35f;
                    const float diff = end - start;

                    if (angle >= start)
                    {
                        result = Quaternion.AngleAxis(-start, right) * velocity;
                        result *= 1.0f - Mathf.Clamp01((angle - start) / diff);
                    }

                    _body.velocity = result;
                }
                else
                    _body.velocity = Vector3.zero;
            }
            else
                _body.velocity = velocity;
        }

        private void turn(float speed)
        {
            var angle = Util.Lerp(0, _horizontalAngleDiff, speed * 0.3f);

            transform.Rotate(0, angle, 0);
            _horizontalAngleDiff -= angle;
        }

        private void clearPotentialCovers()
        {
            _potentialCover = null;
        }

        private bool wantsToAim
        {
            get
            {
                return (_cover.In && _coverAim.IsZoomed) ||
                       _wantsToFire ||
                       _isThrowing ||
                       _wantedToZoom ||
                       _wantsToAim ||
                       _postFireAimWait > float.Epsilon;
            }
        }

        private bool canFire
        {
            get
            {
                return !IsReloading && !IsChangingWeapon && IsGunReady && EquippedWeapon.Gun.LoadedBulletsLeft > 0;
            }
        }

        private void updateClimb()
        {
            if (_isClimbing)
            {
                // We want to update the speed information for smooth transitions.
                updateWalk();

                _climbTime += Time.deltaTime;

                var oldClimbOffset = _climbOffset;
                _climbOffset = Util.Lerp(_climbOffset, Vector3.zero, 100);
                transform.position += _climbOffset - oldClimbOffset;

                var info = _animator.GetCurrentAnimatorStateInfo(0);
                var vault = Animator.StringToHash("Vault");
                var climb = Animator.StringToHash("Climb");
                var climbStart = Animator.StringToHash("Climb start");

                if (info.shortNameHash == climb ||
                    info.shortNameHash == vault ||
                    info.shortNameHash == climbStart)
                {
                    var time = info.normalizedTime;

                    if (time > _normalizedClimbTime)
                        _normalizedClimbTime = time;

                    _hasBeganClimbing = true;
                }
                else if (_hasBeganClimbing)
                    _isClimbing = false;

                if (_climbTime > 5)
                    _isClimbing = false;

                _isIntendingToJump = false;
                _isJumping = false;
                _nextJumpTimer = 0;

                if (_normalizedClimbTime > 0.7f && !_isClimbingAVault)
                    transform.position = Util.Lerp(transform.position, new Vector3(transform.position.x, _finalClimbHeight, transform.position.z), 6);
                else if (_normalizedClimbTime > 0.5f && _isClimbingAVault && _body.velocity.y < 0)
                {
                    if (!findGround(FallThreshold) && !_isFalling)
                    {
                        _animator.SetTrigger("Fall");
                        _isFalling = true;
                        _isGrounded = false;

                        var velocity = _body.velocity;
                        var vector = transform.forward * Vector3.Dot(transform.forward, velocity);
                        _body.velocity = new Vector3(vector.x, velocity.y, vector.z);
                    }
                }
            }
            else
                _climbTime = 0;

            if (!_isFalling)
                updateGround();
        }

        private bool startClimb(Cover cover, bool isVault)
        {
            if (!_isClimbing)
            {
                _normalizedClimbTime = 0;
                _isClimbing = true;
                _isClimbingAVault = isVault;
                _hasBeganClimbing = false;
                _climbTime = 0;
                _climbAngle = cover.Angle;
                _climbDirection = cover.Forward;

                var perfect = cover.ClosestPointTo(transform.position + Vector3.up * 0.2f, -_capsule.radius, _capsule.radius * 0.8f - cover.Depth * 0.5f);
                var count = Physics.RaycastNonAlloc(perfect - cover.Forward * _capsule.radius, cover.Forward, Util.Hits, _capsule.radius * 2, 0x1, QueryTriggerInteraction.Ignore);

                for (int i = 0; i < count; i++)
                {
                    if (!Util.InHiearchyOf(Util.Hits[i].collider.gameObject, gameObject))
                    {
                        perfect = Util.Hits[i].point - cover.Forward * _capsule.radius * 0.8f;
                        break;
                    }
                }

                perfect.y = transform.position.y;
                _climbOffset = transform.position - perfect;
                _climbHeight = cover.Top - perfect.y;
                _finalClimbHeight = cover.Top;
                _vaultPosition = transform.position.y;

                _cover.Clear();

                return true;
            }

            return false;
        }

        private void updateCover(bool isForcedToMaintain)
        {
            if (_cover.In && IsMovingToCoverOffset)
                return;

            var wasInCover = _cover.In;

            var wasNearLeftCorner = false;
            var wasNearRightCorner = false;
            var wasAimingOutsideOfCover = false;

            if (_cover.In)
            {
                wasNearLeftCorner = IsNearLeftCorner;
                wasNearRightCorner = IsNearRightCorner;
            }
            else
            {
                _sideOffset = CoverOffsetState.None;
                _backOffset = CoverOffsetState.None;
                _coverOffset = Vector3.zero;
                _coverOffsetBack = Vector3.zero;
                _coverOffsetSide = Vector3.zero;
                wasAimingOutsideOfCover = IsAiming;
            }

            Vector3 position;

            if (_cover.In)
                position = transform.position - _coverOffset;
            else
                position = transform.position;

            var searchRadius = CoverSettings.EnterDistance + CoverSettings.CornerAimTriggerDistance;
            var head = _animator.GetBoneTransform(HumanBodyBones.Head).position;

            if (_isGrounded)
            {
                _coverSearch.Update(_cover,
                                    position,
                                    head,
                                    searchRadius,
                                    CoverSettings.MaxCrouchDistance,
                                    0,
                                    _capsule.radius,
                                    CoverSettings);
            }
            else
                _coverSearch.Clear();

            if (_cover.In && _isGrounded && (isForcedToMaintain || _inputMovement.IsMoving || !IsAiming))
                _cover.Maintain(_coverSearch, position);

            _potentialCover = null;

            if (_needToEnterCoverByWalkingToIt)
            {
                var isNewCover = _cover.Take(_coverSearch, position);

                if (!isNewCover)
                    _needToEnterCoverByWalkingToIt = false;

                var angle = Util.HorizontalAngle(_inputMovement.Direction);

                if (_cover.Main == null || _inputMovement.Magnitude < 0.5f || _inputMovement.Direction.magnitude < 0.01f || !_cover.Main.IsFrontField(angle, 160))
                    _cover.Clear();
                else if (_cover.HasLeftCorner &&
                         _cover.Main.IsLeft(angle) &&
                         Vector3.Dot(_cover.Main.Left, position - _cover.Main.LeftCorner(position.y)) > -0.1f)
                    _cover.Clear();
                else if (_cover.HasRightCorner &&
                         _cover.Main.IsRight(angle) &&
                         Vector3.Dot(_cover.Main.Right, position - _cover.Main.RightCorner(position.y)) > -0.1f)
                    _cover.Clear();
                else
                {
                    _coverOffset = Vector3.zero;
                    _needToEnterCoverByWalkingToIt = false;
                }
            }
            else if (_cover.In)
            {
                // Leave it as it is
            }
            else if (!_isClimbing && _isGrounded && _wantsToTakeCover)
            {
                var isNewCover = _cover.Take(_coverSearch, position);

                if (_cover.In && _wantsToClimbCover != null)
                    _cover.Clear();
                else
                {
                    if (isNewCover)
                    {
                        if (!_wantsToFaceInADirection && _cover.Main != null)
                        {
                            if (_cover.Main.IsLeft(transform.eulerAngles.y))
                                _cover.StandLeft();
                            else
                                _cover.StandRight();

                            _directionChangeDelay = CoverSettings.DirectionChangeDelay;
                        }

                        _coverOffset = Vector3.zero;
                        instantCoverAnimatorUpdate();
                    }
                }
            }
            else
                _potentialCover = _coverSearch.FindClosest();

            if (_cover.In)
            {
                _hasCrouchCover = !_cover.IsTall;
                _crouchCoverPosition = position;

                if (!wasInCover && _stopAimingWhenEnteringCover)
                {
                    _wantsToAim = false;
                    _coverAim.ImmediateLeave();
                }
                else if (wasAimingOutsideOfCover)
                    _coverAim.ImmediateEnter();

                _wantsToJump = false;
            }
            else
            {
                _hasCrouchCover = _wantsToCrouchNearCovers && !EquippedWeapon.IsNull && _coverSearch.FindClosestCrouchPosition(ref _crouchCoverPosition);
                _wasMovingInCover = false;
            }
        }

        private int weaponType
        {
            get
            {
                if (_weaponEquipState == WeaponEquipState.unequipped &&
                    _equippedWeapon != Weapon)
                {
                    if (Weapon.IsNull || !IsEquipped)
                        return 0;
                    else
                        return (int)Weapon.Type + 1;
                }
                else
                {
                    var weapon = EquippedWeapon;

                    if (weapon.IsNull)
                        return 0;
                    else
                        return (int)weapon.Type + 1;
                }
            }
        }

        private void unmirror()
        {
            if (_ik.HasSwitchedHands)
            {
                _ik.Unmirror();

                var direction = _animator.GetFloat("CoverDirection");
                _animator.SetFloat("CoverDirection", direction * -1);
            }
        }

        private void mirror()
        {
            if (_ik.HasSwitchedHands)
                return;

            var gun = EquippedWeapon.Gun;

            if (gun == null)
                return;

            if (gun != null)
            {
                _ik.Mirror(gun.transform);

                var direction = _animator.GetFloat("CoverDirection");
                _animator.SetFloat("CoverDirection", direction * -1);
            }
        }

        private void updateWeapons()
        {
            if ((_weaponEquipState == WeaponEquipState.equipped || _weaponEquipState == WeaponEquipState.unequipped) && 
                _isGrounded && 
                !IsReloading &&
                (IsEquipped ? Weapon : new WeaponDescription()) != _equippedWeapon)
            {
                if (_isUsingWeapon && _wasWeaponUsed)
                {
                    _isUsingWeapon = false;
                    _wasWeaponUsed = false;
                }

                if (!_isUsingWeapon)
                {
                    if (_weaponEquipState == WeaponEquipState.equipped)
                    {
                        unmirror();
                        _weaponEquipState = WeaponEquipState.unequipping;
                        _animator.SetTrigger("Unequip");

                        if (_equippedWeapon.Gun != null)
                            _equippedWeapon.Gun.CancelFire();
                    }
                    else if (!_isUnequippedButGoingToGrab)
                    {
                        unmirror();

                        _isUnequippedButGoingToGrab = true;
                        _equippedWeapon = new WeaponDescription();
                        _animator.SetTrigger("Equip");
                    }

                    SendMessage("OnWeaponChangeStart", SendMessageOptions.DontRequireReceiver);
                    if (WeaponChangeStarted != null) WeaponChangeStarted.Invoke();
                }
            }

            var gun = _equippedWeapon.Gun;

            if (gun != null)
            {
                gun.Character = this;

                var vector = _aimTarget - transform.position;
                vector.y = 0;
                vector.Normalize();

                gun.AddErrorThisFrame(MovementError);
                gun.SetBaseErrorMultiplierThisFrame(IsZooming ? ZoomErrorMultiplier : 1);
                gun.Allow(IsGunReady && !_isFalling && (!_cover.In || _coverAim.Step == AimStep.Aiming) && Vector3.Dot(vector, transform.forward) > 0.5f && _ik.IsAimingArms);
            }
        }

        private void updateAim()
        {
            _coverAim.Update();

            var wantsToAim = _wantsToAim;
            var weapon = EquippedWeapon;

            if (!weapon.IsNull && weapon.Type == WeaponType.Tool)
                wantsToAim = _isUsingWeapon && weapon.IsAnAimableTool(_isUsingWeaponAlternate);

            if (!_isClimbing && wantsToAim && IsGunScopeReady)
                _coverAim.IsZoomed = true;
            else
                _coverAim.IsZoomed = false;
        }

        private void updateFire()
        {
            if (_isClimbing)
                return;

            if (HasGrenadeInHand)
                return;

            if (wantsToAim)
            {
                if (IsGunReady)
                {
                    var canFire = !_isWeaponBlocked && !_isPumping && _postPumpDelay < float.Epsilon;
                    var gun = EquippedWeapon.Gun;

                    if (gun == null || gun.LoadedBulletsLeft == 0)
                        canFire = false;

                    if (canFire)
                    {
                        if (_wantsToFire)
                        {
                            if (_isLoadingBullet)
                            {
                                _isLoadingBullet = false;
                                _animator.SetTrigger("InterruptLoad");
                            }

                            _coverAim.Angle = _horizontalAngle;

                            if (_hasFireCondition)
                                gun.SetFireCondition(_fireConditionSide);
                            else
                                gun.CancelFireCondition();

                            if (_cover.In)
                            {
                                if (!gun.HasJustFired && !gun.IsAllowed)
                                    gun.FireWhenReady();
                                else
                                {
                                    gun.CancelFire();
                                    gun.TryFireNow();
                                }

                                if (!IsInTallCover || !_isAimingThroughCoverPlane || _backOffset == CoverOffsetState.Using || _sideOffset == CoverOffsetState.Using)
                                    _coverAim.CoverAim(_horizontalAngle);
                                else
                                    _coverAim.WaitAim(_horizontalAngle);
                            }
                            else
                            {
                                _coverAim.FreeAim(_horizontalAngle);

                                gun.CancelFire();
                                gun.TryFireNow();
                            }
                        }
                        else
                            _coverAim.CoverAim(_horizontalAngle);
                    }
                    else
                        _coverAim.Leave();
                }
                else if (!IsGunScopeReady)
                    _coverAim.Leave();
            }
            else
                _coverAim.Leave();
        }

        internal void KeepAiming()
        {
            _postFireAimWait = 0.15f;
        }

        public bool IsFree(Vector3 direction)
        {
            return IsFree(direction, ObstacleDistance, 0.1f);
        }

        public bool IsFree(Vector3 direction, float distance, float height, bool coverMeansFree = true)
        {
            return Util.IsFree(gameObject,
                               transform.position + new Vector3(0, _capsule.height * height, 0),
                               direction,
                               _capsule.radius + distance,
                               coverMeansFree);
        }

        private void updateCommon()
        {
            float requiredUpdateDelay;

            if (_inputMovement.IsMoving || _isRolling || Vector3.Distance(_lastKnownPosition, transform.position) > 0.1f)
            {
                _lastKnownPosition = transform.position;

                if (_cover.In)
                    requiredUpdateDelay = CoverSettings.Update.MovingCover;
                else
                    requiredUpdateDelay = CoverSettings.Update.MovingNonCover;
            }
            else
            {
                if (_cover.In)
                    requiredUpdateDelay = CoverSettings.Update.IdleCover;
                else
                    requiredUpdateDelay = CoverSettings.Update.IdleNonCover;
            }

            if (EquippedWeapon.PreventCovers)
                _cover.Clear();
            else if (_coverUpdateTimer >= requiredUpdateDelay - float.Epsilon || _needsToUpdateCoverBecauseOfDelay || _wantsToImmediatelyUpdatePotentialCover ||
                     (_wantsToTakeCover && _potentialCover != null))
            {
                if (_needsToUpdateCoverBecauseOfDelay)
                {
                    _needsToUpdateCoverBecauseOfDelay = false;
                    _wantsToTakeCover = true;
                }

                _coverUpdateTimer = UnityEngine.Random.Range(-0.05f, 0.1f);
                updateCover(false);
            }

            if (_isWeaponBlocked && _hasCrouchCover && wantsToAim)
                _hasCrouchCover = false;

            if (EquippedWeapon.PreventClimbing)
                _wantsToClimbCover = null;

            if (_wantsToClimbCover != null)
            {
                var climb = getClimb(_wantsToClimbCover);

                if (climb != CoverClimb.No)
                {
                    startClimb(_wantsToClimbCover, climb == CoverClimb.Vault);
                    _wantsToClimbCover = null;
                }
            }

            updateRolling();

            if (_isRolling && _normalizedRollTime < RollSettings.AnimationLock)
            {
                updateWalk();
                updateVertical();
                return;
            }

            updateAim();

            if (_isUsingWeapon)
                updateUse();
            else
                updateFire();

            if (_isInProcess && !_process.CanMove)
                updateGround();
            else
            {
                updateWalk();
                updateVertical();
            }
        }
    
        private void updateWalk()
        {
            Vector3 movement;

            if (_directionChangeDelay > float.Epsilon)
                _directionChangeDelay -= Time.deltaTime;

            _currentMovement = _inputMovement;

            if (EquippedWeapon.IsHeavy && _currentMovement.Magnitude > 0.5f)
                _currentMovement.Magnitude = 0.5f;

            if (_cover.In)
                _isMovingAwayFromCover = false;
            else if (_isMovingAwayFromCover)
            {
                if (_currentMovement.IsMoving)
                    _currentMovement.Direction = (_currentMovement.Direction + _moveFromCoverDirection).normalized;
                else
                {
                    _currentMovement.Direction = _moveFromCoverDirection;
                    _currentMovement.Magnitude = 0.5f;
                }

                if (Vector3.Dot(transform.position - _moveFromCoverPosition, _moveFromCoverDirection) > 0.5f)
                    _isMovingAwayFromCover = false;
            }

            if (_currentMovement.Direction.sqrMagnitude > 0.1f && _currentMovement.IsMoving)
            {
                var overallIntensity = 1.0f;

                var intendedWalkAngle = Util.HorizontalAngle(_currentMovement.Direction);

                if (_cover.In)
                {
                    if (_cover.HasLeftAdjacent &&
                        _cover.LeftAdjacent.IsFrontField(intendedWalkAngle, 360 - CoverSettings.ExitBack) &&
                        _cover.LeftAdjacent.IsLeft(intendedWalkAngle, 5) &&

                        ((IsAiming && IsNearLeftCorner && _cover.Main.IsLeft(intendedWalkAngle) && Vector3.Dot(_cover.Main.Left, _coverOffset) > 0.1f) ||
                         (Vector3.Distance(_cover.LeftAdjacent.ClosestPointTo(transform.position, 0, 0), transform.position) <= _capsule.radius + float.Epsilon) ||
                         Vector3.Distance(_cover.LeftAdjacent.ClosestPointTo(transform.position, 0, 0), transform.position) <= Vector3.Distance(_cover.Main.ClosestPointTo(transform.position, 0, 0), transform.position)))
                    {
                        if (_cover.LeftAdjacent.Angle > _cover.Main.Angle ||
                            (Mathf.Abs(Mathf.DeltaAngle(intendedWalkAngle, _cover.Main.Angle)) + 0.1f > Mathf.Abs(Mathf.DeltaAngle(intendedWalkAngle, _cover.LeftAdjacent.Angle))))
                        {
                            var wasLow = IsInLowCover;

                            _cover.MoveToLeftAdjacent();
                            _sideOffset = CoverOffsetState.None;
                            _coverOffsetSideTarget = Vector3.zero;
                            _coverOffsetSide = Vector3.zero;

                            if (wasLow && IsInTallCover)
                                if (_cover.Main.IsRight(_horizontalAngle, -30))
                                    _cover.StandRight();
                        }
                    }
                    else if (_cover.HasRightAdjacent &&
                             _cover.RightAdjacent.IsFrontField(intendedWalkAngle, 360 - CoverSettings.ExitBack) &&
                             _cover.RightAdjacent.IsRight(intendedWalkAngle, 5) &&

                             ((IsNearRightCorner && _cover.Main.IsRight(intendedWalkAngle) && Vector3.Dot(_cover.Main.Right, _coverOffset) > 0.1f) ||
                              (Vector3.Distance(_cover.RightAdjacent.ClosestPointTo(transform.position, 0, 0), transform.position) <= _capsule.radius + float.Epsilon) ||
                              Vector3.Distance(_cover.RightAdjacent.ClosestPointTo(transform.position, 0, 0), transform.position) <= Vector3.Distance(_cover.Main.ClosestPointTo(transform.position, 0, 0), transform.position)))
                    { 
                        if ((_cover.RightAdjacent.Angle < _cover.Main.Angle) ||
                            (Mathf.Abs(Mathf.DeltaAngle(intendedWalkAngle, _cover.Main.Angle)) + 0.1f > Mathf.Abs(Mathf.DeltaAngle(intendedWalkAngle, _cover.RightAdjacent.Angle))))
                        {
                            var wasLow = IsInLowCover;

                            _cover.MoveToRightAdjacent();
                            _sideOffset = CoverOffsetState.None;
                            _coverOffsetSideTarget = Vector3.zero;
                            _coverOffsetSide = Vector3.zero;

                            if (wasLow && IsInTallCover)
                                if (_cover.Main.IsLeft(_horizontalAngle, -30))
                                    _cover.StandLeft();
                        }
                    }
                    else if (!_cover.Main.IsFrontField(intendedWalkAngle, 360 - CoverSettings.ExitBack))
                        moveAwayFromCover(_currentMovement.Direction);
                    else if (IsAiming && _cover.Main.IsLeft(intendedWalkAngle) && IsNearLeftCorner && !_cover.HasLeftAdjacent)
                        moveAwayFromCover(_currentMovement.Direction);
                    else if (IsAiming && _cover.Main.IsRight(intendedWalkAngle) && IsNearRightCorner && !_cover.HasRightAdjacent)
                        moveAwayFromCover(_currentMovement.Direction);
                }

                if (_cover.In)
                {
                    _currentMovement.Magnitude = 1.0f;

                    if (_cover.Main.IsFrontField(intendedWalkAngle, 90))
                    {
                        overallIntensity = 0;
                    }
                    else if (IsFree(_currentMovement.Direction) && !IsAimingGun && !_wantsToFaceInADirection && !_isRolling && (_directionChangeDelay <= float.Epsilon || IsInTallCover))
                    {
                        if (_cover.Main.IsLeft(intendedWalkAngle, 0))
                        {
                            if (_cover.IsStandingRight)
                            {
                                _directionChangeDelay = CoverSettings.DirectionChangeDelay;
                                _cover.StandLeft();
                            }
                        }
                        else if (_cover.Main.IsRight(intendedWalkAngle, 0))
                        {
                            if (_cover.IsStandingLeft)
                            {
                                _directionChangeDelay = CoverSettings.DirectionChangeDelay;
                                _cover.StandRight();
                            }
                        }
                    }

                    if (_directionChangeDelay > float.Epsilon && !IsAiming)
                        overallIntensity = 0.0f;
                }
                else if (!_isClimbing)
                {
                    if (_currentMovement.Magnitude < 0.75f)
                    {
                        if (_lastWalkMode != WalkMode.walk)
                        {
                            _lastWalkMode = WalkMode.walk;
                            SendMessage("OnWalk", SendMessageOptions.DontRequireReceiver);
                        }
                    }
                    else if (_currentMovement.Magnitude < 1.5f)
                    {
                        if (_lastWalkMode != WalkMode.run)
                        {
                            _lastWalkMode = WalkMode.run;
                            SendMessage("OnRun", SendMessageOptions.DontRequireReceiver);
                        }
                    }
                    else
                    {
                        if (_lastWalkMode != WalkMode.sprint)
                        {
                            _lastWalkMode = WalkMode.sprint;
                            SendMessage("OnSprint", SendMessageOptions.DontRequireReceiver);
                        }
                    }
                }

                var local = Util.HorizontalVector(Util.HorizontalAngle(_currentMovement.Direction) - transform.eulerAngles.y);

                movement = local * _currentMovement.Magnitude * overallIntensity;

                _wasMovingInCover = _cover.In && movement.magnitude > float.Epsilon;
            }
            else
            {
                _wasMovingInCover = false;
                movement = Vector3.zero;

                if (_lastWalkMode != WalkMode.none)
                {
                    _lastWalkMode = WalkMode.none;
                    SendMessage("OnStop", SendMessageOptions.DontRequireReceiver);
                }
            }

            Util.Lerp(ref _localMovement, movement, 8);
            _movementInput = Mathf.Clamp(movement.magnitude * 2, 0, 1);
        }

        private void moveAwayFromCover(Vector3 direction)
        {
            _isMovingAwayFromCover = true;
            _moveFromCoverDirection = Vector3.Lerp(direction, -_cover.ForwardDirection, 0.5f);
            _moveFromCoverPosition = transform.position;
            _needToEnterCoverByWalkingToIt = true;
            _cover.Clear();
        }

        private Vector3 getClosestCoverPosition()
        {
            if (!IsInCover)
                return transform.position;

            var canPeek = !_currentMovement.IsMoving && (_cover.Main.IsTall && CoverSettings.CanUseTallCorners) || (!_cover.Main.IsTall && CoverSettings.CanUseLowCorners);

            return _cover.Main.ClosestPointTo(transform.position,
                                              canPeek && IsByAnOpenLeftCorner ? _capsule.radius : -1000,
                                              canPeek && IsByAnOpenRightCorner ? _capsule.radius : -1000,
                                              _capsule.radius - _cover.Main.Depth);
        }

        private void stickToCover()
        {
            var ignore = false;

            if (_currentMovement.IsMoving && !IsAiming)
            {
                if (Vector3.Dot(_currentMovement.Direction, _cover.Main.Left) > 0 && IsStandingLeftInCover)
                    ignore = true;
                else if (Vector3.Dot(_currentMovement.Direction, _cover.Main.Right) > 0 && !IsStandingLeftInCover)
                    ignore = true;
            }

            var previous = _coverOffset;

            if (ignore)
                _coverOffset = Vector3.zero;
            else
            {
                var closest = getClosestCoverPosition();

                var target = closest + _coverOffsetBack + _coverOffsetSide;
                var offset = target - transform.position;

                if (offset.magnitude > 0.02f && !IsMovingToCoverOffset)
                {
                    var move = offset - Util.Lerp(offset, Vector3.zero, 10);
                    transform.position += move;
                }

                _coverOffset = transform.position - closest;
            }
        }

        private float deltaAngleToTurnTo(float target)
        {
            var current = transform.eulerAngles.y;
            var delta = Mathf.DeltaAngle(current, target);

            if (_cover.In && Mathf.Abs(delta) > 100 && !IsAiming)
            {
                var deltaToCover = Mathf.DeltaAngle(current, _cover.ForwardAngle);

                if (delta > 0 && deltaToCover > 40)
                    delta = -360 + delta;
                else if (delta < 0 && deltaToCover < -40)
                    delta = 360 + delta;
            }

            return delta;
        }

        private void updateBodyAngleDiff()
        {
            if (_isThrowing || _hasThrown)
                _horizontalAngleDiff = deltaAngleToTurnTo(_throwBodyAngle);
            else if (_isIntendingToJump || (_isJumping && _normalizedJumpTime < JumpSettings.AnimationLock))
                _horizontalAngleDiff = deltaAngleToTurnTo(_jumpAngle);
            else if (_isIntendingToRoll || (_isRolling && _normalizedRollTime < RollSettings.AnimationLock))
                _horizontalAngleDiff = deltaAngleToTurnTo(_rollAngle);
            else if (_cover.In)
            {
                if (_sideOffset == CoverOffsetState.Using || _backOffset == CoverOffsetState.Using)
                    _horizontalAngleDiff = deltaAngleToTurnTo(_horizontalAngle);
                else if (isInAnimatedCover || _sideOffset == CoverOffsetState.Exiting || _backOffset == CoverOffsetState.Exiting)
                    _horizontalAngleDiff = deltaAngleToTurnTo(_cover.FaceAngle(_isCrouching));
                else
                    _horizontalAngleDiff = deltaAngleToTurnTo(_horizontalAngle);

                if (IsAiming && !_coverAim.LeaveAfterAiming)
                    updateCoverDirection(_horizontalAngle);
                else
                    modifyBodyAngleDiffInCover();
            }
            else
                _horizontalAngleDiff = deltaAngleToTurnTo(_horizontalAngle);
        }

        private void updateCoverDirection(float angle)
        {
            if (!_cover.In || _wantsToFaceInADirection)
                return;

            if (_cover.Main.IsRight(angle, 0))
            {
                if (_cover.IsTall)
                {
                    if (_cover.IsStandingLeft)
                    {
                        if (IsMoving)
                        {
                            if (_cover.Main.IsRight(angle, 50))
                                _cover.StandRight();
                        }
                        else if ((_cover.Main.IsRight(angle, 20) && (!IsZooming || IsByAnOpenRightCorner)) || _cover.Main.IsRight(angle, 60))
                            _cover.StandRight();
                    }
                }
                else if (_cover.HasRightAdjacent &&
                          Vector3.Distance(_cover.RightAdjacent.ClosestPointTo(transform.position, 0, 0), transform.position) <= _capsule.radius + float.Epsilon)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(angle, _cover.Main.Angle)) + 0.1f > Mathf.Abs(Mathf.DeltaAngle(angle, _cover.RightAdjacent.Angle)))
                    {
                        var wasLow = IsInLowCover;

                        _cover.StandRight();
                        _cover.MoveToRightAdjacent();

                        if (_wantsToAim && wasLow && IsInTallCover)
                            _backOffset = CoverOffsetState.Using;
                    }
                    else
                        _cover.StandLeft();
                }
                else
                    _cover.StandRight();
            }
            else if (_cover.Main.IsLeft(angle, 0))
            {
                if (_cover.IsTall)
                {
                    if (_cover.IsStandingRight)
                    {
                        if (IsMoving)
                        {
                            if (_cover.Main.IsLeft(angle, 50))
                                _cover.StandLeft();
                        }
                        else if ((_cover.Main.IsLeft(angle, 20) && (!IsZooming || IsByAnOpenLeftCorner)) || _cover.Main.IsLeft(angle, 60))
                            _cover.StandLeft();
                    }
                }
                else if (_cover.HasLeftAdjacent &&
                         Vector3.Distance(_cover.LeftAdjacent.ClosestPointTo(transform.position, 0, 0), transform.position) <= _capsule.radius + float.Epsilon)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(angle, _cover.Main.Angle)) + 0.1f > Mathf.Abs(Mathf.DeltaAngle(angle, _cover.LeftAdjacent.Angle)))
                    {
                        var wasLow = IsInLowCover;

                        _cover.StandLeft();
                        _cover.MoveToLeftAdjacent();

                        if (_wantsToAim && wasLow && IsInTallCover)
                            _backOffset = CoverOffsetState.Using;
                    }
                    else
                        _cover.StandRight();
                }
                else if (_cover.Main.IsLeft(angle, 20))
                    _cover.StandLeft();
            }

            if (IsInTallCover)
            {
                if (_cover.IsStandingRight)
                {
                    if (_horizontalAngleDiff > 90 && IsInTallCover && !_cover.Main.IsFront(transform.eulerAngles.y))
                        _horizontalAngleDiff = _horizontalAngleDiff - 360;
                }
                else
                {
                    if (_horizontalAngleDiff < -90 && IsInTallCover && !_cover.Main.IsFront(transform.eulerAngles.y))
                        _horizontalAngleDiff = 360 + _horizontalAngleDiff;
                }
            }
        }

        private void modifyBodyAngleDiffInCover()
        {
            if (!_cover.In)
                return;

            if (_cover.IsStandingRight)
            {
                if (_horizontalAngleDiff < -181 &&
                    _cover.HasLeftAdjacent &&
                     Vector3.Distance(_cover.LeftAdjacent.ClosestPointTo(transform.position, 0, 0), transform.position) <= _capsule.radius + float.Epsilon)
                {
                    _horizontalAngleDiff = 360 + _horizontalAngleDiff;
                }
                else if (_horizontalAngleDiff < -90 && IsInTallCover && _cover.Main.IsFront(transform.eulerAngles.y))
                    _horizontalAngleDiff = 360 + _horizontalAngleDiff;
            }
            else
            {
                if (_horizontalAngleDiff > 179 &&
                    _cover.HasRightAdjacent &&
                     Vector3.Distance(_cover.RightAdjacent.ClosestPointTo(transform.position, 0, 0), transform.position) <= _capsule.radius + float.Epsilon)
                {
                    _horizontalAngleDiff = _horizontalAngleDiff - 360;
                }
                else if (_horizontalAngleDiff > 90 && IsInTallCover && _cover.Main.IsFront(transform.eulerAngles.y))
                    _horizontalAngleDiff = _horizontalAngleDiff - 360;
            }
        }

        private void updateVertical()
        {
            if (_jumpTimer < 999) _jumpTimer += Time.deltaTime;
            if (_ignoreFallTimer > 0) _ignoreFallTimer -= Time.deltaTime;

            updateGround();

            if (EquippedWeapon.IsHeavy)
                _isIntendingToJump = false;

            if (_isGrounded)
            {
                if (_nextJumpTimer > -float.Epsilon) _nextJumpTimer -= Time.deltaTime;

                if (!_cover.In && !_isClimbing && !_isJumping && _nextJumpTimer < float.Epsilon && _wantsToJump)
                    _isIntendingToJump = true;
            }
            else if (_body.velocity.y < -5)
                _isJumping = false;

            if (_isGrounded)
            {
                if (_isIntendingToJump && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, _jumpAngle)) < 10)
                {
                    if (!_isJumping)
                    {
                        _animator.SetTrigger("Jump");
                        _isJumping = true;
                        _jumpTimer = 0;

                        SendMessage("OnJump", SendMessageOptions.DontRequireReceiver);

                        if (Jumped != null)
                            Jumped.Invoke();
                    }

                    _animator.SetFloat("JumpSpeed", _jumpForwardMultiplier);

                    var v = _jumpForwardMultiplier * Util.HorizontalVector(_jumpAngle) * JumpSettings.Speed;
                    v.y = JumpSettings.Strength;
                    _body.velocity = v;
                }
                else if (_isJumping)
                    _isJumping = false;
            }
            else
                _isIntendingToJump = false;

            if (_isJumping)
            {
                var info = _animator.GetCurrentAnimatorStateInfo(0);

                if (info.IsName("Jump")) _normalizedJumpTime = info.normalizedTime;
                else if (info.IsName("Jump Land")) _normalizedJumpTime = 1;
                else
                {
                    info = _animator.GetNextAnimatorStateInfo(0);

                    if (info.IsName("Jump")) _normalizedJumpTime = info.normalizedTime;
                    else if (info.IsName("Jump Land")) _normalizedJumpTime = 1;
                }
            }

            if (_ignoreFallTimer <= float.Epsilon)
            {
                if (!_isFalling)
                {
                    if (((!_isJumping && _body.velocity.y < -1) || (_isJumping && _body.velocity.y < -4)) &&
                        !findGround(FallThreshold))
                    {
                        _animator.SetTrigger("Fall");
                        _isFalling = true;
                    }
                }
                else
                {
                    if (_isGrounded)
                        _isFalling = false;
                }
            }
            else
                _isFalling = false;

            if (_isFalling)
            {
                Vector3 edge;
                if (findEdge(out edge, 0.1f))
                {
                    var offset = transform.position - edge;
                    offset.y = 0;
                    var distance = offset.magnitude;

                    if (distance > 0.01f)
                    {
                        offset /= distance;
                        transform.position += offset * Mathf.Clamp(Time.deltaTime * 3, 0, distance);
                    }
                }
            }
        }

        private void updateGround()
        {
            if (_ignoreFallTimer < float.Epsilon)
                findGroundAndSlope(GroundThreshold);
            else
                _isGrounded = true;

            if (_isGrounded && !_wasGrounded && IsAlive && _nogroundTimer >= 0.3f)
            {
                SendMessage("OnLand", SendMessageOptions.DontRequireReceiver);
                _nextJumpTimer = 0.2f;

                if (Landed != null)
                    Landed.Invoke();
            }

            _wasGrounded = _isGrounded;
        }

        internal bool wantsToFire
        {
            get { return _wantsToFire; }
        }

        internal bool dontChangeArmAimingJustYet
        {
            get { return _dontChangeArmAimingJustYet; }
        }

        private bool isInAnimatedCover
        {
            get
            {
                var isInCover = false;

                if (_cover.In && !_isClimbing)
                {
                    if (!IsAimingGun)
                        isInCover = true;
                    else
                        isInCover = false;
                }

                return isInCover;
            }
        }

        private void updateAnimator()
        {
            if (IsAlive)
            {
                var state = _animator.GetCurrentAnimatorStateInfo(0);

                float runCycle = Mathf.Repeat(state.normalizedTime, 1);

                if (_isGrounded)
                {
                    if (_jumpLegTimer > 0)
                        _jumpLegTimer -= Time.deltaTime;
                    else if (_movementInput > 0.5f)
                        _animator.SetFloat("JumpLeg", runCycle < 0.5f ? 1 : -1);
                }
                else
                    _jumpLegTimer = 0.5f * _movementInput;

                if (IsAlive &&
                    _movementInput > 0.5f &&
                    state.IsName("Walking"))
                {
                    if (runCycle > 0.6f)
                    {
                        if (_lastFoot != 1)
                        {
                            _lastFoot = 1;
                            SendMessage("OnFootstep", _animator.GetBoneTransform(HumanBodyBones.LeftFoot).position, SendMessageOptions.DontRequireReceiver);
                            if (Stepped != null) Stepped.Invoke();
                        }
                    }
                    else if (runCycle > 0.1f)
                    {
                        if (_lastFoot != 0)
                        {
                            _lastFoot = 0;
                            SendMessage("OnFootstep", _animator.GetBoneTransform(HumanBodyBones.RightFoot).position, SendMessageOptions.DontRequireReceiver);
                            if (Stepped != null) Stepped.Invoke();
                        }
                    }
                }
                else
                    _lastFoot = -1;

                if (_immediateIdle)
                {
                    if (!_immediateAim)
                        _animator.SetTrigger("ImmediateIdle");

                    _animator.SetFloat("CrouchToStand", _isCrouching ? 0 : 1);
                    _animator.SetFloat("MovementSpeed", 0);
                    _animator.SetFloat("Rotation", 0);

                    transform.Rotate(0, _horizontalAngleDiff, 0);
                    _currentAnimatedAngle = transform.eulerAngles.y;
                    _currentStep = 0;

                    _immediateIdle = false;
                }

                if (_immediateAim)
                {
                    _animator.SetTrigger("ImmediateAim");
                    _immediateAim = false;
                }

                _animator.SetFloat("Speed", Speed);
                _animator.SetBool("IsDead", false);

                if (_coverOffset.magnitude > float.Epsilon)
                {
                    _animator.SetFloat("SmoothRotation", 0, 0.05f, Time.deltaTime);
                    _animator.SetFloat("Rotation", 0, 0.05f, Time.deltaTime);
                }
                else if (_wantsToRotateSmoothly)
                {
                    if (_isGrounded && _movementInput < 0.5f && !_isClimbing && !_isFalling)
                        _animator.SetFloat("SmoothRotation", _currentStep, 0.05f, Time.deltaTime);
                    else
                        _animator.SetFloat("SmoothRotation", 0, 0.05f, Time.deltaTime);

                    _animator.SetFloat("Rotation", 0, 0.05f, Time.deltaTime);
                }
                else
                {
                    _animator.SetFloat("SmoothRotation", 0, 0.05f, Time.deltaTime);
                    _animator.SetFloat("Rotation", _currentStep, 0.05f, Time.deltaTime);
                }

                var movement = _localMovement;

                if (_currentMovement.Magnitude > float.Epsilon)
                    movement /= _currentMovement.Magnitude;

                if (_ik.HasSwitchedHands)
                    movement.x *= -1;

                _animator.SetBool("IsMirrored", _ik.HasSwitchedHands);

                var movementSpeed = _currentMovement.Magnitude;

                var outOfCoverStand = 1.0f;

                if (_hasCrouchCover && !IsAiming && !(_cover.In && _cover.IsTall))
                    outOfCoverStand = Mathf.Clamp01((Vector3.Distance(transform.position, _crouchCoverPosition) / (CoverSettings.MaxCrouchDistance - CoverSettings.MinCrouchDistance)) - CoverSettings.MinCrouchDistance);

                if (_useSprintingAnimation)
                    movementSpeed = 2;
                else if (_isCrouching || _cover.In)
                    movementSpeed = 0.5f;
                else if (movementSpeed >= 1 - float.Epsilon && !_useSprintingAnimation)
                    movementSpeed = 1;

                _animator.SetFloat("ArmLift", _wantsToLiftArms ? 1 : 0, 0.14f, Time.deltaTime);

                if (_movementInput > 0.5f)
                {
                    _animator.SetFloat("MovementSpeed", movementSpeed * _movementInput, AccelerationDamp * 0.1f, Time.deltaTime);
                    _animator.SetFloat("MovementX", movement.x, AccelerationDamp * 0.02f, Time.deltaTime);
                    _animator.SetFloat("MovementZ", movement.z, AccelerationDamp * 0.02f, Time.deltaTime);
                }
                else
                {
                    _animator.SetFloat("MovementSpeed", 0, DeccelerationDamp * 0.1f, Time.deltaTime);
                    _animator.SetFloat("MovementX", movement.x, DeccelerationDamp * 0.02f, Time.deltaTime);
                    _animator.SetFloat("MovementZ", movement.z, DeccelerationDamp * 0.02f, Time.deltaTime);
                }

                _animator.SetBool("IsFalling", _isFalling && !_isJumping);
                _animator.SetBool("IsGrounded", _isGrounded);
                _animator.SetInteger("WeaponType", weaponType);

                // Small hacks. Better animation transitions.
                _animator.SetBool("IsWeaponReady", IsWeaponReady || _isRolling || (_isClimbing && _normalizedClimbTime > 0.7f));

                _dontChangeArmAimingJustYet = false;

                var bodyValue = 0;

                if (!EquippedWeapon.IsNull && !HasGrenadeInHand)
                {
                    var type = EquippedWeapon.Type;

                    switch (type)
                    {
                        case WeaponType.Pistol:
                            if (EquippedWeapon.Shield != null)
                                bodyValue = 3;
                            else
                                bodyValue = 1;
                            break;

                        case WeaponType.Rifle:
                            bodyValue = 2;
                            break;

                        default:
                            if (type > WeaponType.Tool)
                                type -= 1;

                            bodyValue = (int)type + 1;
                            break;
                    }
                }
                
                _animator.SetFloat("BodyValue", bodyValue, 0.1f, Time.deltaTime);

                if (IsAiming || (_cover.In && _wantsToLiftArms))
                    _animator.SetFloat("IdleToAim", 1, 0.05f, Time.deltaTime);
                else
                    _animator.SetFloat("IdleToAim", 0, 0.05f, Time.deltaTime);

                var gun = EquippedWeapon.Gun;

                if (gun != null)
                {
                    var type = this.weaponType;

                    if (_animator.GetCurrentAnimatorStateInfo(FirstWeaponLayer + type).IsName("Aim"))
                    {
                        if (_animator.IsInTransition(FirstWeaponLayer + type))
                            _dontChangeArmAimingJustYet = true;
                    }
                    else if (_animator.GetNextAnimatorStateInfo(FirstWeaponLayer + type).IsName("Aim"))
                        _dontChangeArmAimingJustYet = true;

                    if (_cover.In)
                        if (_sideOffset == CoverOffsetState.Using || _backOffset == CoverOffsetState.Using)
                        {
                            _ik.ImmediateArmAim();
                            _dontChangeArmAimingJustYet = true;
                            _animator.SetBool("IsUsingWeapon", true);
                        }

                    if (ActiveWeapon.IsShotgunRifle && ActiveWeapon.Type == WeaponType.Rifle)
                        _animator.SetFloat("GunVariant", 2, 0.1f, Time.deltaTime);
                    else if (ActiveWeapon.Type == WeaponType.Pistol && ActiveWeapon.Shield != null)
                        _animator.SetFloat("GunVariant", 1, 0.1f, Time.deltaTime);
                    else
                        _animator.SetFloat("GunVariant", _wantsToScope && gun != null && gun.Scope != null ? 1 : 0, 0.1f, Time.deltaTime);

                    if (!_dontChangeArmAimingJustYet)
                    {
                        _animator.SetBool("IsUsingWeapon", !IsGoingToSprint &&
                                                           !HasGrenadeInHand &&
                                                           !IsMovingToCoverOffsetAndCantAim &&
                                                           (IsAiming ||
                                                            (_isRolling && _wantsToAim) ||
                                                            (IsChangingWeapon && _wantsToAim)));
                    }
                }
                else
                {
                    _animator.SetFloat("Tool", (int)EquippedWeapon.Tool - 1);

                    _animator.SetBool("IsAlternateUse", _isUsingWeaponAlternate);
                    _animator.SetBool("IsUsingWeapon", _isUsingWeapon);
                }

                var isInCover = isInAnimatedCover;

                if (_cover.In && isInCover && !IsMovingToCoverOffset && (_sideOffset == CoverOffsetState.Using || _backOffset == CoverOffsetState.Using))
                    setCoverOffsets(false, false, Vector3.zero, Vector3.zero);

                _animator.SetBool("IsInCover", isInCover);
                _animator.SetBool("IsInLowCover", isInCover && (!_cover.IsTall || _isCrouching));
                _animator.SetBool("IsInTallLeftCover", isInCover && _cover.IsTall && !_isCrouching && _cover.IsStandingLeft);
                _animator.SetBool("IsInTallCoverBack", isInCover && _cover.IsTall && _cover.Main.IsFrontField(_throwAngle, 180));

                if (_cover.In)
                {
                    _animator.SetFloat("CoverDirection", (_ik.HasSwitchedHands ? -1 : 1) * _cover.Direction, 0.2f, Time.deltaTime);
                    _animator.SetFloat("CoverHeight", (_cover.IsTall && !_isCrouching) ? 1.0f : 0.0f, 0.1f, Time.deltaTime);

                    var angle = _isThrowing ? _throwAngle : _horizontalAngle;

                    if (_cover.Main.IsFrontField(angle, 180))
                        angle = Mathf.DeltaAngle(_cover.ForwardAngle, angle);
                    else if (_cover.IsStandingRight)
                        angle = 90;
                    else
                        angle = -90;

                    _animator.SetFloat("ThrowAngle", angle);
                }

                _animator.SetFloat("ClimbHeight", _climbHeight);
                _animator.SetBool("IsVault", _isClimbing && _isClimbingAVault);
                _animator.SetBool("IsClimbing", _isClimbing);

                if (IsAiming && IsInLowCover)
                    _animator.SetFloat("CrouchToStand", ((!_cover.IsTall || _isCrouching) && !_isAimingThroughCoverPlane) ? 0 : outOfCoverStand, 0.1f, Time.deltaTime);
                else
                    _animator.SetFloat("CrouchToStand", _isCrouching ? 0 : outOfCoverStand, 0.1f, Time.deltaTime);

                _animator.SetBool("HasGrenade", _isGrenadeTakenOut);
            }
            else
            {
                _animator.SetBool("IsDead", true);
                _animator.SetBool("IsUsingWeapon", false);
            }
        }

        private void instantCoverAnimatorUpdate()
        {
            _animator.SetFloat("CoverDirection", _cover.Direction);
            _animator.SetFloat("CoverHeight", (_cover.IsTall && !_isCrouching) ? 1.0f : 0.0f);
        }

        private void updateIK()
        {
            if (!IsAlive)
                return;

            var gun = EquippedWeapon.Gun;

            if (IK.Enabled)
            {
                IKConfig config;
                config.Delay = IK.Delay;
                config.MinIterations = IK.MinIterations;
                config.MaxIterations = IK.MaxIterations;
                config.LeftHand = IK.LeftHandOverride != null ? IK.LeftHandOverride : _ik.GetBone(HumanBodyBones.LeftIndexProximal);
                config.RightHand = IK.RightHandOverride != null ? IK.RightHandOverride : _ik.GetBone(HumanBodyBones.RightHand);
                config.Sight = IK.SightOverride != null ? IK.SightOverride : _ik.Sight;
                config.HitBone = IK.HitBoneOverride != null ? IK.HitBoneOverride : _ik.GetBone(HumanBodyBones.Spine);
                config.Gun = gun;
                config.TurnImmediately = _wouldTurnImmediately;
                config.BodyTarget = _bodyTarget;
                config.AimTarget = _aimTarget;

                _ik.Update(config);
            }
            else
                _ik.Skip();

            if (gun != null)
                _target = gun.FindCurrentAimedHealthTarget();
        }

        private bool findGround(float threshold)
        {
            var offset = 0.2f;
            var count = Physics.RaycastNonAlloc(transform.position + Vector3.up * offset, Vector3.down, Util.Hits, threshold + offset, 0x1);

            for (int i = 0; i < count; i++)
            {
                var hit = Util.Hits[i];

                if (!hit.collider.isTrigger)
                    if (hit.collider.gameObject != gameObject)
                        return true;
            }

            return false;
        }

        private void findGroundAndSlope(float threshold)
        {
            _isOnSlope = false;
            _isGrounded = false;

            var offset = 0.4f;
            var highest = 0f;
            var start = transform.position + Vector3.up * offset;
            var count = Physics.RaycastNonAlloc(start, Vector3.down, Util.Hits, threshold + offset, 0x1);

            const float slopeThreshold = 20f;

            for (int i = 0; i < count; i++)
            {
                var hit = Util.Hits[i];

                if (!hit.collider.isTrigger)
                    if (hit.collider.gameObject != gameObject)
                    {
                        var up = Vector3.Dot(Vector3.up, hit.normal);
                        var dist = Vector3.Distance(hit.point, start);

                        if (!_isGrounded || dist < highest)
                        {
                            _slope = Mathf.Acos(up) * Mathf.Rad2Deg;

                            if (up > 0.99f)
                                _slope = 0;

                            _isOnSlope = _slope > slopeThreshold;
                            _groundNormal = hit.normal;
                            _isGrounded = true;
                            highest = dist;
                        }

                        break;
                    }
            }

            if (_isGrounded && !_wantsToJump && !_isFalling && _jumpTimer > 0.1f)
            {
                var maxShift = highest - offset;

                if (_isOnSlope)
                    maxShift -= (_slope - slopeThreshold) / 200f;

                if (maxShift > float.Epsilon)
                {
                    var shift = Time.deltaTime * 0.5f;

                    if (shift > maxShift)
                        shift = maxShift;

                    transform.position -= Vector3.up * shift;
                }
            }
        }

        private float getGoundHeight()
        {
            var count = Physics.RaycastNonAlloc(transform.position + (Vector3.up * (0.1f)), Vector3.down, Util.Hits, 0x1);

            for (int i = 0; i < count; i++)
            {
                var hit = Util.Hits[i];

                if (hit.collider.gameObject != gameObject)
                    return hit.point.y;
            }

            return 0;
        }

        private bool findEdge(out Vector3 position, float threshold)
        {
            var bottom = transform.TransformPoint(_capsule.center - new Vector3(0, _capsule.height * 0.5f + _capsule.radius, 0));
            var count = Physics.OverlapSphereNonAlloc(bottom, _capsule.radius + threshold, Util.Colliders, 0x1);

            for (int i = 0; i < count; i++)
                if (Util.Colliders[i].gameObject != gameObject)
                {
                    position = Util.Colliders[i].ClosestPointOnBounds(bottom);
                    return true;
                }

            position = Vector3.zero;
            return false;
        }

        private CoverClimb getClimb(Cover cover)
        {
            if (cover == null)
                return CoverClimb.No;

            return cover.GetClimbAt(transform.position, _capsule.radius, 3.0f, 1.05f, 1.1f);
        }

        private void endOfWeaponUse()
        {
            var weaponObject = EquippedWeapon.Item;

            if (weaponObject != null)
            {
                if (_isUsingWeaponAlternate)
                {
                    weaponObject.SendMessage("OnUsedAlternate", SendMessageOptions.DontRequireReceiver);
                    if (UsedToolAlternate != null) UsedToolAlternate.Invoke();
                }
                else
                {
                    weaponObject.SendMessage("OnUsed", SendMessageOptions.DontRequireReceiver);
                    if (UsedTool != null) UsedTool.Invoke();
                }
            }

            if (_isUsingWeaponAlternate)
            {
                SendMessage("OnToolUsedAlternate", SendMessageOptions.DontRequireReceiver);
                if (UsedToolAlternate != null) UsedToolAlternate.Invoke();
            }
            else
            {
                SendMessage("OnToolUsed", SendMessageOptions.DontRequireReceiver);
                if (UsedTool != null) UsedTool.Invoke();
            }
        }

        #endregion
    }
}