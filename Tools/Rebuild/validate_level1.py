"""Read-only checks of the saved Level 1 Scene; exports its actual geometry for .NET rules checks."""
from pathlib import Path
import hashlib
import json
import math
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
scene = ROOT / 'Assets/Rebuild/Scenes/Level1GridBattle.unity'
text = scene.read_text()
blocks = {int(m[2]): (int(m[1]), m[3]) for m in re.finditer(r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)', text)}
assert len(blocks) == len(re.findall(r'^--- !u!', text, re.M))
def scalar(body, field):
    return float(re.search(r'^  ' + re.escape(field) + r': (.+)$', body, re.M)[1])
def vec(body, field):
    return tuple(float(v) for v in re.search(re.escape(field)+r': \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', body).groups())
def owner(body):
    return int(re.search(r'm_GameObject: \{fileID: (\d+)\}', body)[1])
transforms = {owner(body): body for kind, body in blocks.values() if kind == 4}
transform_owners = {key: owner(body) for key, (kind, body) in blocks.items() if kind == 4}
cells, component_cells, towers = {}, {}, []
for key, (kind, body) in blocks.items():
    if 'Team5.Rebuild.World.BattleCell' in body:
        transform = transforms[owner(body)]
        point = vec(transform, 'm_LocalPosition')
        key3 = (int(point[0]), int(round(point[1] / 0.5)), int(point[2]))
        assert key3 not in cells, ('Grid conflict', key3)
        assert vec(transform, 'm_LocalScale') == (1, 1, 1)
        assert point[1] == 0
        children = re.findall(r'^  - \{fileID: (\d+)\}', transform, re.M)
        mesh = transforms[transform_owners[int(children[0])]]
        assert vec(mesh, 'm_LocalScale') == (1, 0.5, 1)
        cells[key3] = int(scalar(body, 'kind'))
        component_cells[key] = key3
    if 'Team5.Rebuild.World.BattleTower' in body:
        towers.append({'position': vec(transforms[owner(body)], 'm_LocalPosition'),
            'range': scalar(body, 'range'), 'width': scalar(body, 'attackWidth'), 'area': int(scalar(body, 'attackArea')),
            'hp': scalar(body, 'maxHp'), 'damage': scalar(body, 'damage'), 'interval': scalar(body, 'attackInterval')})
assert len(cells) == 20 * 14
assert {p[0] for p in cells} == set(range(9,29)) and {p[2] for p in cells} == set(range(6,20))
route_body = next(body for kind, body in blocks.values() if 'Team5.Rebuild.World.SceneRoute' in body)
route_keys = [component_cells[int(ref)] for ref in re.findall(r'^  - \{fileID: (\d+)\}', route_body, re.M)]
assert len(route_keys) == len(set(route_keys)) == 19
assert route_keys[0] == (10, 0, 16) and route_keys[-1] == (19, 0, 7)
assert cells[route_keys[0]] == 4 and cells[route_keys[-1]] == 5
for a, b in zip(route_keys, route_keys[1:]):
    assert a[1] == b[1] and abs(a[0]-b[0])+abs(a[2]-b[2]) == 1
    for dx in (-1, 0, 1):
        for dz in (-1, 0, 1):
            for x in range(min(a[0], b[0])+dx, max(a[0], b[0])+dx+1):
                for z in range(min(a[2], b[2])+dz, max(a[2], b[2])+dz+1):
                    assert cells[(x, 0, z)] in (1, 4, 5), ('Blocked slot trajectory', x, z)
occupied = set()
assert len(towers) == 1
for tower in towers:
    x, y, z = tower['position']
    assert x % 3 == z % 3 == 1 and y == 0.25
    for dx in (-1, 0, 1):
        for dz in (-1, 0, 1):
            point = (x+dx, 0, z+dz)
            assert point not in occupied and cells[point] == 3
            occupied.add(point)
assert [(t['area'], t['range'], t['width']) for t in towers] == [(2, 9, 0.9)]
assert towers[0]['position'] == (22,0.25,16)
for match in re.finditer(r'\{fileID: (-?\d+)\}', text):
    ref = int(match[1])
    assert ref == 0 or ref in blocks, ('Dangling local reference', ref)
project_guids = {re.search(r'^guid: (\w+)', p.read_text(encoding='utf-8-sig'), re.M)[1]
                 for p in (ROOT / 'Assets').rglob('*.meta') if re.search(r'^guid: (\w+)', p.read_text(encoding='utf-8-sig'), re.M)}
missing = set(re.findall(r'guid: (\w+)', text)) - project_guids
package_metas = subprocess.check_output(['rg', '--files', '--no-ignore', 'Library/PackageCache', '-g', '*.cs.meta'], cwd=ROOT, text=True).splitlines()
ui_types = {'CanvasScaler.cs.meta', 'GraphicRaycaster.cs.meta', 'Slider.cs.meta', 'Image.cs.meta', 'Button.cs.meta',
            'TextMeshProUGUI.cs.meta', 'UniversalAdditionalCameraData.cs.meta', 'UniversalAdditionalLightData.cs.meta', 'InputSystemUIInputModule.cs.meta'}
for relative in package_metas:
    p = ROOT / relative
    if p.name not in ui_types:
        continue
    for guid in re.findall(r'^guid: (\w+)', p.read_text(encoding='utf-8-sig'), re.M):
        if guid in missing:
            project_guids.add(guid)
project_guids.update(('0000000000000000e000000000000000', '0000000000000000f000000000000000'))
assert set(re.findall(r'guid: (\w+)', text)) <= project_guids, 'Unresolved scene asset GUID'
assert 'ReverseTD' not in text and 'TowerSlot' not in text and 'StageLayout' not in text
report_dir = ROOT / 'Logs/Level1Migration'
original_report = json.loads((report_dir / 'Level1.json').read_text())
for path, expected in original_report['sourceHashes'].items():
    assert hashlib.sha256(Path(path).read_bytes()).hexdigest() == expected, ('Protected source changed', path)
session = next(body for kind, body in blocks.values() if 'Team5.Rebuild.Session.BattleSession' in body)
roster = re.search(r'initialRoster: (.+)', session)[1]
assert 'c7fea7eb60294bfa83cb342d4b00af06' in roster and scalar(session, 'useSceneRosterOnly') == 1
assert scalar(session, 'levelTimeMultiplier') == 2
roster_text = (ROOT / 'Assets/Rebuild/Configuration/Level1Captain.asset').read_text()
members = [{'id': int(member), 'slot': int(slot), 'role': int(role), 'spawnTime': float(at)}
           for member, slot, role, at in re.findall(r'memberId: (\d+)\s+slotId: (\d+)\s+role: (\d+)\s+spawnTime: ([\d.]+)', roster_text)]
assert members == [{'id': 110, 'slot': 4, 'role': 4, 'spawnTime': 0.0}]
assert sum('m_EditorClassIdentifier: Team5.Rebuild.Presentation::Team5.Rebuild.Presentation.LegacyUI.LegacyBattleHud' in body for _, body in blocks.values()) == 1
assert sum('m_EditorClassIdentifier: Team5.Rebuild.Presentation::Team5.Rebuild.Presentation.LegacyUI.LevelOneTutorial' in body for _, body in blocks.values()) == 1
settings = (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text()
assert re.search(r'path: (.+)', settings)[1] == 'Assets/Rebuild/Scenes/StartGame.unity'
assert 'Assets/Rebuild/Scenes/Level1GridBattle.unity' in settings
assert 'Assets/Rebuild/Scenes/Level1GridBattle.unity' in (ROOT / 'Assets/Rebuild/Scripts/Session/BattleSceneFlow.cs').read_text()
camera = next(body for kind, body in blocks.values() if kind == 20)
camera_transform = transforms[owner(camera)]
camera_position = vec(camera_transform, 'm_LocalPosition')
qx, qy, qz, qw = (float(v) for v in re.search(r'm_LocalRotation: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}', camera_transform).groups())
def camera_space(v):
    # Inverse camera rotation, without an external math dependency.
    x, y, z, w = -qx, -qy, -qz, qw
    tx, ty, tz = 2*(y*v[2]-z*v[1]), 2*(z*v[0]-x*v[2]), 2*(x*v[1]-y*v[0])
    return (v[0]+w*tx+y*tz-z*ty, v[1]+w*ty+z*tx-x*tz, v[2]+w*tz+x*ty-y*tx)
tan = math.tan(math.radians(scalar(camera, 'field of view') / 2))
for x in (5.5, 31.5):
    for y in (-0.25, 1.5):
        for z in (2.5, 22.5):
            cx, cy, cz = camera_space((x-camera_position[0], y-camera_position[1], z-camera_position[2]))
            assert scalar(camera, 'near clip plane') < cz < scalar(camera, 'far clip plane')
            if scalar(camera, 'orthographic'):
                limit = scalar(camera, 'orthographic size')
            else:
                limit = cz*tan
            assert abs(cx) < limit*16/9 and abs(cy) < limit, 'Map outside 16:9 camera'
actual = {'route': [(x, 0.25, z) for x, _, z in route_keys],
          'walkable': [p for p, kind in cells.items() if kind in (1, 4, 5)], 'towers': towers, 'members': members}
(report_dir / 'ActualScene.json').write_text(json.dumps(actual))
print('PASS saved scene: 280 unit-size cells, 19 real-cell route references, nine swept slots, 9 legal tower support cells, first tower only, former second tower is Goal.')
print('PASS all local/asset references, current HUD/roster, StartGame entry, Level1 flow, no legacy runtime dependency, original project/prototype hashes unchanged.')
print('PASS new camera frames map, towers and decorative border at 16:9; prototype camera unchanged.')
flow_text = (ROOT / 'Assets/Rebuild/Scripts/Session/BattleSceneFlow.cs').read_text()
begin_run = flow_text.split('public static void BeginRun()')[1].split('public static SquadPreparation')[0]
assert 'flow.QueueScene(BattleScene)' in begin_run and 'QueueScene(UpgradeScene)' not in begin_run
print('PASS Start Game skips Upgrade; Level1 fixed scene roster contains exactly Captain #110 in center slot 4.')
