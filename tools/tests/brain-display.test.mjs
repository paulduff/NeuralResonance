import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import * as THREE from '../../src/NRE.BlazorEditor/wwwroot/vendor/three/three.module.min.js';
import { GLTFLoader } from '../../src/NRE.BlazorEditor/wwwroot/vendor/three/addons/loaders/GLTFLoader.js';
import { registerBrainShell, disposeBrainObject } from '../../src/NRE.BlazorEditor/wwwroot/js/brain-shell.js';
import { firingRateLevel, observeSnapshot, snapshotIsFresh, recentDispatches } from '../../src/NRE.BlazorEditor/wwwroot/js/brain-activity.js';
import { buildCorticalSurfaceParcels, readCorticalSurface, validateCorticalMap } from '../../src/NRE.BlazorEditor/wwwroot/js/brain-surface.js';

async function mappedCortexFixture() {
    const assetRoot = new URL('../../src/NRE.BlazorEditor/wwwroot/data/', import.meta.url);
    const atlas = JSON.parse((await readFile(new URL('brain-atlas.json', assetRoot), 'utf8')).replace(/^\uFEFF/, ''));
    const bytes = await readFile(new URL('models/brain-shell.glb', assetRoot));
    const mapping = JSON.parse(await readFile(new URL('models/cortical-map.json', assetRoot), 'utf8'));
    const gltf = await new GLTFLoader().parseAsync(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), '');
    return { bytes, mapping, model:registerBrainShell(gltf.scene), anchors:atlas.structures.filter(s=>s.layout==='CorticalSheet')
        .map(structure=>({userData:{structure,focusPoint:new THREE.Vector3(...structure.centerMm)}})) };
}

test('gyral annotations preserve all shell triangles and every DNNE cortical ID', async () => {
    const { bytes, mapping, model, anchors } = await mappedCortexFixture();
    assert.equal(mapping.shellSha256, createHash('sha256').update(bytes).digest('hex'));
    const before = readCorticalSurface(model);
    const result = buildCorticalSurfaceParcels(model, anchors, mapping);
    assert.equal(result.parcels.size, 70);
    assert.equal(mapping.territories.filter(t=>t.method==='gyrus-guided').length, 68);
    assert.equal(mapping.territories.filter(t=>t.method==='atlas-guided').every(t=>t.structureId==='Insula'), true);
    assert.equal([...result.parcels.values()].reduce((sum,p)=>sum+p.geometry.attributes.position.count/3,0), before.triangles.length);
    assert.deepEqual(readCorticalSurface(model).triangles, before.triangles, 'annotation must not deform the shell');
    for (const parcel of result.parcels.values()) {
        assert.equal(parcel.geometry.attributes.position.count,parcel.geometry.attributes.color.count);
        assert.ok([...parcel.geometry.attributes.color.array].every(v=>Number.isFinite(v)&&v>=0&&v<=1));
        parcel.geometry.dispose();
    }
    disposeBrainObject(model);
});

test('motor strip precedes sensory strip and obeys the named gyral constraints on both sides', async () => {
    const { mapping, model, anchors } = await mappedCortexFixture();
    const { triangles } = readCorticalSurface(model);
    const lookup = validateCorticalMap(mapping,triangles,anchors);
    for (const side of ['L','R']) {
        const motor=[], sensory=[];
        for (const t of triangles.filter(t=>t.side===side)) {
            const a=lookup.get(t.meshName), region=mapping.territories[a.regions[t.triangleIndex]];
            if (region.structureId==='M1') motor.push(t.centroid[2]);
            if (region.structureId==='S1') sensory.push(t.centroid[2]);
            if (region.structureId==='M1') assert.ok(['precentral','paracentrallobule'].includes(mapping.gyri[a.gyri[t.triangleIndex]].key));
            if (region.structureId==='S1') assert.ok(['postcentral','paracentrallobule'].includes(mapping.gyri[a.gyri[t.triangleIndex]].key));
        }
        assert.ok(motor.length>0&&sensory.length>0);
        assert.ok(motor.reduce((a,b)=>a+b,0)/motor.length > sensory.reduce((a,b)=>a+b,0)/sensory.length, side);
    }
    disposeBrainObject(model);
});

