import * as THREE from '../vendor/three/three.module.min.js';
import { OrbitControls } from '../vendor/three/addons/controls/OrbitControls.js';

const viewPresets = {
    anterior: { position: [0, -3, 245], target: [0, -3, -4], up: [0, 1, 0] },
    posterior: { position: [0, -3, -245], target: [0, -3, -4], up: [0, 1, 0] },
    left: { position: [245, -3, -4], target: [0, -3, -4], up: [0, 1, 0] },
    right: { position: [-245, -3, -4], target: [0, -3, -4], up: [0, 1, 0] },
    superior: { position: [0, 245, -4], target: [0, -3, -4], up: [0, 0, 1] },
    inferior: { position: [0, -245, -4], target: [0, -3, -4], up: [0, 0, 1] }
};

const corticalDimensions = {
    midlineGap: 1.5,
    halfWidth: 68.5,
    halfHeight: 46.5,
    anteriorRadius: 86,
    posteriorRadius: 81,
    verticalCenter: 4
};

let editor = null;

export async function mountEditor() {
    disposeEditor();

    const host = document.getElementById('brainViewport');
    if (!host) {
        return;
    }

    const atlasResponse = await fetch('/data/brain-atlas.json', { cache: 'no-store' });
    if (!atlasResponse.ok) {
        throw new Error(`Unable to load the editor atlas (${atlasResponse.status}).`);
    }

    const atlas = await atlasResponse.json();
    editor = createEditor(host, atlas);
    await editor.start();
}

export function disposeEditor() {
    if (editor) {
        editor.dispose();
        editor = null;
    }
}

function createEditor(host, atlas) {
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x0e1214);
    scene.fog = new THREE.FogExp2(0x0e1214, 0.0016);

    const camera = new THREE.PerspectiveCamera(36, 1, 0.5, 900);
    camera.position.fromArray(viewPresets.anterior.position);
    camera.up.fromArray(viewPresets.anterior.up);

    const renderer = new THREE.WebGLRenderer({
        antialias: true,
        alpha: false,
        powerPreference: 'high-performance'
    });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.75));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.05;
    host.replaceChildren(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.075;
    controls.minDistance = 105;
    controls.maxDistance = 430;
    controls.target.fromArray(viewPresets.anterior.target);

    const root = new THREE.Group();
    scene.add(root);

    scene.add(new THREE.HemisphereLight(0xd9fff7, 0x2c2522, 1.65));
    const keyLight = new THREE.DirectionalLight(0xfff2dc, 2.15);
    keyLight.position.set(-110, 150, 180);
    scene.add(keyLight);
    const rimLight = new THREE.DirectionalLight(0x729fd8, 1.35);
    rimLight.position.set(130, 40, -160);
    scene.add(rimLight);

    const corticalShells = [];
    corticalShells.push(addCorticalShell(root, -1));
    corticalShells.push(addCorticalShell(root, 1));
    addAnatomicalScaffold(root);

    const structureMeshes = [];
    const meshesByInstance = new Map();
    const meshesByStructure = new Map();
    const structureByInstance = new Map();
    const definitionsById = new Map();
    const structureIdByProtocol = new Map();
    const sphereGeometry = new THREE.SphereGeometry(1, 20, 14);

    for (const structure of atlas.structures) {
        const mesh = createStructureMesh(structure, sphereGeometry);
        root.add(mesh);
        structureMeshes.push(mesh);
        meshesByInstance.set(normalizeId(structure.instanceId), mesh);
        structureByInstance.set(normalizeId(structure.instanceId), structure);
        const structureKey = normalizeId(structure.structureId);
        if (!meshesByStructure.has(structureKey)) {
            meshesByStructure.set(structureKey, []);
        }
        meshesByStructure.get(structureKey).push(mesh);
        if (!definitionsById.has(structureKey)) {
            definitionsById.set(structureKey, structure);
        }
        if (Number.isInteger(structure.protocolStructureId)) {
            structureIdByProtocol.set(structure.protocolStructureId, structure.structureId);
        }
    }

    const pathwayGroup = new THREE.Group();
    root.add(pathwayGroup);

    const state = {
        atlas,
        host,
        scene,
        camera,
        renderer,
        controls,
        root,
        corticalShells,
        structureMeshes,
        meshesByInstance,
        meshesByStructure,
        structureByInstance,
        definitionsById,
        structureIdByProtocol,
        structureCounters: new Map(),
        pathwayGroup,
        raycaster: new THREE.Raycaster(),
        pointer: new THREE.Vector2(),
        selected: null,
        hovered: null,
        mode: 'anatomy',
        currentView: 'anterior',
        showPathways: true,
        shellOpacity: 0.12,
        frameAbort: null,
        frameTimer: 0,
        ageTimer: 0,
        lastFrameAt: 0,
        frameFailureCount: 0,
        active: true,
        disposed: false,
        cleanup: [],
        resizeObserver: null
    };

    buildStructureList(state);
    bindControls(state);
    bindPicking(state);
    const workspaceHandler = event => {
        state.active = event.detail === 'brain';
        if (state.active) {
            requestAnimationFrame(() => resize(state));
        }
    };
    window.addEventListener('nre-workspace-changed', workspaceHandler);
    state.cleanup.push(() => window.removeEventListener('nre-workspace-changed', workspaceHandler));
    resize(state);
    state.resizeObserver = new ResizeObserver(() => resize(state));
    state.resizeObserver.observe(host);
    window.lucide?.createIcons({ attrs: { 'aria-hidden': 'true' } });

    return {
        async start() {
            renderer.setAnimationLoop(() => {
                if (!state.active) {
                    return;
                }
                controls.update();
                animateActivity(state);
                renderer.render(scene, camera);
            });
            await pollFrame(state);
            scheduleFramePoll(state);
            state.ageTimer = window.setInterval(() => updateFrameAge(state), 1000);
        },
        dispose() {
            state.disposed = true;
            window.clearTimeout(state.frameTimer);
            window.clearInterval(state.ageTimer);
            state.frameAbort?.abort();
            state.resizeObserver?.disconnect();
            state.cleanup.forEach(cleanup => cleanup());
            renderer.setAnimationLoop(null);
            controls.dispose();
            sphereGeometry.dispose();
            scene.traverse(object => {
                object.geometry?.dispose?.();
                if (Array.isArray(object.material)) {
                    object.material.forEach(material => material.dispose?.());
                } else {
                    object.material?.dispose?.();
                }
            });
            renderer.dispose();
            host.replaceChildren();
        }
    };
}

