using System;
using UnityEngine;

namespace ProjectG.CharacterActions
{
    /// <summary>
    /// 서버 물리 판정에 사용할 대표적인 히트 볼륨 모양입니다.
    /// </summary>
    public enum HitVolumeShape
    {
        Sphere = 0,
        Box = 1,
        Capsule = 2
    }

    /// <summary>
    /// 캡슐의 길이 방향입니다. Unity의 <see cref="CapsuleCollider.direction"/> 값과 같습니다.
    /// </summary>
    public enum HitVolumeAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    /// <summary>
    /// 캐릭터 원점을 기준으로 저장되는 하나의 히트 볼륨 데이터입니다.
    /// 런타임 오브젝트를 참조하지 않으므로 플레이어와 몬스터가 같은 데이터를 사용할 수 있습니다.
    /// </summary>
    [Serializable]
    public struct HitVolumeDefinition
    {
        [SerializeField] private HitVolumeShape _shape;
        [SerializeField] private Vector3 _localCenter;
        [SerializeField] private Vector3 _localEulerAngles;
        [SerializeField] private Vector3 _boxHalfExtents;
        [SerializeField, Min(0.01f)] private float _radius;
        [SerializeField, Min(0.02f)] private float _capsuleHeight;
        [SerializeField] private HitVolumeAxis _capsuleAxis;

        /// <summary>물리 판정 모양입니다.</summary>
        public HitVolumeShape Shape => _shape;

        /// <summary>캐릭터 원점 기준 중심 위치입니다.</summary>
        public Vector3 LocalCenter => _localCenter;

        /// <summary>캐릭터 원점 기준 회전값입니다.</summary>
        public Vector3 LocalEulerAngles => _localEulerAngles;

        /// <summary>박스 중심에서 각 면까지의 거리입니다.</summary>
        public Vector3 BoxHalfExtents => _boxHalfExtents;

        /// <summary>구 또는 캡슐의 반지름입니다.</summary>
        public float Radius => _radius;

        /// <summary>양쪽 둥근 끝을 포함한 캡슐 전체 높이입니다.</summary>
        public float CapsuleHeight => _capsuleHeight;

        /// <summary>캡슐의 길이 방향입니다.</summary>
        public HitVolumeAxis CapsuleAxis => _capsuleAxis;

        /// <summary>
        /// 구 모양 히트 볼륨 데이터를 만듭니다.
        /// </summary>
        public static HitVolumeDefinition CreateSphere(
            Vector3 localCenter,
            float radius,
            Vector3 localEulerAngles = default)
        {
            return new HitVolumeDefinition
            {
                _shape = HitVolumeShape.Sphere,
                _localCenter = localCenter,
                _localEulerAngles = localEulerAngles,
                _boxHalfExtents = Vector3.one * 0.5f,
                _radius = Mathf.Max(0.01f, radius),
                _capsuleHeight = 2f,
                _capsuleAxis = HitVolumeAxis.Y
            };
        }

        /// <summary>
        /// 박스 모양 히트 볼륨 데이터를 만듭니다.
        /// </summary>
        public static HitVolumeDefinition CreateBox(
            Vector3 localCenter,
            Vector3 localEulerAngles,
            Vector3 halfExtents)
        {
            return new HitVolumeDefinition
            {
                _shape = HitVolumeShape.Box,
                _localCenter = localCenter,
                _localEulerAngles = localEulerAngles,
                _boxHalfExtents = MaxComponents(Abs(halfExtents), 0.01f),
                _radius = 0.5f,
                _capsuleHeight = 2f,
                _capsuleAxis = HitVolumeAxis.Y
            };
        }

        /// <summary>
        /// 캡슐 모양 히트 볼륨 데이터를 만듭니다.
        /// </summary>
        public static HitVolumeDefinition CreateCapsule(
            Vector3 localCenter,
            Vector3 localEulerAngles,
            float radius,
            float height,
            HitVolumeAxis axis)
        {
            float safeRadius = Mathf.Max(0.01f, radius);
            return new HitVolumeDefinition
            {
                _shape = HitVolumeShape.Capsule,
                _localCenter = localCenter,
                _localEulerAngles = localEulerAngles,
                _boxHalfExtents = Vector3.one * 0.5f,
                _radius = safeRadius,
                _capsuleHeight = Mathf.Max(safeRadius * 2f, height),
                _capsuleAxis = axis
            };
        }

        /// <summary>
        /// 캐릭터 원점을 기준으로 월드 중심과 회전을 계산합니다.
        /// </summary>
        public void GetWorldPose(Transform origin, out Vector3 center, out Quaternion rotation)
        {
            center = origin.TransformPoint(_localCenter);
            rotation = origin.rotation * Quaternion.Euler(_localEulerAngles);
        }

