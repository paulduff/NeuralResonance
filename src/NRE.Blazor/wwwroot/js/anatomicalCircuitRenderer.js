/**
 * Anatomical Circuit Renderer
 * 
 * Displays neural circuits in their TRUE anatomical shapes at biological 1:1 scale.
 * NO warping, NO cortex-forcing, NO gyrification distortion.
 * 
 * Coordinate System (standard neuroanatomical, unified with neuralRenderer):
 * - X: Left (-) to Right (+), 0 = midline
 * - Y: Inferior (-) to Superior (+), 0 = AC-PC line
 * - Z: Anterior (-) to Posterior (+), 0 = brain center
 * 
 * All measurements in millimeters, scaled for viewport.
 */

window.AnatomicalCircuitRenderer = (function() {
    let scene, camera, renderer, controls;
    let circuitMeshes = new Map();
    let activeCircuits = new Set();
    
    // Scale: 1 unit = 1 mm
    const SCALE = 1.0;
    
    // Anatomical positions relative to brain center (0,0,0)
    // Based on standard neuroanatomical atlases
    // Z convention: -Z = Anterior (frontal), +Z = Posterior (occipital)
    const POSITIONS = {
        // Midline structures (x = 0)
        thalamus:       { x: 0,   y: 0,    z: 0 },      // Central reference
        hypothalamus:   { x: 0,   y: -10,  z: -5 },     // Below thalamus, slightly anterior
        brainstem:      { x: 0,   y: -35,  z: 15 },     // Below and posterior
        cerebellum:     { x: 0,   y: -25,  z: 45 },     // Posterior and inferior
        
        // Bilateral structures (x = lateral distance from midline)
        hippocampus:    { x: 25,  y: -10,  z: -5 },     // Medial temporal
        amygdala:       { x: 22,  y: -8,   z: -15 },    // Anterior to hippocampus
        basal_ganglia:  { x: 20,  y: 5,    z: -5 },     // Lateral to thalamus
        
        // Tiny nuclei in brainstem
        vta:            { x: 0,   y: -20,  z: 5 },      // Midbrain
        locus_coeruleus:{ x: 5,   y: -30,  z: 20 },     // Pons
        
        // Cortex wraps around everything
        cortex:         { x: 0,   y: 25,   z: 0 },      // Superior
    };
    
    // Structure sizes (mm)
    const SIZES = {
        thalamus:       { w: 25, h: 20, d: 30 },
        hypothalamus:   { w: 12, h: 8,  d: 12 },
        brainstem:      { topR: 12, botR: 6, len: 60 },
        cerebellum:     { w: 100, h: 50, d: 55 },
        hippocampus:    { len: 45, r: 6 },
        amygdala:       { w: 15, h: 12, d: 18 },
        basal_ganglia:  { w: 35, h: 25, d: 40 },
        vta:            { r: 3 },
        locus_coeruleus:{ r: 1.5 },
    };
    
    // Colors
    const COLORS = {
        thalamus:       0xF0C040,
        hypothalamus:   0xD08850,
        brainstem:      0x70A0E0,
        cerebellum:     0xB070E0,
        hippocampus:    0x40E070,
        amygdala:       0xF04040,
        basal_ganglia:  0x6060D0,
        vta:            0xFF8000,
        locus_coeruleus:0x4080FF,
        cortex:         0xE8E8C0,
    };
    
    // ==================== INITIALIZATION ====================
    
    function init(containerId) {
        const container = document.getElementById(containerId);
        if (!container) {
            console.error('Container not found:', containerId);
            return false;
        }
        
        scene = new THREE.Scene();
        scene.background = new THREE.Color(0x1a1a2e);
        
        const aspect = container.clientWidth / container.clientHeight;
        camera = new THREE.PerspectiveCamera(45, aspect, 1, 2000);
        camera.position.set(120, 80, -150);  // Right, above, anterior (-Z = front)
        camera.lookAt(0, 0, 0);
        
        renderer = new THREE.WebGLRenderer({ antialias: true });
        renderer.setSize(container.clientWidth, container.clientHeight);
        renderer.setPixelRatio(window.devicePixelRatio);
        container.appendChild(renderer.domElement);
        
        controls = new THREE.OrbitControls(camera, renderer.domElement);
        controls.enableDamping = true;
        controls.dampingFactor = 0.05;
        controls.target.set(0, 0, 0);
        
        scene.add(new THREE.AmbientLight(0xffffff, 0.5));
        
        const mainLight = new THREE.DirectionalLight(0xffffff, 0.8);
        mainLight.position.set(50, 100, 50);
        scene.add(mainLight);
        
        const fillLight = new THREE.DirectionalLight(0xffffff, 0.3);
        fillLight.position.set(-50, -50, -50);
        scene.add(fillLight);
        
        addScaleGrid();
        addAxisLabels();
        animate();
        
        window.addEventListener('resize', () => onResize(container));
        
        console.log('Anatomical Circuit Renderer initialized');
        return true;
    }
    
    function onResize(container) {
        if (!renderer || !camera) return;
        const w = container.clientWidth;
        const h = container.clientHeight;
        camera.aspect = w / h;
        camera.updateProjectionMatrix();
        renderer.setSize(w, h);
    }
    
    function addScaleGrid() {
        const grid = new THREE.GridHelper(200, 20, 0x444466, 0x333344);
        grid.position.y = -60;
        scene.add(grid);
        
        const barGeom = new THREE.BoxGeometry(50, 1, 1);
        const barMat = new THREE.MeshBasicMaterial({ color: 0xffffff });
        const bar = new THREE.Mesh(barGeom, barMat);
        bar.position.set(0, -55, 80);
        scene.add(bar);
        
        addTextSprite('50 mm', 0, -50, 80, 0xffffff);
    }
    
    function addAxisLabels() {
        addTextSprite('POSTERIOR', 0, 0, 100, 0x00ff00);
        addTextSprite('ANTERIOR', 0, 0, -100, 0x00ff00);
        addTextSprite('SUPERIOR', 0, 80, 0, 0xffff00);
        addTextSprite('INFERIOR', 0, -70, 0, 0xffff00);
        addTextSprite('LEFT', -90, 0, 0, 0xff6666);
        addTextSprite('RIGHT', 90, 0, 0, 0xff6666);
    }
    
    function addTextSprite(text, x, y, z, color) {
        const canvas = document.createElement('canvas');
        canvas.width = 256;
        canvas.height = 64;
        const ctx = canvas.getContext('2d');
        ctx.fillStyle = '#' + color.toString(16).padStart(6, '0');
        ctx.font = 'bold 28px Arial';
        ctx.textAlign = 'center';
        ctx.fillText(text, 128, 42);
        
        const texture = new THREE.CanvasTexture(canvas);
        const mat = new THREE.SpriteMaterial({ map: texture, transparent: true });
        const sprite = new THREE.Sprite(mat);
        sprite.scale.set(40, 10, 1);
        sprite.position.set(x, y, z);
        scene.add(sprite);
    }
    
    // ==================== CIRCUIT BUILDERS ====================
    
    function buildThalamus() {
        const group = new THREE.Group();
        const pos = POSITIONS.thalamus;
        const size = SIZES.thalamus;
        
        const geom = new THREE.SphereGeometry(1, 32, 24);
        geom.scale(size.w/2, size.h/2, size.d/2);
        
        const mat = new THREE.MeshPhongMaterial({
            color: COLORS.thalamus,
            transparent: true,
            opacity: 0.75,
            shininess: 60
        });
        
        group.add(new THREE.Mesh(geom, mat));
        
        // Nuclei markers
        const nuclei = [
            { pos: [-8, -3, -5], r: 4, color: 0xFFD060 },
            { pos: [8, -3, -5], r: 3, color: 0xFFD060 },
            { pos: [0, 0, -10], r: 6, color: 0xE0B030 },
        ];
        
        nuclei.forEach(n => {
            const nGeom = new THREE.SphereGeometry(n.r, 12, 12);
            const nMat = new THREE.MeshPhongMaterial({ color: n.color });
            const nMesh = new THREE.Mesh(nGeom, nMat);
            nMesh.position.set(...n.pos);
            group.add(nMesh);
        });
        
        group.position.set(pos.x, pos.y, pos.z);
        return group;
    }
    
    function buildHypothalamus() {
        const group = new THREE.Group();
        const pos = POSITIONS.hypothalamus;
        const size = SIZES.hypothalamus;
        
        const geom = new THREE.SphereGeometry(1, 16, 16);
        geom.scale(size.w/2, size.h/2, size.d/2);
        
        const mat = new THREE.MeshPhongMaterial({
            color: COLORS.hypothalamus,
            transparent: true,
            opacity: 0.8
        });
        
        group.add(new THREE.Mesh(geom, mat));
        group.position.set(pos.x, pos.y, pos.z);
        return group;
    }
    
    function buildBrainstem() {
        const group = new THREE.Group();
        const pos = POSITIONS.brainstem;
        const size = SIZES.brainstem;
        
        const geom = new THREE.CylinderGeometry(size.topR, size.botR, size.len, 24);
        const mat = new THREE.MeshPhongMaterial({
            color: COLORS.brainstem,
            transparent: true,
            opacity: 0.75
        });
        
        group.add(new THREE.Mesh(geom, mat));
        
        // Region markers
        [{ y: 20, color: 0x80B0F0 }, { y: 0, color: 0x60C0C0 }, { y: -20, color: 0x5090D0 }].forEach(r => {
            const rGeom = new THREE.TorusGeometry(size.topR * 0.8, 1, 8, 24);
            const rMat = new THREE.MeshPhongMaterial({ color: r.color });
            const rMesh = new THREE.Mesh(rGeom, rMat);
            rMesh.rotation.x = Math.PI / 2;
            rMesh.position.y = r.y;
            group.add(rMesh);
        });
        
        group.position.set(pos.x, pos.y, pos.z);
        return group;
    }
    
    function buildCerebellum() {
        const group = new THREE.Group();
        const pos = POSITIONS.cerebellum;
        const size = SIZES.cerebellum;
        
        const geom = new THREE.SphereGeometry(1, 48, 32, 0, Math.PI * 2, 0, Math.PI * 0.6);
        geom.scale(size.w/2, size.h/2, size.d/2);
        geom.rotateX(Math.PI);
        
        // Add folia
        const positions = geom.attributes.position;
        for (let i = 0; i < positions.count; i++) {
            const y = positions.getY(i);
            const z = positions.getZ(i);
            positions.setZ(i, z + Math.sin(y * 0.4) * 2);
        }
        positions.needsUpdate = true;
        geom.computeVertexNormals();
        
        const mat = new THREE.MeshPhongMaterial({
            color: COLORS.cerebellum,
            transparent: true,
            opacity: 0.8,
            side: THREE.DoubleSide
        });
        
        group.add(new THREE.Mesh(geom, mat));
        
        // Vermis
        const vermisGeom = new THREE.CylinderGeometry(8, 10, size.d * 0.8, 16);
        const vermisMat = new THREE.MeshPhongMaterial({ color: 0xA060D0, transparent: true, opacity: 0.9 });
        const vermis = new THREE.Mesh(vermisGeom, vermisMat);
        vermis.rotation.x = Math.PI / 2;
        vermis.position.y = 5;
        group.add(vermis);
        
        group.position.set(pos.x, pos.y, pos.z);
        return group;
    }
    
    function buildHippocampus(side) {
        const group = new THREE.Group();
        const pos = POSITIONS.hippocampus;
        const size = SIZES.hippocampus;
        
        const curve = new THREE.CatmullRomCurve3([
            new THREE.Vector3(0, 0, -20),
            new THREE.Vector3(-5, -3, -10),
            new THREE.Vector3(-8, -5, 0),
            new THREE.Vector3(-5, -3, 10),
            new THREE.Vector3(0, 0, 20),
        ]);
        
        const tubeGeom = new THREE.TubeGeometry(curve, 32, size.r, 12, false);
        const tubeMat = new THREE.MeshPhongMaterial({
            color: COLORS.hippocampus,
            transparent: true,
            opacity: 0.85
        });
        
        group.add(new THREE.Mesh(tubeGeom, tubeMat));
        
        // Subregion markers
        [0.1, 0.35, 0.65, 0.9].forEach((t, i) => {
            const colors = [0x30C050, 0x40E070, 0x50F080, 0x60FF90];
            const pt = curve.getPointAt(t);
            const markerGeom = new THREE.SphereGeometry(size.r * 0.4, 8, 8);
            const markerMat = new THREE.MeshPhongMaterial({ color: colors[i] });
            const marker = new THREE.Mesh(markerGeom, markerMat);
            marker.position.copy(pt);
            group.add(marker);
        });
        
        const xSign = (side === 'left') ? -1 : 1;
        group.position.set(pos.x * xSign, pos.y, pos.z);
        if (side === 'left') group.scale.x = -1;
        
        return group;
    }
    
    function buildAmygdala(side) {
        const group = new THREE.Group();
        const pos = POSITIONS.amygdala;
        const size = SIZES.amygdala;
        
        const geom = new THREE.SphereGeometry(1, 24, 16);
        geom.scale(size.d/2, size.h/2, size.w/2);
        
        // Taper
        const positions = geom.attributes.position;
        for (let i = 0; i < positions.count; i++) {
            const x = positions.getX(i);
            if (x > 0) {
                const taper = 1 - (x / (size.d/2)) * 0.4;
                positions.setY(i, positions.getY(i) * taper);
                positions.setZ(i, positions.getZ(i) * taper);
            }
        }
        positions.needsUpdate = true;
        geom.computeVertexNormals();
        
        const mat = new THREE.MeshPhongMaterial({
            color: COLORS.amygdala,
            transparent: true,
            opacity: 0.85
        });
        
        group.add(new THREE.Mesh(geom, mat));
        
        const xSign = (side === 'left') ? -1 : 1;
        group.position.set(pos.x * xSign, pos.y, pos.z);
        
        return group;
    }
    
    function buildBasalGanglia(side) {
        const group = new THREE.Group();
        const pos = POSITIONS.basal_ganglia;
        
        // Caudate
        const caudateCurve = new THREE.CatmullRomCurve3([
            new THREE.Vector3(0, 8, -15),
            new THREE.Vector3(-5, 12, -5),
            new THREE.Vector3(-8, 10, 5),
            new THREE.Vector3(-5, 5, 15),
            new THREE.Vector3(5, -5, 10),
        ]);
        
        const caudateGeom = new THREE.TubeGeometry(caudateCurve, 32, 5, 12, false);
        const caudateMat = new THREE.MeshPhongMaterial({ color: 0x6060D0, transparent: true, opacity: 0.8 });
        group.add(new THREE.Mesh(caudateGeom, caudateMat));
        
        // Caudate head
        const headGeom = new THREE.SphereGeometry(8, 16, 16);
        const head = new THREE.Mesh(headGeom, caudateMat);
        head.position.set(0, 8, -15);
        group.add(head);
        
        // Putamen
        const putamenGeom = new THREE.SphereGeometry(1, 24, 16);
        putamenGeom.scale(8, 12, 18);
        const putamenMat = new THREE.MeshPhongMaterial({ color: 0x7070E0, transparent: true, opacity: 0.7 });
        const putamen = new THREE.Mesh(putamenGeom, putamenMat);
        putamen.position.set(-8, 0, 0);
        group.add(putamen);
        
        // Globus pallidus
        const gpGeom = new THREE.SphereGeometry(1, 16, 12);
        gpGeom.scale(5, 8, 12);
        const gpMat = new THREE.MeshPhongMaterial({ color: 0x5050C0, transparent: true, opacity: 0.75 });
        const gp = new THREE.Mesh(gpGeom, gpMat);
        gp.position.set(-2, 0, 0);
        group.add(gp);
        
        // STN
        const stnGeom = new THREE.SphereGeometry(3, 12, 12);
        const stnMat = new THREE.MeshPhongMaterial({ color: 0x8080F0, emissive: 0x4040A0, emissiveIntensity: 0.3 });
        const stn = new THREE.Mesh(stnGeom, stnMat);
        stn.position.set(-5, -8, 0);
        group.add(stn);
        
        const xSign = (side === 'left') ? -1 : 1;
        group.position.set(pos.x * xSign, pos.y, pos.z);
        if (side === 'left') group.scale.x = -1;
        
        return group;
    }
    
    function buildVTA() {
        const group = new THREE.Group();
        const pos = POSITIONS.vta;
        const size = SIZES.vta;
        
        const geom = new THREE.SphereGeometry(size.r, 16, 16);
        const mat = new THREE.MeshPhongMaterial({
            color: COLORS.vta,
            emissive: COLORS.vta,
            emissiveIntensity: 0.5,
            transparent: true,
            opacity: 0.9
        });
        group.add(new THREE.Mesh(geom, mat));
        
        // Halo
        const haloGeom = new THREE.SphereGeometry(size.r * 4, 16, 16);
        const haloMat = new THREE.MeshBasicMaterial({ color: COLORS.vta, transparent: true, opacity: 0.15, wireframe: true });
        group.add(new THREE.Mesh(haloGeom, haloMat));
        
        group.position.set(pos.x, pos.y, pos.z);
        return group;
    }
    
    function buildLocusCoeruleus(side) {
        const group = new THREE.Group();
        const pos = POSITIONS.locus_coeruleus;
        const size = SIZES.locus_coeruleus;
        
        const geom = new THREE.SphereGeometry(size.r, 12, 12);
        const mat = new THREE.MeshPhongMaterial({
            color: COLORS.locus_coeruleus,
            emissive: COLORS.locus_coeruleus,
            emissiveIntensity: 0.6
        });
        group.add(new THREE.Mesh(geom, mat));
        
        const haloGeom = new THREE.SphereGeometry(size.r * 5, 12, 12);
        const haloMat = new THREE.MeshBasicMaterial({ color: COLORS.locus_coeruleus, transparent: true, opacity: 0.1, wireframe: true });
        group.add(new THREE.Mesh(haloGeom, haloMat));
        
        const xSign = (side === 'left') ? -1 : 1;
        group.position.set(pos.x * xSign, pos.y, pos.z);
        
        return group;
    }
    
    function buildCortex() {
        const group = new THREE.Group();
        
        const lobes = [
            { name: 'Frontal', color: 0x60B8E0, pos: [0, 20, -35], size: [55, 45, 45] },
            { name: 'Parietal', color: 0xE0E060, pos: [0, 35, 5], size: [50, 35, 40] },
            { name: 'Temporal', color: 0x60E080, pos: [50, -5, -10], size: [20, 30, 55] },
            { name: 'Occipital', color: 0xE06080, pos: [0, 15, 55], size: [40, 35, 25] },
        ];
        
        lobes.forEach(lobe => {
            const geom = new THREE.SphereGeometry(1, 24, 16);
            geom.scale(lobe.size[0]/2, lobe.size[1]/2, lobe.size[2]/2);
            
            // Add gyri noise
            const positions = geom.attributes.position;
            for (let i = 0; i < positions.count; i++) {
                const x = positions.getX(i);
                const y = positions.getY(i);
                const z = positions.getZ(i);
                const noise = Math.sin(x * 0.3) * Math.sin(y * 0.3) * Math.sin(z * 0.3) * 2;
                const len = Math.sqrt(x*x + y*y + z*z);
                if (len > 0) {
                    const scale = 1 + noise * 0.05;
                    positions.setX(i, x * scale);
                    positions.setY(i, y * scale);
                    positions.setZ(i, z * scale);
                }
            }
            positions.needsUpdate = true;
            geom.computeVertexNormals();
            
            const mat = new THREE.MeshPhongMaterial({
                color: lobe.color,
                transparent: true,
                opacity: 0.6,
                side: THREE.DoubleSide
            });
            
            // Left
            const meshL = new THREE.Mesh(geom, mat);
            meshL.position.set(-lobe.pos[0] - (lobe.name === 'Temporal' ? 0 : 5), lobe.pos[1], lobe.pos[2]);
            if (lobe.name === 'Temporal') meshL.position.x = -lobe.pos[0];
            group.add(meshL);
            
            // Right
            const meshR = new THREE.Mesh(geom.clone(), mat.clone());
            meshR.position.set(lobe.pos[0] + (lobe.name === 'Temporal' ? 0 : 5), lobe.pos[1], lobe.pos[2]);
            if (lobe.name === 'Temporal') meshR.position.x = lobe.pos[0];
            group.add(meshR);
        });
        
        return group;
    }
    
    // ==================== PUBLIC API ====================
    
    function showCircuit(circuitName) {
        if (circuitMeshes.has(circuitName)) {
            circuitMeshes.get(circuitName).visible = true;
            activeCircuits.add(circuitName);
            return;
        }
        
        let group = new THREE.Group();
        
        switch (circuitName) {
            case 'thalamus':
                group = buildThalamus();
                break;
            case 'hypothalamus':
                group = buildHypothalamus();
                break;
            case 'brainstem':
                group = buildBrainstem();
                break;
            case 'cerebellum':
                group = buildCerebellum();
                break;
            case 'hippocampus':
                group.add(buildHippocampus('left'));
                group.add(buildHippocampus('right'));
                break;
            case 'amygdala':
                group.add(buildAmygdala('left'));
                group.add(buildAmygdala('right'));
                break;
            case 'basal_ganglia':
                group.add(buildBasalGanglia('left'));
                group.add(buildBasalGanglia('right'));
                break;
            case 'vta':
                group = buildVTA();
                break;
            case 'locus_coeruleus':
                group.add(buildLocusCoeruleus('left'));
                group.add(buildLocusCoeruleus('right'));
                break;
            case 'cortex':
                group = buildCortex();
                break;
            default:
                console.warn('Unknown circuit:', circuitName);
                return;
        }
        
        scene.add(group);
        circuitMeshes.set(circuitName, group);
        activeCircuits.add(circuitName);
    }
    
    function hideCircuit(circuitName) {
        if (circuitMeshes.has(circuitName)) {
            circuitMeshes.get(circuitName).visible = false;
            activeCircuits.delete(circuitName);
        }
    }
    
    function showAllCircuits() {
        ['thalamus', 'hypothalamus', 'brainstem', 'cerebellum', 
         'hippocampus', 'amygdala', 'basal_ganglia', 'vta', 
         'locus_coeruleus', 'cortex'].forEach(name => showCircuit(name));
    }
    
    function hideAllCircuits() {
        circuitMeshes.forEach(group => group.visible = false);
        activeCircuits.clear();
    }
    
    function setCircuitOpacity(circuitName, opacity) {
        if (circuitMeshes.has(circuitName)) {
            circuitMeshes.get(circuitName).traverse(child => {
                if (child.material) {
                    child.material.opacity = opacity;
                    child.material.transparent = true;
                }
            });
        }
    }
    
    function highlightCircuit(circuitName) {
        circuitMeshes.forEach((group, name) => {
            setCircuitOpacity(name, name === circuitName ? 1.0 : 0.2);
        });
    }
    
    function resetHighlight() {
        circuitMeshes.forEach((group, name) => setCircuitOpacity(name, 0.75));
    }
    
    function setCameraView(view) {
        if (!camera || !controls) {
            console.warn('Camera not initialized');
            return;
        }
        
        switch (view) {
            case 'sagittal':
                camera.position.set(180, 0, 0);
                break;
            case 'coronal':
                camera.position.set(0, 0, 180);
                break;
            case 'axial':
                camera.position.set(0, 180, 0);
                break;
            case 'oblique':
            default:
                camera.position.set(120, 80, 150);
        }
        camera.lookAt(0, 0, 0);
        controls.target.set(0, 0, 0);
        controls.update();
    }
    
    function animate() {
        requestAnimationFrame(animate);
        if (controls) controls.update();
        if (renderer && scene && camera) renderer.render(scene, camera);
    }
    
    return {
        init,
        showCircuit,
        hideCircuit,
        showAllCircuits,
        hideAllCircuits,
        setCircuitOpacity,
        highlightCircuit,
        resetHighlight,
        setCameraView,
        getActiveCircuits: () => Array.from(activeCircuits),
        getAvailableCircuits: () => Object.keys(POSITIONS)
    };
})();
