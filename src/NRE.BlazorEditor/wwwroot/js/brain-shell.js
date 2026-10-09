import * as THREE from '../vendor/three/three.module.min.js';
import { GLTFLoader } from '../vendor/three/addons/loaders/GLTFLoader.js';

// Registration is illustrative, not a medical atlas registration. Source +X
// points anteriorly; the editor uses +Z anterior and +Y superior.
export function registerBrainShell(model) {
    model.rotation.y = -Math.PI / 2;
    model.updateMatrixWorld(true);
    const bounds = new THREE.Box3().setFromObject(model);
    const size = bounds.getSize(new THREE.Vector3());
    if (![size.x, size.y, size.z].every(value => Number.isFinite(value) && value > 0)) {
        throw new Error('Brain shell has invalid bounds.');
    }
    const container = new THREE.Group();
    container.name = 'imported-brain-shell';
    container.add(model);
    container.scale.set(142 / size.x, 140 / size.y, 172 / size.z);
    container.updateMatrixWorld(true);
    const center = new THREE.Box3().setFromObject(container).getCenter(new THREE.Vector3());
    container.position.copy(new THREE.Vector3(0, -17, 2).sub(center));
    return container;
}

export async function loadBrainShell(opacity) {
    const gltf = await new GLTFLoader().loadAsync('/data/models/brain-shell.glb');
    try {
        const model = registerBrainShell(gltf.scene);
        const shells = [];
        const sourceMaterials = new Set();
        model.traverse(object => {
            if (!object.isMesh) return;
            sourceMaterials.add(object.material);
            object.material = new THREE.MeshPhysicalMaterial({
                color: 0x9bb4bd, transparent: true, opacity, depthWrite: false,
                roughness: 0.72, metalness: 0, side: THREE.FrontSide
            });
            object.renderOrder = 1;
            object.userData.isShell = true;
            shells.push(object);
        });
        sourceMaterials.forEach(material => material.dispose());
        return { model, shells };
    } catch (error) {
        disposeBrainObject(gltf.scene);
        throw error;
    }
}

export function disposeBrainObject(root) {
    const geometries = new Set();
    const materials = new Set();
    const textures = new Set();
    const images = new Set();
    root.traverse(object => {
        if (object.geometry) geometries.add(object.geometry);
        for (const material of [object.material].flat().filter(Boolean)) {
            materials.add(material);
            for (const property of Object.values(material)) {
                if (property?.isTexture) textures.add(property);
            }
        }
    });
    for (const texture of textures) {
        if (texture.source?.data?.close) images.add(texture.source.data);
        texture.dispose();
    }
    images.forEach(image => image.close());
    geometries.forEach(geometry => geometry.dispose());
    materials.forEach(material => material.dispose());
}
