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

Arquitectura completa del módulo: `Rock/Model/Eventos/ARCHITECTURE.md`.
