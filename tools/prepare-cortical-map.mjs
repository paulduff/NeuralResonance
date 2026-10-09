// node tools/prepare-cortical-map.mjs <operator reference.zip> <output.json>
// This prepares display annotations. It changes neither atlas IDs nor neurons.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { createHash } from 'node:crypto';
import * as THREE from '../src/NRE.BlazorEditor/wwwroot/vendor/three/three.module.min.js';
import { unzipSync } from '../src/NRE.BlazorEditor/wwwroot/vendor/three/addons/libs/fflate.module.js';
import { FBXLoader } from '../src/NRE.BlazorEditor/wwwroot/vendor/three/addons/loaders/FBXLoader.js';
import { GLTFLoader } from '../src/NRE.BlazorEditor/wwwroot/vendor/three/addons/loaders/GLTFLoader.js';
import { registerBrainShell, disposeBrainObject } from '../src/NRE.BlazorEditor/wwwroot/js/brain-shell.js';
import { readCorticalSurface } from '../src/NRE.BlazorEditor/wwwroot/js/brain-surface.js';

const [referencePath, outputPath] = process.argv.slice(2);
if (!referencePath || !outputPath || !/\.json$/i.test(outputPath) || resolve(referencePath) === resolve(outputPath)) {
    throw new Error('Provide reference ZIP and a separate output JSON path.');
}
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const archive = await readFile(referencePath);
const fbx = unzipSync(archive)['source/brain_edit_10.fbx'];
if (!fbx || hash(fbx) !== '970606342529f92a1e551585ba32f0f61311403c5b49c6aab07d0b20e0fa25fd') {
    throw new Error('Reference differs from the inspected labelled model; review its names and axes before mapping.');
}
const reference = new FBXLoader().parse(fbx.buffer.slice(fbx.byteOffset, fbx.byteOffset + fbx.byteLength), '');
reference.updateMatrixWorld(true);
const assetRoot = new URL('../src/NRE.BlazorEditor/wwwroot/data/', import.meta.url);
const shellBytes = await readFile(new URL('models/brain-shell.glb', assetRoot));
const shell = registerBrainShell((await new GLTFLoader().parseAsync(shellBytes.buffer.slice(shellBytes.byteOffset, shellBytes.byteOffset + shellBytes.byteLength), '')).scene);
const surface = readCorticalSurface(shell);
const atlas = JSON.parse((await readFile(new URL('brain-atlas.json', assetRoot), 'utf8')).replace(/^\uFEFF/, ''));
const structures = atlas.structures.filter(s => s.layout === 'CorticalSheet');

