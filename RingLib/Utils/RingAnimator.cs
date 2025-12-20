using RingLib.StateMachine;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RingLib.Utils
{
    public class RingAnimator : MonoBehaviour
    {
        private Animator animator;
        private readonly Dictionary<string, float> clipLength = new();

        public string CurrentAnimation { get; private set; }
        public bool Finished { get; private set; }

        private AudioSource audioSource;

        protected virtual void AnimatorStart() { }

        private void Start()
        {
            animator = GetComponent<Animator>();
            var clips = animator.runtimeAnimatorController.animationClips;
            foreach (var clip in clips)
            {
                clipLength[clip.name] = clip.isLooping ? float.MaxValue : clip.length;
            }
            audioSource = GetComponent<AudioSource>();
            AnimatorStart();
        }

        private float NormalizedTime()
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(CurrentAnimation))
            {
                return Mathf.Min(1, stateInfo.normalizedTime);
            }
            return 0;
        }

        private void Update()
        {
            if (CurrentAnimation == null || clipLength[CurrentAnimation] == float.MaxValue)
            {
                return;
            }
            if (NormalizedTime() == 1)
            {
                Finished = true;
            }
        }

        public IEnumerator<Transition> PlayAnimation(string clipName, Func<float, Transition> updater = null)
        {
            if (!clipLength.ContainsKey(clipName))
            {
                Log.LogError(GetType().Name, $"Animation {clipName} not found");
                return null;
            }
            Finished = false;
            CurrentAnimation = clipName;
            animator.Play(clipName, -1, 0);

            IEnumerator<Transition> routine()
            {
                while (!Finished)
                {
                    if (updater != null)
                    {
                        var tansition = updater(NormalizedTime());
                        if (tansition == null)
                        {
                            break;
                        }
                        yield return tansition;
                    }
                    yield return new NoTransition();
                }
            }
            return routine();
        }

        public float ClipLength(string clipName)
        {
            return clipLength[clipName];
        }

        protected void PlaySound(AudioClip clip)
        {
            if (clip == null)
            {
                Log.LogError(GetType().Name, "Clip not found");
                return;
            }
            audioSource.PlayOneShot(clip);
        }

        protected void PlaySoundLoop(AudioClip clip)
        {
            if (clip == null)
            {
                Log.LogError(GetType().Name, "Clip not found");
                return;
            }
            audioSource.clip = clip;
            audioSource.loop = true;
            audioSource.Play();
        }

        protected void StopSoundLoop()
        {
            audioSource.loop = false;
            audioSource.Stop();
        }
    }
}
