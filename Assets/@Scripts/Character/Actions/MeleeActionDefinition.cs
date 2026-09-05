using System;
using UnityEngine;

namespace ProjectG.CharacterActions
{
    /// <summary>
    /// 이전 Melee Action 에셋을 깨뜨리지 않기 위한 호환 형식입니다.
    /// 새 행동은 <see cref="HitboxActionDefinition"/>으로 제작하세요.
    /// </summary>
    [Obsolete("새 행동은 HitboxActionDefinition을 사용하세요.")]
    public sealed class MeleeActionDefinition : HitboxActionDefinition
    {
        /// <summary>
        /// 이전 에셋에 저장된 모양 값을 읽기 위한 호환 열거형입니다.
        /// </summary>
        public enum HitVolumeShape
        {
            Sphere = 0,
            Box = 1
        }

        [Header("Legacy Single Hit Volume")]
        [SerializeField] private HitVolumeShape _shape = HitVolumeShape.Sphere;
        [SerializeField] private Vector3 _localCenter = new Vector3(0f, 1f, 1f);
        [SerializeField] private Vector3 _localEulerAngles;
        [SerializeField] private Vector3 _boxHalfExtents = new Vector3(0.75f, 1f, 0.75f);
        [SerializeField, Min(0.01f)] private float _radius = 1f;

        public HitVolumeShape Shape => _shape;
        public Vector3 LocalCenter => _localCenter;
        public Vector3 LocalEulerAngles => _localEulerAngles;
        public Vector3 BoxHalfExtents => _boxHalfExtents;
        public float Radius => _radius;

        /// <inheritdoc />
        public override int HitVolumeCount => base.HitVolumeCount > 0 ? base.HitVolumeCount : 1;

        /// <inheritdoc />
        public override HitVolumeDefinition GetHitVolume(int index)
        {
            if (base.HitVolumeCount > 0)
                return base.GetHitVolume(index);

            if (index != 0)
                throw new IndexOutOfRangeException();

            if (_shape == HitVolumeShape.Box)
            {
                return HitVolumeDefinition.CreateBox(
                    _localCenter,
                    _localEulerAngles,
                    _boxHalfExtents);
            }

            return HitVolumeDefinition.CreateSphere(
                _localCenter,
                _radius,
                _localEulerAngles);
        }
    }
}
