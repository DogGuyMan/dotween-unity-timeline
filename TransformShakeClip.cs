using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace _Game_Assets.Scripts.Runtime.Unity_Timeline
{
    /// <summary>
    /// 흔들림 데이터를 소유하는 클립. 이 프로젝트의 원칙대로 <b>데이터의 주인은 클립</b>이고,
    /// <see cref="TransformShakeBehaviour"/> 는 재생 시점에 값을 주입받는 셔틀일 뿐이다.
    ///
    /// 카메라 셰이크 감 잡기
    /// - 타격감 있는 충격: duration 0.15~0.3, frequency 25~35, 곡선은 1 → 0 감쇠(기본값)
    /// - 지속되는 지진/긴장: duration 길게, frequency 8~15, 곡선은 평평하게(상수 1)
    /// - 3D 카메라는 <see cref="_positionAmplitude"/> 보다 <see cref="_rotationAmplitude"/> 의 Z(롤)를
    ///   키우는 쪽이 화면이 덜 깨지고 더 세게 보인다. 위치만 흔들면 근경이 뚫려 보이기 쉽다.
    /// </summary>
    [Serializable]
    public class TransformShakeClip : PlayableAsset, ITimelineClipAsset
    {
        [Header("위치 흔들기")]
        [Tooltip("위치를 흔들지 여부. 끄면 아래 진폭은 무시된다.")]
        [SerializeField] private bool _shakePosition = true;

        [Tooltip("축별 최대 이동량(로컬 단위). RectTransform 이면 anchoredPosition3D 기준이라 픽셀 단위로 읽으면 된다.")]
        [SerializeField] private Vector3 _positionAmplitude = new Vector3(0.1f, 0.1f, 0f);

        [Header("회전 흔들기")]
        [Tooltip("회전을 흔들지 여부. 끄면 아래 진폭은 무시된다.")]
        [SerializeField] private bool _shakeRotation = true;

        [Tooltip("축별 최대 회전량(도). 카메라는 Z(롤)를 키우는 게 가장 잘 먹힌다.")]
        [SerializeField] private Vector3 _rotationAmplitude = new Vector3(0.4f, 0.4f, 1.2f);

        [Header("공통")]
        [Tooltip("초당 진동 횟수에 가까운 값. 클수록 잘게 떤다.")]
        [SerializeField, Min(0.01f)] private float _frequency = 25f;

        [Tooltip("클립 정규화 시간(0~1) 에 대한 세기 봉투. 기본값은 1에서 0으로 감쇠라 충격 연출에 맞는다. 평평하게 두면 일정 세기로 계속 흔들린다.")]
        [SerializeField] private AnimationCurve _amplitudeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Tooltip("파형 시드. 여러 셰이크 클립이 붙어 있을 때 값을 다르게 주면 같은 모양으로 반복되지 않는다.")]
        [SerializeField] private int _seed = 0;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<TransformShakeBehaviour>.Create(graph);
            var shake = playable.GetBehaviour();
            shake.shakePosition = _shakePosition;
            shake.positionAmplitude = _positionAmplitude;
            shake.shakeRotation = _shakeRotation;
            shake.rotationAmplitude = _rotationAmplitude;
            shake.frequency = _frequency;
            shake.amplitudeCurve = _amplitudeCurve;
            shake.seed = _seed;
            return playable;
        }

        // 셰이크는 시작 자세 기준 절대 대입이라 클립끼리 섞일 수 없다.
        // Blending 을 열면 나중에 평가된 클립이 앞 클립을 덮어써 블렌딩처럼 보이지 않으므로 닫아둔다.
        public ClipCaps clipCaps => ClipCaps.None;
    }
}
