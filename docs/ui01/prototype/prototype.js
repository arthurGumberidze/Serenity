const app = document.querySelector('.prototype');
const workspace = document.querySelector('#workspace');
const sheet = document.querySelector('#context-sheet');
const ribbon = document.querySelector('#command-ribbon');
const groupMembers = document.querySelector('#group-members');

const A = (label, cls = '', key = '') => `<button class="action ${cls}">${label}${key ? ` <kbd>${key}</kbd>` : ''}</button>`;
const fact = (a,b) => `<div class="fact"><span>${a}</span><b>${b}</b></div>`;
const metric = (name,value,percent,kind='') => `<div class="metric"><span>${name}</span><div class="bar ${kind}"><i style="width:${percent}%"></i></div><b>${value}</b></div>`;
const header = (title, subtitle, actions='') => `<header class="workspace-head"><div><h1>${title}</h1><p>${subtitle}</p></div><div>${actions}</div></header>`;

const residents = {
  doran:{initial:'Д',name:'Доран Серый',age:'31 год',profession:'строитель',now:'Строит каменный дом · 64%',health:'92%',group:'Строители · день'},
  elda:{initial:'Э',name:'Эльда Серая',age:'29 лет',profession:'исследователь',now:'Исследует обработку камня · 62%',health:'100%',group:'Исследователи · день'},
  mira:{initial:'М',name:'Мира Серая',age:'18 лет',profession:'переносчик',now:'Несёт древесину на склад',health:'96%',group:'Переносчики · весь день'},
  neris:{initial:'Н',name:'Нерис Серая',age:'17 лет',profession:'исследователь',now:'Работает за столом знаний',health:'100%',group:'Исследователи · день'},
  tarin:{initial:'Т',name:'Тарин Рыжий',age:'34 года',profession:'строитель',now:'Несёт камень к стройплощадке',health:'88%',group:'Строители · день'},
  rovan:{initial:'Р',name:'Рован Тихий',age:'26 лет',profession:'рабочий',now:'Свободен · ожидает назначения',health:'100%',group:'Без группы'},
  aren:{initial:'А',name:'Арен Следопыт',age:'37 лет',profession:'охотник',now:'Охотится у северного леса',health:'83%',group:'Охотники · утро'},
  kael:{initial:'К',name:'Кейл Серый',age:'16 лет',profession:'ученик',now:'Лечится в доме · ранение',health:'74%',group:'Недоступен'},
  lina:{initial:'Л',name:'Лина Полевая',age:'42 года',profession:'повар',now:'Готовит похлёбку · 3/8',health:'91%',group:'Повара · день'},
  sava:{initial:'С',name:'Сава Младший',age:'14 лет',profession:'помощник',now:'Спит · восстановление 78%',health:'100%',group:'Без группы'}
};
let activeResident = 'doran';
let activeGroup = 'builders';

const quickGroups = {
  builders:{name:'Строители',total:3,members:['doran','tarin','rovan']},
  researchers:{name:'Исследователи',total:2,members:['elda','neris']},
  haulers:{name:'Переносчики',total:2,members:['mira','rovan']},
  hunters:{name:'Охотники',total:1,members:['aren']},
  military:{name:'Военные',total:4,members:['doran','aren','kael','rovan']},
  idle:{name:'Свободные',total:1,members:['rovan']},
  attention:{name:'Требуют внимания',total:1,members:['kael']}
};

function renderGroupMembers(){
  const group=quickGroups[activeGroup];
  groupMembers.innerHTML=`<div class="group-members-head"><span><b>${group.name}</b><small>${group.total} жителей · поиск и фильтры</small></span><button class="close-members" aria-label="Закрыть">×</button></div><label class="member-search">⌕ <input aria-label="Найти жителя в группе" placeholder="Имя, профессия, состояние"></label><div class="member-list">${group.members.map(id=>{const r=residents[id];return `<button class="resident-chip" data-resident="${id}"><span>${r.initial}</span><b>${r.name}</b><small>${r.now}</small><i class="${id==='kael'?'danger':'good'}"></i></button>`}).join('')}</div><div class="member-list-footer"><span>Показано ${group.members.length} из ${group.total}</span><button>Фильтры</button><button>По расстоянию</button></div>`;
}

