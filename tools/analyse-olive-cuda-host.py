"""Check complete saved host-candidate traces against the reviewed cost capture."""
import gzip
from hashlib import sha256
import json
from pathlib import Path
import sys

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
digest=lambda p:sha256(p.read_bytes()).hexdigest().upper()
LEGACY=['seed-transfer-and-ownership','invalid-requests-atomic','thread-ownership','untouched-cell',
        'identity-permutation','numerical-failure-atomic']
EXTRA=['changing-subsets-and-order','rejected-request-preserves-upload','empty-after-valid-and-rejected',
       'returned-report-ownership','failed-work-then-different-subset','disposed-empty-request']


def equal_streams(paths, expected_bytes):
    streams=[gzip.open(p,'rb') for p in paths]
    total=0; checksum=sha256()
    try:
        while True:
            chunks=[s.read(1024*1024) for s in streams]
            assert all(c==chunks[0] for c in chunks), 'Saved states/reports differ: '+str(paths)
            if not chunks[0]:break
            total+=len(chunks[0]);checksum.update(chunks[0])
    finally:
        for stream in streams:stream.close()
    assert total==expected_bytes, 'Unexpected complete trace length.'
    return dict(bytes=total,sha256=checksum.hexdigest().upper())


def audit(package):
    assert read(package/'host-protocol.json')==dict(version='olive-cuda-host-accuracy-v1',counts=[192,3072,12288],
        steps=[.025,.00625],frames=128,modes=['reference','candidate'],legacyControls=LEGACY,hostControls=EXTRA,
        exactRecordedBytesRequired=True,speedMeasured=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    root=package/'study'
    for mode,version in [('reference','isolated-olive-cuda-double-v1'),('candidate','isolated-olive-cuda-host-v1')]:
        folder=root/mode
        assert read(folder/'result.json')==dict(completed=True,mode=mode,version=version,cases=6,contextDisposed=True,
            accuracyPassed=True,speedMeasured=False,physiologyQualified=False,avatarRecoveryAssessed=False)
        assert read(folder/'device.json')==read(package/'timing-evidence/device.json')
        assert read(folder/'memory-after.json')['ownedBufferBytes']==0
        for name,expected in [('controls.json',LEGACY),('host-controls.json',EXTRA)]:
            rows=read(folder/name)
            assert [r['name'] for r in rows]==expected and all(r['passed'] for r in rows)
            assert rows==read(root/'reference'/name), 'Cross-mode control evidence differs.'
        controls=read(folder/'controls.json')
        assert controls[1]['rejected']==6 and controls[4]['frames']==len(controls[4]['matchedStateHashes'])==64
        extra=read(folder/'host-controls.json')
        assert len(extra[0]['frames'])==40 and [r['frame'] for r in extra[0]['frames']]==list(range(40))
        assert all(r['before']==r['after'] for r in extra if 'before' in r and r['name']!='failed-work-then-different-subset')
        assert len(extra[4]['reports'])==1 and extra[4]['before']!=extra[4]['after']
    cases=[]
    for count in [192,3072,12288]:
        for step in [.025,.00625]:
            key=f'cells-{count}-h-{step}';rows=[]
            for mode in ['reference','candidate']:
                folder=root/mode/key; row=read(folder/'result.json')
                assert row==dict(key=key,count=count,step=step,frames=128,recordedBytesMatch=True,
                    reportsSha256=digest(folder/'reports.bin.gz'),finalSha256=digest(folder/'final.bin.gz'))
                rows.append(row)
            traces={}
            for name,size in [('reports',count*128*48),('final',count*152)]:
                paths=[root/m/key/(name+'.bin.gz') for m in ['reference','candidate']]
                paths.append(package/'recorded'/key/('measured-'+name+'.bin.gz'))
                traces[name]=equal_streams(paths,size)
            cases.append(dict(key=key,exactAcrossReferenceCandidateAndRecorded=True,traces=traces))
    for mode in ['reference','candidate']:
        assert read(root/mode/'completed-cases.json')==[read(root/mode/c['key']/'result.json') for c in cases]
    return dict(completed=True,accuracyPassed=True,cases=cases,legacyControlGroups=12,hostControlGroups=12,
        exactControlsAcrossModes=True,contextCleanupReported=True,speedMeasured=False,
        physiologyQualified=False,avatarRecoveryAssessed=False)


if __name__=='__main__':
    package=Path(sys.argv[1]).resolve();result=audit(package)
    with (package/'host-analysis.json').open('x',encoding='utf-8') as f:f.write(json.dumps(result,indent=2)+'\n')
    print('All six full cases and twelve control groups per mode match. No performance measurement was made.',flush=True)
