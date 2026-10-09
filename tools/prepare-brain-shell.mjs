// Prepare the operator-supplied Sketchfab brain for the activity display.
// node tools/prepare-brain-shell.mjs <source.glb> <display.glb>
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { createHash } from 'node:crypto';
import { GLTFLoader } from '../src/NRE.BlazorEditor/wwwroot/vendor/three/addons/loaders/GLTFLoader.js';
import { mergeVertices } from '../src/NRE.BlazorEditor/wwwroot/vendor/three/addons/utils/BufferGeometryUtils.js';
import { disposeBrainObject } from '../src/NRE.BlazorEditor/wwwroot/js/brain-shell.js';

const [sourcePath, outputPath] = process.argv.slice(2);
if (!sourcePath || !outputPath || !/\.glb$/i.test(outputPath) || resolve(sourcePath) === resolve(outputPath)) {
    throw new Error('Provide separate source and output GLB paths; preserve the source.');
}
const source = await readFile(sourcePath);
const gltf = await new GLTFLoader().parseAsync(source.buffer.slice(source.byteOffset, source.byteOffset + source.byteLength), '');
const attribution = gltf.asset.extras;
if (attribution?.source !== 'https://sketchfab.com/3d-models/human-brain-1-copy-1-315449e9517047edb65e3b462c259b91') {
    throw new Error('This preparation uses label identifiers verified for this particular brain asset.');
}
gltf.scene.updateMatrixWorld(true);
const views = [], accessors = [], chunks = [], meshes = [], nodes = [], removed = [];
let byteLength = 0, sourceTriangles = 0, displayTriangles = 0, displayVertices = 0;
function addArray(array, type, componentType, target, bounds) {
    const buffer = Buffer.from(array.buffer, array.byteOffset, array.byteLength);
    const bufferView = views.length;
    views.push({ buffer: 0, byteOffset: byteLength, byteLength: buffer.length, target });
    chunks.push(buffer);
    byteLength += buffer.length;
    const index = accessors.length;
    accessors.push({ bufferView, componentType, count: array.length / (type === 'VEC3' ? 3 : 1), type, ...bounds });
    return index;
}
gltf.scene.traverse(object => {
    if (!object.isMesh) return;
    const triangleCount = (object.geometry.index?.count ?? object.geometry.attributes.position.count) / 3;
    sourceTriangles += triangleCount;
    // The three Foregrou meshes contain the source illustration's number marks.
    // Keep the anatomical meshes, including the small unlabelled fragments.
    if (object.material.name === 'Foregrou') {
        removed.push({ name: object.name, material: object.material.name, triangles: triangleCount });
        return;
    }
    const rawGeometry = object.geometry.clone().applyMatrix4(object.matrixWorld);
    rawGeometry.deleteAttribute('normal');
    rawGeometry.deleteAttribute('uv');
    const geometry = mergeVertices(rawGeometry, 0.0001);
    geometry.computeVertexNormals();
    geometry.computeBoundingBox();
    const position = geometry.attributes.position;
    const normal = geometry.attributes.normal;
    const indices = new Uint32Array(geometry.index.array);
    const positionId = addArray(new Float32Array(position.array), 'VEC3', 5126, 34962,
        { min: geometry.boundingBox.min.toArray(), max: geometry.boundingBox.max.toArray() });
    const normalId = addArray(new Float32Array(normal.array), 'VEC3', 5126, 34962);
    const indexId = addArray(indices, 'SCALAR', 5125, 34963);
    meshes.push({ name: object.parent.name, primitives: [{ attributes: { POSITION: positionId, NORMAL: normalId }, indices: indexId, material: 0 }] });
    nodes.push({ name: object.name, mesh: meshes.length - 1, extras: { anatomicalPart: object.parent.name } });
    displayTriangles += indices.length / 3;
    displayVertices += position.count;
    rawGeometry.dispose();
    geometry.dispose();
});
if (removed.length !== 3 || displayTriangles !== 36632) throw new Error('Unexpected source geometry; review the label removal.');
const document = {
    asset: { version: '2.0', generator: 'DNNE prepare-brain-shell.mjs', extras: { ...attribution,
        modifications: 'Number label meshes removed; transforms baked; duplicate vertices welded; normals smoothed; uniform display material.' } },
    scene: 0, scenes: [{ nodes: nodes.map((_, index) => index) }], nodes, meshes,
    materials: [{ name: 'Editor shell', alphaMode: 'BLEND', pbrMetallicRoughness: {
        baseColorFactor: [0.65, 0.74, 0.78, 0.12], metallicFactor: 0, roughnessFactor: 0.72 } }],
    buffers: [{ byteLength }], bufferViews: views, accessors
};
let json = Buffer.from(JSON.stringify(document));
json = Buffer.concat([json, Buffer.alloc((4 - json.length % 4) % 4, 0x20)]);
const binary = Buffer.concat(chunks);
const header = Buffer.alloc(12), jsonHeader = Buffer.alloc(8), binaryHeader = Buffer.alloc(8);
header.writeUInt32LE(0x46546c67); header.writeUInt32LE(2, 4); header.writeUInt32LE(28 + json.length + binary.length, 8);
jsonHeader.writeUInt32LE(json.length); jsonHeader.writeUInt32LE(0x4e4f534a, 4);
binaryHeader.writeUInt32LE(binary.length); binaryHeader.writeUInt32LE(0x004e4942, 4);
const output = Buffer.concat([header, jsonHeader, json, binaryHeader, binary]);
await mkdir(dirname(outputPath), { recursive: true });
await writeFile(outputPath, output);
const report = { ...attribution, sourceFile: sourcePath.split(/[\\/]/).at(-1),
    sourceSha256: createHash('sha256').update(source).digest('hex'), sourceTriangles,
    displayTriangles, displayVertices, displayBytes: output.length,
    displaySha256: createHash('sha256').update(output).digest('hex'), removedLabels: removed,
    preparation: document.asset.extras.modifications,
    coordinateRegistration: 'Illustrative atlas alignment; source +X anterior, +Y superior after baked scene transforms.' };
await writeFile(outputPath.replace(/\.glb$/i, '.json'), JSON.stringify(report, null, 2) + '\n');
disposeBrainObject(gltf.scene);
console.log(JSON.stringify(report, null, 2));
