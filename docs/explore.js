(function () {
 'use strict';
 [['.feature-dns','dns-karti'],['.feature-sites','site-karti'],['.feature-search','profil-karti']].forEach(function(pair){
  var node=document.querySelector(pair[0]); if(node) node.id=pair[1];
 });
 var demo=document.querySelector('[data-flow]'); if(!demo) return;
 var start=demo.querySelector('[data-flow-start]'), status=demo.querySelector('[data-flow-status]');
 var timer=null, step=0;
 // The step texts and button labels in the page's language (Turkish when the language is unknown).
 var texts={
  tr:{
   steps:[
    '1 / 6 · Tarayıcı siteye bağlantı başlatır. Örneğimiz HTTPS bağlantısının ilk aşamasını izler; GoodbyeDPI şifreli sayfa içeriğini çözmez.',
    '2 / 6 · WinDivert sürücüsü, GoodbyeDPI’ın tanımladığı filtreye uyan paketleri Windows ağ yolundan yakalayıp programa iletir. WinDivert burada paketlere erişim sağlayan araçtır; uygulanacak yöntemi GoodbyeDPI belirler.',
    '3 / 6 · GoodbyeDPI seçilen profilin yöntemlerini uygular. Örneğin ilk TLS verisini daha küçük TCP parçalarıyla gönderebilir. Sahte paketler gibi başka yöntemler de vardır; her profil aynı işlemi yapmaz. Şemadaki bölünme yalnızca bir örnektir.',
    '4 / 6 · GoodbyeDPI, gönderilecek paketleri WinDivert aracılığıyla Windows ağ yoluna geri verir. Paketler normal internet bağlantın üzerinden ilerler; başka bir ülkedeki VPN sunucusuna gitmez.',
    '5 / 6 · Sağlayıcının DPI sistemi trafiği sınıflandırmaya çalışır. Kullanılan yöntem bazı DPI sistemlerinin bağlantıyı doğru tanımasını zorlaştırabilir. Buradaki kutu basitleştirilmiştir; DPI trafiği hat üzerinde veya bir kopyasını izleyerek inceleyebilir.',
    '6 / 6 · Yöntem işe yararsa hedef sunucuyla bağlantı devam eder. TCP parçalama örneğinde sunucunun TCP katmanı veriyi yeniden birleştirir. IP adresin gizlenmez; doğrudan IP engelleri ve her ağ için başarı garantisi yoktur.'
   ],
   restart:'Baştan oynat', again:'Tekrar oynat', playing:'Oynatılıyor…', show:'Tekrar göster'
  },
  en:{
   steps:[
    '1 / 6 · The browser starts a connection to the site. Our example follows the first stage of an HTTPS connection; GoodbyeDPI does not decrypt the encrypted page content.',
    '2 / 6 · The WinDivert driver captures packets that match the filter GoodbyeDPI defines from the Windows network path and passes them to the program. Here WinDivert is the tool that gives access to the packets; GoodbyeDPI decides which method to apply.',
    '3 / 6 · GoodbyeDPI applies the selected profile’s methods. For example, it can send the first TLS data in smaller TCP pieces. There are other methods too, such as fake packets; not every profile does the same thing. The split in the diagram is just an example.',
    '4 / 6 · GoodbyeDPI hands the packets to be sent back to the Windows network path through WinDivert. The packets travel over your normal internet connection; they don’t go to a VPN server in another country.',
    '5 / 6 · The provider’s DPI system tries to classify the traffic. The method used can make it harder for some DPI systems to recognize the connection correctly. The box here is simplified; DPI can inspect traffic inline or by watching a copy of it.',
    '6 / 6 · If the method works, the connection with the target server continues. In the TCP splitting example, the server’s TCP layer puts the data back together. Your IP address is not hidden; there is no guarantee for direct IP blocks or for every network.'
   ],
   restart:'Play from the start', again:'Play again', playing:'Playing…', show:'Show again'
  },
  ru:{
   steps:[
    '1 / 6 · Браузер начинает подключение к сайту. Пример показывает первый этап HTTPS-подключения; GoodbyeDPI не расшифровывает зашифрованное содержимое страницы.',
    '2 / 6 · Драйвер WinDivert перехватывает на сетевом пути Windows пакеты, подходящие под фильтр GoodbyeDPI, и передаёт их программе. Здесь WinDivert даёт доступ к пакетам, а какой метод применить, решает GoodbyeDPI.',
    '3 / 6 · GoodbyeDPI применяет методы выбранного профиля. Например, он может отправить первые данные TLS более мелкими частями TCP. Есть и другие методы, например фальшивые пакеты; не каждый профиль делает одно и то же. Разбиение на схеме — лишь пример.',
    '4 / 6 · GoodbyeDPI через WinDivert возвращает пакеты на сетевой путь Windows. Пакеты идут через ваше обычное интернет-соединение и не попадают на VPN-сервер в другой стране.',
    '5 / 6 · Система DPI провайдера пытается классифицировать трафик. Используемый метод может мешать некоторым системам DPI правильно распознать подключение. Блок здесь упрощён: DPI может анализировать трафик прямо в линии или по его копии.',
    '6 / 6 · Если метод сработал, подключение к целевому серверу продолжается. В примере с разбиением TCP уровень TCP сервера снова собирает данные. IP-адрес не скрывается; для прямых блокировок по IP и для любой сети гарантий нет.'
   ],
   restart:'Сначала', again:'Ещё раз', playing:'Воспроизведение…', show:'Показать снова'
  }
 };
 var text=texts[(document.documentElement.lang||'tr').split('-')[0]]||texts.tr;
 var keys=['browser','capture','modify','inject','inspect','done'];
 var nodes=Array.prototype.slice.call(demo.querySelectorAll('[data-flow-stage]'));
 function show(index){
  demo.dataset.step=keys[index]; status.textContent=text.steps[index];
  nodes.forEach(function(node,i){node.classList.toggle('flow-active',i===index);node.classList.toggle('flow-complete',i<index);if(i===index)node.setAttribute('aria-current','step');else node.removeAttribute('aria-current');});
 }
 nodes.forEach(function(node,index){node.addEventListener('click',function(){clearTimeout(timer);timer=null;show(index);start.disabled=false;start.textContent=text.restart;});});
 function advance(){
  show(step);
  if(step<keys.length-1){step++;timer=setTimeout(advance,5500);}else{timer=null;start.textContent=text.again;start.disabled=false;}
 }
 start.addEventListener('click',function(){
  clearTimeout(timer);step=0;start.disabled=true;start.textContent=text.playing;
  if(matchMedia('(prefers-reduced-motion: reduce)').matches){
   demo.dataset.step='done';status.textContent=text.steps.join(' ');start.textContent=text.show;start.disabled=false;return;
  }
  advance();
 });
 window.addEventListener('pagehide',function(){clearTimeout(timer);start.disabled=false;start.textContent=text.again;});
})();