test('invalid hemisphere, gyral constraints and extra assignments are rejected before geometry replacement', async () => {
    const { mapping, model, anchors } = await mappedCortexFixture();
    const { triangles } = readCorticalSurface(model);
    const wrongSide=structuredClone(mapping);
    wrongSide.territories[0].hemisphere='R';
    assert.throws(()=>validateCorticalMap(wrongSide,triangles,anchors),/territory/);
    const wrongGyrus=structuredClone(mapping);
    const m1=wrongGyrus.territories.findIndex(t=>t.instanceId==='L_M1');
    const mesh=wrongGyrus.meshAssignments.find(a=>a.regions.includes(m1));
    mesh.gyri[mesh.regions.indexOf(m1)]=wrongGyrus.gyri.findIndex(g=>g.key==='cuneus');
    assert.throws(()=>validateCorticalMap(wrongGyrus,triangles,anchors),/constraint/);
    const overflow=structuredClone(mapping);
    overflow.meshAssignments[0].regions.push(0);
    assert.throws(()=>validateCorticalMap(overflow,triangles,anchors),/assignment/);
    disposeBrainObject(model);
});

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

test('anatomical parts retain shared assembly transforms and fit the selected shell', async () => {
    const atlas = JSON.parse((await readFile(new URL('../../src/NRE.BlazorEditor/wwwroot/data/brain-atlas.json', import.meta.url), 'utf8')).replace(/^\uFEFF/, ''));
    const definitions = new Map(atlas.structures.map(structure => [structure.instanceId, structure]));
    const bytes = await readFile(new URL('../../src/NRE.BlazorEditor/wwwroot/data/models/subcortical-parts.glb', import.meta.url));
    const report = JSON.parse(await readFile(new URL('../../src/NRE.BlazorEditor/wwwroot/data/models/subcortical-parts.json', import.meta.url), 'utf8'));
    const shellBytes = await readFile(new URL('../../src/NRE.BlazorEditor/wwwroot/data/models/brain-shell.glb', import.meta.url));
    assert.equal(report.shellSha256, createHash('sha256').update(shellBytes).digest('hex'));
    assert.equal(report.displaySha256, createHash('sha256').update(bytes).digest('hex'));
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
        mesh.userData.focusPointMm.forEach((expected, axis) => assert.ok(Math.abs(center.getComponent(axis) - expected) < 0.0001));
        const assembly=report.assemblies[mesh.userData.assembly];
        assert.equal(mesh.userData.uniformScale, assembly.uniformScale, 'individual rescaling must not destroy size ratios');
        assert.deepEqual(mesh.userData.translationMm, assembly.translationMm, 'individual recentering must not destroy native registration');
        const target=new THREE.Box3(new THREE.Vector3(...assembly.targetBounds.min),new THREE.Vector3(...assembly.targetBounds.max));
        assert.ok(target.expandByScalar(.0001).containsBox(bounds), `${id} escapes its shell envelope`);
        if (mesh.userData.populationEnvelope) assert.ok(['M_CerebellarGranule','M_PurkinjeCellLayer'].includes(id));
        assert.ok(mesh.userData.uniformScale > 0);
        assert.ok([...mesh.geometry.attributes.normal.array].every(Number.isFinite));
    });
    assert.equal(ids.size, 41);
    assert.ok(ids.has('L_GPe') && ids.has('R_GPe') && ids.has('M_CorpusCallosum'));
    assert.ok(ids.has('M_CerebellarLobules') && ids.has('M_CerebellarVermis'));
    // The original cerebellar centre was about 31 mm too posterior and 11 mm too high.
    const target=report.assemblies.cerebellum.targetBounds;
    const correctedCenter=target.min.map((v,i)=>(v+target.max[i])/2);
    assert.ok(Math.abs(correctedCenter[2]-definitions.get('M_CerebellarLobules').centerMm[2])>25);
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
