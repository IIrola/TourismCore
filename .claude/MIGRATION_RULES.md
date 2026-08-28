# Migración arquitectónica BIT → Platform / PIMA / BIT — Reglas invariantes

> Este archivo cambia poco y solo por decisión explícita del equipo. El progreso día a día vive en [MIGRATION_STATE.md](MIGRATION_STATE.md), no acá.

## Rol

Actúa como agente principal de ingeniería de software responsable de ejecutar una migración arquitectónica de gran alcance a partir del código fuente existente en **este repositorio (BitCore)**. Tu responsabilidad no es realizar cambios locales o mecánicos, sino comprender profundamente el sistema actual —sus flujos funcionales, modelos, contratos, persistencia, seguridad e integraciones— y reconstruirlos correctamente dentro de una nueva arquitectura formada inicialmente por tres APIs independientes:

- **Platform API**
- **PIMA API**
- **BIT API**

## Topología de repos (decisión confirmada)

| Bounded context | Repo | URL |
|---|---|---|
| Legacy (READ-ONLY, fuente de análisis) | BitCore | https://github.com/IIrola/bit-core |
| Platform API | PIMAPlatform | https://github.com/IIrola/PIMAPlatform |
| PIMA API | PIMACore | https://github.com/IIrola/PIMACore |
| BIT API (Turismo) | TourismCore | https://github.com/IIrola/TourismCore |

- **Platform, PIMA y BIT son repos separados entre sí y separados de este repo.** No es un monorepo.
- **BitCore (este repo) es el legacy.** Es la fuente de análisis; ninguna de las tres APIs nuevas vive acá.
- Cada repo nuevo ya tiene copiado este archivo (`MIGRATION_RULES.md`) en su propio `.claude/`, más su propio `MIGRATION_STATE.md` local. Si las reglas invariantes cambian, propagar el cambio manualmente a los tres repos — no hay import cross-repo automático.
- El tablero global de ownership de dominio y de flujos migrados (qué pertenece a quién, qué ya se completó end-to-end) se mantiene centralizado en el `MIGRATION_STATE.md` de **este** repo, porque es donde ocurre el análisis AS-IS. Cada repo nuevo lleva además su propio detalle interno de implementación en su `MIGRATION_STATE.md` local.

## Objetivo arquitectónico aprobado (no negociable)

1. BIT deja de ser la plataforma general.
2. BIT representa exclusivamente la vertical de negocio Turismo.
3. PIMA deja de pertenecer a BIT.
4. PIMA se convierte en un motor transversal de validación de identidad, evaluación, scoring y riesgo.
5. Usuarios, organizaciones, autenticación, autorización, tenancy, memberships y administración transversal no pertenecen a ninguna vertical.
6. Estas capacidades pertenecen a Platform.
7. Las futuras verticales —Automotriz, Fintech u otras— deberán poder incorporarse sin depender de BIT.
8. PIMA no debe contener reglas específicas de Turismo, Automotriz, Fintech ni de ninguna organización concreta.
9. Las verticales son responsables de interpretar los resultados de PIMA y aplicar sus decisiones de negocio.
10. Organization, Tenant, BusinessLine, User y Membership son conceptos independientes.
11. Partner no debe utilizarse como raíz universal del modelo organizacional. Debe analizarse como una relación, rol comercial o especialización dentro de una línea de negocio.
12. Una misma Organization podrá participar en múltiples BusinessLines.
13. Un User podrá pertenecer a múltiples Organizations y tener distintos permisos según Organization, Tenant y BusinessLine.
14. Los límites lógicos entre dominios deben preservarse también en persistencia, contratos y dependencias.

## Restricción fundamental sobre el legacy (BitCore = este repo)

El código de este repo debe considerarse **READ-ONLY**.

- No modificarlo.
- No refactorizarlo.
- No migrarlo incrementalmente dentro de sus propios proyectos.
- No utilizarlo como host temporal de los nuevos módulos.
- No agregar endpoints de compatibilidad.
- No introducir fachadas, proxies ni anti-corruption layers temporales destinadas solamente a mantener funcionando la implementación anterior.
- No mantener dos implementaciones de la misma capacidad.
- No dejar TODOs que indiquen que una funcionalidad será migrada posteriormente si esa funcionalidad pertenece al bloque funcional actualmente trabajado.

El código existente sirve como: especificación ejecutable parcial, referencia de reglas actuales, evidencia de comportamientos, fuente para identificar casos de uso, fuente para descubrir dependencias, fuente para reconstruir contratos, fuente para conocer persistencia e integraciones.

La nueva solución (en sus propios repos) debe construirse de forma independiente.

## Principio de migración: la unidad es el flujo funcional completo

La unidad de migración no es una clase, tabla, controller ni servicio. **La unidad de migración es un flujo funcional completo.**

Cuando se trabaje una capacidad, analizar y migrar de extremo a extremo:

```
API / contrato → autorización → aplicación → dominio → persistencia
→ integraciones → observabilidad → pruebas → configuración → documentación técnica necesaria
```

No dejar una capacidad repartida entre nueva arquitectura y legacy.

## Estrategia de trabajo (antes de escribir código, por cada bloque nuevo)

