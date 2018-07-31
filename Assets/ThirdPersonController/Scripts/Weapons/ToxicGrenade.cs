using UnityEngine;

namespace CoverShooter
{
    /// <summary>
    /// A version of a grenade that applies a DamageBuff to objects with CharacterMotor.
    /// </summary>
    public class ToxicGrenade : Grenade
    {
        public float DamageMultiplier = 0.5f;
        public float Duration = 6;

        public ToxicGrenade()
        {
            CenterDamage = 0;
        }

        /// <summary>
        /// Adds a DamageBuff to the target object, but only if it contains CharacterMotor.
        /// </summary>
        protected override void Apply(GameObject target, Vector3 position, Vector3 normal, float fraction)
        {
            base.Apply(target, position, normal, fraction);

            var motor = target.GetComponent<CharacterMotor>();
            if (motor == null) return;

            var buff = target.GetComponent<DamageBuff>();

            if (buff == null || buff.enabled)
                buff = target.gameObject.AddComponent<DamageBuff>();

            buff.Duration = Duration;
            buff.Multiplier = DamageMultiplier;
            buff.Launch();
        }
    }
}
