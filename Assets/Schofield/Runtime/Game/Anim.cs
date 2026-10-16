// Named-clip animation for the GLB characters: cross-fades between looping/one-shot clips, playback
// speed, and an additive recoil layer (the web game's AnimationMixer). Works with glTFast's legacy
// Animation import, and falls back to a Playables mixer if the clips came in as Mecanim.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Schofield
{
    public class Anim
    {
        static readonly HashSet<string> Recoils = new HashSet<string> { "Shoot", "SeatShoot", "ShootL" };
        readonly Animation legacy;
        public string current;
        float speed = 1;

        // Mecanim fallback
        PlayableGraph graph; AnimationMixerPlayable mixer; AnimationLayerMixerPlayable layers;
        readonly Dictionary<string, int> index = new Dictionary<string, int>();
        readonly List<AnimationClipPlayable> clipPlayables = new List<AnimationClipPlayable>();
        readonly List<float> weights = new List<float>();
        float fadeRate = 4; bool once;

        public Anim(GameObject root)
        {
            legacy = root.GetComponentInChildren<Animation>(true);
            // glTFast imports these models' clips for Mecanim; an Animation component playing non-legacy clips
            // writes garbage into the bones on most frames (the models flicker and fly apart). Play those clips
            // through the Playables mixer below instead.
            var fromLegacy = new List<AnimationClip>();
            GameObject animHost = null;
            if (legacy)
            {
                bool allLegacy = true;
                var names = new List<string>();
                foreach (AnimationState st in legacy) { if (st.clip) { names.Add(st.name); if (!st.clip.legacy) { fromLegacy.Add(st.clip); allLegacy = false; } } }
                // legacy clips: use their Mecanim copies (Resources/Anims/<model>/) when the model has them
                var model = Characters.ModelOf(root);
                if (allLegacy && model != null)
                {
                    var copies = new List<AnimationClip>();
                    foreach (var n in names) { var c = Assets.Clip(model, n); if (c) copies.Add(c); }
                    if (copies.Count == names.Count && copies.Count > 0) { fromLegacy = copies; allLegacy = false; }
                }
                if (!allLegacy || names.Count == 0)
                {
                    animHost = legacy.gameObject;
                    legacy.Stop(); legacy.enabled = false; Object.Destroy(legacy);
                    legacy = null;
                }
            }
            if (legacy)
            {
                legacy.cullingType = AnimationCullingType.AlwaysAnimate;
                legacy.playAutomatically = false;
                foreach (AnimationState st in legacy)
                {
                    if (Recoils.Contains(st.name))
                    {
                        st.blendMode = AnimationBlendMode.Additive;
                        st.layer = 10; st.wrapMode = WrapMode.Once; st.weight = 0; st.enabled = false;
                    }
                    else { st.layer = 0; st.wrapMode = WrapMode.Loop; }
                }
                legacy.Stop();
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-noanim") >= 0) legacy.enabled = false;   // testing
                return;
            }
            var animator = root.GetComponentInChildren<Animator>(true);
            if (!animator) { animator = (animHost ? animHost : root).AddComponent<Animator>(); }
            var clips = animator.runtimeAnimatorController ? animator.runtimeAnimatorController.animationClips : new AnimationClip[0];
            if (clips.Length == 0) clips = fromLegacy.ToArray();
            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("anim");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "out", animator);
            layers = AnimationLayerMixerPlayable.Create(graph, 2);
            mixer = AnimationMixerPlayable.Create(graph, 0);
            var rec = AnimationMixerPlayable.Create(graph, 0);
            graph.Connect(mixer, 0, layers, 0); graph.Connect(rec, 0, layers, 1);
            layers.SetInputWeight(0, 1); layers.SetInputWeight(1, 1);
            layers.SetLayerAdditive(1, true);
            foreach (var c in clips)
            {
                if (index.ContainsKey(c.name)) continue;
                var p = AnimationClipPlayable.Create(graph, c);
                bool r = Recoils.Contains(c.name);
                var target = r ? rec : mixer;
                int port = target.GetInputCount();
                target.SetInputCount(port + 1);
                graph.Connect(p, 0, target, port);
                target.SetInputWeight(port, 0);
                index[c.name] = clipPlayables.Count;
                clipPlayables.Add(p); weights.Add(0);
            }
            output.SetSourcePlayable(layers);
            graph.Play();
        }

        public bool Has(string name) => legacy ? legacy[name] != null : index.ContainsKey(name);

        public void Play(string name, float fade = 0.25f, bool once = false, float timeScale = 1, float phase = -1)
        {
            if (!Has(name) || current == name) return;
            current = name; speed = timeScale; this.once = once;
            if (legacy)
            {
                var st = legacy[name];
                st.wrapMode = once ? WrapMode.ClampForever : WrapMode.Loop;
                st.speed = timeScale;
                if (fade > 0 && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nofade") < 0) legacy.CrossFade(name, fade, PlayMode.StopSameLayer); else legacy.Play(name, PlayMode.StopSameLayer);
                st.time = phase >= 0 ? phase * st.length : 0;
                return;
            }
            fadeRate = 1 / Mathf.Max(0.01f, fade);
            var cp = clipPlayables[index[name]];
            cp.SetTime(phase >= 0 ? phase * cp.GetAnimationClip().length : 0);
            cp.SetSpeed(timeScale);
            cp.GetAnimationClip().wrapMode = once ? WrapMode.ClampForever : WrapMode.Loop;
        }

        public void SetSpeed(float s)
        {
            if (current == null || Mathf.Approximately(s, speed)) return;
            speed = s;
            if (legacy) { var st = legacy[current]; if (st) st.speed = s; }
            else clipPlayables[index[current]].SetSpeed(s);
        }

        /// the seated guard recoils with SeatShoot against SeatAim; everyone else with Shoot
        public void Kick(bool left = false)
        {
            string n = left ? "ShootL" : current == "SeatAim" && Has("SeatShoot") ? "SeatShoot" : "Shoot";
            if (!Has(n)) return;
            if (legacy)
            {
                var st = legacy[n];
                st.enabled = true; st.time = 0; st.weight = 1; st.speed = 1;
                legacy.Blend(n, 1, 0);
                return;
            }
            var cp = clipPlayables[index[n]];
            cp.SetTime(0); recoilT[n] = cp.GetAnimationClip().length;
        }
        readonly Dictionary<string, float> recoilT = new Dictionary<string, float>();

        /// normalised time through the current clip
        public float Phase
        {
            get
            {
                if (current == null) return 0;
                if (legacy) { var st = legacy[current]; return st ? Mathf.Repeat(st.normalizedTime, 1) : 0; }
                var cp = clipPlayables[index[current]];
                return Mathf.Repeat((float)(cp.GetTime() / Mathf.Max(0.001f, cp.GetAnimationClip().length)), 1);
            }
        }

        /// Mecanim fallback only: fade weights (legacy fades itself)
        public void Update(float dt)
        {
            if (legacy || !graph.IsValid()) return;
            int ci = current != null && index.TryGetValue(current, out var x) ? x : -1;
            for (int i = 0; i < clipPlayables.Count; i++)
            {
                var name = clipPlayables[i].GetAnimationClip().name;
                if (Recoils.Contains(name))
                {
                    recoilT.TryGetValue(name, out var t);
                    t -= dt; recoilT[name] = t;
                    var parent = (AnimationMixerPlayable)clipPlayables[i].GetOutput(0);
                    for (int port = 0; port < parent.GetInputCount(); port++)
                        if (parent.GetInput(port).Equals(clipPlayables[i])) parent.SetInputWeight(port, t > 0 ? 1 : 0);
                    continue;
                }
                weights[i] = Mathf.MoveTowards(weights[i], i == ci ? 1 : 0, dt * fadeRate);
            }
            for (int port = 0; port < mixer.GetInputCount(); port++)
            {
                var p = (AnimationClipPlayable)mixer.GetInput(port);
                mixer.SetInputWeight(port, weights[index[p.GetAnimationClip().name]]);
            }
        }

        public void Dispose() { if (!legacy && graph.IsValid()) graph.Destroy(); }
    }
}
