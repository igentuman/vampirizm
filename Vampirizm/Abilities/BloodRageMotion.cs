using UnityEngine;

namespace Vampirizm.Abilities
{
    // Shared procedural motion: renderer-independent hand curves in the player's metre-scale frame.
    internal static class BloodRageMotion
    {
        internal static void Hands(int phase, float progress, out Vector3 right, out Vector3 left)
        {
            float sweep = SweepProgress(progress);
            Vector3 rightStart, rightArc, rightEnd, leftStart, leftArc, leftEnd;
            if (phase <= 1)
            {
                rightStart = new Vector3(0.6f, 1.55f, 0.2f); rightEnd = new Vector3(-0.05f, 1.35f, 0.55f);
                leftStart = new Vector3(-0.6f, 1.55f, 0.2f); leftEnd = new Vector3(0.05f, 1.35f, 0.55f);
                rightArc = new Vector3(0.5f, 1.5f, 0.8f); leftArc = new Vector3(-0.5f, 1.5f, 0.8f);
            }
            else if (phase == 2)
            {
                rightStart = new Vector3(0.5f, 1.15f, 0.2f); rightEnd = new Vector3(-0.15f, 1.95f, 0.45f);
                rightArc = new Vector3(0.45f, 1.45f, 0.85f);
                leftStart = leftArc = leftEnd = new Vector3(-0.4f, 1.3f, 0.4f);
            }
            else if (phase == BloodRageCombatRules.Secondary)
            {
                // Wider, further back wind-up and a lower, longer follow-through than the primary finisher.
                rightStart = new Vector3(0.55f, 2.3f, -0.2f); rightEnd = new Vector3(0.3f, 0.65f, 1.05f);
                leftStart = new Vector3(-0.55f, 2.3f, -0.2f); leftEnd = new Vector3(-0.3f, 0.65f, 1.05f);
                rightArc = new Vector3(0.7f, 1.7f, 1.5f); leftArc = new Vector3(-0.7f, 1.7f, 1.5f);
                if (progress < BloodRageCombatRules.HitWindowStart(phase))
                {
                    // Raise both hands before takeoff, then hold them overhead until propulsion starts.
                    float raise = Mathf.SmoothStep(0f, 1f, progress * BloodRageCombatRules.SecondaryDuration / BloodRageCombatRules.SecondaryHopAt);
                    right = Vector3.Lerp(new Vector3(0.65f, 1.2f, 0.25f), rightStart, raise);
                    left = Vector3.Lerp(new Vector3(-0.65f, 1.2f, 0.25f), leftStart, raise);
                    return;
                }
                sweep = SecondarySweepProgress(progress);
            }
            else
            {
                rightStart = new Vector3(0.45f, 2.1f, 0.05f); rightEnd = new Vector3(0.28f, 1.15f, 0.6f);
                leftStart = new Vector3(-0.45f, 2.1f, 0.05f); leftEnd = new Vector3(-0.28f, 1.15f, 0.6f);
                rightArc = new Vector3(0.5f, 1.7f, 0.85f); leftArc = new Vector3(-0.5f, 1.7f, 0.85f);
            }
            // Primary arcs live in the moving torso frame; the same conversion drives damage sweeps.
            right = Arc(rightStart, rightArc, rightEnd, sweep);
            left = Arc(leftStart, leftArc, leftEnd, sweep);
            if (phase == BloodRageCombatRules.Secondary) return;
            // The free hand stays in a bent guard and follows the shoulder instead of hanging at idle.
            if (phase == 0 || phase == 2) left = new Vector3(-0.38f, 1.4f, 0.4f);
            if (phase == 1) right = new Vector3(0.38f, 1.4f, 0.4f);
            StrikePose pose = BloodRageStrikeRules.Pose(phase, progress);
            float weight = BloodRageStrikeRules.PoseWeight(progress);
            Vector3 pivot = Vector3.up * 1.35f;
            Quaternion body = BodyRotation(pose, weight);
            Vector3 shift = new Vector3(pose.HipsSide, pose.HipsDrop, 0f) * weight;
            right = pivot + body * (right - pivot) + shift;
            left = pivot + body * (left - pivot) + shift;
        }

        internal static Quaternion BodyRotation(StrikePose pose, float weight)
            => Quaternion.Euler((pose.HipsPitch + pose.SpinePitch + pose.Spine1Pitch + pose.ChestPitch) * weight,
                BloodRageStrikeRules.TotalYaw(pose) * weight, pose.HipsRoll * weight);

        private static Vector3 Arc(Vector3 start, Vector3 control, Vector3 end, float t)
            => Vector3.Lerp(Vector3.Lerp(start, control, t), Vector3.Lerp(control, end, t), t);

        internal static float SweepProgress(float progress)
            => BloodRageStrikeRules.Sweep(progress);

        // The rake completes well before the dash ends, then holds its extended pose while the body travels.
        internal static float SecondarySweepProgress(float progress)
            => Snap(Mathf.InverseLerp(BloodRageCombatRules.HitWindowStart(BloodRageCombatRules.Secondary),
                BloodRageCombatRules.SecondaryRakeEnd, progress));

        // Fast ease-out with a slight overshoot: the claws slam past the end point and settle back.
        internal static float Snap(float x)
        {
            const float back = 1.2f;
            x = Mathf.Clamp01(x);
            float u = x - 1f;
            return 1f + u * u * ((back + 1f) * u + back);
        }

        internal static Vector3 Forward(Vector3 direction, Vector3 fallback)
        {
            Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(fallback, Vector3.up);
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        }

        internal static Vector3 WorldPoint(Player player, Vector3 local, Vector3 direction)
            => player.GetCenterPoint() + Quaternion.LookRotation(Forward(direction, player.transform.forward)) * (local - Vector3.up * 1.1f);
    }
}
