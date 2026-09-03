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
| Aplicación (orquestación con PIMA, onboarding) | ✅ | |
| Persistencia, API, cliente HTTP a PIMA | ✅ | |
| Catálogo de categorías turísticas | ✅ | Con la regla tipo↔categoría |

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

- **Evaluación de insignia**: usuario entra a Platform → llama a BIT con su token de usuario → BIT pide un token de servicio a Platform → llama a PIMA → PIMA evalúa y devuelve hechos crudos → BIT decide la insignia y la registra con sus razones.
- Alta de perfil turístico, registro de prueba de vida, y lectura de la insignia vigente.

## Verificación

- `dotnet build Tourism.sln` — correcto, **0 warnings**
- `dotnet test Tourism.sln` — **68/68 en verde** (24 dominio, 27 aplicación, 17 infraestructura)
- `dotnet ef migrations has-pending-model-changes` — sin cambios pendientes
- **Verificado end-to-end con los tres servicios corriendo** contra MariaDB 11.4: el flujo completo devolvió insignia Bronce con score 750 y cobertura 30% — retenida por cobertura aunque el puntaje alcanzaba Plata, con ambas razones en la respuesta. El `correlationId` sobrevivió el salto a PIMA. Negativos: token de usuario presentado a PIMA → 401 por audiencia; usuario de otra organización → 403; **PIMA realmente caído → 503 y la insignia anterior intacta con sus razones**.

---

## Iteración 2 — onboarding turístico

`RegisterTourismProfileCommand` fue reemplazado por `OnboardOrganizationCommand`. No es un renombre: cierra la debilidad que el comando anterior tenía que admitir en su propia documentación.

### Lo que cambió y por qué

- **El tenant y la organización salen del token, no del request.** El comando anterior aceptaba un `TenantId` en el cuerpo porque no existía todavía un dueño almacenado contra el que comparar. Platform ahora emite tokens con scope de participación, y no emite uno para una participación retirada, una organización archivada o un tenant suspendido — así que el claim es una garantía *más fuerte* que cualquier consulta que BIT pudiera hacer, y se revalida en cada emisión y cada refresh. El request sigue nombrando la organización para que un desajuste se rechace en voz alta en vez de resolverse en silencio a lo que dijera el token.

- **El onboarding exige prueba de participación.** `ActsInTourismFor(organizationId)` compara el claim `org` con la organización pedida y el claim `bl` con `tourism`. Sin eso, BIT crearía una ficha turística para una empresa que nunca entró a la línea de negocio — exactamente la confusión entre "tiene cuenta" y "opera en turismo" que este corte existe para deshacer. Un token de participación de otra vertical no pasa.

- **`TourismScopeType` reconoce `BusinessLine`.** Estaba deliberadamente ausente mientras Platform no podía emitir ese token, con el argumento de que el enum no debía crecer casos que nadie leyera. Ahora es el scope que más importa acá, y un llamador de otra línea de negocio se reconoce y se rechaza en vez de tratarse como "sin scope".

- **El catálogo de categorías turísticas vive en BIT** (`TourismCategories`), con la regla tipo↔categoría que el legacy validaba **solo en uno de sus dos caminos**: su alta anónima guardaba el entero que llegara, incluido el `0`, que no existe en ningún catálogo. Acá hay una sola regla y todos los caminos pasan por ella. Las categorías se direccionan por código estable en vez de por id entero sembrado por una migración — el legacy incrustaba ese id dentro del identificador público, que es por lo que no podía reemitirse sin romper filas.

- **La primera evaluación se delega, no se reimplementa.** El onboarding manda `AssessOperatorBadgeCommand`: una sola implementación posee la orquestación con PIMA. Repetirla acá es como empiezan a divergir dos versiones de la misma decisión.

- **Un fallo de la evaluación no deshace el onboarding.** La organización entró a turismo de todas formas; rechazar el alta completa porque el motor de identidad parpadeó desharía trabajo que ya salió bien. La ficha queda sin insignia, con una nota que dice por qué — y "sin contactos que evaluar" y "el motor no respondió" son notas distintas, porque colapsarlas en un campo vacío es lo que dejaba al legacy sin poder distinguir una ficha sin revisar de una reprobada.

## Verificación — Iteración 2

- `dotnet build Tourism.sln` — correcto, **0 warnings**
- `dotnet test Tourism.sln` — **80/80 en verde** (24 dominio, 39 aplicación, 17 infraestructura)
- `dotnet ef migrations has-pending-model-changes` — sin cambios pendientes
- El contrato con Platform (los claims `org` y `bl`) está verificado del lado emisor sobre HTTP real: un token con scope de participación sale con `sco=businessline`, `sid`, `tnt`, `org` y `bl=tourism`. Del lado de BIT está cubierto por pruebas sobre el puerto, no por una corrida de los tres servicios en esta iteración.

---

## Iteración 4 — la posesión se pregunta, no se acepta

`AssessOperatorBadgeCommand` ya no tiene campo de posesión. Lo tenía, y eso significaba que el insumo más pesado de un score de identidad —el 30%— era lo que quien llamaba escribiera sobre sí mismo, sin nada en posición de contradecirlo. Ahora se le pregunta a Platform, que es el único servicio que puede decir si alguien probó controlar un contacto.

- **`IPossessionClient` es la segunda dirección de tráfico servicio-a-servicio de BIT.** Hasta acá solo le pedía un token a Platform; ahora también le hace una pregunta, y eso necesita una audiencia propia (`PimaPlatform.Services`), concedida por separado de la de PIMA. Poder evaluar una identidad nunca implicó poder leer lo que Platform sabe de un contacto.

- **Si Platform no responde, la evaluación sigue sin posesión.** El score descansa sobre menos evidencia y lo dice por su cobertura, que es honesto. Rechazar la evaluación entera dejaría que una dependencia caída impidiera evaluar una ficha; afirmar una posesión que nadie confirmó sería peor que las dos.

- **Cada contacto confirmado cuenta una vez.** Platform sabe que se probó y cuándo, no cuántas veces se volvió a probar. Afirmar más inflaría una dimensión que satura con las repeticiones.

El cliente HTTP tiene su propio typed client en vez de compartir el de emisión de tokens: una consulta de posesión lenta no puede demorar la emisión del token, de la que dependen todas las demás llamadas.

## Verificación — Iteración 4

- `dotnet build Tourism.sln` — correcto, **0 warnings**
- `dotnet test Tourism.sln` — **84/84 en verde** (24 dominio, 43 aplicación, 17 infraestructura)
- El lado emisor del contrato quedó verificado sobre HTTP real en Platform: un token de servicio con la audiencia de Platform recibe los hechos avalados, uno con la audiencia de PIMA recibe 401, y un token de usuario también. El lado de BIT está cubierto por pruebas sobre el puerto, no por una corrida de los tres servicios en esta iteración.