function scheduleFramePoll(state) {
    state.frameTimer = window.setTimeout(async () => {
        await pollFrame(state);
        if (!state.disposed) {
            scheduleFramePoll(state);
        }
    }, 900);
}

function addCorticalShell(root, hemisphereSign) {
    const geometry = createCorticalMantleGeometry(hemisphereSign, 72, 44);

    const material = new THREE.MeshPhysicalMaterial({
        color: hemisphereSign < 0 ? 0x7d8997 : 0x8c7e94,
        transparent: true,
        opacity: 0.12,
        depthWrite: false,
        roughness: 0.72,
        metalness: 0,
        clearcoat: 0.18,
        side: THREE.FrontSide
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.renderOrder = 1;
    mesh.userData.isShell = true;
    root.add(mesh);
    return mesh;
}

function createCorticalMantleGeometry(hemisphereSign, thetaSegments, phiSegments) {
    const positions = [];
    const normals = [];
    const indices = [];
    const uvs = [];

    for (let row = 0; row <= phiSegments; row += 1) {
        const v = row / phiSegments;
        const phi = -Math.PI / 2 + (v * Math.PI);
        for (let column = 0; column <= thetaSegments; column += 1) {
            const u = column / thetaSegments;
            const theta = -Math.PI / 2 + (u * Math.PI);
            const point = corticalSurfacePoint(theta, phi, hemisphereSign);
            const normal = corticalSurfaceNormal(theta, phi, hemisphereSign);
            positions.push(point.x, point.y, point.z);
            normals.push(normal.x, normal.y, normal.z);
            uvs.push(u, v);
        }
    }

    addGridIndices(indices, thetaSegments + 1, phiSegments + 1, hemisphereSign < 0);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    geometry.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3));
    geometry.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
    geometry.setIndex(indices);
    geometry.computeBoundingSphere();
    return geometry;
}

function addAnatomicalScaffold(root) {
    const scaffold = new THREE.Group();
    scaffold.name = 'anatomical-scaffold';

    const cerebellarMaterial = translucentMaterial(0x8c8871, 0.1);
    for (const sign of [-1, 1]) {
        const cerebellum = new THREE.Mesh(new THREE.SphereGeometry(1, 38, 24), cerebellarMaterial.clone());
        cerebellum.position.set(sign * 29, -42, -57);
        cerebellum.scale.set(31, 19, 28);
        cerebellum.renderOrder = 1;
        scaffold.add(cerebellum);
    }

    const stem = new THREE.Mesh(
        new THREE.CapsuleGeometry(11, 42, 12, 24),
        translucentMaterial(0x779786, 0.13));
    stem.position.set(0, -42, -25);
    stem.rotation.x = THREE.MathUtils.degToRad(-9);
    stem.renderOrder = 1;
    scaffold.add(stem);

    const callosumCurve = new THREE.CatmullRomCurve3([
        new THREE.Vector3(-39, 19, -8),
        new THREE.Vector3(-18, 32, 4),
        new THREE.Vector3(0, 35, 8),
        new THREE.Vector3(18, 32, 4),
        new THREE.Vector3(39, 19, -8)
    ]);
    const callosum = new THREE.Mesh(
        new THREE.TubeGeometry(callosumCurve, 64, 3.4, 10, false),
        translucentMaterial(0xb9c8d4, 0.22));
    callosum.renderOrder = 2;
    scaffold.add(callosum);

    root.add(scaffold);
}

function createStructureMesh(structure, geometry) {
    const cortical = structure.layout === 'CorticalSheet';
    if (cortical) {
        return createCorticalStructureMesh(structure);
    }

    const color = new THREE.Color(structure.color);
    const material = new THREE.MeshPhysicalMaterial({
        color,
        emissive: color.clone().multiplyScalar(0.08),
        emissiveIntensity: 0.18,
        transparent: true,
        opacity: 0.28,
        depthWrite: false,
        roughness: 0.68,
        metalness: 0,
        side: THREE.DoubleSide
    });
    const mesh = new THREE.Mesh(geometry, material);
    const [x, y, z] = structure.centerMm;
    mesh.position.set(x, y, z);
    const [width, height, depth] = structure.dimensionsMm;
    const scaleFactor = 0.5;
    mesh.scale.set(
        Math.max(2.2, width * scaleFactor),
        Math.max(1.6, height * scaleFactor),
        Math.max(1.8, depth * scaleFactor));
    const baseScale = mesh.scale.clone();
    const [pitch, yaw, roll] = structure.rotationDeg;
    mesh.rotation.set(
        THREE.MathUtils.degToRad(pitch),
        THREE.MathUtils.degToRad(yaw),
        THREE.MathUtils.degToRad(roll));
    mesh.renderOrder = 2;
    mesh.userData = {
        structure,
        activity: 0,
        meanRateHz: 0,
        spikeOut: 0,
        laminarDiagnostics: null,
        isCortical: false,
        focusPoint: mesh.position.clone(),
        baseScale,
        baseOpacity: material.opacity,
        baseColor: color.clone()
    };
    return mesh;
}

function createCorticalStructureMesh(structure) {
    const color = new THREE.Color(structure.color);
    const material = new THREE.MeshPhysicalMaterial({
        color,
        emissive: color.clone().multiplyScalar(0.08),
        emissiveIntensity: 0.18,
        transparent: true,
        opacity: 0.46,
        depthWrite: false,
        roughness: 0.72,
        metalness: 0,
        clearcoat: 0.08,
        side: THREE.DoubleSide
    });
    const hemisphereSign = structure.hemisphere === 'L' ? -1 : 1;
    const { geometry, focusPoint } = createCorticalTerritoryGeometry(structure, hemisphereSign, 34, 8);
    const mesh = new THREE.Mesh(geometry, material);
    mesh.renderOrder = 3;
    mesh.userData = {
        structure,
        activity: 0,
        meanRateHz: 0,
        spikeOut: 0,
        laminarDiagnostics: null,
        isCortical: true,
        focusPoint,
        baseScale: new THREE.Vector3(1, 1, 1),
        baseOpacity: material.opacity,
        baseColor: color.clone()
    };
    return mesh;
}

