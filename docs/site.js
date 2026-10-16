// Shared by the Turkish, English and Russian pages.
(function () {
 var root = document.documentElement;
 function current() { return root.getAttribute("data-theme") || (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"); }
 // Screenshots come in a light and a dark version; show the one that matches the page.
 function swapImages() {
  var theme = current();
  document.querySelectorAll("img[data-light]").forEach(function (img) { img.src = img.getAttribute(theme === "dark" ? "data-dark" : "data-light"); });
 }
 var toggle = document.querySelector(".theme-toggle");
 if (toggle) toggle.addEventListener("click", function () {
  var next = current() === "dark" ? "light" : "dark";
  root.setAttribute("data-theme", next);
  try { localStorage.setItem("theme", next); } catch (e) {}
  swapImages();
 });
 matchMedia("(prefers-color-scheme: dark)").addEventListener("change", function () { if (!root.getAttribute("data-theme")) swapImages(); });
 swapImages();

 // Installer names carry the version, so the download buttons ask GitHub for the latest file.
 // Without JavaScript or when GitHub cannot be reached they keep pointing at the release page.
 fetch("https://api.github.com/repos/trfurkan/GoodbyeDPI-Tray/releases/latest")
  .then(function (r) { return r.ok ? r.json() : null; })
  .then(function (release) {
   if (!release) return;
   var asset = (release.assets || []).filter(function (a) { return /^GoodbyeDPI-Installer.*\.exe$/.test(a.name); })[0];
   if (!asset) return;
   document.querySelectorAll("a[data-installer]").forEach(function (a) { a.href = asset.browser_download_url; });
  })
  .catch(function () {});
})();
