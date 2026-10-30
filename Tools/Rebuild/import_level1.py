"""One-time, offline scene authoring. Runtime reads only the saved Unity Scene.

Reads legacy Team5 data without writing it; reuses the clean prototype's authored
HUD, session, primitive meshes, materials and camera orientation. Never overwrites
an existing destination scene or the source prototype.
This authors the original 39x27 baseline; compact_level1.py applies the later half-size, single-tower revision.
"""
import hashlib
import json
import math
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
OLD = ROOT.parent / 'Team5'
SOURCE = ROOT / 'Assets/Rebuild/Scenes/SquadBattlePrototype.unity'
DEST = ROOT / 'Assets/Rebuild/Scenes/Level1GridBattle.unity'
REPORT = ROOT / 'Logs/Level1Migration'
assert ROOT.name == 'Team5-Rebuild-Clean'
assert not DEST.exists(), 'Destination exists; edit its Scene directly instead of regenerating.'
REPORT.mkdir(parents=True, exist_ok=True)
protected = [SOURCE, OLD / 'Assets/Defense/Data/DefaultStage.asset',
             OLD / 'Assets/Defense/Data/DefaultTower.asset', OLD / 'Assets/Defense/Scenes/DefenseSandbox.unity']
hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
text = SOURCE.read_text(encoding='utf-8-sig')
blocks = {int(m[2]): [int(m[1]), m[3]] for m in re.finditer(
    r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)', text)}
owners, transforms, positions, children, names = {}, {}, {}, {}, {}
for key, (kind, body) in blocks.items():
    owner = re.search(r'm_GameObject: \{fileID: (\d+)\}', body)
    if owner:
        owners.setdefault(int(owner[1]), []).append(key)
    if kind == 1:
        names[key] = re.search(r'^  m_Name: (.*)$', body, re.M)[1]
    if kind in (4, 224):
        go = int(owner[1])
        transforms[go] = key
        father = int(re.search(r'm_Father: \{fileID: (\d+)\}', body)[1])
        children.setdefault(father, []).append(go)
        pos = re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', body)
        positions[go] = [float(pos[i]) for i in (1, 2, 3)]

def named(name):
    return next(key for key, value in names.items() if value == name)

def subtree(go):
    result = [go] + owners.get(go, [])
    for child in children.get(transforms[go], []):
        result += subtree(child)
    return result

def component(go, phrase):
    return next(key for key in owners[go] if phrase in blocks[key][1])

def vector(position):
    return '{x: %g, y: %g, z: %g}' % tuple(position)

def position(body, value):
    return re.sub(r'm_LocalPosition: \{[^}]+\}', 'm_LocalPosition: ' + vector(value), body)

def set_children(body, references):
    value = '  m_Children:' + ('\n' + ''.join('  - {fileID: %d}\n' % ref for ref in references) if references else ' []\n')
    return re.sub(r'  m_Children:(?: \[\]\n|\n(?:  - \{fileID: \d+\}\n)*)', value, body)

grid_go, border_go, towers_go = (named(n) for n in ('GameplayGrid', 'Decorative Border Tiles', 'Scene Towers (3x3 Footprints)'))
templates = {}
for go in children[transforms[grid_go]]:
    cell = next((key for key in owners[go] if 'Team5.Rebuild.World.BattleCell' in blocks[key][1]), None)
    if cell is not None and re.search(r'^  kind: 0$', blocks[cell][1], re.M):
        templates['cell'] = (go, {key: blocks[key][:] for key in subtree(go)})
        break
for label, go in [('border', children[transforms[border_go]][0]), ('tower', named('Tower 1 (3x3)'))]:
    templates[label] = (go, {key: blocks[key][:] for key in subtree(go)})
for root in (grid_go, border_go, towers_go):
    for child in children[transforms[root]]:
        for key in subtree(child):
            del blocks[key]

next_id = 8000000000
def clone(label, parent, name, pos):
    global next_id
    go, template = templates[label]
    mapping = {}
    for key in template:
        next_id += 1
        mapping[key] = next_id
    for key, (kind, body) in template.items():
        body = re.sub(r'fileID: (\d+)', lambda m: 'fileID: ' + str(mapping.get(int(m[1]), int(m[1]))), body)
        if key == go:
            body = re.sub(r'^  m_Name: .*$', '  m_Name: ' + name, body, flags=re.M)
        if key == transforms[go]:
            body = position(body, pos)
            body = re.sub(r'm_Father: \{fileID: \d+\}', 'm_Father: {fileID: %d}' % parent, body)
        blocks[mapping[key]] = [kind, body]
    return mapping[go], mapping[transforms[go]], list(mapping.values())

stage = (OLD / 'Assets/Defense/Data/DefaultStage.asset').read_text()
legacy_scene = (OLD / 'Assets/Defense/Scenes/DefenseSandbox.unity').read_text()
legacy_guid = re.search(r'^guid: (\w+)', (OLD / 'Assets/Defense/Data/DefaultStage.asset.meta').read_text(), re.M)[1]
assert 'layout: {fileID: 11400000, guid: ' + legacy_guid in legacy_scene
legacy_points = [(float(x), float(y)) for x, y in re.findall(r'^  - \{x: ([^,]+), y: ([^}]+)\}', stage.split('lowerWaypoints:')[0], re.M)]
legacy_towers = [(float(x), float(y), int(d), float(r)) for x, y, d, r in re.findall(
    r'- position: \{x: ([^,]+), y: ([^}]+)\}.*?attackDirection: (\d+)\s+attackRange: ([\d.]+)', stage, re.S)]
assert legacy_points == [(-6, 3), (0, 3), (0, 0), (0, -3), (6, -3)]
assert len(legacy_towers) == 2
# Offline conversion only: the resulting scene contains native X/Z Transforms and BattleCell references.
def convert(x, y):
    return (int((x + 6) * 3 + 1), int((y + 4) * 3 + 1))
corners = [convert(x, y) for x, y in legacy_points]
route = [corners[0]]
for end in corners[1:]:
    x, z = route[-1]
    dx, dz = end[0] - x, end[1] - z
    assert dx == 0 or dz == 0
    step = ((dx > 0) - (dx < 0), (dz > 0) - (dz < 0))
    while (x, z) != end:
        x, z = x + step[0], z + step[1]
        route.append((x, z))
road = {(x + dx, z + dz) for x, z in route for dx in (-1, 0, 1) for dz in (-1, 0, 1)}
spawn = {(corners[0][0] + x, corners[0][1] + z) for x in (-1, 0, 1) for z in (-1, 0, 1)}
goal = {(corners[-1][0] + x, corners[-1][1] + z) for x in (-1, 0, 1) for z in (-1, 0, 1)}
tower_positions = [convert(x, y) for x, y, _, _ in legacy_towers]
platforms = {(x + dx, z + dz) for x, z in tower_positions for dx in (-1, 0, 1) for dz in (-1, 0, 1)}
assert not platforms & road
materials = {}
for name in ('Ground', 'Road', 'TowerPlatform', 'Spawn', 'Goal'):
    materials[name] = re.search(r'^guid: (\w+)', (ROOT / ('Assets/Rebuild/Materials/Minimal/' + name + '.mat.meta')).read_text(), re.M)[1]
width, depth = 39, 27
grid_children, border_children, tower_children = [], [], []
cell_ids, cell_kinds = {}, {}
for z in range(depth):
    for x in range(width):
        point = (x, z)
        kind, material = (4, 'Spawn') if point in spawn else (5, 'Goal') if point in goal else (1, 'Road') if point in road else (3, 'TowerPlatform') if point in platforms else (0, 'Ground')
        go, transform, ids = clone('cell', transforms[grid_go], 'Small Cell (%d,0,%d) %s' % (x, z, material), (x, 0, z))
        grid_children.append(transform)
        for key in ids:
            if 'Team5.Rebuild.World.BattleCell' in blocks[key][1]:
                blocks[key][1] = re.sub(r'^  kind: \d+', '  kind: ' + str(kind), blocks[key][1], flags=re.M)
                cell_ids[point] = key
            if blocks[key][0] == 23:
                blocks[key][1] = re.sub(r'(m_Materials:\n  - \{fileID: 2100000, guid: )\w+', lambda m: m[1] + materials[material], blocks[key][1])
        cell_kinds[point] = kind
for z in range(-3, depth + 3):
    for x in range(-3, width + 3):
        if 0 <= x < width and 0 <= z < depth:
            continue
        _, transform, _ = clone('border', transforms[border_go], 'Border Tile (%d,%d)' % (x, z), (x, 0, z))
        border_children.append(transform)
tower_component_ids, tower_data = [], []
direction_map = {0: 1, 1: 2, 2: 3, 3: 4}  # legacy Right/Left/Up/Down -> native +X/-X/+Z/-Z
for index, ((x, z), (_, _, direction, attack_range)) in enumerate(zip(tower_positions, legacy_towers), 1):
    _, transform, ids = clone('tower', transforms[towers_go], 'Level 1 Tower %d (3x3)' % index, (x, 0.25, z))
    tower_children.append(transform)
    cid = next(key for key in ids if 'Team5.Rebuild.World.BattleTower' in blocks[key][1])
    for field, value in {'range': attack_range * 3, 'attackArea': direction_map[direction], 'attackWidth': 0.9,
                         'maxHp': 20, 'damage': 1, 'attackInterval': 1}.items():
        blocks[cid][1] = re.sub(r'^  ' + field + r': .*$', '  %s: %g' % (field, value), blocks[cid][1], flags=re.M)
    tower_component_ids.append(cid)
    tower_data.append({'position': [x, 0.25, z], 'range': attack_range * 3, 'area': direction_map[direction], 'width': 0.9, 'hp': 20, 'damage': 1, 'interval': 1})
for go, refs in [(grid_go, grid_children), (border_go, border_children), (towers_go, tower_children)]:
    blocks[transforms[go]][1] = set_children(blocks[transforms[go]][1], refs)
for key, (kind, body) in list(blocks.items()):
    if 'Team5.Rebuild.World.SceneRoute' in body:
        body = re.sub(r'  cells:\n(?:  - \{fileID: \d+\}\n)+', '  cells:\n' + ''.join('  - {fileID: %d}\n' % cell_ids[p] for p in route), body)
    if 'Team5.Rebuild.Session.BattleSession' in body:
        body = re.sub(r'  towers:\n(?:  - \{fileID: \d+\}\n)+', '  towers:\n' + ''.join('  - {fileID: %d}\n' % ref for ref in tower_component_ids), body)
        body = re.sub(r'^  unitCount: .*$', '  unitCount: 1', body, flags=re.M)
        body = re.sub(r'^  initialRoster: .*$', '  initialRoster: {fileID: 11400000, guid: c7fea7eb60294bfa83cb342d4b00af06, type: 2}', body, flags=re.M)
        body = re.sub(r'^  useSceneRosterOnly: .*\n', '', body, flags=re.M)
        body += '  useSceneRosterOnly: 1\n'
        body = re.sub(r'^  levelTimeMultiplier: .*\n', '', body, flags=re.M)
        body += '  levelTimeMultiplier: 2\n'
    body = body.replace('m_Name: Macro Grid Battle Prototype\n', 'm_Name: Level 1 Grid Battle\n')
    body = body.replace('m_Name: BattleSession (Nine Independent Units)\n', 'm_Name: BattleSession (Level 1 Captain)\n')
    blocks[key][1] = body

session_id = next(key for key, (_, body) in blocks.items() if 'Team5.Rebuild.Session.BattleSession' in body)
session_go = int(re.search(r'm_GameObject: \{fileID: (\d+)\}', blocks[session_id][1])[1])
blocks[session_go][1] = blocks[session_go][1].replace('  m_Layer:', '  - component: {fileID: 884410001}\n  m_Layer:', 1)
blocks[884410001] = [114, '''MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 1e9dc2656d534b42b32d0d612776dca0, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Team5.Rebuild.Presentation::Team5.Rebuild.Presentation.LegacyUI.LevelOneTutorial
  session: {fileID: %d}
''' % (session_go, session_id)]

# Frame only the new scene, preserving the user's prototype camera and its orientation/projection.
camera_id = next(key for key, (kind, _) in blocks.items() if kind == 20)
camera_body = blocks[camera_id][1]
camera_go = int(re.search(r'm_GameObject: \{fileID: (\d+)\}', camera_body)[1])
camera_transform = transforms[camera_go]
q = [float(v) for v in re.search(r'm_LocalRotation: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}', blocks[camera_transform][1]).groups()]
def rotate(v):
    x, y, z, w = q
    tx, ty, tz = 2 * (y*v[2]-z*v[1]), 2 * (z*v[0]-x*v[2]), 2 * (x*v[1]-y*v[0])
    return (v[0]+w*tx+y*tz-z*ty, v[1]+w*ty+z*tx-x*tz, v[2]+w*tz+x*ty-y*tx)
right, up, forward = rotate((1, 0, 0)), rotate((0, 1, 0)), rotate((0, 0, 1))
dot = lambda a, b: sum(x*y for x, y in zip(a, b))
fov = float(re.search(r'^  field of view: (.+)', camera_body, re.M)[1])
tan = math.tan(math.radians(fov / 2))
extents, center = ((width+6)/2, 1.5, (depth+6)/2), ((width-1)/2, 0.5, (depth-1)/2)
offsets = [(extents[0]*x, extents[1]*y, extents[2]*z) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
distance = max(max(abs(dot(v, right))*1.08/(tan*16/9)-dot(v, forward), abs(dot(v, up))*1.08/tan-dot(v, forward)) for v in offsets) + 2
ortho_size = max(max(abs(dot(v, up))*1.08, abs(dot(v, right))*1.08/(16/9)) for v in offsets)
blocks[camera_transform][1] = position(blocks[camera_transform][1], [center[i]-forward[i]*distance for i in range(3)])
camera_body = re.sub(r'^  orthographic size: .*$', '  orthographic size: %g' % ortho_size, camera_body, flags=re.M)
camera_body = re.sub(r'^  far clip plane: .*$', '  far clip plane: %g' % (distance + 100), camera_body, flags=re.M)
blocks[camera_id][1] = camera_body

# Validate every local object reference, route footprint, terrain cell and tower before writing.
output = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join('--- !u!%d &%d\n%s' % (kind, key, body) for key, (kind, body) in sorted(blocks.items(), key=lambda item: item[1][0] == 1660057539))
for match in re.finditer(r'\{fileID: (-?\d+)\}', output):
    ref = int(match[1])
    assert ref == 0 or ref in blocks, ('Dangling reference', ref)
assert len(cell_ids) == width * depth
assert len(route) == 55 and len(set(route)) == 55
assert all(abs(a[0]-b[0])+abs(a[1]-b[1]) == 1 for a, b in zip(route, route[1:]))
assert road <= set(cell_ids)
assert all(cell_kinds[p] in (1, 4, 5) for p in road)
assert all(cell_kinds[p] == 3 for p in platforms)
DEST.write_text(output, encoding='utf-8')
guid = uuid.uuid4().hex
DEST.with_suffix('.unity.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
for p in protected:
    assert hashlib.sha256(p.read_bytes()).hexdigest() == hashes[str(p)], ('Protected file changed', p)
report = {'scene': str(DEST.relative_to(ROOT)), 'guid': guid, 'grid': [width, depth], 'smallCells': len(cell_ids),
          'decorativeCells': len(border_children), 'route': [[x, 0.25, z] for x, z in route],
          'walkable': [[x, 0, z] for x, z in sorted(road)], 'towers': tower_data, 'sourceHashes': hashes,
          'camera': {'position': [center[i]-forward[i]*distance for i in range(3)], 'orthographicSize': ortho_size}}
(REPORT / 'Level1.json').write_text(json.dumps(report, indent=2))
print(json.dumps({k: report[k] for k in ('scene', 'grid', 'smallCells', 'decorativeCells', 'guid')}))
print('PASS: 55 ordered real-cell waypoints; all nine slot trajectories clear; two non-overlapping 3x3 platforms; local references resolved; protected files unchanged.')
