using UnityEngine;

namespace CombatSystem
{
    public static class WeaponSpreadModel
    {
        public struct SpreadInput
        {
            public PenStyle penStyle;
            public bool isGrounded;
            public bool isRunning;
            public bool isMoving;
            public float horizontalSpeed;
            public float prevHorizontalSpeed;
            public int consecutiveShots;
            public float timeSinceLastShot;
        }

        public static float ComputeSpreadDegrees(in SpreadInput input, WeaponEntry weapon)
        {
            float spread = 0.3f;

            switch (input.penStyle)
            {
                case PenStyle.Ballpoint:
                    spread += 0.15f;
                    break;
                case PenStyle.Fineliner:
                    spread -= 0.08f;
                    break;
                case PenStyle.Fountain:
                    spread -= 0.05f;
                    break;
            }

            if (!input.isGrounded)
                spread += 8f;
            else if (input.isRunning)
                spread += 4f;
            else if (input.isMoving)
                spread += 1.5f;

            spread += input.consecutiveShots * 0.4f;

            if (input.prevHorizontalSpeed > 2f && input.horizontalSpeed < 0.3f)
                spread *= 0.3f;
            else if (input.penStyle == PenStyle.Fountain && input.horizontalSpeed < 0.3f)
                spread *= 0.85f;

            float maxSpread = weapon != null ? weapon.maxSpread : 6f;
            return Mathf.Clamp(spread, 0.05f, maxSpread);
        }

        public static float ComputeRecoilKick(PenStyle penStyle)
        {
            switch (penStyle)
            {
                case PenStyle.Ballpoint: return 0.12f;
                case PenStyle.Fountain: return 0.2f;
                case PenStyle.Fineliner: return 0.1f;
                case PenStyle.Rollerball: return 0.13f;
                default: return 0.15f;
            }
        }

        public static float ComputeFireInterval(WeaponEntry weapon)
        {
            if (weapon == null)
                return 0.12f;
            float rate = Mathf.Max(1f, weapon.fireRate);
            float interval = 1f / rate;
            switch (weapon.penStyle)
            {
                case PenStyle.Ballpoint:
                    return interval * 0.85f;
                case PenStyle.Fountain:
                    return interval * 1.25f;
                default:
                    return interval;
            }
        }

        public static float ComputeEffectiveRange(WeaponEntry weapon)
        {
            if (weapon == null)
                return 80f;
            float range = weapon.maxRange;
            if (weapon.penStyle == PenStyle.Fineliner)
                range *= 0.85f;
            return range;
        }

        public static Vector3 ApplySpread(Vector3 direction, float spreadDegrees)
        {
            if (spreadDegrees <= 0.01f)
                return direction.normalized;

            float yaw = Random.Range(-spreadDegrees, spreadDegrees);
            float pitch = Random.Range(-spreadDegrees, spreadDegrees);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            return (rot * direction).normalized;
        }
    }
}
