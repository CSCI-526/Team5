"""Author Level3 once from read-only Team5's actual third battle; runtime uses the Scene only."""
from pathlib import Path
import re, math, json, uuid, hashlib

ROOT = Path(__file__).resolve().parents[2]
assert ROOT.name == 'Team5-Rebuild-Clean'
OLD = ROOT.parent/'Team5'
source = ROOT/'Assets/Rebuild/Scenes/Level2GridBattle.unity'
dest = ROOT/'Assets/Rebuild/Scenes/Level3GridBattle.unity'
assert not dest.exists(), 'Never overwrite an existing authored Level3'
protected = [source, ROOT/'Assets/Rebuild/Scenes/Level1GridBattle.unity',
    OLD/'Assets/Defense/Scenes/DefenseLevel2.unity', OLD/'Assets/Defense/Data/Level2Stage.asset', OLD/'Assets/Defense/Data/DefaultTower.asset']
hashes = {str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
stage = protected[3].read_text(); tower_asset = protected[4].read_text()
assert 'displayedLevelNumber: 3' in protected[2].read_text()
old_points = [(float(x),float(y)) for x,y in re.findall(r'^  - \{x: ([^,]+), y: ([^}]+)\}',stage.split('lowerWaypoints:')[0],re.M)]
assert old_points == [(-8,3),(-2,3),(-2,-3),(3,-3),(3,3),(8,3)]
old_towers = [(float(x),float(y),int(d),float(r)) for x,y,d,r in re.findall(r'- position: \{x: ([^,]+), y: ([^}]+)\}.*?attackDirection: (\d+)\s+attackRange: ([\d.]+)',stage,re.S)]
assert len(old_towers)==10
text = source.read_text()
blocks = {int(m[2]):[int(m[1]),m[3]] for m in re.finditer(r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)',text)}
names,owners,transforms,children = {},{},{},{}
for id,(kind,body) in blocks.items():
    if kind==1: names[id]=re.search(r'^  m_Name: (.*)$',body,re.M)[1]
    m=re.search(r'm_GameObject: \{fileID: (\d+)\}',body)
    if m: owners.setdefault(int(m[1]),[]).append(id)
    if kind in (4,224):
        go=int(m[1]); transforms[go]=id
        parent=int(re.search(r'm_Father: \{fileID: (\d+)\}',body)[1]); children.setdefault(parent,[]).append(go)
def named(name): return next(id for id,value in names.items() if value==name)
def subtree(go):
    result=[go]+owners.get(go,[])
    for child in children.get(transforms[go],[]): result+=subtree(child)
    return result
def position(body,p): return re.sub(r'm_LocalPosition: \{[^}]+\}','m_LocalPosition: {x: %g, y: %g, z: %g}'%tuple(p),body)
def set_children(body,refs):
    return re.sub(r'  m_Children:(?: \[\]\n|\n(?:  - \{fileID: \d+\}\n)*)',
        '  m_Children:\n'+''.join('  - {fileID: %d}\n'%id for id in refs),body)
grid,border,towers_root=[named(n) for n in ('GameplayGrid','Decorative Border Tiles','Scene Towers (3x3 Footprints)')]
ground=next(go for go in children[transforms[grid]] if any(re.search(r'^  kind: 0$',blocks[id][1],re.M) for id in owners[go]))
templates={label:(go,{id:blocks[id][:] for id in subtree(go)}) for label,go in
    [('cell',ground),('border',children[transforms[border]][0]),('tower',children[transforms[towers_root]][0])]}
next_id=max(id for id,(kind,_) in blocks.items() if kind!=1660057539)+1
for root in (grid,border,towers_root):
    for go in children[transforms[root]]:
        for id in subtree(go): del blocks[id]
def clone(label,parent,name,p):
    global next_id
    go,template=templates[label]; mapping={id:next_id+i for i,id in enumerate(template)}; next_id+=len(mapping)
    for id,(kind,body) in template.items():
        body=re.sub(r'fileID: (\d+)',lambda m:'fileID: '+str(mapping.get(int(m[1]),int(m[1]))),body)
        if id==go: body=re.sub(r'^  m_Name: .*$','  m_Name: '+name,body,flags=re.M)
        if id==transforms[go]:
            body=position(body,p); body=re.sub(r'm_Father: \{fileID: \d+\}','m_Father: {fileID: %d}'%parent,body)
        blocks[mapping[id]]=[kind,body]
    return mapping[transforms[go]],list(mapping.values())

min_x,min_z,width,depth=3,6,30,18
corners=[(7,19),(16,19),(16,10),(25,10),(25,19),(31,19)]
route=[corners[0]]
for end in corners[1:]:
    while route[-1]!=end:
        x,z=route[-1]; route.append((x+(end[0]>x)-(end[0]<x),z+(end[1]>z)-(end[1]<z)))
road={(x+dx,z+dz) for x,z in route for dx in (-1,0,1) for dz in (-1,0,1)}
spawn={(7+dx,19+dz) for dx in (-1,0,1) for dz in (-1,0,1)}
goal={(31+dx,19+dz) for dx in (-1,0,1) for dz in (-1,0,1)}
# Separate the old overlapping towers into legal complete Macro Cells, keeping fire groups/directions.
positions=[(19,19),(10,22),(16,7),(16,22),(28,10),(31,10),(25,22),(25,7),(28,16),(28,22)]
platform=set()
for x,z in positions:
    footprint={(x+dx,z+dz) for dx in (-1,0,1) for dz in (-1,0,1)}
    assert x%3==z%3==1 and not footprint&(road|platform)
    platform|=footprint
assert all(min_x<=x<min_x+width and min_z<=z<min_z+depth for x,z in road|platform)
materials={name:re.search(r'^guid: (\w+)',(ROOT/('Assets/Rebuild/Materials/Minimal/'+name+'.mat.meta')).read_text(),re.M)[1]
    for name in ('Ground','Road','Spawn','Goal','TowerPlatform')}
cell_ids,grid_refs,border_refs,tower_refs={},[],[],[]
for z in range(min_z,min_z+depth):
    for x in range(min_x,min_x+width):
        kind,mat=(4,'Spawn') if (x,z) in spawn else (5,'Goal') if (x,z) in goal else (1,'Road') if (x,z) in road else (3,'TowerPlatform') if (x,z) in platform else (0,'Ground')
        t,ids=clone('cell',transforms[grid],f'Small Cell ({x},0,{z}) {mat}',(x,0,z)); grid_refs.append(t)
        for id in ids:
            if 'Team5.Rebuild.World.BattleCell' in blocks[id][1]:
                blocks[id][1]=re.sub(r'^  kind: \d+',f'  kind: {kind}',blocks[id][1],flags=re.M); cell_ids[x,z]=id
            if blocks[id][0]==23:
                blocks[id][1]=re.sub(r'(m_Materials:\n  - \{fileID: 2100000, guid: )\w+',lambda m:m[1]+materials[mat],blocks[id][1])
for z in range(min_z-5,min_z+depth+5):
    for x in range(min_x-5,min_x+width+5):
        if min_x<=x<min_x+width and min_z<=z<min_z+depth: continue
        t,_=clone('border',transforms[border],f'Border Tile ({x},{z})',(x,0,z)); border_refs.append(t)
def scalar(body,field): return float(re.search(r'^  '+field+r': (.+)$',body,re.M)[1])
direction_map={0:1,1:2,2:3,3:4,4:0}
tower_data=[]
for index,((x,z),(_,_,direction,attack_range)) in enumerate(zip(positions,old_towers),1):
    t,ids=clone('tower',transforms[towers_root],f'Level 3 Tower {index} (3x3)',(x,.25,z)); tower_refs.append(t)
    id=next(id for id in ids if 'Team5.Rebuild.World.BattleTower' in blocks[id][1])
    stats=dict(range=attack_range*1.5,damage=scalar(tower_asset,'damage'),attackInterval=1/scalar(tower_asset,'fireRate'),
        targetRule=int(scalar(tower_asset,'targetingMode')),usesMacroFootprint=1,maxHp=scalar(tower_asset,'maxHp'),attackArea=direction_map[direction],attackWidth=.9)
    for key,value in stats.items(): blocks[id][1]=re.sub(r'^  '+key+r': .*$',f'  {key}: {value:g}',blocks[id][1],flags=re.M)
    tower_data.append(dict(position=[x,.25,z],range=stats['range'],area=stats['attackArea'],width=.9,hp=stats['maxHp'],damage=stats['damage'],interval=stats['attackInterval']))
for go,refs in ((grid,grid_refs),(border,border_refs),(towers_root,tower_refs)): blocks[transforms[go]][1]=set_children(blocks[transforms[go]][1],refs)
for id,(_,body) in list(blocks.items()):
    if 'Team5.Rebuild.World.SceneRoute' in body:
        body=re.sub(r'  cells:\n(?:  - \{fileID: \d+\}\n)+','  cells:\n'+''.join('  - {fileID: %d}\n'%cell_ids[p] for p in route),body)
    if 'Team5.Rebuild.Session.BattleSession' in body:
        body=re.sub(r'  towers:\n(?:  - \{fileID: \d+\}\n)+','  towers:\n'+''.join('  - {fileID: %d}\n'%id for id,(kind,b) in blocks.items() if 'Team5.Rebuild.World.BattleTower' in b),body)
        body=re.sub(r'^  displayedLevelNumber: .*$', '  displayedLevelNumber: 3',body,flags=re.M)
    blocks[id][1]=body.replace('Level 2','Level 3').replace('LEVEL 2','LEVEL 3')

# Frame this NEW Scene only. Reuse the authored projection/orientation and fit all terrain/borders.
camera_id=next(id for id,(kind,_) in blocks.items() if kind==20)
camera=blocks[camera_id][1]; camera_go=int(re.search(r'm_GameObject: \{fileID: (\d+)\}',camera)[1]); camera_transform=transforms[camera_go]
q=[float(v) for v in re.search(r'm_LocalRotation: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}',blocks[camera_transform][1]).groups()]
def rotate(v):
    x,y,z,w=q; tx,ty,tz=2*(y*v[2]-z*v[1]),2*(z*v[0]-x*v[2]),2*(x*v[1]-y*v[0])
    return v[0]+w*tx+y*tz-z*ty,v[1]+w*ty+z*tx-x*tz,v[2]+w*tz+x*ty-y*tx
right,up,forward=rotate((1,0,0)),rotate((0,1,0)),rotate((0,0,1))
dot=lambda a,b:sum(x*y for x,y in zip(a,b))
extents=((width+10)/2,2,(depth+10)/2); center=(min_x+(width-1)/2,.5,min_z+(depth-1)/2)
offsets=[(extents[0]*x,extents[1]*y,extents[2]*z) for x in (-1,1) for y in (-1,1) for z in (-1,1)]
tan=math.tan(math.radians(scalar(camera,'field of view')/2))
distance=max(max(abs(dot(v,right))*1.08/(tan*16/9)-dot(v,forward),abs(dot(v,up))*1.08/tan-dot(v,forward)) for v in offsets)+2
ortho=max(max(abs(dot(v,up))*1.08,abs(dot(v,right))*1.08/(16/9)) for v in offsets)
blocks[camera_transform][1]=position(blocks[camera_transform][1],[center[i]-forward[i]*distance for i in range(3)])
camera=re.sub(r'^  orthographic size: .*$',f'  orthographic size: {ortho:g}',camera,flags=re.M)
blocks[camera_id][1]=re.sub(r'^  far clip plane: .*$',f'  far clip plane: {distance+100:g}',camera,flags=re.M)
output='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+''.join('--- !u!%d &%d\n%s'%(kind,id,body) for id,(kind,body) in sorted(blocks.items(),key=lambda item:item[1][0]==1660057539))
assert next_id<2**63 and len(cell_ids)==540 and len(route)==43 and len(border_refs)==580
assert all(int(id)==0 or int(id) in blocks for id in re.findall(r'\{fileID: (-?\d+)\}',output))
dest.write_text(output); guid=uuid.uuid4().hex; Path(str(dest)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\n')
settings=ROOT/'ProjectSettings/EditorBuildSettings.asset'
settings.write_text(settings.read_text().replace('  m_configObjects:',f'  - enabled: 1\n    path: Assets/Rebuild/Scenes/{dest.name}\n    guid: {guid}\n  m_configObjects:'))
for p,digest in hashes.items(): assert hashlib.sha256(Path(p).read_bytes()).hexdigest()==digest,('Protected source changed',p)
out=ROOT/'Logs/Level3Migration'; out.mkdir(exist_ok=True)
(out/'ActualScene.json').write_text(json.dumps(dict(route=[(x,.25,z) for x,z in route],walkable=[(x,0,z) for x,z in sorted(road)],towers=tower_data),indent=2))
(out/'Import.json').write_text(json.dumps(dict(sourceHashes=hashes,grid=[width,depth],min=[min_x,min_z],oldPoints=old_points,oldTowers=old_towers,towers=tower_data,guid=guid),indent=2))
print('PASS Level3 authored: 30x18 cells, 43 ordered route references, 10 non-overlapping 3x3 towers, 580 decorative tiles. Source projects and existing scenes unchanged.')