const residentSheet = () => { const r=residents[activeResident]; return `
  <div class="sheet-head"><div class="identity"><div class="portrait">${r.initial}</div><div><span class="eyebrow">Житель · Тихий Брод</span><h2>${r.name}</h2><p>${r.age} · ${r.profession}</p></div></div><div class="state-line"><span>Сейчас</span><strong>${r.now}</strong></div></div>
  <div class="tabs"><button class="active">Обзор</button><button>Работа</button><button>Семья</button><button>История</button></div>
  <div class="sheet-body"><h3>Состояние</h3>${metric('Здоровье',r.health,parseInt(r.health,10))}${metric('Голод','41%',41)}${metric('Жажда','68%',68,'warn')}${metric('Отдых','76%',76)}
  <h3>Назначение</h3><div class="fact-list">${fact('Профессия',r.profession)}${fact('Группа',r.group)}${fact('Местоположение','Показано камерой')}${fact('Очередь',r.now)}</div>
  <h3>Сильные стороны</h3><span class="tag good">Строительство 7</span><span class="tag">Сила 6</span><span class="tag">Выносливость 5</span>
  <h3>Семья</h3><div class="fact-list">${fact('Супруга','Эльда')}${fact('Дети','Мира, Нерис')}${fact('Дом','Каменный дом')}</div></div>
  <div class="sheet-actions">${A('К жителю','primary')}${A('Назначить работу')}${A('Еще')}</div>`; };

const buildingSheet = () => `
  <div class="sheet-head"><div class="identity"><div class="portrait">⌂</div><div><span class="eyebrow">Жилье · уровень II</span><h2>Каменный дом</h2><p>Семья Дорана · 4 из 6 мест</p></div></div><div class="state-line"><span>Состояние</span><strong>Строится · 64%</strong></div></div>
  <div class="tabs"><button class="active">Стройка</button><button>Люди</button><button>Запасы</button><button>История</button></div>
  <div class="sheet-body"><h3>Этапы</h3>${metric('Расчистка','Готово',100)}${metric('Материалы','18 / 24',75,'warn')}${metric('Работа','64%',64)}
  <div class="fact-list">${fact('Доставить','6 камня')}${fact('Строители','Доран, Тарин')}${fact('Прогноз','Сегодня, 16:40')}${fact('Приоритет','Высокий')}</div>
  <h3>После завершения</h3><span class="tag">6 мест</span><span class="tag good">+12 комфорта</span><span class="tag">защита от холода</span></div>
  <div class="sheet-actions">${A('Срочно','primary')}${A('Обычный')}${A('Отменить','danger')}</div>`;

const settlement = () => `${header('Тихий Брод','Состояние поселения и ближайшие решения',A('Открыть летопись'))}
  <div class="workspace-body"><div class="recommendations"><div class="card selected"><span class="kicker">Главный риск</span><h2>Вода на 8 дней</h2><p>Потребление выше притока на 2 ед. в день.</p>${A('Построить колодец','primary')}</div><div class="card"><span class="kicker">Возможность</span><h2>3 свободных жителя</h2><p>Можно усилить добычу или исследование.</p>${A('Открыть работу')}</div><div class="card"><span class="kicker">Идет хорошо</span><h2>Еда +8 в день</h2><p>Запас стабилен при текущем населении.</p>${A('Проверить цепочку')}</div></div>
  <div class="grid"><div class="card"><span class="kicker">Люди</span><div class="big">10</div><p>7 заняты · 3 свободны · 1 болен</p></div><div class="card"><span class="kicker">Жилье</span><div class="big">10 / 14</div><p>Каменный дом даст еще 6 мест.</p></div><div class="card"><span class="kicker">Еда</span><div class="big">12 дней</div><p>126 единиц · тренд +8/день</p></div><div class="card"><span class="kicker">Производство</span><div class="big">4 / 6</div><p>2 места простаивают из-за работников.</p></div><div class="card"><span class="kicker">Исследование</span><div class="big">62%</div><p>Обработка камня · прогноз 2 дня.</p></div><div class="card"><span class="kicker">Оборона</span><div class="big">Низкая</div><p>4 бойца · нет палисада с востока.</p></div></div></div>`;

