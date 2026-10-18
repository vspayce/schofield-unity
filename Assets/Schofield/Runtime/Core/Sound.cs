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
        float rate = 1; int logged;
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<AudioSource, float> basePitch = new Dictionary<AudioSource, float>();
        class LoopState { public AudioSource src; public float vol, target, fadeRate, rate = 1; public bool music, stopping; }
        readonly Dictionary<string, LoopState> loops = new Dictionary<string, LoopState>();
        AudioLowPassFilter muffle;
        public System.Action<string> onPlay;      // townsfolk listen for gunfire

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SC_PlaybackAudio();
#endif
#if (UNITY_IOS || UNITY_TVOS) && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string SC_AudioReport();
#endif
#if UNITY_TVOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SC_TVAudio();
#endif

        public static Sound Create(Transform listener)
        {
            var go = new GameObject("Sound");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Sound>();
            I.muffle = listener.gameObject.AddComponent<AudioLowPassFilter>();
            I.muffle.cutoffFrequency = 1400;
            I.muffle.enabled = false;            // only in slow-mo: a 22 kHz cutoff is above Nyquist at the 24 kHz the Apple TV runs at, and silences everything
#if UNITY_TVOS && !UNITY_EDITOR
            // Apple TV (often out through AirPlay to HomePods): playback session, stereo, then reopen the engine on it
            try { SC_TVAudio(); } catch { }
            {
                var want = AudioSettings.GetConfiguration(); want.sampleRate = 48000; want.speakerMode = AudioSpeakerMode.Stereo;
                AudioSettings.Reset(want);
            }
#else
            // ask for 48 kHz if a device came up lower (the session may still decide otherwise)
            if (AudioSettings.outputSampleRate < 44100)
            {
                var want = AudioSettings.GetConfiguration(); want.sampleRate = 48000;
                AudioSettings.Reset(want);
            }
#endif
            I.musicVolume = PlayerPrefs.GetFloat("schofield.music", 0.55f);
            I.sfxVolume = PlayerPrefs.GetFloat("schofield.sfx", 1f);
#if UNITY_IOS && !UNITY_EDITOR
            // iPhone only: playback session so the ring/silent switch doesn't mute the game. Apple TV has no
            // switch, and re-categorising the session under a running engine there can leave it silent.
            try { SC_PlaybackAudio(); } catch { }
#endif
            for (int i = 0; i < 28; i++) I.NewSource();
            if (Debug.isDebugBuild) { listener.gameObject.AddComponent<DspTap>(); I.StartCoroutine(I.Probe()); }
            var cfg = AudioSettings.GetConfiguration();
            var test = Assets.Clip("whip_crack");
            Debug.Log($"[audio] rate={AudioSettings.outputSampleRate} speaker={cfg.speakerMode} driver={AudioSettings.driverCapabilities} dsp={cfg.dspBufferSize} paused={AudioListener.pause} vol={AudioListener.volume} music={I.musicVolume} sfx={I.sfxVolume} clip={(test ? test.loadState + "/" + test.length.ToString("F2") + "s/" + test.channels + "ch" : "MISSING")}");
            AudioSettings.OnAudioConfigurationChanged += changed =>
            {
                Debug.Log($"[audio] configuration changed (device {changed}) rate={AudioSettings.outputSampleRate} speaker={AudioSettings.GetConfiguration().speakerMode}");
                if (changed && I) I.Recover();
            };
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
            if (!clip) { if (Debug.isDebugBuild) Debug.Log("[audio] missing clip " + name); return; }
            if (Debug.isDebugBuild && logged++ < 6) Debug.Log($"[audio] play {name} {clip.loadState} vol={volume * (music ? musicVolume : sfxVolume) * master:F2} listenerPaused={AudioListener.pause}");
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
            muffle.enabled = r < 1;
            foreach (var s in pool) if (s.isPlaying && basePitch.TryGetValue(s, out var bp)) s.pitch = bp * r;
        }

        public void Pause(bool on) => AudioListener.pause = on;

        System.Collections.IEnumerator Probe()
        {
            double d0 = AudioSettings.dspTime;
            yield return new WaitForSecondsRealtime(4);
            var sb = new System.Text.StringBuilder();
            sb.Append($"[audio] probe rate={AudioSettings.outputSampleRate} dsp {d0:F2}->{AudioSettings.dspTime:F2} paused={AudioListener.pause} vol={AudioListener.volume} listeners={FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length}");
            foreach (var kv in loops) if (kv.Value.src) sb.Append($" | {kv.Key} playing={kv.Value.src.isPlaying} t={kv.Value.src.time:F1} vol={kv.Value.src.volume:F2}");
            // where does the signal die? the decoded clip, the source's output, the listener's DSP chain
            foreach (var kv in loops)
            {
                var src = kv.Value.src; if (!src || !src.clip) continue;
                var cb = new float[2048]; float cp = 0; bool got = src.clip.GetData(cb, src.clip.samples / 3);
                foreach (var v in cb) cp = Mathf.Max(cp, Mathf.Abs(v));
                var ob = new float[1024]; float op = 0; src.GetOutputData(ob, 0); foreach (var v in ob) op = Mathf.Max(op, Mathf.Abs(v));
                sb.Append($" | {kv.Key} clip={src.clip.loadType}/{src.clip.loadState} data={got}:{cp:F3} srcOut={op:F4} mute={src.mute} prio={src.priority} virt={src.isVirtual}");
            }
            sb.Append($" | dspCalls={DspTap.calls} dspIn={DspTap.peak:F4}");
            // is the mixer actually putting signal out?
            var buf = new float[1024]; float peak = 0;
            AudioListener.GetOutputData(buf, 0); foreach (var v in buf) peak = Mathf.Max(peak, Mathf.Abs(v));
            sb.Append($" | mixPeak={peak:F4}");
            Debug.Log(sb.ToString());
#if (UNITY_IOS || UNITY_TVOS) && !UNITY_EDITOR
            try { Debug.Log("[audio] session " + SC_AudioReport()); } catch (System.Exception e) { Debug.Log("[audio] session report failed " + e.Message); }
#endif
        }

        /// the output device changed (HDMI renegotiated, headphones in or out): restart the engine on the new
        /// route and pick the loops back up — otherwise some devices just go quiet
        void Recover()
        {
            var was = new List<AudioSource>();
            foreach (var a in GetComponentsInChildren<AudioSource>(true)) if (a.isPlaying && a.loop) was.Add(a);
            AudioSettings.Reset(AudioSettings.GetConfiguration());
            foreach (var a in was) if (a) a.Play();
        }

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

    /// debug: counts DSP callbacks on the listener and the loudest sample reaching it
    public class DspTap : MonoBehaviour
    {
        public static int calls; public static float peak;
        void OnAudioFilterRead(float[] data, int ch) { calls++; for (int i = 0; i < data.Length; i++) { float a = data[i] < 0 ? -data[i] : data[i]; if (a > peak) peak = a; } }
    }
}