        /// <summary>
        /// 캐릭터 스케일까지 반영한 월드 박스 반지름 크기를 반환합니다.
        /// </summary>
        public Vector3 GetWorldBoxHalfExtents(Transform origin)
        {
            Vector3 scale = Abs(origin.lossyScale);
            return Vector3.Scale(_boxHalfExtents, scale);
        }

        /// <summary>
        /// 캐릭터 스케일까지 반영한 월드 구 반지름을 반환합니다.
        /// </summary>
        public float GetWorldSphereRadius(Transform origin)
        {
            Vector3 scale = Abs(origin.lossyScale);
            return _radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
        }

        /// <summary>
        /// 서버의 OverlapCapsule 호출에 필요한 두 끝점과 반지름을 계산합니다.
        /// </summary>
        public void GetWorldCapsule(
            Transform origin,
            out Vector3 pointA,
            out Vector3 pointB,
            out float radius)
        {
            GetWorldPose(origin, out Vector3 center, out _);

            Quaternion localRotation = Quaternion.Euler(_localEulerAngles);
            Vector3 localAxis = localRotation * AxisVector(_capsuleAxis);
            Vector3 localTangent = localRotation * TangentVector(_capsuleAxis);
            Vector3 localBitangent = Vector3.Cross(localAxis, localTangent).normalized;

            Vector3 worldAxisVector = origin.TransformVector(localAxis);
            float axisScale = Mathf.Max(0.0001f, worldAxisVector.magnitude);
            Vector3 worldAxis = worldAxisVector / axisScale;
            float tangentScale = origin.TransformVector(localTangent).magnitude;
            float bitangentScale = origin.TransformVector(localBitangent).magnitude;

            radius = _radius * Mathf.Max(tangentScale, bitangentScale);
            float worldHeight = Mathf.Max(radius * 2f, _capsuleHeight * axisScale);
            float segmentHalfLength = Mathf.Max(0f, worldHeight * 0.5f - radius);
            Vector3 offset = worldAxis * segmentHalfLength;
            pointA = center - offset;
            pointB = center + offset;
        }

        /// <summary>
        /// 잘못 입력된 음수나 너무 작은 크기를 안전한 값으로 보정합니다.
        /// </summary>
        public void Sanitize()
        {
            _boxHalfExtents = MaxComponents(Abs(_boxHalfExtents), 0.01f);
            _radius = Mathf.Max(0.01f, _radius);
            _capsuleHeight = Mathf.Max(_radius * 2f, _capsuleHeight);
        }

        /// <summary>
        /// 선택한 캐릭터의 Scene 뷰에 이 볼륨을 그립니다.
        /// </summary>
        public void DrawGizmos(Transform origin)
        {
            if (origin == null)
                return;

            GetWorldPose(origin, out Vector3 center, out Quaternion rotation);
            Matrix4x4 previousMatrix = Gizmos.matrix;

            switch (_shape)
            {
                case HitVolumeShape.Box:
                    Gizmos.matrix = Matrix4x4.TRS(
                        center,
                        rotation,
                        Vector3.Scale(Vector3.one, Abs(origin.lossyScale)));
                    Gizmos.DrawWireCube(Vector3.zero, _boxHalfExtents * 2f);
                    break;

                case HitVolumeShape.Capsule:
                    GetWorldCapsule(origin, out Vector3 pointA, out Vector3 pointB, out float capsuleRadius);
                    Gizmos.matrix = Matrix4x4.identity;
                    Gizmos.DrawWireSphere(pointA, capsuleRadius);
                    Gizmos.DrawWireSphere(pointB, capsuleRadius);
                    Gizmos.DrawLine(pointA, pointB);
                    break;

                default:
                    Gizmos.matrix = Matrix4x4.identity;
                    Gizmos.DrawWireSphere(center, GetWorldSphereRadius(origin));
                    break;
            }

            Gizmos.matrix = previousMatrix;
        }

        private static Vector3 AxisVector(HitVolumeAxis axis)
        {
            return axis switch
            {
                HitVolumeAxis.X => Vector3.right,
                HitVolumeAxis.Z => Vector3.forward,
                _ => Vector3.up
            };
        }

        private static Vector3 TangentVector(HitVolumeAxis axis)
        {
            return axis == HitVolumeAxis.X ? Vector3.up : Vector3.right;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static Vector3 MaxComponents(Vector3 value, float minimum)
        {
            return new Vector3(
                Mathf.Max(minimum, value.x),
                Mathf.Max(minimum, value.y),
                Mathf.Max(minimum, value.z));
        }
    }
}
