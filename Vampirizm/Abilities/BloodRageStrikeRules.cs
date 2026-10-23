using System;

namespace Vampirizm.Abilities
{
    // Primary claw swing: one physics step and one layered full-body pose per swing, both driven by the
    // owner's attack clock. Pure numbers only, so the tests link this file without Unity.
    //
    // Timeline (fractions of the swing; the hit window is 20–55%):
    //   0.00–0.18 wind-up: torso loads toward the striking hand, weight on the rear foot, no travel.
    //   0.20–0.48 strike: pelvis leads the spine into the same sweep as the claws, the step accelerates.
    //   0.30       impact beat: peak step speed, lead foot planted, knees and shoulder dip.
    //   0.48–0.55 follow-through: the body holds past the strike line while the step decelerates to rest.
    //   0.55–1.00 recovery: everything relaxes into locomotion.
    //
    // Angles are degrees about the player's horizontal axes: yaw about up (+ turns right), pitch about
    // right (+ tilts the top forward), roll about forward (+ lifts the right side).
    public static class BloodRageStrikeRules
    {
        public const float WindUpEnd = 0.18f, Impact = 0.3f, FollowThroughEnd = 0.48f, Settle = BloodRageCombatRules.HitEnd;
        public const float StepStart = 0.08f;
        // Half-width of the plant/dip bell in swing fractions: about 0.17 s each side at a 0.6 s swing.
        private const float DipWidth = 0.28f;

        // Metres of forward travel per swing; the two-handed finisher commits a little further.
        public static float StepDistance(int phase) => phase == 3 ? 0.75f : 0.6f;

        // +1: right hand strikes, -1: left hand, 0: both hands (overhead finisher).
        public static int StrikeSide(int phase) => phase == 1 ? -1 : phase == 3 ? 0 : 1;

        // The planted foot alternates left, right, left, right across the combo, opposite the striking hand.
        public static int LeadSide(int phase) => phase % 2 == 0 ? -1 : 1;

        // Normalised step travel 0..1: flat during the wind-up, a triangular speed profile peaking at
        // Impact, complete when recovery begins. Continuous speed at the peak.
        public static float Travel(float t)
        {
            if (float.IsNaN(t) || t <= StepStart) return 0f;
            if (t >= Settle) return 1f;
            float peakShare = (Impact - StepStart) / (Settle - StepStart);
            if (t < Impact)
            {
                float u = (t - StepStart) / (Impact - StepStart);
                return peakShare * u * u;
            }
            float v = (t - Impact) / (Settle - Impact);
            return peakShare + (1f - peakShare) * (1f - (1f - v) * (1f - v));
        }

        // d Travel / dt in swing progress units; multiply by StepDistance / duration for metres per second.
        public static float Speed(float t)
        {
            if (float.IsNaN(t) || t <= StepStart || t >= Settle) return 0f;
            float peakShare = (Impact - StepStart) / (Settle - StepStart);
            if (t < Impact) return 2f * peakShare * (t - StepStart) / ((Impact - StepStart) * (Impact - StepStart));
            return 2f * (1f - peakShare) * (1f - (t - Impact) / (Settle - Impact)) / (Settle - Impact);
        }

        public static float PoseWeight(float t)
            => SmoothStep(t / 0.06f) * (1f - SmoothStep((t - Settle) / (1f - Settle)));

        // Shared by body rotation and hand trajectories: the chest crosses the strike line with the
        // claws, rather than finishing its turn while the arms are still in their wind-up.
        public static float Sweep(float t)
            => SmoothStep((t - BloodRageCombatRules.HitStart) / (FollowThroughEnd - BloodRageCombatRules.HitStart));

        // -1 fully wound, +1 at the end of the claw sweep, relaxing to 0 by the end of the swing.
        public static float Drive(float t)
        {
            if (float.IsNaN(t) || t <= 0f || t >= 1f) return 0f;
            if (t < WindUpEnd) return -SmoothStep(t / WindUpEnd);
            if (t < FollowThroughEnd) return -1f + 2f * Sweep(t);
            if (t < Settle) return 1f - 0.3f * SmoothStep((t - FollowThroughEnd) / (Settle - FollowThroughEnd));
            return 0.7f * (1f - SmoothStep((t - Settle) / (1f - Settle)));
        }

        // Weight drop around the impact beat (knees, lead-foot plant, shoulder dip). The rising slash
        // crouches during its wind-up instead and straightens as the claws come up.
        public static float Dip(int phase, float t)
        {
            if (float.IsNaN(t) || t <= 0f) return 0f;
            float x = (t - DipCentre(phase)) / DipWidth;
            if (x <= -1f || x >= 1f) return 0f;
            float bell = 1f - x * x;
            // The early-crouching rising slash still starts from the locomotion pose.
            return bell * bell * Math.Min(1f, t / 0.08f);
        }

        public static float DipCentre(int phase) => phase == 2 ? WindUpEnd : Impact;

