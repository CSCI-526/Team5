"""Register the AI-repaired continuous leg art; no painting or inpainting here."""
from pathlib import Path
import json, shutil
import numpy as np
import cv2
from PIL import Image

folder=Path(__file__).resolve().parent
source=Path(r'C:/Users/haochen song/.codex/generated_images/01a1c46c-401d-79e0-9726-123dca31166c/exec-71f97dea-454f-4c28-98e0-f061835edfff.png')
shutil.copy2(source,folder/'GeneratedLegRepairAtlas.png')
a=np.array(Image.open(source).convert('RGBA'))
n,labels,stats,centroids=cv2.connectedComponentsWithStats((a[:,:,3]>64).astype('uint8'),8)
ids=sorted([i for i in range(1,n) if stats[i,4]>1000],key=lambda i:stats[i,0])
assert len(ids)==3
names=['F_LEG_CONTINUOUS_SCREEN_L','F_LEG_CONTINUOUS_SCREEN_R','F_SHORTS_CONTINUOUS']
rects=[(108,752,246,301),(387,752,250,301),(254,731,242,93)]
entries=[]
for idx,name,rect in zip(ids,names,rects):
 x,y,w,h,area=stats[idx];pad=4
 x0,y0,x1,y1=max(0,x-pad),max(0,y-pad),min(a.shape[1],x+w+pad),min(a.shape[0],y+h+pad)
 q=a[y0:y1,x0:x1].copy()
 mask=cv2.dilate((labels==idx).astype('uint8'),np.ones((7,7),np.uint8))
 q[:,:,3]*=mask[y0:y1,x0:x1];q[q[:,:,3]==0,:3]=0
 raw=Image.fromarray(q);raw.save(folder/(name+'_raw.png'))
 im=Image.new('RGBA',(725,1061));rx,ry,rw,rh=rect
 im.alpha_composite(raw.resize((rw,rh),Image.Resampling.LANCZOS),(rx,ry))
 if 'LEG_' in name:
  pixels=np.array(im)
  ys=np.array([774,877,1045])
  target=np.array([309,245,199.5] if name.endswith('_L') else [415,461,542.5])
  centers=[]
  for row in ys:
   xs=np.where(pixels[row,:,3]>128)[0];assert len(xs)>0
   centers.append((xs[0]+xs[-1])/2)
  shifts=np.interp(np.arange(1061),ys,target-np.array(centers))
  xx,yy=np.meshgrid(np.arange(725,dtype='float32'),np.arange(1061,dtype='float32'))
  im=Image.fromarray(cv2.remap(pixels,xx-shifts[:,None].astype('float32'),yy,cv2.INTER_CUBIC,borderMode=cv2.BORDER_CONSTANT))
 b=im.getbbox();part=im.crop(b);part.save(folder/(name+'.png'))
 count,cc,ss,c=cv2.connectedComponentsWithStats((np.array(part)[:,:,3]>64).astype('uint8'),8)
 areas=sorted(ss[1:,4],reverse=True);assert areas[0]/sum(areas)>.999
 entries.append({'name':name,'bounds_px':list(b),'source_crop':[int(x0),int(y0),int(x1),int(y1)],'largest_alpha_component_ratio':float(areas[0]/sum(areas))})
(folder/'registration.json').write_text(json.dumps({'layers':entries},indent=2),encoding='utf-8')
print(json.dumps(entries,indent=2))