function createCorticalTerritoryGeometry(structure, hemisphereSign, angularSegments, radialRings) {
    const profile = structure.corticalTerritory;
    if (!profile) {
        throw new Error(`Cortical territory data is missing for ${structure.structureId}.`);
    }

    const center = corticalSurfaceParameters(new THREE.Vector3(...structure.centerMm));
    const positions = [];
    const normals = [];
    const uvs = [];
    const indices = [];
    let focusPoint = null;

    for (let ring = 0; ring <= radialRings; ring += 1) {
        const radialFraction = ring / radialRings;
        for (let segment = 0; segment <= angularSegments; segment += 1) {
            const angle = segment * Math.PI * 2 / angularSegments;
            const boundary = findTerritoryBoundaryRadius(profile.shape, angle);
            const localTheta = Math.cos(angle) * boundary * radialFraction;
            const localPhi = Math.sin(angle) * boundary * radialFraction;
            const point = corticalTerritoryPoint(
                structure.structureId,
                profile,
                center,
                localTheta,
                localPhi,
                hemisphereSign);
            const surface = corticalTerritoryAngles(profile, center, localTheta, localPhi);
            const normal = corticalSurfaceNormal(surface.theta, surface.phi, hemisphereSign);
            positions.push(point.x, point.y, point.z);
            normals.push(normal.x, normal.y, normal.z);
            uvs.push(0.5 + (localTheta * 0.5), 0.5 + (localPhi * 0.5));
            if (ring === 0 && segment === 0) {
                focusPoint = point.clone();
            }
        }
    }

    addGridIndices(indices, angularSegments + 1, radialRings + 1, hemisphereSign < 0);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    geometry.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3));
    geometry.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
    geometry.setIndex(indices);
    geometry.computeBoundingSphere();
    return { geometry, focusPoint };
}

function corticalTerritoryPoint(structureId, profile, center, localTheta, localPhi, hemisphereSign) {
    const angles = corticalTerritoryAngles(profile, center, localTheta, localPhi);
    const surface = corticalSurfacePoint(angles.theta, angles.phi, hemisphereSign);
    const normal = corticalSurfaceNormal(angles.theta, angles.phi, hemisphereSign);
    const edgeDistance = Math.max(Math.abs(localTheta), Math.abs(localPhi));
    const foldEnvelope = THREE.MathUtils.clamp(1 - (edgeDistance * edgeDistance), 0, 1);
    const localFold = profile.foldReliefMm * foldEnvelope *
        Math.sin((angles.theta * 12.2) + (angles.phi * 7.4) + (structureId.length * 0.37));
    return surface.addScaledVector(normal, profile.surfaceOffsetMm + localFold);
}

function corticalTerritoryAngles(profile, center, localTheta, localPhi) {
    const warped = warpTerritoryCoordinates(profile.shape, localTheta, localPhi);
    const rotation = THREE.MathUtils.degToRad(profile.rotationDeg);
    const cos = Math.cos(rotation);
    const sin = Math.sin(rotation);
    const thetaOffset = ((warped.theta * cos) - (warped.phi * sin)) * profile.halfTheta;
    const phiOffset = ((warped.theta * sin) + (warped.phi * cos)) * profile.halfPhi;
    return {
        theta: THREE.MathUtils.clamp(center.theta + profile.centerThetaOffset + thetaOffset, -1.5, 1.5),
        phi: THREE.MathUtils.clamp(center.phi + profile.centerPhiOffset + phiOffset, -1.46, 1.46)
    };
}

function findTerritoryBoundaryRadius(shape, angle) {
    const directionTheta = Math.cos(angle);
    const directionPhi = Math.sin(angle);
    let low = 0;
    let high = 1.55;
    for (let iteration = 0; iteration < 14; iteration += 1) {
        const candidate = (low + high) * 0.5;
        if (isInsideCorticalTerritory(shape, directionTheta * candidate, directionPhi * candidate)) {
            low = candidate;
        } else {
            high = candidate;
        }
    }
    return low;
}

function isInsideCorticalTerritory(shape, theta, phi) {
    const superellipse = (x, y, exponent) =>
        Math.pow(Math.abs(x), exponent) + Math.pow(Math.abs(y), exponent);
    if (Math.abs(theta) > 1.08 || Math.abs(phi) > 1.08) {
        return false;
    }

    switch (shape) {
        case 'VerticalStrip':
            return superellipse(theta / (0.78 + (0.1 * (1 - Math.abs(phi)))), phi, 2.8) <= 1;
        case 'HorizontalStrip':
            return superellipse(theta, phi / (0.72 + (0.14 * (1 - Math.abs(theta)))), 2.7) <= 1;
        case 'Crescent':
            return superellipse(theta, phi + (0.23 * ((theta * theta) - 0.35)), 2.45) <= 1;
        case 'Triangular':
            return phi >= -1 && phi <= 1 && Math.abs(theta) <= (0.34 + (0.58 * ((1 - phi) * 0.5)));
        case 'MedialBand':
            return superellipse(theta, phi / 0.7, 2.65) <= 1;
        case 'VentralBand':
            return superellipse(theta, (phi + (0.12 * theta)) / 0.64, 2.75) <= 1;
        case 'OccipitalBelt':
            return superellipse(theta, (phi + (0.18 * theta) + (0.1 * theta * theta)) / 0.78, 2.55) <= 1;
        case 'TwinLobule':
            return (Math.pow((theta + 0.32) / 0.7, 2) + Math.pow(phi / 0.88, 2) <= 1) ||
                (Math.pow((theta - 0.34) / 0.72, 2) + Math.pow((phi + 0.05) / 0.84, 2) <= 1);
        default:
            return superellipse(theta, phi, 2.45) <= 1;
    }
}

function warpTerritoryCoordinates(shape, theta, phi) {
    switch (shape) {
        case 'Crescent':
            return { theta, phi: phi + (0.23 * ((theta * theta) - 0.35)) };
        case 'Triangular':
            return { theta: theta * (0.82 - (0.12 * phi)), phi };
        case 'MedialBand':
            return { theta, phi: phi * 0.72 };
        case 'VentralBand':
            return { theta, phi: (phi * 0.66) - (0.12 * theta) };
        case 'OccipitalBelt':
            return { theta, phi: (phi * 0.78) - (0.18 * theta) - (0.1 * theta * theta) };
        case 'TwinLobule':
            return { theta: theta + (0.08 * Math.sin(phi * Math.PI)), phi };
        default:
            return { theta, phi };
    }
}

