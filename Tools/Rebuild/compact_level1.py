"""Offline edit of Level1 terrain only. Preserve its saved camera, HUD and first tower settings."""
from pathlib import Path
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[2]
assert ROOT.name == 'Team5-Rebuild-Clean'
path = ROOT / 'Assets/Rebuild/Scenes/Level1GridBattle.unity'
text = path.read_text(encoding='utf-8-sig')
blocks = {int(m[2]): [int(m[1]), m[3]] for m in re.finditer(r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)', text)}
owners, transforms, children, names = {}, {}, {}, {}
for key, (kind, body) in blocks.items():
    match = re.search(r'm_GameObject: \{fileID: (\d+)\}', body)
    if match: owners.setdefault(int(match[1]), []).append(key)
    if kind == 1: names[key] = re.search(r'^  m_Name: (.*)$', body, re.M)[1]
    if kind in (4, 224):
        go = int(match[1]); transforms[go] = key
        father = int(re.search(r'm_Father: \{fileID: (\d+)\}', body)[1])
        children.setdefault(father, []).append(go)
def named(name): return next(key for key, value in names.items() if value == name)
def subtree(go):
    result = [go] + owners.get(go, [])
    for child in children.get(transforms[go], []): result += subtree(child)
    return result
def set_position(body, pos):
    return re.sub(r'm_LocalPosition: \{[^}]+\}', 'm_LocalPosition: {x: %g, y: %g, z: %g}' % tuple(pos), body)
def set_children(body, refs):
    replacement = '  m_Children:\n' + ''.join('  - {fileID: %d}\n' % ref for ref in refs)
    return re.sub(r'  m_Children:(?: \[\]\n|\n(?:  - \{fileID: \d+\}\n)*)', replacement, body)
grid, border, tower_root = [named(n) for n in ('GameplayGrid', 'Decorative Border Tiles', 'Scene Towers (3x3 Footprints)')]
tower_go = named('Level 1 Tower 1 (3x3)')
assert len(children[transforms[tower_root]]) == 2, 'Compact conversion already applied or unexpected tower count.'
camera_blocks = {key: value[:] for key, value in blocks.items() if value[0] == 20 or
    (value[0] in (4, 224) and 'm_GameObject: {fileID: 845802669}' in value[1])}
ground = next(go for go in children[transforms[grid]] if any(re.search(r'^  kind: 0$', blocks[key][1], re.M) for key in owners[go]))
templates = {}
for label, go in [('cell', ground), ('border', children[transforms[border]][0])]:
    templates[label] = (go, {key: blocks[key][:] for key in subtree(go)})
for root in (grid, border):
    for go in children[transforms[root]]:
        for key in subtree(go): del blocks[key]
for go in children[transforms[tower_root]]:
    if go != tower_go:
        for key in subtree(go): del blocks[key]
next_id = 8100000000
def clone(label, parent, name, pos):
    global next_id
    go, template = templates[label]; mapping = {}
    for key in template:
        next_id += 1; mapping[key] = next_id
    for key, (kind, body) in template.items():
        body = re.sub(r'fileID: (\d+)', lambda m: 'fileID: ' + str(mapping.get(int(m[1]), int(m[1]))), body)
        if key == go: body = re.sub(r'^  m_Name: .*$', '  m_Name: ' + name, body, flags=re.M)
        if key == transforms[go]:
            body = set_position(body, pos)
            body = re.sub(r'm_Father: \{fileID: \d+\}', 'm_Father: {fileID: %d}' % parent, body)
        blocks[mapping[key]] = [kind, body]
    return mapping[transforms[go]], list(mapping.values())

# Keep unit-sized tiles and the map near its existing center; never scale the world root.
min_x, min_z, width, depth = 9, 6, 20, 14
route = [(x, 16) for x in range(10, 20)] + [(19, z) for z in range(15, 6, -1)]
road = {(x+dx, z+dz) for x, z in route for dx in (-1, 0, 1) for dz in (-1, 0, 1)}
spawn = {(10+dx, 16+dz) for dx in (-1, 0, 1) for dz in (-1, 0, 1)}
goal = {(19+dx, 7+dz) for dx in (-1, 0, 1) for dz in (-1, 0, 1)}
platform = {(22+dx, 16+dz) for dx in (-1, 0, 1) for dz in (-1, 0, 1)}
assert not platform & road
materials = {name: re.search(r'^guid: (\w+)', (ROOT / ('Assets/Rebuild/Materials/Minimal/'+name+'.mat.meta')).read_text(), re.M)[1]
             for name in ('Ground', 'Road', 'Spawn', 'Goal', 'TowerPlatform')}
cell_ids, grid_children, border_children = {}, [], []
for z in range(min_z, min_z+depth):
    for x in range(min_x, min_x+width):
        p = (x, z)
        kind, material = (4,'Spawn') if p in spawn else (5,'Goal') if p in goal else (1,'Road') if p in road else (3,'TowerPlatform') if p in platform else (0,'Ground')
        transform, ids = clone('cell', transforms[grid], 'Small Cell (%d,0,%d) %s' % (x,z,material), (x,0,z))
        grid_children.append(transform)
        for key in ids:
            if 'Team5.Rebuild.World.BattleCell' in blocks[key][1]:
                blocks[key][1] = re.sub(r'^  kind: \d+', '  kind: '+str(kind), blocks[key][1], flags=re.M); cell_ids[p] = key
            if blocks[key][0] == 23:
                blocks[key][1] = re.sub(r'(m_Materials:\n  - \{fileID: 2100000, guid: )\w+', lambda m: m[1]+materials[material], blocks[key][1])
for z in range(min_z-3, min_z+depth+3):
    for x in range(min_x-3, min_x+width+3):
        if min_x <= x < min_x+width and min_z <= z < min_z+depth: continue
        transform, _ = clone('border', transforms[border], 'Border Tile (%d,%d)' % (x,z), (x,0,z)); border_children.append(transform)
tower_id = next(key for key in owners[tower_go] if 'Team5.Rebuild.World.BattleTower' in blocks[key][1])
blocks[transforms[tower_go]][1] = set_position(blocks[transforms[tower_go]][1], (22,0.25,16))
blocks[transforms[tower_root]][1] = set_children(blocks[transforms[tower_root]][1], [transforms[tower_go]])
blocks[transforms[grid]][1] = set_children(blocks[transforms[grid]][1], grid_children)
blocks[transforms[border]][1] = set_children(blocks[transforms[border]][1], border_children)
for key, (_, body) in list(blocks.items()):
    if 'Team5.Rebuild.World.SceneRoute' in body:
        blocks[key][1] = re.sub(r'  cells:\n(?:  - \{fileID: \d+\}\n)+', '  cells:\n'+''.join('  - {fileID: %d}\n' % cell_ids[p] for p in route), body)
    if 'Team5.Rebuild.Session.BattleSession' in body:
        blocks[key][1] = re.sub(r'  towers:\n(?:  - \{fileID: \d+\}\n)+', '  towers:\n  - {fileID: %d}\n' % tower_id, body)
output = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join('--- !u!%d &%d\n%s' % (kind,key,body)
    for key,(kind,body) in sorted(blocks.items(), key=lambda item: item[1][0] == 1660057539))
for m in re.finditer(r'\{fileID: (-?\d+)\}', output): assert int(m[1]) == 0 or int(m[1]) in blocks
assert all(blocks[key] == value for key,value in camera_blocks.items())
assert len(cell_ids) == 280 and len(route) == 19
path.write_text(output, encoding='utf-8')
(ROOT / 'Logs/CompactLevel1Backup/Result.json').write_text(json.dumps({'grid':[width,depth], 'min':[min_x,min_z],
    'routePoints':len(route), 'borderTiles':len(border_children), 'cameraUnchanged':True, 'sceneHash':hashlib.sha256(path.read_bytes()).hexdigest()}, indent=2))
print('PASS compact map: 20x14 unit-size cells, 19 route points, first tower preserved, former second tower replaced by Goal, camera unchanged.')
