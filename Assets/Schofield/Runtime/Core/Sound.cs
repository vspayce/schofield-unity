// Sound: a pool of AudioSources for one-shots (2D or positioned in the world), named loops with fades,
// music and sfx buses, and the Dead Eye slowdown (everything pitched down and muffled).
// Ported from core/audio.js.
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Schofield
{
    public class Sound : MonoBehaviour
    {
        public static Sound I;
        public float musicVolume = 0.55f, sfxVolume = 1f, master = 0.9f;
        float rate = 1;
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<AudioSource, float> basePitch = new Dictionary<AudioSource, float>();
        class LoopState { public AudioSource src; public float vol, target, fadeRate, rate = 1; public bool music, stopping; }
        readonly Dictionary<string, LoopState> loops = new Dictionary<string, LoopState>();
        AudioLowPassFilter muffle;
        public System.Action<string> onPlay;      // townsfolk listen for gunfire

#if (UNITY_IOS || UNITY_TVOS) && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SC_PlaybackAudio();
#endif

        public static Sound Create(Transform listener)
        {
            var go = new GameObject("Sound");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Sound>();
            I.muffle = listener.gameObject.AddComponent<AudioLowPassFilter>();
            I.muffle.cutoffFrequency = 22000;
            I.musicVolume = PlayerPrefs.GetFloat("schofield.music", 0.55f);
            I.sfxVolume = PlayerPrefs.GetFloat("schofield.sfx", 1f);
#if (UNITY_IOS || UNITY_TVOS) && !UNITY_EDITOR
            try { SC_PlaybackAudio(); } catch { }
#endif
            for (int i = 0; i < 28; i++) I.NewSource();
            return I;
        }

        AudioSource NewSource()
        {
            var go = new GameObject("src"); go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 3; s.maxDistance = 300; s.dopplerLevel = 0;
            pool.Add(s);
            return s;
        }

        AudioSource Free()
        {
            foreach (var s in pool) if (!s.isPlaying) return s;
            return NewSource();
        }

        /// one-shot. position = null plays it flat (in your head)
        public void Play(string name, float volume = 1, float pitch = 1, Vector3? position = null, float delay = 0, bool music = false, float maxDist = 300)
        {
            var clip = Assets.Clip(name);
            onPlay?.Invoke(name);
            if (!clip) return;
            var s = Free();
            s.clip = clip; s.loop = false;
            s.volume = Mathf.Clamp01(volume * (music ? musicVolume : sfxVolume) * master);
            basePitch[s] = pitch;
            s.pitch = pitch * (music ? 1 : rate);
            if (position.HasValue) { s.spatialBlend = 1; s.transform.position = position.Value; s.maxDistance = maxDist; }
            else { s.spatialBlend = 0; s.transform.localPosition = Vector3.zero; }
            if (delay > 0) s.PlayDelayed(delay); else s.Play();
        }

        public void Loop(string name, float volume = 1, bool music = false)
        {
            if (loops.TryGetValue(name, out var l))
            {
                l.target = volume; l.stopping = false; l.fadeRate = 4;
                if (!l.src.isPlaying) l.src.Play();
                return;
            }
            var clip = Assets.Clip(name);
            if (!clip) return;
            var go = new GameObject("loop " + name); go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = true; s.spatialBlend = 0; s.playOnAwake = false;
            l = new LoopState { src = s, vol = 0, target = volume, fadeRate = 6, music = music };
            loops[name] = l;
            s.volume = 0; s.Play();
        }

        public void StopLoop(string name, float fade = 0.3f)
        {
            if (!loops.TryGetValue(name, out var l)) return;
            l.target = 0; l.stopping = true; l.fadeRate = fade > 0 ? 1 / fade : 100;
        }
        public void LoopVolume(string name, float v, float fade = 0.2f)
        {
            if (!loops.TryGetValue(name, out var l)) return;
            l.target = v; l.fadeRate = fade > 0 ? 1 / fade : 100;
        }
        public void LoopRate(string name, float r) { if (loops.TryGetValue(name, out var l)) l.rate = r; }
        public bool HasLoop(string name) => loops.ContainsKey(name) && !loops[name].stopping;

        public void StopAll(float fade = 0.3f)
        {
            foreach (var k in new List<string>(loops.Keys)) StopLoop(k, fade);
            foreach (var s in pool) s.Stop();
        }

        /// Dead Eye: everything slower and muffled
        public void SetRate(float r)
        {
            rate = r;
            muffle.cutoffFrequency = r < 1 ? 1400 : 22000;
            foreach (var s in pool) if (s.isPlaying && basePitch.TryGetValue(s, out var bp)) s.pitch = bp * r;
        }

        public void Pause(bool on) => AudioListener.pause = on;

        public void SetVolumes(float music, float sfx)
        {
            musicVolume = music; sfxVolume = sfx;
            PlayerPrefs.SetFloat("schofield.music", music); PlayerPrefs.SetFloat("schofield.sfx", sfx);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var dead = new List<string>();
            foreach (var kv in loops)
            {
                var l = kv.Value;
                l.vol = Mathf.MoveTowards(l.vol, l.target, dt * l.fadeRate * Mathf.Max(0.05f, Mathf.Abs(l.target - l.vol) + 0.2f));
                l.src.volume = Mathf.Clamp01(l.vol * (l.music ? musicVolume : sfxVolume) * master);
                l.src.pitch = l.rate * (l.music ? 1 : rate);
                if (l.stopping && l.vol <= 0.001f) { Destroy(l.src.gameObject); dead.Add(kv.Key); }
            }
            foreach (var k in dead) loops.Remove(k);
        }
    }
}
