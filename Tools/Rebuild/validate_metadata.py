"""Read-only Unity GUID validation; offline C# builds cannot detect import failures."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
assert ROOT.name == 'Team5-Rebuild-Clean'
seen = {}
for path in (ROOT / 'Assets/Rebuild').rglob('*.meta'):
    match = re.search(r'^guid: (\w+)', path.read_text(encoding='utf-8-sig'), re.M)
    assert match, ('Missing GUID', path)
    guid = match[1]
    assert re.fullmatch(r'[0-9a-f]{32}', guid), ('Invalid Unity GUID', path, guid)
    assert guid not in seen, ('Duplicate GUID', path, seen.get(guid))
    seen[guid] = path
for suffix in ('*.unity', '*.asset', '*.prefab'):
    for path in (ROOT / 'Assets/Rebuild').rglob(suffix):
        for guid in re.findall(r'guid: (\w+)', path.read_text(encoding='utf-8-sig')):
            assert re.fullmatch(r'[0-9a-f]{32}', guid), ('Invalid GUID reference', path, guid)
print(f'PASS {len(seen)} Rebuild metadata GUIDs are valid/unique; Scene, asset and prefab GUID formats are valid.')
