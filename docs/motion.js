/* Decorative SVG only; the demo never changes the visitor's connection. */
(function () {
 'use strict';
 var ns = 'http://www.w3.org/2000/svg';
 var shieldPath = 'M32 4 56 12V30C56 45 46 55 32 60 18 55 8 45 8 30V12Z';
 function svgElement(name, attributes) {
  var node = document.createElementNS(ns, name);
  Object.keys(attributes).forEach(function (key) { node.setAttribute(key, attributes[key]); });
  return node;
 }
 function art(kind) {
  var wrap = document.createElement('div'); wrap.className = 'motion-art motion-' + kind; wrap.setAttribute('aria-hidden', 'true');
  var svg = svgElement('svg', { viewBox: '0 0 600 180', focusable: 'false' });
  var center = svgElement('g', { transform: 'translate(236 26) scale(2)' });
  var shape = svgElement('path', { d: shieldPath, class: 'motion-shield' });
  if (kind === 'intro') {
   [70,90,110].forEach(function (y) { svg.appendChild(svgElement('path', { d: 'M20 '+y+'H580', class: 'motion-lane' })); });
   center.appendChild(shape); svg.appendChild(center);
   for (var i=0;i<9;i++) {
    var packet = svgElement('rect', { x: 0, y: 67+(i%3)*20, width: 12, height: 5, rx: 2.5, class: 'motion-packet' });
    packet.style.setProperty('--delay', (-i*.43)+'s'); svg.appendChild(packet);
   }
   center.appendChild(svgElement('path', { d: 'm28 23 10 9-10 9', class: 'motion-arrow' }));
  } else {
   // All fragments share a shield silhouette, so they assemble into the actual logo.
   var defs = svgElement('defs', {}), clip = svgElement('clipPath', { id: 'closing-shield-clip' });
   clip.appendChild(svgElement('path', { d: shieldPath })); defs.appendChild(clip); svg.appendChild(defs);
   for (var row=0;row<4;row++) for (var col=0;col<4;col++) {
    var piece = svgElement('g', { class: 'shield-fragment' });
    piece.style.setProperty('--dx', ((col-1.5)*65)+'px'); piece.style.setProperty('--dy', ((row-1.5)*42)+'px');
    piece.style.setProperty('--turn', ((row+col)%2 ? 65 : -65)+'deg'); piece.style.setProperty('--delay', ((row+col)*.065)+'s');
    var clipped = svgElement('g', { 'clip-path': 'url(#closing-shield-clip)' });
    clipped.appendChild(svgElement('rect', { x: col*16, y: row*16, width: 16.2, height: 16.2, class: 'motion-shield' }));
    piece.appendChild(clipped); center.appendChild(piece);
   }
   center.appendChild(svgElement('path', { d: 'M14 33H26M38 33H50m-20-9 7 9-7 9', class: 'motion-arrow closing-arrow' })); svg.appendChild(center);
  }
  wrap.appendChild(svg); return wrap;
 }
 var hero = document.querySelector('.hero'), closing = document.querySelector('.closing-download');
 if (hero) hero.insertBefore(art('intro'), hero.firstChild);
 if (closing) closing.insertBefore(art('closing'), closing.firstChild);
 if ('IntersectionObserver' in window) {
  var observer = new IntersectionObserver(function (entries) {
   entries.forEach(function (entry) {
    entry.target.classList.toggle('motion-visible', entry.isIntersecting);
    if (entry.isIntersecting && entry.target === closing) { closing.classList.add('assembled'); observer.unobserve(closing); }
   });
  }, { threshold: .25 });
  if (hero) observer.observe(hero.querySelector('.motion-intro'));
  if (closing) { closing.classList.add('assembly-ready'); observer.observe(closing); }
 } else { document.querySelectorAll('.motion-art').forEach(function (node) { node.classList.add('motion-visible'); }); }
})();