function corticalSurfaceParameters(point) {
    const xMm = Math.max(0, Math.abs(point.x) - corticalDimensions.midlineGap);
    const yMm = point.y - corticalDimensions.verticalCenter;
    const zRadius = point.z >= 0
        ? corticalDimensions.anteriorRadius
        : corticalDimensions.posteriorRadius;
    const xNorm = xMm / corticalDimensions.halfWidth;
    const yNorm = yMm / corticalDimensions.halfHeight;
    const zNorm = point.z / zRadius;
    const equatorial = Math.sqrt((xNorm * xNorm) + (zNorm * zNorm));
    return {
        theta: Math.atan2(zNorm, Math.max(0.0001, xNorm)),
        phi: Math.atan2(yNorm, Math.max(0.0001, equatorial))
    };
}

function corticalSurfaceNormal(theta, phi, hemisphereSign) {
    const epsilon = 0.004;
    const thetaLow = corticalSurfacePoint(theta - epsilon, phi, hemisphereSign);
    const thetaHigh = corticalSurfacePoint(theta + epsilon, phi, hemisphereSign);
    const phiLow = corticalSurfacePoint(theta, phi - epsilon, hemisphereSign);
    const phiHigh = corticalSurfacePoint(theta, phi + epsilon, hemisphereSign);
    const tangentTheta = thetaHigh.sub(thetaLow);
    const tangentPhi = phiHigh.sub(phiLow);
    const normal = tangentTheta.cross(tangentPhi);
    if (normal.lengthSq() < 0.000001) {
        return new THREE.Vector3(hemisphereSign, 0, 0);
    }
    if ((normal.x * hemisphereSign) < 0) {
        normal.multiplyScalar(-1);
    }
    return normal.normalize();
}

function corticalSurfacePoint(theta, phi, hemisphereSign) {
    theta = THREE.MathUtils.clamp(theta, -Math.PI / 2, Math.PI / 2);
    phi = THREE.MathUtils.clamp(phi, -Math.PI / 2, Math.PI / 2);

    const cosPhi = Math.cos(phi);
    const lateral = Math.max(0, cosPhi * Math.cos(theta));
    const vertical = Math.sin(phi);
    const longitudinal = cosPhi * Math.sin(theta);
    const anterior = Math.max(0, longitudinal);
    const posterior = Math.max(0, -longitudinal);
    const superior = Math.max(0, vertical);
    const inferior = Math.max(0, -vertical);
    const lateralShoulder = Math.pow(lateral, 0.72);
    const bell = (value, center, variance) =>
        Math.exp(-Math.pow(value - center, 2) / Math.max(0.001, variance));

    const frontalLobe = Math.pow(anterior, 1.18);
    const parietalCrown = bell(longitudinal, -0.08, 0.24) * Math.pow(superior, 1.15);
    const temporalRoot = bell(longitudinal, -0.1, 0.42) * bell(vertical, -0.43, 0.2) *
        (0.2 + (0.8 * lateralShoulder));
    const temporalTongue = bell(longitudinal, 0.18, 0.56) * bell(vertical, -0.57, 0.13) *
        (0.1 + (0.9 * Math.pow(lateralShoulder, 1.18)));
    const temporalPole = bell(longitudinal, 0.66, 0.1) * bell(vertical, -0.49, 0.12) *
        (0.2 + (0.8 * Math.pow(lateralShoulder, 1.08)));
    const temporalMedialRise = bell(longitudinal, 0.22, 0.52) * bell(vertical, -0.56, 0.2) *
        Math.pow(1 - lateralShoulder, 1.45);
    const orbitalShelf = bell(longitudinal, 0.66, 0.24) * bell(vertical, -0.35, 0.12) *
        (0.42 + (0.58 * lateralShoulder));
    const frontotemporalNotch = bell(longitudinal, 0.68, 0.18) * bell(vertical, -0.47, 0.022) *
        THREE.MathUtils.smoothstep(lateralShoulder, 0.12, 0.82);
    const occipitalLobe = Math.pow(posterior, 1.15);

    const widthRadius = corticalDimensions.halfWidth *
        (1 + (0.035 * frontalLobe) + (0.022 * orbitalShelf) + (0.09 * temporalRoot) +
            (0.135 * temporalTongue) + (0.065 * temporalPole) - (0.06 * occipitalLobe));
    let x = hemisphereSign * (corticalDimensions.midlineGap + (lateral * widthRadius));
    let y = corticalDimensions.verticalCenter + (vertical * corticalDimensions.halfHeight);
    y += 3.6 * frontalLobe * (0.3 + (0.7 * superior));
    y += 2.4 * parietalCrown;
    y -= 8.5 * temporalRoot;
    y -= 13.5 * temporalTongue;
    y -= 6.5 * temporalPole;
    y -= 1.8 * Math.pow(superior, 4);
    y += 1.4 * Math.pow(inferior, 5);

    const shelfTarget = -15.5 - (0.6 * lateralShoulder);
    const shelfBlend = 0.5 * THREE.MathUtils.smoothstep(orbitalShelf, 0.01, 0.92);
    y = (y * (1 - shelfBlend)) + (shelfTarget * shelfBlend);
    y += 9 * temporalMedialRise * (1 - Math.min(0.85, orbitalShelf));

    const longitudinalRadius = longitudinal >= 0
        ? corticalDimensions.anteriorRadius
        : corticalDimensions.posteriorRadius;
    let z = longitudinal * longitudinalRadius;
    z += 1.8 * frontalLobe;
    z -= 1.2 * occipitalLobe;
    z += 8 * orbitalShelf;
    z -= 1.2 * frontotemporalNotch;
    z += temporalRoot;
    z += 3.5 * temporalTongue;
    z += 4.5 * temporalPole;

    const normalizedLongitudinal = THREE.MathUtils.clamp((longitudinal + 0.72) / 1.42, 0, 1);
    const sylvianVertical = 0.07 - (0.27 * normalizedLongitudinal);
    const sylvianFissure = bell(vertical, sylvianVertical, 0.012) * bell(longitudinal, 0.02, 0.46) *
        Math.pow(lateralShoulder, 2.15);
    x = hemisphereSign * Math.max(corticalDimensions.midlineGap, Math.abs(x) - (1.25 * sylvianFissure));
    y -= 0.4 * sylvianFissure;
    x = hemisphereSign * Math.max(corticalDimensions.midlineGap, Math.abs(x) - (0.5 * frontotemporalNotch));
    return new THREE.Vector3(x, y, z);
}

