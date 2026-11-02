/* Reviewed catalog, UI proposal only. No game state is read or written. */
const GROUPS=[['food','Еда','food'],['materials','Материалы','stone'],['craft','Ремесло','tools'],['equipment','Снаряжение','shield'],['health','Медицина','leaf'],['farm','Хозяйство','grain']];
const RESOURCE_ROWS=[
['cooked_meat','Жареное мясо','food','meal','Все жители','3 порции на каждого в день этого блюда. Второй день цикла. Два блюда из 2 мяса и 1 бревна.'],
['cooked_fish','Жареная рыба','food','meal','Все жители','3 порции на каждого в день этого блюда. Первый день цикла. Два блюда из 2 рыбы и 1 бревна.'],
['cooked_berries','Приготовленные ягоды','food','meal','Все жители','3 порции на каждого в назначенные дни. Три блюда из 3 ягод и 1 бревна.'],
['dried_meat','Вяленое мясо','food','meal','Все жители и снабжение','3 порции на каждого в назначенные дни. Две порции из 3 мяса. Отряды не считаются второй раз, если их люди уже в населении.'],
['meat','Сырое мясо','food','chain','Костры, кухни, сушилки','Сырьё только под недостающие жареные и вяленые блюда. Не считается готовой едой.'],
['fish','Сырая рыба','food','chain','Костры и кухни','Сырьё под недостающую жареную рыбу; запас готовых блюд уже вычтен.'],
['berries','Ягоды','food','chain','Готовка; резервный корм','Сырьё для ягодных блюд. Корм добавляется только при выбранной замене концентрата, не одновременно.'],
['carcass','Туша животного','food','chain','Охотники и разделка','Одна туша на недостающую освежёванную. Короткий оборот, а не запас туш на каждого жителя.'],
['dressed_carcass','Освежёванная туша','food','chain','Мясники','Одна туша даёт 5 мяса и 3 кости. Учитывать уже доставленные туши и годные туши в обработке.'],
['field_dressed_carcass','Разделанная в поле туша','food','zero','Мясники','Цель 0: использовать наличные как замену по выходу 3 мяса и 1 кость, не заказывать параллельно обычную тушу.'],
['carrot','Морковь','food','zero','Будущие блюда / ручной заказ','Автоцель 0 до подключения действующего рецепта питания. Категория Food ещё не означает съедобность.'],
['cabbage','Капуста','food','zero','Будущие блюда / ручной заказ','Автоцель 0 до подключения действующего рецепта; ручная цель остаётся доступна.'],
['mushrooms','Грибы','food','zero','Будущие блюда / ручной заказ','Автоцель 0: сейчас нет блюда в списке потребляемой пищи.'],
['milk','Молоко','food','zero','Продукт животноводства','Автоцель 0 без подтверждённого потребителя. Не увеличивать стадо ради заполнения произвольной нормы.'],
['eggs','Яйца','food','zero','Продукт птицеводства','Автоцель 0 без подключённого рецепта. Учитывать в запасах, но не в днях готовой еды.'],
['wood_log','Брёвна','materials','chain','Стройки, топливо, мастерские','Остаток смет подтверждённых строек + расход выбранных рецептов. Топливо готовки входит через рецепты, не второй раз.'],
['stone','Камень','materials','chain','Стройки, копья, стрелы','Невыполненные сметы + входы рецептов. Нет нормы «камень × каждый житель».'],
['branches','Ветки','materials','zero','Заказанные работы','Автоцель 0 без конкретной сметы или рецепта. Ручной стратегический запас разрешён.'],
['clay','Глина','materials','zero','Подключённые стройки / ремесло','Только фактические входы открытых работ. В демонстрационном плане их нет: цель 0.'],
['flint','Кремень','materials','zero','Подключённые рецепты','Цель 0 без потребителя. Не подменяет камень автоматически.'],
['iron_ore','Железная руда','materials','zero','Будущая металлургия','Без работающей цепочки металл → предмет цель 0; не запасать на каждого военного.'],
['copper_ore','Медная руда','materials','zero','Будущая металлургия','Цель 0 без действующего потребителя.'],
['gold_ore','Золотая руда','materials','zero','Будущие изделия / торговля','Цель 0 без конкретного заказа; ручной резерв остаётся.'],
['oil','Нефть','materials','zero','Будущие производства','Автоцель 0 в текущем каменном веке.'],
['plant_fiber','Растительное волокно','craft','chain','Стройки, одежда, луки, корм','Сумма входов утверждённого плана; резервный корм не дублирует концентрат.'],
['raw_hide','Сырая шкура','craft','chain','Одежда и выделка','2 на одежду, 1 на выделанную шкуру. Побочный выход разделки туш вычитается из дополнительной потребности.'],
['cured_hide','Выделанная шкура','craft','chain','Щиты','1 на недостающий щит. Учитывать склад и производство до заказа выделки.'],
['hide','Шкура','craft','zero','Сметы строек','Только реальные сметы с этим ID; не считать взаимозаменяемой с сырой/выделанной без рецепта.'],
['bone','Кости','craft','zero','Побочный продукт разделки','Цель 0 до появления заказа. Не охотиться специально ради бесконечного накопления костей.'],
['feathers','Перья','craft','chain','Мастерская стрел','1 перо на партию из 6 стрел. Существующие перья учитываются.'],
['wool','Шерсть','craft','zero','Будущее ткачество','Цель 0 без действующего текстильного заказа.'],
['combat_bow','Лук охотника','equipment','gear','Только охотники — предложение','1 на активного охотника + запас, минус исправные выданные луки. В каталоге это combat_bow, сейчас боевой: привязку к охоте нужно отдельно внедрить.'],
['stone_spear','Каменное копьё','equipment','gear','Военные с копьём','1 на копейщика + резерв, минус выданные. В будущем не выдавать одновременно меч и копьё одному слоту.'],
['wooden_shield','Деревянный щит','equipment','gear','Военные со щитом','1 на назначенный щитовой комплект + резерв, минус исправные выданные щиты.'],
['arrows','Стрелы','equipment','ammo','Охотники с луком','Предложение: 20 на охотника + запас, минус исправные стрелы в колчанах. Позже норму заменить наблюдаемым расходом.'],
['leather_clothing','Кожаная одежда','equipment','gear','Жители без подходящей одежды','1 комплект на жителя + резерв, минус годные надетые комплекты; сезонность и размеры требуют отдельного правила.'],
['medicine','Лекарство','health','care','Больные и небольшой общий резерв','Предложение: 2 единицы на текущий случай + резерв 1 на 10 жителей, затем надбавка. Курс и расход пока не подтверждены системой лечения.'],
['medicinal_plants','Лекарственные растения','health','chain','Изготовление лекарств','2 растения на недостающее лекарство; прямой лечебный расход добавлять только при реальном потребителе.'],
['animal_feed','Корм для животных','farm','feed','Стадо, не жители','Сумма округлений по загонам: ceil(животных / 3) × дни, затем надбавка. Одна единица концентрата кормит до трёх животных.'],
['wheat','Пшеница','farm','chain','Корм; заказанные рецепты','1 на партию из 6 концентрата. Запас на посев — только после появления реального расхода семян.'],
['spoiled_food','Испорченная еда','farm','zero','Утилизация','Цель всегда 0 в автоматике. Показывать отдельно от еды, предлагать очистку; не включать в запас рациона.']
];
const RECIPES=[
['cooked_meat',2,{meat:2,wood_log:1}],['cooked_fish',2,{fish:2,wood_log:1}],['cooked_berries',3,{berries:3,wood_log:1}],['dried_meat',2,{meat:3}],
['combat_bow',1,{wood_log:2,plant_fiber:2}],['stone_spear',1,{wood_log:1,stone:1,plant_fiber:1}],['wooden_shield',1,{wood_log:3,cured_hide:1}],['arrows',6,{wood_log:1,stone:1,feathers:1}],['leather_clothing',1,{raw_hide:2,plant_fiber:2}],
['medicine',1,{medicinal_plants:2}],['animal_feed',6,{wheat:1,plant_fiber:1}],['cured_hide',1,{raw_hide:1,plant_fiber:1}],['meat',5,{dressed_carcass:1}],['dressed_carcass',1,{carcass:1}]
];
const INITIAL_STOCK={cooked_meat:34,cooked_fish:18,cooked_berries:22,dried_meat:8,meat:12,fish:6,berries:36,wood_log:140,stone:95,plant_fiber:24,combat_bow:1,stone_spear:2,wooden_shield:1,arrows:18,leather_clothing:3,medicine:2,medicinal_plants:7,animal_feed:4,wheat:12,raw_hide:2,cured_hide:1,dressed_carcass:1,field_dressed_carcass:1,carcass:2,feathers:9,bone:15,carrot:20,cabbage:8,mushrooms:3,milk:4,eggs:6,branches:24,clay:14,flint:6,hide:4,wool:2};
const MEALS=['cooked_fish','cooked_meat','cooked_berries','dried_meat'];
const DEMO={population:48,hunters:4,soldiers:8,sick:2,animals:12,days:30,stockDays:3,buffer:25,capacity:2000,buildingWood:50,buildingStone:30,buildingFiber:10,opened:[...MEALS],menu:[],equipped:{combat_bow:3,stone_spear:6,wooden_shield:5,arrows:40,leather_clothing:43}};
function monthMenu(s){return Array.from({length:s.days},(_,day)=>s.opened.includes(s.menu[day])?s.menu[day]:s.opened[day%s.opened.length]||null);}
function planWindow(s,stock,policies,days,monthly=false){
 const target=Object.fromEntries(RESOURCE_ROWS.map(r=>[r[0],0])),why={},extra={};
 const margin=id=>policies[id]?.margin??s.buffer;
 const set=(id,base,desc)=>{target[id]=Math.ceil(base*(1+margin(id)/100));why[id]=desc;};
 const portions={};for(const id of monthMenu(s).slice(0,days))if(id)portions[id]=(portions[id]||0)+s.population*3;
 for(const [id,n] of Object.entries(portions)){if(monthly)target[id]=n;else set(id,n,'');why[id]=`${s.population} жителей × 3 порции × ${n/(s.population*3)||0} дн. этого блюда`;
 }
 for(const [id,n] of Object.entries({combat_bow:s.hunters,stone_spear:s.soldiers,wooden_shield:s.soldiers,arrows:s.hunters*20,leather_clothing:s.population})){
 set(id,n,`${n} нужно по роли / комплекту; выдано ${s.equipped[id]||0}`);target[id]=Math.max(0,target[id]-(s.equipped[id]||0));}
 set('medicine',s.sick*2+Math.ceil(s.population/10),`${s.sick} случая × 2 + ceil(${s.population}/10) резерв`);
 if(monthly){target.animal_feed=Math.ceil(s.animals/3)*days;why.animal_feed='Полный месячный расход концентрата без складской надбавки';}
 else set('animal_feed',Math.ceil(s.animals/3)*days,`Один демонстрационный загон: ceil(${s.animals}/3) × ${days} дн.`);
 target.wood_log=s.buildingWood;target.stone=s.buildingStone;target.plant_fiber=s.buildingFiber;
 for(const id of ['wood_log','stone','plant_fiber'])why[id]='Остаток подтверждённых смет + входы недостающих изделий и блюд';
 const manual=id=>!monthly&&policies[id]?.mode==='manual';
 const have={...stock}; // Available stock, not world nodes or enemy property.
 const effective=id=>manual(id)?policies[id].target:target[id];
 const expanded=new Set();
 for(const [id,yieldCount,inputs] of RECIPES){
  if(MEALS.includes(id)&&!s.opened.includes(id))continue;
  if(!monthly&&RESOURCE_ROWS.find(r=>r[0]===id)[3]==='chain'&&policies[id]?.margin!=null)target[id]=Math.ceil(target[id]*(1+policies[id].margin/100));
  expanded.add(id);
  let missing=Math.max(0,effective(id)-(have[id]||0));
  if(id==='meat'){const field=Math.min(have.field_dressed_carcass||0,Math.ceil(missing/3));missing=Math.max(0,missing-field*3);extra.bone=(extra.bone||0)+field;}
  const batches=Math.ceil(missing/yieldCount);for(const [input,units] of Object.entries(inputs)){target[input]+=batches*units;why[input]=why[input]||'Входы рецептов под дефицит конечных целей; готовый складской запас уже учтён';}
  if(id==='dressed_carcass')extra.raw_hide=(extra.raw_hide||0)+batches;
  if(id==='meat')extra.bone=(extra.bone||0)+batches*3;
 }
 // Planned co-products cover the same chain demand; never request the same hide twice.
 target.raw_hide=Math.max(0,target.raw_hide-(extra.raw_hide||0));
 const constraints=[];
 for(const r of RESOURCE_ROWS){const id=r[0];if(!monthly&&!expanded.has(id)&&['chain','zero'].includes(r[3])&&policies[id]?.margin!=null)target[id]=Math.ceil(target[id]*(1+policies[id].margin/100));if(manual(id)){if(policies[id].target<target[id])constraints.push({id,manual:policies[id].target,needed:target[id]});target[id]=policies[id].target;}why[id]??=r[5];}
 return {target,why,extra,constraints,total:Object.values(target).reduce((a,b)=>a+b,0)};
}
function calculate(s,stock,policies){const operational=planWindow(s,stock,policies,Math.min(s.stockDays,s.days));const month=planWindow(s,{},policies,s.days,true);return {...operational,month:month.target,menu:monthMenu(s),noRecipes:!s.opened.length};}
if(typeof module!=='undefined')module.exports={GROUPS,RESOURCE_ROWS,RECIPES,INITIAL_STOCK,DEMO,MEALS,calculate,monthMenu};