1. Inspeccionar exhaustivamente la estructura del proyecto original relevante al bloque.
2. Identificar todos los proyectos, módulos, servicios, controllers, entidades, repositorios, DbContexts, contratos, middlewares, configuración, autenticación, autorización, integraciones y jobs relevantes.
3. Reconstruir los flujos funcionales reales.
4. Identificar dependencias implícitas, side effects y reglas escondidas.
5. Clasificar las responsabilidades encontradas en: Platform / PIMA / BIT-Turismo / infraestructura compartida técnicamente justificable / comportamiento obsoleto o accidental que no debe trasladarse.
6. Detectar casos donde una clase tenga responsabilidades de más de un bounded context.
7. No trasladar esas clases literalmente: separar sus responsabilidades en el nuevo modelo.
8. Contrastar nombres actuales con su significado de negocio real.
9. No asumir que el modelo legacy representa correctamente las fronteras de negocio.
10. Usar el comportamiento existente como evidencia, no como diseño objetivo.
11. Registrar el resultado de este análisis (aunque sea parcial) en la tabla de inventario AS-IS→TO-BE de `MIGRATION_STATE.md` **antes** de implementar el bloque.

## Libertad del agente

Libertad para recorrer todo el código relacionado con el bloque trabajado y modificar simultáneamente todos los proyectos de la nueva solución que sean necesarios. No limitarse artificialmente a un archivo, una capa, un endpoint, una entidad o una historia aislada. Se puede trabajar Platform, PIMA y BIT en paralelo (en sus repos respectivos) cuando un flujo requiera cambios coordinados.

Preferir consistencia arquitectónica sobre cambios mínimos. Preferir completar vertical slices funcionales sobre producir muchos esqueletos incompletos.

## Fronteras iniciales de dominio

### Platform

Debe tender a ser propietaria de: Users, Authentication, Authorization, Credentials, Sessions/tokens cuando corresponda, Organizations, Tenants, BusinessLines, Memberships, OrganizationBusinessLine, Role/Permission/RoleAssignment, aprovisionamiento y administración transversal.

No debe absorber reglas propias de Turismo ni de PIMA.

### PIMA

Debe tender a ser propietario de: evaluación de identidad, validaciones de identidad, evidencia, resultados de validación, findings, scoring de identidad, scoring y evaluación de riesgo, profiles/policies de evaluación cuando sean parte del mecanismo de evaluación, integraciones externas estrictamente necesarias para producir una evaluación.

PIMA debe entregar hechos y resultados evaluativos. No debe decidir acciones comerciales propias de una vertical.

### BIT (Turismo)

Debe tender a ser propietario exclusivamente del dominio Turismo: workflows turísticos, perfiles turísticos de Organizations, reglas y restricciones propias del negocio turístico, configuración turística, decisiones derivadas de evaluaciones, casos de uso propios de Turismo, orquestación de PIMA necesaria para procesos turísticos.

## Integración entre APIs

- Evitar dependencias directas entre bases de datos.
- No crear foreign keys entre bounded contexts.
- Las referencias entre servicios deben realizarse mediante identificadores estables y contratos explícitos: `UserId`, `OrganizationId`, `TenantId`, `BusinessLineId`, `EvaluationId`, `CorrelationId`/`TraceId`.
- No duplicar entidades globales dentro de cada vertical. Una vertical puede almacenar una especialización local (ej. `TourismOrganizationProfile → OrganizationId`) pero no debe copiar el modelo completo de `Organization` como fuente de verdad propia.

## Seguridad

- Distinguir siempre: usuario → API, vs. servicio → servicio.
- Las comunicaciones machine-to-machine deben tener autenticación y autorización explícitas.
- No confundir identidad del usuario final con identidad del servicio llamante.
- La autorización debe poder considerar: User, Organization, Tenant, BusinessLine, Role/Permission.
- Evitar roles ambiguos globales como `Admin` cuando el scope sea relevante.

## Persistencia

- Cada bounded context evoluciona hacia ownership de sus propios datos.
- No usar tablas compartidas como contrato entre APIs.
- No depender de joins SQL entre Platform, PIMA y BIT.
- No migrar tablas mecánicamente: primero determinar qué contexto es propietario real de cada dato.

## Compatibilidad

Esta solución no requiere compatibilidad runtime con el backend anterior. Por tanto:

- No diseñar endpoints legacy.
- No mantener DTOs únicamente porque el API anterior los usaba.
- No mantener nombres incorrectos únicamente por compatibilidad.
- No generar translation layers temporales.
- No crear flags para alternar entre implementación vieja y nueva.

Sí se deben preservar reglas de negocio válidas y comportamientos funcionales requeridos.

## Calidad — checklist por cada bloque implementado

- Compila toda la nueva solución (del repo correspondiente).
- Ejecuta las pruebas relevantes.
- Agrega pruebas nuevas cuando sean necesarias.
- Corrige warnings significativos.
- Valida DI.
- Valida configuración.
- Valida migrations.
- Valida contratos.
- Valida scopes de autorización.
- Revisa dependencias entre proyectos.
- Elimina código nuevo muerto o duplicado.
- Evita placeholders ficticios.

## Manejo de contradicciones

Si se detecta una contradicción importante entre el comportamiento legacy y las fronteras aprobadas en este documento: **preservar las fronteras aprobadas** y rediseñar el comportamiento correctamente dentro del contexto propietario correcto.

## Cierre de iteración — qué reportar siempre

Al terminar cada iteración, reportar (y volcar el resumen equivalente en `MIGRATION_STATE.md`):

- Qué se analizó.
- Qué decisiones de ownership se confirmaron.
- Qué flujos completos quedaron implementados end-to-end.
- Qué proyectos/repos fueron creados o modificados.
- Qué pruebas se ejecutaron.
- Qué riesgos se descubrieron.
- Qué bloque coherente debe continuar a continuación.

No reportar solamente archivos modificados. Reportar avance funcional y arquitectónico.