// These are anatomical constraints for illustrative functional subdivisions.
// They do not assert cytoarchitectonic boundaries, language dominance or an
// independent right-hemisphere registration. Insula has no named reference.
const rules = {
    V1: ['cuneus','lingual','occipital'], V2: ['cuneus','lingual','occipital'],
    V3: ['cuneus','lingual','occipital'], V4: ['cuneus','lingual','occipital'],
    Mt: ['middletemporal','inferiortemporal','occipital'],
    A1: ['superiortemporal'], AuditoryAssociationCortex: ['superiortemporal'],
    WernickePstgPsts: ['superiortemporal'],
    S1: ['postcentral','paracentrallobule'],
    SecondarySomatosensoryCortex: ['supramarginal','parietallobe'],
    EntorhinalCortex: ['parahippocampal'], ParahippocampalCortex: ['parahippocampal'],
    PerirhinalCortex: ['parahippocampal','inferiortemporal'],
    Pfc: ['middlefrontalgyrus','superiorfrontal','inferiorfrontalgyrus'],
    DorsomedialPrefrontalCortex: ['superiorfrontal'],
    VentromedialPrefrontalCortex: ['medialorbitalfrontal'],
    FrontalEyeFields: ['middlefrontalgyrus','superiorfrontal'],
    BrocaBa44Ba45: ['inferiorfrontalgyrus'],
    SupramarginalAngular: ['supramarginal','parietallobe'],
    PremotorCortex: ['middlefrontalgyrus','superiorfrontal'],
    OrbitofrontalCortex: ['lateralorbitalfrontalcortex','medialorbitalfrontal'],
    Insula: [], Ppc: ['parietallobe'],
    TemporalAssociation: ['middletemporal','superiortemporal'],
    InferotemporalCortex: ['inferiortemporal','middletemporal'], FusiformGyrus: ['fusiform'],
    TemporalPole: ['temporalpole'], TemporoparietalJunction: ['supramarginal','superiortemporal','parietallobe'],
    Precuneus: ['precuneus'], MidcingulateCortex: ['cingulategyrus'],
    PosteriorCingulate: ['cingulategyrus'], RetrosplenialCortex: ['cingulategyrus'], Acc: ['cingulategyrus'],
    Sma: ['paracentrallobule','superiorfrontal'], M1: ['precentral','paracentrallobule']
};
const labels = {
    precentral:'Precentral gyrus', postcentral:'Postcentral gyrus', paracentrallobule:'Paracentral lobule',
    superiorfrontal:'Superior frontal gyrus', middlefrontalgyrus:'Middle frontal gyrus',
    inferiorfrontalgyrus:'Inferior frontal gyrus', lateralorbitalfrontalcortex:'Lateral orbitofrontal cortex',
    medialorbitalfrontal:'Medial orbitofrontal cortex', cingulategyrus:'Cingulate gyrus',
    parietallobe:'Parietal lobe', supramarginal:'Supramarginal gyrus', precuneus:'Precuneus',
    superiortemporal:'Superior temporal gyrus', middletemporal:'Middle temporal gyrus',
    inferiortemporal:'Inferior temporal gyrus', fusiform:'Fusiform gyrus', temporalpole:'Temporal pole',
    parahippocampal:'Parahippocampal gyrus', cuneus:'Cuneus', lingual:'Lingual gyrus', occipital:'Occipital cortex'
};
const canonical = name => name.toLowerCase().replace(/[_.\s]/g, '').replace(/\d+$/, '').replace(/^occipitallobe$/, 'occipital');
const referenceBounds = new THREE.Box3();
const referenceTriangles = [], gyri = [], gyrusByKey = new Map();
reference.traverse(mesh => {
    if (!mesh.isMesh) return;
    const key = canonical(mesh.name);
    if (!labels[key]) return; // Never assign generic/unmeasured objects to nuclei.
    const geometry = mesh.geometry.clone().applyMatrix4(mesh.matrixWorld);
    const positions = geometry.attributes.position, index = geometry.index;
    const count = index?.count ?? positions.count;
    if (!count) { geometry.dispose(); return; }
    if (!gyrusByKey.has(key)) {
        gyrusByKey.set(key, gyri.length);
        gyri.push({ key, name: labels[key], color: '#' + (mesh.material.color?.getHexString() ?? '758fa8'), sourceMeshes: [] });
    }
    const gyrus = gyrusByKey.get(key);
    gyri[gyrus].sourceMeshes.push(mesh.name);
    const vertices = [new THREE.Vector3(), new THREE.Vector3(), new THREE.Vector3()];
    for (let i = 0; i < count; i += 3) {
        vertices.forEach((v, j) => v.fromBufferAttribute(positions, index ? index.getX(i + j) : i + j));
        vertices.forEach(v => referenceBounds.expandByPoint(v));
        referenceTriangles.push({ point: vertices[0].clone().add(vertices[1]).add(vertices[2]).multiplyScalar(1/3).toArray(), gyrus });
    }
    geometry.dispose();
});
if (gyri.length !== 21) throw new Error(`Unexpected reference labels: ${gyri.length}.`);
const unmappedGyrus = gyri.length;
gyri.push({ key:'unmapped', name:'Atlas-guided / no named reference', color:'#657382', sourceMeshes:[] });
const bounds = { L: new THREE.Box3(), R: new THREE.Box3() };
for (const triangle of surface.triangles) {
    for (let v = 0; v < 9; v += 3) bounds[triangle.side].expandByPoint(new THREE.Vector3(...triangle.positions.slice(v,v+3)));
}
const nativeSize = referenceBounds.getSize(new THREE.Vector3());
const registeredPoints = {};
for (const side of ['L','R']) {
    const b = bounds[side], size = b.getSize(new THREE.Vector3());
    const sign = side === 'L' ? -1 : 1;
    const lateralExtent = side === 'L' ? -b.min.x : b.max.x;
    registeredPoints[side] = referenceTriangles.map(({ point:p, gyrus }) => ({ gyrus, point: [
        sign * (referenceBounds.max.x - p[0]) / nativeSize.x * lateralExtent,
        b.min.y + (p[1] - referenceBounds.min.y) / nativeSize.y * size.y,
        b.max.z - (p[2] - referenceBounds.min.z) / nativeSize.z * size.z
    ] }));
}
const squaredDistance = (a,b) => a.reduce((sum,x,axis) => sum+(x-b[axis])**2,0);
// A balanced point tree keeps the offline transfer independent of third-party
// Python packages and avoids an all-pairs surface comparison.
function pointTree(points, indices = points.map((_,i)=>i), depth=0) {
    if (!indices.length) return null;
    const axis = depth % 3;
    indices.sort((a,b) => points[a].point[axis]-points[b].point[axis] || a-b);
    const middle = indices.length >> 1;
    return { index:indices[middle], axis, left:pointTree(points,indices.slice(0,middle),depth+1), right:pointTree(points,indices.slice(middle+1),depth+1) };
}
function nearestPoint(points, tree, point) {
    let best = null, distance = Infinity;
    function visit(node) {
        if (!node) return;
        const sample = points[node.index], d = squaredDistance(sample.point, point);
        if (d < distance) { best=sample; distance=d; }
        const delta = point[node.axis]-sample.point[node.axis];
        visit(delta < 0 ? node.left : node.right);
        if (delta*delta < distance) visit(delta < 0 ? node.right : node.left);
    }
    visit(tree); return { sample:best, distance:Math.sqrt(distance) };
}
const trees = { L:pointTree(registeredPoints.L), R:pointTree(registeredPoints.R) };
const territories = structures.map(s => ({ instanceId:s.instanceId, structureId:s.structureId, name:s.displayName,
    hemisphere:s.hemisphere, referenceGyri:rules[s.structureId], method:rules[s.structureId]?.length ? 'gyrus-guided' : 'atlas-guided',
    originalAnchorMm:s.centerMm, anchorMm:null, triangleCount:0 }));
