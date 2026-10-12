"""Compare saved isolated target-pulse responses with their repeated controls."""
import argparse
import json
from pathlib import Path


def analyse(directory: Path) -> dict:
    report = json.loads((directory / 'response-report.json').read_text())
    traces = {case['name']: [json.loads(line) for line in
        (directory / (case['name'] + '-trace.jsonl')).read_text().splitlines()]
        for case in report['cases']}
    baseline = traces['control-before']

    def physical(row):
        # Sequence/time metadata is not a body-state difference.
        return [row['body']['articulation'], row['centerOfMass'], row['supportCenter'],
            row['centerOfMassVelocity'], row['preStepErrorX'], row['preStepErrorZ']]

    controls_match = all(physical(a) == physical(b) for a, b in
        zip(baseline, traces['control-after'])) and len(baseline) == len(traces['control-after'])
    pre_pulse_match = all(physical(a) == physical(b) for rows in traces.values()
        for a, b in zip(rows, baseline) if a['seconds'] < report['pulseStartSeconds'])
    cases = []
    for case in report['cases']:
        rows = traces[case['name']]
        samples = []
        for target in [1.11, 1.31, 1.51, 2.01, 3.99]:
            index = min(range(len(rows)), key=lambda i: abs(rows[i]['seconds'] - target))
            row, control = rows[index], baseline[index]
            samples.append({'seconds': row['seconds'],
                'deltaLateralErrorMillimeters': (row['preStepErrorX'] - control['preStepErrorX']) * 1000,
                'deltaLateralVelocityMillimetersPerSecond':
                    (row['centerOfMassVelocity']['x'] - control['centerOfMassVelocity']['x']) * 1000,
                'deltaActuatorTorquesNewtonMeters': {key: row[key] - control[key] for key in
                    ['leftHipTorque', 'rightHipTorque', 'leftAnkleTorque', 'rightAnkleTorque']}})
        cases.append({'name': case['name'], 'finite': case['finite'], 'samples': samples})
    return {'completed': report['completed'], 'finite': report['finite'], 'isolated': report['isolated'],
        'controlsMatchExactly': controls_match, 'prePulsePhysicalStatesMatchExactly': pre_pulse_match,
        'cases': cases, 'standingQualified': False, 'neuronalControlAssessed': False,
        'liveSensoryDeliveryAssessed': False,
        'interpretation': 'Small target pulses produce physical responses; early bilateral hip response has the expected mirrored sign. Responses are small and affected by retained ankle feedback. This is not an open-loop transfer function or standing qualification.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    args = parser.parse_args()
    result = analyse(args.directory)
    output = args.directory / 'trace-analysis.json'
    output.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({key: result[key] for key in ['completed', 'finite', 'isolated',
        'controlsMatchExactly', 'prePulsePhysicalStatesMatchExactly']}))
    for case in result['cases']:
        sample = case['samples'][1]
        print(case['name'], 'delta X at 1.31 s (mm):', round(sample['deltaLateralErrorMillimeters'], 5))
