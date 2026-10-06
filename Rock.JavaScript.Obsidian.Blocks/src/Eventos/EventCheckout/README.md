# EventCheckout — partials del checkout

Patrón `Event/RegistrationEntry`: el shell `../eventCheckout.obs` crea el estado con
`provideCheckoutState()` y contiene TODO el `<style>` (NO-scoped a propósito: estiliza también a
estos partials); cada paso lo inyecta con `useCheckoutState()` y destructura solo lo que su
template usa.

| Archivo | Qué es |
|---|---|
| `checkoutState.partial.ts` | Composable maestro: wizard, hold/timer, promo, NIT, pasarela, submit. `CheckoutState = ReturnType<...>` ⇒ destructuring typecheckeado. |
| `attendeeState.partial.ts` | Sub-composable de asistentes/preguntas (unidades, prefill, invitados, validación, `buildLines`). Se expone vía `...attendee` dentro del estado. |
| `types.partial.ts` | Tipos espejo de los bags C# + `guestValue`. |
| `ticketsStep` / `attendeesStep` / `reviewStep` / `paymentStep` / `doneStep` `.partial.obs` | Template de cada paso + destructuring del estado. |

⚠️ **El build NO typecheckea bindings de template.** Tras tocar un partial, compila
(`npm run build-fast`) y verifica que el bundle NO contenga `_ctx.` (identificador sin binding)
ni `resolveComponent` (componente sin importar):

```powershell
Select-String -Path ..\..\..\..\RockWeb\Obsidian\Blocks\Eventos\eventCheckout.obs.js -Pattern '_ctx\.|resolveComponent'
# Sin resultados = todos los nombres del template resolvieron.
```

⚠️ **Texto dinámico ⇒ `<span class="notranslate">`.** El traductor del sitio (VidaRealTranslator)
reemplaza nodos de texto y Vue sigue actualizando el viejo: un `{{ busy ? "Procesando…" : "Pagar" }}`
sin protección se queda mostrando "Procesando…" con el botón habilitado. Cualquier texto nuevo que
dependa del estado va envuelto:

```html
<span class="notranslate">{{ busy ? "Reservando…" : "Continuar" }}</span>
```

**Moneda**: usar `formatCurrency` del estado (`Q175.00`, sin espacio); no formatear con `Intl`
directo en un partial.

Arquitectura completa del módulo: `Rock/Model/Eventos/ARCHITECTURE.md`.
