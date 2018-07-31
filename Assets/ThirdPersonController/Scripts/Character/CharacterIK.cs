using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoverShooter
{
    public enum ChainElement
    {
        lowerArm,
        upperArm,
        shoulder,
        head,
        neck,
        chest,
        spine,
    }

    public struct IKConfig
    {
        /// <summary>
        /// Minimum amount of iterations performed for each IK chain.
        /// </summary>
        public int MinIterations;

        /// <summary>
        /// The IK will be performed till either the target state is reached or maximum amount of iterations are performed.
        /// </summary>
        public int MaxIterations;

        /// <summary>
        /// Time in seconds to wait between IK updates.
        /// </summary>
        public DistanceRange Delay;

        /// <summary>
        /// Position of a left hand to maintain on a gun.
        /// </summary>
        public Transform LeftHand;

        /// <summary>
        /// Position of a right hand to adjust by recoil.
        /// </summary>
        public Transform RightHand;

        /// <summary>
        /// Bone to adjust when a character is hit.
        /// </summary>
        public Transform HitBone;

        /// <summary>
        /// Transform to manipulate so it is facing towards a target. Used when aiming a head.
        /// </summary>
        public Transform Sight;

        /// <summary>
        /// Gun to be manipulated by the IK.
        /// </summary>
        public BaseGun Gun;

        /// <summary>
        /// Is character currently turning immediately towards a target.
        /// </summary>
        public bool TurnImmediately;

        /// <summary>
        /// Position at which the gun is supposed to be pointing at.
        /// </summary>
        public Vector3 AimTarget;

        /// <summary>
        /// Position at which the body is supposed to be rotated at.
        /// </summary>
        public Vector3 BodyTarget;
    }

    public struct CharacterIK
    {
        /// <summary>
        /// Is the main hand currently left.
        /// </summary>
        public bool HasSwitchedHands
        {
            get { return _switchedWeapon != null; }
        }

        /// <summary>
        /// True if arm aim intensity is at least 90%.
        /// </summary>
        public bool IsAimingArms
        {
            get { return _armAimIntensity > 0.9f; }
        }

        /// <summary>
        /// Sight transform that was found inside the character's head object.
        /// </summary>
        public Transform Sight;

        private CharacterMotor _motor;
        private Animator _animator;

        private Transform[] _bones;
        private Dictionary<Transform, Transform> _mirrors;

        private Transform[] _left;
        private Transform[] _right;
        private Transform[] _middle;

        private IKBone[] _chain;

        private int[] _leftIndices;
        private int[] _rightIndices;
        private int[] _middleIndices;
        private bool _areArmsComingOutOfNeck;

        private IK _aimIK;
        private IK _recoilIK;
        private IK _leftHandIK;
        private IK _rightHandIK;
        private IK _sightIK;

        private Transform _originalWeaponBone;
        private Transform _switchedWeaponBone;
        private Transform _switchedWeapon;
        private Vector3 _originalLocalWeaponPosition;
        private Quaternion _originalLocalWeaponRotation;
        private Vector3 _originalLocalWeaponScale;

        private Vector3 _lastHit;
        private float _lastHitStrength;
        private float _lastHitWait;

        private Quaternion _aim;
        private float _aimDistance;
        private Quaternion _relativeAim;
        private float _relativeAimDistance;

        private Quaternion _preciseHorizontalRotation;

        private float _leftHandAimIntensity;
        private float _armAimIntensity;
        private float _previousArmAimTargetIntensity;
        private float _throwAimIntensity;
        private float _headAimIntensity;

        /// <summary>
        /// Applies a transformation based on the bullet direction.
        /// </summary>
        public void Hit(Vector3 normal, float strength, float wait)
        {
            if (_lastHitWait > float.Epsilon)
                return;

            _lastHit = normal;
            _lastHitStrength = 1.0f;
            _lastHitWait = wait;
        }

        /// <summary>
        /// Finds a bone by type.
        /// </summary>
        public Transform GetBone(HumanBodyBones bone)
        {
            return _bones[(int)bone];
        }

        /// <summary>
        /// Set up the character IK when creating the character. Finds bone chains.
        /// </summary>
        public void Setup(CharacterMotor motor)
        {
            _motor = motor;
            _animator = motor.GetComponent<Animator>();

            _aim = Quaternion.identity;
            _relativeAim = Quaternion.identity;
            _preciseHorizontalRotation = Quaternion.identity;

            _aimIK = new IK();
            _recoilIK = new IK();
            _leftHandIK = new IK();
            _rightHandIK = new IK();
            _sightIK = new IK();

            _mirrors = new Dictionary<Transform, Transform>();

            int maxBone = 0;

            for (int bone = 0; bone < (int)HumanBodyBones.LastBone; bone++)
                if (bone > maxBone)
                    maxBone = bone;

            _bones = new Transform[maxBone + 1];

            for (int bone = 0; bone < (int)HumanBodyBones.LastBone; bone++)
                _bones[bone] = _animator.GetBoneTransform((HumanBodyBones)bone);

            for (int bone = 0; bone < (int)HumanBodyBones.LastBone; bone++)
            {
                var text = ((HumanBodyBones)bone).ToString();
                var id = bone;

                if (text.StartsWith("Left"))
                {
                    var other = (int)Enum.Parse(typeof(HumanBodyBones), text.Replace("Left", "Right"));

                    if (_bones[id] == null ||
                        _bones[other] == null)
                        continue;

                    _mirrors[_bones[id]] = _bones[other];
                    _mirrors[_bones[other]] = _bones[id];
                }
                else if (!text.StartsWith("Right"))
                {
                    if (_bones[id] != null)
                        _mirrors[_bones[id]] = _bones[id];
                }
            }

            var head = _bones[(int)HumanBodyBones.Head];

            for (int i = 0; i < head.childCount; i++)
            {
                var child = head.GetChild(i);

                if (child.name == "Look" ||
                    child.name == "Sight" ||
                    child.name == "look" ||
                    child.name == "sight")
                {
                    Sight = child;
                    break;
                }

                for (int j = 0; j < child.childCount; j++)
                {
                    var sub = child.GetChild(j);

                    if (sub.name == "Look" ||
                        sub.name == "Sight" ||
                        sub.name == "look" ||
                        sub.name == "sight")
                    {
                        Sight = sub;
                        break; // Doesn't exit the main loop, we give priority to the upper children.
                    }
                }
            }

            _left = findChain(_bones[(int)HumanBodyBones.LeftLowerArm]);
            _right = findChain(_bones[(int)HumanBodyBones.RightLowerArm]);
            _middle = findChain(_bones[(int)HumanBodyBones.Head]);

            _leftIndices = findIndices(_left);
            _rightIndices = findIndices(_right);
            _middleIndices = findIndices(_middle);

            _areArmsComingOutOfNeck = (findIndex(_left, HumanBodyBones.Neck) >= 0) || (findIndex(_right, HumanBodyBones.Neck) >= 0);
        }

        /// <summary>
        /// Switches the main hand from right to left.
        /// </summary>
        public void Mirror(Transform gun)
        {
            if (_switchedWeapon != null)
                Unmirror();

            var newBone = gun.transform.parent;

            if (_mirrors.ContainsKey(newBone))
                newBone = _mirrors[newBone];

            if (newBone == gun.transform.parent)
                return;

            var referenceTransform = gun;
            var gunComponent = gun.GetComponent<BaseGun>();
            if (gunComponent != null && gunComponent.Aim != null)
                referenceTransform = gunComponent.Aim.transform;

            var referenceForward = referenceTransform.forward;

            _switchedWeapon = gun.transform;
            _originalWeaponBone = _switchedWeapon.transform.parent;
            _originalLocalWeaponPosition = _switchedWeapon.localPosition;
            _originalLocalWeaponRotation = _switchedWeapon.localRotation;
            _originalLocalWeaponScale = _switchedWeapon.localScale;

            _switchedWeaponBone = newBone;
            _switchedWeapon.SetParent(_switchedWeaponBone, false);
            _switchedWeapon.localScale = new Vector3(_originalLocalWeaponScale.x, -_originalLocalWeaponScale.y, _originalLocalWeaponScale.z);

            if (Vector3.Dot(referenceTransform.forward, referenceForward) < 0)
            {
                _switchedWeapon.localPosition = new Vector3(-_originalLocalWeaponPosition.x, -_originalLocalWeaponPosition.y, -_originalLocalWeaponPosition.z);
                _switchedWeapon.Rotate(Vector3.up, 180);
            }
        }

        /// <summary>
        /// Switches the main hand back from left to right.
        /// </summary>
        public void Unmirror()
        {
            if (_switchedWeapon == null)
                return;

            _switchedWeapon.SetParent(_originalWeaponBone);
            _switchedWeapon.localPosition = _originalLocalWeaponPosition;
            _switchedWeapon.localRotation = _originalLocalWeaponRotation;
            _switchedWeapon.localScale = _originalLocalWeaponScale;
            _switchedWeapon = null;
        }

        /// <summary>
        /// Set timers and intensities to zero.
        /// </summary>
        public void Skip()
        {
            _headAimIntensity = 0;
            _throwAimIntensity = 0;
            _leftHandAimIntensity = 0;
            _armAimIntensity = 0;
        }

        /// <summary>
        /// Immediately sets arm aim intensity to 1.
        /// </summary>
        public void ImmediateArmAim()
        {
            _previousArmAimTargetIntensity = 1;
            _armAimIntensity = 1;
        }

        /// <summary>
        /// Moves bones.
        /// </summary>
        public void Update(IKConfig config)
        {
            updateHeadAimIntennsity();
            updateThrowAimIntensity();
            updateArmAimIntennsity();
            updateLeftHandIntensity();

            var gun = config.Gun;
            var aimTransform = gun != null ? gun.transform.Find("Aim") : null;
            var cover = _motor.Cover;

            var cameraDistance = 0f;

            if (CameraManager.Main != null && CameraManager.Main.transform != null)
                cameraDistance = Vector3.Distance(_motor.transform.position, CameraManager.Main.transform.position);

            var delay = config.Delay.Get(cameraDistance);

            if (_lastHitWait > float.Epsilon)
                _lastHitWait -= Time.deltaTime;

            if (_lastHitStrength > float.Epsilon)
            {
                if (config.HitBone != null)
                {
                    var forwardDot = Vector3.Dot(-config.HitBone.transform.forward, _lastHit);
                    var rightDot = Vector3.Dot(config.HitBone.transform.right, _lastHit);

                    config.HitBone.localRotation *= Quaternion.Euler(forwardDot * _lastHitStrength, 0, rightDot * _lastHitStrength);
                }

                _lastHitStrength -= Time.deltaTime * 5.0f;
            }

            if (config.TurnImmediately)
            {
                _aim = Quaternion.FromToRotation(Vector3.forward, config.BodyTarget - _motor.AimOrigin);
                _aimDistance = Vector3.Distance(config.BodyTarget, _motor.AimOrigin);
            }
            else
            {
                Util.Lerp(ref _aim, Quaternion.FromToRotation(Vector3.forward, config.BodyTarget - _motor.AimOrigin), 4);
                Util.Lerp(ref _aimDistance, Vector3.Distance(config.BodyTarget, _motor.AimOrigin), 4);
            }

            var adaptIntensity = _motor.IsZooming ? 4f : 1f;

            var currentBodyTarget = _motor.AimOrigin + _aim * Vector3.forward * _aimDistance;
            var accurateAimTarget = config.AimTarget;
            var applyRecoilRotation = true;

            if (gun != null && gun.HasRaycastSetup)
            {
                accurateAimTarget = gun.RaycastTarget;
                applyRecoilRotation = false;

                adaptIntensity *= Mathf.Clamp(1 + (Mathf.Abs(_motor.HorizontalRecoil) + Mathf.Abs(_motor.VerticalRecoil)), 1, 10);
            }

            if (isExtremeTarget(accurateAimTarget))
                accurateAimTarget = currentBodyTarget;

            var ikAimTarget = accurateAimTarget;

            if (gun != null)
            {
                var origin = _motor.AimOrigin;
                var target = ikAimTarget;

                if (Vector3.Distance(target, origin) < 2)
                    ikAimTarget = origin + (target - origin).normalized * 2;
            }

            Util.Lerp(ref _relativeAim, Quaternion.Lerp(Quaternion.identity, Quaternion.FromToRotation(currentBodyTarget - _motor.AimOrigin, ikAimTarget - _motor.AimOrigin), _armAimIntensity), adaptIntensity);
            Util.Lerp(ref _relativeAimDistance, Vector3.Distance(ikAimTarget, _motor.AimOrigin), adaptIntensity);

            var right = Util.HorizontalVector(Util.HorizontalAngle(currentBodyTarget - _motor.AimOrigin) + 90);
            var recoilRotation = applyRecoilRotation ? Quaternion.AngleAxis(-_motor.VerticalRecoil, right) * Quaternion.AngleAxis(_motor.HorizontalRecoil, Vector3.up) : Quaternion.identity;

            var smoothAimTarget = _motor.AimOrigin + recoilRotation * (_relativeAim * (currentBodyTarget - _motor.AimOrigin)).normalized * _relativeAimDistance;
            clampAimTarget(ref smoothAimTarget);

            var recoilShift = Vector3.zero;

            if (gun != null)
            {
                var distance = 0f;

                if (HasSwitchedHands)
                    distance = Vector3.Distance(_bones[(int)HumanBodyBones.LeftShoulder].position, _bones[(int)HumanBodyBones.LeftHand].position);
                else
                    distance = Vector3.Distance(_bones[(int)HumanBodyBones.RightShoulder].position, _bones[(int)HumanBodyBones.RightHand].position);

                recoilShift -= distance * gun.transform.forward * _motor.VerticalRecoil / 20f;
                recoilShift -= distance * gun.transform.right * _motor.HorizontalRecoil / 20f;
            }

            if (config.RightHand != null && gun != null && recoilShift.magnitude > 0.01f)
            {
                setIKTarget(_recoilIK, config.RightHand, true);

                Vector3 rightHandTarget;

                if (_motor.EquippedWeapon.Type == WeaponType.Rifle)
                {
                    rightHandTarget = _recoilIK.GetTargetPosition() + recoilShift * 0.5f;

                    setIKTarget(_recoilIK, _bones[(int)HumanBodyBones.RightShoulder], true);
                    buildMainChain(ChainElement.chest, ChainElement.spine, false);

                    _recoilIK.Bones = _chain;
                    _recoilIK.UpdateMove(_recoilIK.GetTargetPosition() + recoilShift * 0.5f, 0, 1, 1, 1);

                    setIKTarget(_recoilIK, config.RightHand, true);
                }
                else
                    rightHandTarget = _recoilIK.GetTargetPosition() + recoilShift;

                buildMainChain(ChainElement.lowerArm, ChainElement.shoulder, false);

                _recoilIK.Bones = _chain;
                _recoilIK.UpdateMove(rightHandTarget, 0, 1, 1, 1);
            }

            var aimingAffectsLeftHand = _motor.EquippedWeapon.Type != WeaponType.Pistol || _motor.EquippedWeapon.Shield == null;

            if (aimTransform != null && _armAimIntensity > 0.01f)
            {
                setIKTarget(_aimIK, aimTransform, false);
                buildMainChain(ChainElement.shoulder, ChainElement.spine, aimingAffectsLeftHand);

                _aimIK.Bones = _chain;
                _aimIK.UpdateAim(smoothAimTarget, delay, _armAimIntensity * _armAimIntensity, config.MinIterations,config.MaxIterations);
            }

            if (gun != null && _armAimIntensity > 0.01f)
            {
                var bone = config.RightHand;

                if (HasSwitchedHands)
                {
                    if (_mirrors.ContainsKey(bone))
                        bone = _mirrors[bone];
                    else
                        bone = null;
                }

                if (bone != null)
                {
                    var reference = gun.Aim != null ? gun.Aim.transform : gun.transform;
                    var currentTarget = Util.GetClosestStaticHit(gun.Origin, gun.Origin + reference.forward * gun.Distance, 0);

                    var currentHorizontalAngle = Util.HorizontalAngle(currentTarget - bone.position);
                    var accurateHorizontalAngle = Util.HorizontalAngle(accurateAimTarget - bone.position);

                    var horizontalRotation = Quaternion.AngleAxis(Mathf.DeltaAngle(currentHorizontalAngle, accurateHorizontalAngle), Vector3.up);
                    Util.Lerp(ref _preciseHorizontalRotation, Quaternion.Lerp(Quaternion.identity, horizontalRotation, _armAimIntensity), 2 * adaptIntensity);
                   
                    bone.rotation = _preciseHorizontalRotation * bone.rotation;

                    if (_leftHandAimIntensity > 0.01f)
                    {
                        bone = _bones[(int)HumanBodyBones.LeftLowerArm];

                        if (HasSwitchedHands)
                        {
                            if (_mirrors.ContainsKey(bone))
                                bone = _mirrors[bone];
                            else
                                bone = null;
                        }

                        if (bone != null)
                            bone.rotation = Quaternion.Lerp(Quaternion.identity, _preciseHorizontalRotation, _leftHandAimIntensity) * bone.rotation;
                    }
                }

                if (!gun.HasRaycastSetup)
                    gun.SetupRaycastThisFrame(gun.Origin, config.AimTarget);
            }
            else
                _preciseHorizontalRotation = Quaternion.identity;

            if (gun != null && config.LeftHand != null && _leftHandAimIntensity > 0.01f)
            {
                Transform hand = null;

                if (_motor.IsCrouching && gun.LeftHandOverwrite.Crouch != null)
                    hand = gun.LeftHandOverwrite.Crouch;
                else if (_motor.IsAimingGun)
                    hand = gun.LeftHandOverwrite.Aim;
                else if (_motor.IsInCover)
                {
                    if (_motor.IsInTallCover)
                        hand = gun.LeftHandOverwrite.TallCover;
                    else
                        hand = gun.LeftHandOverwrite.LowCover;
                }

                if (hand == null)
                    hand = gun.LeftHandDefault;

                if (hand != null)
                {
                    setIKTarget(_leftHandIK, config.LeftHand, true);
                    buildSupportChain(ChainElement.lowerArm, ChainElement.shoulder, false);

                    _leftHandIK.Bones = _chain;
                    _leftHandIK.UpdateMove(hand.position, delay, _leftHandAimIntensity, config.MinIterations, config.MaxIterations);
                }
            }

            if (config.Sight != null && _headAimIntensity > 0.01f)
            {
                setIKTarget(_sightIK, config.Sight, true);

                if (_areArmsComingOutOfNeck)
                    buildMiddleChain(ChainElement.head, ChainElement.head);
                else
                    buildMiddleChain(ChainElement.head, ChainElement.neck);

                _sightIK.Bones = _chain;
                _sightIK.UpdateAim(smoothAimTarget, delay, _headAimIntensity, config.MinIterations, config.MaxIterations);
            }
        }

        private void updateThrowAimIntensity()
        {
            float targetIntensity = 0;

            if (_motor.IsThrowingGrenade && !_motor.IsInTallCover)
                targetIntensity = 1;

            Util.Lerp(ref _throwAimIntensity, targetIntensity, 6);
        }

        private void updateArmAimIntennsity()
        {
            var targetIntensity = 0f;

            if (_motor.WasAimingGun && _motor.IsAimingGun && !_motor.IsMovingToCoverOffsetAndCantAim)
                targetIntensity = 1;

            if (_motor.IsChangingWeaponOrHasJustChanged)
                targetIntensity = 0;

            var gun = _motor.EquippedWeapon.Gun;

            if (gun != null && _motor.IsPumping)
                targetIntensity = 0;
            else if (gun != null && _motor.IsLoadingBullet)
                targetIntensity = 0;
            else if (gun != null && _motor.IsLoadingMagazine)
                targetIntensity = 0;
            else if (_motor.IsInCover && _motor.IsInCoverOffset)
                targetIntensity = 1;

            if (_motor.dontChangeArmAimingJustYet && _previousArmAimTargetIntensity < targetIntensity)
                targetIntensity = 0.0f;
            else
                _previousArmAimTargetIntensity = targetIntensity;

            if (targetIntensity > _armAimIntensity)
            {
                if (_motor.wantsToFire && !_motor.IsInCover && !_motor.Weapon.IsNull && _motor.IsEquipped)
                    _armAimIntensity = targetIntensity;
                else
                    Util.Move(ref _armAimIntensity, targetIntensity, Time.deltaTime * 10);
            }
            else if (_armAimIntensity > targetIntensity)
                Util.Move(ref _armAimIntensity, targetIntensity, Time.deltaTime * 10);
        }

        private void updateLeftHandIntensity()
        {
            float targetIntensity = 0f;

            if (_motor.IsLeftHandAimReady && !_motor.IsClimbingOrVaulting && !_motor.IsFalling)
            {
                if (_motor.EquippedWeapon.Type == WeaponType.Pistol)
                {
                    if (_motor.IsAimingGun || _motor.IsInCover)
                        targetIntensity = 1;
                }
                else if (_motor.IsInCover)
                    targetIntensity = 1;
                else
                {
                    AnimatorStateInfo state;

                    var next = _animator.GetNextAnimatorStateInfo(0);
                    var intensity = 1f;

                    if (next.shortNameHash != 0)
                    {
                        intensity = _animator.GetAnimatorTransitionInfo(0).normalizedTime;
                        state = next;
                    }
                    else
                        state = _animator.GetCurrentAnimatorStateInfo(0);

                    if (state.IsName("Walking"))
                        targetIntensity = intensity;
                }
            }

            if (targetIntensity > _leftHandAimIntensity)
            {
                if (_motor.wantsToFire)
                    _leftHandAimIntensity = targetIntensity;
                else
                    Util.Move(ref _leftHandAimIntensity, targetIntensity, Time.deltaTime * 15);
            }
            else
                Util.Move(ref _leftHandAimIntensity, targetIntensity, Time.deltaTime * 15);
        }

        private void updateHeadAimIntennsity()
        {
            float targetIntensity = 0f;

            if (_motor.IsAiming && !_motor.IsMovingToCoverOffsetAndCantAim)
                targetIntensity = 1;

            if (targetIntensity > _headAimIntensity)
                Util.Lerp(ref _headAimIntensity, targetIntensity, 2);
            else
                Util.Lerp(ref _headAimIntensity, targetIntensity, 15);
        }

        private void setIKTarget(IK ik, Transform value, bool useMirror)
        {
            ik.TargetParentBone = value;

            while (ik.TargetParentBone != null && !_mirrors.ContainsKey(ik.TargetParentBone)) // Using _mirrors just to check if it is a human bone.
            {
                var parent = ik.TargetParentBone.parent;

                if (parent == null)
                {
                    ik.TargetParentBone = null;
                    break;
                }

                ik.TargetParentBone = parent;
            }

            Debug.Assert(ik.TargetParentBone != null);

            var isNonHumanTarget = ik.TargetParentBone != value;

            if (HasSwitchedHands && useMirror)
                ik.TargetParentBone = _mirrors[ik.TargetParentBone];

            if (isNonHumanTarget)
            {
                ik.OffsetOrientation = Quaternion.FromToRotation(ik.TargetParentBone.forward, value.forward);
                ik.Offset = Quaternion.Inverse(ik.OffsetOrientation) * (value.transform.position - ik.TargetParentBone.position);
            }
            else
            {
                ik.Offset = Vector3.zero;
                ik.OffsetOrientation = Quaternion.identity;
            }
        }

        private float getPenetration(Vector3 start, Vector3 end)
        {
            var vector = end - start;
            var distance = vector.magnitude;
            var direction = vector / distance;

            var count = Physics.RaycastNonAlloc(start,
                                                direction,
                                                Util.Hits,
                                                distance,
                                                0x1,
                                                QueryTriggerInteraction.Ignore);

            var penetration = distance;

            for (int i = 0; i < count; i++)
            {
                if (!Util.InHiearchyOf(Util.Hits[i].collider.gameObject, _motor.gameObject))
                {
                    if (Util.Hits[i].distance < penetration)
                        penetration = Util.Hits[i].distance;
                }
            }

            return distance - penetration;
        }

        private bool isExtremeTarget(Vector3 aimTarget)
        {
            var vector = aimTarget - _motor.AimOriginWithCoverOffset;
            var center = _motor.transform.eulerAngles.y;
            var horizontal = Util.HorizontalAngle(vector);
            var horizontalDelta = Mathf.DeltaAngle(center, horizontal);

            if (horizontalDelta > _motor.AimSettings.MaxAimAngle || horizontalDelta < -_motor.AimSettings.MaxAimAngle)
                return true;
            else
                return false;
        }

        private void clampAimTarget(ref Vector3 aimTarget)
        {
            var vector = aimTarget - _motor.AimOrigin;
            var center = _motor.transform.eulerAngles.y;
            var angle = Util.HorizontalAngle(vector);
            var delta = Mathf.DeltaAngle(center, angle);

            if (delta > _motor.AimSettings.MaxAimAngle)
                aimTarget = _motor.AimOrigin + Quaternion.AngleAxis(_motor.AimSettings.MaxAimAngle - delta, Vector3.up) * vector;
            else if (delta < -_motor.AimSettings.MaxAimAngle)
                aimTarget = _motor.AimOrigin + Quaternion.AngleAxis(-_motor.AimSettings.MaxAimAngle - delta, Vector3.up) * vector;
        }

        private void buildSupportChain(ChainElement start, ChainElement end, bool mirror)
        {
            if (HasSwitchedHands)
                buildChain(_right, bestIndex(_rightIndices, start), bestIndex(_rightIndices, end), mirror);
            else
                buildChain(_left, bestIndex(_leftIndices, start), bestIndex(_leftIndices, end), mirror);
        }

        private void buildMainChain(ChainElement start, ChainElement end, bool mirror)
        {
            if (HasSwitchedHands)
                buildChain(_left, bestIndex(_leftIndices, start), bestIndex(_leftIndices, end), mirror);
            else
                buildChain(_right, bestIndex(_rightIndices, start), bestIndex(_rightIndices, end), mirror);
        }

        private void buildMiddleChain(ChainElement start, ChainElement end)
        {
            buildChain(_middle, bestIndex(_middleIndices, start), bestIndex(_middleIndices, end), false);
        }

        private void buildChain(Transform[] bones, int start, int end, bool mirror)
        {
            var count = end - start + 1;

            if (count < 0)
                count = 0;

            if (_chain == null || _chain.Length < count)
                _chain = new IKBone[count];

            for (int i = 0; i < count; i++)
            {
                var bone = bones[end - i];
                var weight = (i == count - 1) ? 0.8f : 0.5f;

                if (mirror)
                {
                    var other = _mirrors[bone];

                    if (other != null && other != bone)
                        _chain[i] = new IKBone(bone, other, weight);
                    else
                        _chain[i] = new IKBone(bone, weight);
                }
                else
                    _chain[i] = new IKBone(bone, weight);
            }
        }

        private int bestIndex(int[] indices, ChainElement element)
        {
            var i = (int)element;

            while (i < indices.Length)
            {
                if (indices[i] >= 0)
                    return indices[i];

                i++;
            }

            i = (int)element;

            while (i > 0)
            {
                if (indices[i] >= 0)
                    return indices[i];

                i--;
            }

            return 0;
        }

        private int[] findIndices(Transform[] chain)
        {
            var result = new int[(int)ChainElement.spine + 1];

            result[(int)ChainElement.lowerArm] = findIndex(chain, HumanBodyBones.LeftLowerArm);
            if (result[(int)ChainElement.lowerArm] < 0)
                result[(int)ChainElement.lowerArm] = findIndex(chain, HumanBodyBones.RightLowerArm);

            result[(int)ChainElement.upperArm] = findIndex(chain, HumanBodyBones.LeftUpperArm);
            if (result[(int)ChainElement.upperArm] < 0)
                result[(int)ChainElement.upperArm] = findIndex(chain, HumanBodyBones.RightUpperArm);

            result[(int)ChainElement.shoulder] = findIndex(chain, HumanBodyBones.LeftShoulder);
            if (result[(int)ChainElement.shoulder] < 0)
                result[(int)ChainElement.shoulder] = findIndex(chain, HumanBodyBones.RightShoulder);

            result[(int)ChainElement.head] = findIndex(chain, HumanBodyBones.Head);
            result[(int)ChainElement.neck] = findIndex(chain, HumanBodyBones.Neck);
            result[(int)ChainElement.chest] = findIndex(chain, HumanBodyBones.Chest);
            result[(int)ChainElement.spine] = findIndex(chain, HumanBodyBones.Spine);

            return result;
        }

        private int findIndex(Transform[] chain, HumanBodyBones bone)
        {
            var value = _bones[(int)bone];

            for (int i = 0; i < chain.Length; i++)
                if (chain[i] == value)
                    return i;

            return -1;
        }

        private Transform[] findChain(Transform start)
        {
            var list = new List<Transform>();
            var node = start;

            while (node != null && _mirrors.ContainsKey(node))
            {
                list.Add(node);
                node = node.parent;
            }

            return list.ToArray();
        }
    }
}
