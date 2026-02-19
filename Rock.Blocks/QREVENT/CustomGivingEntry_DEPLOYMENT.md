# Custom Giving Entry - Deployment & QA Guide

## 📋 Resumen Ejecutivo

Bloque Obsidian custom para procesamiento de donaciones nativo en Rock RMS 15.5.

**Características:**
- ✅ One-time y scheduled transactions
- ✅ Business giving
- ✅ Anonymous giving
- ✅ Gateway tokenization nativa
- ✅ Logging estructurado para troubleshooting
- ✅ Validaciones robustas de seguridad
- ✅ Manejo de errores con prevención de orphan charges

---

## 🚀 Pasos de Despliegue

### Pre-requisitos

- [ ] Rock RMS 15.5 o superior
- [ ] Acceso a la solución Visual Studio de Rock
- [ ] Acceso SQL a la base de datos Rock
- [ ] Financial Gateway configurado en Rock (ej: CyberSource, NMI, etc.)
- [ ] Al menos una cuenta financiera activa
- [ ] System Communication configurado para receipts (opcional)

### Paso 1: Compilar el Código

1. **Agregar archivos al proyecto:**
   ```
   Rock.Blocks/QREVENT/CustomGivingEntry.cs
   Rock.JavaScript.Obsidian.Blocks/src/QREVENT/CustomGivingEntry.obs
   ```

2. **Actualizar Rock.Blocks.csproj:**
   ```xml
   <Compile Include="QREVENT\CustomGivingEntry.cs" />
   ```

3. **Actualizar Rock.JavaScript.Obsidian.Blocks.njsproj:**
   ```xml
   <Content Include="src\QREVENT\CustomGivingEntry.obs" />
   ```

4. **Compilar la solución:**
   ```bash
   # En Visual Studio: Build > Build Solution
   # O desde CLI:
   msbuild Rock.sln /p:Configuration=Release
   ```

5. **Compilar los bloques Obsidian:**
   ```bash
   cd Rock.JavaScript.Obsidian.Blocks
   npm install
   npm run build
   ```

6. **Verificar que no hay errores de compilación.**

### Paso 2: Ejecutar Migración SQL

1. **Ubicar el archivo:**
   ```
   Rock.Blocks/QREVENT/CustomGivingEntry_Migration.sql
   ```

2. **Ejecutar en SQL Server Management Studio:**
   - Conectar a la base de datos Rock
   - Abrir `CustomGivingEntry_Migration.sql`
   - Ejecutar el script (F5)
   - Verificar mensaje: "Custom Giving Entry BlockType migration completed successfully"

3. **Verificar en Rock:**
   - Navegar a: **CMS Configuration > Block Types**
   - Buscar: "Custom Giving Entry"
   - Debe aparecer en la categoría "Custom"

### Paso 3: Desplegar Archivos

**Opción A: Deployment desde Visual Studio**
- Build > Publish
- Los archivos se copian automáticamente a `RockWeb/`

**Opción B: Deployment Manual**
1. Copiar `Rock.Blocks.dll` a `RockWeb/bin/`
2. Copiar archivos compilados de Obsidian a `RockWeb/Themes/Rock/Scripts/Obsidian/`

### Paso 4: Configurar el Bloque

1. **Crear o navegar a la página donde quieres el bloque**
   - Ej: External Website > Give

2. **Agregar el bloque:**
   - Click en: **Zone > Add Block**
   - Buscar: "Custom Giving Entry"
   - Click: **Add**

3. **Configurar atributos del bloque:**

   | Atributo | Requerido | Ejemplo |
   |----------|-----------|---------|
   | Financial Gateway | ✅ Sí | CyberSource Gateway |
   | Accounts | ✅ Sí | General Fund, Building Fund |
   | Source | ✅ Sí | Website |
   | Batch Name Prefix | No | Online Giving |
   | Enable ACH | No | True |
   | Enable Credit Card | No | True |
   | Allow Scheduled | No | True |
   | Enable Business Giving | No | True |
   | Enable Anonymous Giving | No | False |
   | Enable Comment Entry | No | False |
   | Receipt Email | No | Contribution Receipt |
   | Connection Status | ✅ Sí | Prospect |
   | Record Status | ✅ Sí | Pending |