const build = () => `${header('Строительство','Компактный каталог — мир и место размещения остаются видны',A('Очередь строек'))}
  <div class="build-drawer-body"><div class="build-categories"><button class="active">★ Рекомендовано</button><button>⌂ Жилье</button><button>◉ Еда и вода</button><button>⚒ Производство</button><button>▣ Склады</button><button>♜ Защита</button></div>
  <div class="build-strip"><button class="build-tile selected"><span class="build-glyph">◉</span><span><b>Колодец</b><small>🪵 8 · ⬡ 12 · 9 ч</small></span><em>Рекомендуется</em></button><button class="build-tile"><span class="build-glyph">⌂</span><span><b>Каменный дом</b><small>🪵 10 · ⬡ 24 · 18 ч</small></span></button><button class="build-tile"><span class="build-glyph">◫</span><span><b>Сушилка</b><small>🪵 14 · ◌ 6 · 11 ч</small></span></button><button class="build-tile"><span class="build-glyph">♜</span><span><b>Палисад</b><small>🪵 18 · 7 ч</small></span></button><button class="build-tile locked"><span class="build-glyph">⚒</span><span><b>Мастерская</b><small>Нужно исследование</small></span></button></div></div>`;

const techNode = (cls,type,title,detail,state='Доступно') => `<button class="tech-node ${cls}" data-tech-type="${type}"><span class="tech-type ${type}">${type==='build'?'Строительство':type==='defense'?'Оборона':type==='farm'?'Фермерство':type==='craft'?'Ремесло':'Медицина'}</span><b>${title}</b><small>${detail}</small><em>${state}</em></button>`;
const research = () => `${header('Дерево знаний','Каменный век · зависимости и направления развития',A('Исследователи: 2'))}
  <div class="workspace-body research-body"><div class="research-summary"><div><span class="kicker">Активное исследование</span><h2>Обработка камня · 62%</h2><p>Эльда и Нерис · 38 знаний/день · около 2 дней</p></div><div class="progress"><i style="width:62%"></i></div></div>
  <div class="tech-filters" aria-label="Направления исследований"><button class="active" data-tech-filter="all">Все</button><button data-tech-filter="build">Строительство</button><button data-tech-filter="defense">Оборона</button><button data-tech-filter="farm">Фермерство</button><button data-tech-filter="craft">Ремесло</button><button data-tech-filter="medicine">Медицина</button></div>
  <div class="tech-map" data-filter="all"><svg class="tech-links" viewBox="0 0 1200 470" preserveAspectRatio="none" aria-hidden="true"><g><path d="M150 235H270M420 95H510M420 235H510M420 375H510M660 95H750M660 235H750M660 375H750M900 95H1020M900 205H1020M900 295H1020M900 375H1020"/><path d="M270 235C240 235 240 95 270 95M270 235C240 235 240 375 270 375M660 235C700 235 700 205 750 205M660 235C700 235 700 295 750 295"/></g></svg>
  ${techNode('root done','craft','Контроль огня','Костёр · приготовление','Открыто')}
  ${techNode('n-build-1 done','build','Простые инструменты','Топор · молот','Открыто')}${techNode('n-build-2 active','build','Обработка камня','Дом · мастерская','Изучается · 62%')}${techNode('n-build-3 locked','build','Каменная кладка','Мост · стены','Нужна обработка камня')}${techNode('n-build-4 locked','build','Архитектура','Большие здания','Будущая эпоха')}
  ${techNode('n-farm-1 done','farm','Собирательство','Ягоды · травы','Открыто')}${techNode('n-farm-2','farm','Земледелие','Поля · зерно')}${techNode('n-farm-3 locked','farm','Орошение','Каналы · урожай','Нужно земледелие')}${techNode('n-farm-4 locked','farm','Животноводство','Загоны · разведение','Нужно земледелие')}
  ${techNode('n-defense-1 done','defense','Охота','Лук · разделка','Открыто')}${techNode('n-defense-2','defense','Стрельба','Тренировка · башня')}${techNode('n-defense-3 locked','defense','Палисады','Стены · ворота','Нужна кладка')}${techNode('n-medicine locked','medicine','Лекарские знания','Лечение · госпиталь','Нужны травы')}
  </div></div>`;

