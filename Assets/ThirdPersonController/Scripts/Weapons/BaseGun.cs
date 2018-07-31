using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoverShooter
{
    /// <summary>
    /// Guns raycasts bullets, manage magazine and recoil.
    /// For player characters bullets originate at camera in order for player to be able to fire on targets they can see, even if there is a small obstacle in front of the gun. The fire origin is set by a camera. Since AI do not have Third Person Camera attached their bullets are fired starting from the Aim marker, which usually is at the end of the gun.
    /// Each weapon has two marker objects. Aim defines point of origin for AI bullets and is also used when rotating character’s arms till the marker points towards the target. Left Hand object marks the position for character’s left hand. Naming of left and right hands refers to the general case as character’s handedness can be swapped.
    /// The intended position of the left hand might differ in some animations, to handle that there are left hand marker overwrites you can use to set up IK for the left hand for some specific situations. Empty values are not used as overwrites.
    /// Currently there are two kinds of weapons, pistols and rifles. The type defines character animations when using the weapon.
    /// </summary>
    public abstract class BaseGun : MonoBehaviour
    {
        /// <summary>
        /// Point of creation for bullets in world space.
        /// </summary>
        public Vector3 Origin { get { return Aim == null ? transform.position : Aim.transform.position; } }

        /// <summary>
        /// Direction of bullets when created at the origin.
        /// </summary>
        public Vector3 Direction { get { return Aim == null ? transform.forward : Aim.transform.forward; } }

        /// <summary>
        /// Whether the gun can be used to perform a melee hit right now.
        /// </summary>
        public bool CanHit
        {
            get { return _hitWait <= 0; }
        }

        /// <summary>
        /// Returns true if the gun fired during the last update.
        /// </summary>
        public bool HasJustFired
        {
            get { return _hasJustFired; }
        }

        /// <summary>
        /// Returns true if the gun is allowed to fire.
        /// </summary>
        public bool IsAllowed
        {
            get { return _isAllowed; }
        }

        /// <summary>
        /// Renderer attached to the object.
        /// </summary>
        public Renderer Renderer
        {
            get { return _renderer; }
        }

        /// <summary>
        /// Origin to cast bullets from.
        /// </summary>
        public Vector3 RaycastOrigin
        {
            get { return _isUsingCustomRaycast ? _customRaycastOrigin : Origin; }
        }

        /// <summary>
        /// Target position at which bullets are fired at.
        /// </summary>
        public Vector3 RaycastTarget
        {
            get { return _isUsingCustomRaycast ? _customRaycastTarget : (Origin + Direction * Distance); }
        }

        /// <summary>
        /// Are raycast settings setup manually by some other component.
        /// </summary>
        public bool HasRaycastSetup
        {
            get { return _isUsingCustomRaycast; }
        }

        /// <summary>
        /// Can the gun be loaded with more bullets.
        /// </summary>
        public abstract bool CanLoad { get; }

        /// <summary>
        /// Number of bullets left in the gun.
        /// </summary>
        public abstract int LoadedBulletsLeft { get; }

        /// <summary>
        /// Is the gun fully loaded with bullets.
        /// </summary>
        public abstract bool IsFullyLoaded { get; }

        /// <summary>
        /// Load percentage for the ammo ui.
        /// </summary>
        public abstract float LoadPercentage { get; }

        /// <summary>
        /// Name of the gun to be display on the HUD.
        /// </summary>
        [Tooltip("Name of the gun to be display on the HUD.")]
        public string Name = "Gun";

        /// <summary>
        /// How many degrees should the camera FOV be reduced when using scope on the gun.
        /// </summary>
        [Tooltip("How many degrees should the camera FOV be reduced when using scope on the gun.")]
        public float Zoom = 30;

        /// <summary>
        /// Sprite that's displayed when zooming in.
        /// </summary>
        [Tooltip("Sprite that's displayed when zooming in.")]
        public Sprite Scope;

        /// <summary>
        /// Rate of fire in shots per second.
        /// </summary>
        [Tooltip("Rate of fire in shots per second.")]
        [Range(0, 1000)]
        public float Rate = 7;

        /// <summary>
        /// Bullets fired per single shot.
        /// </summary>
        [Tooltip("Bullets fired per single shot.")]
        public int BulletsPerShot = 1;

        /// <summary>
        /// If firing multiple bullets per shot, should only a single bullet be removed from the inventory.
        /// </summary>
        [Tooltip("If firing multiple bullets per shot, should only a single bullet be removed from the inventory.")]
        public bool ConsumeSingleBulletPerShot = true;

        /// <summary>
        /// Maximum distance of a bullet hit. Objects further than this value are ignored.
        /// </summary>
        [Tooltip("Maximum distance of a bullet hit. Objects further than this value are ignored.")]
        public float Distance = 50;

        /// <summary>
        /// Damage dealt by a single bullet.
        /// </summary>
        [Tooltip("Damage dealt by a single bullet.")]
        [Range(0, 1000)]
        public float Damage = 10;

        /// <summary>
        /// Maximum degrees of error the gun can make when firing.
        /// </summary>
        [Tooltip("Maximum degrees of error the gun can make when firing.")]
        public float Error = 0;

        /// <summary>
        /// Should the gun be reloaded automatically when the magazine is empty.
        /// </summary>
        [Tooltip("Should the gun be reloaded automatically when the magazine is empty.")]
        public bool AutoReload = false;

        /// <summary>
        /// Is the gun reloaded with whole magazines or bullet by bullet.
        /// </summary>
        [Tooltip("Is the gun reloaded with whole magazines or bullet by bullet.")]
        public bool ReloadWithMagazines = true;

        /// <summary>
        /// If reloading bullet by bullet, can the gun be fired during reload.
        /// </summary>
        [Tooltip("If reloading bullet by bullet, can the gun be fired during reload.")]
        public bool CanInterruptBulletLoad = true;

        /// <summary>
        /// After a new magazine or the last bullet is loaded, should the gun be pumped.
        /// </summary>
        [Tooltip("After a new magazine or the last bullet is loaded, should the gun be pumped.")]
        public bool PumpAfterFinalLoad = false;

        /// <summary>
        /// Should the gun be pumped after each bullet load.
        /// </summary>
        [Tooltip("Should the gun be pumped after each bullet load.")]
        public bool PumpAfterBulletLoad = false;

        /// <summary>
        /// Should the gun be pumped after firing a shot.
        /// </summary>
        [Tooltip("Should the gun be pumped after firing a shot.")]
        public bool PumpAfterFire = false;

        /// <summary>
        /// Damage done by a melee attack.
        /// </summary>
        [Tooltip("Damage done by a melee attack.")]
        public float MeleeDamage = 20;

        /// <summary>
        /// Distance of a sphere that checks for melee targets in front of the character.
        /// </summary>
        [Tooltip("Distance of a sphere that checks for melee targets in front of the character.")]
        public float MeleeDistance = 1.5f;

        /// <summary>
        /// Radius of a sphere that checks for melee targets in front of the character.
        /// </summary>
        [Tooltip("Radius of a sphere that checks for melee targets in front of the character.")]
        public float MeleeRadius = 1.0f;

        /// <summary>
        /// Height of a sphere that checks for melee targets in front of the character.
        /// </summary>
        [Tooltip("Height of a sphere that checks for melee targets in front of the character.")]
        public float MeleeHeight = 0.5f;

        /// <summary>
        /// Time in seconds for to wait for another melee hit.
        /// </summary>
        [Tooltip("Time in seconds for to wait for another melee hit.")]
        public float HitCooldown = 0.4f;

        /// <summary>
        /// Will the character fire by just aiming the mobile controller.
        /// </summary>
        [Tooltip("Will the character fire by just aiming the mobile controller.")]
        public bool FireOnMobileAim = true;

        /// <summary>
        /// Should the laser be visible only when zooming.
        /// </summary>
        [Tooltip("Should the laser be visible only when zooming.")]
        public bool LaserOnlyOnZoom = true;

        /// <summary>
        /// Should a debug ray be displayed.
        /// </summary>
        [Tooltip("Should a debug ray be displayed.")]
        public bool DebugAim = false;

        /// <summary>
        /// Link to the object that controls the aiming direction.
        /// </summary>
        [Tooltip("Link to the object that controls the aiming direction.")]
        public GameObject Aim;

        /// <summary>
        /// Object to be instantiated as a bullet.
        /// </summary>
        [Tooltip("Object to be instantiated as a bullet.")]
        public GameObject Bullet;

        /// <summary>
        /// Link to the object that controls the position of character's left hand relative to the weapon.
        /// </summary>
        [Tooltip("Link to the object that controls the position of character's left hand relative to the weapon.")]
        public Transform LeftHandDefault;

        /// <summary>
        /// Should the gun's crosshair be used instead of the one set in the Crosshair component.
        /// </summary>
        [Tooltip("Should the gun's crosshair be used instead of the one set in the Crosshair component.")]
        public bool UseCustomCrosshair;

        /// <summary>
        /// Custom crosshair settings to override the ones set in the Crosshair component. Used only if UseCustomCrosshair is enabled.
        /// </summary>
        [Tooltip("Custom crosshair settings to override the ones set in the Crosshair component. Used only if UseCustomCrosshair is enabled.")]
        public CrosshairSettings CustomCrosshair = CrosshairSettings.Default();

        /// <summary>
        /// Settings that manage gun's recoil behaviour.
        /// </summary>
        [Tooltip("Settings that manage gun's recoil behaviour.")]
        public GunRecoilSettings Recoil = GunRecoilSettings.Default();

        /// <summary>
        /// Links to objects that overwrite the value in LeftHand based on the gameplay situation.
        /// </summary>
        [Tooltip("Links to objects that overwrite the value in LeftHand based on the gameplay situation.")]
        public HandOverwrite LeftHandOverwrite;

        /// <summary>
        /// Force the pistol to use this laser instead of finding one on its own.
        /// </summary>
        [Tooltip("Force the pistol to use this laser instead of finding one on its own.")]
        public Laser LaserOverwrite;

        /// <summary>
        /// Owning object with a CharacterMotor component.
        /// </summary>
        [HideInInspector]
        public CharacterMotor Character;

        /// <summary>
        /// Event executed at the beginning of a magazine load animation.
        /// </summary>
        public Action MagazineLoadStarted;

        /// <summary>
        /// Event executed after the gun has been fully loaded.
        /// </summary>
        public Action FullyLoaded;

        /// <summary>
        /// Event executed at the beginning of a bullet load animation.
        /// </summary>
        public Action BulletLoadStarted;

        /// <summary>
        /// Event executed after a bullet has been loaded.
        /// </summary>
        public Action BulletLoaded;

        /// <summary>
        /// Event executed at the start of a pump animation.
        /// </summary>
        public Action PumpStarted;

        /// <summary>
        /// Event executed at the end of a pump animation.
        /// </summary>
        public Action Pumped;

        /// <summary>
        /// Event executed after a successful fire.
        /// </summary>
        public Action Fired;

        /// <summary>
        /// Event executed every time there is an attempt to fire with no bullets in the gun.
        /// </summary>
        public Action EmptyFire;

        /// <summary>
        /// Event executed after a series of bullet fires as started.
        /// </summary>
        public Action FireStarted;

        /// <summary>
        /// Event executed after a series of bullet fires has stopped.
        /// </summary>
        public Action FireStopped;

        /// <summary>
        /// Event executed after a bullet hit something.
        /// </summary>
        public Action<Hit> SuccessfulyHit;

        private Renderer _renderer;

        private bool _hasJustFired;

        private bool _isUsingCustomRaycast;
        private Vector3 _customRaycastOrigin;
        private Vector3 _customRaycastTarget;

        private float _fireWait = 0;
        private bool _isGoingToFire;
        private bool _isFiringOnNextUpdate;
        private bool _isAllowed;
        private bool _wasAllowedAndFiring;

        private float _hitWait = 0;

        private RaycastHit[] _hits = new RaycastHit[16];

        private Laser _laser;

        private bool _isIgnoringSelf = true;
        private bool _hasFireCondition;
        private int _fireConditionSide;

        private float _additionalError = 0;
        private float _errorMultiplier = 1;

        private bool _hasUpdatedThisFrame;
        private bool _hasManuallyUpdated;

        /// <summary>
        /// Command to use the weapon.
        /// </summary>
        public void ToUse()
        {
            TryFireNow();
        }

        /// <summary>
        /// Notified of a magazine load start by the CharacterMotor.
        /// </summary>
        public virtual void OnMagazineLoadStart()
        {
            if (MagazineLoadStarted != null)
                MagazineLoadStarted();
        }

        /// <summary>
        /// Notified of a magazine load start by the CharacterMotor.
        /// </summary>
        public virtual void OnBulletLoadStart()
        {
            if (BulletLoadStarted != null)
                BulletLoadStarted();
        }

        /// <summary>
        /// Notified of a magazine load start by the CharacterMotor.
        /// </summary>
        public virtual void OnPumpStart()
        {
            if (PumpStarted != null)
                PumpStarted();
        }

        /// <summary>
        /// Notified of a magazine load start by the CharacterMotor.
        /// </summary>
        public virtual void OnPumped()
        {
            if (Pumped != null)
                Pumped();
        }

        /// <summary>
        /// Get the LineRenderer if there is one.
        /// </summary>
        protected virtual void Start()
        {
            _laser = LaserOverwrite;

            if (_laser == null)
                _laser = GetComponent<Laser>();

            if (_laser == null)
                _laser = GetComponentInChildren<Laser>();

            if (_laser == null && transform.parent != null)
            {
                _laser = transform.parent.GetComponentInChildren<Laser>();

                if (_laser != null && _laser.GetComponent<BaseGun>() != null)
                    _laser = null;
            }
        }

        /// <summary>
        /// Sets the gun to ignore hitting it's owner.
        /// </summary>
        public void IgnoreSelf(bool value = true)
        {
            _isIgnoringSelf = value;
        }

        /// <summary>
        /// Sets the gun to not fire if aiming at a friend.
        /// </summary>
        public void SetFireCondition(int side)
        {
            _hasFireCondition = true;
            _fireConditionSide = side;
        }

        /// <summary>
        /// Sets the gun to fire in any condition.
        /// </summary>
        public void CancelFireCondition()
        {
            _hasFireCondition = false;
        }

        /// <summary>
        /// Returns a game object the gun is currently aiming at.
        /// </summary>
        public GameObject FindCurrentAimedTarget()
        {
            var hit = Raycast();

            if (hit.collider != null)
                return hit.collider.gameObject;
            else
                return null;
        }

        /// <summary>
        /// Returns a game object with CharacterHealth the gun is currently aiming at.
        /// </summary>
        public GameObject FindCurrentAimedHealthTarget()
        {
            return getHealthTarget(FindCurrentAimedTarget());
        }

        /// <summary>
        /// Finds best CharacterHealth gameobject.
        /// </summary>
        private GameObject getHealthTarget(GameObject target)
        {
            while (target != null)
            {
                var health = CharacterHealth.Get(target);

                if (health != null)
                {
                    if (health.Health <= float.Epsilon)
                        target = null;

                    break;
                }

                var parent = target.transform.parent;

                if (parent != null)
                    target = parent.gameObject;
                else
                    target = null;
            }

            return target;
        }

        private void OnValidate()
        {
            Distance = Mathf.Max(0, Distance);
        }

        /// <summary>
        /// Calculates direction to target from origin.
        /// </summary>
        private Vector3 calculateRaycastDirection()
        {
            var direction = (RaycastTarget - RaycastOrigin).normalized;

            var error = (_additionalError + Error * _errorMultiplier) * 0.5f;
            var x = UnityEngine.Random.Range(-error, error);
            var y = UnityEngine.Random.Range(-error, error);

            var up = Vector3.up;
            if (direction.y > 0.99f || direction.y < -0.99f)
                up = Vector3.forward;

            var right = Vector3.Cross(up, direction);

            if (error > 0.1f && x * x + y * y >= error * error)
            {
                var magnitude = Mathf.Sqrt(x * x + y * y) * error;
                x /= magnitude;
                y /= magnitude;

                x *= UnityEngine.Random.Range(-1f, 1f);
                y *= UnityEngine.Random.Range(-1f, 1f);
            }

            return Quaternion.AngleAxis(x, right) * Quaternion.AngleAxis(y, up) * direction;
        }

        /// <summary>
        /// Finds the renderer.
        /// </summary>
        protected virtual void Awake()
        {
            _renderer = GetComponent<Renderer>();
        }

        public abstract bool LoadMagazine();
        public abstract bool LoadBullet();
        protected abstract void Consume();

        /// <summary>
        /// Perform the hit attack. Animation is carried out by the character.
        /// </summary>
        public void MeleeHit()
        {
            if (Character == null || _hitWait > 0)
                return;

            _hitWait = HitCooldown;

            var position = Character.transform.position + Vector3.up * MeleeHeight;

            var bestHit = new RaycastHit();
            var bestHitHasHealth = false;
            var count = Physics.SphereCastNonAlloc(position, MeleeRadius, Character.transform.forward, _hits, MeleeDistance);

            for (int i = 0; i < count; i++)
                if (_hits[i].collider != null && _hits[i].collider.gameObject != null && _hits[i].collider.gameObject != Character)
                {
                    var hit = _hits[i];
                    var hasHealth = hit.collider.GetComponent<CharacterHealth>() != null;

                    if (bestHit.collider == null ||
                        (hasHealth && !bestHitHasHealth) ||
                        (hit.distance < bestHit.distance && (!bestHitHasHealth || hasHealth)))
                    {
                        bestHit = hit;
                        bestHitHasHealth = hasHealth;
                    }
                }

            if (bestHit.collider != null)
                bestHit.collider.SendMessage("OnHit",
                                             new Hit(bestHit.point, bestHit.normal, MeleeDamage, Character.gameObject, bestHit.collider.gameObject),
                                             SendMessageOptions.DontRequireReceiver);
        }

        /// <summary>
        /// Sets the gun to try firing during the next update.
        /// Gun fires only when both fire mode is on and the gun is allowed to fire.
        /// </summary>
        public void TryFireNow()
        {
            _isFiringOnNextUpdate = true;
        }

        /// <summary>
        /// Sets the fire mode on. It stays on until CancelFire() is called or the gun has fired.
        /// Gun fires only when both fire mode is on and the gun is allowed to fire.
        /// </summary>
        public void FireWhenReady()
        {
            _isGoingToFire = true;
        }

        /// <summary>
        /// Sets the fire mode off.
        /// </summary>
        public void CancelFire()
        {
            _isGoingToFire = false;
        }

        /// <summary>
        /// Sets whether the gun is allowed to fire. Manipulated when changing weapons or a reload animation is playing.
        /// </summary>
        /// <param name="value"></param>
        public void Allow(bool value)
        {
            _isAllowed = value;
        }

        /// <summary>
        /// Sets the position from which bullets are spawned. The game usually sets it as the camera position.
        /// </summary>
        public void SetupRaycastThisFrame(Vector3 origin, Vector3 target)
        {
            _isUsingCustomRaycast = true;
            _customRaycastOrigin = origin;
            _customRaycastTarget = target;
        }

        /// <summary>
        /// Sets the aim error in degrees for the next frame. Errors are stacked.
        /// </summary>
        public void AddErrorThisFrame(float degrees)
        {
            _additionalError += degrees;
        }

        /// <summary>
        /// Sets the base error (Error property) multiplier for this frame.
        /// </summary>
        public void SetBaseErrorMultiplierThisFrame(float multiplier)
        {
            _errorMultiplier = multiplier;
        }

        /// <summary>
        /// Call the update method manually. Performed by the CharacterMotor, in order to fire the weapon after the weapon has performed it's IK.
        /// </summary>
        public void UpdateManually()
        {
            _hasManuallyUpdated = true;

            if (!_hasUpdatedThisFrame)
                Frame();
        }

        private void LateUpdate()
        {
            if (!_hasManuallyUpdated)
            {
                _hasUpdatedThisFrame = true;
                Frame();
            }
            else
            {
                _hasUpdatedThisFrame = false;
                _hasManuallyUpdated = false;
            }
        }

        protected virtual void Frame()
        {
            _hasJustFired = false;

            if (_isGoingToFire)
                _isFiringOnNextUpdate = true;

            if (_hitWait >= 0)
                _hitWait -= Time.deltaTime;

            if (DebugAim)
            {
                Debug.DrawLine(Origin, Origin + (RaycastTarget - Origin).normalized * Distance, Color.red);

                if (_isUsingCustomRaycast)
                    Debug.DrawLine(_customRaycastOrigin, _customRaycastTarget, Color.green);
            }

            // Notify character if the trigger is pressed. Used to make faces.
            {
                var isAllowedAndFiring = _isGoingToFire && _isAllowed;

                if (Character != null)
                {
                    if (isAllowedAndFiring && !_wasAllowedAndFiring)
                    {
                        Character.gameObject.SendMessage("OnStartGunFire", SendMessageOptions.DontRequireReceiver);
                        if (FireStarted != null) FireStarted.Invoke();
                    }

                    if (!isAllowedAndFiring && _wasAllowedAndFiring)
                    {
                        Character.gameObject.SendMessage("OnStopGunFire", SendMessageOptions.DontRequireReceiver);
                        if (FireStopped != null) FireStopped.Invoke();
                    }
                }

                _wasAllowedAndFiring = isAllowedAndFiring;
            }

            _fireWait -= Time.deltaTime;

            // Check if the trigger is pressed.
            if (_isFiringOnNextUpdate && _isAllowed)
            {
                // Time in seconds between bullets.
                var fireDelay = 1.0f / Rate;

                var delay = 0f;

                // Fire all bullets in this frame.
                while (_fireWait < 0)
                {
                    var hasFired = false;

                    for (int i = 0; i < BulletsPerShot; i++)
                    {
                        if (LoadedBulletsLeft <= 0)
                            break;

                        if (fire(delay, !ConsumeSingleBulletPerShot))
                            hasFired = true;
                    }

                    if (hasFired && ConsumeSingleBulletPerShot)
                        Consume();

                    if (hasFired)
                    {
                        SendMessage("OnFire", delay, SendMessageOptions.DontRequireReceiver);
                        if (Fired != null) Fired.Invoke();

                        if (Character != null)
                        {
                            if (PumpAfterFire)
                                Character.InputPump(0.1f);

                            Character.InputRecoil(Recoil.Vertical, Recoil.Horizontal);
                            ThirdPersonCamera.Shake(Character, Recoil.ShakeIntensity, Recoil.ShakeTime);
                        }
                    }
                    else
                    {
                        SendMessage("OnEmptyFire", SendMessageOptions.DontRequireReceiver);
                        if (EmptyFire != null) EmptyFire.Invoke();
                    }

                    delay += fireDelay;
                    _fireWait += fireDelay;
                    _isGoingToFire = false;
                }
            }

            _isFiringOnNextUpdate = false;
            _isUsingCustomRaycast = false;

            // Clamp fire delay timer.
            if (_fireWait < 0) _fireWait = 0;

            if (_laser != null && Character != null)
                _laser.Alpha = Character.IsZooming ? 1 : 0;

            adjustLaser();

            _additionalError = 0;
            _errorMultiplier = 1;
        }

        private void adjustLaser()
        {
            // Adjust the laser.
            if (_laser != null)
            {
                var origin = Origin;
                var direction = Direction;

                bool isFriend;
                var hit = Raycast(origin, direction, out isFriend, false);

                if (hit.collider == null)
                    _laser.Setup(origin, origin + direction * Distance);
                else
                    _laser.Setup(origin, hit.point);
            }
        }

        /// <summary>
        /// Cast a single bullet using raycasting.
        /// </summary>
        private bool fire(float delay, bool consume)
        {
            bool isFriend;
            var direction = calculateRaycastDirection();
            var hit = Raycast(RaycastOrigin, direction, out isFriend, true);

            if (Character != null)
                Character.KeepAiming();

            if (!isFriend)
            {
                var end = hit.point;

                if (consume)
                    Consume();

                if (hit.collider == null)
                    end = RaycastOrigin + Distance * direction;

                var damage = Damage * (Character != null ? Character.DamageMultiplier : 1);

                if (Bullet != null)
                {
                    var bullet = GameObject.Instantiate(Bullet);
                    bullet.transform.position = Origin;
                    bullet.transform.LookAt(end);

                    var projectile = bullet.GetComponent<Projectile>();
                    var vector = end - Origin;

                    var trail = bullet.GetComponent<TrailRenderer>();
                    if (trail == null) trail = bullet.GetComponentInChildren<TrailRenderer>();

                    if (trail != null)
                        trail.Clear();

                    if (projectile != null)
                    {
                        projectile.Distance = vector.magnitude;
                        projectile.Direction = vector.normalized;

                        if (hit.collider != null)
                        {
                            projectile.Target = hit.collider.gameObject;
                            projectile.Hit = new Hit(hit.point, -direction, damage, Character.gameObject, hit.collider.gameObject);
                        }

                    }
                    else if (hit.collider != null)
                        hit.collider.SendMessage("OnHit",
                                                 new Hit(hit.point, -direction, damage, Character.gameObject, hit.collider.gameObject),
                                                 SendMessageOptions.DontRequireReceiver);

                    bullet.SetActive(true);
                }
                else if (hit.collider != null)
                    hit.collider.SendMessage("OnHit",
                                             new Hit(hit.point, -direction, damage, Character.gameObject, hit.collider.gameObject),
                                             SendMessageOptions.DontRequireReceiver);

                if (hit.collider != null && Character != null)
                {
                    var hitStruct = new Hit(hit.point, -direction, damage, Character.gameObject, hit.collider.gameObject);
                    Character.SendMessage("OnSuccessfulHit", hitStruct, SendMessageOptions.DontRequireReceiver);
                    if (SuccessfulyHit != null) SuccessfulyHit.Invoke(hitStruct);
                }

                _hasJustFired = true;
                return true;
            }
            else
            {
                _hasJustFired = true;
                return false;
            }
        }

        /// <summary>
        /// Finds an object and a hit position a bullet would hit if fired.
        /// </summary>
        public RaycastHit Raycast()
        {
            bool isFriend;
            return Raycast(RaycastOrigin, (RaycastTarget - RaycastOrigin).normalized, out isFriend, false);
        }

        /// <summary>
        /// Finds an object and a hit position a bullet would hit if fired. Checks if it is a friend.
        /// </summary>
        public RaycastHit Raycast(Vector3 origin, Vector3 direction, out bool isFriend, bool friendCheck)
        {
            RaycastHit closestHit = new RaycastHit();
            float closestDistance = Distance * 10;

            var minDistance = 0f;

            if (_isUsingCustomRaycast)
                minDistance = Vector3.Distance(Origin, RaycastOrigin);

            if (minDistance > 0.5f)
                minDistance -= 0.5f;

            isFriend = false;
            var count = Physics.RaycastNonAlloc(origin, direction, _hits, Distance);

            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];

                if (Character != null && Util.InHiearchyOf(hit.collider.gameObject, Character.gameObject))
                    continue;

                if (hit.distance < closestDistance && hit.distance > minDistance)
                {
                    var isOk = true;
                    var isShield = false;

                    if (hit.collider.isTrigger)
                    {
                        if (BodyPartHealth.Contains(hit.collider.gameObject))
                            isOk = true;
                        else
                        {
                            var shield = BulletShield.Get(hit.collider.gameObject);

                            if (shield != null)
                            {
                                if (Vector3.Dot(shield.transform.forward, hit.normal) >= -0.2f)
                                {
                                    isOk = true;
                                    isShield = true;
                                }
                                else
                                    isOk = false;
                            }
                            else
                                isOk = false;
                        }
                    }
                    else
                    {
                        var health = CharacterHealth.Get(hit.collider.gameObject);

                        if (health != null)
                            isOk = health.IsRegisteringHits;
                    }

                    if (isOk)
                    {
                        if (!isShield && (_isIgnoringSelf || _hasFireCondition) && friendCheck)
                        {
                            var root = getHealthTarget(hit.collider.gameObject);

                            if (root != null)
                            {
                                if (_isIgnoringSelf && Character != null && root == Character.gameObject)
                                    isFriend = true;
                                else if (_hasFireCondition)
                                {
                                    var actor = Actors.Get(root);

                                    if (actor != null)
                                        isFriend = actor.Side == _fireConditionSide;
                                    else
                                        isFriend = false;
                                }
                                else
                                    isFriend = false;
                            }
                            else
                                isFriend = false;
                        }

                        closestHit = hit;
                        closestDistance = hit.distance;
                    }
                }
            }

            return closestHit;
        }
    }
}