/**
 * Body Avatar — lightweight 3D rig.
 *
 * Renders in a draggable floating window using THREE.WebGLRenderer.
 * Polls /api/monitor/body for joint activations.
 *
 * Controls:
 *   Arrow keys: rotate rig (yaw/pitch)
 *   PageUp/PageDown: roll
 */
window.BodyAvatar = (function () {
  let container;
  let canvas;
  let statusEl;
  let visible = false;

  let pollTimer = null;
  let state = null;
  let smooth = null;
  const LERP = 0.14;

  // 3D
  let scene, camera, renderer;
  let rig;
  let animHandle = 0;
  let rotYaw = 0, rotPitch = 0, rotRoll = 0;
  const ROT_STEP = 0.07;
  let keyHandlerAttached = false;

  // parts
  const joints = {}; // name -> Mesh
  const bones = {};  // name -> Mesh
  let torsoMesh = null;
  let pelvisMesh = null;

  function lerp(a, b, t) { return a + (b - a) * t; }
  function clamp(v, a, b) { return v < a ? a : (v > b ? b : v); }

  function ensureKeyHandler() {
    if (keyHandlerAttached) return;
    keyHandlerAttached = true;
    window.addEventListener('keydown', (e) => {
      if (!visible) return;
      if (e.key === 'ArrowLeft') { rotYaw -= ROT_STEP; e.preventDefault(); }
      else if (e.key === 'ArrowRight') { rotYaw += ROT_STEP; e.preventDefault(); }
      else if (e.key === 'ArrowUp') { rotPitch -= ROT_STEP; e.preventDefault(); }
      else if (e.key === 'ArrowDown') { rotPitch += ROT_STEP; e.preventDefault(); }
      else if (e.key === 'PageUp') { rotRoll -= ROT_STEP; e.preventDefault(); }
      else if (e.key === 'PageDown') { rotRoll += ROT_STEP; e.preventDefault(); }
    }, { passive: false });
  }

  function init() {
    if (container) return;

    container = document.createElement('div');
    container.id = 'body-avatar-window';
    container.innerHTML = `
      <div class="ba-titlebar">
        <span class="ba-title">Body Avatar</span>
        <span class="ba-close" onclick="BodyAvatar.hide()">✕</span>
      </div>
      <canvas id="ba-canvas" width="280" height="360"></canvas>
      <div class="ba-status" id="ba-status">Idle</div>
    `;
    document.body.appendChild(container);

    canvas = container.querySelector('#ba-canvas');
    statusEl = container.querySelector('#ba-status');

    // draggable
    let dragging = false, dx = 0, dy = 0;
    const titlebar = container.querySelector('.ba-titlebar');
    titlebar.addEventListener('mousedown', (e) => {
      dragging = true;
      dx = e.clientX - container.offsetLeft;
      dy = e.clientY - container.offsetTop;
      e.preventDefault();
    });
    window.addEventListener('mousemove', (e) => {
      if (!dragging) return;
      container.style.left = (e.clientX - dx) + 'px';
      container.style.top = (e.clientY - dy) + 'px';
    });
    window.addEventListener('mouseup', () => { dragging = false; });

    init3D();
  }

  function init3D() {
    if (typeof THREE === 'undefined') {
      console.warn('BodyAvatar: THREE not found.');
      return;
    }

    renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: true });
    renderer.setPixelRatio(Math.max(1, window.devicePixelRatio || 1));
    renderer.setSize(canvas.width, canvas.height, false);

    scene = new THREE.Scene();
    camera = new THREE.PerspectiveCamera(45, canvas.width / canvas.height, 0.01, 50);
    camera.position.set(0.0, 1.35, 2.6);
    camera.lookAt(0, 0.9, 0);

    const hemi = new THREE.HemisphereLight(0xffffff, 0x2b2f3a, 0.85);
    scene.add(hemi);
    const dir = new THREE.DirectionalLight(0xffffff, 0.9);
    dir.position.set(2.5, 3.5, 2.0);
    scene.add(dir);

    const groundMat = new THREE.MeshStandardMaterial({ color: 0x141820, roughness: 1.0, metalness: 0.0, transparent: true, opacity: 0.35 });
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(4, 4), groundMat);
    ground.rotation.x = -Math.PI / 2;
    ground.position.y = 0.02;
    scene.add(ground);

    rig = new THREE.Group();
    scene.add(rig);

    buildRigMeshes();
  }

  function buildRigMeshes() {
    const baseMat = new THREE.MeshStandardMaterial({ color: 0x8ea7c9, roughness: 0.55, metalness: 0.08 });
    const jointMat = new THREE.MeshStandardMaterial({ color: 0xb9c9e6, roughness: 0.35, metalness: 0.15 });

    const jointGeo = new THREE.SphereGeometry(0.055, 18, 16);
    const smallJointGeo = new THREE.SphereGeometry(0.045, 16, 14);
    const tinyJointGeo = new THREE.SphereGeometry(0.040, 14, 12);

    function addJoint(name, geo) {
      const m = new THREE.Mesh(geo, jointMat.clone());
      m.userData = { baseColor: m.material.color.clone() };
      rig.add(m);
      joints[name] = m;
    }

    addJoint('waist', jointGeo);
    addJoint('neck', jointGeo);
    addJoint('head', jointGeo);

    addJoint('l_shoulder', smallJointGeo);
    addJoint('r_shoulder', smallJointGeo);
    addJoint('l_elbow', tinyJointGeo);
    addJoint('r_elbow', tinyJointGeo);
    addJoint('l_wrist', tinyJointGeo);
    addJoint('r_wrist', tinyJointGeo);

    addJoint('l_hip', smallJointGeo);
    addJoint('r_hip', smallJointGeo);
    addJoint('l_knee', tinyJointGeo);
    addJoint('r_knee', tinyJointGeo);
    addJoint('l_ankle', tinyJointGeo);
    addJoint('r_ankle', tinyJointGeo);

    torsoMesh = new THREE.Mesh(new THREE.CylinderGeometry(0.26, 0.18, 0.56, 4, 1, false), baseMat.clone());
    torsoMesh.position.set(0, 1.04, 0);
    torsoMesh.rotation.y = Math.PI / 4;
    torsoMesh.userData = { baseColor: torsoMesh.material.color.clone() };
    rig.add(torsoMesh);

    pelvisMesh = new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.20, 0.22, 4, 1, false), baseMat.clone());
    pelvisMesh.position.set(0, 0.76, 0);
    pelvisMesh.rotation.y = Math.PI / 4;
    pelvisMesh.userData = { baseColor: pelvisMesh.material.color.clone() };
    rig.add(pelvisMesh);

    function addBone(name, rTop, rBot) {
      const geo = new THREE.CylinderGeometry(rTop, rBot, 1.0, 12, 1, false);
      const mesh = new THREE.Mesh(geo, baseMat.clone());
      mesh.userData = { baseColor: mesh.material.color.clone() };
      rig.add(mesh);
      bones[name] = mesh;
    }

    addBone('l_upperArm', 0.045, 0.038);
    addBone('r_upperArm', 0.045, 0.038);
    addBone('l_lowerArm', 0.038, 0.032);
    addBone('r_lowerArm', 0.038, 0.032);
    addBone('l_thigh', 0.060, 0.050);
    addBone('r_thigh', 0.060, 0.050);
    addBone('l_shin', 0.050, 0.042);
    addBone('r_shin', 0.050, 0.042);
    addBone('spine', 0.055, 0.045);
    addBone('neckBone', 0.040, 0.035);
  }

  function setJointPos(name, x, y, z) {
    const j = joints[name];
    if (j) j.position.set(x, y, z);
  }

  function placeBoneBetween(boneName, aName, bName) {
    const bone = bones[boneName];
    const a = joints[aName];
    const b = joints[bName];
    if (!bone || !a || !b) return;

    const av = a.position;
    const bv = b.position;
    const mid = new THREE.Vector3().addVectors(av, bv).multiplyScalar(0.5);
    bone.position.copy(mid);

    const dir = new THREE.Vector3().subVectors(bv, av);
    const len = dir.length();
    if (len < 1e-4) { bone.visible = false; return; }
    bone.visible = true;
    dir.normalize();
    bone.quaternion.copy(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir));
    bone.scale.set(1, len, 1);
  }

  function applyRigPose(s) {
    const hipY = 0.72;
    const shoulderY = 1.22;
    const neckY = 1.33;
    const headY = 1.48;
    const hipW = 0.22;
    const shoulderW = 0.30;
    const upperArm = 0.26;
    const lowerArm = 0.24;
    const thigh = 0.34;
    const shin = 0.34;

    const la = s?.leftArm ?? 0;
    const ra = s?.rightArm ?? 0;
    const ll = s?.leftLeg ?? 0;
    const rl = s?.rightLeg ?? 0;
    const core = s?.core ?? 0;

    const armLiftL = (la - 0.5) * 1.3;
    const armLiftR = (ra - 0.5) * 1.3;
    const legKickL = (ll - 0.5) * 0.9;
    const legKickR = (rl - 0.5) * 0.9;
    const twist = (core - 0.5) * 0.55;

    setJointPos('waist', 0, 0.88, 0);
    setJointPos('neck', 0, neckY, 0);
    setJointPos('head', 0, headY, 0);

    setJointPos('l_shoulder', -shoulderW, shoulderY, 0);
    setJointPos('r_shoulder', +shoulderW, shoulderY, 0);
    setJointPos('l_hip', -hipW, hipY, 0);
    setJointPos('r_hip', +hipW, hipY, 0);

    // left arm
    {
      const sh = joints.l_shoulder.position;
      const el = new THREE.Vector3(sh.x - 0.10, sh.y - upperArm * (0.75 - 0.15 * Math.sin(armLiftL)), sh.z + upperArm * Math.sin(armLiftL));
      joints.l_elbow.position.copy(el);
      const wr = new THREE.Vector3(el.x - 0.08, el.y - lowerArm * (0.85 - 0.10 * Math.sin(armLiftL)), el.z + lowerArm * Math.sin(armLiftL));
      joints.l_wrist.position.copy(wr);
    }
    // right arm
    {
      const sh = joints.r_shoulder.position;
      const el = new THREE.Vector3(sh.x + 0.10, sh.y - upperArm * (0.75 - 0.15 * Math.sin(armLiftR)), sh.z + upperArm * Math.sin(armLiftR));
      joints.r_elbow.position.copy(el);
      const wr = new THREE.Vector3(el.x + 0.08, el.y - lowerArm * (0.85 - 0.10 * Math.sin(armLiftR)), el.z + lowerArm * Math.sin(armLiftR));
      joints.r_wrist.position.copy(wr);
    }
    // legs
    {
      const hp = joints.l_hip.position;
      const kn = new THREE.Vector3(hp.x, hp.y - thigh, hp.z + thigh * 0.35 * Math.sin(legKickL));
      joints.l_knee.position.copy(kn);
      const an = new THREE.Vector3(hp.x, kn.y - shin, kn.z + shin * 0.35 * Math.sin(legKickL));
      joints.l_ankle.position.copy(an);
    }
    {
      const hp = joints.r_hip.position;
      const kn = new THREE.Vector3(hp.x, hp.y - thigh, hp.z + thigh * 0.35 * Math.sin(legKickR));
      joints.r_knee.position.copy(kn);
      const an = new THREE.Vector3(hp.x, kn.y - shin, kn.z + shin * 0.35 * Math.sin(legKickR));
      joints.r_ankle.position.copy(an);
    }

    placeBoneBetween('l_upperArm', 'l_shoulder', 'l_elbow');
    placeBoneBetween('l_lowerArm', 'l_elbow', 'l_wrist');
    placeBoneBetween('r_upperArm', 'r_shoulder', 'r_elbow');
    placeBoneBetween('r_lowerArm', 'r_elbow', 'r_wrist');
    placeBoneBetween('l_thigh', 'l_hip', 'l_knee');
    placeBoneBetween('l_shin', 'l_knee', 'l_ankle');
    placeBoneBetween('r_thigh', 'r_hip', 'r_knee');
    placeBoneBetween('r_shin', 'r_knee', 'r_ankle');
    placeBoneBetween('spine', 'waist', 'neck');
    placeBoneBetween('neckBone', 'neck', 'head');

    if (torsoMesh) {
      torsoMesh.rotation.set(0, Math.PI / 4 + twist, 0);
    }
    if (pelvisMesh) {
      pelvisMesh.rotation.set(0, Math.PI / 4 + twist * 0.65, 0);
    }
  }

  function updateMaterialsFromActivation(s) {
    const act = {
      leftArm: clamp(s?.leftArm ?? 0, 0, 1),
      rightArm: clamp(s?.rightArm ?? 0, 0, 1),
      leftLeg: clamp(s?.leftLeg ?? 0, 0, 1),
      rightLeg: clamp(s?.rightLeg ?? 0, 0, 1),
      core: clamp(s?.core ?? 0, 0, 1)
    };

    function setHot(mesh, k) {
      if (!mesh || !mesh.material) return;
      const base = mesh.userData?.baseColor;
      if (!base) return;
      const m = mesh.material;
      m.color.copy(base).multiplyScalar(lerp(1.0, 1.35, k));
      if (m.emissive) m.emissive.setRGB(0.10 * k, 0.12 * k, 0.16 * k);
    }

    const armL = act.leftArm + act.core * 0.35;
    const armR = act.rightArm + act.core * 0.35;
    const legL = act.leftLeg + act.core * 0.25;
    const legR = act.rightLeg + act.core * 0.25;
    const coreK = act.core;

    setHot(bones.l_upperArm, armL); setHot(bones.l_lowerArm, armL);
    setHot(joints.l_shoulder, armL); setHot(joints.l_elbow, armL); setHot(joints.l_wrist, armL);
    setHot(bones.r_upperArm, armR); setHot(bones.r_lowerArm, armR);
    setHot(joints.r_shoulder, armR); setHot(joints.r_elbow, armR); setHot(joints.r_wrist, armR);
    setHot(bones.l_thigh, legL); setHot(bones.l_shin, legL);
    setHot(joints.l_hip, legL); setHot(joints.l_knee, legL); setHot(joints.l_ankle, legL);
    setHot(bones.r_thigh, legR); setHot(bones.r_shin, legR);
    setHot(joints.r_hip, legR); setHot(joints.r_knee, legR); setHot(joints.r_ankle, legR);

    setHot(torsoMesh, coreK); setHot(pelvisMesh, coreK); setHot(bones.spine, coreK);
    setHot(bones.neckBone, coreK * 0.6); setHot(joints.waist, coreK); setHot(joints.neck, coreK * 0.8); setHot(joints.head, coreK * 0.45);
  }

  function tick3D() {
    animHandle = requestAnimationFrame(tick3D);
    if (!renderer || !scene || !camera || !rig) return;
    rig.rotation.set(rotPitch, rotYaw, rotRoll);
    if (smooth) {
      applyRigPose(smooth);
      updateMaterialsFromActivation(smooth);
    }
    renderer.render(scene, camera);
  }

  async function poll() {
    try {
      const resp = await fetch('/api/monitor/body');
      if (!resp.ok) return;
      const s = await resp.json();
      state = s;
      if (!smooth) smooth = JSON.parse(JSON.stringify(state));
      else {
        for (const k of Object.keys(state)) {
          const b = state[k];
          if (typeof b === 'number') smooth[k] = lerp(smooth[k] ?? 0, b, LERP);
          else smooth[k] = b;
        }
      }
      if (statusEl) statusEl.textContent = state?.mode ?? 'Active';
    } catch { }
  }

  function show() {
    init();
    if (!container) return;
    visible = true;
    container.style.display = 'block';
    container.style.left = container.style.left || '18px';
    container.style.top = container.style.top || '120px';
    ensureKeyHandler();

    if (!pollTimer) pollTimer = setInterval(poll, 120);
    if (!animHandle) tick3D();
  }

  function hide() {
    visible = false;
    if (container) container.style.display = 'none';
    if (pollTimer) { clearInterval(pollTimer); pollTimer = null; }
    if (animHandle) { cancelAnimationFrame(animHandle); animHandle = 0; }
  }

  function toggle() {
    if (visible) hide();
    else show();
  }

  return { init, show, hide, toggle };
})();
