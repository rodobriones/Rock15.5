/*!
 * VidaReal DOM Translator  (com_vidareal / Translator)
 * Traduce la UI de Rock (ingles) al idioma destino en el DOM, en runtime.
 * Lee de cache (localStorage -> BD); lo nuevo lo traduce la IA via el endpoint
 * REST y se persiste. Degrada con gracia: ante cualquier error deja el original.
 *
 * ALCANCE POR DEFECTO = UI, NO DATOS. Ver README: ampliar el alcance corre
 * riesgo de traducir nombres/valores de la BD.
 *
 * Inyeccion: agregar en Site -> Page Header Content:
 *   <script src="/Plugins/com_vidareal/Translator/translator.js?v=2" defer></script>
 *
 * Cobertura de carga (Rock arma la pagina por bloques que llegan async):
 *  - Carga inicial / recarga completa  -> boot() -> rescanBurst
 *  - Postback parcial WebForms          -> Sys.WebForms.PageRequestManager
 *  - Inyeccion AJAX/Obsidian en el DOM  -> MutationObserver (debounced)
 *  - Navegacion client-side (Obsidian)  -> hook history pushState/replaceState/popstate
 *  - Render tardio                      -> rescanBurst escalonado (0/600/1500ms)
 * Fuera de alcance: contenido dentro de <iframe>, Shadow DOM, ::before/::after,
 * y dialogos nativos (alert/confirm). Ver CONTEXT.md.
 */
