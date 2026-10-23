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

 // Fetch every page so older published releases are not omitted.
 // Static release content remains usable without JavaScript or if GitHub is unavailable.
 var repository = "https://github.com/trfurkan/GoodbyeDPI-Tray/";
 function trusted(url) { return typeof url === "string" && url.indexOf(repository + "releases/") === 0; }
 function installer(release) {
  return (release.assets || []).filter(function (a) { return /^GoodbyeDPI-Installer.*\.exe$/.test(a.name) && trusted(a.browser_download_url); })[0];
 }
 function element(tag, className, text) {
  var node = document.createElement(tag);
  if (className) node.className = className;
  if (text) node.textContent = text;
  return node;
 }
 function link(text, url, className) {
  var node = element("a", className, text); node.href = url; return node;
 }
 function fetchReleases(page) {
  return fetch("https://api.github.com/repos/trfurkan/GoodbyeDPI-Tray/releases?per_page=100&page=" + page)
   .then(function (response) {
    if (!response.ok) throw new Error("Releases unavailable");
    var more = /rel="next"/.test(response.headers.get("Link") || "");
    return response.json().then(function (items) {
     if (!Array.isArray(items)) throw new Error("Invalid releases response");
     return more ? fetchReleases(page + 1).then(function (rest) { return items.concat(rest); }) : items;
    });
   });
 }
 fetchReleases(1)
  .then(function (data) {
   if (!Array.isArray(data)) return;
   var releases = data.filter(function (r) { return !r.draft && trusted(r.html_url); })
    .sort(function (a, b) { return Date.parse(b.published_at) - Date.parse(a.published_at); });
   if (!releases.length) return;
   var stable = releases.filter(function (r) { return !r.prerelease; })[0];
   var asset = stable && installer(stable);
   if (asset) document.querySelectorAll("a[data-installer]").forEach(function (a) { a.href = asset.browser_download_url; });
   var list = document.querySelector("[data-releases]");
   if (!list) return;
   var fragment = document.createDocumentFragment();
   var primary = stable || releases[0];
   var history = element("details", "release-history");
   history.appendChild(element("summary", "", list.dataset.others + " (" + (releases.length - 1) + ")"));
   var older = element("div", "release-list"); history.appendChild(older);
   releases.forEach(function (release, index) {
    var card = element("article", "release-card");
    var meta = element("div", "release-meta");
    if (release === stable) meta.appendChild(element("span", "release-badge", list.dataset.latest));
    if (release.prerelease) meta.appendChild(element("span", "release-badge", list.dataset.prerelease));
    var date = new Date(release.published_at);
    if (!isNaN(date.getTime())) {
     var time = element("time", "", date.toLocaleDateString(root.lang, { year: "numeric", month: "long", day: "numeric" }));
     time.dateTime = date.toISOString(); meta.appendChild(time);
    }
    card.appendChild(meta);
    var title = element("h3"); title.appendChild(link(release.tag_name || release.name, release.html_url)); card.appendChild(title);
    if (release.body && release.body.trim()) {
     var details = element("details"); details.open = release === primary;
     details.appendChild(element("summary", "", list.dataset.notes));
     // Release text is untrusted: never insert it as HTML or execute Markdown HTML.
     var text = release.body.replace(/^#{1,6}\s+/gm, "").replace(/\*\*([^*]+)\*\*/g, "$1");
     details.appendChild(element("div", "release-body", text)); card.appendChild(details);
    }
    var actions = element("div", "actions");
    var download = installer(release);
    if (download) actions.appendChild(link(list.dataset.download, download.browser_download_url, "btn primary"));
    actions.appendChild(link("GitHub", release.html_url, "btn")); card.appendChild(actions);
    if (release === primary) fragment.appendChild(card); else older.appendChild(card);
   });
   if (older.childElementCount) fragment.appendChild(history);
   list.replaceChildren(fragment);
  })
  .catch(function () {});
})();
