import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import * as THREE from '../../src/NRE.BlazorEditor/wwwroot/vendor/three/three.module.min.js';
import { GLTFLoader } from '../../src/NRE.BlazorEditor/wwwroot/vendor/three/addons/loaders/GLTFLoader.js';
import { registerBrainShell, disposeBrainObject } from '../../src/NRE.BlazorEditor/wwwroot/js/brain-shell.js';
import { firingRateLevel, observeSnapshot, snapshotIsFresh, recentDispatches } from '../../src/NRE.BlazorEditor/wwwroot/js/brain-activity.js';
import { buildCorticalSurfaceParcels } from '../../src/NRE.BlazorEditor/wwwroot/js/brain-surface.js';

test('rate scale separates high rates and distinguishes missing samples from silence', () => {
    assert.equal(firingRateLevel(0), 0);
    assert.equal(firingRateLevel(50), 0.25);
    assert.equal(firingRateLevel(130), 0.65);
    assert.equal(firingRateLevel(250), 1);
    for (const missing of [null, undefined, NaN, Infinity, -1]) assert.equal(firingRateLevel(missing), null);
    assert.throws(() => firingRateLevel(10, 0), RangeError);
});

test('folded cortical partition preserves surface triangles and hemisphere identity', async () => {
    const atlas = JSON.parse((await readFile(new URL('../../src/NRE.BlazorEditor/wwwroot/data/brain-atlas.json', import.meta.url), 'utf8')).replace(/^\uFEFF/, ''));
    const bytes = await readFile(new URL('../../src/NRE.BlazorEditor/wwwroot/data/models/brain-shell.glb', import.meta.url));
    const gltf = await new GLTFLoader().parseAsync(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), '');
    const model = registerBrainShell(gltf.scene);
    const anchors = atlas.structures.filter(structure => structure.layout === 'CorticalSheet').map(structure => ({
        userData: { structure, focusPoint: new THREE.Vector3(...structure.centerMm) }
    }));
    const result = buildCorticalSurfaceParcels(model, anchors);
    assert.equal(result.sourceTriangles, 18520);
    assert.equal([...result.parcels.values()].reduce((sum, parcel) => sum + parcel.geometry.attributes.position.count / 3, 0), result.sourceTriangles);
    for (const [id, parcel] of result.parcels) {
        assert.ok(parcel.geometry.attributes.position.count > 0);
        const p = parcel.geometry.attributes.position;
        for (let i = 0; i < p.count; i += 3) {
            const x = (p.getX(i) + p.getX(i + 1) + p.getX(i + 2)) / 3;
            assert.ok(id.startsWith(x < 0 ? 'L_' : 'R_'), `${id} crosses hemisphere mapping`);
        }
        parcel.geometry.dispose();
    }
    disposeBrainObject(model);
});

test('named anatomical parts map uniquely and preserve centres and uniform scaling', async () => {
    const atlas = JSON.parse((await readFile(new URL('../../src/NRE.BlazorEditor/wwwroot/data/brain-atlas.json', import.meta.url), 'utf8')).replace(/^\uFEFF/, ''));
    const definitions = new Map(atlas.structures.map(structure => [structure.instanceId, structure]));
    const bytes = await readFile(new URL('../../src/NRE.BlazorEditor/wwwroot/data/models/subcortical-parts.glb', import.meta.url));
    const gltf = await new GLTFLoader().parseAsync(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), '');
    const ids = new Set();
    gltf.scene.traverse(mesh => {
        if (!mesh.isMesh) return;
        const id = mesh.userData.instanceId;
        assert.equal(ids.has(id), false);
        ids.add(id);
        const structure = definitions.get(id);
        assert.ok(structure, id);
        const bounds = new THREE.Box3().setFromObject(mesh);
        const center = bounds.getCenter(new THREE.Vector3());
        structure.centerMm.forEach((expected, axis) => assert.ok(Math.abs(center.getComponent(axis) - expected) < 0.0001));
        const size = bounds.getSize(new THREE.Vector3());
        const expectedVolume = structure.dimensionsMm.reduce((a, b) => a * b, 1);
        assert.ok(Math.abs(size.x * size.y * size.z / expectedVolume - 1) < 0.0001);
        assert.ok(mesh.userData.uniformScale > 0);
        assert.ok([...mesh.geometry.attributes.normal.array].every(Number.isFinite));
    });
    assert.equal(ids.size, 37);
    assert.ok(ids.has('L_GPe') && ids.has('R_GPe') && ids.has('M_CorpusCallosum'));
    disposeBrainObject(gltf.scene);
});

test('repeated frames do not refresh a stopped snapshot; restarting ticks can refresh', () => {
    let observation = observeSnapshot({ tick: null, receivedAt: null }, 100, 1000, true);
    observation = observeSnapshot(observation, 100, 7000, true);
    assert.equal(observation.receivedAt, 1000);
    assert.equal(snapshotIsFresh(observation.receivedAt, 7000), false);
    observation = observeSnapshot(observation, 1, 8000, true);
    assert.equal(snapshotIsFresh(observation.receivedAt, 8000), true);
    assert.equal(observeSnapshot(observation, 2, 9000, false), observation);
    assert.equal(snapshotIsFresh(null, 1000), false);
});

test('dispatch window excludes old, future and un-timestamped events', () => {
    const current = { wallClockUnixMs: 9000 };
    assert.deepEqual(recentDispatches([{ wallClockUnixMs: 1000 }, current,
        { WallClockUnixMs: 10000 }, { wallClockUnixMs: 10001 }, {}], 10000),
        [current, { WallClockUnixMs: 10000 }]);
});

test('actual display GLB loads, has finite normals and registers to atlas axes', async () => {
    const bytes = await readFile(new URL('../../src/NRE.BlazorEditor/wwwroot/data/models/brain-shell.glb', import.meta.url));
    const gltf = await new GLTFLoader().parseAsync(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), '');
    const model = registerBrainShell(gltf.scene);
    const bounds = new THREE.Box3().setFromObject(model);
    const size = bounds.getSize(new THREE.Vector3());
    const center = bounds.getCenter(new THREE.Vector3());
    for (const [actual, expected] of [[size.x,142],[size.y,140],[size.z,172],[center.x,0],[center.y,-17],[center.z,2]]) {
        assert.ok(Math.abs(actual - expected) < 0.0001, `${actual} should equal ${expected}`);
    }
    assert.ok(gltf.scene.rotation.y < 0, 'source anterior +X must rotate to atlas anterior +Z');
    let triangles = 0;
    let disposedGeometries = 0;
    let disposedMaterials = 0;
    const observedMaterials = new Set();
    model.traverse(object => {
        if (!object.isMesh) return;
        const positions = object.geometry.attributes.position;
        const normals = object.geometry.attributes.normal;
        assert.ok([...positions.array].every(Number.isFinite));
        assert.ok([...normals.array].every(Number.isFinite));
        assert.ok([...object.geometry.index.array].every(index => index < positions.count));
        triangles += object.geometry.index.count / 3;
        object.geometry.addEventListener('dispose', () => disposedGeometries++);
        if (!observedMaterials.has(object.material)) {
            observedMaterials.add(object.material);
            object.material.addEventListener('dispose', () => disposedMaterials++);
        }
    });
    assert.equal(triangles, 36632);
    model.traverse(object => { if (object.isMesh) assert.notEqual(object.material.name, 'Foregrou'); });
    disposeBrainObject(model);
    assert.equal(disposedGeometries, 13);
    assert.equal(disposedMaterials, 1);
});
