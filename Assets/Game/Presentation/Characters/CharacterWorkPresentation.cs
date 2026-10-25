using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Characters
{
    public sealed partial class CharacterPresenter
    {
        private readonly Dictionary<Transform, Quaternion> poseBefore = new();
        private readonly Dictionary<Transform, Quaternion> poseAfter = new();
        private Quaternion lastLeftHandOffset = Quaternion.identity;
        private float previousPoseTime, workPoseClock, huntingDraw;
        private int lastWorkImpactCycle;
        private bool huntingReleased;
        private GameObject fishingRod;
        private LineRenderer fishingLine, bowString;
        private Material workLineMaterial, rodMaterial;
        private Vector3 fishingTarget;
        public int WorkImpactSequence { get; private set; }
        public Vector3 WorkContactPoint => workContact;
        public Vector3 BowLaunchPoint => huntingBow != null ? huntingBow.transform.position : transform.position + Vector3.up * 1.3f;
        public Vector3 ArrowNockPoint => animator != null && animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.RightHand).position : BowLaunchPoint;
        public bool HasFishingRod => fishingRod != null && fishingRod.activeInHierarchy;
        public void SetFishingTarget(Vector3 point) { fishingTarget = point; }
        public void SetHuntingDraw(float fraction, bool released)
        { huntingDraw = Mathf.Clamp01(fraction); huntingReleased = released; }

        private LineRenderer CreateWorkLine(string name, Transform parent, float width)
        {
            workLineMaterial ??= new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            workLineMaterial.SetColor("_BaseColor", new Color(.83f,.76f,.58f));
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.sharedMaterial = workLineMaterial; line.useWorldSpace = true;
            line.widthMultiplier = width; line.positionCount = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }
        private void CreateFishingRod(Transform hand)
        {
            fishingRod = new GameObject("Fishing rod");
            fishingRod.transform.SetParent(hand, false);
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Wooden rod shaft"; shaft.transform.SetParent(fishingRod.transform, false);
            shaft.transform.localPosition = Vector3.up * 1.05f;
            shaft.transform.localScale = new Vector3(.035f,1.05f,.035f);
            Destroy(shaft.GetComponent<Collider>());
            rodMaterial ??= new Material(Shader.Find("Universal Render Pipeline/Lit"));
            rodMaterial.SetColor("_BaseColor",new Color(.34f,.17f,.055f));
            shaft.GetComponent<Renderer>().sharedMaterial = rodMaterial;
            fishingLine = CreateWorkLine("Fishing line", fishingRod.transform, .012f);
            fishingRod.SetActive(false);
        }

        private void PoseArm(bool left, Vector3 grip)
        {
            var upper = left ? leftUpperArmBone : rightUpperArmBone;
            var lower = left ? leftLowerArmBone : rightLowerArmBone;
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (upper == null || lower == null || hand == null) return;
            var shoulder = upper.position;
            var a = Vector3.Distance(shoulder, lower.position);
            var b = Vector3.Distance(lower.position, hand.position);
            var vector = grip - shoulder;
            if (vector.sqrMagnitude < .0001f) return;
            var distance = Mathf.Clamp(vector.magnitude, Mathf.Abs(a-b)+.01f, a+b-.01f);
            var axis = vector.normalized;
            var along = (a*a-b*b+distance*distance)/(2f*distance);
            var bend = Vector3.ProjectOnPlane(Vector3.down + transform.right * (left ? -.5f : .5f), axis).normalized;
            var elbow = shoulder + axis*along + bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var rotation = Quaternion.FromToRotation(lower.position-shoulder, elbow-shoulder)*upper.rotation;
            if (left) ApplyOffset(upper,Quaternion.Inverse(upper.rotation)*rotation,ref lastLeftUpperArmOffset);
            else ApplyOffset(upper,Quaternion.Inverse(upper.rotation)*rotation,ref lastRightUpperArmOffset);
            rotation = Quaternion.FromToRotation(hand.position-lower.position, grip-lower.position)*lower.rotation;
            if (left) ApplyOffset(lower,Quaternion.Inverse(lower.rotation)*rotation,ref lastLeftLowerArmOffset);
            else ApplyOffset(lower,Quaternion.Inverse(lower.rotation)*rotation,ref lastRightLowerArmOffset);
        }

        private void ApplyBowDrawPose()
        {
            if (huntingBow == null) return;
            var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var chest = transform.position + Vector3.up * 1.35f;
            var bowGrip = chest + transform.forward * .62f - transform.right * .12f;
            PoseArm(true, bowGrip);
            var nock = Vector3.Lerp(bowGrip-transform.forward*.08f,
                chest + transform.right*.18f - transform.forward*.13f, Mathf.SmoothStep(0,1,huntingDraw));
            if (huntingReleased) nock = chest + transform.right*.23f - transform.forward*.23f;
            PoseArm(false,nock);
            var desired = Quaternion.LookRotation(transform.forward,Vector3.up) * Quaternion.Inverse(huntingBow.transform.localRotation);
            ApplyOffset(left,Quaternion.Inverse(left.rotation)*desired,ref lastLeftHandOffset);
            if (bowString == null) bowString = CreateWorkLine("Drawn bow string",huntingBow.transform,.012f);
            bowString.SetPosition(0,huntingBow.transform.position+Vector3.up*.52f);
            bowString.SetPosition(1,huntingReleased ? huntingBow.transform.position : right.position);
            bowString.SetPosition(2,huntingBow.transform.position-Vector3.up*.52f);
        }

        private void ApplyFishingPose()
        {
            if (fishingRod == null) return;
            var grip = transform.position + Vector3.up*1.05f + transform.forward*.38f + transform.right*.1f;
            PoseArm(false,grip); PoseArm(true,grip-transform.forward*.12f-transform.right*.1f);
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var direction = (transform.forward + Vector3.up*.45f).normalized;
            var desired = Quaternion.LookRotation(transform.right,direction) * Quaternion.Inverse(fishingRod.transform.localRotation);
            ApplyOffset(hand,Quaternion.Inverse(hand.rotation)*desired,ref lastWorkHandOffset);
            var tip = fishingRod.transform.position+fishingRod.transform.up*2.1f;
            var end = fishingTarget == Vector3.zero ? tip+transform.forward*.8f-Vector3.up : fishingTarget;
            fishingLine.SetPosition(0,tip);
            fishingLine.SetPosition(1,Vector3.Lerp(tip,end,.5f)-Vector3.up*.12f);
            fishingLine.SetPosition(2,end+Vector3.up*.06f);
        }
    }
}
