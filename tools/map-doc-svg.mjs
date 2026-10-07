/**
 * Pure map-document v2 to passive SVG adapter. Geometry is independent of the site
 * runtime. The source renderer uses painter order and isolated composite opacity:
 * applying opacity to each child would darken overlaps and change walls/rooms.
 * Reference semantics are checked by verify-map-docs.mjs against the collected
 * renderer, while this module rejects data it does not know how to preserve.
 */
import { requireValue } from './resource-bundle.mjs';

export const CONVERTER_VERSION = 'map-doc-v2/1';
const defaults = ['zone','empty','terrain','layout','objects','walls','stairs','accents','room-numbers'];
const roles = {
  building:{fill:'#a1d4a8',group:'layout'}, structure:{fill:'#332d2f',group:'objects'},
  concrete:{fill:'#59595b',group:'layout'}, road:{fill:'#6d6e71',group:'layout'},
  water:{fill:'#4d7c8b',group:'layout'}, 'terrain-low':{fill:'#353943',group:'terrain'},
  'terrain-high':{fill:'#424751',group:'terrain'}, danger:{fill:'#ed1c24',opacity:.75,edge:'#ed1c24',edgeWidth:2,edgeDash:4,group:'accents'},
  'danger-solid':{fill:'#d32027',group:'layout'}, 'alarm-zone':{fill:'#ed1c24',opacity:.12,group:'layout'},
  accent:{fill:'#fff200',group:'accents'}, 'warning-sign':{fill:'#b5a926',group:'accents'},
  wall:{stroke:'#000',width:10,group:'walls'}, 'wall-thin':{stroke:'#000',width:5,group:'walls'},
  floor:{fill:'#3a3e48',group:'empty'}, empty:{fill:'#24272d',group:'empty'}, void:{fill:'#101010',group:'empty'},
  'stairs-up':{stroke:'#fff200',width:10,stairs:'up',group:'stairs'},
  'stairs-down':{stroke:'#c97c00',width:10,stairs:'down',group:'stairs'},
  'stairs-both':{stroke:'#fff200',width:10,stairs:'both',group:'stairs'},
  zone:{fill:'#3a3e48',outline:'#000',outlineWidth:15,group:'zone'},
  'structure-light':{fill:'#a7a9ac',group:'objects'}, swamp:{fill:'#6d6e83',group:'layout'},
  rail:{stroke:'#6d6e71',width:2,dash:12,group:'layout'},
  btr:{stroke:'#009245',width:2,dash:12,gapDot:'btr-stop',group:'accents'},
  'btr-stop':{fill:'#00bf55',edge:'#000',edgeWidth:2,group:'accents'},
  signal:{stroke:'#d7df23',width:2,dash:4,group:'accents'},
  'power-line':{stroke:'#009245',width:2,dash:12,group:'accents'},
  spikes:{stroke:'#231f20',width:10.224,group:'objects'},
  'spike-floor':{fill:'#4d4d4f',edge:'#231f20',edgeWidth:1,group:'objects'},
};
const shapeFields = new Set(['id','kind','role','style','renderGroup','points','holes','rect','angle','closed',
  'width','smooth','gaps','precision','text','stairsProfile','stairsBackground','compositeGroups','locked']);
const styleFields = new Set(['fill','stroke','lineWidth','lineCap','lineJoin','miterLimit','dash','dashOffset',
  'opacity','fillOpacity','strokeOpacity','fillRule','shadow']);
const number = (value, label) => { requireValue(typeof value === 'number' && Number.isFinite(value), `Invalid ${label}`); return value; };
const unit = (value, label) => { number(value,label); requireValue(value >= 0 && value <= 1, `Invalid ${label}`); };
const point = (value, label) => { requireValue(Array.isArray(value) && value.length === 2, `Invalid ${label}`); value.forEach(n=>number(n,label)); };
const fields = (value, allowed, label) => { for(const key of Object.keys(value)) requireValue(allowed.has(key), `Unknown ${label} field: ${key}`); };
export const escapeXml = value => String(value).replaceAll('&','&amp;').replaceAll('"','&quot;').replaceAll('<','&lt;').replaceAll('>','&gt;');
const fmt = value => String(Object.is(value,-0) ? 0 : value);
const attrs = values => Object.entries(values).filter(([,v])=>v !== undefined).map(([k,v])=>` ${k}="${escapeXml(v)}"`).join('');
const pathElement = (d, values = {}) => `<path${attrs({d,fill:'none',...values})}/>`;
const round = value => Math.round(value*10)/10;
const closed = shape => shape.kind !== 'wall' || !!shape.closed;
export function renderGroup(shape) { return shape.renderGroup || (shape.kind === 'text' ? 'accents' : roles[shape.role].group); }