const dynasty = () => `${header('Династия Серых','Правление, семья и линия наследования',A('История правлений'))}
  <div class="workspace-body"><div class="family-tree"><button class="person ruler"><b>Доран I</b><small>Монарх · 31 год</small></button><button class="person spouse"><b>Эльда</b><small>Супруга · исследователь</small></button><div class="lineage"></div><button class="person heir"><b>Мира</b><small>Наследник · допустима</small></button><button class="person child2"><b>Нерис</b><small>Вторая в очереди</small></button><button class="person child3"><b>Кейл</b><small>Недопустим · ребенок</small></button></div>
  <h3>Линия престолонаследия</h3><div class="succession"><span class="tag good">1 · Мира · назначена</span><span class="tag">2 · Нерис</span><span class="tag warn">3 · Кейл · возраст</span></div>
  <div class="grid" style="margin-top:20px"><div class="card"><span class="kicker">Правление</span><h2>18 дней</h2><p>Основано поселение, открыт огонь.</p></div><div class="card"><span class="kicker">Династия</span><h2>5 живых</h2><p>3 взрослых · 2 ребенка</p></div><div class="card"><span class="kicker">Риск</span><h2>Низкий</h2><p>Два допустимых наследника.</p></div></div></div>`;

const workgroups = () => `${header('Рабочие группы','Назначение жителей, смен, территории и приоритетов',A('Создать группу','primary'))}
  <div class="workspace-body groups-layout"><div class="group-list"><button class="group-card selected"><b>Строители</b><small>3 жителя · дневная смена · высокий приоритет</small></button><button class="group-card"><b>Переносчики</b><small>2 жителя · весь день · обычный приоритет</small></button><button class="group-card"><b>Собиратели</b><small>3 жителя · дневная смена · обычный приоритет</small></button><button class="group-card"><b>Охотники</b><small>2 жителя · утро · высокий приоритет</small></button></div><div><div class="card selected"><span class="kicker">Выбрана группа</span><h2>Строители</h2><p>Командир: Доран · территория: Тихий Брод · задача: строительство и ремонт</p><div class="fact-list">${fact('Смена','06:00–18:00')}${fact('Приоритет','Высокий')}${fact('Замена недоступных','Автоматически')}${fact('Текущая задача','Каменный дом · 64%')}</div></div><h3>Состав</h3><div class="member-grid"><button class="member"><b>Доран</b><small>Командир · строит</small></button><button class="member"><b>Тарин</b><small>Доставляет камень</small></button><button class="member"><b>Рован</b><small>Свободен</small></button></div><div style="margin-top:14px">${A('Добавить выбранных','primary')}${A('Изменить смену')}${A('Задать территорию')}${A('Распустить','danger')}</div></div></div></div>`;

const events = () => `${header('Центр внимания','Решения, угрозы и история уведомлений',A('Показать журнал'))}
  <div class="workspace-body"><div class="event-stage"><div class="event-art"></div><span class="kicker" style="display:block;margin-top:18px">Решение · осталось 2 дня</span><h1 style="font:34px Georgia,serif;margin:7px 0">Спор у нового колодца</h1><p style="color:var(--muted);line-height:1.5">Семьи Дорана и Тарина требуют первой очереди на воду. Решение повлияет на отношения и рабочее расписание.</p><div class="choices"><button class="choice"><b>Вода распределяется поровну</b><small>Стабильность + · обе семьи немного недовольны</small></button><button class="choice"><b>Сначала семья строителя</b><small>Строительство быстрее · отношения с Тарином −</small></button><button class="choice"><b>Пусть решит старейшина</b><small>Результат зависит от Influence и отношений</small></button></div></div></div>`;

