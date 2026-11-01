// Offline catalog + actual demo-controller checks. This is not a browser/layout test.
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const path = require('node:path');
const elements = new Map();
class Element {
  constructor(){this.style={setProperty(){}};this.dataset={};this.classList={add(){},remove(){},toggle(){}};this.listeners={};this.scrollTop=0;this.value='';}
  setAttribute(){} append(){} querySelector(){return new Element()}
  querySelectorAll(){return []} addEventListener(type,fn){this.listeners[type]=fn}
  getBoundingClientRect(){return {x:0,y:0,left:0,top:0,width:1598,height:760}}
  showModal(){} close(){}
}
const get=id=>{if(!elements.has(id))elements.set(id,new Element());return elements.get(id)};
const sandbox={console,document:{getElementById:get,createElement:()=>new Element(),querySelectorAll:()=>[]},window:{addEventListener(){}},requestAnimationFrame:fn=>fn(),setTimeout:()=>0,clearTimeout(){}};
vm.createContext(sandbox);
for(const file of ['catalog.js','app.js']) vm.runInContext(fs.readFileSync(path.join(__dirname,file),'utf8'),sandbox,{filename:file});
const review=sandbox.researchReview,{nodes,lanes}=review.catalog;
const map=new Map(nodes.map(n=>[n.id,n]));
assert.equal(map.size,nodes.length);assert.ok(nodes.length>=45&&nodes.length<=60);
const visited=new Set(),visiting=new Set();
function walk(n){
 if(visited.has(n.id))return;assert.ok(!visiting.has(n.id),'cycle '+n.id);visiting.add(n.id);
 for(const id of n.all){assert.ok(map.has(id),'missing '+id);walk(map.get(id));}
 visiting.delete(n.id);visited.add(n.id);
}
const future=n=>['PLANNED','PROPOSED'].includes(n.status);
function point(n){if(n.lane==='root')return {x:670,y:27,r:21};const x=132+lanes.findIndex(l=>l.id===n.lane)*260;return n.hub?{x,y:138,r:44}:{x:x+n.dx,y:253+(n.level-1)*100,r:21}}
const drawable=nodes.filter(n=>n.lane!=='epoch');
for(const n of nodes){
 walk(n);assert.equal(n.any.length,0,'OR was not removed');
 if(['root','epoch'].includes(n.lane))continue;
 assert.equal(n.all.length,1,'each research must have one actual predecessor');
 const p=map.get(n.all[0]);assert.ok(p.lane==='root'||p.lane===n.lane,'cross-direction prerequisite');
 assert.ok(point(p).y<point(n).y,'not top-to-bottom');
 if(!future(n))assert.ok(!future(p),'future-only gate');
}
for(let i=0;i<drawable.length;i++)for(let j=i+1;j<drawable.length;j++){
 const a=point(drawable[i]),b=point(drawable[j]);assert.ok(Math.hypot(a.x-b.x,a.y-b.y)>=a.r+b.r+10,'overlapping circles');
}
const edges=drawable.filter(n=>n.all.length).map(n=>({parent:map.get(n.all[0]),child:n}));
for(const edge of edges){
 const a=point(edge.parent),b=point(edge.child);
 for(const n of drawable){
  if(n===edge.parent||n===edge.child)continue;const p=point(n),dx=b.x-a.x,dy=b.y-a.y;
  const t=Math.max(0,Math.min(1,((p.x-a.x)*dx+(p.y-a.y)*dy)/(dx*dx+dy*dy)));
  assert.ok(Math.hypot(p.x-(a.x+t*dx),p.y-(a.y+t*dy))>p.r+3,'edge passes through '+n.id);
 }
}
function orient(a,b,c){return (b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x)}
for(let i=0;i<edges.length;i++)for(let j=i+1;j<edges.length;j++){
 const e=edges[i],f=edges[j];if([e.parent,e.child].some(n=>n===f.parent||n===f.child))continue;
 const a=point(e.parent),b=point(e.child),c=point(f.parent),d=point(f.child);
 assert.ok(!(orient(a,b,c)*orient(a,b,d)<0&&orient(c,d,a)*orient(c,d,b)<0),'crossed edges');
}