export function validateDocument(doc, mapId = doc?.map) {
  requireValue(doc?.version === 2 && doc.map === mapId, 'Unsupported document version or map ID');
  fields(doc,new Set(['map','version','layers','groupOrder','wallWidths','bgFill','levelContexts','sharedGroups','heightConfig','updatedClient']), 'document');
  requireValue(doc.layers && typeof doc.layers === 'object' && !Array.isArray(doc.layers) && Object.keys(doc.layers).length, 'Document has no layers');
  requireValue(Array.isArray(doc.groupOrder) && new Set(doc.groupOrder).size === doc.groupOrder.length
    && doc.groupOrder.every(group=>defaults.includes(group)), 'Invalid document group order');
  requireValue(!doc.bgFill || roles[doc.bgFill]?.fill, `Unknown background role: ${doc.bgFill}`);
  for(const [role,width] of Object.entries(doc.wallWidths ?? {})) {
    requireValue(roles[role] && number(width,'wall width') > 0, 'Invalid wall width');
  }
  for(const [layer, shapes] of Object.entries(doc.layers)) {
    requireValue(/^[a-zA-Z0-9_-]+$/.test(layer) && Array.isArray(shapes), 'Invalid document layer');
    const groups = new Map();
    for(const shape of shapes) {
      fields(shape,shapeFields,'shape');
      requireValue(['poly','wall','text','ellipse','rect'].includes(shape.kind), `Unknown shape: ${shape.kind}`);
      requireValue(roles[shape.role], `Unknown role: ${shape.role}`);
      requireValue(defaults.includes(renderGroup(shape)), `Unknown render group: ${renderGroup(shape)}`);
      if (shape.kind === 'text') {
        const text = shape.text; requireValue(text && typeof text.value === 'string', 'Invalid text');
        fields(text,new Set(['value','x','y','size','fontFamily','fontWeight','letterSpacing','align','baseline','rotate','lineHeight','runs']), 'text');
        for(const key of ['x','y','size']) number(text[key],`text ${key}`);
        requireValue(text.size > 0, 'Invalid font size');
        if(text.runs) for(const run of text.runs) {
          fields(run,new Set(['start','end','x','y','size','fontFamily','fontWeight','letterSpacing','fill']), 'text run');
          requireValue(Number.isInteger(run.start) && Number.isInteger(run.end) && run.start >= 0 && run.end <= text.value.length && run.end >= run.start, 'Invalid text run range');
          number(run.x,'text run x');number(run.y,'text run y');
        }
      } else {
        if(['rect','ellipse'].includes(shape.kind)) {
          requireValue(Array.isArray(shape.rect) && shape.rect.length === 2, 'Invalid rectangle'); shape.rect.forEach(p=>point(p,'rect point'));
        } else {
          requireValue(Array.isArray(shape.points) && shape.points.length >= 2, 'Invalid shape points'); shape.points.forEach(p=>point(p,'point'));
        }
        for(const hole of shape.holes ?? []) { requireValue(Array.isArray(hole) && hole.length >= 3, 'Invalid polygon hole'); hole.forEach(p=>point(p,'hole point')); }
        for(const gap of shape.gaps ?? []) {
          fields(gap,new Set(['seg','t','w']), 'gap'); const count = vertices(shape).length - (closed(shape)?0:1);
          requireValue(Number.isInteger(gap.seg) && gap.seg >= 0 && gap.seg < count, 'Invalid gap segment');
          unit(gap.t,'gap position');requireValue(number(gap.w,'gap width') > 0, 'Invalid gap width');
        }
      }
      for(const key of ['width','angle']) if(shape[key] !== undefined) number(shape[key],key);
      if(shape.precision !== undefined) requireValue(shape.precision === 'source', 'Unknown point precision');
      if(shape.style) {
        const style=shape.style; fields(style,styleFields,'style');
        for(const key of ['lineWidth','miterLimit','dashOffset']) if(style[key] !== undefined) number(style[key],key);
        for(const key of ['opacity','fillOpacity','strokeOpacity']) if(style[key] !== undefined) unit(style[key],key);
        if(style.dash) { requireValue(Array.isArray(style.dash),'Invalid dash');style.dash.forEach(n=>requireValue(number(n,'dash')>=0,'Invalid dash')); }
        if(style.shadow) {fields(style.shadow,new Set(['color','blur','x','y']),'shadow'); for(const key of ['blur','x','y'])number(style.shadow[key],`shadow ${key}`);}
        if(style.fillRule) requireValue(['nonzero','evenodd'].includes(style.fillRule),'Unknown fill rule');
      }
      if(shape.stairsProfile) {
        fields(shape.stairsProfile,new Set(['borderWidth','frameOffset','frameWidth','spans']),'stairs profile');
        requireValue(roles[shape.role].stairs && shape.points?.length === 2 && shape.width > 0, 'Invalid stairs profile');
        for(const key of ['borderWidth','frameOffset','frameWidth'])number(shape.stairsProfile[key],key);
        for(const span of shape.stairsProfile.spans) {point(span,'stair span');unit(span[0],'stair span');unit(span[1],'stair span');requireValue(span[0]<=span[1],'Inverted stair span');}
      }
      (shape.compositeGroups ?? []).forEach((group, depth)=>{
        fields(group,new Set(['id','opacity','renderGroup']),'composite group');
        requireValue(typeof group.id === 'string' && group.id, 'Invalid composite group ID'); unit(group.opacity,'composite opacity');
        if(group.renderGroup) requireValue(defaults.includes(group.renderGroup), 'Unknown composite render group');
        const key=JSON.stringify(shape.compositeGroups.slice(0,depth).map(g=>g.id))+'/'+group.id;
        const signature=JSON.stringify([group.opacity,group.renderGroup]);
        requireValue(!groups.has(key)||groups.get(key)===signature, 'Inconsistent composite group');groups.set(key,signature);
      });
    }
  }
  for(const [level,contexts] of Object.entries(doc.levelContexts ?? {})) {
    requireValue(Object.hasOwn(doc.layers,level)&&Array.isArray(contexts),'Invalid level context');const seen=new Set();
    for(const context of contexts) { fields(context,new Set(['layer','opacity']),'level context');requireValue(Object.hasOwn(doc.layers,context.layer)&&context.layer!==level&&!seen.has(context.layer),'Invalid context reference');unit(context.opacity,'context opacity');seen.add(context.layer); }
  }
  for(const [level,groups] of Object.entries(doc.sharedGroups ?? {})) requireValue(Object.hasOwn(doc.layers,level)&&Array.isArray(groups)&&new Set(groups).size===groups.length&&groups.every(g=>defaults.includes(g)), 'Invalid shared group reference');
  return doc;
}

