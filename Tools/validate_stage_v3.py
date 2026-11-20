"""Offline catalog topology checks. Run from the project root (Python 3.9+). No Unity install needed.
Checks exact StageV3StageCatalog.json shipped with this patch. DOES NOT simulate Unity combat.
"""
from pathlib import Path
import json
from collections import deque

ROOT = Path(__file__).resolve().parent.parent
CATALOG = ROOT / 'Assets' / 'Rebuild' / 'Editor' / 'StageV3StageCatalog.json'
ALLOWED = '.RSG!e'


def expand_route(points):
    corners = list(zip(points[::2], points[1::2]))
    for i in range(1, len(corners)):
        a, b = corners[i - 1], corners[i]
        assert a[0] == b[0] or a[1] == b[1], (a, b)
        dx = (b[0] > a[0]) - (b[0] < a[0]); dz = (b[1] > a[1]) - (b[1] < a[1])
        for t in range(abs(a[0] - b[0]) + abs(a[1] - b[1]) + 1):
            yield a[0] + dx * t, a[1] + dz * t


def build(stage):
    w, h = stage['width'], stage['height']
    cells = [['#' if x == 0 or z == 0 or x == w-1 or z == h-1 or stage['corridor']
              else '.' for x in range(w)] for z in range(h)]
    anchors = list(expand_route(stage['route']))
    if stage['corridor']:
        for x, z in anchors:
            for dx in range(-2, 3):
                for dz in range(-2, 3):
                    if 0 < x+dx < w-1 and 0 < z+dz < h-1:
                        cells[z+dz][x+dx] = '.'
    bounds = stage['cover']
    for i in range(0, len(bounds), 4):
        x0, z0, x1, z1 = bounds[i:i+4]
        for z in range(z0, z1+1):
            for x in range(x0, x1+1):
                assert 0 < x < w-1 and 0 < z < h-1
                cells[z][x] = '#'
    for x, z in anchors:
        for dx in (-1, 0, 1):
            for dz in (-1, 0, 1):
                xx, zz = x+dx, z+dz
                assert 0 <= xx < w and 0 <= zz < h and cells[zz][xx] != '#', (stage['stage'], (xx, zz))
                cells[zz][xx] = 'R'
    for x,z,char in [(*anchors[0], 'S'), (*anchors[-1], 'G')]:
        for dx in (-1, 0, 1):
            for dz in (-1, 0, 1):
                cells[z+dz][x+dx] = char
    for tower in stage['towers']:
        x,z=tower['x'],tower['z']
        assert cells[z][x] in '#.', (stage['stage'],'tower blocks route',(x,z))
        cells[z][x]='T'
    for hazard in stage['hazards']:
        x,z=hazard['x'],hazard['z']
        assert cells[z][x] in ALLOWED, (stage['stage'],'hazard blocked',(x,z))
        cells[z][x]='e' if hazard['kind']=='Sentry' else '!'
    return cells, anchors


def full_squad_safe(grid, pos):
    x,z=pos
    return all(0 <= z+dz < len(grid) and 0 <= x+dx < len(grid[0]) and
               grid[z+dz][x+dx] in ALLOWED for dz in (-1,0,1) for dx in (-1,0,1))


def shortest_route(grid, start, end):
    frontier=deque([(start,0)]); seen={start}; goal_steps=None
    while frontier:
        (x,z), dist=frontier.popleft()
        if (x,z)==end: goal_steps=dist
        for dx,dz in [(1,0),(-1,0),(0,1),(0,-1)]:
            nxt=(x+dx,z+dz)
            if nxt not in seen and full_squad_safe(grid,nxt):
                seen.add(nxt);frontier.append((nxt,dist+1))
    return goal_steps,seen


def valid_attack(t, dx, dz):
    shape=t['shape'];r=t['range']
    if shape=='Circle':return dx*dx+dz*dz<=r*r
    dirs={'PositiveX':(1,0),'NegativeX':(-1,0),'PositiveZ':(0,1),'NegativeZ':(0,-1),
          'ConePositiveX':(1,0),'ConeNegativeX':(-1,0),'ConePositiveZ':(0,1),'ConeNegativeZ':(0,-1)}
    fx,fz=dirs[shape];forward=dx*fx+dz*fz;side=abs(dx*fz-dz*fx)
    if forward<0 or forward>r:return False
    if shape.startswith('Cone'): return dx*dx+dz*dz<=r*r and side <= forward*.7813
    return side<=.30


def validate():
    catalog=json.loads(CATALOG.read_text())['stages'];assert len(catalog)==7
    report=[]
    for stage in catalog:
        n=stage['stage'];grid,anchors=build(stage)
        steps,safe=shortest_route(grid,anchors[0],anchors[-1]);assert steps is not None,(n,'unreachable')
        assert steps<=42,(n,'too long',steps)
        safe_mid={z for x,z in safe if x==stage['width']//2}
        if n>=3:assert max(safe_mid)-min(safe_mid)>=5,(n,'no alternate side route')
        for tower in stage['towers']:
            shape=tower['shape']
            assert shape!='Square'
            if n<6:assert shape not in ('PositiveX','NegativeX','PositiveZ','NegativeZ')
            assert sum(valid_attack(tower,x-tower['x'],z-tower['z']) for x,z in safe)>=2,(n,'tower cannot reach any route',tower)
        report.append(f"Stage {n}: {stage['name']} | safe shortest={steps} cells | safe centers={len(safe)} | "
                      f"towers={len(stage['towers'])} hazards={len(stage['hazards'])} | "
                      f"approach variation={max(safe_mid)-min(safe_mid) if safe_mid else 0}")
    return report,catalog


if __name__=='__main__':
    report,catalog=validate()
    print('\n'.join(report))
    print('PASS: offline topology / 3x3 formation / hazard / tower coverage constraints')
    for stage in catalog:
        grid,_=build(stage)
        print('\nLEVEL '+str(stage['stage'])+' — '+stage['name'])
        print('\n'.join(''.join(row) for row in reversed(grid)))
