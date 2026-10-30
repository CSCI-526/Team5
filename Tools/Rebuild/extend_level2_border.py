"""One-time saved Scene edit: extend Level2's three decorative rings to five."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
assert ROOT.name == 'Team5-Rebuild-Clean'
path = ROOT / 'Assets/Rebuild/Scenes/Level2GridBattle.unity'
text = path.read_text()
blocks = {int(m[2]): (int(m[1]), m[3]) for m in re.finditer(r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)', text)}
root_go = next(id for id, (kind, body) in blocks.items() if kind == 1 and '  m_Name: Decorative Border Tiles\n' in body)
parent = next(id for id, (kind, body) in blocks.items() if kind == 4 and f'm_GameObject: {{fileID: {root_go}}}' in body)
tiles = {id: body for id, (kind, body) in blocks.items() if kind == 4 and f'm_Father: {{fileID: {parent}}}' in body}
def position(body):
    return tuple(float(v) for v in re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', body).groups())
coords = {position(body) for body in tiles.values()}
assert len(coords) == 432 and min(x for x,y,z in coords) == -3 and max(x for x,y,z in coords) == 41, 'Expected original three rings; refuse repeated or unexpected edit.'
template_transform, template_body = next(iter(tiles.items()))
template_go = int(re.search(r'm_GameObject: \{fileID: (\d+)\}', template_body)[1])
template_ids = [template_go] + [id for id, (_, body) in blocks.items() if f'm_GameObject: {{fileID: {template_go}}}' in body]
assert {blocks[id][0] for id in template_ids} == {1, 4, 23, 33}, 'Decorative template must have no scripts or colliders.'
next_id = max(id for id, (kind, _) in blocks.items() if kind != 1660057539) + 1
new_blocks, new_children = {}, []
for z in range(-5, 32):
    for x in range(-5, 44):
        if -3 <= x <= 41 and -3 <= z <= 29:
            continue
        mapping = {id: next_id+i for i, id in enumerate(template_ids)}
        next_id += len(mapping)
        for id in template_ids:
            kind, body = blocks[id]
            body = re.sub(r'fileID: (\d+)', lambda m: 'fileID: '+str(mapping.get(int(m[1]), int(m[1]))), body)
            if kind == 1:
                body = re.sub(r'^  m_Name: .*$', f'  m_Name: Border Tile ({x},{z})', body, flags=re.M)
            if kind == 4:
                body = re.sub(r'm_LocalPosition: \{[^}]+\}', f'm_LocalPosition: {{x: {x}, y: 0, z: {z}}}', body)
                new_children.append(mapping[id])
                assert 'm_LocalScale: {x: 1, y: 0.5, z: 1}' in body
            new_blocks[mapping[id]] = (kind, body)
assert len(new_children) == 328
assert next_id < 2**63, 'Unity fileIDs must fit a signed 64-bit integer.'
kind, parent_body = blocks[parent]
replacement = '  m_Children:\n' + ''.join(f'  - {{fileID: {id}}}\n' for id in [*tiles, *new_children])
parent_body = re.sub(r'  m_Children:\n(?:  - \{fileID: \d+\}\n)+', replacement, parent_body)
output = text.replace(blocks[parent][1], parent_body, 1)
extra = ''.join(f'--- !u!{kind} &{id}\n{body}' for id, (kind, body) in new_blocks.items())
scene_roots = re.search(r'^--- !u!1660057539 &', output, re.M)
assert scene_roots
output = output[:scene_roots.start()] + extra + output[scene_roots.start():]
result = {int(m[2]): (int(m[1]), m[3]) for m in re.finditer(r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)', output)}
assert all(result[id] == value for id, value in blocks.items() if id != parent), 'Existing Scene objects changed.'
assert all(int(id) == 0 or int(id) in result for id in re.findall(r'\{fileID: (-?\d+)\}', output)), 'Broken local reference.'
backup = ROOT / 'Logs/Level2BorderBackup'
backup.mkdir(exist_ok=True)
assert not (backup / path.name).exists(), 'Backup already exists; refuse overwrite.'
(backup / path.name).write_bytes(path.read_bytes())
path.write_text(output)
print('PASS Added 328 saved decorative tiles: 3 -> 5 rings, 432 -> 760 tiles. All existing objects, camera and battle configuration unchanged.')