        // Forward lean amplitude at full drive; the rising slash leans back as it comes up.
        public static float LeanAmplitude(int phase) => phase == 3 ? 18f : phase == 2 ? -5f : 8f;

        public static StrikePose Pose(int phase, float t)
        {
            float d = Drive(t), dip = Dip(phase, t);
            int side = StrikeSide(phase), lead = LeadSide(phase);
            float lean = LeanAmplitude(phase);
            var pose = new StrikePose { Side = side, Lead = lead };
            // Torso twist: load toward the striking hand, swing through to the other side.
            // A short delay up the chain makes the weight transfer drive the shoulders.
            // Fade the lead at the endpoints so the pose starts and ends exactly at rest.
            float leadTime = 0.03f * SmoothStep(t / WindUpEnd) * (1f - SmoothStep((t - Impact) / (FollowThroughEnd - Impact)));
            pose.HipsYaw = -Drive(t + leadTime) * side * 18f;
            pose.SpineYaw = -Drive(t + leadTime * 0.67f) * side * 10f;
            pose.Spine1Yaw = -Drive(t + leadTime * 0.33f) * side * 10f;
            pose.ChestYaw = -d * side * 12f;
            // Lean back in the wind-up, forward through the strike, plus the impact crouch.
            pose.HipsPitch = d * lean * 0.35f + dip * 4f;
            pose.SpinePitch = d * lean * 0.2f + dip * 1f;
            pose.Spine1Pitch = d * lean * 0.2f + dip * 1f;
            pose.ChestPitch = d * lean * 0.25f + dip * 3f;
            // Weight shift: rear foot loaded while wound, lead foot loaded through the strike.
            pose.HipsRoll = -d * lead * 4f;
            // Small visual weight shift inside the capsule; grounded leg IK keeps the soles planted.
            pose.HipsSide = d * lead * 0.045f;
            pose.HipsDrop = -dip * (phase == 3 ? 0.085f : 0.06f);
            // Shoulder line: the striking shoulder protracts and drops; the other retracts.
            if (side == 0)
            {
                pose.RightShoulderRoll = -dip * 6f;
                pose.LeftShoulderRoll = dip * 6f;
            }
            else
            {
                float strike = -d * side * 14f, other = -d * side * 6f;
                pose.RightShoulderYaw = side > 0 ? strike : other;
                pose.LeftShoulderYaw = side < 0 ? strike : other;
                pose.RightShoulderRoll = side > 0 ? -dip * 8f : -dip * 2f;
                pose.LeftShoulderRoll = side < 0 ? dip * 8f : dip * 2f;
            }
            // Keep the eyes on the target: the head undoes three quarters of the twist.
            float totalYaw = pose.HipsYaw + pose.SpineYaw + pose.Spine1Yaw + pose.ChestYaw;
            float totalPitch = pose.HipsPitch + pose.SpinePitch + pose.Spine1Pitch + pose.ChestPitch;
            pose.NeckYaw = -0.3f * totalYaw;
            pose.HeadYaw = -0.45f * totalYaw;
            pose.HeadPitch = -0.3f * totalPitch;
            // Legs: thighs undo the hips' yaw/pitch so the feet keep facing forward; the lead leg
            // plants (thigh forward, knee bent) while the rear leg drives.
            pose.ThighCounterYaw = -pose.HipsYaw;
            pose.ThighCounterPitch = -pose.HipsPitch;
            pose.LeadThighPitch = -dip * 8f - dip * 2f;
            pose.LeadKneeBend = dip * 12f + dip * 4f;
            pose.RearThighPitch = dip * 6f - dip * 2f;
            pose.RearKneeBend = dip * 4f + dip * 4f;
            return pose;
        }

        // Fingers point at the target during the sweep, dipping for descending rakes and lifting for the riser.
        public static void ClawDirection(int phase, out float up, out float forward)
        {
            up = phase == 3 ? -0.6f : phase == 2 ? 0.2f : -0.25f;
            forward = 1f;
        }

        public static float TotalYaw(StrikePose pose) => pose.HipsYaw + pose.SpineYaw + pose.Spine1Yaw + pose.ChestYaw;

        private static float SmoothStep(float x)
        {
            x = Math.Max(0f, Math.Min(1f, x));
            return x * x * (3f - 2f * x);
        }

    }

    public struct StrikePose
    {
        public int Side, Lead;
        public float HipsYaw, HipsPitch, HipsRoll;
        public float HipsSide, HipsDrop;
        public float SpineYaw, SpinePitch, Spine1Yaw, Spine1Pitch, ChestYaw, ChestPitch;
        public float RightShoulderYaw, RightShoulderRoll, LeftShoulderYaw, LeftShoulderRoll;
        public float NeckYaw, HeadYaw, HeadPitch;
        public float ThighCounterYaw, ThighCounterPitch;
        public float LeadThighPitch, LeadKneeBend, RearThighPitch, RearKneeBend;
    }
}
