// Self-check de las salvaguardas de datos. Correr: node test_translator.js
const assert = require("assert");
const { normalize, translatable } = require("./translator.js");

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
    "Primicia | Email: p@gmail.com | NIT" // email INCRUSTADO en texto (regresion 2026-08-14)
].forEach(s =>
    assert.strictEqual(translatable(normalize(s)), false, "NO debe traducir: " + s));

// Los digitos normales de UI NO deben bloquear la traduccion
["Page 1 of 20", "Top 100 results"].forEach(s =>
    assert.strictEqual(translatable(normalize(s)), true, "debe traducir: " + s));

console.log("OK: salvaguardas de datos pasan");
