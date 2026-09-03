// Self-check de las salvaguardas de datos. Correr: node test_translator.js
const assert = require("assert");
const { normalize, translatable, churning, slotKey } = require("./translator.js");

// normalize debe coincidir con TranslatorNormalization.Normalize (C#): trim + colapsar espacios.
assert.strictEqual(normalize("  Save   Changes \n"), "Save Changes");
assert.strictEqual(normalize(""), "");
// Regresion bug C2: el set de espacios debe incluir NBSP y BOM (igual que el C#).
assert.strictEqual(normalize("Save Changes"), "Save Changes", "NBSP debe colapsar");
assert.strictEqual(normalize("﻿Save﻿"), "Save", "BOM/zero-width debe recortarse");
assert.strictEqual(normalize("Save﻿Changes"), "Save Changes", "BOM INTERNO debe colapsar (paridad con el set del server)");
assert.strictEqual(normalize("A  B"), "A B", "em-space debe colapsar");

// SÍ traducir (UI real)
["Save", "Add Person", "Are you sure?", "First Name"].forEach(s =>
    assert.strictEqual(translatable(normalize(s)), true, "debe traducir: " + s));

// NO traducir (datos / no-UI) -> proteger de corromper datos
[
    "john.doe@vidareal.tv",        // email
    "https://rock.org/x",          // url
    "/page/123",                   // path
    "Q1,234.50",                   // moneda
    "$10.00",
    "12/05/2026",                  // fecha
    "8470F648-58B6-405A-8C4D-CD661F6678DB", // guid
    "{{ Person.NickName }}",       // Lava
    "{% if x %}",                  // Lava
    "12345",                       // numero
    "()",                          // sin letras
    "x",                           // muy corto
    // SENSIBLE (regresion 2026-08-14: una tarjeta real llego a traducirse)
    "Visa 4487 9x00 xxxx 1071",    // tarjeta enmascarada con 'x'
    "Visa •••• 4242",              // tarjeta enmascarada con bullets
    "Tarjeta ****1071",            // tarjeta enmascarada con asteriscos
    "Cuenta 12345678 activa",      // 8+ digitos incrustados en texto
    "Primicia | Email: p@gmail.com | NIT", // email INCRUSTADO en texto (regresion 2026-08-14)
    // Regresion 2026-09-02: 25,000 de 33,474 filas del cache eran datos, no UI.
    "104h 56m 42s",                // cronometro en vivo (ReservationScanner): 7,438 filas
    "3m 07s",
    "45s",
    "Added: 8/16/2026",            // la IA reordena dia/mes -> corrompe el dato visible
    "Vence 12-05-2026",
    "October 1966",                // mes+anio suelto = fecha de nacimiento, no UI
    "Feb 2018",
    "Perez Family",                // asi nombra Rock a una familia (dato)
    "Veliz Rivera Family"
].forEach(s =>
    assert.strictEqual(translatable(normalize(s)), false, "NO debe traducir: " + s));

// Los digitos normales de UI NO deben bloquear la traduccion
["Page 1 of 20", "Top 100 results"].forEach(s =>
    assert.strictEqual(translatable(normalize(s)), true, "debe traducir: " + s));

// Las reglas nuevas NO deben tragarse UI legitima (falsos positivos)
[
    "Add Family",                  // boton real de Rock: "Family" precedido de verbo
    "Edit Family",
    "My Family",
    "Family",                      // label suelto
    "Save",
    "Select the group",
    "May",                         // mes SIN anio = puede ser UI ("May" en un dropdown)
    "5 items"                      // no es un cronometro: no termina en d/h/m/s
].forEach(s =>
    assert.strictEqual(translatable(normalize(s)), true, "debe traducir (falso positivo): " + s));

// --- guard de churn por ranura (contadores en vivo) ---
// Nodos falsos: churning solo necesita parentElement/tagName/classList.
function fakeNode(classChain) {
    let parent = null;
    // se construye de la hoja hacia la raiz
    for (const cls of classChain.slice().reverse()) {
        const [tag, ...classes] = cls.split(".");
        parent = { tagName: tag, classList: classes, parentElement: parent };
    }
    return { parentElement: parent };
}

// Un contador reescribe EL MISMO nodo -> se apaga tras SLOT_MAX_CHANGES (3).
const timer = fakeNode(["DIV.rsCountdown", "STRONG.rsNum"]);
assert.strictEqual(churning(timer, "104h 56m 42s"), false, "1er valor: solo se registra");
assert.strictEqual(churning(timer, "104h 56m 41s"), false, "1a reescritura");
assert.strictEqual(churning(timer, "104h 56m 40s"), false, "2a reescritura");
assert.strictEqual(churning(timer, "104h 56m 39s"), false, "3a reescritura");
assert.strictEqual(churning(timer, "104h 56m 38s"), true, "4a reescritura: ranura apagada");
assert.strictEqual(churning(timer, "104h 56m 37s"), true, "queda apagada");

// REGRESION 1.4.6: nodos HERMANOS que comparten ranura (las pestanas del perfil:
// Profile | Extended Attributes | Steps | Groups | Documents | Contributions |
// Benevolence | Security | History) NO son un contador. Apagaban la ranura a partir
// de la 4a pestana y solo se traducian 3 por tanda. Cada pestana es un nodo distinto.
const tabs = ["Profile", "Extended Attributes", "Steps", "Groups", "Documents",
              "Contributions", "Benevolence", "Security", "History"];
tabs.forEach(t => {
    const tab = fakeNode(["UL.nav.nav-tabs", "LI", "A"]);   // nodo NUEVO, misma ranura
    assert.strictEqual(churning(tab, t), false, "pestana no debe apagar la ranura: " + t);
});

// Repetir el MISMO texto no cuenta como churn (un label re-renderizado no se apaga).
const label = fakeNode(["DIV.panel", "LABEL.control-label"]);
for (let i = 0; i < 10; i++) {
    assert.strictEqual(churning(label, "First Name"), false, "texto estable nunca es churn");
}

// La identidad de la ranura ignora clases de ESTADO (is-active, open...): si no,
// un boton que se activa/desactiva se leeria como ranura nueva en cada render.
assert.strictEqual(
    slotKey(fakeNode(["DIV.btn.is-active"])),
    slotKey(fakeNode(["DIV.btn"])),
    "las clases de estado no deben cambiar la ranura");

console.log("OK: salvaguardas de datos pasan");
