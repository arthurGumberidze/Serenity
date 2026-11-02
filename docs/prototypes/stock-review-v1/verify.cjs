// Actual proposal calculator + demo controller. Does not claim browser render verification.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const c=require('./catalog.js'),clone=x=>structuredClone(x),s=clone(c.DEMO);
const resourceYaml=fs.readFileSync(path.resolve(__dirname,'../../..','Assets/Game/Art/Config/ResourceCatalog.asset'),'utf8');
const ids=[...resourceYaml.matchAll(/^  - id: (\w+)/gm)].map(m=>m[1]);
assert.deepEqual(c.RESOURCE_ROWS.map(r=>r[0]).sort(),ids.filter(id=>id!=='water').sort());
assert.equal(new Set(c.RESOURCE_ROWS.map(r=>r[0])).size,41);
assert.equal(c.monthMenu(s)[0],'cooked_fish');assert.equal(c.monthMenu(s)[1],'cooked_meat');
let r=c.calculate(s,c.INITIAL_STOCK,{});
assert.equal(c.MEALS.reduce((a,id)=>a+r.month[id],0),48*3*30);
assert.equal(r.month.cooked_fish,48*3*8);assert.equal(r.month.cooked_meat,48*3*8);
assert.equal(r.target.cooked_fish,180);assert.equal(r.target.dried_meat,0);
s.opened=['cooked_fish'];s.menu=['cooked_meat'];r=c.calculate(s,{},{});
assert(c.monthMenu(s).every(id=>id==='cooked_fish'));assert.equal(r.month.cooked_fish,4320);assert.equal(r.month.cooked_meat,0);
s.opened=[];r=c.calculate(s,{},{});assert(r.noRecipes);assert(c.MEALS.every(id=>r.month[id]===0));
const zero=clone(c.DEMO);zero.population=zero.hunters=zero.soldiers=zero.sick=zero.animals=0;zero.buildingWood=zero.buildingStone=zero.buildingFiber=0;
r=c.calculate(zero,{},{});assert(Object.values(r.target).every(n=>n===0));
const noHunters=clone(c.DEMO);noHunters.hunters=0;r=c.calculate(noHunters,{},{});assert.equal(r.target.combat_bow,0);assert.equal(r.target.arrows,0);assert(r.target.stone_spear>0);
const noMilitary=clone(c.DEMO);noMilitary.soldiers=0;r=c.calculate(noMilitary,{},{});assert.equal(r.target.stone_spear,0);assert.equal(r.target.wooden_shield,0);
const p={wood_log:{mode:'manual',target:10},berries:{mode:'manual',target:40}};r=c.calculate(c.DEMO,{},p);assert.equal(r.target.wood_log,10);assert.equal(r.target.berries,40);assert(r.constraints.some(x=>x.id==='wood_log'));
const large=Object.fromEntries(c.RESOURCE_ROWS.map(row=>[row[0],10000]));r=c.calculate(c.DEMO,large,{});assert.equal(r.target.meat,0);assert.equal(r.target.fish,0);assert.equal(r.target.wood_log,c.DEMO.buildingWood);
// Input multiplier for intermediate cured hides must propagate into its raw inputs.
const noCarcass=clone(c.DEMO);noCarcass.opened=['cooked_fish'];const a=c.calculate(noCarcass,{},{}),b=c.calculate(noCarcass,{}, {cured_hide:{mode:'auto',margin:100}});assert(b.target.raw_hide>a.target.raw_hide);
const big=clone(c.DEMO);big.population=10000;big.hunters=1000;big.soldiers=3000;r=c.calculate(big,{},{});assert(Object.values(r.target).every(n=>Number.isSafeInteger(n)&&n>=0));
// Run actual app with an event-capable DOM stub: only behavior, not CSS geometry.
const elements=new Map(),listeners={};
class Element{constructor(id=''){this.id=id;this.value='';this.checked=true;this.dataset={};this.style={};this.hidden=false;this.innerHTML='';this.classList={add(){},remove(){},toggle(){}};this.attrs={};this.offsetWidth=410;this.offsetHeight=400;}setAttribute(k,v){this.attrs[k]=String(v);}getAttribute(k){return this.attrs[k];}querySelector(){return new Element();}contains(){return false;}matches(){return false;}scrollIntoView(){}insertAdjacentHTML(){}getBoundingClientRect(){return {left:20,right:220,top:70,bottom:100};}}
const get=id=>{if(!elements.has(id))elements.set(id,new Element(id));return elements.get(id);};
const sandbox={console,structuredClone,innerWidth:1920,innerHeight:1080,setTimeout:()=>1,clearTimeout(){},window:{addEventListener(){}},document:{getElementById:get,querySelectorAll:()=>[],querySelector:()=>new Element(),addEventListener:(type,fn)=>listeners[type]=fn}};
vm.createContext(sandbox);vm.runInContext(fs.readFileSync(path.join(__dirname,'catalog.js'),'utf8')+'\n'+fs.readFileSync(path.join(__dirname,'app.js'),'utf8'),sandbox);
assert(get('resource-groups').innerHTML.match(/class="groupchip"/g).length===6);
get('calculate').onclick();assert(!get('preview').hidden);get('apply').onclick();assert(get('preview').hidden);
assert.equal(vm.runInContext('policies.berries.target',sandbox),40);assert.equal(vm.runInContext('policies.cooked_fish.target',sandbox),180);
vm.runInContext('scenario.population=60;updateFromScenario()',sandbox);assert.equal(vm.runInContext('policies.cooked_fish.target',sandbox),225);assert.equal(vm.runInContext('policies.berries.target',sandbox),40);
listeners.change({target:{dataset:{target:'cooked_fish'},value:'123'}});assert.equal(vm.runInContext('policies.cooked_fish.mode',sandbox),'manual');
vm.runInContext('scenario.population=100;updateFromScenario()',sandbox);assert.equal(vm.runInContext('policies.cooked_fish.target',sandbox),123);
get('large-stock').onclick();assert.equal((get('resource-groups').innerHTML.match(/class="groupchip"/g)||[]).length,6);assert(!get('resource-groups').innerHTML.includes('water'));
get('bell').onclick();assert(!get('popover').hidden);listeners.keydown({key:'Escape'});assert(get('popover').hidden);
const html=fs.readFileSync(path.join(__dirname,'index.html'),'utf8');const domIds=[...html.matchAll(/\bid="([^"]+)"/g)].map(m=>m[1]);assert.equal(new Set(domIds).size,domIds.length);
const css=fs.readFileSync(path.join(__dirname,'style.css'),'utf8');assert(css.includes('grid-template-columns:repeat(6,minmax(0,1fr))'));assert(css.includes('position:fixed;z-index:10'));assert(!/\.groupchip[^{}]*\{[^}]*display:none/.test(css));
console.log('PASS: 41 catalog resources, 30-day/3-portion menu, recipe gates, role demand, stock netting, manual locks, dependency margins, large counts, actual demo event flow, bundle inputs. Browser rendering not tested.');
