"""Validate the saved Level3 Scene and export its real geometry for offline combat checks."""
from pathlib import Path
import re, json, hashlib

ROOT=Path(__file__).resolve().parents[2]
path=ROOT/'Assets/Rebuild/Scenes/Level3GridBattle.unity'
text=path.read_text()
b={int(m[2]):(int(m[1]),m[3]) for m in re.finditer(r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)',text)}
def owner(body):return int(re.search(r'm_GameObject: \{fileID: (\d+)\}',body)[1])
def vec(body):return tuple(float(v) for v in re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}',body).groups())
def scalar(body,key):return float(re.search(r'^  '+key+r': (.+)$',body,re.M)[1])
transforms={owner(body):body for kind,body in b.values() if kind==4}
cells,refs,towers={},{},[]
for id,(kind,body) in b.items():
    if 'Team5.Rebuild.World.BattleCell' in body:
        t=transforms[owner(body)];x,y,z=vec(t);key=(int(x),int(round(y/.5)),int(z))
        assert key not in cells and y==0
        assert 'm_LocalScale: {x: 1, y: 1, z: 1}' in t
        child=int(re.search(r'^  - \{fileID: (\d+)\}',t,re.M)[1]);assert 'm_LocalScale: {x: 1, y: 0.5, z: 1}' in b[child][1]
        cells[key]=int(scalar(body,'kind'));refs[id]=key
    if 'Team5.Rebuild.World.BattleTower' in body:
        towers.append(dict(position=vec(transforms[owner(body)]),range=scalar(body,'range'),area=int(scalar(body,'attackArea')),
            width=scalar(body,'attackWidth'),hp=scalar(body,'maxHp'),damage=scalar(body,'damage'),interval=scalar(body,'attackInterval')))
assert len(cells)==540 and len(towers)==10
assert {p[0] for p in cells}==set(range(3,33)) and {p[2] for p in cells}==set(range(6,24))
route_body=next(body for _,body in b.values() if 'Team5.Rebuild.World.SceneRoute' in body)
route=[refs[int(id)] for id in re.findall(r'^  - \{fileID: (\d+)\}',route_body,re.M)]
assert len(route)==len(set(route))==43 and route[0]==(7,0,19) and route[-1]==(31,0,19)
assert cells[route[0]]==4 and cells[route[-1]]==5
assert list(cells.values()).count(4)==list(cells.values()).count(5)==9
assert scalar(route_body,'requiredWidth')==3
for a,c in zip(route,route[1:]):
    assert a[1]==c[1] and abs(a[0]-c[0])+abs(a[2]-c[2])==1
    for dx in (-1,0,1):
        for dz in (-1,0,1):
            for x in range(min(a[0],c[0])+dx,max(a[0],c[0])+dx+1):
                for z in range(min(a[2],c[2])+dz,max(a[2],c[2])+dz+1):assert cells[(x,0,z)] in (1,4,5)
occupied=set()
for t in towers:
    x,y,z=t['position'];assert x%3==z%3==1 and y==.25 and (t['hp'],t['damage'],t['interval'])==(20,1,1)
    for dx in (-1,0,1):
        for dz in (-1,0,1):
            key=(int(x)+dx,0,int(z)+dz);assert key not in occupied and cells[key]==3;occupied.add(key)
assert len(occupied)==90 and sum(t['area']==0 for t in towers)==4
border=[body for kind,body in b.values() if kind==4 and 'm_Father: {fileID: 347706729}' in body]
assert len(border)==580 and all('m_LocalScale: {x: 1, y: 0.5, z: 1}' in body for body in border)
assert all(0<id<2**63 for id in b)
assert all(int(id)==0 or int(id) in b for id in re.findall(r'\{fileID: (-?\d+)\}',text))
assert 'LevelOneTutorial' not in text and 'ReverseTD' not in text
session=next(body for _,body in b.values() if 'Team5.Rebuild.Session.BattleSession' in body)
assert scalar(session,'displayedLevelNumber')==3 and scalar(session,'attemptsPerLevel')==3 and scalar(session,'useSceneRosterOnly')==0
assert len(re.findall(r'^  - \{fileID: \d+\}',session.split('  towers:')[1].split('  unitCount:')[0],re.M))==10
report=json.loads((ROOT/'Logs/Level3Migration/Import.json').read_text())
assert json.loads(json.dumps(towers))==report['towers']
for name,digest in report['sourceHashes'].items():assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==digest,('Protected source changed',name)
source=(ROOT/'Assets/Rebuild/Scenes/Level2GridBattle.unity').read_text()
source_b={int(m[2]):(int(m[1]),m[3]) for m in re.finditer(r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)',source)}
assert all(body==source_b[id][1] for id,(kind,body) in b.items() if kind==224 and id in source_b),'Existing UI layout changed'
settings=(ROOT/'ProjectSettings/EditorBuildSettings.asset').read_text()
assert 'path: Assets/Rebuild/Scenes/Level3GridBattle.unity' in settings
(ROOT/'Logs/Level3Migration/ActualScene.json').write_text(json.dumps(dict(route=[(x,.25,z) for x,y,z in route],
    walkable=[p for p,kind in cells.items() if kind in (1,4,5)],towers=towers),indent=2))
print('PASS Level3: 30x18 cells, 43 route cells, nine-slot swept clearance, ten legal Macro footprints, four square ranges, five border rings, stable references, existing scenes/UI protected.')