export function vertices(shape) {
  if(!['rect','ellipse'].includes(shape.kind)) return shape.points ?? [];
  const [[x,y],[endX,endY]]=shape.rect;
  if(shape.angle) {
    const radians=shape.angle*Math.PI/180,c=Math.cos(radians),s=Math.sin(radians),dx=endX-x,dy=endY-y;
    const width=dx*c+dy*s,height=-dx*s+dy*c;
    return [[x,y],[x+c*width,y+s*width],[endX,endY],[x-s*height,y+c*height]];
  }
  return [[Math.min(x,endX),Math.min(y,endY)],[Math.max(x,endX),Math.min(y,endY)],
    [Math.max(x,endX),Math.max(y,endY)],[Math.min(x,endX),Math.max(y,endY)]];
}
export function polyPath(points, close=false, smooth=false) {
  if(!points.length)return '';
  let d=`M${points[0].map(fmt).join(' ')}`;
  if(smooth) {
    const at=index=>close?points[(index%points.length+points.length)%points.length]:points[Math.max(0,Math.min(points.length-1,index))];
    for(let index=0;index<(close?points.length:points.length-1);index++) {
      const a=at(index-1),b=at(index),c=at(index+1),e=at(index+2);
      d+=`C${[b[0]+(c[0]-a[0])/6,b[1]+(c[1]-a[1])/6,c[0]-(e[0]-b[0])/6,c[1]-(e[1]-b[1])/6,...c].map(fmt).join(' ')}`;
    }
  } else for(const p of points.slice(1))d+=`L${p.map(fmt).join(' ')}`;
  return d+(close?'Z':'');
}
function shapePath(shape) {
  let d;
  if(shape.kind==='ellipse') {
    const p=vertices(shape),cx=(p[0][0]+p[2][0])/2,cy=(p[0][1]+p[2][1])/2;
    const rx=Math.hypot(p[1][0]-p[0][0],p[1][1]-p[0][1])/2,ry=Math.hypot(p[3][0]-p[0][0],p[3][1]-p[0][1])/2;
    const r=(shape.angle||0)*Math.PI/180,dx=rx*Math.cos(r),dy=rx*Math.sin(r);
    d=`M${cx+dx} ${cy+dy}A${rx} ${ry} ${shape.angle||0} 0 1 ${cx-dx} ${cy-dy}A${rx} ${ry} ${shape.angle||0} 0 1 ${cx+dx} ${cy+dy}Z`;
  } else d=polyPath(vertices(shape),closed(shape),shape.smooth);
  return d+(shape.holes??[]).map(hole=>polyPath(hole,true)).join('');
}

