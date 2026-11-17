from pathlib import Path
import re,json,uuid,hashlib
R=Path.cwd(); vendor=R/'Assets/Haons SD series Pack'; out=R/'Assets/Rebuild/HaonTest'

def guid(path):return re.search(r'^guid: (\w+)',Path(str(path)+'.meta').read_text(encoding='utf-8-sig'),re.M)[1]
def meta(path,folder=False):
 m=Path(str(path)+'.meta')
 if not m.exists():m.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if folder else ''),encoding='utf-8')
 return guid(path)
def folder(path):
 path.mkdir(parents=True,exist_ok=True);meta(path,True)
for p in (out,out/'Parts',out/'Materials'):folder(p)
rootprefab=vendor/'Prefab/CharacterSet/prf_Set Costume02 Misaki.prefab'
look={guid(p.with_suffix('')):p.with_suffix('') for p in vendor.rglob('*.meta')}
prefabs=set();materials=set()
def collect(p):
 if p in prefabs:return
 prefabs.add(p)
 for g in set(re.findall(r'guid: (\w+)',p.read_text(encoding='utf-8-sig'))):
  dep=look.get(g)
  if dep and dep.suffix=='.prefab':collect(dep)
  elif dep and dep.suffix=='.mat':materials.add(dep)
collect(rootprefab)
newpaths={p:(out/'Parts'/p.name) for p in prefabs if p!=rootprefab}
newpaths[rootprefab]=out/'HaonUnitVisual.prefab'
for p in materials:newpaths[p]=out/'Materials'/(p.stem+'_URP.mat')
mapping={guid(p):meta(q) for p,q in newpaths.items()}
driver=R/'Assets/Rebuild/Scripts/Presentation/Minimal/HaonUnitAnimation.cs';driverGuid=meta(driver)
controller=out/'HaonIdleRun.controller';controllerGuid=meta(controller)
idle=vendor/'Animation/Common/StandA_idleA.FBX';run=vendor/'Animation/Common/RunA_Front.FBX'
controllerText='''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!91 &9100000
AnimatorController:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: HaonIdleRun
  serializedVersion: 5
  m_AnimatorParameters:
  - m_Name: IsMoving
    m_Type: 4
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}
  m_AnimatorLayers:
  - serializedVersion: 5
    m_Name: Base Layer
    m_StateMachine: {fileID: 11070000}
    m_Mask: {fileID: 0}
    m_Motions: []
    m_Behaviours: []
    m_BlendingMode: 0
    m_SyncedLayerIndex: -1
    m_DefaultWeight: 1
    m_IKPass: 0
    m_SyncedLayerAffectsTiming: 0
    m_Controller: {fileID: 9100000}
'''
for name,id,clip,transition in [('Idle',11020000,idle,11010000),('Run',11020001,run,11010001)]:
 controllerText+=f'''--- !u!1102 &{id}
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions:
  - {{fileID: {transition}}}
  m_StateMachineBehaviours: []
  m_Position: {{x: 50, y: 50, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {guid(clip)}, type: 3}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
'''
for id,dest,condition in [(11010000,11020001,1),(11010001,11020000,2)]:
 controllerText+=f'''--- !u!1101 &{id}
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: {condition}
    m_ConditionEvent: IsMoving
    m_EventTreshold: 0
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: {dest}}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.15
  m_TransitionOffset: 0
  m_ExitTime: 0
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 3
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
'''
controllerText+='''--- !u!1107 &11070000
AnimatorStateMachine:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Base Layer
  m_ChildStates:
  - serializedVersion: 1
    m_State: {fileID: 11020000}
    m_Position: {x: 240, y: 100, z: 0}
  - serializedVersion: 1
    m_State: {fileID: 11020001}
    m_Position: {x: 500, y: 100, z: 0}
  m_ChildStateMachines: []
  m_AnyStateTransitions: []
  m_EntryTransitions: []
  m_StateMachineTransitions: {}
  m_StateMachineBehaviours: []
  m_AnyStatePosition: {x: 40, y: 0, z: 0}
  m_EntryPosition: {x: 40, y: 100, z: 0}
  m_ExitPosition: {x: 700, y: 100, z: 0}
  m_ParentStateMachinePosition: {x: 800, y: 20, z: 0}
  m_DefaultState: {fileID: 11020000}
'''
controller.write_text(controllerText,encoding='utf-8')
# Independent URP/Lit copies. Toon uses _MainTex as its actual painted texture, not its placeholder _BaseMap.
template=(R/'Assets/Rebuild/Materials/SingleCellTower/Foundation.mat').read_text(encoding='utf-8-sig')
def material_property(p, pattern, fallback):
 text=p.read_text(encoding='utf-8-sig')
 match=re.search(pattern,text)
 if match:return match[1]
 parent=re.search(r'm_Parent: \{fileID: 2100000, guid: (\w+)',text)
 if parent and parent[1] in look:return material_property(look[parent[1]],pattern,fallback)
 return fallback
