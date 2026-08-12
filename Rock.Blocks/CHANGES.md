# Rock.Blocks — Cambios y personalizaciones VidaReal

## Contexto general

`Rock.Blocks` contiene los **bloques de servidor Obsidian** (ASP.NET / C#) del CMS Rock RMS.
Cada clase en este proyecto es un `RockBlockType` que expone:

- **`GetObsidianBlockInitialization()`** — inicializa el estado del bloque y lo envía al frontend Vue/Obsidian.
- **`[BlockAction]` methods** — endpoints invocados desde el frontend mediante `invokeBlockAction`.
- **Atributos de bloque** — configuración declarativa (páginas enlazadas, textos, flags).

Los bloques Obsidian reemplazan a los WebForms tradicionales (`.ascx`). Rock 18.x continúa el proceso de "chop" (migración WebForms → Obsidian).

---

## Rama de trabajo

- Repositorio: fork de `SparkDevNetwork/Rock`
- Rama: `hotfix-18.1`
- Organización: VidaReal (iglesia)

---

## Resumen de cambios en Rock.Blocks

| Archivo | Tipo de cambio | Descripción |
|---|---|---|
| `Security/Login.cs` | Modificacion upstream | Se agrega `Category = p.PhotoUrl` al mapeo de personas en seleccion passwordless |
| `Security/VRSimpleRegistration.cs` | Nuevo — personalización VidaReal | Bloque de registro simplificado post-validacion passwordless (ver detalle abajo) |
| `Dar/CybersourceDonationEntry.cs` | Modificacion previa | Bloque de donacion con Cybersource (rama anterior, no en esta sesion) |
| `Wallet/PaseDigital.cs` | Nuevo — personalización VidaReal | Tarjeta web del pase digital de la iglesia (diseño del pase de wallet: navy, "Asisto a" = campus de la persona, QR = Alternate Id). Imágenes leídas de la WalletTemplate. Botón "Guardar en mi teléfono" oculto por defecto (BooleanField para reactivarlo; emite el pase real al clic). GUID BlockType `f0a1b2c3-d4e5-4f60-8a01-950000000002`, registrado en migración 015 y renombrado en la 016 del plugin Wallet (antes `VidaAventuraPass`). Frontend: `src/Wallet/paseDigital.obs` |

---

## Bloques personalizados VidaReal

### VRSimpleRegistration

Bloque completamente nuevo, creado por VidaReal. No existe en el Rock original.

- **Namespace:** `Rock.Blocks.Security`
- **Categoria en Rock:** `VidaReal > Security`
- **GUID bloque:** `61C805E0-F228-4DCA-9934-3F12FEC67C7D`
- **GUID EntityType:** `E0AE2775-BFB2-4F28-A7C3-9FC968C42A86`
- **Archivo frontend esperado:** `Rock.JavaScript.Obsidian.Blocks/src/Security/vrSimpleRegistration.obs` (a crear)
- **Proposito:** Ver `Security/CHANGES.md`

---

## Notas para futuras sesiones

- Los bloques con prefijo `VR` son creaciones propias de VidaReal, no modificaciones de Rock.
- El bloque `Login.cs` es el original de Rock con un cambio minimo documentado en `Security/CHANGES.md`.
- El resto de archivos en `Security/` (AccountEntry, ConfirmAccount, ForgotUserName, LoginHistory, etc.) son Rock puro sin modificar.
