/* Standalone review simulation. It never connects to Unity or writes game saves. */
(() => {
'use strict';
const {nodes,lanes,practice,epoch}=SerenityResearchProposal;
const $=id=>document.getElementById(id), byId=new Map(nodes.map(n=>[n.id,n]));
const category=new Map(lanes.map(l=>[l.id,l]));
const W=1340,H=810,CW=42,CH=42;
const labels={completed:'Изучено',available:'Доступно',researching:'Исследуется',locked:'Закрыто',practice:'Нужна практика',future:'Будущая механика'};
const resources={wood:'древесина',stone:'камень'};
let selected='woodworking',filter='all',query='',done=new Set(),active=null,progress=0;
let facts={},stock={},scale=1,offset={x:0,y:0},drag=null,moved=false,toastTimer;
const elms=new Map();
const future=n=>['PLANNED','PROPOSED'].includes(n.status);
const icon=name=>`<svg class="icon" aria-hidden="true"><use href="#i-${name}"/></svg>`;
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function nodeIcon(n){if(n.icon)return n.icon;return n.id==='basic_survival'?'fire':n.lane==='epoch'?'crown':/fire|cooking/.test(n.id)?'fire':/fish/.test(n.id)?'fish':/shelter|housing/.test(n.id)?'house':/herbal|wound|anatomy/.test(n.id)?'cross':/record|knowledge|philosophy/.test(n.id)?'book':category.get(n.lane)?.icon||'book'}
function position(n){if(n.lane==='root')return {x:649,y:6};const center=132+lanes.findIndex(l=>l.id===n.lane)*260;return n.hub?{x:center-44,y:94}:{x:center+n.dx-21,y:232+(n.level-1)*100}}
function prereqs(n){return n.all.every(id=>done.has(id))&&(!n.any.length||n.any.some(id=>done.has(id)))}
function practical(n){return n.practice.every(k=>(facts[k]||0)>=practice[k].goal)}
function affordable(n){return Object.entries(n.cost).every(([r,c])=>(stock[r]||0)>=c)}
function epochProgress(){
 const learned=nodes.filter(n=>!['root','epoch'].includes(n.lane)&&n.id!=='gathering'&&done.has(n.id)&&!future(n));
 const developed=lanes.filter(l=>learned.filter(n=>n.lane===l.id).length>=epoch.perLane);
 const achievements=lanes.filter(l=>learned.some(n=>n.lane===l.id&&n.key));
 const base=epoch.foundations.filter(id=>done.has(id)).length;
 const practicalCount=epoch.practice.filter(k=>facts[k]>=practice[k].goal).length;
 const pieces=[Math.min(1,learned.length/epoch.count),Math.min(1,developed.length/epoch.lanes),Math.min(1,achievements.length/epoch.keys),base/epoch.foundations.length,practicalCount/epoch.practice.length];
 return {learned,developed,achievements,base,practicalCount,ready:pieces.every(x=>x===1),percent:Math.round(pieces.reduce((a,b)=>a+b,0)/pieces.length*100)};
}
function state(n){
 if(done.has(n.id))return 'completed'; if(active===n.id)return 'researching'; if(future(n))return 'future';
 if(n.lane==='epoch')return epochProgress().ready?'available':'locked';
 if(!prereqs(n))return 'locked'; return practical(n)&&affordable(n)?'available':'practice';
}
function notify(s){$('toast').textContent=s;$('toast').classList.add('show');clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').classList.remove('show'),3500)}
function setScenario(which){
 facts={shelter:1,food:24,hides:0,herbs:6,soldiers:0,population:10};stock={wood:30,stone:20};
 done=new Set(['basic_survival','gathering']);active=null;progress=0;
 const base=['stone_tools','controlled_fire','shelter_construction','woodworking','organized_labor','advanced_stonecraft'];
 const sets={
 early:['stone_tools','controlled_fire','shelter_construction','observation','plant_lore','kinship'],
 peaceful:[...base,'clay_working','hide_working','fishing','cooking','food_preservation','reliable_housing','seasonal_preparation','kinship','settlement_council','spiritual_tradition','shared_storage'],
 scholar:[...base,'observation','plant_lore','animal_lore','herbalism','wound_care','season_calendar','record_keeping','knowledge_transfer','natural_philosophy','hunting','fishing'],
 military:[...base,'primitive_weapons','militia','territory_guard','formation_training','throwing_weapons','military_organization','fortification','hunting','cooking','food_preservation'],
 mixed:[...base,'fishing','cooking','food_preservation','observation','plant_lore','herbalism','wound_care','season_calendar']
 };
 function include(id){const n=byId.get(id);if(future(n))return;for(const p of n.all)include(p);done.add(id)}
 for(const id of sets[which]||[])include(id);
 if(which==='start'){facts={shelter:0,food:12,hides:0,herbs:0,soldiers:0,population:10};}
 else if(which==='early'){active='woodworking';progress=42;}
 else{facts={shelter:3,food:64,hides:4,herbs:12,soldiers:which==='military'?6:0,population:14};}
 $('scenario').value=which; render();
}
function choose(id,focus=false){selected=id;render();if(focus)centerOn(id)}
function createNodes(){
 for(const n of nodes){
  if(n.lane==='epoch')continue;
  const p=position(n),e=document.createElement('button');e.type='button';e.dataset.id=n.id;e.className='node';
  e.style.left=p.x+'px';e.style.top=p.y+'px';e.style.setProperty('--lane',category.get(n.lane)?.color||'#cfb77d');
  if(n.lane==='root')e.classList.add('root');if(n.hub)e.classList.add('hub');
  e.innerHTML=`${icon(nodeIcon(n))}<span><span class="label">${esc(n.hub?category.get(n.lane).name:n.name)}</span><span class="meta"></span></span>${n.key?'<span class="star" title="Ключевое достижение направления">✦</span>':''}`;
  e.addEventListener('click',()=>{if(!moved)choose(n.id)});$('nodes').append(e);elms.set(n.id,e);
 }
 $('laneHeaders').innerHTML=lanes.map((l,i)=>`<div class="constellation" style="left:${i*260}px;--lane:${l.color}"><span>${l.subtitle}</span></div>`).join('');
 $('filters').innerHTML=`<button data-filter="all">Все направления</button>`+lanes.map(l=>`<button data-filter="${l.id}" title="Подсветить направление; остальные связи остаются на схеме">${l.name}</button>`).join('');
 $('filters').querySelectorAll('button').forEach(b=>b.onclick=()=>{filter=b.dataset.filter;render()});
}
function drawEdges(){
 let paths='';
 for(const n of nodes){
  if(n.lane==='epoch'||n.lane==='root')continue;
  const child=position(n),cs=n.hub?88:42;
  for(const id of n.all){
   const p=byId.get(id),parent=position(p),ps=p.lane==='root'?42:p.hub?88:42;
   const ax=parent.x+ps/2,ay=parent.y+ps/2,bx=child.x+cs/2,by=child.y+cs/2;
   const distance=Math.hypot(bx-ax,by-ay),ux=(bx-ax)/distance,uy=(by-ay)/distance;
   const sx=ax+ux*(ps/2+5),sy=ay+uy*(ps/2+5),ex=bx-ux*(cs/2+5),ey=by-uy*(cs/2+5);
   const hi=selected===id||selected===n.id;
   let opacity=hi?1:done.has(id)?.72:.23;
   if(filter!=='all'&&n.lane!==filter&&!hi)opacity=.07;
   paths+=`<path d="M${sx} ${sy} L${ex} ${ey}" stroke="${hi?'#f2d997':done.has(id)?'#bcae7e':'#81909a'}" opacity="${opacity}" class="${hi?'highlight':''}" data-from="${id}" data-to="${n.id}"/>`;
  }
 }
 $('edges').innerHTML=paths;
}

function render(){
 let matches=0;
 for(const n of nodes){
  if(n.lane==='epoch')continue;
  const e=elms.get(n.id),s=state(n),match=(!query||n.name.toLocaleLowerCase('ru').includes(query))&&(filter==='all'||n.lane===filter||n.lane==='root');
  if(match)matches++;
  const related=selected===n.id||byId.get(selected).all.includes(n.id)||byId.get(selected).any.includes(n.id)||n.all.includes(selected)||n.any.includes(selected);
  e.className=`node ${n.lane==='root'?'root':''} ${n.hub?'hub':''} ${s} ${selected===n.id?'selected':''} ${!match?'dim':''} ${related&&!query?'related':''}`;
  e.querySelector('.meta').textContent=(s==='completed'?'✓ ':s==='locked'?'◇ ':'')+labels[s]+(n.any.length?' · ИЛИ':'');
  e.title=n.name+' — '+labels[s]+(n.key?' · ключевое достижение':'');e.setAttribute('aria-label',e.title);e.setAttribute('aria-pressed',String(selected===n.id));
 }
 $('searchCount').textContent=query?matches:'';$('noResults').hidden=!query||matches>0;
 $('filters').querySelectorAll('button').forEach(b=>{b.classList.toggle('selected',b.dataset.filter===filter);b.setAttribute('aria-pressed',String(b.dataset.filter===filter))});
 $('activeName').textContent=active?byId.get(active).name:'Выберите новое направление';
 $('activeBar').style.width=(active?progress:0)+'%';
 $('activeMeta').textContent=active?`${progress}% · 4 исследователя · демонстрация`:'Группа исследователей распределяется автоматически';
 drawEdges();renderEpoch();renderDetail();
 const ep=epochProgress();$('routeSummary').textContent=`${ep.learned.length} открытий · ${ep.developed.length}/3 развитых направлений · ${ep.ready?'условия эпохи выполнены':'можно проверить зависимости'}`;
}
function renderEpoch(){
 const e=epochProgress(),complete=done.has('stone_age_mastery');
 $('epochCard').innerHTML=`${icon('crown')}<div><span class="eyebrow">РУБЕЖ ЭПОХИ</span><h2>Освоение каменного века</h2></div><div class="epochStats"><span><b>${e.learned.length}/14</b> открытий</span><span><b>${e.developed.length}/3</b> направления</span><span><b>${e.achievements.length}/3</b> достижения</span></div><button class="epochAction">${complete?'✓ Освоено':e.ready?'Готово к освоению →':'Все условия →'}</button>`;
 $('epochCard').querySelector('button').onclick=()=>choose('stone_age_mastery');
}

function reqLine(text,met){return `<div class="requirement"><span class="${met?'check':'missing'}">${met?'✓':'○'}</span><span>${esc(text)}</span></div>`}
function reqNode(id){return `<div class="requirement"><span class="${done.has(id)?'check':'missing'}">${done.has(id)?'✓':'○'}</span><button class="prereqButton" data-select="${id}">${esc(byId.get(id).name)} ↗</button></div>`}
function researchRate(){return (4+(done.has('record_keeping')?1:0)+(done.has('knowledge_transfer')?1:0))*1.1*(1+(done.has('observation')?.05:0)+(done.has('season_calendar')?.05:0)+(done.has('natural_philosophy')?.1:0))}
function renderDetail(){
 const n=byId.get(selected),s=state(n),ep=n.lane==='epoch';
 let body=`<div class="detailIcon">${icon(nodeIcon(n))}</div><div class="eyebrow">${ep?'РУБЕЖ ЭПОХИ':category.get(n.lane)?.name||'ОБЩИЙ КОРЕНЬ'}</div><h2>${esc(n.name)}</h2><span class="state">${labels[s]}</span><h3>Что это даёт</h3><p>${esc(n.effect)}</p>${n.description?`<p class="note">${esc(n.description)}</p>`:""}`;
 if(n.key)body+='<p class="note">✦ Ключевое достижение направления. Учитывается при завершении эпохи.</p>';
 if(ep){
  const e=epochProgress();body+=`<h3>Все условия вместе</h3>`+reqLine(`14 технологий: ${e.learned.length}/14`,e.learned.length>=14)+reqLine(`По 3 технологии в 3 направлениях: ${e.developed.length}/3`,e.developed.length>=3)+reqLine(`Ключевые достижения в 3 направлениях: ${e.achievements.length}/3`,e.achievements.length>=3);
  body+='<h3>Обязательные основы</h3>'+epoch.foundations.map(reqNode).join('');
  body+='<h3>Практика поселения</h3>'+epoch.practice.map(k=>reqLine(practice[k].name+` · ${facts[k]}/${practice[k].goal}`,facts[k]>=practice[k].goal)).join('');
  body+='<p class="note">Стартовые знания и будущие механики не считаются. Военная ветка необязательна; условия достижимы через мирное развитие.</p><p class="futureNote">Следующая историческая эпоха ещё не реализована (U38). Этот рубеж завершает каменный век и сохраняет доступ к его дереву; новый век не запускается.</p>';
 }else{
  body+='<h3>Зависимости</h3>';
  if(!n.all.length&&!n.any.length)body+='<p class="note">Независимое исследование. Предшественники не нужны.</p>';
  else{if(n.all.length)body+='<p class="note">Сначала изучите:</p>'+n.all.map(reqNode).join('');if(n.any.length)body+='<div class="orGroup"><small>И ЛЮБОЕ ОДНО ИЗ:</small>'+n.any.map(reqNode).join('')+'</div>';}
  if(n.practice.length)body+='<h3>Практические условия</h3>'+n.practice.map(k=>reqLine(practice[k].name+` · ${facts[k]}/${practice[k].goal}`,facts[k]>=practice[k].goal)).join('');
 }
 if(n.days){
  const level=n.days<=2?'Начальное':n.days<=3?'Базовое':n.days<=5?'Развитое':ep?'Эпохальное':'Ключевое';
  body+=`<h3>Стоимость · ${level}</h3><div class="metrics"><div class="metric"><b>${n.days} чел.-дн.</b><span>Работа исследователей</span></div><div class="metric"><b>≈ ${Math.ceil(n.days*24/researchRate())} мин</b><span>Нормальная скорость ×1</span></div></div><p class="note">Оценка при полностью занятой группе и навыке ×1,1. Сон, здоровье и другие дела увеличат время.</p>`;
  if(Object.keys(n.cost).length)body+=Object.entries(n.cost).map(([r,c])=>reqLine(`${resources[r]}: ${c} (в демо ${stock[r]})`,stock[r]>=c)).join('')+'<p class="note">Материалы списываются один раз при старте.</p>';
 }
 if(n.future)body+=`<h3>Будущие возможности</h3><p class="futureNote">${esc(n.future)}</p>`;
 if(s==='researching')body+=`<div class="progress"><i style="width:${progress}%"></i></div><p class="note">${progress}% · ускоренная проверка макета</p><button class="primary" id="finishDemo">Завершить в демо</button>`;
 else if(!['completed','future'].includes(s))body+=`<button class="primary" id="startDemo" ${s!=='available'||active?'disabled':''}>${active?'Дождитесь текущего исследования':s==='available'?'Начать в демо':s==='practice'?'Не выполнены условия':'Сначала выполните зависимости'}</button>`;
 else if(s==='future')body+='<p class="note">Эту технологию нельзя исследовать до появления указанной механики. Она не требуется для эпохи.</p>';
 const children=nodes.filter(c=>c.all.includes(n.id)||c.any.includes(n.id));
 if(children.length)body+='<h3>Следующие возможности</h3>'+children.map(c=>`<div class="requirement"><button class="prereqButton" data-select="${c.id}">${esc(c.name)} ↗${c.any.includes(n.id)?' · один из путей':''}</button></div>`).join('');
 const statuses={IMPLEMENTED:'IMPLEMENTED · действующий эффект подтверждён',PARTIAL:'PARTIAL · существующая механика, привязка или часть эффекта требует реализации',PLANNED:'PLANNED · механика есть в планах проекта',PROPOSED:'PROPOSED · предложение, механика ещё не утверждена'};
 body+=`<details><summary>Интеграция с текущей игрой</summary><p>${statuses[n.status]}</p><p>${esc(n.hook||n.future||'Существующее стартовое знание.')}</p><p>Идентификатор: <code>${n.id}</code>. Это проект схемы, а не отчёт о внесённых изменениях.</p></details>`;
 const scroll=$('detail').scrollTop;$('detail').innerHTML=body;
 if($('detail').dataset.selected!==selected){$('detail').scrollTop=0;$('detail').dataset.selected=selected;}else $('detail').scrollTop=scroll;
 $('detail').querySelectorAll('[data-select]').forEach(b=>b.onclick=()=>choose(b.dataset.select,true));
 if($('startDemo'))$('startDemo').onclick=()=>{if(state(n)!=='available'||active)return;for(const [r,c]of Object.entries(n.cost))stock[r]-=c;active=n.id;progress=1;render();notify('Исследование запущено только в макете')};
 if($('finishDemo'))$('finishDemo').onclick=()=>{done.add(active);active=null;progress=0;render();notify(ep?'Каменный век освоен в демонстрации':'Изучено. Зависимые технологии обновлены')};
}
function transform(){ $('world').style.transform=`translate(${offset.x}px,${offset.y}px) scale(${scale})`;$('zoomText').textContent=Math.round(scale*100)+'%' }
function fit(){const r=$('viewport').getBoundingClientRect();scale=Math.min((r.width-24)/W,(r.height-16)/H,1.25);offset={x:(r.width-W*scale)/2,y:(r.height-H*scale)/2};transform()}
function zoom(next,x,y){const r=$('viewport').getBoundingClientRect();x??=r.width/2;y??=r.height/2;next=Math.max(.4,Math.min(2,next));offset={x:x-(x-offset.x)*next/scale,y:y-(y-offset.y)*next/scale};scale=next;transform()}
function centerOn(id){const r=$('viewport').getBoundingClientRect(),n=byId.get(id),p=n.lane==='epoch'?{x:280,y:738}:position(n);scale=Math.max(1,scale);offset={x:r.width/2-(p.x+(n.lane==='epoch'?390:n.hub?44:21))*scale,y:r.height/2-(p.y+(n.lane==='epoch'?30:n.hub?44:21))*scale};transform()}
const vp=$('viewport');
vp.addEventListener('wheel',e=>{e.preventDefault();e.stopPropagation();const r=vp.getBoundingClientRect();zoom(scale*Math.exp(-e.deltaY*.0015),e.clientX-r.left,e.clientY-r.top)},{passive:false});
vp.addEventListener('pointerdown',e=>{moved=false;if(e.button!==0||e.target.closest('button'))return;drag={x:e.clientX,y:e.clientY,ox:offset.x,oy:offset.y};vp.setPointerCapture(e.pointerId);vp.classList.add('dragging')});
vp.addEventListener('pointermove',e=>{if(!drag)return;const dx=e.clientX-drag.x,dy=e.clientY-drag.y;if(Math.abs(dx)+Math.abs(dy)>3)moved=true;offset={x:drag.ox+dx,y:drag.oy+dy};transform()});
function release(){drag=null;vp.classList.remove('dragging');setTimeout(()=>moved=false,0)}vp.addEventListener('pointerup',release);vp.addEventListener('pointercancel',release);
let fitted=true;$('fit').onclick=()=>{fitted=true;fit()};$('center').onclick=()=>{fitted=false;centerOn(selected)};$('plus').onclick=()=>{fitted=false;zoom(scale*1.2)};$('minus').onclick=()=>{fitted=false;zoom(scale/1.2)};
vp.addEventListener('wheel',()=>fitted=false);vp.addEventListener('pointermove',()=>{if(drag)fitted=false});
window.addEventListener('resize',()=>{if(fitted)fit()});
$('search').addEventListener('input',e=>{query=e.target.value.trim().toLocaleLowerCase('ru');render()});
$('search').addEventListener('keydown',e=>{if(e.key==='Enter'){const n=nodes.find(n=>n.lane!=='epoch'&&n.name.toLocaleLowerCase('ru').includes(query)&&(filter==='all'||n.lane===filter));if(n)choose(n.id,true)}});
$('scenario').onchange=e=>setScenario(e.target.value);$('reset').onclick=()=>{filter='all';query='';$('search').value='';selected='woodworking';setScenario('early');fitted=true;fit()};
$('conditionsOpen').onclick=()=>{
 $('conditionFields').innerHTML=Object.entries(practice).map(([k,v])=>`<label>${esc(v.name)}<input type="number" min="0" max="100000" step="1" value="${facts[k]}" data-fact="${k}"></label>`).join('')+Object.entries(resources).map(([k,v])=>`<label>Материал: ${v}<input type="number" min="0" max="100000" step="1" value="${stock[k]}" data-stock="${k}"></label>`).join('');$('conditions').showModal();
};
$('conditionsApply').onclick=()=>{const value=e=>Math.max(0,Math.min(100000,Math.floor(Number(e.value)||0)));$('conditions').querySelectorAll('[data-fact]').forEach(e=>facts[e.dataset.fact]=value(e));$('conditions').querySelectorAll('[data-stock]').forEach(e=>stock[e.dataset.stock]=value(e));$('conditions').close();render()};
$('aboutOpen').onclick=()=>$('about').showModal();document.querySelectorAll('[data-close]').forEach(b=>b.onclick=()=>$(b.dataset.close).close());
$('aboutContent').innerHTML=`<p>Новая схема по референсу Anno: пять крупных круглых узлов направлений, небольшие открытия и короткие связи сверху вниз. 51 технология. Игра не изменена.</p><h3>Одно правило для всех связей</h3><p>Линия означает: сначала изучите верхнее открытие, затем станет доступно нижнее. У каждого открытия один предшественник. Никаких «ИЛИ», скрытых обязательных связей или требований из соседней ветки.</p><p>Развилки сохраняют выбор: от одной технологии может идти несколько разных продолжений. Изучение одного не закрывает другие.</p><h3>Что упрощено в правилах</h3><p>Приготовление пищи зависит только от огня. Для рыбного лагеря нужны простые укрытия, для земледелия — постоянное жильё. Военная организация требует строевой подготовки; ремесленные и военные цепочки больше не пересекаются. Это изменения предлагаемой логики, а не сокрытие линий.</p><h3>Как читать карту</h3><p>Наведите на круг, чтобы увидеть название. Нажмите — полное описание появится справа. Золотой контур означает доступное открытие, зелёный — изученное, голубой — текущее, тёмный — закрытое. Серые узлы с меткой ◇ ждут будущих механик. ✦ — ключевое достижение.</p><h3>Завершение эпохи</h3><p>14 открытий, по 3 исследования в любых 3 направлениях, ключевые достижения в 3 направлениях, базовые знания и практические условия поселения. Военная ветка необязательна. Рубеж не связан линиями со всем деревом; оставшиеся открытия доступны после него.</p><h3>Стоимость и интеграция</h3><p>Стоимость, практические требования и статусы интеграции остаются предложением для согласования. Расчёт основан на существующем времени игры: сутки за 24 реальные минуты. Навыки и ограниченный вклад группы влияют на длительность; это ещё не замер прохождения. Будущие механики не блокируют эпоху. Реализация в Unity начнётся только после одобрения.</p>`;
createNodes();setScenario('early');requestAnimationFrame(fit);
// Read-only inspection surface for focused prototype verification.
globalThis.researchReview={snapshot:()=>({selected,filter,scale,done:[...done],active,facts:{...facts},stock:{...stock},epoch:epochProgress(),states:Object.fromEntries(nodes.map(n=>[n.id,state(n)]))}),catalog:SerenityResearchProposal};
})();