function addGridIndices(indices, columns, rows, reverseWinding) {
    for (let row = 0; row < rows - 1; row += 1) {
        for (let column = 0; column < columns - 1; column += 1) {
            const a = (row * columns) + column;
            const b = a + 1;
            const c = a + columns;
            const d = c + 1;
            if (reverseWinding) {
                indices.push(a, d, b, a, c, d);
            } else {
                indices.push(a, b, d, a, d, c);
            }
        }
    }
}

function translucentMaterial(color, opacity) {
    return new THREE.MeshPhysicalMaterial({
        color,
        transparent: true,
        opacity,
        depthWrite: false,
        roughness: 0.76,
        metalness: 0,
        side: THREE.DoubleSide
    });
}

function buildStructureList(state) {
    const list = document.getElementById('structureList');
    const count = document.getElementById('structureCount');
    const search = document.getElementById('structureSearch');
    const definitions = [...state.definitionsById.values()];
    count.textContent = String(definitions.length);

    const render = () => {
        const query = normalizeId(search.value);
        const filtered = definitions.filter(definition =>
            normalizeId(`${definition.displayName} ${definition.structureId} ${definition.group}`).includes(query));
        const groups = new Map();
        for (const definition of filtered) {
            if (!groups.has(definition.group)) {
                groups.set(definition.group, []);
            }
            groups.get(definition.group).push(definition);
        }

        const fragment = document.createDocumentFragment();
        for (const groupName of [...groups.keys()].sort()) {
            const label = document.createElement('div');
            label.className = 'structure-group-label';
            label.textContent = groupName;
            fragment.appendChild(label);

            const groupDefinitions = groups.get(groupName)
                .sort((left, right) => left.displayName.localeCompare(right.displayName));
            for (const definition of groupDefinitions) {
                const button = document.createElement('button');
                button.type = 'button';
                button.className = 'structure-row';
                button.dataset.structureId = definition.structureId;
                button.setAttribute('role', 'option');
                button.innerHTML =
                    `<span class="structure-swatch" style="background:${escapeAttribute(definition.color)}"></span>` +
                    `<span class="structure-label">${escapeHtml(definition.displayName)}</span>` +
                    '<span class="structure-health">idle</span>';
                button.addEventListener('click', () => selectStructureById(state, definition.structureId));
                fragment.appendChild(button);
            }
        }
        list.replaceChildren(fragment);
        syncListSelection(state);
    };

    search.addEventListener('input', render);
    state.cleanup.push(() => search.removeEventListener('input', render));
    render();
}

function bindControls(state) {
    for (const button of document.querySelectorAll('[data-editor-view]')) {
        const handler = () => applyView(state, button.dataset.editorView);
        button.addEventListener('click', handler);
        state.cleanup.push(() => button.removeEventListener('click', handler));
    }

    for (const button of document.querySelectorAll('[data-editor-action]')) {
        const handler = () => {
            if (button.dataset.editorAction === 'reset' || button.dataset.editorAction === 'fit') {
                applyView(state, 'anterior');
            }
        };
        button.addEventListener('click', handler);
        state.cleanup.push(() => button.removeEventListener('click', handler));
    }

    for (const button of document.querySelectorAll('[data-editor-mode]')) {
        const handler = () => {
            state.mode = button.dataset.editorMode;
            document.querySelectorAll('[data-editor-mode]').forEach(candidate =>
                candidate.classList.toggle('active', candidate === button));
            applyDisplayMode(state);
        };
        button.addEventListener('click', handler);
        state.cleanup.push(() => button.removeEventListener('click', handler));
    }

    const opacity = document.getElementById('shellOpacity');
    const opacityValue = document.getElementById('shellOpacityValue');
    const opacityHandler = () => {
        state.shellOpacity = Number(opacity.value) / 100;
        opacityValue.value = `${opacity.value}%`;
        state.corticalShells.forEach(shell => { shell.material.opacity = state.shellOpacity; });
    };
    opacity.addEventListener('input', opacityHandler);
    state.cleanup.push(() => opacity.removeEventListener('input', opacityHandler));

    const pathways = document.getElementById('showPathways');
    const pathwaysHandler = () => {
        state.showPathways = pathways.checked;
        state.pathwayGroup.visible = state.showPathways;
    };
    pathways.addEventListener('change', pathwaysHandler);
    state.cleanup.push(() => pathways.removeEventListener('change', pathwaysHandler));
}

function bindPicking(state) {
    const canvas = state.renderer.domElement;
    const pointerHandler = event => {
        const bounds = canvas.getBoundingClientRect();
        state.pointer.x = ((event.clientX - bounds.left) / bounds.width) * 2 - 1;
        state.pointer.y = -((event.clientY - bounds.top) / bounds.height) * 2 + 1;
        state.raycaster.setFromCamera(state.pointer, state.camera);
        const hit = state.raycaster.intersectObjects(state.structureMeshes, false)[0]?.object ?? null;
        state.hovered = hit;
        canvas.style.cursor = hit ? 'pointer' : 'grab';
    };
    const leaveHandler = () => {
        state.hovered = null;
        canvas.style.cursor = 'grab';
    };
    const clickHandler = () => {
        if (state.hovered) {
            selectMesh(state, state.hovered);
        }
    };
    canvas.addEventListener('pointermove', pointerHandler);
    canvas.addEventListener('pointerleave', leaveHandler);
    canvas.addEventListener('click', clickHandler);
    state.cleanup.push(() => canvas.removeEventListener('pointermove', pointerHandler));
    state.cleanup.push(() => canvas.removeEventListener('pointerleave', leaveHandler));
    state.cleanup.push(() => canvas.removeEventListener('click', clickHandler));
}

function selectStructureById(state, structureId) {
    const candidates = state.meshesByStructure.get(normalizeId(structureId));
    if (!candidates?.length) {
        return;
    }
    const preferred = candidates.find(mesh => mesh.userData.structure.hemisphere === 'L') ?? candidates[0];
    selectMesh(state, preferred);
}

