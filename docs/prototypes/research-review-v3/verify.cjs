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
const visited=new Set(),visiting=new Set(),occupied=new Set();
function walk(n){
 if(visited.has(n.id))return;assert.ok(!visiting.has(n.id),'cycle '+n.id);visiting.add(n.id);
 for(const id of [...n.all,...n.any]){assert.ok(map.has(id),'missing '+id);walk(map.get(id));}
 visiting.delete(n.id);visited.add(n.id);
}
const future=n=>['PLANNED','PROPOSED'].includes(n.status);
for(const n of nodes){
 walk(n);
 if(lanes.some(l=>l.id===n.lane)){
  const cell=[n.lane,n.col,n.row].join(':');assert.ok(!occupied.has(cell),'overlap '+cell);occupied.add(cell);
  assert.ok(n.col>=0&&n.col<=5&&n.row>=0&&n.row<=1);
 }
 for(const id of [...n.all,...n.any]){
  const p=map.get(id);assert.ok(p.col<n.col,`not left-to-right: ${id} -> ${n.id}`);
 }
 if(!future(n)){
  assert.ok(n.all.every(id=>!future(map.get(id))),'mandatory future '+n.id);
  assert.ok(!n.any.length||n.any.some(id=>!future(map.get(id))),'future-only OR '+n.id);
 }
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
scenario('start');complete('Управляемый огонь');complete('Рыболовство');
assert.equal(review.snapshot().states.cooking,'available','fishing OR must be sufficient with fire');
assert.ok(!review.snapshot().done.includes('hunting'));complete('Приготовление пищи');
assert.equal(review.snapshot().stock.wood,23,'costs paid exactly once');
complete('Каменные орудия');assert.equal(review.snapshot().states.hide_working,'practice');
scenario('peaceful');get('conditions').querySelectorAll=selector=>selector==='[data-fact]'?[{dataset:{fact:'food'},value:'0'}]:[];
get('conditionsApply').onclick();assert.equal(review.snapshot().epoch.ready,false,'food prerequisite must gate era');
console.log(JSON.stringify({technologies:nodes.length,retainedIds:oldIds.length,lanes:lanes.length,graph:'DAG; valid references; unique cells; forward edges; no mandatory future tech',controller:'initial states, route gates, search, start, complete and blocked start passed',routes:result,limits:'DOM stub only: no rendered visual or browser interaction verification'},null,2));
