"""Download the pinned, hash-checked anatomical source meshes for preparation."""
import argparse
import concurrent.futures
import hashlib
import json
from pathlib import Path
import urllib.request

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('destination', type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
manifest = json.loads((root / 'src/NRE.BlazorEditor/wwwroot/data/models/subcortical-parts.json').read_text())
args.destination.mkdir(parents=True, exist_ok=True)

def download(source):
    data = urllib.request.urlopen(source['url'], timeout=30).read()
    if hashlib.sha256(data).hexdigest() != source['sha256']:
        raise ValueError('Source hash mismatch: ' + source['file'])
    (args.destination / source['file']).write_bytes(data)

with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    list(pool.map(download, manifest['sources']))
(args.destination / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
print(f"Downloaded and verified {len(manifest['sources'])} anatomical source files.")
