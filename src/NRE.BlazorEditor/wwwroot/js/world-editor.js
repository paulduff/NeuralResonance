import * as THREE from '../vendor/three/three.module.min.js';
import { OrbitControls } from '../vendor/three/addons/controls/OrbitControls.js';

const WORLD_SIZE = 132;
const VISUAL_SUBDIVISIONS = 4;
const VISUAL_VOXEL_SIZE = 1 / VISUAL_SUBDIVISIONS;
const HEIGHT_UNITS_PER_METER = 4;
const TERRAIN_HEIGHT_UNIT = 0.25;
const TERRAIN_HALF_HEIGHT_UNIT = TERRAIN_HEIGHT_UNIT * 0.5;
const MINIMUM_TERRAIN_HEIGHT = 1 * HEIGHT_UNITS_PER_METER;
const MAXIMUM_TERRAIN_HEIGHT = 18 * HEIGHT_UNITS_PER_METER;
const SEA_LEVEL_METERS = 3;
const SEA_LEVEL_HEIGHT_UNITS = SEA_LEVEL_METERS * HEIGHT_UNITS_PER_METER;
const CLIFF_THRESHOLD_HEIGHT_UNITS = 4;
const SHELTER_FOUNDATION_HALF_EXTENT = 4.55;
const SHELTER_ENTRANCE_HALF_WIDTH = 1.75;
const SHELTER_ENTRANCE_START = 3.45;
const SHELTER_ENTRANCE_END = 8.0;
const SHELTER_GRADE_WIDTH = 2.5;
const NOMINAL_ENERGY_JOULES = 8_000_000;
const WORLD_MAX_FORWARD_SPEED = 1.8;
const PREVIEW_SEED = 317;
const JOINT_LIMITS = Object.freeze({
    shoulder: [-0.70, 2.62],
    shoulderAbduction: [-0.18, 2.45],
    elbow: [0, 2.62],
    hip: [-0.35, 2.09],
    hipAbduction: [-0.45, 0.78],
    knee: [0, 2.45],
    ankle: [-0.78, 0.52],
    ankleRoll: [-0.26, 0.52],
    neckYaw: [-1.35, 1.35],
    neckPitch: [-0.78, 0.95]
});

let world = null;

export function mountWorld() {
    disposeWorld();
    const host = document.getElementById('worldViewport');
    if (!host) {
        return;
    }

    world = createWorld(host);
    world.start();
}

export function disposeWorld() {
    world?.dispose();
    world = null;
}

export function getWorldDiagnostics() {
    if (!world?.diagnostics) {
        return null;
    }
    return world.diagnostics();
}

function createWorld(host) {
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x9fcbd5);
    scene.fog = new THREE.Fog(0xa8cbd0, 58, 210);

    const camera = new THREE.PerspectiveCamera(48, 1, 0.1, 520);
    camera.position.set(17, 13, 25);

    const renderer = new THREE.WebGLRenderer({
        antialias: true,
        alpha: false,
        powerPreference: 'high-performance'
    });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.18;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFShadowMap;
    host.replaceChildren(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.075;
    controls.minDistance = 5;
    controls.maxDistance = 215;
    controls.maxPolarAngle = Math.PI * 0.49;
    controls.target.set(0, 1.1, 6);

    const environment = new THREE.Group();
    const habitats = new THREE.Group();
    const entities = new THREE.Group();
    const avatar = createAvatar();
    const trail = createTrail();
    scene.add(environment, habitats, entities, trail.line, avatar.root);

    addSky(scene);
    const ambient = new THREE.HemisphereLight(0xe5f8ff, 0x607454, 2.45);
    scene.add(ambient);
    const sun = new THREE.DirectionalLight(0xfff1ce, 3.55);
    sun.position.set(-58, 92, 46);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    sun.shadow.camera.left = -78;
    sun.shadow.camera.right = 78;
    sun.shadow.camera.top = 78;
    sun.shadow.camera.bottom = -78;
    sun.shadow.camera.near = 1;
    sun.shadow.camera.far = 220;
    sun.shadow.bias = -0.0008;
    scene.add(sun);

    const state = {
        host,
        scene,
        camera,
        renderer,
        controls,
        environment,
        habitats,
        entities,
        avatar,
        trail,
        ambient,
        sun,
        seed: PREVIEW_SEED,
        heights: null,
        shelterSites: [],
        waterMaterials: [],
        entityRoots: new Map(),
        active: false,
        cameraMode: 'orbit',
        avatarMode: 'body',
        atmosphere: true,
        targetPosition: new THREE.Vector3(0, 0, 6),
        targetHeading: 180,
        currentHeading: 180,
        motorDrive: {
            left: 0, right: 0, manipulator: 0,
            leftHipCoronal: 0, rightHipCoronal: 0,
            leftAnkleSagittal: 0, rightAnkleSagittal: 0,
            leftAnkleCoronal: 0, rightAnkleCoronal: 0,
            trunkYaw: 0,
            headYaw: 0, headPitch: 0,
            stand: 0, crouch: 0, sit: 0, lie: 0
        },
        motion: { forwardSpeed: 0, verticalVelocity: 0, grounded: true },
        articulation: {
            leftHip: 0, rightHip: 0,
            leftHipAbduction: 0, rightHipAbduction: 0,
            leftKnee: 0, rightKnee: 0,
            leftAnkle: 0, rightAnkle: 0,
            leftAnkleRoll: 0, rightAnkleRoll: 0,
            leftFootLoad: 0, rightFootLoad: 0,
            leftFootPressure: { heelMedial: 0, heelLateral: 0, forefootMedial: 0, forefootLateral: 0 },
            rightFootPressure: { heelMedial: 0, heelLateral: 0, forefootMedial: 0, forefootLateral: 0 },
            leftShoulder: 0, rightShoulder: 0,
            leftShoulderAbduction: 0, rightShoulderAbduction: 0,
            leftElbow: 0, rightElbow: 0,
            manipulatorExtension: 0,
            trunkPitch: 0, trunkRoll: 0, trunkYaw: 0,
            neckYaw: 0, neckPitch: 0,
            supportPlaneOffset: 0,
            posture: 'standing', bodyHeight: 1.74,
            upright: 1, support: 0, balanceError: 0,
            balance: {
                phase: 'stable', margin: 0,
                centerOfMassX: 0, centerOfMassY: 0.94, centerOfMassZ: 0,
                centerOfPressureX: 0, centerOfPressureZ: 0,
                extrapolatedCenterOfMassX: 0, extrapolatedCenterOfMassZ: 0,
                fallPitch: 0, fallRoll: 0,
                fallPitchVelocity: 0, fallRollVelocity: 0
            },
            muscles: []
        },
        previousTarget: new THREE.Vector3(Number.NaN, Number.NaN, Number.NaN),
        walkPhase: 0,
        lastFrameTime: performance.now(),
        frameTimer: 0,
        disposed: false,
        cleanup: [],
        resizeObserver: null,
        lastStateAt: 0,
        entitySignature: '',
        hasFramedAvatar: false,
        commandInFlight: false
    };

    rebuildEnvironment(state, PREVIEW_SEED);
    setPreviewEntities(state);
    bindWorkspace(state);
    bindWorldControls(state);
    resize(state);
    state.resizeObserver = new ResizeObserver(() => resize(state));
    state.resizeObserver.observe(host);
    window.lucide?.createIcons({ attrs: { 'aria-hidden': 'true' } });

    return {
        start() {
            renderer.setAnimationLoop(now => animate(state, now));
            pollWorldState(state);
            scheduleWorldPoll(state);
        },
        dispose() {
            state.disposed = true;
            window.clearTimeout(state.frameTimer);
            state.resizeObserver?.disconnect();
            state.cleanup.forEach(cleanup => cleanup());
            renderer.setAnimationLoop(null);
            controls.dispose();
            disposeChildren(scene);
            renderer.dispose();
            host.replaceChildren();
        },
        diagnostics() {
            return {
                target: state.targetPosition.toArray(),
                avatar: state.avatar.root.position.toArray(),
                terrainTop: terrainTopAt(state.heights, state.targetPosition.x, state.targetPosition.z),
                camera: state.camera.position.toArray(),
                cameraTarget: state.controls.target.toArray(),
                active: state.active,
                mode: state.avatarMode
            };
        }
    };
}

