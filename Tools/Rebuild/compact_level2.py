"""One-time edit-time compaction of Level2. No runtime terrain generation."""
from pathlib import Path
import re
import json
import hashlib

ROOT = Path(__file__).resolve().parents[2]
assert ROOT.name == 'Team5-Rebuild-Clean'
path = ROOT / 'Assets/Rebuild/Scenes/Level2GridBattle.unity'
text = path.read_text()
blocks = {int(m[2]): [int(m[1]), m[3]] for m in re.finditer(r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)', text)}
original = {id: value[:] for id, value in blocks.items()}
names, owners, transforms, children = {}, {}, {}, {}
for id, (kind, body) in blocks.items():
    if kind == 1: names[id] = re.search(r'^  m_Name: (.*)$', body, re.M)[1]
    match = re.search(r'm_GameObject: \{fileID: (\d+)\}', body)
    if match: owners.setdefault(int(match[1]), []).append(id)
    if kind in (4, 224):
        go = int(match[1]); transforms[go] = id
        parent = int(re.search(r'm_Father: \{fileID: (\d+)\}', body)[1])
        children.setdefault(parent, []).append(go)
def named(name): return next(id for id, value in names.items() if value == name)
def subtree(go):
    result = [go] + owners.get(go, [])
    for child in children.get(transforms[go], []): result += subtree(child)
    return result
def set_position(body, p):
    return re.sub(r'm_LocalPosition: \{[^}]+\}', 'm_LocalPosition: {x: %g, y: %g, z: %g}' % p, body)
def set_children(body, refs):
    replacement = '  m_Children:\n' + ''.join('  - {fileID: %d}\n' % id for id in refs)
    return re.sub(r'  m_Children:(?: \[\]\n|\n(?:  - \{fileID: \d+\}\n)*)', replacement, body)
grid, border, tower_root = [named(n) for n in ('GameplayGrid','Decorative Border Tiles','Scene Towers (3x3 Footprints)')]
assert sum('Team5.Rebuild.World.BattleCell' in body for _,body in blocks.values()) == 39*27, 'Unexpected map or already compacted.'
tower_gos = children[transforms[tower_root]]
assert len(tower_gos) == 2
ground = next(go for go in children[transforms[grid]] if any(re.search(r'^  kind: 0$', blocks[id][1], re.M) for id in owners[go]))
templates = {label: (go, {id: blocks[id][:] for id in subtree(go)}) for label,go in
    [('cell',ground),('border',children[transforms[border]][0])]}
for root in (grid,border):
    for go in children[transforms[root]]:
        for id in subtree(go): del blocks[id]
next_id = max(id for id,(kind,_) in original.items() if kind != 1660057539) + 1
def clone(label, parent, name, position):
    global next_id
    go, template = templates[label]
    mapping = {id: next_id+i for i,id in enumerate(template)}; next_id += len(mapping)
    for id,(kind,body) in template.items():
        body = re.sub(r'fileID: (\d+)', lambda m: 'fileID: '+str(mapping.get(int(m[1]),int(m[1]))), body)
        if id == go: body = re.sub(r'^  m_Name: .*$', '  m_Name: '+name, body, flags=re.M)
        if id == transforms[go]:
            body = set_position(body,position)
            body = re.sub(r'm_Father: \{fileID: \d+\}', 'm_Father: {fileID: %d}' % parent, body)
        blocks[mapping[id]] = [kind,body]
    return mapping[transforms[go]], list(mapping.values())

min_x,min_z,width,depth = 9,6,20,14
# Retain the original horizontal / vertical / horizontal route and 3-cell clearance.
# Its lower lane leaves a complete 3x3 platform below it for the upward-firing tower.
route = [(x,16) for x in range(10,20)] + [(19,z) for z in range(15,9,-1)] + [(x,10) for x in range(20,28)]
road = {(x+dx,z+dz) for x,z in route for dx in (-1,0,1) for dz in (-1,0,1)}
spawn = {(10+dx,16+dz) for dx in (-1,0,1) for dz in (-1,0,1)}
goal = {(27+dx,10+dz) for dx in (-1,0,1) for dz in (-1,0,1)}
tower_positions = [(22,.25,16),(19,.25,7)]
platform = {(int(x)+dx,int(z)+dz) for x,y,z in tower_positions for dx in (-1,0,1) for dz in (-1,0,1)}
assert not platform & road
assert all(min_x<=x<min_x+width and min_z<=z<min_z+depth for x,z in road|platform)
materials = {name: re.search(r'^guid: (\w+)', (ROOT/('Assets/Rebuild/Materials/Minimal/'+name+'.mat.meta')).read_text(),re.M)[1]
    for name in ('Ground','Road','Spawn','Goal','TowerPlatform')}
cell_ids,grid_refs,border_refs = {},[],[]
for z in range(min_z,min_z+depth):
    for x in range(min_x,min_x+width):
        kind,mat = (4,'Spawn') if (x,z) in spawn else (5,'Goal') if (x,z) in goal else (1,'Road') if (x,z) in road else (3,'TowerPlatform') if (x,z) in platform else (0,'Ground')
        transform,ids = clone('cell',transforms[grid],f'Small Cell ({x},0,{z}) {mat}',(x,0,z))
        grid_refs.append(transform)
        for id in ids:
            if 'Team5.Rebuild.World.BattleCell' in blocks[id][1]:
                blocks[id][1] = re.sub(r'^  kind: \d+',f'  kind: {kind}',blocks[id][1],flags=re.M)
                cell_ids[x,z] = id
            if blocks[id][0] == 23:
                blocks[id][1] = re.sub(r'(m_Materials:\n  - \{fileID: 2100000, guid: )\w+',lambda m:m[1]+materials[mat],blocks[id][1])
for z in range(min_z-5,min_z+depth+5):
    for x in range(min_x-5,min_x+width+5):
        if min_x<=x<min_x+width and min_z<=z<min_z+depth: continue
        transform,_ = clone('border',transforms[border],f'Border Tile ({x},{z})',(x,0,z)); border_refs.append(transform)
changed = {transforms[grid],transforms[border]}
blocks[transforms[grid]][1] = set_children(blocks[transforms[grid]][1],grid_refs)
blocks[transforms[border]][1] = set_children(blocks[transforms[border]][1],border_refs)
for go,p in zip(tower_gos,tower_positions):
    blocks[transforms[go]][1] = set_position(blocks[transforms[go]][1],p); changed.add(transforms[go])
for id,(_,body) in list(blocks.items()):
    if 'Team5.Rebuild.World.SceneRoute' in body:
        blocks[id][1] = re.sub(r'  cells:\n(?:  - \{fileID: \d+\}\n)+','  cells:\n'+''.join('  - {fileID: %d}\n'%cell_ids[p] for p in route),body)
        changed.add(id)
assert all(value == original[id] for id,value in blocks.items() if id in original and id not in changed), 'Unrelated Scene data changed'
assert len(cell_ids)==280 and len(border_refs)==440 and len(route)==24 and next_id<2**63
output = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+''.join('--- !u!%d &%d\n%s'%(kind,id,body)
    for id,(kind,body) in sorted(blocks.items(),key=lambda item:item[1][0]==1660057539))
assert all(int(id)==0 or int(id) in blocks for id in re.findall(r'\{fileID: (-?\d+)\}',output))
backup = ROOT/'Logs/CompactLevel2Backup'
backup.mkdir(exist_ok=True)
assert not (backup/path.name).exists(), 'Do not overwrite an existing backup'
(backup/path.name).write_bytes(path.read_bytes())
path.write_text(output)
(backup/'Result.json').write_text(json.dumps(dict(grid=[width,depth],routePoints=len(route),roadWidth=3,
    borderRings=5,borderTiles=len(border_refs),towers=tower_positions,cameraAndUiUnchanged=True,
    sceneHash=hashlib.sha256(path.read_bytes()).hexdigest()),indent=2))
print('PASS Level2 compacted: 39x27 -> 20x14, 24 route cells, 3-wide road, two original towers, five decorative rings. Camera/UI/combat configuration unchanged.')