const army = () => `${header('Армия','Люди, готовность, снабжение и приказы',A('Сформировать отряд','primary'))}
  <div class="workspace-body army-layout"><div class="unit-list"><button class="unit selected"><b>Стража Тихого Брода</b><small>4 бойца · Доран · в поселении</small></button><button class="unit"><b>Охотничий отряд</b><small>3 бойца · Тарин · в пути</small></button></div><div><div class="readiness"><div class="card"><span class="kicker">Численность</span><div class="big">4 / 6</div></div><div class="card"><span class="kicker">Мораль</span><div class="big">78%</div></div><div class="card"><span class="kicker">Снабжение</span><div class="big">5 дней</div></div><div class="card"><span class="kicker">Готовность</span><div class="big">Средняя</div></div></div><h3>Состав</h3><table class="roster"><thead><tr><th>Боец</th><th>Звание</th><th>Здоровье</th><th>Опыт</th><th>Состояние</th></tr></thead><tbody><tr><td>Доран</td><td>Капитан</td><td>92%</td><td>7</td><td>Готов</td></tr><tr><td>Арен</td><td>Рядовой</td><td>100%</td><td>3</td><td>Готов</td></tr><tr><td>Кейл</td><td>Рядовой</td><td>74%</td><td>2</td><td>Ранен</td></tr><tr><td>Рован</td><td>Рядовой</td><td>100%</td><td>4</td><td>Готов</td></tr></tbody></table><div style="margin-top:14px">${A('На карту','primary')}${A('Удерживать')}${A('Состав')}${A('Распустить','danger')}</div></div></div></div>`;

const map = () => `${header('Держава','Карта, армии, маршруты, разведка и дипломатия',A('Дипломатия'))}
  <div class="workspace-body"><div class="map-legend"><span class="tag good">Своя территория</span><span class="tag danger">Известный противник</span><span class="tag">Не исследовано</span><span class="tag">Слой: снабжение</span></div><div class="strategic-map"><button class="region player" style="left:25%;top:35%">⌂ Тихий Брод</button><button class="region" style="left:48%;top:58%">◈ Каменный перевал</button><button class="region enemy" style="left:72%;top:28%">⚑ Клан Ворона</button><div class="route" style="left:31%;top:42%;width:330px;transform:rotate(17deg)"></div><div class="route" style="left:53%;top:57%;width:270px;transform:rotate(-27deg)"></div></div><div class="map-actions"><div class="card"><span class="kicker">Выбрано</span><h2>Охотничий отряд</h2><p>3 бойца · снабжение 4 дня</p></div><div class="card"><span class="kicker">Маршрут</span><h2>Каменный перевал</h2><p>1 день 6 часов · дождь · средний риск</p></div><div class="card">${A('Отправить отряд','primary')}${A('Очистить маршрут')}</div></div></div>`;

const diplomacy = () => `${header('Дипломатия','Будущий канонический раздел',A('Вернуться к карте'))}<div class="workspace-body"><div class="diplomacy-lock"><div class="seal">✦</div><h1 style="font:34px Georgia,serif">Система еще не реализована</h1><p style="color:var(--muted);line-height:1.6">Здесь появятся известные государства, отношения, интересы, договоры, требования, репутация и задержка сообщений. Макет не создает фиктивные дипломатические данные и не меняет состояние мира.</p><div style="margin-top:18px"><span class="tag">Торговля</span><span class="tag">Союзы</span><span class="tag">Вассалитет</span><span class="tag">Послы</span><span class="tag">Репутация</span></div></div></div>`;

const about = () => `${header('О макете UI01','Проверка архитектуры информации до Unity-реализации',A('Закрыть'))}<div class="workspace-body"><div class="grid"><div class="card"><span class="kicker">Принцип 1</span><h2>Мир прежде панелей</h2><p>Постоянно видны только решения ближайшего горизонта.</p></div><div class="card"><span class="kicker">Принцип 2</span><h2>Одна сущность — одна карточка</h2><p>Житель, здание и поселение имеют устойчивую структуру.</p></div><div class="card"><span class="kicker">Принцип 3</span><h2>Команда идет в сервис</h2><p>Макет не предлагает новую gameplay authority.</p></div><div class="card"><span class="kicker">Принцип 4</span><h2>Причина рядом</h2><p>Заблокированное действие всегда объясняет, как его открыть.</p></div><div class="card"><span class="kicker">Принцип 5</span><h2>Постепенное раскрытие</h2><p>Эпоха и исследования управляют сложностью интерфейса.</p></div><div class="card"><span class="kicker">Статус</span><h2>Только дизайн</h2><p>Полная реализация начнется после пользовательского утверждения.</p></div></div></div>`;