for p in materials:
 text=p.read_text(encoding='utf-8-sig');color=material_property(p,r'_BaseColor: (\{[^}]+\})','{r: 1, g: 1, b: 1, a: 1}')
 texture=material_property(p,r'- _MainTex:\n        m_Texture: (\{[^}]+\})','{fileID: 0}')
 new=template.replace('SingleCellTower_Foundation',p.stem+'_URP')
 new=re.sub(r'(_BaseColor: |(?<!Base)_Color: )\{[^}]+\}',lambda m:m[1]+color,new)
 for key in ('_BaseMap','_MainTex'):
  new=re.sub(r'(- '+key+r':\n        m_Texture: )\{[^}]+\}',lambda m:m[1]+texture,new)
 # All character surfaces use cutout for clean eyes/hair card edges. Invisible authoring helper stays invisible.
 new=new.replace('m_ValidKeywords: []','m_ValidKeywords:\n  - _ALPHATEST_ON').replace('_AlphaClip: 0','_AlphaClip: 1').replace('m_CustomRenderQueue: -1','m_CustomRenderQueue: 2450').replace('RenderType: Opaque','RenderType: TransparentCutout').replace('_Cull: 2','_Cull: 0')
 if 'UnVisible' in p.name:new=re.sub(r'(_BaseColor: |(?<!Base)_Color: )\{[^}]+\}',lambda m:m[1]+'{r: 1, g: 1, b: 1, a: 0}',new)
 newpaths[p].write_text(new,encoding='utf-8')
# Sanitized visual copies preserve all character meshes, bones and prefab references, but omit optional spring/demo behaviours.
removedIdsByGuid = {}
for sourcePrefab in prefabs:
    removedIdsByGuid[guid(sourcePrefab)] = {match[2] for block in re.split(r'(?m)(?=^--- !u!)',sourcePrefab.read_text(encoding='utf-8-sig'))
        if (match := re.match(r'--- !u!(\d+) &(-?\d+)',block)) and int(match[1]) in (114,54,65,135,136,143)}
