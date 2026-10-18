// Decode the GLBs' animations exactly as the web game does (three.js GLTFLoader + MeshoptDecoder) and
// write each clip resampled at 30 fps, converted to Unity's handedness, keyed by the node path under
// the model root.
import fs from 'fs';
import path from 'path';
const root = process.argv[2], out = process.argv[3];
// the web game's checkout (for its three.js): ~/Schofield, or SCHOFIELD_WEB
// usage: node Tools/anims.mjs Assets/Schofield/Resources/Models Builds/anims.json
const WEB = process.env.SCHOFIELD_WEB || [path.join(process.env.HOME, 'Schofield'), path.join(process.env.HOME, 'mnt/Schofield')].find((p) => fs.existsSync(p));
const THREE = await import(WEB + '/node_modules/three/build/three.module.js');
const { GLTFLoader } = await import(WEB + '/node_modules/three/examples/jsm/loaders/GLTFLoader.js');
const { MeshoptDecoder } = await import(WEB + '/node_modules/three/examples/jsm/libs/meshopt_decoder.module.js');
globalThis.self = globalThis;
const loader = new GLTFLoader();
loader.register(() => ({ name: 'notex', loadTexture: () => Promise.resolve(new THREE.Texture()) }));
loader.setMeshoptDecoder(MeshoptDecoder);
// no textures in node: stub the image loading
loader.manager.setURLModifier((u) => u);
THREE.ImageBitmapLoader && (THREE.ImageBitmapLoader.prototype.load = function (u, onLoad) { onLoad({}); });
THREE.TextureLoader.prototype.load = function () { return new THREE.Texture(); };
const result = {};
for (const f of fs.readdirSync(root).filter((f) => f.endsWith('.glb'))) {
  const buf = fs.readFileSync(path.join(root, f));
  const ab = buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength);
  let gltf;
  try { gltf = await loader.parseAsync(ab, ''); } catch (e) { console.error(f, e.message); continue; }
  const scene = gltf.scene;
  const pathOf = (o) => { const p = []; while (o && o !== scene) { p.unshift(o.name); o = o.parent; } return p.join('/'); };
  const clips = {};
  for (const clip of gltf.animations) {
    const fps = 30, n = Math.max(2, Math.round(clip.duration * fps) + 1);
    const tracks = [];
    for (const tr of clip.tracks) {
      const dot = tr.name.lastIndexOf('.');
      const nodeName = tr.name.slice(0, dot), prop = tr.name.slice(dot + 1);
      const node = scene.getObjectByName(nodeName);
      if (!node) continue;
      const interp = tr.createInterpolant();
      const size = tr.getValueSize();
      const vals = [];
      let prev = null;
      for (let i = 0; i < n; i++) {
        const t = Math.min(clip.duration, i / fps);
        const v = Array.from(interp.evaluate(t)).slice(0, size);
        if (prop === 'quaternion') {
          // right- to left-handed (glTFast negates x): (x, -y, -z, w); keep each key in the previous one's hemisphere
          let q = [v[0], -v[1], -v[2], v[3]];
          if (prev && q[0] * prev[0] + q[1] * prev[1] + q[2] * prev[2] + q[3] * prev[3] < 0) q = q.map((c) => -c);
          prev = q; vals.push(...q);
        } else if (prop === 'position') vals.push(-v[0], v[1], v[2]);
        else if (prop === 'scale') vals.push(v[0], v[1], v[2]);
      }
      if (!['quaternion', 'position', 'scale'].includes(prop)) continue;
      tracks.push({ path: pathOf(node), prop, values: vals.map((x) => Math.round(x * 1e6) / 1e6) });
    }
    clips[clip.name] = { duration: clip.duration, fps, frames: n, tracks };
  }
  result[f.replace('.glb', '')] = clips;
  console.log(f, Object.keys(clips).join(','));
}
fs.writeFileSync(out, JSON.stringify(result));
