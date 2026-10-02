using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace _Game_Assets.Scripts.Runtime.Unity_Timeline
{
    /// <summary>
    /// 바인딩된 Transform 을 흔드는 Playable. 주 용도는 카메라 셰이크다.
    ///
    /// 설계 원칙
    /// - <b>결정적(deterministic)</b>: 흔들림 값은 오직 클립 로컬 시간의 함수다.
    ///   <see cref="Random"/> 이나 프레임 누적을 쓰지 않으므로 스크럽·일시정지·역재생에서
    ///   같은 시간이면 항상 같은 자세가 나온다. Timeline 이 시간을 소유한다는 이 프로젝트의 원칙과 같다.
    /// - <b>시작값 기준 절대 대입</b>: 매 프레임 <c>시작값 + 오프셋</c> 을 대입한다.
    ///   더하기 누적이 아니라서 드리프트가 없고, 클립이 끝나면 시작값으로 정확히 복원된다.
    /// - 좌표계는 <b>로컬</b>이다. 카메라 리그(부모가 이동/추적, 자식이 셰이크)에 그대로 맞는다.
    ///
    /// <see cref="DOTweenTrack"/> 에 <see cref="DOTweenClip"/> 과 나란히 올린다.
    /// 한 트랙이 곧 하나의 쓰기 경로라서, 두 트랙이 같은 Transform 을 두고 싸우는 일이 없다.
    /// 단 <b>같은 트랙 안에서도 클립이 겹치면</b> 둘 다 시작값 기준 절대 대입이라 나중에 평가된 쪽만 남는다.
    /// 이동과 셰이크를 정말 동시에 걸어야 하면 리그를 한 단 파서
    /// 부모(이동) / 자식(셰이크) 로 나누고 트랙을 둘로 두는 편이 확실하다.
    /// </summary>
    public class TransformShakeBehaviour : PlayableBehaviour
    {
        /// <summary>축마다 노이즈 슬라이스를 어긋내는 상수. 정수를 피해야 Perlin 격자점(=0.5)에 걸리지 않는다.</summary>
        private const float AXIS_SEED_STRIDE = 17.31f;
        /// <summary>시드 하나가 차지하는 노이즈 좌표 간격. 시드가 다르면 파형이 겹치지 않는다.</summary>
        private const float SEED_STRIDE = 71.13f;
        /// <summary>위치/회전이 서로 다른 파형을 쓰도록 분리하는 오프셋.</summary>
        private const float POSITION_SEED_OFFSET = 3.77f;
        private const float ROTATION_SEED_OFFSET = 129.41f;

        // ---- 클립이 주입하는 데이터. 직렬화 주체는 어디까지나 클립이고 여기는 셔틀이다. ----
        public bool shakePosition;
        public Vector3 positionAmplitude;
        public bool shakeRotation;
        public Vector3 rotationAmplitude;
        public float frequency;
        public AnimationCurve amplitudeCurve;
        public int seed;

        /// <summary>흔들 대상. 트랙 바인딩이 playerData 로 내려온다.</summary>
        private Transform transform;
        /// <summary>이번 재생/스크럽 구간의 첫 프레임을 처리했는지.</summary>
        private bool firstFrameProcessed;
        /// <summary>클립 진입 시점의 로컬 위치. 매 프레임 여기에 오프셋을 더한다.</summary>
        private Vector3 startPosition;
        /// <summary>클립 진입 시점의 로컬 회전(오일러).</summary>
        private Vector3 startRotation;
        /// <summary>이 Behaviour 를 대표하는 클립. duration 을 신뢰 가능한 값으로 얻으려고 트랙이 넣어준다.</summary>
        private TimelineClip clip;
        /// <summary>트랙 재생 전 바인딩의 로컬 위치. 에디트 모드 복원용.</summary>
        private Vector3 trackBindingStartPosition;
        /// <summary>트랙 재생 전 바인딩의 로컬 회전. 에디트 모드 복원용.</summary>
        private Vector3 trackBindingStartRotation;

        /// <summary>
        /// RectTransform 이면 anchoredPosition3D, 아니면 localPosition 을 쓴다.
        /// UI 패널 셰이크에서도 앵커 레이아웃과 싸우지 않게 하기 위한 분기이며,
        /// 트랙의 시작값 캡처와 규약을 공유하려고 static 으로 둔다.
        /// </summary>
        public static Vector3 GetLocalPosition(Transform target)
        {
            return target is RectTransform rect ? rect.anchoredPosition3D : target.localPosition;
        }

        public static void SetLocalPosition(Transform target, Vector3 position)
        {
            if (target is RectTransform rect)
                rect.anchoredPosition3D = position;
            else
                target.localPosition = position;
        }

        /// <summary>
        /// 트랙이 필요한 값을 넣어준다.
        /// </summary>
        /// <param name="clip">이 Behaviour 를 대표하는 클립</param>
        /// <param name="trackBindingStartPosition">트랙 재생 전 바인딩의 로컬 위치</param>
        /// <param name="trackBindingStartRotation">트랙 재생 전 바인딩의 로컬 회전(오일러)</param>
        public void Initialize(TimelineClip clip, Vector3 trackBindingStartPosition, Vector3 trackBindingStartRotation)
        {
            this.clip = clip;
            this.trackBindingStartPosition = trackBindingStartPosition;
            this.trackBindingStartRotation = trackBindingStartRotation;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            transform = playerData as Transform;
            if (transform == null)
            {
                return;
            }

            // 첫 프레임이면 이번 구간의 기준 자세를 잡아둔다. 이후 프레임은 전부 이 값 기준 절대 대입이다.
            if (!firstFrameProcessed)
            {
                startPosition = GetLocalPosition(transform);
                startRotation = transform.localEulerAngles;
                firstFrameProcessed = true;
            }

            // 클립 로컬 시간. Timeline 이 매 평가마다 RuntimeClip.EvaluateAt 에서
            // SetTime(clip.ToLocalTime(디렉터시간)) 으로 '써 넣는' 값이지 Time.deltaTime 을 누적한 값이 아니다.
            // 그래서 시크·역재생·감속·에디트 모드 Evaluate 가 전부 같은 결과를 준다.
            var localTime = (float)playable.GetTime();

            // 로컬 시간은 clipIn 에서 시작해 duration * timeScale 만큼 흐른다(ToLocalTime 의 정의).
            // clip.duration 은 타임라인 좌표계 길이라, 클립 앞을 트리밍(clipIn)하거나 속도 배율(timeScale)을
            // 주면 그대로 나눌 수 없다.
            // 지금은 clipCaps 가 None 이라 TimelineClip 의 getter 자체가 clipIn=0 / timeScale=1 로 고정해 주므로
            // 아래 식은 time / duration 과 같은 값이 된다. 나중에 ClipIn 이나 SpeedMultiplier 를 열어도
            // 봉투가 어긋나지 않게 일반형으로 적어 둔다.
            var clipIn = clip != null ? (float)clip.clipIn : 0f;
            var localSpan = clip != null
                ? (float)(clip.duration * clip.timeScale)
                : (float)playable.GetDuration();
            var normalizedTime = localSpan > 0f ? Mathf.Clamp01((localTime - clipIn) / localSpan) : 0f;

            // 진폭 봉투. 곡선이 비어 있으면 감쇠 없이 일정 세기로 흔든다.
            var envelope = amplitudeCurve != null && amplitudeCurve.length > 0
                ? amplitudeCurve.Evaluate(normalizedTime)
                : 1f;

            // 노이즈 좌표는 로컬 시간 그대로 쓴다. clipIn 으로 앞을 자르면 파형도 그만큼 잘려 들어가고,
            // timeScale 을 주면 그만큼 빨리 떨리는 게 트리밍/배속의 자연스러운 의미다.
            var noiseTime = localTime * frequency;

            if (shakePosition)
            {
                var offset = Vector3.Scale(SampleNoiseVector(noiseTime, POSITION_SEED_OFFSET), positionAmplitude) * envelope;
                SetLocalPosition(transform, startPosition + offset);
            }

            if (shakeRotation)
            {
                var offset = Vector3.Scale(SampleNoiseVector(noiseTime, ROTATION_SEED_OFFSET), rotationAmplitude) * envelope;
                transform.localEulerAngles = startRotation + offset;
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // 재생/스크럽 구간이 끝났다. 셰이크는 일시적 연출이므로 항상 시작 자세로 되돌린다.
            firstFrameProcessed = false;

            if (transform == null)
            {
                return;
            }

            if (shakePosition)
            {
                SetLocalPosition(transform, startPosition);
            }

            if (shakeRotation)
            {
                transform.localEulerAngles = startRotation;
            }

            base.OnBehaviourPause(playable, info);
        }

        public override void OnGraphStop(Playable playable)
        {
            // 에디트 모드에서 타임라인을 더 이상 보지 않을 때만 트랙 시작 자세로 되돌린다.
            // 런타임 값까지 되감으면 게임 로직이 만든 자세를 망친다.
            if (Application.isPlaying)
            {
                return;
            }

            if (transform == null)
            {
                return;
            }

            SetLocalPosition(transform, trackBindingStartPosition);
            transform.localEulerAngles = trackBindingStartRotation;
            base.OnGraphStop(playable);
        }

        /// <summary>
        /// 축마다 다른 노이즈 슬라이스를 뽑아 [-1,1] 벡터로 만든다.
        /// 축을 서로 어긋낸 좌표에서 샘플링해야 세 축이 같은 위상으로 움직이는(= 대각선으로만 흔들리는) 현상을 피한다.
        /// </summary>
        private Vector3 SampleNoiseVector(float noiseTime, float channelSeedOffset)
        {
            var seedBase = seed * SEED_STRIDE + channelSeedOffset;
            return new Vector3(
                SampleNoise(noiseTime, seedBase),
                SampleNoise(noiseTime, seedBase + AXIS_SEED_STRIDE),
                SampleNoise(noiseTime, seedBase + AXIS_SEED_STRIDE * 2f));
        }

        /// <summary>
        /// Perlin 은 [0,1] 을 돌려주므로 [-1,1] 로 재사상해 좌우 대칭 진동으로 만든다.
        /// 시간만 넣으면 값이 정해지는 순수 함수라 스크럽·역재생에서도 결과가 같다.
        /// </summary>
        private static float SampleNoise(float noiseTime, float axisSeed)
        {
            return Mathf.PerlinNoise(noiseTime, axisSeed) * 2f - 1f;
        }
    }
}
