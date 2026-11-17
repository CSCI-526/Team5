from pathlib import Path
import re,json
R=Path.cwd();template=(R/'Tools/Rebuild/create_haon_unit_test.py').read_text(encoding='utf-8-sig').split("roster=out/'HaonTestSquad.asset'")[0]
configs=[('Tank3D','prf_Set Costume02 Yuko.prefab',(.5,.56,.65)),('Heal3D','prf_Set Costume02 Misaki.prefab',(.3,.78,.45)),('Captain3D','prf_Set Costume02 UTC.prefab',None)]
report=[]
for variant,character,hairColor in configs:
 out=R/'Assets/Rebuild/HaonTest'/variant
 source=(R/'Assets/Haons SD series Pack/Prefab/CharacterSet'/character).read_text(encoding='utf-8-sig')
 rootTransform=next(int(m[1]) for m in re.finditer(r'(?ms)^--- !u!4 &(\d+)\n.*?(?=^--- !u!|\Z)',source) if 'm_Father: {fileID: 0}' in m[0])
 rootAnimator=int(re.search(r'^--- !u!95 &(\d+)',source,re.M)[1])
 code=template.replace("out=R/'Assets/Rebuild/HaonTest'",f"out=R/'Assets/Rebuild/HaonTest/{variant}'").replace("rootprefab=vendor/'Prefab/CharacterSet/prf_Set Costume02 Misaki.prefab'",f"rootprefab=vendor/'Prefab/CharacterSet/{character}'").replace('rootTransform=5362025121098124061;rootAnimator=5362025121090413709',f'rootTransform={rootTransform};rootAnimator={rootAnimator}').replace('  maxHeight: 1.2\n','  maxHeight: 1.65\n').replace('  maxFootprint: 0.72\n','  maxFootprint: 0.99\n')
 exec(compile(code,f'<Haon {variant} generator>','exec'),{})
 prefab=out/'HaonUnitVisual.prefab';text=prefab.read_text().replace('m_Name: HaonUnitVisual\n',f'm_Name: Haon{variant}\n',1);prefab.write_text(text,encoding='utf-8')
 if hairColor:
  for mat in (out/'Materials').glob('*Hair*.mat'):
   text=mat.read_text();color=f'{{r: {hairColor[0]}, g: {hairColor[1]}, b: {hairColor[2]}, a: 1}}'
   text=re.sub(r'(_BaseColor: |(?<!Base)_Color: )\{[^}]+\}',lambda m:m[1]+color,text);mat.write_text(text,encoding='utf-8')
 if variant=='Tank3D':
  eye=out/'Materials/mat_Eye1_A4_URP.mat'
  eyeGuid=re.search(r'^guid: (\w+)',Path(str(eye)+'.meta').read_text(),re.M)[1]
  rightEye=out/'Parts/prf_Eye1_R Set.prefab'
  rightEye.write_text(rightEye.read_text().replace('ee80d417a088139489d5079c71242ad3',eyeGuid),encoding='utf-8')
 report.append({'variant':variant,'source_character':character,'prefab':str(prefab.relative_to(R)),'hair_tint':hairColor})
(R/'Logs/HaonUnitTest/RoleVariants.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))
