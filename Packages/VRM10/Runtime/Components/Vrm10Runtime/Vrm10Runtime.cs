using System;
using System.Collections.Generic;
using System.Linq;
using UniGLTF;
using UnityEngine;

namespace UniVRM10
{
    /// <summary>
    /// VRM全体を制御するコンポーネント。
    /// 
    /// 各フレームの更新で、以下の処理を行います。
    /// 
    /// 1. ControlRigの更新
    /// 2. Constraintの更新
    /// 3. LookAtの更新
    /// 4. Expressionの更新
    /// 5. SpringBoneの更新
    /// 
    /// </summary>
    public class Vrm10Runtime : IDisposable
    {
        private readonly Vrm10Instance m_instance;
        private readonly Transform m_head;

        public Vrm10RuntimeControlRig ControlRig { get; }
        public IVrm10Constraint[] Constraints { get; }
        public Vrm10RuntimeLookAt LookAt { get; }
        public Vrm10RuntimeExpression Expression { get; }
        public IVrm10SpringBoneRuntime SpringBone { get; }

        IReadOnlyDictionary<Transform, TransformState> _initPose;

        public Vrm10Runtime(Vrm10Instance instance, bool useControlRig, IVrm10SpringBoneRuntime springBoneRuntime,
            IReadOnlyDictionary<Transform, TransformState> initPose, bool isPrefabInstance)
        {
            if (!Application.isPlaying)
            {
                UniGLTFLogger.Warning($"{nameof(Vrm10Runtime)} expects runtime behaviour.");
            }

            _initPose = initPose;
            m_instance = instance;
            if (m_instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (!instance.TryGetBoneTransform(HumanBodyBones.Head, out m_head))
            {
                throw new Exception();
            }

            // Ensure T-Pose before applying ControlRig
            if (instance.TryGetComponent<Animator>(out var animator) && animator.avatar != null)
            {
                var avatar = animator.avatar;
                var root = instance.transform;
                
                // Store current pose
                var handler = new HumanPoseHandler(avatar, root);
                var currentPose = new HumanPose();
                handler.GetHumanPose(ref currentPose);
                
                // Apply T-Pose
                HumanPoseTransfer.SetTPose(avatar, root);
                
                // Reapply current pose after T-Pose to maintain intended posture
                handler.SetHumanPose(ref currentPose);
            }

            if (useControlRig)
            {
                ControlRig = new Vrm10RuntimeControlRig(instance.Humanoid, m_instance.transform);
            }
            Constraints = instance.GetComponentsInChildren<IVrm10Constraint>();
            LookAt = new Vrm10RuntimeLookAt(instance, instance.Humanoid, ControlRig);
            Expression = new Vrm10RuntimeExpression(instance, LookAt.EyeDirectionApplicable, isPrefabInstance, initPose);
            SpringBone = springBoneRuntime;
        }

        public void Dispose()
        {
            ControlRig?.Dispose();
        }

        public void Process()
        {
            // 1. ControlRig
            ControlRig?.Process();

            // 2. Constraints
            foreach (var constraint in Constraints)
            {
                constraint.Process();
            }

            // 3. LookAt
            LookAt.Process();

            // 4. Expression
            Expression.Process();

            // 5. SpringBone
            SpringBone.Process();
        }
    }
}