4. **Guardar configuración**

5. **Verificar que el bloque aparece en la página**

---

## ✅ Checklist de QA Manual

### Test Suite 1: One-Time Transactions

- [ ] **TC-001: One-Time Gift - Person (Authenticated)**
  - Login como usuario existente
  - Ingresar monto (ej: $50)
  - Seleccionar campus
  - Seleccionar frecuencia "One Time"
  - Ingresar datos de tarjeta (usar test card del gateway)
  - Completar donación
  - **Esperado:** Transacción creada en Financial > Transactions
  - **Esperado:** Batch creado/actualizado correctamente
  - **Esperado:** Receipt enviado si está configurado

- [ ] **TC-002: One-Time Gift - Person (Anonymous)**
  - NO hacer login
  - Ingresar monto
  - Completar pasos hasta datos del donante
  - Ingresar: Nombre, Apellido, Email, Teléfono
  - Completar pago
  - **Esperado:** Person creado en Rock con email
  - **Esperado:** Transaction asociada al person
  - **Esperado:** Connection Status = configurado
  - **Esperado:** Record Status = configurado

- [ ] **TC-003: Multiple Accounts**
  - Ingresar montos en 2+ accounts diferentes
  - Completar donación
  - **Esperado:** Transaction Details correctos para cada account
  - **Esperado:** Total correcto

- [ ] **TC-004: Anonymous Gift Flag**
  - Habilitar "Enable Anonymous Giving" en block settings
  - Hacer donación con "Anónimo" activado
  - **Esperado:** Transaction.ShowAsAnonymous = true

### Test Suite 2: Scheduled Transactions

- [ ] **TC-005: Scheduled Gift - Monthly**
  - Seleccionar frecuencia "Monthly"
  - Seleccionar fecha de inicio (futuro)
  - Completar pago
  - **Esperado:** FinancialScheduledTransaction creado
  - **Esperado:** No se crea FinancialTransaction inmediatamente
  - **Esperado:** Gateway tiene scheduled payment activo

- [ ] **TC-006: Scheduled Gift - Weekly**
  - Seleccionar frecuencia "Weekly"
  - Completar pago
  - **Esperado:** Frecuencia correcta en scheduled transaction

### Test Suite 3: Business Giving

- [ ] **TC-007: Business Gift - New Business**
  - NO hacer login
  - Cambiar a "Negocio"
  - Ingresar nombre del negocio
  - Ingresar datos de contacto
  - Completar pago
  - **Esperado:** Person tipo Business creado
  - **Esperado:** Person tipo Person creado como contacto
  - **Esperado:** Relación Known Relationship Business Contact creada
  - **Esperado:** Transaction asociada al Business

- [ ] **TC-008: Business Gift - Existing Contact**
  - Login como usuario
  - Cambiar a "Negocio"
  - Ingresar nombre de negocio existente asociado al usuario
  - Completar pago
  - **Esperado:** Se usa Business existente
  - **Esperado:** No se crea duplicado

### Test Suite 4: Error Handling

- [ ] **TC-009: Invalid Email**
  - Ingresar email inválido (ej: "test@")
  - **Esperado:** Error de validación en frontend

- [ ] **TC-010: Amount Zero**
  - Dejar todos los montos en $0
  - Click "Continuar"
  - **Esperado:** Error "Debes capturar al menos un monto mayor a 0"

- [ ] **TC-011: Gateway Decline**
  - Usar tarjeta de test que el gateway rechaza
  - **Esperado:** Error de gateway mostrado al usuario
  - **Esperado:** NO se crea transaction en Rock
  - **Esperado:** Log de error en System > Logs

