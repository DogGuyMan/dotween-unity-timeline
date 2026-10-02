// MIT License

// Copyright (c) 2024 Breakstep Studios

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace _Game_Assets.Scripts.Runtime.Unity_Timeline
{
    /// <summary>
    /// A transform track. Hosts several clip types that all drive the bound <see cref="Transform"/>:
    /// <see cref="DOTweenClip"/> for tweening to a target, <see cref="TransformShakeClip"/> for shaking.
    /// <see cref="TrackClipTypeAttribute"/> allows multiple, so more clip types can be registered here.
    ///
    /// Clips on this track all write absolute values derived from the pose captured when they start,
    /// so overlapping clips do not blend - the one evaluated last wins. Lay them out sequentially.
    /// </summary>
    [TrackColor(148/255f,222/255f,89/255f)]
    [TrackBindingType(typeof(Transform))]
    [TrackClipType(typeof(DOTweenClip))]
    [TrackClipType(typeof(TransformShakeClip))]
    public class DOTweenTrack : TrackAsset
    {
        protected override Playable CreatePlayable(PlayableGraph graph, GameObject gameObject, TimelineClip clip)
        {
            //playable.GetDuration() returns some super strange results when ClipCaps.Extrapolation is set
            //in order to make extrapolation work intuitively with DOTWeen we need clip.duration, so we override
            //CreatePlayable and pass the current clip to our behavior.
            //Thank my dude here https://forum.unity.com/threads/trying-to-get-percentage-of-the-way-through-playable.503672/#post-3281262
            //additional info here https://forum.unity.com/threads/timeline-adds-1-million-to-playable-getduration-when-extrapolation-is-set-to-anything-but-none.1324440/
            var playable = base.CreatePlayable(graph, gameObject, clip);

            // grab the track binding so that we can initialize the clip's behavior with its values
            var director = gameObject.GetComponent<PlayableDirector>();
            var trackBinding = director == null ? null : director.GetGenericBinding(this) as Transform;
            if (trackBinding == null)
            {
                return playable;
            }

            //this track accepts more than one clip type, so we must NOT blind-cast to a single behavior.
            //converting a Playable to ScriptPlayable<T> validates the handle's actual playable type, so a
            //hard cast throws as soon as a clip of another type is placed on this track. Branch on the clip
            //asset instead - each clip type creates exactly one behavior type in its CreatePlayable.
            if (clip.asset is DOTweenClip)
            {
                // If the target is a RectTransform, capture position via anchoredPosition3D (same convention as the behavior).
                ((ScriptPlayable<DOTweenBehavior>)playable).GetBehaviour().Initialize(
                    clip, DOTweenBehavior.GetPosition(trackBinding), trackBinding.eulerAngles, trackBinding.localScale);
            }
            else if (clip.asset is TransformShakeClip)
            {
                // Shake works in local space, so it captures a local pose rather than a world one.
                ((ScriptPlayable<TransformShakeBehaviour>)playable).GetBehaviour().Initialize(
                    clip, TransformShakeBehaviour.GetLocalPosition(trackBinding), trackBinding.localEulerAngles);
            }

            return playable;
        }
    }
}