(function () {
    "use strict";

    var API = "/api/com_vidareal/Translator";
    var DONE = "vrtrDone";            // dataset flag en nodos ya procesados
    var CACHE_PREFIX = "vrtr:";       // localStorage key prefix
    var LANG_KEY = "vrtr:lang";       // idioma elegido por el usuario (switcher)
    var EPOCH_KEY = "vrtr:epoch";     // marca de invalidacion del cache local (la manda el server)
    var BATCH = 200;                  // items por request (<= tope del server)
    var DEBOUNCE_MS = 400;

    // Defaults de exclusion. Ampliables via el block attribute ExcludeSelectors (Config endpoint).
    // #cms-admin-footer: barra admin de Rock (Page Load Time / ViewState / HTML Size). Ademas de ser
    // chrome, el load time CAMBIA en cada carga -> cada pagina generaria un string unico pagado a Azure.
    // Check-in: se excluyen SOLO los contenedores que llevan nombres de personas/familias (ninos
    // incluidos) usando los ganchos del markup STOCK de Rock — no el .btn-checkin-select completo,
    // para que los botones de accion (Check Out/Save/Cancel...) SI se traduzcan tras un upgrade de
    // Rock que revierta las traducciones hechas a mano en los .ascx del fork.
    // Clasico: .js-person-select/.checkin-person/.checkin-person-list (Multi/CheckOut PersonSelect),
    // .js-family-select (FamilySelect). Next-gen: family/attendee buttons + banner.
    // GAP conocido: PersonSelect.ascx (modo individual) no tiene clase distintiva en stock; si se usa
    // ese modo, agregar .btn-checkin-select en Exclude Selectors del panel (config, no codigo).
    var EXCLUDE_CONTAINERS =
        "script,style,code,pre,kbd,samp,textarea,svg,[contenteditable]," +
        "[data-no-translate],.notranslate,.vrtr-skip,#cms-admin-footer," +
        ".js-person-select,.checkin-person,.checkin-person-list,.js-family-select," +
        ".family-button,.attendee-button,.attendee-banner";
    // Interior de grids de datos: NO traducir (solo encabezados th, que no son td).
    var DATA_CELLS = ".grid-table td, .grid-table .grid-actions, .js-grid-table td";
    // <select> de UI cuyas <option> SI se traducen aunque la heuristica dude.
    var UI_SELECT_WHITELIST = [];

    var cfg = { enabled: true, targetLanguage: "es", sourceLanguage: "en", showSwitcher: false, availableLanguages: [], switcherContainer: "" };
    var sessionMisses = new Map();    // normText -> # intentos sin resultado (reintenta hasta MAX_MISS_RETRIES)
    var MAX_MISS_RETRIES = 3;         // respuesta parcial del modelo: reintentar, no abandonar para siempre
    var inFlight = new Set();         // normText con request en vuelo: los bursts (0/600/1500ms) se
                                      // solapan con la latencia de la IA; sin este guard, un string
                                      // NUEVO se re-pediria 2-3 veces y Azure se pagaria 2-3 veces
    var processedText = new WeakMap(); // text node -> nodeValue YA procesado. Se compara contra el
                                       // valor actual: si la app cambia el texto del nodo despues,
                                       // difiere y se re-traduce. Ademas evita el bucle "texto ya
                                       // traducido se re-recolecta y se paga a Azure como identidad".

    /* ---------- util ---------- */

    // DEBE coincidir con TranslatorNormalization.Normalize (C#): trim + colapsar espacios.
    function normalize(s) {
        return (s || "").trim().replace(/\s+/g, " ");
    }

    function cacheKey(norm) { return CACHE_PREFIX + cfg.targetLanguage + ":" + norm; }

    function cacheGet(norm) {
        try { return localStorage.getItem(cacheKey(norm)); } catch (e) { return null; }
    }
    function cacheSet(norm, val) {
        try {
            localStorage.setItem(cacheKey(norm), val);
        } catch (e) {
            // Cuota llena: purga las claves de traduccion (no la del idioma) y
            // reintenta una vez. Sin esto, al llenarse se re-pediria todo al server
            // en cada carga de por vida.
            try {
                clearTranslationCache();
                localStorage.setItem(cacheKey(norm), val);
            } catch (e2) { /* sigue sin entrar: se re-pedira al server, no rompe */ }
        }
    }

    // Borra SOLO las traducciones cacheadas (conserva idioma elegido y epoch).
    function clearTranslationCache() {
        Object.keys(localStorage).forEach(function (k) {
            if (k.indexOf(CACHE_PREFIX) === 0 && k !== LANG_KEY && k !== EPOCH_KEY) localStorage.removeItem(k);
        });
    }

    // Invalidacion remota: si el epoch del server cambio (correccion manual,
    // borrado o purga en BD), el cache local puede tener traducciones viejas ->
    // limpiarlo. Sin esto, una correccion NUNCA llegaria a un navegador que ya
    // tenia cacheada la traduccion anterior.
    function checkCacheEpoch(serverEpoch) {
        try {
            var epoch = serverEpoch || "";
            if (localStorage.getItem(EPOCH_KEY) !== epoch) {
                clearTranslationCache();
                localStorage.setItem(EPOCH_KEY, epoch);
            }
        } catch (e) { /* sin localStorage: no hay cache que invalidar */ }
    }

    // Regex de "esto parece DATO, no UI" -> no traducir.
    var RE_LAVA = /\{\{|\}\}|\{%|%\}/;
    // SIN anclas: un email INCRUSTADO en texto ("... | Email: x@y.com | NIT ...") tambien es dato
    // sensible (regresion 2026-08-14: se tradujo un string con email+NIT de un donante).
    var RE_EMAIL = /\S+@\S+\.\S+/;
    var RE_URL = /^(https?:\/\/|www\.|\/)\S+$/i;
    var RE_GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    var RE_HASLETTER = /[A-Za-zÀ-ÿ]/;
    // Solo numeros/moneda/fecha/puntuacion (sin palabras): Q1,234.50  $10  12/05/2026  (555) 123-4567
    var RE_DATALIKE = /^[\sQ$€¥.,:;%#@()\/\-+0-9]+$/;
    // SENSIBLE: enmascarado de secretos (tarjetas/cuentas): "•••• 4242", "****1071", "xxxx", "9x00".
    var RE_MASKED = /[•*]{2,}|\b[xX]{2,}\b|\d[xX]\d/;

    function translatable(norm) {
        if (!norm) return false;
        if (norm.length < 2 || norm.length > 2000) return false;
        if (!RE_HASLETTER.test(norm)) return false;     // sin letras -> no es UI
        if (RE_LAVA.test(norm)) return false;            // merge fields Lava
        if (RE_EMAIL.test(norm)) return false;
        if (RE_URL.test(norm)) return false;
        if (RE_GUID.test(norm)) return false;
        if (RE_DATALIKE.test(norm)) return false;
        if (RE_MASKED.test(norm)) return false;          // secretos enmascarados -> dato sensible
        // 8+ digitos en total = numero de tarjeta/cuenta/telefono incrustado en texto
        // ("Visa 4487 9x00 xxxx 1071"). Ningun label de UI real trae tantos digitos.
        if ((norm.match(/\d/g) || []).length >= 8) return false;
        return true;
    }

    /* ---------- recoleccion ---------- */

    // Registra un objetivo: norm -> lista de funciones apply(translation)
    function register(map, norm, applyFn) {
        var arr = map.get(norm);
        if (!arr) { arr = []; map.set(norm, arr); }
        arr.push(applyFn);
    }

    // matches/closest LANZAN si un selector (de config) es invalido. Defensivo:
    // nunca dejar que un selector malo aborte toda la traduccion de la pagina.
    function safeClosest(el, sel) {
        try { return el && el.closest ? el.closest(sel) : null; } catch (e) { return null; }
    }
    function safeMatches(el, sel) {
        try { return !!(el && el.matches && el.matches(sel)); } catch (e) { return false; }
    }
    function validSelector(sel) {
        try { document.createDocumentFragment().querySelector(sel); return true; } catch (e) { return false; }
    }
    function safeQuery(sel) {
        try { return document.querySelector(sel); } catch (e) { return null; }
    }
    // Elementos que matchean sel DENTRO de root, INCLUYENDO el propio root si
    // matchea (querySelectorAll excluye al root). Clave para recolectar subarboles
    // concretos: un <input> anadido con placeholder, o un <select>, puede SER el root.
    function matchingEls(root, sel) {
        var out = [];
        if (root && root.nodeType === 1 && safeMatches(root, sel)) out.push(root);
        if (root && root.querySelectorAll) {
            var d = root.querySelectorAll(sel);
            for (var i = 0; i < d.length; i++) out.push(d[i]);
        }
        return out;
    }

    function isExcluded(el) {
        if (!el || !el.closest) return true;
        return !!safeClosest(el, EXCLUDE_CONTAINERS);
    }

    // Aplica traduccion a un text node preservando whitespace de borde.
    function applyTextNode(node, original) {
        return function (t) {
            var m = original.match(/^(\s*)([\s\S]*?)(\s*)$/);
            node.nodeValue = (m ? m[1] : "") + t + (m ? m[3] : "");
            // Registrar el valor APLICADO: el nodo ya esta en el idioma destino;
            // sin esto, el siguiente rescan lo recolectaria como string "nuevo" y
            // se pagaria a Azure una traduccion identidad (es->es) por cada string.
            processedText.set(node, node.nodeValue);
        };
    }

    function applyAttr(el, attr) {
        return function (t) { try { el.setAttribute(attr, t); } catch (e) {} };
    }

    // Heuristica: ¿traducir las <option> de este <select>?
    // SALVAGUARDA DE DATOS (regla dura): por defecto NO se traducen las <option>.
    // Pueden ser datos con nombres propios (campus, personas, estados, paises) y
    // una heuristica puede equivocarse. Solo se traducen las options de selects
    // declarados explicitamente en la whitelist de UI (config "UI Select
    // Whitelist", p.ej. #ddlStatus). El value NUNCA se toca en ningun caso.
    function shouldTranslateSelect(sel) {
        for (var i = 0; i < UI_SELECT_WHITELIST.length; i++) {
            if (UI_SELECT_WHITELIST[i] && safeMatches(sel, UI_SELECT_WHITELIST[i])) return true;
        }
        return false;
    }

    function collect(root, map) {
        // 1) Text nodes
        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
            acceptNode: function (n) {
                if (processedText.get(n) === n.nodeValue) return NodeFilter.FILTER_REJECT; // sin cambios desde que se proceso
                if (!n.nodeValue || !n.nodeValue.trim()) return NodeFilter.FILTER_REJECT;
                var p = n.parentElement;
                if (!p) return NodeFilter.FILTER_REJECT;
                if (isExcluded(p)) return NodeFilter.FILTER_REJECT;
                if (safeClosest(p, DATA_CELLS)) return NodeFilter.FILTER_REJECT; // celda de datos
                // <option>: se maneja aparte (respetando value)
                if (p.tagName === "OPTION") return NodeFilter.FILTER_REJECT;
                var norm = normalize(n.nodeValue);
                return translatable(norm) ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
            }
        });
        var n;
        while ((n = walker.nextNode())) {
            var original = n.nodeValue;
            var norm = normalize(original);
            register(map, norm, applyTextNode(n, original));
            // Marcar procesado (con SU texto actual) SOLO si ya esta resuelto (en
            // cache). Un nodo aun no traducido queda recolectable -> se reintenta
            // en el proximo run (hasta MAX_MISS_RETRIES) en vez de quedar en ingles
            // para siempre. Si despues cambia el texto, difiere del valor guardado
            // y se vuelve a procesar/traducir.
            if (cacheGet(norm) !== null) processedText.set(n, n.nodeValue);
        }

        // 2) Atributos visibles (incluye el root si el mismo los tiene)
        matchingEls(root, "[placeholder],[title],[aria-label],[alt]").forEach(function (el) {
            if (el.dataset.vrtrAttr || isExcluded(el) || safeClosest(el, DATA_CELLS)) return;
            el.dataset.vrtrAttr = "1"; // evita re-traducir el atributo en re-scans
            ["placeholder", "title", "aria-label", "alt"].forEach(function (attr) {
                // placeholder solo en campos de texto (nunca el value editable)
                if (attr === "placeholder" && el.tagName !== "INPUT" && el.tagName !== "TEXTAREA") return;
                var v = el.getAttribute(attr);
                if (!v) return;
                var norm = normalize(v);
                if (!translatable(norm)) return;
                register(map, norm, applyAttr(el, attr));
            });
        });

        // 2b) Botones <input type=submit|button|reset>: aquí el `value` ES la
        // etiqueta VISIBLE (no un dato; el postback de Rock va por __EVENTTARGET).
        // Seguro traducirlo. NUNCA para otros tipos de input (text/password/etc.).
        matchingEls(root, "input[type='submit'],input[type='button'],input[type='reset']").forEach(function (el) {
            if (el.dataset.vrtrVal || isExcluded(el) || safeClosest(el, DATA_CELLS)) return;
            var v = el.getAttribute("value");
            if (!v) return;
            var norm = normalize(v);
            if (!translatable(norm)) return;
            el.dataset.vrtrVal = "1";
            register(map, norm, applyAttr(el, "value"));
        });

        // 3) <option> (texto visible, NUNCA el value). Incluye el root si es <select>.
        matchingEls(root, "select").forEach(function (sel) {
            if (isExcluded(sel) || !shouldTranslateSelect(sel)) return;
            for (var i = 0; i < sel.options.length; i++) {
                var opt = sel.options[i];
                if (opt.dataset[DONE]) continue;
                // SALVAGUARDA: si el <option> NO tiene atributo value explicito, el
                // texto ES lo que se envia al guardar -> traducirlo corromperia el
                // dato. Solo traducimos options con value propio (envio independiente).
                if (!opt.hasAttribute("value")) continue;
                var norm = normalize(opt.text);
                if (!translatable(norm)) continue;
                register(map, norm, (function (o) { return function (t) { o.text = t; }; })(opt));
                opt.dataset[DONE] = "1";
            }
        });
    }

    /* ---------- resolver (cache + servidor) ---------- */

    function applyAll(applyFns, t) {
        applyFns.forEach(function (fn) { try { fn(t); } catch (e) {} });
    }

    function resolve(map) {
        if (map.size === 0) return;
        var pending = [];
        map.forEach(function (applyFns, norm) {
            var cached = cacheGet(norm);
            if (cached !== null) { applyAll(applyFns, cached); return; }
            if (inFlight.has(norm)) return;                          // ya pedido, respuesta en camino
            if ((sessionMisses.get(norm) || 0) >= MAX_MISS_RETRIES) return;
            pending.push(norm);
        });
        if (pending.length === 0) return;

        for (var i = 0; i < pending.length; i += BATCH) {
            sendBatch(pending.slice(i, i + BATCH), map);
        }
    }

    function sendBatch(items, map) {
        items.forEach(function (norm) { inFlight.add(norm); });
        fetch(API + "/Resolve", {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ targetLanguage: cfg.targetLanguage, items: items })
        }).then(function (r) {
            return r.ok ? r.json() : null;
        }).then(function (data) {
            var results = (data && data.results) || {};
            var gotNew = false;
            items.forEach(function (norm) {
                inFlight.delete(norm);
                var t = results[norm];
                if (typeof t === "string" && t.length) {
                    cacheSet(norm, t);
                    gotNew = true;
                    var fns = map.get(norm);
                    if (fns) applyAll(fns, t);
                } else {
                    // respuesta parcial/sin traduccion: cuenta el intento; se
                    // reintenta en proximos runs hasta MAX_MISS_RETRIES.
                    sessionMisses.set(norm, (sessionMisses.get(norm) || 0) + 1);
                }
            });
            // Nodos que aparecieron MIENTRAS este request estaba en vuelo se
            // saltaron (inFlight). Con las traducciones ya en cache, una pasada
            // extra los cubre sin re-pedir nada (todo sale de cacheGet).
            if (gotNew) setTimeout(function () { run(document.body); }, 0);
        }).catch(function () {
            // degradar: dejar original (y liberar para que un run futuro reintente)
            items.forEach(function (norm) { inFlight.delete(norm); });
        });
    }

    /* ---------- ciclo ---------- */

    function run(root) {
        try {
            var map = new Map();
            collect(root || document.body, map);
            resolve(map);
        } catch (e) { /* nunca romper la pagina */ }
    }

    // Re-escaneo ESCALONADO de TODO el body: cubre contenido que renderiza TARDE
    // (bloques Obsidian/Vue y AJAX), carga inicial y navegacion. Poco frecuente.
    // run() es idempotente: processedText + cache evitan retrabajo y refetch.
    var burstTimers = [];
    function rescanBurst() {
        burstTimers.forEach(clearTimeout);
        burstTimers = [0, 600, 1500].map(function (d) {
            return setTimeout(function () { run(document.body); }, d);
        });
    }

    // PERFORMANCE: en lugar de re-barrer todo document.body en CADA mutacion del
    // observer, acumulamos solo los subarboles anadidos/mutados y recolectamos
    // unicamente esos (un solo resolve por tanda, con debounce).
    var pendingRoots = [];
    var observerTimer = null;
    function queueRoots(roots) {
        for (var i = 0; i < roots.length; i++) {
            if (roots[i]) pendingRoots.push(roots[i]);
        }
        clearTimeout(observerTimer);
        observerTimer = setTimeout(flushObserver, DEBOUNCE_MS);
    }
    function flushObserver() {
        try {
            var roots = pendingRoots;
            pendingRoots = [];
            var map = new Map();
            for (var i = 0; i < roots.length; i++) {
                collect(roots[i], map);
            }
            resolve(map);
        } catch (e) { /* nunca romper la pagina */ }
    }

    function startObserver() {
        var obs = new MutationObserver(function (mutations) {
            var roots = [];
            for (var i = 0; i < mutations.length; i++) {
                var m = mutations[i];
                if (m.type === "childList") {
                    for (var j = 0; j < m.addedNodes.length; j++) {
                        var node = m.addedNodes[j];
                        if (node.nodeType === 1) {
                            roots.push(node);                       // elemento -> recolecta su subarbol
                        } else if (node.nodeType === 3 && node.parentElement) {
                            roots.push(node.parentElement);          // text node -> su padre
                        }
                    }
                } else if (m.type === "attributes" && m.target && m.target.nodeType === 1) {
                    roots.push(m.target);                            // matchingEls incluye al root
                }
            }
            if (roots.length) {
                queueRoots(roots);
            }
        });
        obs.observe(document.body, {
            childList: true, subtree: true,
            attributes: true, attributeFilter: ["placeholder", "title", "aria-label", "alt"]
        });
    }

    // El admin de Rock (WebForms) refresca y navega via UpdatePanel async
    // postbacks. Este es el hook idiomatico y a prueba de balas: re-traduce
    // despues de CADA postback parcial (mas fiable que solo el MutationObserver).
    function hookPartialPostbacks() {
        try {
            if (window.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager) {
                Sys.WebForms.PageRequestManager.getInstance().add_endRequest(function () {
                    rescanBurst();
                });
            }
        } catch (e) { /* sitio sin ASP.NET AJAX */ }
    }

    // Obsidian navega del lado del cliente (history API) SIN recargar ni hacer
    // postback de WebForms -> ni boot() ni el hook de PageRequestManager
    // disparan. Parcheamos history + popstate para re-escanear al cambiar de ruta.
    function hookSpaNavigation() {
        var fire = function () { try { rescanBurst(); } catch (e) {} };
        ["pushState", "replaceState"].forEach(function (m) {
            var orig = history[m];
            if (typeof orig === "function") {
                history[m] = function () {
                    var r = orig.apply(this, arguments);
                    fire();
                    return r;
                };
            }
        });
        window.addEventListener("popstate", fire);
    }

    // Estilos del switcher. FLOTANTE = "pestañita" discreta abajo-derecha: colapsada
    // muestra solo el idioma activo (36px) y se ATENUA tras unos segundos; al tocarla
    // despliega la lista en columna hacia arriba. La expansion es por TAP/CLICK
    // explicito: en iOS los botones no reciben focus al tocarlos, asi que
    // hover/:focus-within no son fiables en touch. safe-area = barra de home iOS.
    // INLINE (SwitcherContainer) = pills en flujo, sin tab ni atenuado.
    // z-index 1030 = debajo del backdrop de modales de Rock (1040) para NO taparlos.
    function injectSwitcherStyles() {
        if (document.getElementById("vrtr-switcher-style")) return;
        var st = document.createElement("style");
        st.id = "vrtr-switcher-style";
        st.textContent =
            "#vrtr-switcher{position:fixed;right:0;bottom:calc(30px + env(safe-area-inset-bottom,0px));z-index:1030;" +
            "display:flex;flex-direction:column;align-items:flex-end;gap:6px;transition:opacity .25s ease;}" +
            "#vrtr-switcher.vrtr-dim{opacity:.5;}" +
            "#vrtr-switcher .vrtr-tab{border:0;border-radius:8px 0 0 8px;background:#3b43f6;color:#fff;" +
            "font-weight:800;font-size:12px;letter-spacing:.04em;line-height:1;width:44px;height:36px;" +
            "cursor:pointer;box-shadow:0 4px 12px rgba(15,23,42,.25);}" +
            "#vrtr-switcher .vrtr-menu{display:none;flex-direction:column;align-items:flex-end;gap:4px;}" +
            "#vrtr-switcher.vrtr-open .vrtr-menu{display:flex;}" +
            "#vrtr-switcher .vrtr-btn{border:0;border-radius:8px 0 0 8px;background:#fff;color:#1f2937;" +
            "font-size:13px;line-height:1;padding:10px 14px;min-width:112px;text-align:right;cursor:pointer;" +
            "box-shadow:0 4px 12px rgba(15,23,42,.18);}" +
            "#vrtr-switcher .vrtr-btn.is-active{background:#3b43f6;color:#fff;font-weight:700;}" +
            "#vrtr-switcher.vrtr-inline{position:static;flex-direction:row;gap:0;display:inline-flex;vertical-align:middle;}" +
            "#vrtr-switcher.vrtr-inline .vrtr-btn{border-radius:0;min-width:0;box-shadow:none;padding:5px 12px;" +
            "text-align:center;background:#b7b8f6;color:#fff;}" +
            "#vrtr-switcher.vrtr-inline .vrtr-btn.is-active{background:#3b43f6;}" +
            "#vrtr-switcher.vrtr-inline .vrtr-btn:first-child{border-radius:6px 0 0 6px;}" +
            "#vrtr-switcher.vrtr-inline .vrtr-btn:last-child{border-radius:0 6px 6px 0;}";
        document.head.appendChild(st);
    }

    // Switcher de idioma. El idioma elegido se persiste (localStorage) y se
    // recarga la pagina (cada idioma tiene su cache).
    function renderSwitcher() {
        if (!cfg.showSwitcher) return;
        if (window.self !== window.top) return;                 // no en iframes (modales de Rock)
        if (document.getElementById("vrtr-switcher")) return;

        var langs = cfg.availableLanguages.filter(function (l) { return l && l.code; });
        if (!langs.length) return;

        var container = cfg.switcherContainer ? safeQuery(cfg.switcherContainer) : null;
        injectSwitcherStyles();

        var wrap = document.createElement("div");
        wrap.id = "vrtr-switcher";
        wrap.className = "notranslate" + (container ? " vrtr-inline" : "");
        wrap.setAttribute("data-no-translate", "1");

        function pick(l) {
            if (l.code === cfg.targetLanguage) return false;    // ya activo
            try { localStorage.setItem(LANG_KEY, l.code); } catch (e) {}
            location.reload();
            return true;
        }

        function makeBtn(l) {
            var b = document.createElement("button");
            b.type = "button";
            b.className = "vrtr-btn" + (l.code === cfg.targetLanguage ? " is-active" : "");
            b.textContent = l.label || l.code;
            b.title = l.label || l.code;
            return b;
        }

        // --- modo inline (dentro de un contenedor del theme): pills, sin tab ---
        if (container) {
            langs.forEach(function (l) {
                var b = makeBtn(l);
                b.addEventListener("click", function () { pick(l); });
                wrap.appendChild(b);
            });
            container.appendChild(wrap);
            return;
        }

        // --- modo flotante: pestañita colapsada + menu vertical al tocar ---
        var menu = document.createElement("div");
        menu.className = "vrtr-menu";

        var tab = document.createElement("button");
        tab.type = "button";
        tab.className = "vrtr-tab";
        tab.textContent = (cfg.targetLanguage || "").toUpperCase();
        tab.title = "Change language";
        tab.setAttribute("aria-haspopup", "true");
        tab.setAttribute("aria-expanded", "false");

        // Atenuado en reposo: presente pero sin competir con el contenido.
        var dimTimer = null;
        function scheduleDim(ms) {
            clearTimeout(dimTimer);
            dimTimer = setTimeout(function () {
                if (!wrap.classList.contains("vrtr-open")) wrap.classList.add("vrtr-dim");
            }, ms);
        }
        function setOpen(open) {
            wrap.classList.toggle("vrtr-open", open);
            tab.setAttribute("aria-expanded", open ? "true" : "false");
            if (open) { clearTimeout(dimTimer); wrap.classList.remove("vrtr-dim"); }
            else scheduleDim(1500);
        }

        langs.forEach(function (l) {
            var b = makeBtn(l);
            b.addEventListener("click", function () {
                if (!pick(l)) setOpen(false);                   // mismo idioma: solo cerrar
            });
            menu.appendChild(b);
        });

        tab.addEventListener("click", function () {
            setOpen(!wrap.classList.contains("vrtr-open"));
        });
        wrap.addEventListener("mouseenter", function () {
            clearTimeout(dimTimer);
            wrap.classList.remove("vrtr-dim");
        });
        wrap.addEventListener("mouseleave", function () {
            if (!wrap.classList.contains("vrtr-open")) scheduleDim(1500);
        });
        wrap.addEventListener("touchstart", function () {
            wrap.classList.remove("vrtr-dim");
        }, { passive: true });

        // Tap/click FUERA cierra el menu (capture: sobrevive a stopPropagation de la pagina).
        document.addEventListener("click", function (e) {
            if (wrap.classList.contains("vrtr-open") && !wrap.contains(e.target)) setOpen(false);
        }, true);

        wrap.appendChild(menu);
        wrap.appendChild(tab);
        document.body.appendChild(wrap);

        // Primera visita (sin idioma elegido aun): visible unos segundos para que
        // el visitante descubra que puede cambiar idioma; despues, atenuado rapido.
        var firstVisit = true;
        try { firstVisit = !localStorage.getItem(LANG_KEY); } catch (e) {}
        scheduleDim(firstVisit ? 4000 : 1500);
    }

    var started = false;
    function start() {
        if (!cfg.enabled || started) return;                   // guard: nunca arrancar dos veces (evita doble observer)
        started = true;
        try { renderSwitcher(); } catch (e) { /* el switcher nunca debe romper el arranque */ }
        if (cfg.targetLanguage === cfg.sourceLanguage) return; // idioma original: no traducir, dejar UI tal cual
        rescanBurst();
        startObserver();
        hookPartialPostbacks();
        hookSpaNavigation();
    }

    function boot() {
        fetch(API + "/Config", { credentials: "same-origin" })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (c) {
                if (c) {
                    checkCacheEpoch(c.cacheEpoch);
                    cfg.enabled = c.enabled !== false;
                    cfg.sourceLanguage = c.sourceLanguage || "en";
                    cfg.showSwitcher = !!c.showSwitcher;
                    cfg.switcherContainer = c.switcherContainer || "";
                    cfg.availableLanguages = c.availableLanguages || [];
                    var saved = null;
                    try { saved = localStorage.getItem(LANG_KEY); } catch (e) {}
                    cfg.targetLanguage = saved || c.targetLanguage || "es";
                    EXCLUDE_CONTAINERS = mergeSelectors(EXCLUDE_CONTAINERS, c.exclude);
                    UI_SELECT_WHITELIST = linesToArray(c.uiSelectWhitelist).filter(validSelector);
                }
                start();
            })
            // Si Config falla, igual arrancamos con defaults (es) en vez de no
            // traducir nada. Antes: un fetch fallido mataba observer y traduccion.
            .catch(function () { start(); });
    }

    function linesToArray(s) {
        return (s || "").split(/\r?\n/).map(function (x) { return x.trim(); }).filter(Boolean);
    }
    function mergeSelectors(base, extraLines) {
        // Descarta selectores invalidos (un selector roto en config haria que
        // closest()/matches() lanzaran y se desactivara la traduccion).
        var extra = linesToArray(extraLines).filter(validSelector);
        return extra.length ? base + "," + extra.join(",") : base;
    }

    if (typeof document !== "undefined") {
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", boot);
        } else {
            boot();
        }
    }

    // Exporta funciones puras para tests bajo Node (no afecta al navegador).
    if (typeof module !== "undefined" && module.exports) {
        module.exports = { normalize: normalize, translatable: translatable };
    }
})();
