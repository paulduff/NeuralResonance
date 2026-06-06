// AvatarRenderer — 3D artist mannequin (wooden model) rendered into a small inset.
//
// Called from Blazor fast-frame loop:
//   AvatarRenderer.init();
//   AvatarRenderer.render(float[21]);
//   AvatarRenderer.destroy();
//
// Uses global THREE (loaded in _Host.cshtml).

window.AvatarRenderer = (() => {
  let inset, emo;
  let canvas, renderer, scene, camera;
  let root, rig;
  let _raf = 0;
  let _alive = false;

  // orientation (arrow-key cluster)
  let yaw = 0, pitch = 0, roll = 0;
  let _keyHandler = null;

  // cached body vector
  let _b = null;
  let _bSmooth = null;

  // mannequin parts
  const P = {};

  function ensureThree() {
    if (!window.THREE) {
      console.warn('AvatarRenderer: THREE not found (three.min.js not loaded).');
      return false;
    }
    return true;
  }

  // --- materials (wood) ---
  const WOOD = {
    base: 0xd9c7a6,
    dark: 0xb99f78,
    accent: 0xe8dcc6
  };

  function matWood(color, rough = 0.82) {
    return new THREE.MeshStandardMaterial({
      color,
      roughness: rough,
      metalness: 0.02
    });
  }

  function mkSphere(r, color) {
    const g = new THREE.SphereGeometry(r, 20, 16);
    return new THREE.Mesh(g, matWood(color));
  }

  function mkCylinder(rTop, rBot, h, color, radial = 18) {
    const g = new THREE.CylinderGeometry(rTop, rBot, h, radial, 1, false);
    return new THREE.Mesh(g, matWood(color));
  }

  function mkBox(w, h, d, color) {
    const g = new THREE.BoxGeometry(w, h, d);
    return new THREE.Mesh(g, matWood(color, 0.78));
  }

  function mkCapsule(headR, headH, color) {
    // Cylinder + spheres (capsule approximation)
    const grp = new THREE.Group();
    const cylH = Math.max(0.0001, headH - headR * 2);
    const cyl = mkCylinder(headR * 0.92, headR * 0.92, cylH, color, 20);
    grp.add(cyl);
    const s1 = mkSphere(headR, color);
    const s2 = mkSphere(headR, color);
    s1.position.y = cylH * 0.5;
    s2.position.y = -cylH * 0.5;
    grp.add(s1); grp.add(s2);
    return grp;
  }

  function mkTorus(r, tube, color) {
    const g = new THREE.TorusGeometry(r, tube, 10, 28);
    return new THREE.Mesh(g, matWood(color, 0.7));
  }

  function setBetween(mesh, a, b) {
    // Assumes mesh is a cylinder with Y-axis height.
    const dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
    const len = Math.max(0.0001, Math.sqrt(dx*dx + dy*dy + dz*dz));

    mesh.position.set((a.x + b.x) * 0.5, (a.y + b.y) * 0.5, (a.z + b.z) * 0.5);
    mesh.scale.set(1, len / (mesh.geometry.parameters.height || 1), 1);

    const dir = new THREE.Vector3(dx/len, dy/len, dz/len);
    const up = new THREE.Vector3(0, 1, 0);
    const q = new THREE.Quaternion().setFromUnitVectors(up, dir);
    mesh.setRotationFromQuaternion(q);
  }

  function setBetweenWithRoll(mesh, a, b, rollRad) {
    setBetween(mesh, a, b);
    if (rollRad && Math.abs(rollRad) > 1e-5) mesh.rotateY(rollRad);
  }

  function buildRig() {
    scene = new THREE.Scene();
    scene.background = null;

    // Lighting to read as a wooden mannequin
    scene.add(new THREE.AmbientLight(0xffffff, 0.35));

    const hemi = new THREE.HemisphereLight(0xffffff, 0x202838, 0.85);
    scene.add(hemi);

    const key = new THREE.DirectionalLight(0xffffff, 0.95);
    key.position.set(2.2, 3.4, 2.8);
    scene.add(key);

    const rim = new THREE.DirectionalLight(0xffffff, 0.35);
    rim.position.set(-2.0, 2.2, -3.0);
    scene.add(rim);

    camera = new THREE.PerspectiveCamera(38, 1, 0.01, 100);
    camera.position.set(0.2, 1.25, 4.4);
    camera.lookAt(0, 1.1, 0);

    root = new THREE.Group();
    scene.add(root);

    // Stand / base
    const base = mkCylinder(0.50, 0.55, 0.06, 0xb79a72, 32);
    base.position.set(0, 0.03, 0);
    root.add(base);

    const pole = mkCylinder(0.06, 0.06, 0.85, 0x9b8d7a, 20);
    pole.position.set(0, 0.48, -0.02);
    root.add(pole);

    const mount = mkSphere(0.07, 0x9b8d7a);
    mount.position.set(0, 0.92, -0.02);
    root.add(mount);

    rig = new THREE.Group();
    rig.position.set(0, 0.12, 0);
    root.add(rig);

    // --- proportions (artist mannequin-like) ---
    // Joints
    P.hipL = mkSphere(0.075, WOOD.base);
    P.hipR = mkSphere(0.075, WOOD.base);
    P.kneeL = mkSphere(0.065, WOOD.base);
    P.kneeR = mkSphere(0.065, WOOD.base);
    P.ankleL = mkSphere(0.060, WOOD.base);
    P.ankleR = mkSphere(0.060, WOOD.base);

    P.shoulderL = mkSphere(0.070, WOOD.base);
    P.shoulderR = mkSphere(0.070, WOOD.base);
    P.elbowL = mkSphere(0.060, WOOD.base);
    P.elbowR = mkSphere(0.060, WOOD.base);
    P.wristL = mkSphere(0.055, WOOD.base);
    P.wristR = mkSphere(0.055, WOOD.base);

    // Torso segments (rounded blocks like a mannequin)
    P.pelvis = mkBox(0.34, 0.17, 0.20, WOOD.base);
    P.pelvis.position.y = 0.92;

    P.abdomen = mkBox(0.30, 0.18, 0.18, WOOD.accent);
    P.abdomen.position.y = 1.08;

    P.chest = mkBox(0.34, 0.22, 0.20, WOOD.base);
    P.chest.position.y = 1.28;

    // Waist ring seam
    P.waistRing = mkTorus(0.16, 0.012, WOOD.dark);
    P.waistRing.rotation.x = Math.PI * 0.5;
    P.waistRing.position.y = 1.02;

    // Neck + head
    P.neck = mkCylinder(0.06, 0.07, 0.14, WOOD.base, 18);
    P.neck.position.y = 1.48;

    P.head = mkCapsule(0.13, 0.32, WOOD.base);
    P.head.position.set(0, 1.70, 0);

    // Shoulder yoke (wood block that connects shoulders)
    P.shoulderYoke = mkBox(0.36, 0.10, 0.16, WOOD.accent);
    P.shoulderYoke.position.y = 1.40;

    // Limbs (tapered cylinders)
    P.upperArmL = mkCylinder(0.055, 0.050, 0.30, WOOD.base);
    P.lowerArmL = mkCylinder(0.050, 0.045, 0.28, WOOD.base);
    P.upperArmR = mkCylinder(0.055, 0.050, 0.30, WOOD.base);
    P.lowerArmR = mkCylinder(0.050, 0.045, 0.28, WOOD.base);

    P.thighL = mkCylinder(0.070, 0.060, 0.40, WOOD.base);
    P.shinL  = mkCylinder(0.060, 0.050, 0.38, WOOD.base);
    P.thighR = mkCylinder(0.070, 0.060, 0.40, WOOD.base);
    P.shinR  = mkCylinder(0.060, 0.050, 0.38, WOOD.base);

    // Hands/feet blocks
    P.handL = mkBox(0.09, 0.035, 0.12, WOOD.accent);
    P.handR = mkBox(0.09, 0.035, 0.12, WOOD.accent);
    P.footL = mkBox(0.10, 0.04, 0.16, WOOD.accent);
    P.footR = mkBox(0.10, 0.04, 0.16, WOOD.accent);

    // Joint seam rings (elbows/knees)
    P.elbowRingL = mkTorus(0.055, 0.010, WOOD.dark);
    P.elbowRingR = mkTorus(0.055, 0.010, WOOD.dark);
    P.kneeRingL  = mkTorus(0.062, 0.010, WOOD.dark);
    P.kneeRingR  = mkTorus(0.062, 0.010, WOOD.dark);

    // Orient rings
    P.elbowRingL.rotation.x = Math.PI * 0.5;
    P.elbowRingR.rotation.x = Math.PI * 0.5;
    P.kneeRingL.rotation.x  = Math.PI * 0.5;
    P.kneeRingR.rotation.x  = Math.PI * 0.5;

    // Add to rig
    for (const k in P) rig.add(P[k]);

    // Slight soften edges (mannequin look)
    P.pelvis.castShadow = false; P.abdomen.castShadow = false; P.chest.castShadow = false;
  }

  function init() {
    if (_alive) return;
    if (!ensureThree()) return;

    inset = document.getElementById('avatarInset');
    emo = document.getElementById('avatarEmotions');
    if (!inset) return;

    // Hide legacy SVG if present
    const svg = document.getElementById('avatarSvg');
    if (svg) svg.style.display = 'none';

    canvas = document.getElementById('avatarCanvas');
    if (!canvas) {
      canvas = document.createElement('canvas');
      canvas.id = 'avatarCanvas';
      canvas.style.width = '170px';
      canvas.style.height = '220px';
      canvas.style.display = 'block';
      canvas.style.borderRadius = '12px';
      canvas.style.background = 'rgba(0,0,0,0.10)';
      inset.insertBefore(canvas, emo || null);
    }

    inset.style.display = '';

    renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, preserveDrawingBuffer: false });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));

    buildRig();
    resize();

    _alive = true;

    _keyHandler = (e) => {
      const step = (e.shiftKey ? 0.10 : 0.06);
      if (e.key === 'ArrowLeft') { yaw -= step; e.preventDefault(); }
      else if (e.key === 'ArrowRight') { yaw += step; e.preventDefault(); }
      else if (e.key === 'ArrowUp') { pitch -= step; e.preventDefault(); }
      else if (e.key === 'ArrowDown') { pitch += step; e.preventDefault(); }
      else if (e.key === 'PageUp') { roll -= step; e.preventDefault(); }
      else if (e.key === 'PageDown') { roll += step; e.preventDefault(); }
    };

    window.addEventListener('keydown', _keyHandler, { passive: false });
    window.addEventListener('resize', resize);

    loop();
  }

  function resize() {
    if (!renderer || !camera || !canvas) return;
    const w = Math.max(1, Math.floor(canvas.clientWidth));
    const h = Math.max(1, Math.floor(canvas.clientHeight));
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
  }

  function applyBody() {
    if (!_b || !_b.length || !rig) return;

    function clamp(v, lo, hi) { return Math.max(lo, Math.min(hi, v)); }
    function normalizeMotor(raw, rest, gain) {
      const v = Number.isFinite(raw) ? raw : 0;
      if (v >= 0) return clamp((v - rest) * gain, -1, 1);
      return clamp(v * gain, -1, 1);
    }
    function enforceMinLen(a, b, minLen) {
      const d = new THREE.Vector3().subVectors(b, a);
      const len = d.length();
      if (len >= minLen || len < 1e-6) return;
      d.multiplyScalar(minLen / len);
      b.copy(a).add(d);
    }

    if (!_bSmooth || _bSmooth.length !== _b.length) _bSmooth = Array.from(_b);
    const smoothAlpha = 0.22;
    for (let i = 0; i < _b.length; i++) {
      const target = Number.isFinite(_b[i]) ? _b[i] : 0;
      _bSmooth[i] += (target - _bSmooth[i]) * smoothAlpha;
    }

    // Inputs (float[21]) — subtle pose modulation around neutral tone.
    const ht = clamp(_bSmooth[0] || 0, -1, 1);
    const hn = clamp(_bSmooth[1] || 0, -1, 1);
    const sL = normalizeMotor(_bSmooth[2], 0.18, 2.4);
    const sR = normalizeMotor(_bSmooth[3], 0.18, 2.4);
    const eL = normalizeMotor(_bSmooth[4], 0.10, 2.8);
    const eR = normalizeMotor(_bSmooth[5], 0.10, 2.8);
    const wL = normalizeMotor(_bSmooth[6], 0.06, 3.0);
    const wR = normalizeMotor(_bSmooth[7], 0.06, 3.0);
    const hL = normalizeMotor(_bSmooth[8], 0.24, 2.2);
    const hR = normalizeMotor(_bSmooth[9], 0.24, 2.2);
    const kL = normalizeMotor(_bSmooth[10], 0.32, 2.4);
    const kR = normalizeMotor(_bSmooth[11], 0.32, 2.4);
    const torsoLean = clamp(_bSmooth[12] || 0, -1, 1);
    const torsoTwist = clamp(_bSmooth[13] || 0, -1, 1);
    const animEnergy = clamp(_bSmooth[20] || 0, 0, 1);
    const amp = 0.70 + animEnergy * 0.90;

    // Base frame
    const hipY = 0.92;
    const shSpan = 0.22;
    const hipSpan = 0.14;

    const hipLpos = new THREE.Vector3(
      -hipSpan - hL * 0.04 * amp,
      hipY - hL * 0.04 * amp,
      0.02 + (torsoLean + hL * 0.3) * 0.03
    );
    const hipRpos = new THREE.Vector3(
      +hipSpan + hR * 0.04 * amp,
      hipY - hR * 0.04 * amp,
      0.02 + (torsoLean + hR * 0.3) * 0.03
    );

    const kneeLpos = new THREE.Vector3(
      hipLpos.x - hL * 0.04 * amp,
      0.58 + (kL * 0.10 - hL * 0.03) * amp,
      0.04 + (kL * 0.10 + hL * 0.08) * amp
    );
    const kneeRpos = new THREE.Vector3(
      hipRpos.x + hR * 0.04 * amp,
      0.58 + (kR * 0.10 - hR * 0.03) * amp,
      0.04 + (kR * 0.10 + hR * 0.08) * amp
    );

    const ankleLpos = new THREE.Vector3(
      hipLpos.x - hL * 0.01 * amp,
      0.18 - kL * 0.05 * amp,
      0.03 + (kL * 0.11 + hL * 0.05) * amp
    );
    const ankleRpos = new THREE.Vector3(
      hipRpos.x + hR * 0.01 * amp,
      0.18 - kR * 0.05 * amp,
      0.03 + (kR * 0.11 + hR * 0.05) * amp
    );

    enforceMinLen(hipLpos, kneeLpos, 0.26);
    enforceMinLen(hipRpos, kneeRpos, 0.26);
    enforceMinLen(kneeLpos, ankleLpos, 0.24);
    enforceMinLen(kneeRpos, ankleRpos, 0.24);

    const shoulderLpos = new THREE.Vector3(
      -shSpan - sL * 0.02 * amp,
      1.42 + sL * 0.08 * amp,
      torsoTwist * 0.04
    );
    const shoulderRpos = new THREE.Vector3(
      +shSpan + sR * 0.02 * amp,
      1.42 + sR * 0.08 * amp,
      -torsoTwist * 0.04
    );

    const elbowLpos = new THREE.Vector3(
      shoulderLpos.x - 0.20 + eL * 0.11 * amp,
      shoulderLpos.y - 0.22 + eL * 0.06 * amp,
      0.08 + eL * 0.04 * amp
    );
    const elbowRpos = new THREE.Vector3(
      shoulderRpos.x + 0.20 - eR * 0.11 * amp,
      shoulderRpos.y - 0.22 + eR * 0.06 * amp,
      0.08 + eR * 0.04 * amp
    );

    const wristLpos = new THREE.Vector3(
      elbowLpos.x - 0.16 + wL * 0.10 * amp,
      elbowLpos.y - 0.20 + wL * 0.06 * amp,
      0.09 + wL * 0.04 * amp
    );
    const wristRpos = new THREE.Vector3(
      elbowRpos.x + 0.16 - wR * 0.10 * amp,
      elbowRpos.y - 0.20 + wR * 0.06 * amp,
      0.09 + wR * 0.04 * amp
    );

    rig.rotation.set(torsoLean * 0.14 * amp, torsoTwist * 0.20 * amp, 0);

    if (P.head) {
      P.head.position.set(ht * 0.04 * amp, 1.70 + hn * 0.05 * amp, 0);
      P.head.rotation.set(torsoLean * 0.10 * amp, torsoTwist * 0.10 * amp, 0);
    }

    if (P.shoulderYoke) {
      P.shoulderYoke.rotation.set(0, torsoTwist * 0.10, 0);
    }

    P.hipL.position.copy(hipLpos);
    P.hipR.position.copy(hipRpos);
    P.kneeL.position.copy(kneeLpos);
    P.kneeR.position.copy(kneeRpos);
    P.ankleL.position.copy(ankleLpos);
    P.ankleR.position.copy(ankleRpos);

    P.shoulderL.position.copy(shoulderLpos);
    P.shoulderR.position.copy(shoulderRpos);
    P.elbowL.position.copy(elbowLpos);
    P.elbowR.position.copy(elbowRpos);
    P.wristL.position.copy(wristLpos);
    P.wristR.position.copy(wristRpos);

    setBetween(P.thighL, hipLpos, kneeLpos);
    setBetween(P.shinL, kneeLpos, ankleLpos);
    setBetween(P.thighR, hipRpos, kneeRpos);
    setBetween(P.shinR, kneeRpos, ankleRpos);

    setBetweenWithRoll(P.upperArmL, shoulderLpos, elbowLpos, -0.20);
    setBetween(P.lowerArmL, elbowLpos, wristLpos);
    setBetweenWithRoll(P.upperArmR, shoulderRpos, elbowRpos, +0.20);
    setBetween(P.lowerArmR, elbowRpos, wristRpos);

    P.elbowRingL.position.copy(elbowLpos);
    P.elbowRingR.position.copy(elbowRpos);
    P.kneeRingL.position.copy(kneeLpos);
    P.kneeRingR.position.copy(kneeRpos);

    const handFwd = new THREE.Vector3(0, 0, 1);
    P.handL.position.copy(wristLpos).addScaledVector(handFwd, 0.07);
    P.handR.position.copy(wristRpos).addScaledVector(handFwd, 0.07);

    P.footL.position.copy(ankleLpos).addScaledVector(handFwd, 0.09);
    P.footR.position.copy(ankleRpos).addScaledVector(handFwd, 0.09);
    P.footL.rotation.set(0, 0, 0);
    P.footR.rotation.set(0, 0, 0);

    const shW = Math.max(0.32, Math.min(0.40, Math.abs(shoulderRpos.x - shoulderLpos.x) + 0.14));
    P.shoulderYoke.scale.set(shW / 0.36, 1.0, 1.0);

    const hipW = Math.max(0.28, Math.min(0.36, Math.abs(hipRpos.x - hipLpos.x) + 0.14));
    P.pelvis.scale.set(hipW / 0.34, 1.0, 1.0);

    if (emo) emo.textContent = '';
  }

  function loop() {
    if (!_alive) return;
    _raf = requestAnimationFrame(loop);

    if (root) root.rotation.set(pitch, yaw, roll);

    applyBody();
    if (renderer && scene && camera) renderer.render(scene, camera);
  }

  function render(b) {
    _b = Array.isArray(b) ? b : Array.from(b || []);
  }

  function destroy() {
    _alive = false;
    if (_raf) cancelAnimationFrame(_raf);
    _raf = 0;

    window.removeEventListener('resize', resize);
    if (_keyHandler) window.removeEventListener('keydown', _keyHandler);
    _keyHandler = null;

    if (renderer) {
      try { renderer.dispose(); } catch {}
    }

    if (canvas && canvas.parentElement) canvas.parentElement.removeChild(canvas);
    canvas = null;

    renderer = null; scene = null; camera = null; root = null; rig = null;
    _b = null; _bSmooth = null;
    for (const k in P) delete P[k];

    if (inset) inset.style.display = 'none';
  }

  return { init, render, destroy };
})();