- [ ] **TC-012: Duplicate Transaction**
  - Intentar procesar el mismo transaction GUID dos veces
  - **Esperado:** Error "A transaction with this unique identifier already exists"

- [ ] **TC-013: Scheduled Date in Past**
  - Seleccionar scheduled con fecha en el pasado
  - **Esperado:** Error de validación

### Test Suite 5: Security

- [ ] **TC-014: XSS Prevention**
  - Ingresar `<script>alert('XSS')</script>` en campos de texto
  - **Esperado:** Script sanitizado, no ejecutado

- [ ] **TC-015: SQL Injection Prevention**
  - Ingresar `'; DROP TABLE Person; --` en campos
  - **Esperado:** Input sanitizado, no afecta la BD

- [ ] **TC-016: Invalid Account**
  - Modificar request en browser DevTools para incluir account no permitido
  - **Esperado:** Error "Account key 'X' is not allowed in this block"

### Test Suite 6: Edge Cases

- [ ] **TC-017: Large Amount**
  - Ingresar monto muy grande (ej: $999,999.99)
  - **Esperado:** Procesa correctamente (o error si gateway tiene límite)

- [ ] **TC-018: Special Characters in Names**
  - Ingresar nombres con acentos, ñ, apóstrofes (ej: "O'Brien")
  - **Esperado:** Se guardan correctamente sin error

- [ ] **TC-019: Long Comment**
  - Habilitar comments
  - Ingresar comment de 500+ caracteres
  - **Esperado:** Se trunca a 500 caracteres

---

## 🔍 Troubleshooting

### Problema: "Block not found"
**Síntoma:** El bloque no aparece en la lista de Block Types

**Solución:**
1. Verificar que ejecutaste la migración SQL
2. Verificar en SQL: `SELECT * FROM BlockType WHERE Name = 'Custom Giving Entry'`
3. Si no existe, ejecutar nuevamente `CustomGivingEntry_Migration.sql`

### Problema: "Gateway not configured"
**Síntoma:** Error al intentar hacer donación

**Solución:**
1. Verificar que el atributo "Financial Gateway" está configurado en block settings
2. Verificar en SQL: `SELECT * FROM FinancialGateway WHERE IsActive = 1`
3. Asegurar que el gateway component implementa `IObsidianHostedGatewayComponent`

### Problema: Orphan Charge
**Síntoma:** Gateway cobró pero no hay transaction en Rock

**Solución:**
1. Buscar en System > Logs el mensaje CRITICAL con el transaction GUID
2. El log incluye: `TransactionGuid`, `TransactionCode`, `PersonId`, `Amount`
3. Crear manualmente la transaction en Rock usando esos datos
4. O hacer refund en el gateway si es necesario

**Logs a buscar:**
```sql
SELECT * FROM ExceptionLog
WHERE Description LIKE '%CRITICAL: Gateway charge succeeded%'
ORDER BY CreatedDateTime DESC
```

### Problema: Receipt no enviado
**Síntoma:** Donación procesada pero no llega email

**Solución:**
1. Verificar que "Receipt Email" está configurado en block settings
2. Verificar System Communication está activo
3. Verificar en Rock > System > System Jobs que "Send Email" está corriendo
4. Verificar en Communication > Communication History

### Problema: Compilation Errors
**Síntoma:** Errores al compilar el bloque

**Solución:**
1. Verificar que todas las referencias de NuGet están actualizadas
2. Limpiar solución: Build > Clean Solution
3. Rebuild: Build > Rebuild Solution
4. Verificar que Rock.Logging namespace está disponible (Rock 15.5+)

---

## 📊 Logs y Monitoreo

### Logs Importantes

El bloque genera logs estructurados en los siguientes puntos:

