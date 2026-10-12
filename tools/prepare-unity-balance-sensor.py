"""Port Avatar's balance-error encoding; no balance dynamics or pose integration."""
import argparse
import hashlib
from pathlib import Path
import re


def block(source, start):
    opening = source.index('{', start)
    depth = 1
    for end in range(opening + 1, len(source)):
        depth += (source[end] == '{') - (source[end] == '}')
        if depth == 0:
            return source[start:end + 1]
    raise ValueError('Unclosed canonical source block.')


def generate(root):
    source = (root / 'src/NRE.SimAvatar/AvatarBalanceDynamics.cs').read_text(encoding='utf-8-sig')
    phase = block(source, source.index('public enum AvatarBalancePhase'))
    method = block(source, source.index('    private static double ResolveBalanceError('))
    method = method.replace('private static double', 'public static double', 1)
    constants = []
    for name in ['FallCommitSeconds', 'AcceptablePosturalTiltRadians', 'FallenAngleRadians']:
        value = re.search(r'private const double ' + name + r' = ([^;]+);', source).group(1)
        constants.append('    private const double ' + name + ' = ' + value + ';')
    selected = phase + '\n' + '\n'.join(constants) + '\n' + method
    digest = hashlib.sha256(selected.encode()).hexdigest()
    return ('// Generated from AvatarBalanceDynamics.cs; do not edit.\n// Encoding SHA256: ' + digest +
        '\nusing System;\nnamespace Nre.UnityWorld {\n' + phase +
        '\npublic static class AvatarBalanceSensor {\n' + '\n'.join(constants) + '\n' + method + '\n}\n}\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    destination = root / 'UnityWorld/Assets/NRE/Runtime/BalanceErrorCore.cs'
    expected = generate(root)
    if args.check:
        if destination.read_text(encoding='utf-8-sig') != expected:
            raise SystemExit('Unity balance-error encoding is stale.')
    else:
        destination.write_text(expected, encoding='utf-8')


if __name__ == '__main__':
    main()
