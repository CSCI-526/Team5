"""Read saved Scenes without opening Unity; export Level2 for the offline rules runner."""
from pathlib import Path
import re
import json
import subprocess

ROOT = Path(__file__).resolve().parents[2]
def blocks(text):
    return {int(m[2]): (int(m[1]), m[3]) for m in re.finditer(r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)', text)}
def vec(body, field):
    return tuple(float(v) for v in re.search(re.escape(field)+r': \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', body).groups())
def scalar(body, field):
    return float(re.search(r'^  ' + field + r': (.+)$', body, re.M)[1])
def owner(body):
    return int(re.search(r'm_GameObject: \{fileID: (\d+)\}', body)[1])
text = (ROOT / 'Assets/Rebuild/Scenes/Level2GridBattle.unity').read_text()
b = blocks(text)
assert all(0 < id < 2**63 for id in b), 'Unity Scene fileID outside signed Int64 range'
transforms = {owner(body): body for kind, body in b.values() if kind == 4}
cells, refs, towers = {}, {}, []
for id, (kind, body) in b.items():
    if 'Team5.Rebuild.World.BattleCell' in body:
        p = vec(transforms[owner(body)], 'm_LocalPosition')
        p = (int(p[0]), int(round(p[1] / .5)), int(p[2]))
        assert p not in cells
        cells[p] = int(scalar(body, 'kind'))
        refs[id] = p
    if 'Team5.Rebuild.World.BattleTower' in body:
        towers.append(dict(position=vec(transforms[owner(body)], 'm_LocalPosition'),
            range=scalar(body, 'range'), width=scalar(body, 'attackWidth'),
            area=int(scalar(body, 'attackArea')), hp=scalar(body, 'maxHp'),
            damage=scalar(body, 'damage'), interval=scalar(body, 'attackInterval')))
assert len(cells) == 20 * 14 and len(towers) == 2
assert {p[0] for p in cells} == set(range(9,29)) and {p[2] for p in cells} == set(range(6,20))
route_body = next(body for _, body in b.values() if 'Team5.Rebuild.World.SceneRoute' in body)
route = [refs[int(id)] for id in re.findall(r'^  - \{fileID: (\d+)\}', route_body, re.M)]
assert len(route) == 24 and cells[route[0]] == 4 and cells[route[-1]] == 5
assert route[0] == (10,0,16) and route[-1] == (27,0,10)
for a, c in zip(route, route[1:]):
    assert a[1] == c[1] and abs(a[0]-c[0])+abs(a[2]-c[2]) == 1
    for dx in (-1, 0, 1):
        for dz in (-1, 0, 1):
            for x in range(min(a[0], c[0])+dx, max(a[0], c[0])+dx+1):
                for z in range(min(a[2], c[2])+dz, max(a[2], c[2])+dz+1):
                    assert cells[(x, 0, z)] in (1, 4, 5)
occupied = set()
for tower in towers:
    x, y, z = tower['position']
    for dx in (-1, 0, 1):
        for dz in (-1, 0, 1):
            key = (int(x)+dx, 0, int(z)+dz)
            assert key not in occupied and cells[key] == 3
            occupied.add(key)
assert 'LevelOneTutorial' not in text and '884410001' not in text
assert '  displayedLevelNumber: 2' in text and '  useSceneRosterOnly: 0' in text
assert '  levelTimeMultiplier: 1' in text
backup = blocks((ROOT / 'Logs/CompactLevel2Backup/Level2GridBattle.unity').read_text())
moved_go = {owner(body) for kind,body in b.values() if 'Team5.Rebuild.World.BattleTower' in body}
moved_go.update(id for id,(kind,body) in b.items() if kind == 1 and any('m_Name: '+name+'\n' in body for name in ('GameplayGrid','Decorative Border Tiles')))
assert all(body == backup[id][1] for id, (kind, body) in b.items() if kind in (4,20) and id in backup and owner(body) not in moved_go), 'Saved camera or unrelated geometry changed'
assert all(body == backup[id][1] for id,(kind,body) in b.items() if id in backup and kind == 114 and 'Team5.Rebuild.World.SceneRoute' not in body), 'Combat, UI or Scene configuration changed'
border = [body for kind, body in b.values() if kind == 4 and 'm_Father: {fileID: 347706729}' in body]
assert len(border) == 440
assert all('m_LocalScale: {x: 1, y: 0.5, z: 1}' in body for body in border)
border_gos = {owner(body) for body in border}
assert all(kind in (4,23,33) for kind,body in b.values() if re.search(r'm_GameObject: \{fileID: (\d+)\}',body) and owner(body) in border_gos)
for kind,body in b.values():
    if 'Team5.Rebuild.World.BattleCell' not in body: continue
    cell_transform = transforms[owner(body)]
    assert 'm_LocalScale: {x: 1, y: 1, z: 1}' in cell_transform
    child = int(re.search(r'^  - \{fileID: (\d+)\}',cell_transform,re.M)[1])
    assert 'm_LocalScale: {x: 1, y: 0.5, z: 1}' in b[child][1]
settlement = (ROOT / 'Assets/Rebuild/Scenes/LevelSettlement.unity').read_text()
sb = blocks(settlement)
assert 'ContinueToUpgrade' in settlement and 'OffenseData' not in settlement
assert 'm_text: +10 GOLD' in settlement and 'm_text: +1 TANK UNIT' in settlement
for field in ('missionHeading', 'rewardHeading', 'rewardAmount', 'rewardDescription'):
    id = int(re.search(r'  '+field+r': \{fileID: (\d+)\}', settlement)[1])
    assert 'm_text:' in sb[id][1]
guids = {'0000000000000000e000000000000000', '0000000000000000f000000000000000'}
needed = set(re.findall(r'guid: (\w+)', text + settlement))
matches = subprocess.check_output(['rg', '--no-ignore', '--hidden', '--no-filename',
    '-g', '*.meta', '^guid: (' + '|'.join(needed - guids) + ')\\r?$',
    'Assets', 'Library/PackageCache'], cwd=ROOT, text=True)
guids.update(re.findall(r'^guid: (\w+)', matches, re.M))
assert needed <= guids, ('Unresolved asset GUID', needed - guids)
for scene_text in (text, settlement):
    ids = blocks(scene_text)
    local = re.findall(r'\{fileID: (-?\d+)\}', scene_text)
    assert all(int(id) == 0 or int(id) in ids for id in local), 'Unresolved local Scene reference'
out = ROOT / 'Logs/Level2Migration'
out.mkdir(exist_ok=True)
(out / 'ActualScene.json').write_text(json.dumps(dict(route=[(x,.25,z) for x,y,z in route],
    walkable=[p for p,k in cells.items() if k in (1,4,5)], towers=towers)))
print('PASS Level2: 20x14 unit-size cells, 24 route points, nine-slot swept clearance, two 3x3 towers and five border rings; camera/UI/combat settings unchanged.')
print('PASS settlement: reward labels, Continue callback, local fileIDs and all asset GUIDs resolve; no legacy data.')
