import * as THREE from '../vendor/three/three.module.min.js';

const corticalParts = new Set(['frontal1', 'pariet1', 'temp1', 'occipit1']);

// Partition the real folded surface by the existing DNNE atlas anchors. This
// is an illustrative nearest-anchor mapping, not a histological parcellation.
export function buildCorticalSurfaceParcels(model, territories) {
    model.updateMatrixWorld(true);
    const parcels = new Map();
    const corticalShells = [];
    let sourceTriangles = 0;
    const candidates = { L: [], R: [] };
    for (const territory of territories) candidates[territory.userData.structure.hemisphere].push(territory);
    for (const side of ['L', 'R']) {
        if (!candidates[side].length) throw new Error(`Missing ${side} cortical anchors.`);
    }
    model.traverse(mesh => {
        if (!mesh.isMesh || !corticalParts.has(mesh.userData.anatomicalPart)) return;
        corticalShells.push(mesh);
        const geometry = mesh.geometry.clone().applyMatrix4(mesh.matrixWorld);
        const positions = geometry.attributes.position;
        const normals = geometry.attributes.normal;
        const indices = geometry.index;
        const count = indices?.count ?? positions.count;
        const points = [new THREE.Vector3(), new THREE.Vector3(), new THREE.Vector3()];
        for (let index = 0; index < count; index += 3) {
            const vertexIds = [0, 1, 2].map(offset => indices ? indices.getX(index + offset) : index + offset);
            points.forEach((point, offset) => point.fromBufferAttribute(positions, vertexIds[offset]));
            const centroid = points[0].clone().add(points[1]).add(points[2]).multiplyScalar(1 / 3);
            const side = centroid.x < 0 ? 'L' : 'R';
            let nearest = null, minimum = Infinity;
            for (const territory of candidates[side]) {
                const distance = centroid.distanceToSquared(territory.userData.focusPoint);
                if (distance < minimum) { nearest = territory; minimum = distance; }
            }
            const id = nearest.userData.structure.instanceId;
            if (!parcels.has(id)) parcels.set(id, { positions: [], normals: [], focusPoint: centroid.clone(), closest: minimum });
            const parcel = parcels.get(id);
            if (minimum < parcel.closest) { parcel.focusPoint.copy(centroid); parcel.closest = minimum; }
            for (const vertex of vertexIds) {
                parcel.positions.push(positions.getX(vertex), positions.getY(vertex), positions.getZ(vertex));
                parcel.normals.push(normals.getX(vertex), normals.getY(vertex), normals.getZ(vertex));
            }
            sourceTriangles++;
        }
        geometry.dispose();
    });
    for (const parcel of parcels.values()) {
        const geometry = new THREE.BufferGeometry();
        geometry.setAttribute('position', new THREE.Float32BufferAttribute(parcel.positions, 3));
        geometry.setAttribute('normal', new THREE.Float32BufferAttribute(parcel.normals, 3));
        geometry.computeBoundingSphere();
        parcel.geometry = geometry;
        delete parcel.positions;
        delete parcel.normals;
    }
    return { parcels, corticalShells, sourceTriangles };
}