**Info Level:**
```
"Starting ProcessGiving. TransactionGuid: {guid}, IsScheduled: {bool}, TotalAmount: {decimal}"
"Creating scheduled payment at gateway. TransactionGuid: {guid}, PersonId: {int}, Amount: {decimal}"
"Gateway scheduled payment created successfully. TransactionGuid: {guid}, GatewayScheduleId: {string}"
"Scheduled transaction saved to Rock database. TransactionGuid: {guid}"
"Charging payment at gateway. TransactionGuid: {guid}, PersonId: {int}, Amount: {decimal}"
"Gateway charge succeeded. TransactionGuid: {guid}, TransactionCode: {string}"
"Transaction saved to Rock database. TransactionGuid: {guid}, TransactionCode: {string}"
```

**Warning Level:**
```
"ProcessGiving called with null bag"
"ProcessGiving called with empty transaction GUID"
"Invalid email format. TransactionGuid: {guid}, Email: {email}"
```

**Error Level:**
```
"Gateway failed to create scheduled payment. TransactionGuid: {guid}, Error: {message}"
"Gateway charge failed. TransactionGuid: {guid}, Error: {message}"
"Unexpected error processing gift. TransactionGuid: {guid}, PersonId: {int}, Error: {message}"
```

**Critical Level (⚠️ REQUIERE ACCIÓN INMEDIATA):**
```
"CRITICAL: Gateway scheduled payment succeeded but Rock database save failed. ORPHAN SCHEDULED PAYMENT.
 TransactionGuid: {guid}, GatewayScheduleId: {id}, PersonId: {int}, Amount: {decimal}, Error: {message}"

"CRITICAL: Gateway charge succeeded but Rock database save failed. ORPHAN CHARGE.
 TransactionGuid: {guid}, TransactionCode: {code}, PersonId: {int}, Amount: {decimal}, Error: {message}"
```

### Consultas SQL Útiles

**Transacciones del día:**
```sql
SELECT
    t.Id,
    t.Guid,
    t.TransactionDateTime,
    t.TotalAmount,
    p.NickName + ' ' + p.LastName AS Person,
    t.TransactionCode
FROM FinancialTransaction t
INNER JOIN PersonAlias pa ON t.AuthorizedPersonAliasId = pa.Id
INNER JOIN Person p ON pa.PersonId = p.Id
WHERE CAST(t.TransactionDateTime AS DATE) = CAST(GETDATE() AS DATE)
ORDER BY t.TransactionDateTime DESC
```

**Scheduled Transactions activos:**
```sql
SELECT
    st.Id,
    st.Guid,
    st.StartDate,
    st.TotalAmount,
    p.NickName + ' ' + p.LastName AS Person,
    dv.Value AS Frequency,
    st.IsActive
FROM FinancialScheduledTransaction st
INNER JOIN PersonAlias pa ON st.AuthorizedPersonAliasId = pa.Id
INNER JOIN Person p ON pa.PersonId = p.Id
INNER JOIN DefinedValue dv ON st.TransactionFrequencyValueId = dv.Id
WHERE st.IsActive = 1
ORDER BY st.StartDate DESC
```

**Orphan Charges (logs críticos):**
```sql
SELECT TOP 20
    CreatedDateTime,
    Description,
    ExceptionType
FROM ExceptionLog
WHERE Description LIKE '%CRITICAL:%'
ORDER BY CreatedDateTime DESC
```

---

## ⚠️ Riesgos Restantes

### Riesgo 1: Orphan Charges (MITIGADO)
**Descripción:** Si el gateway cobra exitosamente pero falla SaveChanges en Rock, se crea un cargo huérfano.

**Mitigación Implementada:**
- Logging CRITICAL con todos los detalles para reconciliación manual
- Try-catch granular alrededor de SaveChanges
- Transaction GUID único previene duplicados

**Acción Requerida:**
- Monitorear logs CRITICAL diariamente
- Crear proceso de reconciliación manual si ocurre
- Considerar implementar retry logic en versión futura

**Severidad:** MEDIA (ocurrencia rara, pero impacto alto)