const screens = {
  resident:{workspace:'',sheet:residentSheet,ribbon:()=>`${A('Переместить','primary','ПКМ')}${A('Добавить в группу','', 'Shift')}${A('Прямое управление','','T')}`},
  building:{workspace:'',sheet:buildingSheet,ribbon:()=>`${A('Приоритет','primary')}${A('Доставить сейчас')}${A('Отменить','danger')}`},
  settlement:{workspace:settlement,sheet:null,ribbon:null}, build:{workspace:build,sheet:null,ribbon:()=>`${A('Повернуть','','R')}${A('Подтвердить','primary','ЛКМ')}${A('Отмена','','Esc')}`},
  research:{workspace:research,sheet:null,ribbon:null}, dynasty:{workspace:dynasty,sheet:null,ribbon:null}, workgroups:{workspace:workgroups,sheet:null,ribbon:null}, events:{workspace:events,sheet:null,ribbon:null}, army:{workspace:army,sheet:null,ribbon:null}, map:{workspace:map,sheet:null,ribbon:()=>`${A('Локальный мир','','M')}${A('Слои карты')}${A('Дипломатия')}`}, diplomacy:{workspace:diplomacy,sheet:null,ribbon:null}, about:{workspace:about,sheet:null,ribbon:null}
};

function show(name){
  const screen=screens[name]||screens.resident; app.dataset.screen=name;
  groupMembers.classList.remove('open');
  workspace.innerHTML=screen.workspace?screen.workspace():''; sheet.innerHTML=screen.sheet?screen.sheet():''; ribbon.innerHTML=screen.ribbon?screen.ribbon():'';
  workspace.classList.toggle('with-sheet',Boolean(screen.sheet));
  workspace.classList.toggle('build-drawer',name==='build');
  document.querySelectorAll('[data-go]').forEach(b=>b.classList.toggle('active',b.dataset.go===name));
  document.querySelectorAll('[data-resident]').forEach(b=>b.classList.toggle('selected',b.dataset.resident===activeResident));
  if(name==='map') setTimeout(()=>document.querySelector('.workspace-head .action')?.addEventListener('click',()=>show('diplomacy')),0);
  if(name==='diplomacy') setTimeout(()=>document.querySelector('.workspace-head .action')?.addEventListener('click',()=>show('map')),0);
}

document.addEventListener('click',e=>{
  const resident=e.target.closest('.resident-chip[data-resident]');
  if(resident){activeResident=resident.dataset.resident;app.dataset.selectedResident=activeResident;show('resident');return}
  const group=e.target.closest('.quick-group[data-group]');
  if(group){activeGroup=group.dataset.group;app.dataset.selectedGroup=activeGroup;document.querySelectorAll('.quick-group').forEach(b=>b.classList.toggle('selected',b===group));renderGroupMembers();groupMembers.classList.add('open');return}
  if(e.target.closest('.close-members')){groupMembers.classList.remove('open');return}
  const filter=e.target.closest('.tech-filters [data-tech-filter]');
  if(filter){const value=filter.dataset.techFilter;document.querySelectorAll('[data-tech-filter]').forEach(b=>b.classList.toggle('active',b===filter));document.querySelector('.tech-map')?.setAttribute('data-filter',value);app.dataset.techFilter=value;return}
  const target=e.target.closest('[data-go]');if(target)show(target.dataset.go)
});
document.addEventListener('keydown',e=>{const keys=['settlement','resident','workgroups','build','research','dynasty','army','map','events'];const i=Number(e.key)-1;if(i>=0&&i<keys.length)show(keys[i]);if(e.key==='Escape'){if(groupMembers.classList.contains('open'))groupMembers.classList.remove('open');else show('resident')}});
show('resident');