// A gap is measured along the complete polyline, so it can cross a vertex or wrap
// around a closed wall. Cutting only its named segment leaves a false wall stub.
export function wallSegments(shape) {
  if(!shape.gaps?.length||shape.smooth)return null;
  const points=vertices(shape),count=points.length-(closed(shape)?0:1);
  const lengths=Array.from({length:count},(_,i)=>Math.hypot(points[(i+1)%points.length][0]-points[i][0],points[(i+1)%points.length][1]-points[i][1]));
  const total=lengths.reduce((a,b)=>a+b,0),cuts=new Map();
  for(const gap of shape.gaps) {
    const center=lengths.slice(0,gap.seg).reduce((a,b)=>a+b,0)+lengths[gap.seg]*gap.t,half=gap.w/2;
    let ranges=[[Math.max(0,center-half),Math.min(total,center+half)]];
    if(closed(shape)) {
      if(half*2>=total)ranges=[[0,total]];
      else {if(center-half<0)ranges.push([total+center-half,total]);if(center+half>total)ranges.push([0,center+half-total]);}
    }
    let offset=0;
    for(let i=0;i<count;i++) {for(const [start,end] of ranges){const a=Math.max(0,start-offset),b=Math.min(lengths[i],end-offset);if(b-a>1e-8){if(!cuts.has(i))cuts.set(i,[]);cuts.get(i).push([a<1e-8?0:a/lengths[i],lengths[i]-b<1e-8?1:b/lengths[i]]);}}offset+=lengths[i];}
  }
  for(const ranges of cuts.values()) {ranges.sort((a,b)=>a[0]-b[0]);for(let i=ranges.length-1;i>0;i--)if(ranges[i][0]<=ranges[i-1][1]){ranges[i-1][1]=Math.max(ranges[i-1][1],ranges[i][1]);ranges.splice(i,1);}}
  const quantize=shape.precision==='source'?n=>n:round,interpolate=(a,b,t)=>a.map((n,i)=>quantize(n+(b[i]-n)*t));
  const paths=[];let current=[];
  for(let i=0;i<count;i++) {
    const a=points[i],b=points[(i+1)%points.length];if(!current.length)current.push([...a]);
    for(const [start,end]of cuts.get(i)??[]) {if(start>0)current.push(interpolate(a,b,start));if(current.length>=2)paths.push(current);current=end<1?[interpolate(a,b,end)]:[];}
    if(current.length)current.push([...b]);
  }
  if(current.length>=2)paths.push(current);
  if(closed(shape)&&paths.length>1) {const first=paths[0][0],last=paths.at(-1).at(-1);if(Math.abs(first[0]-last[0])<.01&&Math.abs(first[1]-last[1])<.01){const tail=paths.pop();paths[0]=tail.concat(paths[0].slice(1));}}
  return paths;
}
function ordered(shapes,order,depth=0) {
  const grouped=new Map(),entries=[];
  shapes.forEach((shape,index)=>{const composite=shape.compositeGroups?.[depth];if(!composite){entries.push({shapes:[shape],group:renderGroup(shape),index});return;}
    let entry=grouped.get(composite.id);if(!entry){entry={shapes:[],group:composite.renderGroup||renderGroup(shape),index,composite:true};grouped.set(composite.id,entry);entries.push(entry);}entry.shapes.push(shape);});
  return entries.sort((a,b)=>order.indexOf(a.group)-order.indexOf(b.group)||a.index-b.index).flatMap(entry=>entry.composite?ordered(entry.shapes,order,depth+1):entry.shapes);
}
function groupOrder(doc) {
  const result=[...doc.groupOrder];
  for(const group of defaults)if(!result.includes(group)){const index=result.findIndex(g=>defaults.indexOf(g)>defaults.indexOf(group));result.splice(index<0?result.length:index,0,group);}
  return result;
}
function isBackdrop(shape,size) {
  const points=vertices(shape),margin=1000;
  return ['floor','empty'].includes(shape.role)&&['poly','rect'].includes(shape.kind)&&points.length===4&&!shape.holes?.length&&!shape.smooth
    && new Set(points.map(p=>p.join(','))).size===4&&points.every(([x,y])=>(x===margin||x===size.width-margin)&&(y===margin||y===size.height-margin));
}
export function documentPasses(doc,level,size) {
  const order=groupOrder(doc),own=doc.layers[level];const contexts=[...(doc.levelContexts?.[level]??[])];
  for(const [layer,groups]of Object.entries(doc.sharedGroups??{}))if(groups.length&&!contexts.some(c=>c.layer===layer))contexts.push({layer,opacity:0});
  const contextShapes=contexts.flatMap(({layer,opacity})=>{
    if(layer===level)return [];const shared=new Set(doc.sharedGroups?.[layer]??[]);
    let serial=0,previous=-1;
    return ordered(doc.layers[layer].filter(s=>opacity>0||shared.has(renderGroup(s))),order).map(shape=>{const alpha=shared.has(renderGroup(shape))?1:opacity;if(alpha!==previous)serial++;previous=alpha;
      return {...shape,compositeGroups:[{id:`context:${layer}:${serial}`,opacity:alpha,renderGroup:'accents'},...shape.compositeGroups??[]]};});
  });
  if(!contextShapes.length)return [ordered(own,order)];
  const background={kind:'rect',role:'void',rect:[[1000,1000],[size.width-1000,size.height-1000]],style:{fill:roles[doc.bgFill]?.fill||'#101010'},background:true};
  const backdrops=own.filter(shape=>renderGroup(shape)!=='zone'&&isBackdrop(shape,size)),set=new Set(backdrops);
  return [size.width>2000&&size.height>2000?[background]:[],ordered(backdrops,order),contextShapes,
    ordered(own.filter(shape=>renderGroup(shape)!=='zone'&&!set.has(shape)),order),ordered(own.filter(shape=>renderGroup(shape)==='zone'),order)];
}