if (territories.some(t=>!t.referenceGyri)) throw new Error('Every cortical ID needs an explicit mapping policy.');
const diagnostics = [];
for (const triangle of surface.triangles) {
    const nearest = nearestPoint(registeredPoints[triangle.side],trees[triangle.side],triangle.centroid);
    triangle.gyrus=nearest.sample.gyrus;
    triangle.referenceDistanceMm=nearest.distance;
    const candidates = structures.filter(s=>s.hemisphere===triangle.side);
    const old = candidates.reduce((best,s)=>squaredDistance(triangle.centroid,s.centerMm)<squaredDistance(triangle.centroid,best.centerMm)?s:best);
    // Keep the atlas-only insular representation rather than claiming an exact
    // insular label from a source that does not name it.
    triangle.atlasReserved = rules[old.structureId].length===0 ? old.instanceId : null;
    diagnostics.push(nearest.distance);
}
const usedSeeds = new Set();
const eligible = territory => surface.triangles.filter(t => t.side===territory.hemisphere &&
    (territory.method==='atlas-guided' ? t.atlasReserved===territory.instanceId :
        !t.atlasReserved && territory.referenceGyri.includes(gyri[t.gyrus].key)));
const seedOrder = territories.map(t=>({territory:t,cells:eligible(t)})).sort((a,b)=>a.cells.length-b.cells.length);
for (const {territory,cells} of seedOrder) {
    if (!cells.length) throw new Error(`No surface cells available for ${territory.instanceId}.`);
    let seed=null, minimum=Infinity;
    for (const cell of cells) {
        if (usedSeeds.has(cell)) continue;
        const distance=squaredDistance(cell.centroid,territory.originalAnchorMm);
        if (distance<minimum) { seed=cell; minimum=distance; }
    }
    if (!seed) throw new Error(`No distinct seed for ${territory.instanceId}.`);
    territory.anchorMm=seed.centroid;
    territory.anchorShiftMm=Math.sqrt(minimum);
    usedSeeds.add(seed); seed.seedOwner=territories.indexOf(territory);
}
for (const triangle of surface.triangles) {
    const candidates = territories.filter(t=>t.hemisphere===triangle.side &&
        (triangle.atlasReserved ? t.instanceId===triangle.atlasReserved : t.referenceGyri.includes(gyri[triangle.gyrus].key)));
    if (!candidates.length) throw new Error(`No functional subdivision for ${gyri[triangle.gyrus].name}.`);
    const owner = triangle.seedOwner !== undefined ? territories[triangle.seedOwner] :
        candidates.reduce((best,t)=>squaredDistance(triangle.centroid,t.anchorMm)<squaredDistance(triangle.centroid,best.anchorMm)?t:best);
    triangle.owner=territories.indexOf(owner);
    if (owner.method==='atlas-guided') triangle.gyrus=unmappedGyrus;
    owner.triangleCount++;
}
const meshAssignments = [];
for (const triangle of surface.triangles) {
    let entry=meshAssignments.at(-1);
    if (!entry || entry.mesh!==triangle.meshName) {
        entry={mesh:triangle.meshName,regions:[],gyri:[]};meshAssignments.push(entry);
    }
    entry.regions.push(triangle.owner);entry.gyri.push(triangle.gyrus);
}
const distances = [...diagnostics].sort((a,b)=>a-b);
const report = {
    schemaVersion:1, shellSha256:hash(shellBytes), shellTriangleCount:surface.triangles.length,
    reference:{ title:'Detailed Labeled Surface Structures', author:'Ruth Lilly Medical Library, IU School of Medicine',
        source:'https://sketchfab.com/3d-models/detailed-labeled-surface-structures-d91080b5af91497b822728c08804f33c',
        upstreamSource:'https://www.myminifactory.com/object/3d-print-brain-72477',
        publisherPreparation:'Labelled surface model in progress; consultation from Dr. Garcia and Dr. Eckel.',
        license:'CC BY-NC-SA 4.0', licenseUrl:'https://creativecommons.org/licenses/by-nc-sa/4.0/',
        archiveSha256:hash(archive), fbxSha256:hash(fbx), sourceHemisphere:'Single hemisphere; anatomical side not established by source labels',
        modifications:'Named gyral annotations transferred to operator shell; mirrored template on both displayed hemispheres; illustrative DNNE subdivisions.',
        registration:'Reference +Z posterior converted to display +Z anterior; cortical envelope fit per hemisphere. Annotation coordinates fitted; display shell geometry unchanged.',
        limitation:'Model in progress. Envelope registration and shared-gyrus subdivisions are illustrative; no validated histological boundaries or independent right-hemisphere anatomy.' },
    coordinateSystem:'DNNE display millimetres; X left negative, Y superior, Z anterior',
    referenceBounds:{min:referenceBounds.min.toArray(),max:referenceBounds.max.toArray()},
    corticalBounds:Object.fromEntries(Object.entries(bounds).map(([side,b])=>[side,{min:b.min.toArray(),max:b.max.toArray()}])),
    referenceDistanceMm:{median:distances[Math.floor(distances.length/2)],p95:distances[Math.floor(distances.length*.95)],maximum:distances.at(-1)},
    gyri, territories, meshAssignments
};
if (territories.some(t=>t.triangleCount===0)) throw new Error('Mapping would remove a cortical territory.');
await mkdir(dirname(outputPath),{recursive:true});
await writeFile(outputPath,JSON.stringify(report)+'\n');
console.log(JSON.stringify({ triangles:report.shellTriangleCount,gyri:gyri.length-1,territories:territories.length,
    guided:territories.filter(t=>t.method==='gyrus-guided').length,distanceMm:report.referenceDistanceMm,
    counts:territories.filter(t=>t.hemisphere==='L').map(t=>({id:t.structureId,triangles:t.triangleCount,shift:t.anchorShiftMm})) },null,2));
disposeBrainObject(reference);disposeBrainObject(shell);