function addSky(scene) {
    const sky = new THREE.Mesh(
        new THREE.SphereGeometry(300, 32, 18),
        new THREE.ShaderMaterial({
            side: THREE.BackSide,
            depthWrite: false,
            uniforms: {
                topColor: { value: new THREE.Color(0x4e91b5) },
                horizonColor: { value: new THREE.Color(0xc5d9d6) },
                lowerColor: { value: new THREE.Color(0x839a86) }
            },
            vertexShader: `varying vec3 vPosition; void main() { vPosition = position; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
            fragmentShader: `
                varying vec3 vPosition;
                uniform vec3 topColor;
                uniform vec3 horizonColor;
                uniform vec3 lowerColor;
                void main() {
                    float h = normalize(vPosition).y;
                    vec3 lower = mix(lowerColor, horizonColor, smoothstep(-0.18, 0.08, h));
                    vec3 color = mix(lower, topColor, smoothstep(0.02, 0.78, h));
                    gl_FragColor = vec4(color, 1.0);
                }`
        })
    );
    scene.add(sky);

    const cloudMaterial = new THREE.MeshStandardMaterial({
        color: 0xf4f5ec,
        transparent: true,
        opacity: 0.45,
        roughness: 1,
        depthWrite: false
    });
    const cloudGeometry = new THREE.BoxGeometry(1, 1, 1);
    const clouds = new THREE.InstancedMesh(cloudGeometry, cloudMaterial, 54);
    const dummy = new THREE.Object3D();
    const random = mulberry32(6817);
    for (let i = 0; i < 54; i++) {
        const bank = Math.floor(i / 3);
        dummy.position.set(
            -92 + ((bank % 6) * 34) + (random() * 8),
            35 + (random() * 13),
            -76 + (Math.floor(bank / 6) * 72) + (random() * 10));
        dummy.scale.set(7 + random() * 8, 0.7 + random() * 1.2, 2.8 + random() * 4);
        dummy.rotation.y = random() * Math.PI;
        dummy.updateMatrix();
        clouds.setMatrixAt(i, dummy.matrix);
    }
    clouds.frustumCulled = false;
    clouds.userData.atmosphere = true;
    scene.add(clouds);
}

function rebuildEnvironment(state, seed) {
    disposeChildren(state.environment);
    state.environment.clear();
    state.waterMaterials.length = 0;
    state.seed = seed;
    state.heights = generateHeightMap(seed);
    state.shelterSites = buildPreviewShelterSites(seed);
    prepareShelterGround(state.heights, state.shelterSites);
    addFineVoxelTerrain(state);
    addWater(state);
    const floraCount = addTrees(state, seed + 991, state.shelterSites);
    addRockFormations(state, seed + 331, state.shelterSites);
    setText('worldSeed', String(seed));
    setText('floraCount', floraCount.toLocaleString());
    setText('terrainCellCount', (WORLD_SIZE * WORLD_SIZE * VISUAL_SUBDIVISIONS * VISUAL_SUBDIVISIONS).toLocaleString());

    const previewShelters = state.shelterSites.map(site => ({
        ...site,
        kind: 'shelter',
        y: terrainTopAt(state.heights, site.x, site.z)
    }));
    syncHabitats(state, previewShelters);
    const startY = terrainTopAt(state.heights, 0, 6) + 0.03;
    state.targetPosition.set(0, startY, 6);
    state.avatar.root.position.copy(state.targetPosition);
}

function generateHeightMap(seed) {
    const heights = Array.from({ length: WORLD_SIZE }, () => new Int16Array(WORLD_SIZE));
    const center = (WORLD_SIZE - 1) * 0.5;
    const maxRadius = WORLD_SIZE * 0.64;
    const mountains = [
        [-center * 0.55, -center * 0.25, WORLD_SIZE * 0.20, 5.6],
        [center * 0.42, center * 0.18, WORLD_SIZE * 0.18, 4.8],
        [0, center * 0.46, WORLD_SIZE * 0.16, 3.7]
    ];

    for (let x = 0; x < WORLD_SIZE; x++) {
        for (let z = 0; z < WORLD_SIZE; z++) {
            const wx = x - center;
            const wz = z - center;
            const radius = Math.hypot(wx, wz);
            const radialFalloff = clamp(radius / maxRadius, 0, 1);
            const n1 = fractalNoise((wx * 0.075) + (seed * 0.0013), (wz * 0.075) + (seed * 0.0021), 4, 0.55);
            const n2 = fractalNoise((wx * 0.19) + (seed * 0.0032), (wz * 0.19) + (seed * 0.0019), 3, 0.45);
            const ridge = Math.abs((n2 * 2) - 1);
            let sculpted = (n1 * 0.74) + ((1 - ridge) * 0.26) - (radialFalloff * 0.42);
            for (const [mx, mz, mountainRadius, gain] of mountains) {
                const distance = Math.hypot(wx - mx, wz - mz);
                if (distance <= mountainRadius) {
                    const t = 1 - (distance / mountainRadius);
                    sculpted += (t * t) * (gain / 10);
                }
            }
            const valleyRadius = WORLD_SIZE * 0.17;
            if (radius < valleyRadius) {
                sculpted -= (1 - (radius / valleyRadius)) * 0.25;
            }
            heights[x][z] = clamp(
                Math.round((1 + (sculpted * 10)) * HEIGHT_UNITS_PER_METER),
                MINIMUM_TERRAIN_HEIGHT,
                MAXIMUM_TERRAIN_HEIGHT);
        }
    }
    return heights;
}

function addFineVoxelTerrain(state) {
    const geometry = new THREE.BoxGeometry(
        VISUAL_VOXEL_SIZE * 0.985,
        TERRAIN_HEIGHT_UNIT,
        VISUAL_VOXEL_SIZE * 0.985);
    const palette = {
        seabed: 0xb7a274,
        shore: 0xd0bb82,
        grass: 0x78a45b,
        upland: 0x82926c,
        rock: 0x93988e,
        snow: 0xe1e7e5
    };
    const counts = Object.fromEntries(Object.keys(palette).map(key => [key, 0]));
    const half = (WORLD_SIZE - 1) * 0.5;
    for (let x = 0; x < WORLD_SIZE; x++) {
        for (let z = 0; z < WORLD_SIZE; z++) {
            const slope = localSlope(state.heights, x, z);
            for (let sx = 0; sx < VISUAL_SUBDIVISIONS; sx++) {
                for (let sz = 0; sz < VISUAL_SUBDIVISIONS; sz++) {
                    const worldX = visualVoxelCoordinate(x, sx, half);
                    const worldZ = visualVoxelCoordinate(z, sz, half);
                    const height = heightUnitsAtWorld(state.heights, worldX, worldZ);
                    counts[terrainCategory(height, slope)]++;
                }
            }
        }
    }
    const meshes = {};
    const indices = {};
    for (const [key, color] of Object.entries(palette)) {
        const material = new THREE.MeshLambertMaterial({ color, flatShading: true });
        const mesh = new THREE.InstancedMesh(geometry, material, counts[key]);
        mesh.receiveShadow = true;
        mesh.instanceMatrix.setUsage(THREE.StaticDrawUsage);
        meshes[key] = mesh;
        indices[key] = 0;
        state.environment.add(mesh);
    }
    const matrix = new THREE.Matrix4();
    const position = new THREE.Vector3();
    const scale = new THREE.Vector3();
    const quaternion = new THREE.Quaternion();

    for (let x = 0; x < WORLD_SIZE; x++) {
        for (let z = 0; z < WORLD_SIZE; z++) {
            const slope = localSlope(state.heights, x, z);
            for (let sx = 0; sx < VISUAL_SUBDIVISIONS; sx++) {
                for (let sz = 0; sz < VISUAL_SUBDIVISIONS; sz++) {
                    const worldX = visualVoxelCoordinate(x, sx, half);
                    const worldZ = visualVoxelCoordinate(z, sz, half);
                    const height = heightUnitsAtWorld(state.heights, worldX, worldZ);
                    const category = terrainCategory(height, slope);
                    const mesh = meshes[category];
                    position.set(
                        worldX,
                        (height * TERRAIN_HEIGHT_UNIT * 0.5) - TERRAIN_HALF_HEIGHT_UNIT,
                        worldZ);
                    scale.set(1, height, 1);
                    matrix.compose(position, quaternion, scale);
                    mesh.setMatrixAt(indices[category]++, matrix);
                }
            }
        }
    }
    Object.values(meshes).forEach(mesh => mesh.instanceMatrix.needsUpdate = true);
}

function terrainCategory(height, slope) {
    if (height < SEA_LEVEL_HEIGHT_UNITS) {
        return 'seabed';
    }
    if (height === SEA_LEVEL_HEIGHT_UNITS) {
        return 'shore';
    }
    if (height >= 13 * HEIGHT_UNITS_PER_METER) {
        return 'snow';
    }
    if (height >= 9 * HEIGHT_UNITS_PER_METER || slope >= 4 * HEIGHT_UNITS_PER_METER) {
        return 'rock';
    }
    return height >= 7 * HEIGHT_UNITS_PER_METER || slope >= 2 * HEIGHT_UNITS_PER_METER
        ? 'upland'
        : 'grass';
}

function addWater(state) {
    let count = 0;
    const half = (WORLD_SIZE - 1) * 0.5;
    for (let x = 0; x < WORLD_SIZE; x++) {
        for (let z = 0; z < WORLD_SIZE; z++) {
            for (let sx = 0; sx < VISUAL_SUBDIVISIONS; sx++) {
                for (let sz = 0; sz < VISUAL_SUBDIVISIONS; sz++) {
                    const worldX = visualVoxelCoordinate(x, sx, half);
                    const worldZ = visualVoxelCoordinate(z, sz, half);
                    if (heightUnitsAtWorld(state.heights, worldX, worldZ) < SEA_LEVEL_HEIGHT_UNITS) {
                        count++;
                    }
                }
            }
        }
    }
    const geometry = new THREE.BoxGeometry(VISUAL_VOXEL_SIZE * 0.98, 0.10, VISUAL_VOXEL_SIZE * 0.98);
    const material = new THREE.MeshPhysicalMaterial({
        color: 0x3f9dbc,
        transparent: true,
        opacity: 0.72,
        roughness: 0.17,
        metalness: 0.03,
        transmission: 0.08,
        clearcoat: 0.65,
        depthWrite: false
    });
    state.waterMaterials.push(material);
    const mesh = new THREE.InstancedMesh(geometry, material, count);
    mesh.receiveShadow = true;
    mesh.renderOrder = 2;
    mesh.userData.water = true;
    const dummy = new THREE.Object3D();
    let index = 0;
    for (let x = 0; x < WORLD_SIZE; x++) {
        for (let z = 0; z < WORLD_SIZE; z++) {
            for (let sx = 0; sx < VISUAL_SUBDIVISIONS; sx++) {
                for (let sz = 0; sz < VISUAL_SUBDIVISIONS; sz++) {
                    const worldX = visualVoxelCoordinate(x, sx, half);
                    const worldZ = visualVoxelCoordinate(z, sz, half);
                    if (heightUnitsAtWorld(state.heights, worldX, worldZ) >= SEA_LEVEL_HEIGHT_UNITS) {
                        continue;
                    }
                    dummy.position.set(
                        worldX,
                        SEA_LEVEL_METERS - 0.05,
                        worldZ);
                    dummy.updateMatrix();
                    mesh.setMatrixAt(index++, dummy.matrix);
                }
            }
        }
    }
    state.environment.add(mesh);
}

function addTrees(state, seed, shelterSites) {
    const trees = [];
    const half = (WORLD_SIZE - 1) * 0.5;
    for (let x = 2; x < WORLD_SIZE - 2; x++) {
        for (let z = 2; z < WORLD_SIZE - 2; z++) {
            const height = state.heights[x][z];
            if (height <= SEA_LEVEL_HEIGHT_UNITS + HEIGHT_UNITS_PER_METER) {
                continue;
            }
            const worldX = x - half;
            const worldZ = z - half;
            if (isInsideShelterClearance(worldX, worldZ, shelterSites)) {
                continue;
            }
            const placement = fractalNoise((x * 0.31) + (seed * 0.013), (z * 0.31) + (seed * 0.017), 2, 0.5);
            if (placement >= 0.81 && Math.hypot(x - half, z - half) > 8) {
                trees.push({ x: worldX, y: terrainTopAt(state.heights, worldX, worldZ), z: worldZ, size: 0.9 + placement * 0.35 });
            }
        }
    }

    const trunkGeometry = new THREE.CylinderGeometry(0.22, 0.30, 2.5, 7);
    const trunkMaterial = new THREE.MeshStandardMaterial({ color: 0x73533a, roughness: 0.96 });
    const canopyGeometry = new THREE.IcosahedronGeometry(1.18, 1);
    const canopyMaterial = new THREE.MeshStandardMaterial({ color: 0x397347, roughness: 0.92 });
    const trunks = new THREE.InstancedMesh(trunkGeometry, trunkMaterial, trees.length);
    const canopies = new THREE.InstancedMesh(canopyGeometry, canopyMaterial, trees.length);
    trunks.castShadow = true;
    trunks.receiveShadow = true;
    canopies.castShadow = true;
    const dummy = new THREE.Object3D();
    for (let index = 0; index < trees.length; index++) {
        const tree = trees[index];
        dummy.position.set(tree.x, tree.y + 1.25, tree.z);
        dummy.scale.set(tree.size, tree.size, tree.size);
        dummy.rotation.y = hash01(index, seed) * Math.PI;
        dummy.updateMatrix();
        trunks.setMatrixAt(index, dummy.matrix);
        dummy.position.y = tree.y + 3.0;
        dummy.scale.set(tree.size * 1.2, tree.size, tree.size * 1.2);
        dummy.updateMatrix();
        canopies.setMatrixAt(index, dummy.matrix);
    }
    state.environment.add(trunks, canopies);
    return trees.length;
}

function addRockFormations(state, seed, shelterSites) {
    const formations = [];
    const random = mulberry32(seed);
    for (let attempt = 0; attempt < 200 && formations.length < 20; attempt++) {
        const x = -61 + random() * 122;
        const z = -61 + random() * 122;
        const radius = 0.45 + random() * 0.8;
        const scaleY = radius * (0.65 + random() * 0.5);
        const rotationX = random();
        const rotationY = random() * Math.PI;
        const rotationZ = random();
        if (!isInsideShelterClearance(x, z, shelterSites)) {
            formations.push({ x, z, radius, scaleY, rotationX, rotationY, rotationZ });
        }
    }
    const geometry = new THREE.DodecahedronGeometry(1, 0);
    const material = new THREE.MeshStandardMaterial({ color: 0x74766e, roughness: 0.98 });
    const rocks = new THREE.InstancedMesh(geometry, material, formations.length);
    rocks.castShadow = true;
    rocks.receiveShadow = true;
    const dummy = new THREE.Object3D();
    for (let i = 0; i < formations.length; i++) {
        const formation = formations[i];
        const y = terrainTopAt(state.heights, formation.x, formation.z);
        dummy.position.set(formation.x, y + formation.radius * 0.55, formation.z);
        dummy.scale.set(formation.radius * 1.3, formation.scaleY, formation.radius);
        dummy.rotation.set(formation.rotationX, formation.rotationY, formation.rotationZ);
        dummy.updateMatrix();
        rocks.setMatrixAt(i, dummy.matrix);
    }
    state.environment.add(rocks);
}

function buildPreviewShelterSites(seed) {
    const shelters = [{ x: 0, z: 0, scale: 1 }];
    const random = mulberry32(seed + 4127);
    for (let i = 0; i < 11; i++) {
        const angle = (i / 11) * Math.PI * 2 + (random() - 0.5) * 0.24;
        const radius = 18 + (i % 3) * 10 + random() * 4;
        const x = Math.cos(angle) * radius;
        const z = Math.sin(angle) * radius;
        shelters.push({ x, z, scale: 0.78 });
    }
    return shelters;
}

function prepareShelterGround(heights, sites) {
    const half = (WORLD_SIZE - 1) * 0.5;
    for (const site of sites) {
        const targetHeight = Math.max(
            SEA_LEVEL_HEIGHT_UNITS + HEIGHT_UNITS_PER_METER,
            heightAtWorld(heights, site.x, site.z));
        const gradeWidth = SHELTER_GRADE_WIDTH * site.scale;
        const entranceCenter = (SHELTER_ENTRANCE_START + SHELTER_ENTRANCE_END) * 0.5;
        for (let x = 0; x < WORLD_SIZE; x++) {
            for (let z = 0; z < WORLD_SIZE; z++) {
                const localX = (x - half - site.x) / site.scale;
                const localZ = (z - half - site.z) / site.scale;
                const foundationDistance = distanceToRectangle(
                    localX,
                    localZ,
                    SHELTER_FOUNDATION_HALF_EXTENT,
                    SHELTER_FOUNDATION_HALF_EXTENT) * site.scale;
                const entranceDistance = distanceToRectangle(
                    localX,
                    localZ - entranceCenter,
                    SHELTER_ENTRANCE_HALF_WIDTH,
                    (SHELTER_ENTRANCE_END - SHELTER_ENTRANCE_START) * 0.5) * site.scale;
                const distance = Math.min(foundationDistance, entranceDistance);
                if (distance <= 0) {
                    heights[x][z] = targetHeight;
                } else if (distance < gradeWidth) {
                    const blend = 1 - (distance / gradeWidth);
                    heights[x][z] = clamp(
                        Math.round(heights[x][z] + ((targetHeight - heights[x][z]) * blend)),
                        MINIMUM_TERRAIN_HEIGHT,
                        MAXIMUM_TERRAIN_HEIGHT);
                }
            }
        }
    }
}

function heightAtWorld(heights, worldX, worldZ) {
    return heightUnitsAtWorld(heights, worldX, worldZ);
}

function distanceToRectangle(x, z, halfWidth, halfDepth) {
    return Math.hypot(Math.max(Math.abs(x) - halfWidth, 0), Math.max(Math.abs(z) - halfDepth, 0));
}

function isInsideShelterClearance(worldX, worldZ, sites) {
    return sites.some(site => {
        const localX = Math.abs((worldX - site.x) / site.scale);
        const localZ = (worldZ - site.z) / site.scale;
        const insideFoundation = localX <= SHELTER_FOUNDATION_HALF_EXTENT &&
            Math.abs(localZ) <= SHELTER_FOUNDATION_HALF_EXTENT;
        const insideEntrance = localX <= SHELTER_ENTRANCE_HALF_WIDTH &&
            localZ >= SHELTER_ENTRANCE_START && localZ <= SHELTER_ENTRANCE_END;
        return insideFoundation || insideEntrance;
    });
}

function syncHabitats(state, data) {
    disposeChildren(state.habitats);
    state.habitats.clear();
    data.forEach((item, index) => state.habitats.add(createHabitat(item, index === 0)));
    setText('habitatCount', String(data.length));
}

function createHabitat(item, central) {
    const root = new THREE.Group();
    root.position.set(item.x, item.y, item.z);
    const scale = central ? 1 : 0.78;
    root.scale.setScalar(scale);
    const wall = new THREE.MeshStandardMaterial({ color: 0xc3aa79, roughness: 0.78 });
    const trim = new THREE.MeshStandardMaterial({ color: 0x514b43, roughness: 0.85 });
    const glass = new THREE.MeshPhysicalMaterial({
        color: 0x89c5ce,
        transparent: true,
        opacity: 0.44,
        roughness: 0.16,
        clearcoat: 0.55,
        depthWrite: false
    });
    const slab = (material, x, y, z, sx, sy, sz) => {
        const mesh = new THREE.Mesh(new THREE.BoxGeometry(sx, sy, sz), material);
        mesh.position.set(x, y, z);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        root.add(mesh);
    };
    slab(trim, 0, -0.10, 0, 8.2, 0.28, 8.2);
    slab(wall, 0, 1.2, -3.8, 8, 2.4, 0.32);
    slab(wall, -3.8, 1.2, 0, 0.32, 2.4, 7.3);
    slab(wall, 3.8, 1.2, 0, 0.32, 2.4, 7.3);
    slab(wall, -2.5, 1.2, 3.8, 2.8, 2.4, 0.32);
    slab(wall, 2.5, 1.2, 3.8, 2.8, 2.4, 0.32);
    slab(glass, 0, 2.55, 0, 6.4, 0.28, 6.4);
    if (central) {
        const core = new THREE.Mesh(
            new THREE.IcosahedronGeometry(0.72, 2),
            new THREE.MeshStandardMaterial({ color: 0x59c6b3, emissive: 0x1f7d70, emissiveIntensity: 1.4, roughness: 0.3 }));
        core.position.y = 1.22;
        root.add(core);
    }
    return root;
}

function createAvatar() {
    const root = new THREE.Group();
    const body = new THREE.Group();
    const neural = new THREE.Group();
    root.add(body, neural);
    const rig = createAvatarRig(body);

    const materials = {
        skin: avatarMaterial(0xc98f71),
        tunic: avatarMaterial(0x2d657b),
        trousers: avatarMaterial(0x2e3942),
        boots: avatarMaterial(0x252728),
        eyes: avatarMaterial(0x182126),
        sclera: avatarMaterial(0xe8ddd2),
        features: avatarMaterial(0x6f4538),
        hair: avatarMaterial(0x35271f),
        hairHighlight: avatarMaterial(0x4a382d)
    };
    const bodyMaterials = Object.values(materials);
    const part = (geometry, material, position, scale = [1, 1, 1], parent = body) => {
        const mesh = new THREE.Mesh(geometry, material);
        mesh.position.set(...position);
        mesh.scale.set(...scale);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        parent.add(mesh);
        return mesh;
    };

    const torsoProfile = [
        [0.19, 0.00],
        [0.22, 0.10],
        [0.23, 0.24],
        [0.27, 0.39],
        [0.31, 0.52],
        [0.29, 0.60],
        [0.15, 0.67]
    ].map(([radius, height]) => new THREE.Vector2(radius, height));
    part(new THREE.LatheGeometry(torsoProfile, 32), materials.tunic, [0, 0.01, 0], [1, 1, 0.62], rig.pelvis);
    part(new THREE.SphereGeometry(0.25, 22, 14), materials.trousers, [0, -0.03, 0], [1, 0.68, 0.70], rig.pelvis);
    part(new THREE.CylinderGeometry(0.082, 0.094, 0.18, 18), materials.skin, [0, 0, 0], [1, 1, 1], rig.neck);

    // A smaller cranium and defined lower face keep the silhouette adult rather than toy-like.
    part(new THREE.SphereGeometry(0.225, 32, 22), materials.skin, [0, 0.04, 0], [0.90, 1.06, 0.91], rig.head);
    part(new THREE.SphereGeometry(0.158, 28, 18), materials.skin, [0, -0.09, 0.018], [0.86, 0.78, 0.88], rig.head);
    part(new THREE.SphereGeometry(0.035, 16, 10), materials.skin, [0, 0.005, 0.215], [0.55, 0.90, 1.15], rig.head);

    // Flattened pinnae sit just behind the jaw line and remain visible from oblique views.
    part(new THREE.SphereGeometry(0.064, 20, 14), materials.skin, [-0.207, 0.03, -0.005], [0.38, 1, 0.66], rig.head);
    part(new THREE.SphereGeometry(0.064, 20, 14), materials.skin, [0.207, 0.03, -0.005], [0.38, 1, 0.66], rig.head);

    part(new THREE.SphereGeometry(0.031, 18, 12), materials.sclera, [-0.073, 0.06, 0.203], [1.20, 0.62, 0.42], rig.head);
    part(new THREE.SphereGeometry(0.031, 18, 12), materials.sclera, [0.073, 0.06, 0.203], [1.20, 0.62, 0.42], rig.head);
    part(new THREE.SphereGeometry(0.015, 16, 10), materials.eyes, [-0.073, 0.06, 0.218], [1, 0.88, 0.55], rig.head);
    part(new THREE.SphereGeometry(0.015, 16, 10), materials.eyes, [0.073, 0.06, 0.218], [1, 0.88, 0.55], rig.head);
    const leftBrow = part(new THREE.BoxGeometry(0.105, 0.014, 0.018), materials.hair, [-0.078, 0.105, 0.211], [1, 1, 1], rig.head);
    const rightBrow = part(new THREE.BoxGeometry(0.105, 0.014, 0.018), materials.hair, [0.078, 0.105, 0.211], [1, 1, 1], rig.head);
    leftBrow.rotation.z = -0.06;
    rightBrow.rotation.z = 0.06;
    const mouth = part(new THREE.BoxGeometry(0.078, 0.009, 0.012), materials.features, [0, -0.085, 0.166], [1, 1, 1], rig.head);
    mouth.rotation.x = 0.10;

    // Tapered sides and overlapping top locks replace the old hemispherical bowl cap.
    part(new THREE.SphereGeometry(0.238, 28, 18, 0, Math.PI * 2, 0, Math.PI * 0.37), materials.hair,
        [0, 0.085, -0.018], [0.91, 1.02, 0.93], rig.head);
    // One continuous ellipsoidal shell wraps from temple to temple around the
    // back of the skull and down to the nape, leaving the pinnae exposed.
    part(new THREE.SphereGeometry(
        0.231, 36, 18,
        Math.PI * 0.98, Math.PI * 1.04,
        0.48, 1.70), materials.hair,
        [0, 0.04, -0.006], [0.91, 1.02, 0.93], rig.head);
    const hairLocks = [
        [-0.105, 0.245, 0.005, 0.095, 0.060, 0.105],
        [-0.035, 0.268, 0.018, 0.100, 0.068, 0.110],
        [0.045, 0.270, 0.008, 0.105, 0.070, 0.108],
        [0.118, 0.245, -0.005, 0.090, 0.058, 0.100]
    ];
    hairLocks.forEach((lock, index) => part(
        new THREE.IcosahedronGeometry(1, 2),
        index % 2 === 0 ? materials.hair : materials.hairHighlight,
        lock.slice(0, 3),
        lock.slice(3),
        rig.head));

    const locator = new THREE.Mesh(
        new THREE.RingGeometry(0.38, 0.43, 48),
        new THREE.MeshBasicMaterial({ color: 0x72dfcc, transparent: true, opacity: 0.78, side: THREE.DoubleSide, depthWrite: false }));
    locator.rotation.x = -Math.PI * 0.5;
    locator.position.y = 0.025;
    root.add(locator);

    const leftArm = createLimb(rig.leftClavicle, -0.232, 0.03, materials.tunic, materials.skin, false, rig.bones, 'Left');
    const rightArm = createLimb(rig.rightClavicle, 0.232, 0.03, materials.tunic, materials.skin, false, rig.bones, 'Right');
    const leftLeg = createLimb(rig.pelvis, -0.135, -0.02, materials.trousers, materials.boots, true, rig.bones, 'Left');
    const rightLeg = createLimb(rig.pelvis, 0.135, -0.02, materials.trousers, materials.boots, true, rig.bones, 'Right');
    rig.skeleton = new THREE.Skeleton(rig.bones);
    rig.visuals = createRigVisuals(rig);

    const nerveMaterial = new THREE.MeshStandardMaterial({
        color: 0xf4d157,
        emissive: 0xb37708,
        emissiveIntensity: 1.8,
        roughness: 0.4
    });
    const brainMaterial = new THREE.MeshStandardMaterial({
        color: 0xffb06d,
        emissive: 0xb33e22,
        emissiveIntensity: 1.35,
        roughness: 0.52
    });
    part(new THREE.SphereGeometry(0.12, 24, 16), brainMaterial, [-0.065, 1.65, -0.005], [1, 0.82, 0.85], neural);
    part(new THREE.SphereGeometry(0.12, 24, 16), brainMaterial, [0.065, 1.65, -0.005], [1, 0.82, 0.85], neural);
    part(new THREE.CylinderGeometry(0.025, 0.038, 0.92, 10), nerveMaterial, [0, 1.09, 0], [1, 1, 1], neural);
    addNerve(neural, nerveMaterial, [[0, 1.42, 0], [-0.22, 1.28, 0], [-0.39, 0.96, 0], [-0.42, 0.62, 0]]);
    addNerve(neural, nerveMaterial, [[0, 1.42, 0], [0.22, 1.28, 0], [0.39, 0.96, 0], [0.42, 0.62, 0]]);
    addNerve(neural, nerveMaterial, [[0, 0.91, 0], [-0.13, 0.69, 0], [-0.16, 0.34, 0], [-0.16, 0.05, 0.05]]);
    addNerve(neural, nerveMaterial, [[0, 0.91, 0], [0.13, 0.69, 0], [0.16, 0.34, 0], [0.16, 0.05, 0.05]]);
    neural.visible = false;

    return {
        root,
        body,
        neural,
        bodyMaterials,
        rig,
        limbs: { leftArm, rightArm, leftLeg, rightLeg }
    };
}

function createAvatarRig(parent) {
    const bones = [];
    const makeBone = (boneParent, name, position) => {
        const bone = new THREE.Bone();
        bone.name = name;
        bone.position.set(...position);
        boneParent.add(bone);
        bones.push(bone);
        return bone;
    };
    const pelvis = makeBone(parent, 'Pelvis', [0, 0.78, 0]);
    const lumbar = makeBone(pelvis, 'LumbarSpine', [0, 0.19, 0]);
    const thoracic = makeBone(lumbar, 'ThoracicSpine', [0, 0.28, 0]);
    const neck = makeBone(thoracic, 'CervicalSpine', [0, 0.20, 0]);
    const head = makeBone(neck, 'Head', [0, 0.20, 0]);
    const leftClavicle = makeBone(thoracic, 'LeftClavicle', [-0.06, 0.06, 0]);
    const rightClavicle = makeBone(thoracic, 'RightClavicle', [0.06, 0.06, 0]);
    return { bones, pelvis, lumbar, thoracic, neck, head, leftClavicle, rightClavicle };
}

function avatarMaterial(color) {
    return new THREE.MeshStandardMaterial({
        color,
        roughness: 0.7,
        metalness: 0,
        transparent: true,
        opacity: 1
    });
}

function createLimb(parent, x, y, upperMaterial, endMaterial, leg, bones, side) {
    const makeBone = (boneParent, name, position) => {
        const bone = new THREE.Bone();
        bone.name = name;
        bone.position.set(...position);
        boneParent.add(bone);
        bones.push(bone);
        return bone;
    };
    const upperLength = leg ? 0.39 : 0.34;
    const lowerLength = leg ? 0.38 : 0.32;
    const radius = leg ? 0.082 : 0.064;
    const pivot = makeBone(parent, `${side}${leg ? 'Hip' : 'Shoulder'}`, [x, y, 0]);
    const shoulder = new THREE.Mesh(new THREE.SphereGeometry(radius * 0.92, 16, 11), upperMaterial);
    shoulder.scale.set(0.92, 1.05, 0.86);
    shoulder.castShadow = true;
    pivot.add(shoulder);
    const upper = new THREE.Mesh(new THREE.CylinderGeometry(radius, radius * 0.84, upperLength, 16), upperMaterial);
    upper.position.y = -upperLength * 0.5;
    upper.castShadow = true;
    pivot.add(upper);
    const joint = makeBone(pivot, `${side}${leg ? 'Knee' : 'Elbow'}`, [0, -upperLength, 0]);
    const jointCap = new THREE.Mesh(new THREE.SphereGeometry(radius * 0.88, 16, 10), leg ? upperMaterial : endMaterial);
    jointCap.scale.set(1, 0.88, 0.92);
    jointCap.castShadow = true;
    joint.add(jointCap);
    const lower = new THREE.Mesh(new THREE.CylinderGeometry(radius * 0.84, radius * 0.67, lowerLength, 16), leg ? upperMaterial : endMaterial);
    lower.position.y = -lowerLength * 0.5;
    lower.castShadow = true;
    joint.add(lower);
    const distal = makeBone(joint, `${side}${leg ? 'Ankle' : 'Wrist'}`, [0, -lowerLength, 0]);
    const end = new THREE.Mesh(new THREE.SphereGeometry(radius * 1.05, 18, 12), endMaterial);
    end.position.set(0, 0, leg ? 0.07 : 0);
    end.scale.set(leg ? 1.04 : 0.72, leg ? 0.62 : 1.28, leg ? 1.72 : 0.68);
    end.castShadow = true;
    distal.add(end);
    let toe = null;
    if (leg) {
        toe = makeBone(distal, `${side}Toe`, [0, -0.025, 0.15]);
    }
    if (!leg) {
        const thumb = new THREE.Mesh(new THREE.SphereGeometry(radius * 0.48, 12, 8), endMaterial);
        thumb.position.set(Math.sign(x) * radius * 0.72, radius * 0.12, radius * 0.20);
        thumb.scale.set(0.72, 1.15, 0.72);
        thumb.castShadow = true;
        distal.add(thumb);
    }
    return { pivot, joint, distal, toe };
}

function createRigVisuals(rig) {
    const boneMaterial = new THREE.MeshStandardMaterial({
        color: 0xe6d8b9,
        emissive: 0x66583d,
        emissiveIntensity: 0.65,
        roughness: 0.66,
        transparent: true,
        opacity: 0.82,
        depthWrite: false
    });
    const jointMaterial = new THREE.MeshStandardMaterial({
        color: 0xf0e3c6,
        emissive: 0x806d48,
        emissiveIntensity: 0.7,
        roughness: 0.62,
        transparent: true,
        opacity: 0.88,
        depthWrite: false
    });
    const visuals = [];
    const up = new THREE.Vector3(0, 1, 0);
    for (const bone of rig.bones) {
        const jointRadius = bone.name === 'Pelvis' || bone.name.endsWith('Hip') ? 0.033 : 0.024;
        const joint = new THREE.Mesh(new THREE.SphereGeometry(jointRadius, 12, 8), jointMaterial);
        bone.add(joint);
        visuals.push(joint);
        const childBones = bone.children.filter(child => child.isBone);
        for (const child of childBones) {
            const offset = child.position.clone();
            const length = offset.length();
            if (length < 0.001) {
                continue;
            }
            const shaft = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.017, length, 8), boneMaterial);
            shaft.position.copy(offset).multiplyScalar(0.5);
            shaft.quaternion.setFromUnitVectors(up, offset.clone().normalize());
            bone.add(shaft);
            visuals.push(shaft);
        }
    }
    visuals.forEach(visual => visual.visible = false);
    return visuals;
}

function addNerve(parent, material, points) {
    const curve = new THREE.CatmullRomCurve3(points.map(point => new THREE.Vector3(...point)));
    parent.add(new THREE.Mesh(new THREE.TubeGeometry(curve, 18, 0.012, 6, false), material));
}

function setAvatarMode(state, mode) {
    state.avatarMode = mode;
    state.avatar.neural.visible = mode === 'neural';
    state.avatar.rig.visuals.forEach(visual => visual.visible = mode === 'neural');
    for (const material of state.avatar.bodyMaterials) {
        material.opacity = mode === 'neural' ? 0.16 : 1;
        material.depthWrite = mode !== 'neural';
    }
    document.querySelectorAll('[data-avatar-mode]').forEach(button =>
        button.classList.toggle('active', button.dataset.avatarMode === mode));
}

function createTrail() {
    const geometry = new THREE.BufferGeometry();
    const positions = new Float32Array(180 * 3);
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geometry.setDrawRange(0, 0);
    const material = new THREE.LineBasicMaterial({ color: 0xf2c560, transparent: true, opacity: 0.9 });
    return { line: new THREE.Line(geometry, material), positions, points: [] };
}

function appendTrail(state, point) {
    const points = state.trail.points;
    const previous = points.at(-1);
    if (previous && previous.distanceToSquared(point) < 0.10) {
        return;
    }
    points.push(point.clone().add(new THREE.Vector3(0, 0.05, 0)));
    if (points.length > 180) {
        points.shift();
    }
    points.forEach((value, index) => value.toArray(state.trail.positions, index * 3));
    state.trail.line.geometry.attributes.position.needsUpdate = true;
    state.trail.line.geometry.setDrawRange(0, points.length);
}

function setPreviewEntities(state) {
    const random = mulberry32(state.seed + 2111);
    const createPositions = (count, kind, radiusMin, radiusMax) => Array.from({ length: count }, (_, index) => {
        const angle = random() * Math.PI * 2;
        const radius = radiusMin + random() * (radiusMax - radiusMin);
        const x = Math.cos(angle) * radius;
        const z = Math.sin(angle) * radius;
        return { kind, x, y: terrainTopAt(state.heights, x, z) + 0.2, z, headingDeg: random() * 360, variant: index % 3 === 0 ? 'Long' : 'Short' };
    });
    syncEntityType(state, 'food', createPositions(12, 'food', 12, 56), createFood);
    syncEntityType(state, 'device', createPositions(5, 'device', 10, 48), createDevice);
    syncEntityType(state, 'predator', createPositions(3, 'predator', 24, 52), createPredator);
    setText('resourceCount', '17');
    setText('predatorCount', '3');
}

function syncEntityType(state, key, data, factory) {
    let roots = state.entityRoots.get(key) ?? [];
    if (roots.length !== data.length) {
        roots.forEach(root => {
            state.entities.remove(root);
            disposeChildren(root);
        });
        roots = data.map(item => {
            const root = factory(item);
            state.entities.add(root);
            return root;
        });
        state.entityRoots.set(key, roots);
    }
    data.forEach((item, index) => {
        roots[index].position.set(item.x, item.y, item.z);
        roots[index].rotation.y = THREE.MathUtils.degToRad(item.headingDeg ?? 0);
    });
}

function createFood() {
    const root = new THREE.Group();
    const fruit = new THREE.Mesh(
        new THREE.SphereGeometry(0.20, 16, 12),
        new THREE.MeshStandardMaterial({ color: 0xe6a63d, emissive: 0x7a3e0b, emissiveIntensity: 0.55, roughness: 0.48 }));
    const leaf = new THREE.Mesh(
        new THREE.SphereGeometry(0.10, 10, 7),
        new THREE.MeshStandardMaterial({ color: 0x4d8f4b, roughness: 0.8 }));
    leaf.position.set(0.10, 0.17, 0);
    leaf.scale.set(1.3, 0.35, 0.7);
    root.add(fruit, leaf);
    return root;
}

function createDevice(item) {
    const root = new THREE.Group();
    const longRange = String(item.variant ?? '').toLowerCase().includes('long');
    const material = new THREE.MeshStandardMaterial({
        color: longRange ? 0x8bc1d2 : 0xb49acb,
        emissive: longRange ? 0x164c62 : 0x4c295f,
        emissiveIntensity: 0.7,
        metalness: 0.58,
        roughness: 0.28
    });
    const body = new THREE.Mesh(new THREE.BoxGeometry(longRange ? 0.52 : 0.36, 0.13, 0.18), material);
    const grip = new THREE.Mesh(new THREE.BoxGeometry(0.12, 0.28, 0.13), material);
    grip.position.set(-0.10, -0.16, 0);
    root.add(body, grip);
    return root;
}

function createPredator() {
    const root = new THREE.Group();
    const fur = new THREE.MeshStandardMaterial({ color: 0x604235, roughness: 0.96 });
    const furLight = new THREE.MeshStandardMaterial({ color: 0x8a6750, roughness: 0.94 });
    const dark = new THREE.MeshStandardMaterial({ color: 0x201a18, roughness: 0.86 });
    const addPart = (geometry, material, position, scale = [1, 1, 1]) => {
        const mesh = new THREE.Mesh(geometry, material);
        mesh.position.set(...position);
        mesh.scale.set(...scale);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        root.add(mesh);
        return mesh;
    };

    // Overlapping shoulder, barrel and rump masses give the animal a bear's heavy forequarters.
    addPart(new THREE.SphereGeometry(0.58, 24, 16), fur, [0, 0.47, -0.10], [1.00, 0.76, 1.30]);
    addPart(new THREE.SphereGeometry(0.54, 24, 16), fur, [0, 0.60, 0.36], [1.08, 0.94, 0.92]);
    addPart(new THREE.SphereGeometry(0.50, 22, 15), fur, [0, 0.46, -0.63], [0.98, 0.82, 0.92]);
    addPart(new THREE.SphereGeometry(0.41, 22, 15), fur, [0, 0.72, 0.68], [0.96, 1.02, 0.94]);
    addPart(new THREE.SphereGeometry(0.35, 24, 17), fur, [0, 0.82, 0.94], [1.00, 0.90, 0.92]);
    addPart(new THREE.SphereGeometry(0.22, 20, 14), furLight, [0, 0.70, 1.21], [1.06, 0.72, 1.22]);
    addPart(new THREE.SphereGeometry(0.095, 18, 12), dark, [0, 0.72, 1.43], [1.08, 0.72, 0.78]);

    for (const x of [-0.22, 0.22]) {
        addPart(new THREE.SphereGeometry(0.12, 18, 12), fur, [x, 1.08, 0.88], [1, 1.05, 0.72]);
        addPart(new THREE.SphereGeometry(0.066, 14, 10), dark, [x, 1.08, 0.925], [1, 1, 0.45]);
        addPart(new THREE.SphereGeometry(0.031, 14, 9), dark, [x * 0.62, 0.86, 1.245], [1, 0.78, 0.52]);
    }

    for (const x of [-0.36, 0.36]) {
        for (const z of [-0.48, 0.38]) {
            const front = z > 0;
            addPart(
                new THREE.CylinderGeometry(front ? 0.14 : 0.125, 0.115, front ? 0.56 : 0.50, 14),
                fur,
                [x, front ? 0.22 : 0.18, z],
                [1, 1, 1]);
            addPart(
                new THREE.SphereGeometry(front ? 0.145 : 0.13, 18, 11),
                fur,
                [x, -0.105, z + 0.10],
                [1.05, 0.52, 1.42]);
        }
    }
    addPart(new THREE.SphereGeometry(0.10, 16, 10), fur, [0, 0.52, -1.08], [1, 0.78, 0.82]);
    return root;
}

function bindWorkspace(state) {
    const buttons = [...document.querySelectorAll('[data-workspace-tab]')];
    const select = name => {
        buttons.forEach(button => button.classList.toggle('active', button.dataset.workspaceTab === name));
        document.querySelectorAll('[data-workspace-panel]').forEach(panel => {
            panel.hidden = panel.dataset.workspacePanel !== name;
        });
        state.active = name === 'world';
        if (state.active) {
            requestAnimationFrame(() => resize(state));
        }
        window.dispatchEvent(new CustomEvent('nre-workspace-changed', { detail: name }));
    };
    buttons.forEach(button => {
        const handler = () => select(button.dataset.workspaceTab);
        button.addEventListener('click', handler);
        state.cleanup.push(() => button.removeEventListener('click', handler));
    });
}

function bindWorldControls(state) {
    document.querySelectorAll('[data-world-command]').forEach(button => {
        const handler = () => sendWorldCommand(state, button.dataset.worldCommand);
        button.addEventListener('click', handler);
        state.cleanup.push(() => button.removeEventListener('click', handler));
    });
    document.querySelectorAll('[data-world-action]').forEach(button => {
        const handler = () => {
            if (button.dataset.worldAction === 'overview') {
                state.cameraMode = 'orbit';
                state.camera.position.set(0, 145, 0.1);
                state.controls.target.set(0, 0, 0);
            } else {
                state.cameraMode = 'orbit';
                frameAvatar(state, true);
            }
            updateCameraButtons(state);
            state.controls.update();
        };
        button.addEventListener('click', handler);
        state.cleanup.push(() => button.removeEventListener('click', handler));
    });
    document.querySelectorAll('[data-world-camera]').forEach(button => {
        const handler = () => {
            state.cameraMode = button.dataset.worldCamera;
            updateCameraButtons(state);
        };
        button.addEventListener('click', handler);
        state.cleanup.push(() => button.removeEventListener('click', handler));
    });
    document.querySelectorAll('[data-avatar-mode]').forEach(button => {
        const handler = () => setAvatarMode(state, button.dataset.avatarMode);
        button.addEventListener('click', handler);
        state.cleanup.push(() => button.removeEventListener('click', handler));
    });
    const trailToggle = document.getElementById('showAvatarTrail');
    const trailHandler = () => state.trail.line.visible = trailToggle.checked;
    trailToggle.addEventListener('change', trailHandler);
    state.cleanup.push(() => trailToggle.removeEventListener('change', trailHandler));
    const atmosphereToggle = document.getElementById('showWorldAtmosphere');
    const atmosphereHandler = () => {
        state.atmosphere = atmosphereToggle.checked;
        state.scene.fog = state.atmosphere ? new THREE.Fog(0xa8cbd0, 58, 210) : null;
        state.scene.traverse(object => {
            if (object.userData.atmosphere) {
                object.visible = state.atmosphere;
            }
        });
    };
    atmosphereToggle.addEventListener('change', atmosphereHandler);
    state.cleanup.push(() => atmosphereToggle.removeEventListener('change', atmosphereHandler));
}

async function sendWorldCommand(state, command) {
    if (state.commandInFlight) {
        return;
    }
    state.commandInFlight = true;
    document.querySelectorAll('[data-world-command]').forEach(button => button.disabled = true);
    try {
        const response = await fetch(`/editor/api/world/${command}`, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: '{}',
            signal: AbortSignal.timeout(5000)
        });
        if (!response.ok) {
            throw new Error(`World command returned HTTP ${response.status}.`);
        }
        await pollWorldState(state);
    } catch (error) {
        setText('worldTelemetryLog', error instanceof Error ? error.message : String(error));
    } finally {
        state.commandInFlight = false;
        document.querySelectorAll('[data-world-command]').forEach(button => button.disabled = false);
    }
}

function updateCameraButtons(state) {
    document.querySelectorAll('[data-world-camera]').forEach(button =>
        button.classList.toggle('active', button.dataset.worldCamera === state.cameraMode));
}

function animate(state, now) {
    if (!state.active) {
        state.lastFrameTime = now;
        return;
    }
    const dt = Math.min(0.05, Math.max(0.001, (now - state.lastFrameTime) / 1000));
    state.lastFrameTime = now;
    const positionBlend = 1 - Math.exp(-dt * 8);
    state.avatar.root.position.x = THREE.MathUtils.lerp(state.avatar.root.position.x, state.targetPosition.x, positionBlend);
    state.avatar.root.position.z = THREE.MathUtils.lerp(state.avatar.root.position.z, state.targetPosition.z, positionBlend);
    state.avatar.root.position.y = state.motion.grounded
        ? terrainTopAt(state.heights, state.avatar.root.position.x, state.avatar.root.position.z) + 0.03
        : THREE.MathUtils.lerp(state.avatar.root.position.y, state.targetPosition.y, positionBlend);
    state.currentHeading = lerpAngle(state.currentHeading, state.targetHeading, 1 - Math.exp(-dt * 9));
    state.avatar.root.rotation.y = THREE.MathUtils.degToRad(state.currentHeading);

    const gaitPosture = state.articulation.posture === 'standing' || state.articulation.posture === 'crouching';
    const gait = state.motion.grounded && gaitPosture
        ? clamp(Math.abs(state.motion.forwardSpeed) / WORLD_MAX_FORWARD_SPEED, 0, 1)
        : 0;
    state.walkPhase += dt * (1.5 + gait * 7);
    const articulationBlend = 1 - Math.exp(-dt * 12);
    const rotateToward = (bone, axis, target) => {
        bone.rotation[axis] = THREE.MathUtils.lerp(bone.rotation[axis], target, articulationBlend);
    };
    rotateToward(state.avatar.limbs.leftArm.pivot, 'x', -clampJoint(state.articulation.leftShoulder, 'shoulder'));
    rotateToward(state.avatar.limbs.rightArm.pivot, 'x', -clampJoint(state.articulation.rightShoulder, 'shoulder'));
    rotateToward(state.avatar.limbs.leftArm.pivot, 'z', -clampJoint(state.articulation.leftShoulderAbduction, 'shoulderAbduction'));
    rotateToward(state.avatar.limbs.rightArm.pivot, 'z', clampJoint(state.articulation.rightShoulderAbduction, 'shoulderAbduction'));
    rotateToward(state.avatar.limbs.leftArm.joint, 'x', -clampJoint(state.articulation.leftElbow, 'elbow'));
    rotateToward(state.avatar.limbs.rightArm.joint, 'x', -clampJoint(state.articulation.rightElbow, 'elbow'));
    rotateToward(state.avatar.limbs.leftArm.distal, 'x', state.articulation.leftElbow * 0.08);
    rotateToward(state.avatar.limbs.rightArm.distal, 'x', state.articulation.rightElbow * 0.08);
    // Telemetry follows the ISB convention (flexion positive). Three.js positive
    // X rotates a downward femur posteriorly, so the visual transform is negated.
    rotateToward(state.avatar.limbs.leftLeg.pivot, 'x', -clampJoint(state.articulation.leftHip, 'hip'));
    rotateToward(state.avatar.limbs.rightLeg.pivot, 'x', -clampJoint(state.articulation.rightHip, 'hip'));
    rotateToward(state.avatar.limbs.leftLeg.pivot, 'z', -clampJoint(state.articulation.leftHipAbduction, 'hipAbduction'));
    rotateToward(state.avatar.limbs.rightLeg.pivot, 'z', clampJoint(state.articulation.rightHipAbduction, 'hipAbduction'));
    rotateToward(state.avatar.limbs.leftLeg.joint, 'x', clampJoint(state.articulation.leftKnee, 'knee'));
    rotateToward(state.avatar.limbs.rightLeg.joint, 'x', clampJoint(state.articulation.rightKnee, 'knee'));
    rotateToward(state.avatar.limbs.leftLeg.distal, 'x', clampJoint(state.articulation.leftAnkle, 'ankle'));
    rotateToward(state.avatar.limbs.rightLeg.distal, 'x', clampJoint(state.articulation.rightAnkle, 'ankle'));
    rotateToward(state.avatar.limbs.leftLeg.distal, 'z', -clampJoint(state.articulation.leftAnkleRoll, 'ankleRoll'));
    rotateToward(state.avatar.limbs.rightLeg.distal, 'z', clampJoint(state.articulation.rightAnkleRoll, 'ankleRoll'));
    rotateToward(state.avatar.limbs.leftLeg.toe, 'x', Math.max(0.02, -state.articulation.leftAnkle * 0.35));
    rotateToward(state.avatar.limbs.rightLeg.toe, 'x', Math.max(0.02, -state.articulation.rightAnkle * 0.35));
    rotateToward(state.avatar.rig.pelvis, 'y', 0);
    rotateToward(state.avatar.rig.pelvis, 'z', state.articulation.trunkRoll * 0.24);
    rotateToward(state.avatar.rig.lumbar, 'x', -state.articulation.trunkPitch * 0.42);
    rotateToward(state.avatar.rig.lumbar, 'y', state.articulation.trunkYaw);
    rotateToward(state.avatar.rig.lumbar, 'z', state.articulation.trunkRoll * 0.36);
    rotateToward(state.avatar.rig.thoracic, 'x', -state.articulation.trunkPitch * 0.58);
    rotateToward(state.avatar.rig.thoracic, 'z', state.articulation.trunkRoll * 0.40);
    rotateToward(state.avatar.rig.leftClavicle, 'z', -0.03);
    rotateToward(state.avatar.rig.rightClavicle, 'z', 0.03);
    rotateToward(state.avatar.rig.neck, 'x',
        (state.articulation.trunkPitch * 0.20) - clampJoint(state.articulation.neckPitch, 'neckPitch'));
    rotateToward(state.avatar.rig.neck, 'y', clampJoint(state.articulation.neckYaw, 'neckYaw'));
    rotateToward(state.avatar.rig.neck, 'z', -state.articulation.trunkRoll * 0.26);
    rotateToward(state.avatar.rig.head, 'x', state.articulation.trunkPitch * 0.18);
    rotateToward(state.avatar.rig.head, 'z', -state.articulation.trunkRoll * 0.22);
    const heightCompression = clamp((1.74 - state.articulation.bodyHeight) / 1.40, 0, 1);
    state.avatar.rig.pelvis.position.y = THREE.MathUtils.lerp(
        state.avatar.rig.pelvis.position.y,
        0.78 - (heightCompression * 0.48),
        articulationBlend);
    const lyingProgress = state.articulation.posture === 'lying'
        ? clamp((1.48 - state.articulation.bodyHeight) / 1.14, 0, 1)
        : 0;
    const lyingRotation = -Math.PI * 0.48 * lyingProgress;
    const physicalPitch = clamp(state.articulation.balance.fallPitch, -1.5, 1.5);
    const physicalRoll = clamp(state.articulation.balance.fallRoll, -1.5, 1.5);
    const bodyPitch = lyingRotation - physicalPitch;
    state.avatar.body.rotation.x = THREE.MathUtils.lerp(state.avatar.body.rotation.x, bodyPitch, articulationBlend);
    state.avatar.neural.rotation.x = THREE.MathUtils.lerp(state.avatar.neural.rotation.x, bodyPitch, articulationBlend);
    state.avatar.body.rotation.z = THREE.MathUtils.lerp(state.avatar.body.rotation.z, physicalRoll, articulationBlend);
    state.avatar.neural.rotation.z = THREE.MathUtils.lerp(state.avatar.neural.rotation.z, physicalRoll, articulationBlend);
    const gaitBob = Math.abs(Math.sin(state.walkPhase * 2)) * gait * 0.025;
    const proneClearance = lyingProgress * 0.035;
    state.avatar.body.position.y = gaitBob + proneClearance + state.articulation.supportPlaneOffset;
    state.avatar.neural.position.y = gaitBob + proneClearance + state.articulation.supportPlaneOffset;

    if (state.cameraMode === 'follow') {
        const heading = THREE.MathUtils.degToRad(state.currentHeading);
        const forward = new THREE.Vector3(Math.sin(heading), 0, Math.cos(heading));
        const desired = state.avatar.root.position.clone()
            .addScaledVector(forward, -8)
            .add(new THREE.Vector3(0, 5.2, 0));
        state.camera.position.lerp(desired, 1 - Math.exp(-dt * 3.8));
        state.controls.target.lerp(state.avatar.root.position.clone().add(new THREE.Vector3(0, 1.05, 0)), 1 - Math.exp(-dt * 5));
    }
    state.controls.update();
    if (state.atmosphere) {
        const pulse = 0.68 + Math.sin(now * 0.0013) * 0.05;
        state.waterMaterials.forEach(material => material.opacity = pulse);
    }
    state.renderer.render(state.scene, state.camera);
}

function scheduleWorldPoll(state) {
    state.frameTimer = window.setTimeout(async () => {
        await pollWorldState(state);
        if (!state.disposed) {
            scheduleWorldPoll(state);
        }
    }, 700);
}

async function pollWorldState(state) {
    try {
        const response = await fetch('/editor/api/world-state', {
            cache: 'no-store',
            credentials: 'same-origin',
            signal: AbortSignal.timeout(4500)
        });
        if (!response.ok) {
            throw new Error(`World gateway returned HTTP ${response.status}.`);
        }
        const envelope = await response.json();
        if (!envelope.available || !envelope.state) {
            applyWorldOffline(state, envelope.message ?? 'WorldSim is offline.');
            return;
        }
        applyWorldState(state, envelope.state, envelope);
    } catch (error) {
        if (!state.disposed) {
            applyWorldOffline(state, error instanceof Error ? error.message : String(error));
        }
    }
}

function applyWorldState(state, snapshot, envelope) {
    const seed = integerValue(snapshot, 'seed') || PREVIEW_SEED;
    if (seed !== state.seed) {
        rebuildEnvironment(state, seed);
    }
    state.lastStateAt = Date.now();
    const authoritativeX = numberValue(snapshot, 'avatarX');
    const authoritativeY = numberValue(snapshot, 'avatarY');
    const authoritativeZ = numberValue(snapshot, 'avatarZ');
    state.targetPosition.set(authoritativeX, authoritativeY, authoritativeZ);
    state.targetHeading = normalizeDegrees(numberValue(snapshot, 'avatarHeadingDeg'));
    if (!state.hasFramedAvatar) {
        frameAvatar(state, true);
        state.hasFramedAvatar = true;
    }
    state.motorDrive.left = numberValue(snapshot, 'leftMotorDrive');
    state.motorDrive.right = numberValue(snapshot, 'rightMotorDrive');
    state.motorDrive.manipulator = numberValue(snapshot, 'manipulatorDrive');
    state.motorDrive.leftHipCoronal = numberValue(snapshot, 'leftHipCoronalDrive');
    state.motorDrive.rightHipCoronal = numberValue(snapshot, 'rightHipCoronalDrive');
    state.motorDrive.leftAnkleSagittal = numberValue(snapshot, 'leftAnkleSagittalDrive');
    state.motorDrive.rightAnkleSagittal = numberValue(snapshot, 'rightAnkleSagittalDrive');
    state.motorDrive.leftAnkleCoronal = numberValue(snapshot, 'leftAnkleCoronalDrive');
    state.motorDrive.rightAnkleCoronal = numberValue(snapshot, 'rightAnkleCoronalDrive');
    state.motorDrive.trunkYaw = numberValue(snapshot, 'trunkYawDrive');
    state.motorDrive.headYaw = numberValue(snapshot, 'headYawDrive');
    state.motorDrive.headPitch = numberValue(snapshot, 'headPitchDrive');
    state.motorDrive.stand = numberValue(snapshot, 'standDrive');
    state.motorDrive.crouch = numberValue(snapshot, 'crouchDrive');
    state.motorDrive.sit = numberValue(snapshot, 'sitDrive');
    state.motorDrive.lie = numberValue(snapshot, 'lieDrive');
    state.motion.forwardSpeed = numberValue(snapshot, 'avatarForwardSpeed');
    state.motion.verticalVelocity = numberValue(snapshot, 'avatarVerticalVelocity');
    state.motion.grounded = booleanValue(snapshot, 'avatarGrounded');
    const articulation = value(snapshot, 'articulation');
    if (articulation && typeof articulation === 'object') {
        state.articulation.leftHip = numberValue(articulation, 'leftHipAngleRadians');
        state.articulation.rightHip = numberValue(articulation, 'rightHipAngleRadians');
        state.articulation.leftHipAbduction = numberValue(articulation, 'leftHipAbductionRadians');
        state.articulation.rightHipAbduction = numberValue(articulation, 'rightHipAbductionRadians');
        state.articulation.leftKnee = numberValue(articulation, 'leftKneeAngleRadians');
        state.articulation.rightKnee = numberValue(articulation, 'rightKneeAngleRadians');
        state.articulation.leftAnkle = numberValue(articulation, 'leftAnkleAngleRadians');
        state.articulation.rightAnkle = numberValue(articulation, 'rightAnkleAngleRadians');
        state.articulation.leftAnkleRoll = numberValue(articulation, 'leftAnkleRollRadians');
        state.articulation.rightAnkleRoll = numberValue(articulation, 'rightAnkleRollRadians');
        state.articulation.leftFootLoad = numberValue(articulation, 'leftFootLoadNewtons');
        state.articulation.rightFootLoad = numberValue(articulation, 'rightFootLoadNewtons');
        readFootPressure(state.articulation.leftFootPressure, value(articulation, 'leftFootPressure'));
        readFootPressure(state.articulation.rightFootPressure, value(articulation, 'rightFootPressure'));
        state.articulation.leftShoulder = numberValue(articulation, 'leftShoulderAngleRadians');
        state.articulation.rightShoulder = numberValue(articulation, 'rightShoulderAngleRadians');
        state.articulation.leftShoulderAbduction = numberValue(articulation, 'leftShoulderAbductionRadians');
        state.articulation.rightShoulderAbduction = numberValue(articulation, 'rightShoulderAbductionRadians');
        state.articulation.leftElbow = numberValue(articulation, 'leftElbowAngleRadians');
        state.articulation.rightElbow = numberValue(articulation, 'rightElbowAngleRadians');
        state.articulation.manipulatorExtension = numberValue(articulation, 'manipulatorExtensionFraction');
        state.articulation.trunkPitch = numberValue(articulation, 'trunkPitchRadians');
        state.articulation.trunkRoll = numberValue(articulation, 'trunkRollRadians');
        state.articulation.trunkYaw = numberValue(articulation, 'trunkYawRadians');
        state.articulation.neckYaw = numberValue(articulation, 'neckYawRadians');
        state.articulation.neckPitch = numberValue(articulation, 'neckPitchRadians');
        state.articulation.supportPlaneOffset = numberValue(articulation, 'supportPlaneOffsetMeters');
        const musculoskeletal = value(articulation, 'musculoskeletal');
        if (musculoskeletal && typeof musculoskeletal === 'object') {
            state.articulation.posture = textValue(musculoskeletal, 'posture') || 'standing';
            state.articulation.bodyHeight = numberValue(musculoskeletal, 'bodyHeightMeters') || 1.74;
            state.articulation.upright = numberValue(musculoskeletal, 'uprightFraction');
            state.articulation.support = numberValue(musculoskeletal, 'supportFraction');
            state.articulation.balanceError = numberValue(musculoskeletal, 'balanceError');
            state.articulation.muscles = arrayValue(musculoskeletal, 'muscles') ?? [];
            const balance = value(musculoskeletal, 'balance');
            if (balance && typeof balance === 'object') {
                state.articulation.balance.phase = textValue(balance, 'phase') || 'stable';
                state.articulation.balance.margin = numberValue(balance, 'supportMarginMeters');
                state.articulation.balance.centerOfMassX = numberValue(balance, 'centerOfMassXMeters');
                state.articulation.balance.centerOfMassY = numberValue(balance, 'centerOfMassYMeters');
                state.articulation.balance.centerOfMassZ = numberValue(balance, 'centerOfMassZMeters');
                state.articulation.balance.centerOfPressureX = numberValue(balance, 'centerOfPressureXMeters');
                state.articulation.balance.centerOfPressureZ = numberValue(balance, 'centerOfPressureZMeters');
                state.articulation.balance.extrapolatedCenterOfMassX = numberValue(balance, 'extrapolatedCenterOfMassXMeters');
                state.articulation.balance.extrapolatedCenterOfMassZ = numberValue(balance, 'extrapolatedCenterOfMassZMeters');
                state.articulation.balance.fallPitch = numberValue(balance, 'fallPitchRadians');
                state.articulation.balance.fallRoll = numberValue(balance, 'fallRollRadians');
                state.articulation.balance.fallPitchVelocity = numberValue(balance, 'fallPitchVelocityRadiansPerSecond');
                state.articulation.balance.fallRollVelocity = numberValue(balance, 'fallRollVelocityRadiansPerSecond');
            }
        }
    }
    const worldRunning = booleanValue(snapshot, 'running');
    const resumeButton = document.querySelector('[data-world-command="resume"]');
    const pauseButton = document.querySelector('[data-world-command="pause"]');
    if (resumeButton) resumeButton.hidden = worldRunning;
    if (pauseButton) pauseButton.hidden = !worldRunning;
    appendTrail(state, state.targetPosition);

    const foods = arrayValue(snapshot, 'foodPickups');
    const devices = arrayValue(snapshot, 'weaponPickups');
    const predators = arrayValue(snapshot, 'predators');
    const shelters = arrayValue(snapshot, 'shelters');
    if (foods || devices || predators) {
        syncEntityType(state, 'food', foods ?? [], createFood);
        syncEntityType(state, 'device', devices ?? [], createDevice);
        syncEntityType(state, 'predator', predators ?? [], createPredator);
        setText('resourceCount', String((foods?.length ?? 0) + (devices?.length ?? 0)));
        setText('predatorCount', booleanValue(snapshot, 'predatorsSuspended')
            ? 'suspended'
            : String(predators?.length ?? 0));
    }
    if (shelters) {
        const signature = shelters.map(item => `${item.x}:${item.z}`).join('|');
        if (signature !== state.entitySignature) {
            state.entitySignature = signature;
            syncHabitats(state, shelters);
        }
    }

    const energy = clamp(numberValue(snapshot, 'storedEnergyJoules') / NOMINAL_ENERGY_JOULES, 0, 1);
    const hydration = clamp(numberValue(snapshot, 'hydrationFraction'), 0, 1);
    const tissue = clamp(numberValue(snapshot, 'tissueIntegrityFraction'), 0, 1);
    setMeter('avatarEnergy', 'avatarEnergyValue', energy);
    setMeter('avatarHydration', 'avatarHydrationValue', hydration);
    setMeter('avatarTissue', 'avatarTissueValue', tissue);
    setText('avatarVitalState', textValue(snapshot, 'vitalState') || 'Unknown');
    document.getElementById('avatarVitalDot').className = `status-dot ${tissue > 0.6 ? 'online' : tissue > 0.25 ? 'degraded' : 'offline'}`;
    const brainConnected = booleanValue(snapshot, 'brainConnected');
    const brainLinkAges = [value(snapshot, 'frameAgeSeconds'), value(snapshot, 'telemetryAgeSeconds')]
        .map(candidate => Number(candidate))
        .filter(candidate => Number.isFinite(candidate));
    const brainLinkAge = brainLinkAges.length > 0 ? Math.min(...brainLinkAges) : Number.POSITIVE_INFINITY;
    setText('avatarBrainLink', brainConnected ? 'Connected' : Number.isFinite(brainLinkAge) ? `Delayed ${Math.round(brainLinkAge)}s` : 'Waiting');
    setText('avatarPhysiologyMode', booleanValue(snapshot, 'motorTrainingMode')
        ? 'Motor training | sustained metabolism'
        : 'Standard metabolism');
    setText('avatarDevelopmentStage', splitCamel(textValue(snapshot, 'developmentStage') || 'Terrain'));
    setText('avatarPosition', `${authoritativeX.toFixed(1)}, ${authoritativeY.toFixed(1)}, ${authoritativeZ.toFixed(1)}`);
    setText('avatarPosture', capitalize(state.articulation.posture));
    const ascentMode = textValue(snapshot, 'terrainAscentMode') || 'none';
    const ascentProgress = clamp(numberValue(snapshot, 'terrainAscentProgress'), 0, 1);
    const ascentStarted = Math.max(0, Math.round(numberValue(snapshot, 'terrainAscentStarted')));
    const ascentCompleted = Math.max(0, Math.round(numberValue(snapshot, 'terrainAscentCompleted')));
    const ascentAborted = Math.max(0, Math.round(numberValue(snapshot, 'terrainAscentAborted')));
    const ascentRejected = Math.max(0, Math.round(numberValue(snapshot, 'terrainAscentRejected')));
    setText('avatarTerrainAscent', ascentMode === 'none'
        ? `${ascentCompleted}/${ascentStarted} complete | ${ascentAborted} aborted | ${ascentRejected} rejected`
        : `${capitalize(ascentMode)} ${Math.round(ascentProgress * 100)}% | ${ascentCompleted}/${ascentStarted} complete`);
    const marginMillimeters = Math.round(state.articulation.balance.margin * 1_000);
    setText('avatarBalance', `${capitalize(state.articulation.balance.phase.replaceAll('_', ' '))} | ${marginMillimeters} mm | ${Math.round(state.articulation.balanceError * 100)}% error`);
    setText('avatarDistance', numberValue(snapshot, 'distanceTravelled').toFixed(1));
    setText('avatarShelter', booleanValue(snapshot, 'inShelter') ? 'Yes' : 'No');
    setText('avatarSleep', booleanValue(snapshot, 'neuronalSleep') ? 'Sleeping' : 'Awake');
    setText('avatarFood', String(integerValue(snapshot, 'foodConsumed')));
    setText('avatarWeapons', String(integerValue(snapshot, 'weaponCharges')));
    setDrive('leftMotorDrive', 'leftMotorDriveValue', state.motorDrive.left);
    setDrive('rightMotorDrive', 'rightMotorDriveValue', state.motorDrive.right);
    setDrive('manipulatorDrive', 'manipulatorDriveValue', state.motorDrive.manipulator);
    setDrive('leftHipCoronalDrive', 'leftHipCoronalDriveValue', state.motorDrive.leftHipCoronal);
    setDrive('rightHipCoronalDrive', 'rightHipCoronalDriveValue', state.motorDrive.rightHipCoronal);
    setDrive('leftAnkleSagittalDrive', 'leftAnkleSagittalDriveValue', state.motorDrive.leftAnkleSagittal);
    setDrive('rightAnkleSagittalDrive', 'rightAnkleSagittalDriveValue', state.motorDrive.rightAnkleSagittal);
    setDrive('leftAnkleCoronalDrive', 'leftAnkleCoronalDriveValue', state.motorDrive.leftAnkleCoronal);
    setDrive('rightAnkleCoronalDrive', 'rightAnkleCoronalDriveValue', state.motorDrive.rightAnkleCoronal);
    setDrive('trunkYawDrive', 'trunkYawDriveValue', state.motorDrive.trunkYaw);
    setDrive('headYawDrive', 'headYawDriveValue', state.motorDrive.headYaw);
    setDrive('headPitchDrive', 'headPitchDriveValue', state.motorDrive.headPitch);
    setDrive('standDrive', 'standDriveValue', state.motorDrive.stand);
    setDrive('crouchDrive', 'crouchDriveValue', state.motorDrive.crouch);
    setDrive('sitDrive', 'sitDriveValue', state.motorDrive.sit);
    setDrive('lieDrive', 'lieDriveValue', state.motorDrive.lie);
    setText('motorChannelGroundState', ascentMode !== 'none'
        ? ascentMode
        : state.motion.grounded ? 'grounded' : 'airborne');
    setSignedChannel('leftShoulderChannel', 'leftShoulderChannelValue', state.articulation.leftShoulder, 1.2);
    setSignedChannel('rightShoulderChannel', 'rightShoulderChannelValue', state.articulation.rightShoulder, 1.2);
    setSignedChannel('leftShoulderAbductionChannel', 'leftShoulderAbductionChannelValue', state.articulation.leftShoulderAbduction, 1.2);
    setSignedChannel('rightShoulderAbductionChannel', 'rightShoulderAbductionChannelValue', state.articulation.rightShoulderAbduction, 1.2);
    setSignedChannel('leftElbowChannel', 'leftElbowChannelValue', state.articulation.leftElbow, 1.5);
    setSignedChannel('rightElbowChannel', 'rightElbowChannelValue', state.articulation.rightElbow, 1.5);
    setSignedChannel('leftHipChannel', 'leftHipChannelValue', state.articulation.leftHip, 0.65);
    setSignedChannel('rightHipChannel', 'rightHipChannelValue', state.articulation.rightHip, 0.65);
    setSignedChannel('leftHipAbductionChannel', 'leftHipAbductionChannelValue', state.articulation.leftHipAbduction, 0.78);
    setSignedChannel('rightHipAbductionChannel', 'rightHipAbductionChannelValue', state.articulation.rightHipAbduction, 0.78);
    setSignedChannel('leftKneeChannel', 'leftKneeChannelValue', state.articulation.leftKnee, 1.2);
    setSignedChannel('rightKneeChannel', 'rightKneeChannelValue', state.articulation.rightKnee, 1.2);
    setSignedChannel('leftAnkleChannel', 'leftAnkleChannelValue', state.articulation.leftAnkle, 0.65);
    setSignedChannel('rightAnkleChannel', 'rightAnkleChannelValue', state.articulation.rightAnkle, 0.65);
    setSignedChannel('leftAnkleRollChannel', 'leftAnkleRollChannelValue', state.articulation.leftAnkleRoll, 0.52);
    setSignedChannel('rightAnkleRollChannel', 'rightAnkleRollChannelValue', state.articulation.rightAnkleRoll, 0.52);
    setSignedChannel('trunkPitchChannel', 'trunkPitchChannelValue', state.articulation.trunkPitch, 0.25);
    setSignedChannel('trunkRollChannel', 'trunkRollChannelValue', state.articulation.trunkRoll, 0.25);
    setSignedChannel('trunkYawChannel', 'trunkYawChannelValue', state.articulation.trunkYaw, 0.61);
    setSignedChannel('neckYawChannel', 'neckYawChannelValue', state.articulation.neckYaw, 1.35);
    setSignedChannel('neckPitchChannel', 'neckPitchChannelValue', state.articulation.neckPitch, 0.95);
    setUnsignedChannel('manipulatorExtensionChannel', 'manipulatorExtensionChannelValue', state.articulation.manipulatorExtension, 1.0,
        value => `${Math.round(value * 100)}%`);
    const leftHandAperture = clamp(numberValue(snapshot, 'leftHandApertureFraction'), 0, 1);
    const rightHandAperture = clamp(numberValue(snapshot, 'rightHandApertureFraction'), 0, 1);
    const leftGripForce = Math.max(0, numberValue(snapshot, 'leftGripForceNewtons'));
    const rightGripForce = Math.max(0, numberValue(snapshot, 'rightGripForceNewtons'));
    setUnsignedChannel('leftHandApertureChannel', 'leftHandApertureChannelValue', leftHandAperture, 1.0,
        value => `${Math.round(value * 100)}%`);
    setUnsignedChannel('rightHandApertureChannel', 'rightHandApertureChannelValue', rightHandAperture, 1.0,
        value => `${Math.round(value * 100)}%`);
    setUnsignedChannel('leftGripForceChannel', 'leftHandState', leftGripForce, 180.0,
        value => `${splitCamel(textValue(snapshot, 'leftHandPhase') || 'Open')} | ${Math.round(value)} N`);
    setUnsignedChannel('rightGripForceChannel', 'rightHandState', rightGripForce, 180.0,
        value => `${splitCamel(textValue(snapshot, 'rightHandPhase') || 'Open')} | ${Math.round(value)} N`);
    setUnsignedChannel('leftFootLoadChannel', 'leftFootLoadChannelValue', state.articulation.leftFootLoad, 720.0,
        value => `${Math.round(value)} N`);
    setUnsignedChannel('rightFootLoadChannel', 'rightFootLoadChannelValue', state.articulation.rightFootLoad, 720.0,
        value => `${Math.round(value)} N`);
    setPressureBalanceChannel(
        'leftFootLongitudinalChannel',
        'leftFootLongitudinalChannelValue',
        state.articulation.leftFootPressure.heelMedial + state.articulation.leftFootPressure.heelLateral,
        state.articulation.leftFootPressure.forefootMedial + state.articulation.leftFootPressure.forefootLateral);
    setPressureBalanceChannel(
        'rightFootLongitudinalChannel',
        'rightFootLongitudinalChannelValue',
        state.articulation.rightFootPressure.heelMedial + state.articulation.rightFootPressure.heelLateral,
        state.articulation.rightFootPressure.forefootMedial + state.articulation.rightFootPressure.forefootLateral);
    setPressureBalanceChannel(
        'leftFootLateralChannel',
        'leftFootLateralChannelValue',
        state.articulation.leftFootPressure.heelMedial + state.articulation.leftFootPressure.forefootMedial,
        state.articulation.leftFootPressure.heelLateral + state.articulation.leftFootPressure.forefootLateral);
    setPressureBalanceChannel(
        'rightFootLateralChannel',
        'rightFootLateralChannelValue',
        state.articulation.rightFootPressure.heelMedial + state.articulation.rightFootPressure.forefootMedial,
        state.articulation.rightFootPressure.heelLateral + state.articulation.rightFootPressure.forefootLateral);
    renderMuscleTelemetry(state.articulation.muscles);
    setText('worldElapsed', `${numberValue(snapshot, 'elapsedSeconds').toFixed(1)} s`);
    setText('worldVisited', `${integerValue(snapshot, 'visitedTerrainCells').toLocaleString()} / ${integerValue(snapshot, 'explorableTerrainCells').toLocaleString()}`);
    setText('worldMotorDispatch', integerValue(snapshot, 'neuronalMotorDispatchTotal').toLocaleString());
    const sensoryFrames = integerValue(snapshot, 'retinalFramesAccepted') + integerValue(snapshot, 'cochlearFramesAccepted') +
        integerValue(snapshot, 'physicalBodyFramesAccepted') + integerValue(snapshot, 'somaticFramesAccepted');
    setText('worldSensoryFrames', sensoryFrames.toLocaleString());
    setText('worldHandSequence', `${integerValue(snapshot, 'handContacts').toLocaleString()} / ${integerValue(snapshot, 'holds').toLocaleString()} / ${integerValue(snapshot, 'releases').toLocaleString()}`);
    setText('worldHeading', `${String(Math.round(state.targetHeading)).padStart(3, '0')} deg`);
    document.querySelector('.world-compass > i').style.transform = `rotate(${state.targetHeading - 90}deg)`;

    const status = envelope.status === 'live' ? 'Live world' : envelope.status === 'stale' ? 'Stale snapshot' : 'World paused';
    setText('worldPresence', status);
    setText('worldSnapshotAge', `${Math.round(envelope.ageSeconds)}s old`);
    setText('worldTelemetryAge', envelope.status);
    document.getElementById('worldPresenceDot').className = `status-dot ${envelope.status === 'live' ? 'online' : envelope.status === 'stale' ? 'degraded' : 'offline'}`;
    const predatorNote = booleanValue(snapshot, 'predatorsSuspended')
        ? 'predators suspended for motor learning'
        : `${predators?.length ?? 0} predators`;
    const entityNote = foods ? `${foods.length} food, ${devices?.length ?? 0} devices, ${predatorNote}.` :
        'Live avatar pose; this older snapshot has no entity coordinates.';
    const handMisses = integerValue(snapshot, 'graspMisses');
    const fatigueReleases = integerValue(snapshot, 'fatigueReleases');
    setText('worldTelemetryLog', `${status}. ${entityNote} Last interaction: ${textValue(snapshot, 'lastInteractionOutcome') || 'none'}. Hand misses: ${handMisses.toLocaleString()}; fatigue releases: ${fatigueReleases.toLocaleString()}.`);
}

function applyWorldOffline(state, message) {
    setText('worldPresence', 'Offline preview');
    setText('worldSnapshotAge', 'Awaiting WorldSim');
    setText('worldTelemetryAge', 'offline');
    setText('worldTelemetryLog', `${message} Seeded preview remains available.`);
    setText('avatarBrainLink', 'Offline');
    document.getElementById('worldPresenceDot').className = 'status-dot offline';
}

function setMeter(id, outputId, fraction) {
    document.getElementById(id).style.width = `${Math.round(fraction * 100)}%`;
    setText(outputId, `${Math.round(fraction * 100)}%`);
}

function setDrive(id, outputId, value) {
    document.getElementById(id).style.width = `${Math.round(clamp(Math.abs(value), 0, 1) * 100)}%`;
    setText(outputId, value.toFixed(2));
}

function renderMuscleTelemetry(muscles) {
    const host = document.getElementById('muscleTelemetryList');
    if (!host) return;
    host.replaceChildren();
    const ordered = [...(muscles ?? [])].sort((left, right) => {
        const side = textValue(left, 'side').localeCompare(textValue(right, 'side'));
        return side || textValue(left, 'name').localeCompare(textValue(right, 'name'));
    });
    for (const muscle of ordered) {
        const activation = clamp(numberValue(muscle, 'activation'), 0, 1);
        const force = Math.max(0, numberValue(muscle, 'forceNewtons'));
        const fatigue = clamp(numberValue(muscle, 'fatigueFraction'), 0, 1);
        const row = document.createElement('div');
        row.className = 'muscle-row';
        const label = document.createElement('span');
        label.textContent = `${textValue(muscle, 'side')} ${splitCamel(textValue(muscle, 'name'))}`;
        label.title = `${Math.round(activation * 100)}% activation, ${Math.round(force)} N, ${Math.round(fatigue * 100)}% fatigue`;
        const track = document.createElement('div');
        const bar = document.createElement('i');
        bar.style.width = `${Math.round(activation * 100)}%`;
        if (fatigue > 0.5) bar.classList.add('fatigued');
        track.appendChild(bar);
        const output = document.createElement('output');
        output.textContent = `${Math.round(force)} N`;
        row.append(label, track, output);
        host.appendChild(row);
    }
    setText('muscleTelemetrySummary', `${ordered.length} muscles`);
}

function splitCamel(text) {
    return text.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function capitalize(text) {
    return text ? `${text[0].toUpperCase()}${text.slice(1)}` : '';
}

function readFootPressure(target, source) {
    if (!source || typeof source !== 'object') return;
    target.heelMedial = numberValue(source, 'heelMedialLoadNewtons');
    target.heelLateral = numberValue(source, 'heelLateralLoadNewtons');
    target.forefootMedial = numberValue(source, 'forefootMedialLoadNewtons');
    target.forefootLateral = numberValue(source, 'forefootLateralLoadNewtons');
}

function setSignedChannel(id, outputId, value, fullScale) {
    const bar = document.getElementById(id);
    if (!bar) return;
    const normalized = clamp(value / fullScale, -1, 1);
    const width = Math.abs(normalized) * 50;
    bar.style.left = `${normalized < 0 ? 50 - width : 50}%`;
    bar.style.width = `${width}%`;
    bar.classList.toggle('negative', normalized < 0);
    const degrees = THREE.MathUtils.radToDeg(value);
    setText(outputId, `${degrees > 0 ? '+' : ''}${degrees.toFixed(1)} deg`);
}

function setUnsignedChannel(id, outputId, value, fullScale, formatter) {
    const bar = document.getElementById(id);
    if (!bar) return;
    bar.style.left = '0';
    bar.style.width = `${clamp(value / fullScale, 0, 1) * 100}%`;
    bar.classList.remove('negative');
    setText(outputId, formatter(value));
}

function setPressureBalanceChannel(id, outputId, firstLoad, secondLoad) {
    const bar = document.getElementById(id);
    if (!bar) return;
    const total = Math.max(0, firstLoad) + Math.max(0, secondLoad);
    const normalized = total > 0.001 ? clamp((secondLoad - firstLoad) / total, -1, 1) : 0;
    const width = Math.abs(normalized) * 50;
    bar.style.left = `${normalized < 0 ? 50 - width : 50}%`;
    bar.style.width = `${width}%`;
    bar.classList.toggle('negative', normalized < 0);
    setText(outputId, `${Math.round(firstLoad)} / ${Math.round(secondLoad)} N`);
}

function clampJoint(value, joint) {
    const limits = JOINT_LIMITS[joint];
    return limits ? clamp(value, limits[0], limits[1]) : value;
}

function resize(state) {
    const width = Math.max(1, state.host.clientWidth);
    const height = Math.max(1, state.host.clientHeight);
    state.camera.aspect = width / height;
    state.camera.updateProjectionMatrix();
    state.renderer.setSize(width, height, false);
}

function frameAvatar(state, immediate) {
    const focus = state.targetPosition.clone().add(new THREE.Vector3(0, 1.0, 0));
    const desired = focus.clone().add(new THREE.Vector3(17, 12, 19));
    if (immediate) {
        state.camera.position.copy(desired);
        state.controls.target.copy(focus);
    } else {
        state.camera.position.lerp(desired, 0.3);
        state.controls.target.lerp(focus, 0.3);
    }
}

function terrainTopAt(heights, worldX, worldZ) {
    return (heightUnitsAtWorld(heights, worldX, worldZ) * TERRAIN_HEIGHT_UNIT) -
        TERRAIN_HALF_HEIGHT_UNIT;
}

function heightUnitsAtWorld(heights, worldX, worldZ) {
    const half = (WORLD_SIZE - 1) * 0.5;
    const gridX = clamp(worldX + half, 0, WORLD_SIZE - 1);
    const gridZ = clamp(worldZ + half, 0, WORLD_SIZE - 1);
    const x0 = Math.floor(gridX);
    const z0 = Math.floor(gridZ);
    const x1 = Math.min(WORLD_SIZE - 1, x0 + 1);
    const z1 = Math.min(WORLD_SIZE - 1, z0 + 1);
    const h00 = heights[x0][z0];
    const h10 = heights[x1][z0];
    const h01 = heights[x0][z1];
    const h11 = heights[x1][z1];
    const containsCliff = Math.abs(h10 - h00) >= CLIFF_THRESHOLD_HEIGHT_UNITS ||
        Math.abs(h11 - h01) >= CLIFF_THRESHOLD_HEIGHT_UNITS ||
        Math.abs(h01 - h00) >= CLIFF_THRESHOLD_HEIGHT_UNITS ||
        Math.abs(h11 - h10) >= CLIFF_THRESHOLD_HEIGHT_UNITS;
    if (containsCliff) {
        const nearestX = clamp(Math.floor(gridX + 0.5), 0, WORLD_SIZE - 1);
        const nearestZ = clamp(Math.floor(gridZ + 0.5), 0, WORLD_SIZE - 1);
        return heights[nearestX][nearestZ];
    }

    const tx = smoothStep(gridX - x0);
    const tz = smoothStep(gridZ - z0);
    return clamp(
        Math.round(lerp(lerp(h00, h10, tx), lerp(h01, h11, tx), tz)),
        MINIMUM_TERRAIN_HEIGHT,
        MAXIMUM_TERRAIN_HEIGHT);
}

function visualVoxelCoordinate(cell, subdivision, half) {
    return (cell - half) + (((subdivision + 0.5) / VISUAL_SUBDIVISIONS) - 0.5);
}

function localSlope(heights, x, z) {
    const left = heights[Math.max(0, x - 1)][z];
    const right = heights[Math.min(WORLD_SIZE - 1, x + 1)][z];
    const back = heights[x][Math.max(0, z - 1)];
    const front = heights[x][Math.min(WORLD_SIZE - 1, z + 1)];
    return Math.max(Math.abs(right - left), Math.abs(front - back));
}

function fractalNoise(x, z, octaves, persistence) {
    let amplitude = 1;
    let frequency = 1;
    let total = 0;
    let maximum = 0;
    for (let index = 0; index < octaves; index++) {
        total += valueNoise(x * frequency, z * frequency) * amplitude;
        maximum += amplitude;
        amplitude *= persistence;
        frequency *= 2;
    }
    return maximum <= 1e-9 ? 0 : total / maximum;
}

function valueNoise(x, z) {
    const xi = Math.floor(x);
    const zi = Math.floor(z);
    const tx = x - xi;
    const tz = z - zi;
    const sx = smoothStep(tx);
    const sz = smoothStep(tz);
    return lerp(
        lerp(hash01(xi, zi), hash01(xi + 1, zi), sx),
        lerp(hash01(xi, zi + 1), hash01(xi + 1, zi + 1), sx),
        sz);
}

function hash01(x, z) {
    let n = (Math.imul(x | 0, 374761393) + Math.imul(z | 0, 668265263)) | 0;
    n = Math.imul(n ^ (n >> 13), 1274126177);
    n ^= n >> 16;
    return (n & 0x7fffffff) / 0x7fffffff;
}

function mulberry32(seed) {
    let value = seed | 0;
    return () => {
        value = (value + 0x6D2B79F5) | 0;
        let result = value;
        result = Math.imul(result ^ (result >>> 15), result | 1);
        result ^= result + Math.imul(result ^ (result >>> 7), result | 61);
        return ((result ^ (result >>> 14)) >>> 0) / 4294967296;
    };
}

function arrayValue(object, name) {
    const result = value(object, name);
    return Array.isArray(result) ? result : null;
}

function booleanValue(object, name) {
    return Boolean(value(object, name));
}

function numberValue(object, name) {
    const result = Number(value(object, name));
    return Number.isFinite(result) ? result : 0;
}

function integerValue(object, name) {
    return Math.round(numberValue(object, name));
}

function textValue(object, name) {
    const result = value(object, name);
    return result == null ? '' : String(result);
}

function value(object, camelName) {
    if (!object || typeof object !== 'object') {
        return undefined;
    }
    if (Object.hasOwn(object, camelName)) {
        return object[camelName];
    }
    const pascalName = camelName.charAt(0).toUpperCase() + camelName.slice(1);
    return object[pascalName];
}

function setText(id, text) {
    const element = document.getElementById(id);
    if (element) {
        element.textContent = text;
    }
}

function normalizeDegrees(value) {
    return ((value % 360) + 360) % 360;
}

function lerpAngle(current, target, amount) {
    const delta = ((target - current + 540) % 360) - 180;
    return normalizeDegrees(current + delta * amount);
}

function smoothStep(value) {
    return value * value * (3 - (2 * value));
}

function lerp(a, b, amount) {
    return a + ((b - a) * amount);
}

function clamp(value, minimum, maximum) {
    return Math.max(minimum, Math.min(maximum, value));
}

function disposeChildren(root) {
    root.traverse(object => {
        object.geometry?.dispose?.();
        if (Array.isArray(object.material)) {
            object.material.forEach(material => material.dispose?.());
        } else {
            object.material?.dispose?.();
        }
    });
}