removed={};rootTransform=5362025121098124061;rootAnimator=5362025121090413709
for p in prefabs:
 text=p.read_text(encoding='utf-8-sig');blocks=re.split(r'(?m)(?=^--- !u!)',text)
 remove=set()
 for block in blocks:
  match=re.match(r'--- !u!(\d+) &(-?\d+)',block)
  if match and int(match[1]) in (114,54,65,135,136,143):remove.add(match[2])
 removed[str(p.relative_to(R))]=len(remove)
 clean=[]
 for block in blocks:
  match=re.match(r'--- !u!(\d+) &(-?\d+)',block)
  if match and match[2] in remove:continue
  for id in remove:block=re.sub(r'^  - component: \{fileID: '+id+r'\}\n','',block,flags=re.M)
  if p==rootprefab and match and match[2]==str(rootTransform):
   block=block.replace('m_Father: {fileID: 0}','m_Father: {fileID: 90000001}')
  if p==rootprefab and match and int(match[1])==95:
   block=re.sub(r'm_Controller: \{[^}]+\}',f'm_Controller: {{fileID: 9100000, guid: {controllerGuid}, type: 2}}',block)
   block=block.replace('m_ApplyRootMotion: 1','m_ApplyRootMotion: 0').replace('m_CullingMode: 1','m_CullingMode: 0')
  # Remove nested overrides for optional components removed from the copied source prefab.
  block=re.sub(r'(?m)^    - target: \{fileID: (-?\d+), guid: (\w+), type: 3\}\n(?:      .*\n)*',
      lambda m: '' if m[1] in removedIdsByGuid.get(m[2],set()) else m[0], block)
  clean.append(block)
 text=''.join(clean)
 for old,new in mapping.items():text=text.replace(old,new)
 if p==rootprefab:
  prefix=f'''--- !u!1 &90000000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 90000001}}
  - component: {{fileID: 90000002}}
  - component: {{fileID: 90000003}}
  m_Layer: 0
  m_Name: HaonUnitVisual
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &90000001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 90000000}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {rootTransform}}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
'''
  for id,script,classname in [(90000002,'734b639726d800d42bd50b82a245e7ab','UnitView'),(90000003,driverGuid,'HaonUnitAnimation')]:
   prefix+=f'''--- !u!114 &{id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 90000000}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Team5.Rebuild.Presentation::Team5.Rebuild.Presentation.{classname}
'''
   if id==90000003:prefix+=f'''  modelRoot: {{fileID: {rootTransform}}}
  characterAnimator: {{fileID: {rootAnimator}}}
  maxHeight: 1.2
  maxFootprint: 0.72
  facingYawOffset: 0
'''
  text=text.replace('--- !u!',prefix+'--- !u!',1)
 newpaths[p].write_text(text,encoding='utf-8')
roster=out/'HaonTestSquad.asset';rosterGuid=meta(roster)
text=(R/'Assets/Rebuild/Configuration/PrototypeSquad.asset').read_text(encoding='utf-8-sig').replace('m_Name: PrototypeSquad','m_Name: HaonTestSquad')
text+='''  - enabled: 1
    memberId: 111
    slotId: 4
    role: 1
    spawnTime: 0
'''
roster.write_text(text,encoding='utf-8')
scene=R/'Assets/Rebuild/Scenes/HaonUnitTest.unity';meta(scene)
text=(R/'Assets/Rebuild/Scenes/LevelTemplate.unity').read_text(encoding='utf-8-sig')
text=text.replace('guid: 57e190ca22f2491db6c91c21a8dcb6a4','guid: '+rosterGuid).replace('useSceneRosterOnly: 0','useSceneRosterOnly: 1')
# Only the Presentation binding is extended. Camera, cells, routes and tower blocks are copied byte-for-byte.
text=text.replace('  showTowerRanges: 1','  showTowerRanges: 1\n  useTestUnitVisual: 1\n  testVisualMemberId: 111\n  testUnitPrefab: {fileID: 90000002, guid: '+guid(newpaths[rootprefab])+', type: 3}')
scene.write_text(text,encoding='utf-8')
report={'source_character':str(rootprefab.relative_to(R)),'prefabs_copied':len(prefabs),'materials_converted':len(materials),'optional_behaviours_removed':removed,'idle':{'path':str(idle.relative_to(R)),'name':'StandA_idleA','rig':'Humanoid'},'run':{'path':str(run.relative_to(R)),'name':'RunA_front@loop','rig':'Humanoid'},'test_member_id':111,'test_slot_id':4,'test_scene':str(scene.relative_to(R))}
(R/'Logs/HaonUnitTest/SetupReport.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