function stairs(points,width,color,background=true) {
  const p=points.filter((point,index)=>!index||Math.hypot(point[0]-points[index-1][0],point[1]-points[index-1][1])>1e-9);
  if(p.length<2||width<=0)return '';
  const distances=[0],normals=[];
  for(let i=1;i<p.length;i++){const dx=p[i][0]-p[i-1][0],dy=p[i][1]-p[i-1][1],len=Math.hypot(dx,dy);distances.push(distances.at(-1)+len);normals.push([-dy/len,dx/len]);}
  const vectors=p.map((_,i)=>{if(!i)return normals[0];if(i===p.length-1)return normals.at(-1);const a=normals[i-1],b=normals[i],dot=1+a[0]*b[0]+a[1]*b[1];return dot<.125?b:[(a[0]+b[0])/dot,(a[1]+b[1])/dot];});
  const left=p.map((v,i)=>v.map((n,j)=>n+vectors[i][j]*width/2)),right=p.map((v,i)=>v.map((n,j)=>n-vectors[i][j]*width/2));
  const length=distances.at(-1),thickness=Math.min(length,width*.11),count=Math.max(1,Math.round(length/(width*.335))),step=count>1?(length-thickness)/(count-1):0;
  const at=(edge,d)=>{let i=0;while(i+2<distances.length&&distances[i+1]<d)i++;const t=(d-distances[i])/(distances[i+1]-distances[i]);return edge[i].map((n,j)=>n+(edge[i+1][j]-n)*t);};
  const section=(edge,a,b)=>[at(edge,a),...edge.filter((_,i)=>distances[i]>a&&distances[i]<b),at(edge,b)];
  let svg=background?pathElement(polyPath([...left,...right.slice().reverse()],true),{fill:'#59595b'}):'';
  for(let i=0;i<count;i++){const start=count>1?step*i:(length-thickness)/2,end=Math.min(length,start+thickness);svg+=pathElement(polyPath([...section(left,start,end),...section(right,start,end).reverse()],true),{fill:color});}
  return svg+pathElement(polyPath(left)+polyPath(right),{stroke:'#000','stroke-width':Math.min(width,length)*.08,'stroke-linecap':'butt'});
}
function stairArrow(points,padding,width,color,kind) {
  if(kind==='both')return `<g transform="translate(${-width*.3} ${-width*.3})">${stairArrow(points,padding,width,'#fff200','up')}</g><g transform="translate(${width*.3} ${width*.3})">${stairArrow(points,padding,width,'#c97c00','down')}</g>`;
  const sign=kind==='up'?1:-1,dx=sign*Math.SQRT1_2,dy=-sign*Math.SQRT1_2,x=Math.max(...points.map(p=>p[0]))+padding,y=Math.max(...points.map(p=>p[1]))+padding;
  const tip=[x+dx*width/2,y+dy*width/2],tail=[x-dx*width/2,y-dy*width/2],neck=[tip[0]-dx*width*.45,tip[1]-dy*width*.45];
  const triangle=polyPath([tip,[neck[0]+dy*width*.25,neck[1]-dx*width*.25],[neck[0]-dy*width*.25,neck[1]+dx*width*.25]],true),line=polyPath([tail,neck]);
  return pathElement(line,{stroke:'#000','stroke-width':width*.35,'stroke-linecap':'round'})+pathElement(triangle,{fill:'#000',stroke:'#000','stroke-width':width*.2,'stroke-linejoin':'round'})
    +pathElement(line,{stroke:color,'stroke-width':width*.15,'stroke-linecap':'round'})+pathElement(triangle,{fill:color});
}

function curveSamples(points,close) {
  const at=index=>close?points[(index%points.length+points.length)%points.length]:points[Math.max(0,Math.min(points.length-1,index))],result=[points[0]];
  for(let i=0;i<(close?points.length:points.length-1);i++) {
    const a=at(i-1),b=at(i),c=at(i+1),d=at(i+2),c1=b.map((n,j)=>n+(c[j]-a[j])/6),c2=c.map((n,j)=>n-(d[j]-b[j])/6);
    for(let step=1;step<=8;step++){const t=step/8,u=1-t;result.push(b.map((n,j)=>u*u*u*n+3*u*u*t*c1[j]+3*u*t*t*c2[j]+t*t*t*c[j]));}
  }
  return result;
}
function spikes(points,width,color) {
  const segments=points.slice(1).map((end,i)=>({a:points[i],b:end,length:Math.hypot(end[0]-points[i][0],end[1]-points[i][1])})).filter(s=>s.length>1e-8);
  const length=segments.reduce((n,s)=>n+s.length,0);if(!length||width<=0)return '';
  const count=Math.min(10000,Math.max(1,Math.round(length/(width*5.933/10.224))));let index=0,offset=0;
  const at=(distance,side=0)=>{while(index<segments.length-1&&distance>offset+segments[index].length)offset+=segments[index++].length;const s=segments[index],t=Math.max(0,Math.min(1,(distance-offset)/s.length)),dx=(s.b[0]-s.a[0])/s.length,dy=(s.b[1]-s.a[1])/s.length;return [s.a[0]+t*(s.b[0]-s.a[0])-dy*side,s.a[1]+t*(s.b[1]-s.a[1])+dx*side];};
  let first=at(0),d='';const nodes=[first];
  for(let i=0;i<count;i++){const middle=(i+.5)*length/count,left=at(middle,width/2),right=at(middle,-width/2),end=at((i+1)*length/count);d+=polyPath([first,left,end,right],true);nodes.push(left,right,end);first=end;}
  return pathElement(d,{stroke:color,'stroke-width':width*.5/10.224})+nodes.map(p=>`<circle cx="${p[0]}" cy="${p[1]}" r="${width*.053}" fill="${color}"/>`).join('');
}

