"""Independent audit of saved transfer/arithmetic bytes. Does not access a GPU."""
from hashlib import sha256
import json
from pathlib import Path
import struct
import sys

root=Path(sys.argv[1]).resolve()
read=lambda name:json.loads((root/name).read_text(encoding='utf-8-sig'))
protocol=read('protocol.json');device=read('device.json');compiler=read('compiler.json');result=read('results.json')
assert protocol['version']=='cuda-double-interop-v2'
counts=[1,255,256,257,4097,65537]
assert protocol['counts']==counts and protocol['launchesPerBuffer']==7 and protocol['guardsPerEnd']==8
assert protocol['exactArithmeticRequired'] and protocol['nativeContextCleanupRequired']
assert result['completed'] and result['interopPassed'] and result['contextDisposed']
assert not any(result[k] for k in ('biologicalModelExecuted','speedupMeasured','numericalSolverQualified','avatarRecoveryAssessed'))
assert compiler['archiveSha256']=='8C50A52467826167E0DBE99936140C52D62272BFC5849FE2D6587D050C8C5D29'
assert compiler['nvrtcMajor']==13 and compiler['nvrtcMinor']==0
assert device['Architecture'] in compiler['SupportedArchitectures'] and device['Ordinal']==0 and device['TotalMemoryBytes']>0
assert compiler['Options']==[f"--gpu-architecture=compute_{device['Architecture']}",'--std=c++17','--fmad=false','--ftz=false','--prec-div=true','--prec-sqrt=true']
assert sha256((root/'interop.ptx').read_bytes()).hexdigest().upper()==compiler['ptxSha256']
assert [r['count'] for r in result['cases']]==counts
for row in result['cases']:
    n=row['count'];initial=[-12345.625]*8+[1.0]*n+[-12345.625]*8
    expected=initial.copy()
    for i in range(n):expected[i+8]=1.0+7*(1+i%7)*2.0**-40
    before=(root/f'transfer-{n}.bin').read_bytes();after=(root/f'result-{n}.bin').read_bytes()
    assert before==struct.pack('<'+'d'*len(initial),*initial),f'transfer {n}'
    assert after==struct.pack('<'+'d'*len(expected),*expected),f'arithmetic/guards {n}'
    assert sha256(after).hexdigest().upper()==row['resultSha256']
    assert row['launches']==7 and row['passed'] and row['guardsPassed']
    assert row['first']==expected[8] and row['last']==expected[n+7]
report=dict(completed=True,exactTransferAndArithmeticCases=len(counts),contextCleanupReported=True,
    device=device,archiveSha256=compiler['archiveSha256'],gpuKernelInteropOnly=True,
    biologicalModelExecuted=False,speedupMeasured=False,numericalSolverQualified=False,avatarRecoveryAssessed=False)
(root/'analysis.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('Six GPU transfer/arithmetic cases independently match every saved byte, including guard regions.')
print('This is interoperability evidence, not a neuronal solver or speedup qualification.')