function selectMesh(state, mesh) {
    state.selected = mesh;
    const structure = mesh.userData.structure;
    const emptySelection = document.getElementById('selectionEmpty');
    emptySelection.hidden = true;
    emptySelection.style.display = 'none';
    document.getElementById('selectionDetails').hidden = false;
    document.getElementById('selectionHemisphere').textContent =
        structure.hemisphere === 'M' ? 'Midline structure' : `${structure.hemisphere === 'L' ? 'Left' : 'Right'} hemisphere`;
    document.getElementById('selectionName').textContent = structure.displayName;
    document.getElementById('selectionId').textContent = structure.structureId;
    document.getElementById('selectionModel').textContent = structure.neuronModel;
    document.getElementById('selectionPlasticity').textContent = structure.plasticity;
    document.getElementById('selectionSource').textContent = structure.source;
    updateSelectionTelemetry(state);
    syncListSelection(state);

    const target = mesh.userData.focusPoint.clone();
    const direction = state.camera.position.clone().sub(state.controls.target).normalize();
    state.controls.target.copy(target);
    state.camera.position.copy(target.clone().add(direction.multiplyScalar(155)));
    state.controls.update();
}

function syncListSelection(state) {
    const selectedId = state.selected?.userData?.structure?.structureId;
    document.querySelectorAll('.structure-row').forEach(row => {
        const selected = selectedId && normalizeId(row.dataset.structureId) === normalizeId(selectedId);
        row.classList.toggle('selected', Boolean(selected));
        row.setAttribute('aria-selected', selected ? 'true' : 'false');
    });
}

function applyView(state, name) {
    const preset = viewPresets[name] ?? viewPresets.anterior;
    state.currentView = name in viewPresets ? name : 'anterior';
    state.camera.position.fromArray(preset.position);
    state.camera.up.fromArray(preset.up);
    state.controls.target.fromArray(preset.target);
    updateCameraProjection(state);
    updateOrientationLabels(state.currentView);
    document.querySelectorAll('[data-editor-view]').forEach(button =>
        button.classList.toggle('active', button.dataset.editorView === state.currentView));
    state.controls.update();
}

function applyDisplayMode(state) {
    for (const mesh of state.structureMeshes) {
        const baseOpacity = mesh.userData.baseOpacity;
        const activity = mesh.userData.activity;
        mesh.material.opacity = state.mode === 'activity'
            ? Math.min(0.9, 0.08 + (activity * 0.78))
            : baseOpacity;
        mesh.material.emissiveIntensity = state.mode === 'activity'
            ? 0.12 + (activity * 2.4)
            : 0.18;
    }
}

async function pollFrame(state) {
    state.frameAbort?.abort();
    state.frameAbort = new AbortController();
    const timeout = window.setTimeout(() => state.frameAbort?.abort(), 14000);
    try {
        const response = await fetch('/editor/api/frame', {
            cache: 'no-store',
            credentials: 'same-origin',
            signal: state.frameAbort.signal
        });
        if (!response.ok) {
            throw new Error(`Telemetry gateway returned HTTP ${response.status}.`);
        }
        const frame = await response.json();
        state.lastFrameAt = Date.now();
        state.frameFailureCount = 0;
        applyFrame(state, frame);
        setRuntimeState(frame, 'online');
    } catch (error) {
        if (!state.disposed) {
            state.frameFailureCount += 1;
            const message = error instanceof Error ? error.message : String(error);
            const ageMs = state.lastFrameAt ? Date.now() - state.lastFrameAt : Number.POSITIVE_INFINITY;
            if (ageMs <= 20000) {
                setRuntimeDelayed(message, ageMs);
            } else {
                setRuntimeOffline(message);
            }
        }
    } finally {
        window.clearTimeout(timeout);
    }
}

function applyFrame(state, frame) {
    const runtime = value(frame, 'state') ?? {};
    const snapshot = value(frame, 'latestSnapshot') ?? {};
    const structureStates = value(snapshot, 'structureStates') ?? [];
    const dispatchSpikes = value(frame, 'dispatchSpikes') ?? [];
    const outputLog = value(frame, 'outputLog') ?? [];
    const dispatchActivity = new Map();

    if (Array.isArray(dispatchSpikes)) {
        for (const spike of dispatchSpikes) {
            const sourceId = resolveStructureId(state, value(spike, 'sourceStructure') ?? value(spike, 'source'));
            const targetId = resolveStructureId(state, value(spike, 'targetStructure') ?? value(spike, 'target'));
            if (sourceId) {
                addDispatchActivity(
                    dispatchActivity,
                    sourceId,
                    value(spike, 'sourceHemisphere'),
                    1.0);
            }
            if (targetId) {
                addDispatchActivity(
                    dispatchActivity,
                    targetId,
                    value(spike, 'targetHemisphere'),
                    0.45);
            }
        }
    }

    for (const mesh of state.structureMeshes) {
        mesh.userData.activity = 0;
        mesh.userData.meanRateHz = 0;
        mesh.userData.spikeOut = 0;
        mesh.userData.laminarDiagnostics = null;
    }

    if (Array.isArray(structureStates)) {
        for (const structureState of structureStates) {
            const rawStructureId = value(structureState, 'structureId') ?? value(structureState, 'id');
            const structureId = resolveStructureId(state, rawStructureId);
            if (!structureId) {
                continue;
            }
            const meanRate = firstNumberValue(structureState, 'meanFiringRateHz', 'meanRateHz');
            const spikeOut = firstNumberValue(structureState, 'spikeOutCount', 'spikeOut');
            const spikeIn = firstNumberValue(structureState, 'spikeInCount', 'spikeIn');
            const activeNeurons = firstNumberValue(structureState, 'activeNeuronCount');
            const laminarDiagnostics = value(structureState, 'corticalLaminarDiagnostics') ?? null;
            const structureKey = normalizeId(structureId);
            const previous = state.structureCounters.get(structureKey);
            const spikeDelta = previous
                ? Math.max(0, spikeOut - previous.spikeOut) + Math.max(0, spikeIn - previous.spikeIn)
                : 0;
            state.structureCounters.set(structureKey, { spikeOut, spikeIn });
            const baselineActivity = Math.min(
                1,
                (meanRate / 8) +
                (activeNeurons / 24) +
                (Math.min(48, spikeDelta) / 64));
            for (const mesh of state.meshesByStructure.get(structureKey) ?? []) {
                const recentDispatches = dispatchActivityForMesh(dispatchActivity, structureKey, mesh);
                mesh.userData.activity = Math.min(
                    1,
                    baselineActivity + (Math.min(12, recentDispatches) / 12));
                mesh.userData.meanRateHz = meanRate;
                mesh.userData.spikeOut = spikeOut;
                mesh.userData.laminarDiagnostics = laminarDiagnostics;
            }
        }
    }

    for (const [dispatchKey, recentDispatches] of dispatchActivity) {
        const [structureKey, hemisphere] = dispatchKey.split('|');
        const dispatchLevel = Math.min(1, recentDispatches / 8);
        for (const mesh of meshesForStructureHemisphere(state, structureKey, hemisphere)) {
            mesh.userData.activity = Math.max(mesh.userData.activity, dispatchLevel);
        }
    }

    applyDisplayMode(state);
    updateStructureHealthRows(state);
    updateSelectionTelemetry(state);
    updatePathways(state, dispatchSpikes);

    setText('runtimeTick', integerValue(runtime, 'tick').toLocaleString());
    setText('runtimeServices', integerValue(runtime, 'serviceCount').toLocaleString());
    setText('simulationClock', `${numberValue(runtime, 'simulationClockMs').toFixed(1)} ms`);
    setText('snapshotTick', integerValue(snapshot, 'tick').toLocaleString());
    setText('nonOkServices', countNonOk(runtime).toLocaleString());
    setText('dispatchCount', (Array.isArray(dispatchSpikes) ? dispatchSpikes.length : 0).toLocaleString());
    setText('telemetryAge', 'live');
    setText('telemetryLog', formatLog(outputLog));
}

