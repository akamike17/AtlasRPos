# Atlas Restaurant POS — Seguridad

Complementa AGENTS.md.

## Fuente de confianza
El navegador nunca es fuente confiable para:
- identidad;
- empresa;
- sucursal;
- caja;
- sesión de caja;
- roles;
- precios;
- totales;
- estados;
- fechas del servidor.

Obtener server-side siempre que sea posible.

## Authentication
Mantener Cookie Authentication existente.
No sustituir esquema ni flujo sin autorización.
Mantener:
- LoginPath
- AccessDeniedPath
- HttpOnly
- SameSite
- SlidingExpiration

según configuración vigente.

## Authorization
Mantener autorización global existente.
Administración puede requerir roles.
Operación POS no debe convertirse automáticamente en exclusiva de Administrador.

## Fetch
Mantener:
- Fetch sin sesión → 401
- Fetch sin permiso → 403
- navegación normal sin sesión → redirect Login
- navegación sin permiso → Denegado

No retornar HTML inesperado a clientes esperando JSON.

## Antiforgery
Todo POST mutable debe validar antiforgery.
Fetch debe enviar token.
Nunca desactivar antiforgery para facilitar una prueba.

## Passwords
Usar IPasswordHasher<Usuario>.
Nunca:
- texto plano;
- PasswordHash en JSON;
- PasswordHash en auditoría;
- PasswordHash en logs;
- password en claims.

Edición:
password vacío → conservar hash.
password nuevo → generar nuevo hash.

## Secrets
Nunca almacenar secretos en:
- source code;
- appsettings;
- migraciones;
- auditoría;
- logs;
- claims;
- archivos de prueba persistentes.

En desarrollo utilizar User Secrets cuando corresponda.
Nunca mostrar sus valores en reportes.

## Claims
Usar claims server-side existentes.
No aceptar equivalentes manipulables desde formularios cuando el claim ya representa el contexto operativo.
No guardar información financiera o secretos en claims.

## Auditoría
Usar IAuditoriaService existente.
Snapshots deben excluir:
- Password
- PasswordHash
- tokens
- API keys
- ConnectionStrings
- secretos

## Errores
No devolver stack traces, SQL interno o excepciones completas al navegador.
Registrar técnicamente mediante ILogger cuando corresponda.
Respuesta al usuario:
mensaje controlado.

## Integraciones futuras
Credenciales de:
- APIs
- terminales
- facturación
- delivery
- proveedores externos

deben permanecer desacopladas de lógica de dominio y fuera del código fuente.