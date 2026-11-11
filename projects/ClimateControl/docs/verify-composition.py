from pathlib import Path
import json, hashlib
root=Path(__file__).resolve().parents[1]
bom=json.loads((root/'docs/electronic-composition.json').read_text())
count=0
for component in bom['components']:
    assert (root/component['path']).is_dir(), component['id']
    for item in component['files']:
        path=root/item['path']
        assert hashlib.sha256(path.read_bytes()).hexdigest()==item['sha256'], item['path']
        count+=1
print(f'PASS: {len(bom["components"])} components, {count} source file hashes')
