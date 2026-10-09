// node tools/prepare-brain-parts.mjs <directory with downloaded VTKs and manifest.json> <output.glb>
import { readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import * as THREE from '../src/NRE.BlazorEditor/wwwroot/vendor/three/three.module.min.js';
import { VTKLoader } from '../src/NRE.BlazorEditor/wwwroot/vendor/three/addons/loaders/VTKLoader.js';
import { mergeGeometries, mergeVertices } from '../src/NRE.BlazorEditor/wwwroot/vendor/three/addons/utils/BufferGeometryUtils.js';
import { writeBrainGeometry } from './brain-glb-writer.mjs';

const [sourceDirectory, outputPath] = process.argv.slice(2);
if (!sourceDirectory || !outputPath || !/\.glb$/i.test(outputPath)) throw new Error('Provide the source directory and output GLB path.');
const manifest = JSON.parse(await readFile(join(sourceDirectory, 'manifest.json'), 'utf8'));
const atlas = JSON.parse((await readFile(new URL('../src/NRE.BlazorEditor/wwwroot/data/brain-atlas.json', import.meta.url), 'utf8')).replace(/^\uFEFF/, ''));
const definitions = new Map(atlas.structures.map(structure => [structure.instanceId, structure]));
const parts = [];
const rasToAtlas = new THREE.Matrix4().set(1,0,0,0, 0,0,1,0, 0,1,0,0, 0,0,0,1);
for (const part of manifest.parts) {
    const structure = definitions.get(part.instanceId);
    if (!structure) throw new Error(`Unmapped source part ${part.instanceId}`);
    const geometries = [];
    for (const file of part.files) {
        const bytes = await readFile(join(sourceDirectory, file));
        const source = manifest.sources.find(source => source.file === file);
        if (createHash('sha256').update(bytes).digest('hex') !== source.sha256) throw new Error(`Source hash changed: ${file}`);
        let geometry = new VTKLoader().parse(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength));
        geometry.deleteAttribute('normal'); geometry.deleteAttribute('color');
        const welded = mergeVertices(geometry, 0.0001);
        geometry.dispose(); geometry = welded;
        geometry.applyMatrix4(rasToAtlas);
        // Swapping anterior/superior axes changes handedness; restore outward winding.
        const index = geometry.index.array;
        for (let i = 0; i < index.length; i += 3) [index[i + 1], index[i + 2]] = [index[i + 2], index[i + 1]];
        geometry.computeVertexNormals();
        geometries.push(geometry);
    }
    const geometry = mergeGeometries(geometries);
    if (!geometry) throw new Error(`Cannot combine ${part.instanceId}`);
    geometries.forEach(geometry => geometry.dispose());
    geometry.computeBoundingBox();
    const size = geometry.boundingBox.getSize(new THREE.Vector3());
    const center = geometry.boundingBox.getCenter(new THREE.Vector3());
    if (![size.x, size.y, size.z].every(value => value > 0 && Number.isFinite(value))) throw new Error('Invalid part bounds.');
    const dimensions = structure.dimensionsMm;
    const scale = Math.cbrt(dimensions[0] * dimensions[1] * dimensions[2] / (size.x * size.y * size.z));
    geometry.translate(-center.x, -center.y, -center.z);
    geometry.scale(scale, scale, scale);
    geometry.translate(...structure.centerMm);
    parts.push({ name: part.instanceId, geometry, extras: {
        instanceId: part.instanceId, sourceAtlas: manifest.atlas, sourceFiles: part.files,
        sourceGeometry: part.files.length > 1 ? 'Composite anatomical mesh' : 'Named anatomical mesh',
        registration: 'RAS axes converted; uniform display scale; existing DNNE atlas centre', uniformScale: scale
    } });
}
const output = await writeBrainGeometry(outputPath, parts, {
    atlas: manifest.atlas, source: 'https://www.openanatomy.org/atlas-pages/atlas-spl-nac-brain.html',
    revision: manifest.revision, license: '3D Slicer License', licenseFile: 'SPL-Slicer-License.txt',
    modifications: 'Selected named meshes, RAS axis conversion, uniform atlas sizing and recentering. Display alignment is illustrative.'
});
const report = { ...manifest, modifications: 'Named anatomical shapes uniformly scaled and recentered to the DNNE display atlas; not a medical registration.',
    instances: parts.map(part => ({ instanceId: part.name, ...part.extras, triangles: part.geometry.index.count / 3 })),
    displayBytes: output.length, displaySha256: createHash('sha256').update(output).digest('hex') };
await writeFile(outputPath.replace(/\.glb$/i, '.json'), JSON.stringify(report, null, 2) + '\n');
parts.forEach(part => part.geometry.dispose());
console.log(JSON.stringify({ instances: parts.length, bytes: output.length, sha256: report.displaySha256 }));
