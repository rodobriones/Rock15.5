# Rock Core Library — Cambios VidaReal

## Qué es este proyecto

`Rock/` es la librería núcleo (core) de **Rock CMS**, el sistema de gestión de iglesias desarrollado por Spark Development Network. Contiene los modelos de dominio, servicios de datos, lógica de negocio y proveedores de seguridad que sustentan toda la plataforma.

Este fork (`hotfix-18.1`) es mantenido por **VidaReal**, una iglesia latinoamericana, y extiende o modifica el core de Rock para adaptarlo a sus necesidades operativas (idioma español, formatos de teléfono latinoamericanos, UX personalizada).

---

## Advertencia: cambios al core son de alto impacto

> **ATENCION:** Los archivos dentro de `Rock/` son la base de toda la plataforma. Un cambio incorrecto aquí puede afectar la autenticación de todos los usuarios, el envío de OTPs, los bloques de login, las APIs v2 y cualquier bloque Obsidian que dependa de estos servicios. Revisar con cuidado antes de hacer merge hacia `develop`.

---

## Resumen de cambios realizados al core

Todos los cambios listados abajo son **modificaciones no comprometidas** (working tree) sobre la base del tag/branch `hotfix-18.1` del upstream de Spark. Son personalizaciones propias de VidaReal y **no deben fusionarse ciegamente** con actualizaciones futuras de upstream sin resolver conflictos.

### Area: Autenticación sin contraseña (Passwordless / OTP)

Los tres archivos modificados forman un conjunto coherente: mejoran el flujo de inicio de sesión sin contraseña para usuarios latinoamericanos con números de teléfono que incluyen código de país, y añaden la foto de perfil al paso de selección de persona cuando hay múltiples coincidencias.

---

## Tabla de archivos modificados

| Archivo | Lineas cambiadas | Categoria | Impacto |
|---|---|---|---|
| `Rock/Security/Authentication/PasswordlessAuthentication.cs` | ~60 lineas | Logica principal | Alto — afecta todo el flujo OTP |
| `Rock/Model/Security/RemoteAuthenticationSessionService.cs` | 5 lineas | Generador de codigos | Medio — cambia el formato del codigo OTP |
| `Rock/Security/Authentication/OneTimePasscode/MatchingPersonResult.cs` | 5 lineas | Modelo de datos | Bajo — agrega propiedad `PhotoUrl` |

Para el detalle tecnico de cada archivo, ver:
- [`Rock/Security/Authentication/CHANGES.md`](Security/Authentication/CHANGES.md)
