/* Local UI simulation only: no network, service or Windows setting access. */
(function () {
 'use strict';
 function createDemo(clock) {
  var state = { mode: 'running', profile: 0, dns: 1, until: 0, busy: false };
  return {
   state: state,
   reset: function () { state.mode='running'; state.profile=0; state.dns=1; state.until=0; state.busy=false; },
   usesDns: function () { return [0, 3, 4, 5].indexOf(state.profile) !== -1; },
   begin: function () { if (state.busy) return false; state.busy = true; return true; },
   finish: function (action, value) {
    if (action === 'toggle') { state.mode = state.mode === 'running' ? 'stopped' : 'running'; state.until = 0; }
    if (action === 'stop') { state.mode = 'stopped'; state.until = 0; }
    if (action === 'pause') { state.mode = 'paused'; state.until = clock() + value * 60000; }
    if (action === 'profile') state.profile = value;
    if (action === 'dns') state.dns = value;
    if (action === 'restart' || action === 'find') { state.mode = 'running'; state.until = 0; }
    if (action === 'find') state.profile = 0;
    state.busy = false;
   },
   tick: function () { if (!state.busy && state.mode === 'paused' && clock() >= state.until) { state.mode = 'running'; state.until = 0; return true; } return false; }
  };
 }
 if (typeof module !== 'undefined' && module.exports) { module.exports = createDemo; return; }
 var root = document.querySelector('[data-demo]');
 if (!root) return;
 var language = document.documentElement.lang.split('-')[0];
 var strings = {
  tr: ['GoodbyeDPI çalışıyor','GoodbyeDPI durduruldu','Geçici olarak durduruldu','Durum değişiyor…','Başlat','Durdur','Devam et','Mola ver','Molayı yönet','Yeniden başlat','5 dakika durdur','15 dakika durdur','Molayı bitir ve durdur','Bir saniye…','Etkin profil','Moladan dönüş','Bu profil DNS’e dokunmaz, seçim şu an kullanılmıyor.','Örnek işlem tamamlandı.','Örnek profil karşılaştırması…','Örnek karşılaştırma tamamlandı: Arx seçildi. Gerçek bağlantın test edilmedi.','Etkileşimli örnek: düğmeleri ve listeleri deneyebilirsin. İşlemler yalnızca bu önizlemeyi değiştirir.','önerilen',"DNS’siz",'DNS ile','filtresiz','reklamsız','güvenli','GoodbyeDPI varsayılanı'],
  en: ['GoodbyeDPI is running','GoodbyeDPI stopped','Temporarily paused','Changing state…','Start','Stop','Resume','Pause','Manage pause','Restart','Pause for 5 minutes','Pause for 15 minutes','End pause and stop','One moment…','Active profile','Resumes at','This profile does not use DNS. Your selection is saved but inactive.','Example action completed.','Simulating profile comparison…','Example comparison complete: Arx selected. Your connection was not tested.','Interactive example: try the controls and lists. Actions only change this preview.','recommended','without DNS','with DNS','unfiltered','ad-free','secure','GoodbyeDPI default'],
  ru: ['GoodbyeDPI работает','GoodbyeDPI остановлен','Временно приостановлен','Изменение состояния…','Запустить','Остановить','Продолжить','Пауза','Управление паузой','Перезапустить','Пауза на 5 минут','Пауза на 15 минут','Завершить паузу и остановить','Подожди…','Активный профиль','Продолжение в','Этот профиль не использует DNS. Выбор сохранён, но не применяется.','Действие в примере завершено.','Пример сравнения профилей…','Пример завершён: выбран Arx. Твоё подключение не проверялось.','Интерактивный пример: попробуй кнопки и списки. Действия меняют только этот интерфейс.','рекомендуется','без DNS','с DNS','без фильтров','без рекламы','защищённый','по умолчанию в GoodbyeDPI']
 };
 var t = strings[language] || strings.en;
 var profiles = ['Arx · '+t[21], 'Nyx · '+t[22], 'Kitsune · '+t[22], 'Nyx · '+t[23], 'Kitsune · '+t[23], 'Raijin · '+t[23], 'Raijin · '+t[22]];
 var dnsNames = ['AdGuard · '+t[24], 'AdGuard · '+t[25], 'Quad9 · '+t[26], 'OpenDNS', 'CleanBrowsing · '+t[26], 'Yandex ('+t[27]+')'];
 var model = createDemo(Date.now), state = model.state;
 t[20] = {"tr":"Uygulamanın örnek görünümüdür. Gerçek bağlantını etkilemez.","en":"App preview only. It does not affect your actual connection.","ru":"Это пример интерфейса. Он не влияет на твоё подключение."}[language] || 'App preview only. It does not affect your actual connection.';
 root.setAttribute('role','group'); root.setAttribute('aria-label',t[20]);
 root.querySelector('.preview-content').removeAttribute('aria-hidden');
 var caption = root.parentNode.querySelector('.preview-caption'); caption.textContent = t[20];
 var title = root.querySelector('.preview-heading strong'), detail = root.querySelector('.preview-heading small');
 title.setAttribute('role','status');
 var shield = root.querySelector('.preview-shield'); shield.setAttribute('aria-hidden','true');
 shield.innerHTML = '<svg viewBox="0 0 64 64"><circle class="shield-halo" cx="32" cy="32" r="31"/><path class="shield-shape" d="M32 4 56 12V30C56 45 46 55 32 60 18 55 8 45 8 30V12Z"/><path class="shield-cut" d="M14 32H27M37 32H50"/><g class="shield-packets"><circle cy="32" r="4"/><circle cy="32" r="4"/><circle cy="32" r="4"/></g><circle class="shield-track" cx="32" cy="32" r="22"/><circle class="shield-spinner" cx="32" cy="32" r="22"/></svg>';
 function button(text, parent, handler) { var b = document.createElement('button'); b.type='button'; b.textContent=text; b.addEventListener('click',handler); parent.appendChild(b); return b; }
 var actions=root.querySelector('.preview-actions'); actions.replaceChildren();
 var pauseBox=document.createElement('div'); pauseBox.className='preview-pause-box'; actions.appendChild(pauseBox);
 var pause=button(t[7]+' ⌄',pauseBox,function(){ menu.hidden=!menu.hidden; pause.setAttribute('aria-expanded',String(!menu.hidden)); });
 var pauseLabel=document.createElement('span');
 var pauseArrow=document.createElementNS('http://www.w3.org/2000/svg','svg');
 pauseArrow.setAttribute('viewBox','0 0 16 16'); pauseArrow.setAttribute('aria-hidden','true');
 pauseArrow.innerHTML='<path d="m4 6 4 4 4-4" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>';
 pause.replaceChildren(pauseLabel,pauseArrow);
 pause.setAttribute('aria-expanded','false'); pause.setAttribute('aria-controls','preview-pause-menu');
 var menu=document.createElement('div'); menu.className='preview-pause-menu'; menu.id='preview-pause-menu'; menu.hidden=true; pauseBox.appendChild(menu);
 function closeMenu() { menu.hidden=true; pause.setAttribute('aria-expanded','false'); }
 button(t[10],menu,function(){ closeMenu(); act('pause',5); });
 button(t[11],menu,function(){ closeMenu(); act('pause',15); });
 var endPause=button(t[12],menu,function(){ closeMenu(); act('stop'); });
 var toggle=button(t[5],actions,function(){ act('toggle'); }); toggle.className='preview-stop';
 document.addEventListener('click',function(e){ if(!pauseBox.contains(e.target)) closeMenu(); });
 pauseBox.addEventListener('keydown',function(e){ if(e.key==='Escape'){ closeMenu(); pause.focus(); } });
 var fields=root.querySelectorAll('.preview-field');
 function select(field, options, action) {
  var old=field.querySelector('.preview-select'), select=document.createElement('select'); select.className='preview-select';
  select.setAttribute('aria-label',field.querySelector('.preview-label > span').textContent);
  options.forEach(function(text,i){ var o=document.createElement('option'); o.value=i; o.textContent=text; select.appendChild(o); });
  old.replaceWith(select); select.addEventListener('change',function(){ act(action,Number(select.value)); }); return select;
 }
 var profile=select(fields[0],profiles,'profile'), dns=select(fields[1],dnsNames,'dns');
 var dnsHint=fields[1].querySelector('small'), originalHint=dnsHint.textContent;
 var findOld=root.querySelector('.preview-find');
 var find=document.createElement('button'); find.type='button'; find.className='preview-find'; find.textContent=findOld.textContent; findOld.replaceWith(find); find.addEventListener('click',function(){ act('find'); });
 var pendingAction = null;
 var resetRow=document.createElement('div'); resetRow.className='preview-reset-row'; root.appendChild(resetRow);
 var reset=button(({tr:'Sıfırla',en:'Reset',ru:'Сбросить'})[language] || 'Reset',resetRow,function(){
  clearTimeout(pendingAction); pendingAction=null; closeMenu(); model.reset();
  profile.classList.remove('selection-flash'); dns.classList.remove('selection-flash'); paint();
 });
 function paint() {
  var mode=state.busy?'busy':state.mode;
  if(root.dataset.state!==mode) { root.dataset.state=mode; shield.classList.remove('state-pop'); void shield.offsetWidth; shield.classList.add('state-pop'); }
  title.textContent=t[state.busy?3:state.mode==='running'?0:state.mode==='paused'?2:1];
  detail.textContent=state.mode==='paused'?t[15]+': '+new Date(state.until).toLocaleTimeString(language)+' · '+Math.max(0,Math.ceil((state.until-Date.now())/1000))+' s':t[14]+': '+profiles[state.profile]+(model.usesDns()?' · DNS: '+dnsNames[state.dns]:'');
  toggle.textContent=t[state.busy?13:state.mode==='running'?5:state.mode==='paused'?6:4];
  toggle.disabled=state.busy; pause.disabled=state.busy || state.mode==='stopped';
  pauseLabel.textContent=t[state.mode==='paused'?8:7]; endPause.hidden=state.mode!=='paused';
  profile.disabled=state.busy; dns.disabled=state.busy || !model.usesDns(); find.disabled=state.busy || state.mode==='paused';
  profile.value=state.profile; dns.value=state.dns;
  dnsHint.textContent=model.usesDns()?originalHint:t[16];
 }
 function act(action,value) {
  if(!model.begin()) return;
  paint();
  pendingAction=setTimeout(function(){ pendingAction=null; model.finish(action,value); paint();
   if(action==='profile' || action==='dns') { var selected=action==='profile'?profile:dns; selected.classList.remove('selection-flash'); void selected.offsetWidth; selected.classList.add('selection-flash'); }
  },action==='find'?2400:950);
 }
 setInterval(function(){ if(state.mode==='paused') { model.tick(); paint(); } },1000);
 paint();
})();
