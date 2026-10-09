import { writeFile, mkdir } from 'node:fs/promises';
import { dirname } from 'node:path';

export async function writeBrainGeometry(path, parts, extras) {
    const chunks = [], bufferViews = [], accessors = [], meshes = [], nodes = [];
    let byteLength = 0;
    function attribute(array, type, componentType, target, bounds = {}) {
        const bytes = Buffer.from(array.buffer, array.byteOffset, array.byteLength);
        const bufferView = bufferViews.length;
        bufferViews.push({ buffer: 0, byteOffset: byteLength, byteLength: bytes.length, target });
        chunks.push(bytes); byteLength += bytes.length;
        accessors.push({ bufferView, componentType, count: array.length / (type === 'VEC3' ? 3 : 1), type, ...bounds });
        return accessors.length - 1;
    }
    for (const part of parts) {
        const geometry = part.geometry;
        geometry.computeBoundingBox();
        const position = attribute(new Float32Array(geometry.attributes.position.array), 'VEC3', 5126, 34962,
            { min: geometry.boundingBox.min.toArray(), max: geometry.boundingBox.max.toArray() });
        const normal = attribute(new Float32Array(geometry.attributes.normal.array), 'VEC3', 5126, 34962);
        const indices = attribute(new Uint32Array(geometry.index.array), 'SCALAR', 5125, 34963);
        meshes.push({ name: part.name, primitives: [{ attributes: { POSITION: position, NORMAL: normal }, indices, material: 0 }] });
        nodes.push({ name: part.name, mesh: meshes.length - 1, extras: part.extras });
    }
    const document = { asset: { version: '2.0', generator: 'DNNE anatomical display preparation', extras },
        scene: 0, scenes: [{ nodes: nodes.map((_, i) => i) }], nodes, meshes,
        materials: [{ name: 'Anatomical display', pbrMetallicRoughness: { metallicFactor: 0, roughnessFactor: 0.7 } }],
        buffers: [{ byteLength }], bufferViews, accessors };
    let json = Buffer.from(JSON.stringify(document));
    json = Buffer.concat([json, Buffer.alloc((4 - json.length % 4) % 4, 32)]);
    const binary = Buffer.concat(chunks);
    const header = Buffer.alloc(12), jsonHeader = Buffer.alloc(8), binaryHeader = Buffer.alloc(8);
    header.writeUInt32LE(0x46546c67); header.writeUInt32LE(2, 4); header.writeUInt32LE(28 + json.length + binary.length, 8);
    jsonHeader.writeUInt32LE(json.length); jsonHeader.writeUInt32LE(0x4e4f534a, 4);
    binaryHeader.writeUInt32LE(binary.length); binaryHeader.writeUInt32LE(0x004e4942, 4);
    const output = Buffer.concat([header, jsonHeader, json, binaryHeader, binary]);
    await mkdir(dirname(path), { recursive: true });
    await writeFile(path, output);
    return output;
}
