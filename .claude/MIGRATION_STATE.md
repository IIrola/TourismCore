# Estado de la migración — vista local de TourismCore (BIT API)

> El tablero **global** de la migración (inventario AS-IS→TO-BE, decisiones de ownership confirmadas, riesgos cross-repo) vive en el repo `BitCore` (legacy), en `.claude/MIGRATION_STATE.md`. Este archivo es solo para el detalle de implementación **local** a este repo.

## Estado de implementación

**Iteración 1, bloque 1 — dominio turístico: ✅ completo.**

Estructura: `Api → Infrastructure → Application → Domain`, misma forma que Platform y PIMA.

| Pieza | Estado | Nota |
|---|---|---|
| `TourismOrganizationProfile` | ✅ | Especialización por `OrganizationId`, sin duplicar Organization |
| `TourismProfileType` | ✅ | Turista / Operador, recuperado del legacy |
| `TourismBadge` + `BadgeAssessment` | ✅ | La decisión turística, como función pura |
| `IdentityAssessment` | ✅ | Forma propia de BIT del resultado de PIMA |
| Aplicación (orquestación con PIMA) | ⬜ | |
| Persistencia, API, cliente HTTP a PIMA | ⬜ | |

## Decisiones locales de implementación

- **Sin referencia de proyecto a PIMA ni a Platform.** BIT depende de la *forma* de una respuesta, no del ensamblado que la produjo; `IdentityAssessment` es un tipo propio. Un cambio interno de PIMA no puede romper el build acá.

- **BIT no duplica `Organization`.** `TourismOrganizationProfile` se indexa por `OrganizationId` y solo guarda atributos genuinamente turísticos. Razón social, identificador fiscal y país son de Platform. El legacy tenía la clasificación turística sobre el registro de identidad, y por eso no se podía tocar identidad sin tocar turismo.

- **La decisión del escudo vive acá, no en el motor.** PIMA dice qué sostiene la evidencia y se detiene; qué significa eso para una ficha turística es un juicio de negocio y pertenece a la vertical que debe defenderlo. En el legacy ambas cosas estaban fundidas dentro del servicio de scoring, y por eso el motor nunca fue reusable: no solo medía identidad, decidía una insignia turística y la recortaba por plan.

- **Una evaluación inconclusa da `Undetermined`, no la peor insignia.** En el legacy la ausencia de evidencia y la peor evidencia salían ambas como nivel 0: un operador recién registrado que nadie había revisado se publicaba igual que uno revisado y reprobado. Son afirmaciones distintas.

- **La cobertura limita hasta dónde llega la insignia.** Un puntaje casi perfecto sacado de una señal de cinco no es la misma afirmación que el mismo puntaje sacado de las cinco. El legacy no podía distinguirlos porque nunca publicó cobertura.

- **La prueba de vida limita la insignia.** Recuperado del *proof of life* del legacy: en turismo una ficha que se apagó es un riesgo propio, más allá de lo bien que verificara su identidad en su momento.

- **`Platinum` no se otorga.** Era el nivel con puerta comercial del legacy (requería cierto plan). Si un plan puede levantar una insignia es una decisión de negocio todavía abierta, así que nada acá lo concede.

- **Toda decisión se explica.** `BadgeDecision` devuelve las razones. Una decisión de negocio que no puede decir por qué es el problema del legacy repetido: su escudo era un número en una fila sin registro de cómo llegó ahí.

## Flujos migrados a este repo (end-to-end)

_(ninguno todavía — está la regla de decisión, falta la orquestación con PIMA y la superficie de API)_

## Verificación

- `dotnet build Tourism.sln` — correcto, **0 warnings**
- `dotnet test Tourism.sln` — **24/24 en verde**