### Riesgo 2: Gateway Component Compatibility
**Descripción:** El bloque asume que el gateway implementa `IObsidianHostedGatewayComponent`.

**Mitigación Implementada:**
- Validación al inicio de ProcessGiving
- Error claro si gateway no es compatible

**Acción Requerida:**
- Verificar que tu gateway específico (CyberSource, NMI, etc.) es compatible
- Probar con gateway real antes de producción

**Severidad:** BAJA (se detecta temprano)

### Riesgo 3: Concurrencia en Creación de Person
**Descripción:** Si dos requests simultáneos intentan crear el mismo person (mismo email), podría haber duplicados.

**Mitigación Parcial:**
- `PersonService.FindPerson` busca antes de crear
- Unique index en Person.Email en Rock previene duplicados

**Acción Requerida:**
- Ninguna acción requerida si Rock tiene unique index en email
- Considerar implementar distributed lock si se observa problema

**Severidad:** BAJA (Rock maneja esto nativamente)

### Riesgo 4: Input Validation Bypass
**Descripción:** Usuario malicioso podría modificar request en browser DevTools.

**Mitigación Implementada:**
- Todas las validaciones críticas están en backend
- Sanitización de inputs en backend
- Validación de accounts permitidos

**Acción Requerida:**
- Ninguna, las validaciones son server-side

**Severidad:** BAJA (bien mitigado)

### Riesgo 5: Rate Limiting
**Descripción:** No hay rate limiting implementado, posible abuso.

**Mitigación:**
- NO IMPLEMENTADA en este bloque
- Depende de Rock o IIS/firewall

**Acción Requerida:**
- Considerar implementar rate limiting a nivel IIS o firewall
- Monitorear uso excesivo en logs

**Severidad:** MEDIA (si es sitio público)

---

## 📈 Métricas de Éxito

Después de deployment, monitorear:

- [ ] **Transaction Success Rate:** ≥ 95%
  - Query: `SELECT COUNT(*) WHERE TransactionCode IS NOT NULL / COUNT(*) total attempts`

- [ ] **Gateway Error Rate:** ≤ 5%
  - Buscar logs: "Gateway charge failed"

- [ ] **Orphan Charge Incidents:** 0
  - Buscar logs CRITICAL diarios

- [ ] **Average Processing Time:** ≤ 5 segundos
  - Desde click "Procesar donación" hasta success message

- [ ] **User Drop-off Rate:** ≤ 20%
  - Google Analytics o similar

---

## 🎯 Post-Deployment Checklist

Después de deployment exitoso:

- [ ] Ejecutar todos los test cases de QA
- [ ] Hacer una donación real de $1 (usar tarjeta real)
- [ ] Verificar que el $1 llegó a la cuenta financiera
- [ ] Verificar que el receipt se envió (si configurado)
- [ ] Agregar monitoreo de logs CRITICAL a dashboard de soporte
- [ ] Documentar proceso de reconciliación de orphan charges
- [ ] Capacitar a equipo de soporte sobre troubleshooting
- [ ] Configurar alertas para logs CRITICAL (opcional)
- [ ] Establecer revisión semanal de métricas

---

## 📞 Soporte

**Logs:** System > Logs (buscar "CustomGivingEntry")
**Transactions:** Finance > Transactions
**Scheduled:** Finance > Scheduled Transactions
**Block Settings:** Page > Zone > Block Settings

**Para desarrollo adicional:**
- Backend: `Rock.Blocks/QREVENT/CustomGivingEntry.cs`
- Frontend: `Rock.JavaScript.Obsidian.Blocks/src/QREVENT/CustomGivingEntry.obs`
- Migración: `Rock.Blocks/QREVENT/CustomGivingEntry_Migration.sql`

---

**Versión:** 1.0
**Última actualización:** 2026-02-14
**Compatible con:** Rock RMS 15.5+
**Autor:** Custom Development Team