// The stop glyph is a source vector asset, not an executable drawing callback.
// Its original coordinate radius is retained so noncircular stops stay elliptical.
const stopGlyph='m1.547,.288c-.14-.167-.14-.41,0-.577l3.814-4.545c.355-.423.3-1.053-.123-1.408h0c-.423-.355-1.053-.3-1.408.123l-3.485,4.154c-.179.214-.508.214-.688,0l-3.485-4.154c-.355-.423-.985-.478-1.408-.123h0c-.423.355-.478.985-.123,1.408l3.814,4.545c.14.167.14.41,0,.577l-3.814,4.545c-.355.423-.3,1.053.123,1.408h0c.423.355,1.053.3,1.408-.123l3.485-4.154c.179-.214.508-.214.688,0l3.485,4.154c.355.423.985.478,1.408.123h0c.423-.355.478-.985.123-1.408l-3.814-4.545Z';
function stopSymbol(x,y,rx,ry=rx,angle=0,opacity=1) {
  return `<g transform="translate(${x} ${y}) rotate(${angle}) scale(${rx/12.049} ${ry/12.049})" opacity="${opacity}">${pathElement(stopGlyph,{stroke:'#000','stroke-width':1,'stroke-linejoin':'miter'})}</g>`;
}

export function convertDocument(doc,size) {
  validateDocument(doc);number(size.width,'map width');number(size.height,'map height');
  const defs=[];let serial=0;
  const width=shape=>shape.style?.lineWidth??(shape.width||doc.wallWidths?.[shape.role]||roles[shape.role].width||5);
  const shadowFilter=shadow=>{const id=`shadow-${serial++}`;defs.push(`<filter id="${id}" x="-100%" y="-100%" width="300%" height="300%"><feGaussianBlur in="SourceAlpha" stdDeviation="${shadow.blur/2}" result="blur"/><feOffset in="blur" dx="${shadow.x}" dy="${shadow.y}" result="offset"/><feFlood flood-color="${escapeXml(shadow.color)}"/><feComposite in2="offset" operator="in"/><feMerge><feMergeNode/><feMergeNode in="SourceGraphic"/></feMerge></filter>`);return `url(#${id})`;};
  const textShape=(text,color,opacity,filter)=>{
    let runs=text.runs?.length?text.runs.map(run=>({...text,value:text.value.slice(run.start,run.end),x:run.x*text.size,y:run.y*text.size,size:(run.size??1)*text.size,fontFamily:run.fontFamily??text.fontFamily,fontWeight:run.fontWeight??text.fontWeight,letterSpacing:run.letterSpacing===undefined?text.letterSpacing:run.letterSpacing*text.size,fill:run.fill}))
      :text.value.split(/\r?\n/).map((value,i)=>({...text,value,x:0,y:i*text.size*(text.lineHeight??1.2)}));
    const baseline={alphabetic:'alphabetic',top:'text-before-edge',hanging:'hanging',middle:'central',bottom:'text-after-edge',ideographic:'ideographic'};
    return `<g transform="translate(${text.x} ${text.y}) rotate(${text.rotate||0})">`+runs.filter(run=>run.fill!=='none').map(run=>`<text${attrs({x:run.x,y:run.y,fill:run.fill||color,opacity,filter,'font-size':run.size,'font-family':run.fontFamily||'bender, pretendard, Source Sans Pro, Arial, sans-serif','font-weight':run.fontWeight||'bold','letter-spacing':run.letterSpacing??0,'text-anchor':['left','start'].includes(run.align)?'start':['right','end'].includes(run.align)?'end':'middle','dominant-baseline':baseline[run.baseline||'middle'],'xml:space':'preserve'})}>${escapeXml(run.value)}</text>`).join('')+'</g>';
  };
  function draw(shape) {
    const role=roles[shape.role],style=shape.style,points=vertices(shape);
    let svg='';
    if(shape.stairsProfile) {
      const [a,b]=points,profile=shape.stairsProfile,w=shape.width,len=Math.hypot(b[0]-a[0],b[1]-a[1]),top=(profile.frameOffset-profile.frameWidth/2)*w;
      const frame=polyPath([[0,top],[len,top],[len,top+profile.frameWidth*w],[0,top+profile.frameWidth*w]],true);
      svg=`<g transform="translate(${a.join(' ')}) rotate(${Math.atan2(b[1]-a[1],b[0]-a[0])*180/Math.PI})">`+pathElement(frame,{stroke:'#000','stroke-width':profile.borderWidth*w,'stroke-miterlimit':10})
        +pathElement(profile.spans.map(([start,end])=>polyPath([[start*len,0],[end*len,0]])).join(''),{stroke:role.stroke,'stroke-width':w,'stroke-linecap':'butt'})+'</g>';
    } else if(shape.kind==='text') {
      svg=style?(style.fill&&style.fill!=='none'?textShape(shape.text,style.fill,(style.opacity??1)*(style.fillOpacity??1),style.shadow?shadowFilter(style.shadow):undefined):'')
        :textShape(shape.text,role.fill||role.stroke||'#000',1);
    } else if(style) {
      const d=shapePath(shape),alpha=style.opacity??1,filter=style.shadow?shadowFilter(style.shadow):undefined;
      if(style.fill&&style.fill!=='none')svg+=pathElement(d,{fill:shape.role==='water'?roles.water.fill:style.fill,opacity:alpha*(style.fillOpacity??1),'fill-rule':style.fillRule||(shape.holes?.length?'evenodd':'nonzero'),filter});
      if(style.stroke&&style.stroke!=='none'&&(style.lineWidth??width(shape))>0) {
        const segments=wallSegments(shape),strokePath=segments?segments.map(segment=>polyPath(segment)).join('')+(shape.holes??[]).map(h=>polyPath(h,true)).join(''):d;
        svg+=pathElement(strokePath,{stroke:style.stroke,'stroke-width':style.lineWidth??width(shape),'stroke-linecap':style.lineCap||'butt','stroke-linejoin':style.lineJoin||'miter','stroke-miterlimit':style.miterLimit??4,'stroke-dasharray':style.dash?.length?style.dash.join(' '):undefined,'stroke-dashoffset':style.dashOffset||0,opacity:alpha*(style.strokeOpacity??1),filter});
      }
    } else if(role.fill&&!role.outline) {
      svg=pathElement(shapePath(shape),{fill:role.fill,opacity:role.opacity??1,'fill-rule':shape.holes?.length?'evenodd':'nonzero'});
      if(role.edge)svg+=pathElement(shapePath(shape),{stroke:role.edge,'stroke-width':role.edgeWidth,'stroke-dasharray':role.edgeDash?`${role.edgeDash} ${role.edgeDash}`:undefined});
      if(shape.role==='spike-floor'||shape.role==='alarm-zone') {
        const clip=`detail-clip-${serial++}`;defs.push(`<clipPath id="${clip}">${pathElement(shapePath(shape),{fill:'#000','clip-rule':shape.holes?.length?'evenodd':'nonzero'})}</clipPath>`);
        if(shape.role==='spike-floor') {
          const radians=(shape.angle||0)*Math.PI/180,c=Math.cos(radians),s=Math.sin(radians),local=points.map(([x,y])=>[x*c+y*s,-x*s+y*c]);
          const x1=Math.min(...local.map(p=>p[0])),x2=Math.max(...local.map(p=>p[0])),y1=Math.min(...local.map(p=>p[1])),y2=Math.max(...local.map(p=>p[1]));
          const step=Math.max(5,Math.sqrt((x2-x1)*(y2-y1)/10000)),columns=Math.floor((x2-x1)/step),rows=Math.floor((y2-y1)/step),startX=(x1+x2-(columns-1)*step)/2,startY=(y1+y2-(rows-1)*step)/2;
          let dots='';for(let row=0;row<rows;row++)for(let col=0;col<columns;col++)dots+=`<circle cx="${startX+col*step}" cy="${startY+row*step}" r="0.5"/>`;
          svg+=`<g clip-path="url(#${clip})"><g fill="#231f20" transform="rotate(${shape.angle||0})">${dots}</g></g>`;
        } else {
          const x1=Math.min(...points.map(p=>p[0])),x2=Math.max(...points.map(p=>p[0])),y1=Math.min(...points.map(p=>p[1])),y2=Math.max(...points.map(p=>p[1]));
          // Source alarm-zone hatching has a fixed map-space grid origin, rather
          // than restarting at each polygon. Keeping this phase aligns adjacent
          // clipped polygons; these are source asset coordinates, not screen pixels.
          const step=Math.max(6,Math.ceil(Math.sqrt((x2-x1)*(y2-y1)/10000)/6)*6),startX=1651.258+Math.floor((x1-1651.258-2)/step)*step,startY=1604.927+Math.floor((y1-1604.927-2)/step)*step;
          let marks='';for(let y=startY;y<=y2+2;y+=step)for(let x=startX;x<=x2+2;x+=step)marks+=`<rect x="${x-1.5}" y="${y-.05}" width="3" height="0.1"/><rect x="${x-.05}" y="${y-1.5}" width="0.1" height="3"/>`;
          svg+=`<g clip-path="url(#${clip})" opacity="0.3" fill="none" stroke="#ed1c24" stroke-width="0.25">${marks}</g>`;
        }
      }
    } else if(role.stairs&&shape.kind!=='ellipse') {
      const w=width(shape);
      if(shape.kind==='rect') {
        const a=points[0].map((n,i)=>round((n+points[1][i])/2)),b=points[3].map((n,i)=>round((n+points[2][i])/2)),span=round(Math.hypot(points[1][0]-points[0][0],points[1][1]-points[0][1]));
        svg=stairs([a,b],span,role.stroke,shape.stairsBackground!==false)+stairArrow(points,0,span,role.stroke,role.stairs);
      } else {
        const sampled=shape.smooth?curveSamples(points,closed(shape)):points;
        svg=(wallSegments(shape)??[closed(shape)?[...sampled,sampled[0]]:sampled]).map(segment=>stairs(segment,w,role.stroke,shape.stairsBackground!==false)).join('')+stairArrow(sampled,w/2,w,role.stroke,role.stairs);
      }
    } else if(role.stroke) {
      if(shape.role==='spikes'&&shape.kind!=='ellipse') {
        const sampled=shape.smooth?curveSamples(points,closed(shape)):points;
        svg=(wallSegments(shape)??[closed(shape)?[...sampled,sampled[0]]:sampled]).map(segment=>spikes(segment,width(shape),role.stroke)).join('');
      } else svg=pathElement(wallSegments(shape)?.map(segment=>polyPath(segment)).join('')??shapePath(shape),{stroke:role.stroke,'stroke-width':width(shape),'stroke-dasharray':role.dash?`${role.dash} ${role.dash}`:undefined});
    }
    if(shape.role==='btr-stop'&&shape.kind==='ellipse') {
      const p=vertices(shape),rx=Math.hypot(p[1][0]-p[0][0],p[1][1]-p[0][1])/2,ry=Math.hypot(p[3][0]-p[0][0],p[3][1]-p[0][1])/2;
      svg+=stopSymbol((p[0][0]+p[2][0])/2,(p[0][1]+p[2][1])/2,rx,ry,shape.angle||0,style?.opacity??1);
    }
    if(role.gapDot&&shape.gaps?.length&&!shape.smooth) for(const gap of shape.gaps) {
      const a=points[gap.seg],b=points[(gap.seg+1)%points.length],x=a[0]+(b[0]-a[0])*gap.t,y=a[1]+(b[1]-a[1])*gap.t,r=gap.w/2;
      svg+=`<g opacity="${style?.opacity??1}"><circle cx="${x}" cy="${y}" r="${r}" fill="#00bf55" stroke="#000" stroke-width="2"/>${stopSymbol(x,y,r)}</g>`;
    }
    const extentMask=shape.role==='void'&&['poly','rect'].includes(shape.kind)&&points.length===4
      && points.every(([x,y])=>(x===1000||x===size.width-1000)&&(y===1000||y===size.height-1000));
    return `<g${attrs({'data-source-layer':shape.sourceLayer,'data-source-index':shape.sourceIndex,'data-map-background':shape.background||isBackdrop(shape,size)||extentMask?'true':undefined})}>${svg}</g>`;
  }
  function outline(shape) {
    const role=roles[shape.role];if(!role.outline)return '';
    const d=polyPath(vertices(shape),true,shape.smooth);
    return `<g data-map-background="true">`+(size.width>2000&&size.height>2000?pathElement(polyPath([[1000,1000],[size.width-1000,1000],[size.width-1000,size.height-1000],[1000,size.height-1000]],true)+d,{fill:'#101010','fill-rule':'evenodd'}):'')+'</g>'+pathElement(d,{stroke:role.outline,'stroke-width':role.outlineWidth});
  }
  function composite(shapes,depth=0) {
    let result='';
    for(let i=0;i<shapes.length;) {
      const shape=shapes[i],group=shape.compositeGroups?.[depth];
      if(!group){result+=draw(shape);i++;continue;}
      let end=i+1;while(end<shapes.length&&shapes[end].compositeGroups?.[depth]?.id===group.id)end++;
      const children=shapes.slice(i,end);
      result+=`<g${attrs({'data-composite':group.id,opacity:group.opacity})}>`+composite(children,depth+1)
        +children.filter(child=>!child.compositeGroups?.[depth+1]&&renderGroup(child)==='zone').map(outline).join('')+'</g>';i=end;
    }
    return result;
  }
  const annotated={...doc,layers:Object.fromEntries(Object.entries(doc.layers).map(([layer,shapes])=>[layer,shapes.map((shape,index)=>({...shape,sourceLayer:layer,sourceIndex:index}))]))};
  let content='';
  if(doc.bgFill&&size.width>2000&&size.height>2000)content+=`<rect data-map-background="true" x="1000" y="1000" width="${size.width-2000}" height="${size.height-2000}" fill="${roles[doc.bgFill].fill}"/>`;
  for(const level of Object.keys(doc.layers)) {
    const passes=documentPasses(annotated,level,size);
    content+=`<g id="${level}" data-map-level="${level}">`+passes.map(pass=>composite(pass)).join('')
      +annotated.layers[level].filter(shape=>renderGroup(shape)==='zone'&&!shape.compositeGroups?.length).map(outline).join('')+'</g>';
  }
  // The source MapLayers draw path applies its getTransform/clearRect helper only
  // to Customs. That helper transforms the rectangle from (1000,1000) to
  // (width-1000,height-1000), then clears its device-pixel exterior with ceil/floor.
  // A map-coordinate clip preserves the terrain; the oracle executes the original
  // helper so its one-pixel viewport rounding remains independently observable.
  if(doc.map==='customs') {defs.push(`<clipPath id="terrain-clip"><rect x="1000" y="1000" width="${size.width-2000}" height="${size.height-2000}"/></clipPath>`);content=`<g clip-path="url(#terrain-clip)">${content}</g>`;}
  return `<?xml version="1.0" encoding="UTF-8"?>\n<svg xmlns="http://www.w3.org/2000/svg" width="${size.width}" height="${size.height}" viewBox="0 0 ${size.width} ${size.height}" class="svg-map">\n<defs>${defs.join('')}</defs>\n${content}\n</svg>\n`;
}