function updatePathways(state, traces) {
    for (const child of [...state.pathwayGroup.children]) {
        child.geometry.dispose();
        child.material.dispose();
        state.pathwayGroup.remove(child);
    }
    if (!state.showPathways || !Array.isArray(traces)) {
        return;
    }

    const unique = new Map();
    for (const trace of traces.slice(-90)) {
        const sourceId = resolveStructureId(state, value(trace, 'sourceStructure') ?? value(trace, 'source'));
        const targetId = resolveStructureId(state, value(trace, 'targetStructure') ?? value(trace, 'target'));
        if (!sourceId || !targetId) {
            continue;
        }
        const sourceHemisphere = normalizeHemisphere(value(trace, 'sourceHemisphere'));
        const targetHemisphere = normalizeHemisphere(value(trace, 'targetHemisphere'));
        const pathwayKey =
            `${normalizeId(sourceId)}:${sourceHemisphere ?? '*'}>` +
            `${normalizeId(targetId)}:${targetHemisphere ?? '*'}`;
        unique.set(pathwayKey, { sourceId, sourceHemisphere, targetId, targetHemisphere });
    }

    for (const { sourceId, sourceHemisphere, targetId, targetHemisphere } of unique.values()) {
        const source = meshesForStructureHemisphere(state, sourceId, sourceHemisphere)[0];
        const target = meshesForStructureHemisphere(state, targetId, targetHemisphere)[0];
        if (!source || !target) {
            continue;
        }
        const sourcePoint = source.userData.focusPoint;
        const targetPoint = target.userData.focusPoint;
        const midpoint = sourcePoint.clone().lerp(targetPoint, 0.5);
        midpoint.y += 10 + (sourcePoint.distanceTo(targetPoint) * 0.08);
        const curve = new THREE.QuadraticBezierCurve3(sourcePoint, midpoint, targetPoint);
        const geometry = new THREE.BufferGeometry().setFromPoints(curve.getPoints(22));
        const material = new THREE.LineBasicMaterial({
            color: 0x73d7c4,
            transparent: true,
            opacity: 0.3,
            depthWrite: false
        });
        state.pathwayGroup.add(new THREE.Line(geometry, material));
    }
}

function updateStructureHealthRows(state) {
    document.querySelectorAll('.structure-row').forEach(row => {
        const meshes = state.meshesByStructure.get(normalizeId(row.dataset.structureId)) ?? [];
        const activity = meshes.reduce((maximum, mesh) => Math.max(maximum, mesh.userData.activity), 0);
        const health = row.querySelector('.structure-health');
        if (activity > 0.45) {
            health.textContent = 'active';
            health.style.color = '#8fddcf';
        } else if (activity > 0.05) {
            health.textContent = 'signal';
            health.style.color = '#e3b35a';
        } else {
            health.textContent = 'quiet';
            health.style.color = '';
        }
    });
}

function updateSelectionTelemetry(state) {
    if (!state.selected) {
        return;
    }
    setText('selectionRate', `${state.selected.userData.meanRateHz.toFixed(2)} Hz`);
    setText('selectionSpikes', Math.round(state.selected.userData.spikeOut).toLocaleString());
    document.getElementById('selectionActivity').style.width =
        `${Math.round(state.selected.userData.activity * 100)}%`;
    updateLaminarTelemetry(state.selected.userData.laminarDiagnostics);
}

function updateLaminarTelemetry(diagnostics) {
    const panel = document.getElementById('selectionLaminar');
    const populationList = document.getElementById('selectionLaminarPopulations');
    if (!panel || !populationList) {
        return;
    }

    const populations = value(diagnostics, 'populations');
    if (!diagnostics || !Array.isArray(populations) || populations.length === 0) {
        panel.hidden = true;
        populationList.replaceChildren();
        return;
    }

    panel.hidden = false;
    setText(
        'selectionInhibitoryBalance',
        `${Math.round(numberValue(diagnostics, 'inhibitoryBalance') * 100)}% inhibition`);
    const rows = populations.map(population => {
        const row = document.createElement('div');
        row.className = 'laminar-population';
        row.title = String(value(population, 'role') ?? 'cortical population');

        const name = document.createElement('b');
        name.textContent = String(value(population, 'name') ?? 'population');
        const output = document.createElement('output');
        const rate = numberValue(population, 'meanFiringRateHz');
        const active = integerValue(population, 'activeNeuronCount');
        const count = integerValue(population, 'neuronCount');
        output.textContent = `${rate.toFixed(2)} Hz  ${active}/${count}`;
        row.append(name, output);
        return row;
    });
    populationList.replaceChildren(...rows);
}

function animateActivity(state) {
    const now = performance.now() * 0.004;
    for (const mesh of state.structureMeshes) {
        const selected = mesh === state.selected;
        const hovered = mesh === state.hovered;
        const activity = mesh.userData.activity;
        const pulse = activity > 0.02 ? 1 + (Math.sin(now + mesh.id) * activity * 0.045) : 1;
        const emphasis = selected ? 1.18 : hovered ? 1.08 : 1;
        const geometryEmphasis = mesh.userData.isCortical ? 1 : pulse * emphasis;
        mesh.scale.copy(mesh.userData.baseScale).multiplyScalar(geometryEmphasis);
        mesh.material.emissiveIntensity +=
            ((selected ? 1.6 : state.mode === 'activity' ? 0.12 + (activity * 2.4) : 0.18) -
                mesh.material.emissiveIntensity) * 0.12;
        mesh.userData.renderPulse = pulse * emphasis;
    }
}

