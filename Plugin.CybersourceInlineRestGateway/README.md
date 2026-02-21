# Cybersource Inline REST Gateway (Rock 18.1)

## Ubicacion
- Solucion: `CybersourceInlineRestGateway.sln`
- Proyecto: `CybersourceInlineRestGateway/CybersourceInlineRestGateway.csproj`
- Codigo: `CybersourceInlineRestGateway/CybersourceInlineRestGateway.cs`

## Build
```powershell
dotnet build .\CybersourceInlineRestGateway.sln -c Release
```

## Output DLL
`CybersourceInlineRestGateway\bin\Release\net472\CybersourceInlineRestGateway.dll`

## Carpeta Deploy
`Deploy\`

Incluye:
- `CybersourceInlineRestGateway.dll`
- `CybersourceInlineRestGateway.pdb`
- DLLs de referencia copiadas (Rock/Newtonsoft/etc) para empaquetado.

## Nota funcional
Esta base implementa:
- Charge one-time inline con tarjeta
- Credit/refund basico

Y deja como NO implementado por ahora:
- Scheduled payments
- AutomatedCharge con ReferencePaymentInfo
