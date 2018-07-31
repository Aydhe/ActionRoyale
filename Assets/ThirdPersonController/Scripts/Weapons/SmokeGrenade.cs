﻿using UnityEngine;

namespace CoverShooter
{
    /// <summary>
    /// A version of a grenade that applies a VisionBuff to objects with CharacterMotor.
    /// </summary>
    public class SmokeGrenade : Grenade
    {
        public float VisionMultiplier = 0.1f;
        public float Duration = 6;

        public SmokeGrenade()
        {
            CenterDamage = 0;
        }

        /// <summary>
        /// Adds a VisionBuff to the target object, but only if it contains CharacterMotor.
        /// </summary>
        protected override void Apply(GameObject target, Vector3 position, Vector3 normal, float fraction)
        {
            base.Apply(target, position, normal, fraction);

            var motor = target.GetComponent<CharacterMotor>();
            if (motor == null) return;

            var buff = target.GetComponent<VisionBuff>();

            if (buff == null || buff.enabled)
                buff = target.gameObject.AddComponent<VisionBuff>();

            buff.Duration = Duration;
            buff.Multiplier = VisionMultiplier;
            buff.Launch();
        }
    }
}