function setRuntimeState(frame, stateName) {
    const runtime = value(frame, 'state') ?? {};
    const nonOk = countNonOk(runtime);
    const status = nonOk > 0 ? 'degraded' : stateName;
    const dot = document.getElementById('runtimeStatusDot');
    dot.className = `status-dot ${status}`;
    setText('runtimeStatus', nonOk > 0 ? `${nonOk} service issue${nonOk === 1 ? '' : 's'}` : 'Engine online');
}

function setRuntimeOffline(message) {
    const dot = document.getElementById('runtimeStatusDot');
    dot.className = 'status-dot offline';
    setText('runtimeStatus', 'Engine unavailable');
    setText('telemetryAge', 'connection lost');
    setText('telemetryLog', message);
}

function setRuntimeDelayed(message, ageMs) {
    const dot = document.getElementById('runtimeStatusDot');
    dot.className = 'status-dot degraded';
    setText('runtimeStatus', 'Telemetry delayed');
    setText('telemetryAge', `${Math.max(1, Math.round(ageMs / 1000))}s ago`);
    setText('telemetryLog', message);
}

function updateFrameAge(state) {
    if (!state.lastFrameAt) {
        return;
    }
    const seconds = Math.max(0, Math.round((Date.now() - state.lastFrameAt) / 1000));
    setText('telemetryAge', seconds < 2 ? 'live' : `${seconds}s ago`);
}

function resize(state) {
    const width = Math.max(1, state.host.clientWidth);
    const height = Math.max(1, state.host.clientHeight);
    state.camera.aspect = width / height;
    updateCameraProjection(state);
    state.renderer.setSize(width, height, false);
}

function updateCameraProjection(state) {
    state.camera.updateProjectionMatrix();
    if (state.currentView === 'anterior') {
        state.camera.projectionMatrix.elements[0] *= -1;
        state.camera.projectionMatrixInverse.copy(state.camera.projectionMatrix).invert();
    }
}

function updateOrientationLabels(viewName) {
    const orientation = document.getElementById('anteriorOrientation');
    if (orientation) {
        orientation.hidden = viewName !== 'anterior';
    }
}

function countNonOk(runtime) {
    const telemetry = value(runtime, 'serviceTelemetry');
    if (!telemetry || typeof telemetry !== 'object') {
        return integerValue(runtime, 'nonOkServiceCount');
    }
    return Object.values(telemetry).filter(entry => {
        const status = String(value(entry, 'status') ?? value(entry, 'state') ?? '').toLowerCase();
        return status && status !== 'ok' && status !== 'healthy' && status !== 'running';
    }).length;
}

function formatLog(log) {
    if (!Array.isArray(log) || log.length === 0) {
        return 'No recent ControlProgram output.';
    }
    return log.slice(-8).map(entry => typeof entry === 'string' ? entry : JSON.stringify(entry)).join('\n');
}

function value(object, propertyName) {
    if (!object || typeof object !== 'object') {
        return undefined;
    }
    if (Object.prototype.hasOwnProperty.call(object, propertyName)) {
        return object[propertyName];
    }
    const pascalName = propertyName.charAt(0).toUpperCase() + propertyName.slice(1);
    return object[pascalName];
}

function numberValue(object, propertyName) {
    const parsed = Number(value(object, propertyName));
    return Number.isFinite(parsed) ? parsed : 0;
}

function firstNumberValue(object, ...propertyNames) {
    for (const propertyName of propertyNames) {
        const candidate = value(object, propertyName);
        if (candidate === undefined || candidate === null || candidate === '') {
            continue;
        }
        const parsed = Number(candidate);
        if (Number.isFinite(parsed)) {
            return parsed;
        }
    }
    return 0;
}

function integerValue(object, propertyName) {
    return Math.round(numberValue(object, propertyName));
}

function setText(id, text) {
    const element = document.getElementById(id);
    if (element) {
        element.textContent = text;
    }
}

function normalizeId(valueToNormalize) {
    return String(valueToNormalize ?? '').trim().toLowerCase().replace(/[^a-z0-9]/g, '');
}

function normalizeHemisphere(valueToNormalize) {
    const normalized = String(valueToNormalize ?? '').trim().toUpperCase();
    return normalized === 'L' || normalized === 'R' || normalized === 'M'
        ? normalized
        : null;
}

function dispatchActivityKey(structureId, hemisphere) {
    return `${normalizeId(structureId)}|${normalizeHemisphere(hemisphere) ?? '*'}`;
}

function addDispatchActivity(activity, structureId, hemisphere, amount) {
    const key = dispatchActivityKey(structureId, hemisphere);
    activity.set(key, (activity.get(key) ?? 0) + amount);
}

function meshesForStructureHemisphere(state, structureId, hemisphere) {
    const meshes = state.meshesByStructure.get(normalizeId(structureId)) ?? [];
    const normalizedHemisphere = normalizeHemisphere(hemisphere);
    if (!normalizedHemisphere) {
        return meshes;
    }
    const matching = meshes.filter(mesh =>
        normalizeHemisphere(mesh.userData.structure.hemisphere) === normalizedHemisphere);
    return matching.length > 0 ? matching : meshes;
}

function dispatchActivityForMesh(activity, structureId, mesh) {
    const hemisphere = mesh.userData.structure.hemisphere;
    return (activity.get(dispatchActivityKey(structureId, hemisphere)) ?? 0) +
        (activity.get(dispatchActivityKey(structureId, null)) ?? 0);
}

function resolveStructureId(state, structureId) {
    if (typeof structureId === 'number' && Number.isInteger(structureId)) {
        return normalizeId(state.structureIdByProtocol.get(structureId));
    }
    const numericId = Number(structureId);
    if (String(structureId ?? '').trim() !== '' && Number.isInteger(numericId)) {
        const mapped = state.structureIdByProtocol.get(numericId);
        if (mapped) {
            return normalizeId(mapped);
        }
    }
    return normalizeId(structureId);
}

function escapeHtml(valueToEscape) {
    return String(valueToEscape)
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}

function escapeAttribute(valueToEscape) {
    return escapeHtml(valueToEscape).replaceAll('`', '&#096;');
}