const domain=fs.readFileSync(path.join(__dirname,'../../..','Assets/Game/Domain/Research/ResearchModels.cs'),'utf8');
const oldIds=[...domain.matchAll(/new TechnologyDefinition\(T\("([^"]+)"\)/g)].map(m=>m[1]);
assert.equal(oldIds.length,33);for(const id of oldIds)assert.ok(map.has(id),'lost stable ID '+id);
function scenario(value){get('scenario').onchange({target:{value}});return review.snapshot()}
assert.equal(scenario('start').states.woodworking,'locked');
assert.equal(review.snapshot().states.stone_tools,'available');
assert.equal(review.snapshot().states.pottery,'future');
assert.equal(scenario('early').states.herbalism,'available');
assert.equal(review.snapshot().states.formation_training,'locked');
const result=[];
for(const route of ['peaceful','scholar','military','mixed']){
 const s=scenario(route);assert.equal(s.epoch.ready,true,route+' cannot finish era');
 assert.equal(s.states.stone_age_mastery,'available');
 assert.equal(s.states.pottery,'future');
 if(route==='peaceful')assert.ok(!s.done.some(id=>map.get(id).lane==='military'));
 for(const id of s.done){const n=map.get(id);assert.ok(n.all.every(p=>s.done.includes(p)),route+' invalid AND '+id);assert.ok(!n.any.length||n.any.some(p=>s.done.includes(p)),route+' invalid OR '+id);}
 const days=s.done.reduce((sum,id)=>sum+map.get(id).days,map.get('stone_age_mastery').days);
 result.push({route,learned:s.epoch.learned.length,workDays:days,hoursAtFourWorkers:Math.round(days*24/4.4/60*10)/10});
}
// Exercise real click handlers, not a second implementation of prerequisites.
scenario('start');
get('search').value='Каменные орудия';
get('search').listeners.input({target:get('search')});get('search').listeners.keydown({key:'Enter'});
assert.equal(review.snapshot().selected,'stone_tools');
get('startDemo').onclick();assert.equal(review.snapshot().active,'stone_tools');
get('finishDemo').onclick();assert.equal(review.snapshot().states.woodworking,'available');
get('search').value='Приготовление пищи';get('search').listeners.input({target:get('search')});get('search').listeners.keydown({key:'Enter'});
assert.equal(review.snapshot().states.cooking,'locked');get('startDemo').onclick();assert.equal(review.snapshot().active,null);
function select(name){get('search').value=name;get('search').listeners.input({target:get('search')});get('search').listeners.keydown({key:'Enter'})}
function complete(name){select(name);get('startDemo').onclick();assert.ok(review.snapshot().active);get('finishDemo').onclick()}
scenario('start');complete('Управляемый огонь');
assert.equal(review.snapshot().states.cooking,'available','fire alone must unlock cooking');
assert.ok(!review.snapshot().done.includes('hunting'));complete('Приготовление пищи');
assert.equal(review.snapshot().stock.wood,23,'costs paid exactly once');
complete('Каменные орудия');complete('Обработка древесины');
// Organized labor needs a shelter in the demo; supply it through the existing conditions control.
get('conditions').querySelectorAll=selector=>selector==='[data-fact]'?[{dataset:{fact:'shelter'},value:'1'}]:[];
get('conditionsApply').onclick();complete('Организация труда');assert.equal(review.snapshot().states.hide_working,'practice');
scenario('peaceful');get('conditions').querySelectorAll=selector=>selector==='[data-fact]'?[{dataset:{fact:'food'},value:'0'}]:[];
get('conditionsApply').onclick();assert.equal(review.snapshot().epoch.ready,false,'food prerequisite must gate era');
console.log(JSON.stringify({technologies:nodes.length,retainedIds:oldIds.length,lanes:lanes.length,graph:'DAG; one predecessor; no OR; no cross-direction prerequisite; no crossing edges or node intersections; no future gate',controller:'initial states, route gates, search, start, complete and blocked start passed',routes:result,limits:'DOM stub only: no rendered visual or browser interaction verification'},null,2));
