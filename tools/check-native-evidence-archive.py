"""Visible-only recording controls. No neural, muscle, Unity or ODE execution."""
import argparse
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import shutil
from unittest.mock import patch
from zipfile import ZipFile,ZIP_DEFLATED,BadZipFile
import native_evidence_archive as store

EXPECTED=24


def run(output):
    assert not output.exists();output.mkdir();rows=[]
    def check(key,action):
        try:action();row=dict(key=key,passed=True)
        except Exception as error:row=dict(key=key,passed=False,error=repr(error))
        rows.append(row);print(key,row['passed'],flush=True)
    def reject(action,errors=(ValueError,FileExistsError,KeyError)):
        try:action()
        except errors:return
        raise AssertionError('Invalid recording was accepted.')
    source=output/'source';(source/'bridge').mkdir(parents=True)
    contents={'initial.json':b'\xef\xbb\xbf{"value":-0.0}\r\n','bridge/request-000001.json':b'{"bits":"3FB999999999999A"}',
        'bridge/reply-000001.json':b'{"value":1.0000000000000002}', 'bridge/hello.json':b'\x00\xffbinary',
        'steps.jsonl':b'{"step":1}\n{"step":2}\n','empty.bin':b''}
    for key,data in contents.items():(source/key).write_bytes(data)
    expected=[dict(path=k,bytes=len(v),sha256=hashlib.sha256(v).hexdigest().upper()) for k,v in sorted(contents.items())]
    target=output/'good.zip';result=store.pack(source,target,expected)
    with store.EvidenceArchive(target,result['archiveSha256']) as archive:
        def same_bytes():
            assert archive.verify(expected)['files']==6
            assert all((archive.root/k).read_bytes()==v for k,v in contents.items())
            assert all((source/k).read_bytes()==v for k,v in contents.items())
        check('all-original-bytes-and-source-preserved',same_bytes)
        check('bom-crlf-and-bit-strings-preserved',lambda:assert_equal((archive.root/'initial.json').read_text('utf-8-sig'),'{"value":-0.0}\r\n'))
        check('canonical-glob-and-order',lambda:assert_equal([p.name for p in sorted((archive.root/'bridge').glob('request-*.json'))],['request-000001.json']))
        check('read-only-api',lambda:reject(lambda:(archive.root/'initial.json').open('w')))
        check('foreign-manifest-rejected',lambda:reject(lambda:archive.verify(expected[:-1])))
        check('missing-member-rejected',lambda:reject(lambda:(archive.root/'missing.json').read_bytes()))
        check('unsafe-logical-path-rejected',lambda:reject(lambda:archive.root/'../outside'))
        def streamed():
            with (archive.root/'steps.jsonl').open('rb') as stream:assert stream.read()==contents['steps.jsonl']
        check('checked-stream-preserves-bytes',streamed)
    check('closed-reader-rejected',lambda:reject(lambda:archive.root.read_bytes()))
    check('existing-archive-not-overwritten',lambda:reject(lambda:store.pack(source,target)))
    check('archive-inside-source-rejected',lambda:reject(lambda:store.pack(source,source/'bad.zip')))
    check('wrong-archive-hash-rejected',lambda:reject(lambda:store.EvidenceArchive(target,'0'*64)))
    def malformed(key,mutate,extra=None):
        with ZipFile(target) as z:manifest=json.loads(z.read(store.INDEX));data={k:z.read(k) for k in contents}
        mutate(manifest,data);bad=output/(key+'.zip')
        with ZipFile(bad,'w',compression=ZIP_DEFLATED) as z:
            for k,v in data.items():z.writestr(k,v)
            if extra:extra(z)
            z.writestr(store.INDEX,json.dumps(manifest))
        def verify():
            with store.EvidenceArchive(bad) as r:r.verify()
        reject(verify)
    check('member-tampering-rejected',lambda:malformed('tampered',lambda m,d:d.update({'initial.json':b'X'*len(d['initial.json'])})))
    check('duplicate-member-rejected',lambda:malformed('duplicate',lambda m,d:None,lambda z:z.writestr('initial.json',contents['initial.json'])))
    check('unsafe-archive-name-rejected',lambda:malformed('unsafe',lambda m,d:d.update({'../outside':b'x'})))
    check('extra-unindexed-file-rejected',lambda:malformed('extra',lambda m,d:d.update({'extra.json':b'x'})))
    check('wrong-format-rejected',lambda:malformed('version',lambda m,d:m.update(version='unknown')))
    check('wrong-byte-total-rejected',lambda:malformed('total',lambda m,d:m.update(totalBytes=m['totalBytes']+1)))
    def changed_source():
        copy=output/'changing';shutil.copytree(source,copy);original=store.inventory;calls=0
        def altered(path):
            nonlocal calls
            calls+=1
            if calls==2:(copy/'initial.json').write_bytes(b'changed while writing')
            return original(path)
        bad=output/'changing.zip'
        with patch.object(store,'inventory',altered):reject(lambda:store.pack(copy,bad))
        assert not bad.exists() and (output/'changing.zip.pending').exists()
    check('changed-source-keeps-unpublished-prefix',changed_source)
    def distinct_owners():
        with store.EvidenceArchive(target) as a,store.EvidenceArchive(target) as b:assert a.root/'initial.json'!=b.root/'initial.json'
    check('reader-identity-is-not-just-filename',distinct_owners)
    def truncated():
        bad=output/'truncated.zip';bad.write_bytes(target.read_bytes()[:-80])
        reject(lambda:store.EvidenceArchive(bad),errors=(BadZipFile,ValueError))
    check('truncated-archive-rejected',truncated)
    check('case-ambiguous-archive-rejected',lambda:malformed('case-duplicate',lambda m,d:d.update({'INITIAL.JSON':contents['initial.json']})))
    check('wrong-index-size-rejected',lambda:malformed('entry-size',lambda m,d:m['files'][0].update(bytes=m['files'][0]['bytes']+1)))
    def unpublished():
        copy=output/'unpublished';shutil.copytree(source,copy);(copy/'request.tmp').write_bytes(b'partial')
        bad=output/'unpublished.zip';reject(lambda:store.pack(copy,bad));assert not bad.exists()
    check('unpublished-source-record-rejected',unpublished)
    assert len(rows)==EXPECTED
    result=dict(completed=True,expected=EXPECTED,passed=sum(r['passed'] for r in rows),cases=rows,
        modelsExecuted=False,nativePhysicsExecuted=False,oldCapturesModified=False)
    (output/'controls.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    if result['passed']!=EXPECTED:raise RuntimeError('Recording controls failed; all fixtures retained.')


def assert_equal(a,b):assert a==b,(a,b)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('output',type=Path);a=p.parse_args();run(a.output.resolve())
