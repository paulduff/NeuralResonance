import * as THREE from '../vendor/three/three.module.min.js';

const corticalParts = new Set(['frontal1', 'pariet1', 'temp1', 'occipit1']);

// One ordered read is used by the offline annotation transfer and the viewer.
// Positions/normals stay on the supplied shell; annotations never deform it.
export function readCorticalSurface(model) {
    model.updateMatrixWorld(true);
    const corticalShells = [];
    const triangles = [];
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
            triangles.push({ meshName: mesh.name, triangleIndex: index / 3,
                centroid: centroid.toArray(), side: centroid.x < 0 ? 'L' : 'R',
                positions: vertexIds.flatMap(v => [positions.getX(v), positions.getY(v), positions.getZ(v)]),
                normals: vertexIds.flatMap(v => [normals.getX(v), normals.getY(v), normals.getZ(v)]) });
        }
        geometry.dispose();
    });
    return { triangles, corticalShells };
}

export function validateCorticalMap(mapping, triangles, territories) {
    if (mapping.schemaVersion !== 1 || mapping.shellTriangleCount !== triangles.length ||
        !Array.isArray(mapping.gyri) || !Array.isArray(mapping.territories) || !Array.isArray(mapping.meshAssignments)) {
        throw new Error('Invalid cortical map schema or shell triangle count.');
    }
    const definitions = new Map(territories.map(t => [t.userData.structure.instanceId, t.userData.structure]));
    const seen = new Set();
    for (const territory of mapping.territories) {
        const definition = definitions.get(territory.instanceId);
        if (!definition || seen.has(territory.instanceId) || territory.hemisphere !== definition.hemisphere ||
            !['gyrus-guided','atlas-guided'].includes(territory.method) || !Array.isArray(territory.referenceGyri) ||
            !Array.isArray(territory.anchorMm) || territory.anchorMm.length !== 3 || !territory.anchorMm.every(Number.isFinite)) {
            throw new Error('Invalid or duplicated cortical territory.');
        }
        seen.add(territory.instanceId);
    }
    if (seen.size !== definitions.size) throw new Error('Incomplete cortical map.');
    for (const gyrus of mapping.gyri) {
        if (typeof gyrus.name !== 'string' || !/^#[0-9a-f]{6}$/i.test(gyrus.color)) throw new Error('Invalid gyral label/colour.');
    }
    const byMesh = new Map();
    for (const assignment of mapping.meshAssignments) {
        if (byMesh.has(assignment.mesh) || !Array.isArray(assignment.regions) || !Array.isArray(assignment.gyri)) {
            throw new Error('Invalid or duplicated shell mesh assignment.');
        }
        byMesh.set(assignment.mesh, assignment);
    }
    const counts = mapping.territories.map(() => 0);
    const meshCounts = new Map();
    for (const triangle of triangles) {
        const entry = byMesh.get(triangle.meshName);
        const region = entry?.regions[triangle.triangleIndex], label = entry?.gyri[triangle.triangleIndex];
        const territory = Number.isInteger(region) ? mapping.territories[region] : null;
        const gyrus = Number.isInteger(label) ? mapping.gyri[label] : null;
        if (!territory || !gyrus || territory.hemisphere !== triangle.side ||
            (territory.method === 'gyrus-guided' && !territory.referenceGyri.includes(gyrus.key))) {
            throw new Error('Cortical annotation crosses a hemisphere or gyral constraint.');
        }
        counts[region]++;
        meshCounts.set(triangle.meshName, (meshCounts.get(triangle.meshName) ?? 0) + 1);
    }
    if (byMesh.size !== meshCounts.size || [...byMesh].some(([name,a]) =>
        a.regions.length !== meshCounts.get(name) || a.gyri.length !== meshCounts.get(name)) ||
        counts.some((count,i) => count === 0 || count !== mapping.territories[i].triangleCount)) {
        throw new Error('Incomplete or overflowing cortical assignment.');
    }
    return byMesh;
}

// Gyrus-guided assignments are illustrative subdivisions. If no map is
// available, the existing nearest-anchor partition remains a visible fallback.
export function buildCorticalSurfaceParcels(model, territories, mapping = null) {
    const { triangles, corticalShells } = readCorticalSurface(model);
    const assignments = mapping ? validateCorticalMap(mapping, triangles, territories) : null;
    const parcels = new Map(), byId = new Map(territories.map(t => [t.userData.structure.instanceId,t]));
    const candidates = { L: [], R: [] };
    for (const territory of territories) candidates[territory.userData.structure.hemisphere].push(territory);
    for (const side of ['L', 'R']) if (!candidates[side].length) throw new Error(`Missing ${side} cortical anchors.`);
    const colours = mapping?.gyri.map(g => new THREE.Color(g.color).toArray());
    for (const triangle of triangles) {
        const centroid = new THREE.Vector3(...triangle.centroid);
        let nearest, minimum, annotation = null, label = null;
        if (assignments) {
            const assignment = assignments.get(triangle.meshName);
            annotation = mapping.territories[assignment.regions[triangle.triangleIndex]];
            label = assignment.gyri[triangle.triangleIndex];
            nearest = byId.get(annotation.instanceId);
            minimum = centroid.distanceToSquared(new THREE.Vector3(...annotation.anchorMm));
        } else {
            nearest = null; minimum = Infinity;
            for (const territory of candidates[triangle.side]) {
                const distance = centroid.distanceToSquared(territory.userData.focusPoint);
                if (distance < minimum) { nearest = territory; minimum = distance; }
            }
        }
        const id = nearest.userData.structure.instanceId;
        if (!parcels.has(id)) parcels.set(id, { positions: [], normals: [], colours: [],
            focusPoint: centroid.clone(), closest: minimum, annotation, gyrusCounts: new Map() });
        const parcel = parcels.get(id);
        if (minimum < parcel.closest) { parcel.focusPoint.copy(centroid); parcel.closest = minimum; }
        parcel.positions.push(...triangle.positions);parcel.normals.push(...triangle.normals);
        if (label !== null) {
            parcel.colours.push(...colours[label], ...colours[label], ...colours[label]);
            parcel.gyrusCounts.set(mapping.gyri[label].name, (parcel.gyrusCounts.get(mapping.gyri[label].name) ?? 0) + 1);
        }
    }
    for (const parcel of parcels.values()) {
        const geometry = new THREE.BufferGeometry();
        geometry.setAttribute('position', new THREE.Float32BufferAttribute(parcel.positions, 3));
        geometry.setAttribute('normal', new THREE.Float32BufferAttribute(parcel.normals, 3));
        if (parcel.colours.length) geometry.setAttribute('color', new THREE.Float32BufferAttribute(parcel.colours, 3));
        geometry.computeBoundingSphere();
        parcel.geometry = geometry;
        delete parcel.positions;
        delete parcel.normals;
        delete parcel.colours;
    }
    return { parcels, corticalShells, sourceTriangles: triangles.length };
}
