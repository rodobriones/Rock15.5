# Cambios en RockWeb/Scripts — VidaReal fork de Rock 18.1

Rama: `hotfix-18.1`  
Base de comparacion: commit `ca2ca0ec94`

---

## `Rock/Controls/datePicker.js` — Soporte de idioma espanol

### Que se cambio

Se agrego soporte de localizacion al control `datePicker` de Rock, que usa internamente la libreria `bootstrap-datepicker`. El cambio tiene dos partes:

**1. Deteccion y normalizacion del idioma**

```javascript
var language = (options.language || "en").toLowerCase();
if (language.indexOf("-") >= 0) {
    language = language.split("-")[0];
}
```

Toma la opcion `language` del bloque Rock que invoca el datepicker (por ejemplo, `"es-MX"` o `"es-PE"`), la normaliza a minusculas y extrae solo el codigo de dos letras (descartando el subtag regional). Asi `"es-MX"` se convierte en `"es"`.

**2. Registro inline del locale espanol**

```javascript
if (language === "es"
    && $.fn.datepicker
    && $.fn.datepicker.dates
    && !$.fn.datepicker.dates.es) {
    $.fn.datepicker.dates.es = {
        days: ["Domingo", "Lunes", "Martes", "Miercoles", "Jueves", "Viernes", "Sabado"],
        daysShort: ["Dom", "Lun", "Mar", "Mie", "Jue", "Vie", "Sab"],
        daysMin: ["Do", "Lu", "Ma", "Mi", "Ju", "Vi", "Sa"],
        months: ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                 "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"],
        monthsShort: ["Ene", "Feb", "Mar", "Abr", "May", "Jun",
                      "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"],
        today: "Hoy",
        clear: "Limpiar",
        titleFormat: "MM yyyy"
    };
}
```

Registra el locale `"es"` directamente en el objeto de datos de bootstrap-datepicker, solo si no estaba ya registrado (idempotente). Evita depender de un archivo locale externo adicional.

**3. Fallback a ingles**

```javascript
if (!($.fn.datepicker && $.fn.datepicker.dates && $.fn.datepicker.dates[language])) {
    language = "en";
}
```

Si el idioma solicitado no tiene un locale registrado en bootstrap-datepicker, cae de vuelta a ingles en lugar de generar un error silencioso.

**4. Aplicacion del idioma al datepicker**

```javascript
var datePicker = $datePickerInputGroup.datepicker({
    format: dateFormat,
    language: language,   // <- linea nueva
    assumeNearbyYear: 10,
    autoclose: true,
    ...
});
```

Pasa el `language` resuelto a la inicializacion del datepicker.

### Por que se hizo este cambio

El sitio de VidaReal opera en espanol. Sin este cambio, todos los calendarios de seleccion de fecha mostraban los nombres de dias y meses en ingles (Sun, Mon, Tue... January, February...). Esto afectaba la experiencia de cualquier bloque que usa un campo de fecha: formularios de registro a eventos, check-in, registro de familia, etc.

La solucion es self-contained (no requiere un archivo `.js` locale adicional en el bundle) y maneja correctamente tanto `"es"` como `"es-*"` (cualquier variante regional del espanol).

### Archivos relacionados

El control `datePicker.obs` en `Rock.JavaScript.Obsidian/Framework/Controls/datePicker.obs` y `datePickerBase.obs` tambien fueron modificados en este fork, probablemente para pasar la opcion `language` al script subyacente.
