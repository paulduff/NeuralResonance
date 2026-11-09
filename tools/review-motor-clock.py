"""Review a reported-complete clock capture; audit saved rows without rerunning its schedules."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))

def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result

def main(capture,output):
    assert not output.exists();capture=capture.resolve()
    hashing=module('saved_motor_hashes',ROOT/'tools/review-olive-and-motor-20261109.py')
    report=hashing.verify(capture,['binaries.json'],'167ddd6203ec362b46e713a6b1f6efcbfa145349')
    status=read(capture/'window-status.json')
    assert status['ClockAuditPassed'] and status['OriginalMismatchReproduced']
    assert not any(status[k] for k in ('LiveBrainAccessed','OldSavesLoaded','GpuExecuted','SpeedupMeasured',
        'ActiveCandidateQualified','PhysiologyQualified','AvatarRecoveryAssessed','ModelExecuted','CausalCalciumExplanationEstablished'))
    analyzer=module('saved_clock_analysis',ROOT/'tools/analyse-motor-clock.py')
    # The reported window already ran the independent C#/Python schedules. Here
    # verify complete captured files/binaries and recompute totals from saved CSVs.
    analysis=analyzer.audit(capture,replay_arithmetic=False)
    assert analysis==read(capture/'clock-analysis.json')
    assert analysis['originalClockMismatchReproduced'] and analysis['endpointDifferenceConservesRepresentedTime']
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedRowAnalysisReproduced=True,scheduleReplayReexecuted=False,modelExecuted=False,analysis=analysis)
    output.mkdir();(output/'review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='analysis'},indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('capture',type=Path);parser.add_argument('output',type=Path)
    parser.add_argument('--operator-reported',action='store_true',required=True);args=parser.parse_args();main(args.capture,args.output.resolve())
