// node tools/prepare-brain-parts.mjs <source directory with manifest.json> <output.glb>
import { readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import * as THREE from '../src/NRE.BlazorEditor/wwwroot/vendor/three/three.module.min.js';
import { VTKLoader } from '../src/NRE.BlazorEditor/wwwroot/vendor/three/addons/loaders/VTKLoader.js';
import { GLTFLoader } from '../src/NRE.BlazorEditor/wwwroot/vendor/three/addons/loaders/GLTFLoader.js';
import { mergeGeometries, mergeVertices } from '../src/NRE.BlazorEditor/wwwroot/vendor/three/addons/utils/BufferGeometryUtils.js';
import { registerBrainShell, disposeBrainObject } from '../src/NRE.BlazorEditor/wwwroot/js/brain-shell.js';
import { writeBrainGeometry } from './brain-glb-writer.mjs';

const [sourceDirectory, outputPath] = process.argv.slice(2);
if (!sourceDirectory || !outputPath || !/\.glb$/i.test(outputPath)) throw new Error('Provide the source directory and output GLB path.');
const manifest = JSON.parse(await readFile(join(sourceDirectory, 'manifest.json'), 'utf8'));
if (!manifest.registrationReferences) throw new Error('Shared assembly registration references are required. Fetch current sources first.');
const assetRoot = new URL('../src/NRE.BlazorEditor/wwwroot/data/', import.meta.url);
const atlas = JSON.parse((await readFile(new URL('brain-atlas.json', assetRoot), 'utf8')).replace(/^\uFEFF/, ''));
const definitions = new Map(atlas.structures.map(s => [s.instanceId, s]));
const shellBytes = await readFile(new URL('models/brain-shell.glb', assetRoot));
const shellSha256 = createHash('sha256').update(shellBytes).digest('hex');
const shell = registerBrainShell((await new GLTFLoader().parseAsync(shellBytes.buffer.slice(shellBytes.byteOffset, shellBytes.byteOffset + shellBytes.byteLength), '')).scene);
shell.updateMatrixWorld(true);
const targets = { cerebrum:new THREE.Box3(), cerebellum:new THREE.Box3() };
shell.traverse(mesh => {
    if (!mesh.isMesh) return;
    const part=mesh.userData.anatomicalPart;
    if (['frontal1','pariet1','temp1','occipit1'].includes(part)) targets.cerebrum.union(new THREE.Box3().setFromObject(mesh));
    if (part==='cereb1') targets.cerebellum.union(new THREE.Box3().setFromObject(mesh));
});
disposeBrainObject(shell);
const rasToDisplay = new THREE.Matrix4().set(1,0,0,0, 0,0,1,0, 0,1,0,0, 0,0,0,1);
const cache=new Map();
async function sourceGeometry(file) {
    if (cache.has(file)) return cache.get(file);
    const bytes=await readFile(join(sourceDirectory,file));
    const source=manifest.sources.find(s=>s.file===file);
    if (!source || createHash('sha256').update(bytes).digest('hex')!==source.sha256) throw new Error(`Source hash changed: ${file}`);
    const raw=new VTKLoader().parse(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength));
    raw.deleteAttribute('normal'); raw.deleteAttribute('color');
    const geometry=mergeVertices(raw, .0001); raw.dispose();
    geometry.applyMatrix4(rasToDisplay);
    // Axis permutation reverses handedness; restore outward winding.
    const indices=geometry.index.array;
    for (let i=0;i<indices.length;i+=3) [indices[i+1],indices[i+2]]=[indices[i+2],indices[i+1]];
    geometry.computeVertexNormals(); geometry.computeBoundingBox();
    cache.set(file,geometry); return geometry;
}
const assemblies={};
for (const [name,files] of Object.entries(manifest.registrationReferences)) {
    const native=new THREE.Box3();
    for (const file of files) native.union((await sourceGeometry(file)).boundingBox);
    const nativeSize=native.getSize(new THREE.Vector3()), targetSize=targets[name].getSize(new THREE.Vector3());
    if (![...nativeSize,...targetSize].every(v=>Number.isFinite(v)&&v>0)) throw new Error('Invalid assembly bounds.');
    const margin=name==='cerebrum'?.92:.97;
    const scale=Math.min(...targetSize.toArray().map((v,i)=>v/nativeSize.getComponent(i)))*margin;
    const translation=targets[name].getCenter(new THREE.Vector3()).sub(native.getCenter(new THREE.Vector3()).multiplyScalar(scale));
    assemblies[name]={uniformScale:scale,translationMm:translation.toArray(),referenceFiles:files,
        nativeBounds:{min:native.min.toArray(),max:native.max.toArray()},
        targetBounds:{min:targets[name].min.toArray(),max:targets[name].max.toArray()}};
}
const parts=[];
for (const part of manifest.parts) {
    const structure=definitions.get(part.instanceId);
    if (!structure) throw new Error(`Unmapped source part ${part.instanceId}`);
    const name=structure.group==='Cerebellum'?'cerebellum':'cerebrum';
    const assembly=assemblies[name];
    const geometries=[];
    for (const file of part.files) geometries.push(await sourceGeometry(file));
    const geometry=mergeGeometries(geometries);
    if (!geometry) throw new Error(`Cannot combine ${part.instanceId}`);
    if (part.populationEnvelope) {
        const b=assembly.nativeBounds, center=b.min.map((v,i)=>(v+b.max[i])/2);
        geometry.translate(...center.map(v=>-v));
        geometry.scale(part.envelopeScale,part.envelopeScale,part.envelopeScale);
        geometry.translate(...center);
    }
    geometry.scale(assembly.uniformScale,assembly.uniformScale,assembly.uniformScale);
    geometry.translate(...assembly.translationMm); geometry.computeBoundingBox();
    parts.push({name:part.instanceId,geometry,extras:{instanceId:part.instanceId,sourceAtlas:manifest.atlas,
        sourceFiles:part.files,assembly:name,uniformScale:assembly.uniformScale,
        translationMm:assembly.translationMm,focusPointMm:geometry.boundingBox.getCenter(new THREE.Vector3()).toArray(),
        populationEnvelope:Boolean(part.populationEnvelope),
        sourceGeometry:part.populationEnvelope?'Illustrative population envelope; layer boundaries not segmented':
            part.files.length>1?'Composite named anatomical segments':'Named anatomical mesh',
        registration:'Shared uniform assembly fit to the selected shell; native relative placement retained'}});
}
// Unsegmented cerebellar nuclei remain schematic in the corrected shell frame.
const legacy=definitions.get('M_CerebellarLobules');
const target=targets.cerebellum, targetSize=target.getSize(new THREE.Vector3());
const legacyScale=Math.min(...targetSize.toArray().map((v,i)=>v/legacy.dimensionsMm[i]))*.8;
const legacyTranslation=target.getCenter(new THREE.Vector3()).sub(new THREE.Vector3(...legacy.centerMm).multiplyScalar(legacyScale));
const schematicCerebellum={uniformScale:legacyScale,translationMm:legacyTranslation.toArray()};
const modifications='Named anatomical segments fitted as two uniform assemblies to the selected shell. Native positions and size ratios retained within each assembly. Granule/Purkinje envelopes are schematic. Cross-assembly fit is illustrative.';
const output=await writeBrainGeometry(outputPath,parts,{atlas:manifest.atlas,revision:manifest.revision,shellSha256,
    license:'3D Slicer License',licenseFile:'SPL-Slicer-License.txt',modifications});
const report={...manifest,schemaVersion:2,shellSha256,assemblies,schematicCerebellum,modifications,
    instances:parts.map(p=>({...p.extras,triangles:p.geometry.index.count/3})),
    displayBytes:output.length,displaySha256:createHash('sha256').update(output).digest('hex')};
await writeFile(outputPath.replace(/\.glb$/i,'.json'),JSON.stringify(report,null,2)+'\n');
parts.forEach(p=>p.geometry.dispose());cache.forEach(g=>g.dispose());
console.log(JSON.stringify({instances:parts.length,bytes:output.length,assemblies,sha256:report.displaySha256}));
