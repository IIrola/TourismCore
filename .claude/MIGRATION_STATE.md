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

---

## Iteración 5 — la insignia tiene que mirar las denuncias

`BadgeAssessment.Decide` toma `ReportStanding` como **parámetro obligatorio, sin valor por defecto**. Una vertical que se olvide de mirar si hay una denuncia sostenida contra un operador publicaría una insignia sobre evidencia de identidad sola — y con un parámetro obligatorio, no compila.

- **Una denuncia sostenida deja al operador sin insignia**, cualquiera sea su score, y se decide antes de leerlo. Deliberadamente distinto del legacy, que hacía esto *dentro del motor de scoring*, para toda la plataforma, y con denuncias que nadie había revisado: una acusación de esa mañana era indistinguible de una probada, y toda vertical heredaba el veredicto estuviera o no de acuerdo. Acá es una regla de turismo, aplicada solo a lo que un revisor sostuvo, y otra vertical puede pesar el mismo hecho distinto.
- **Una denuncia sin revisar retiene el Oro en Plata, sin quitar nada.** Un reclamo que nadie decidió es razón para no hacer todavía la afirmación pública más fuerte, no para quitar una insignia. Si no, cualquiera con cuenta de reporte le costaría a un competidor su posición presentando algo — que es exactamente lo que el legacy permitía, de inmediato y en toda la plataforma.
- **Un valor de standing que esta versión no reconoce se lee como "nada se sostiene".** Un número que una futura versión de PIMA invente no puede convertirse en una acusación contra un operador, y adivinar su peor significado dejaría que esa versión quitara insignias en silencio.

## Verificación — Iteración 5

- `dotnet build Tourism.sln` — correcto, **0 warnings**
- `dotnet test Tourism.sln` — **93/93 en verde** (28 dominio, 45 aplicación, 20 infraestructura)
- El mapeo del cable está probado contra el **cuerpo literal que PIMA devuelve**, capturado de una llamada real y no reconstruido desde el DTO: un cuerpo armado con la misma forma que el lector espera no prueba nada sobre el lector.

---

## Iteración 7 — la página pública es de BIT

El legacy servía el perfil público desde el motor de identidad: una respuesta anónima que fusionaba nombre, hechos de riesgo y datos de negocio turístico — razón social, categoría, "proof of life" —, estos últimos viviendo como columnas en el usuario de Platform y en catálogos sembrados solo con rubros de turismo. El motor tenía que saber cómo se ve una ficha turística.

Ahora **PIMA publica hechos y BIT publica la página**. La etiqueta de categoría vive acá, que es donde "Guías de Turistas" corresponde.

### Decisiones

- **Los hechos se piden primero, y la página existe solo si el motor liberó algo.** Un operador que retiró su consentimiento no tiene página, por más que BIT siga teniendo su fila. Ese es el motivo de que el consentimiento se haga cumplir en un lugar en vez de recordarse en tres — y el orden importa: leer primero los registros propios dejaría los datos de un operador retirado a una rama olvidada de publicarse.

- **Un identificador público por identidad, no por vertical.** BIT direcciona su página con el identificador de PIMA en lugar de acuñar uno propio: una persona que aparece en dos verticales tiene una identidad pública, no dos. Lo que BIT posee es la página; de quién es la página lo dice el motor.

- **El enlace se aprende de una evaluación, no se busca por contacto.** La evaluación de insignia devuelve el identificador y el perfil lo guarda. Buscarlo por contacto sería otra pregunta con otro consentimiento — si un desconocido con un contacto puede confirmar de quién es — y quien pide la evaluación ya sabe exactamente sobre quién preguntó.

- **Un motor caído no es una ficha inexistente.** Se distingue de "no listado", igual que en la decisión de insignia: una dependencia caída no es un veredicto sobre el operador.

- **Un sujeto listado por otra vertical no tiene página turística.** Respuesta propia, porque es una situación real y no un error.

- **El lookup anónimo es un POST con el contacto en el cuerpo.** Un contacto es dato personal y no va en una URL, un log de proxy o un historial. Su predecesor era `GET ?contact=` — y además creaba perfiles de identidad y mandaba correos de activación.

- **Toda respuesta negativa del lookup tiene la misma forma.** Nadie tiene ese contacto, no está listado, no es encontrable, o ese contacto en particular está retirado: cualquier distinción sería una manera de confirmar de quién es un contacto.

## Verificación — Iteración 7

- `dotnet build Tourism.sln` — correcto, **0 warnings**
- `dotnet test Tourism.sln` — **101/101 en verde** (28 dominio, 53 aplicación, 20 infraestructura)
- **Verificado end-to-end contra MariaDB 11.4** con los tres servicios sobre HTTP: la página anónima compone lo turístico (`lodging → Lodging and accommodation`, insignia) con los hechos que PIMA liberó — score y denuncias ausentes cuando el operador no los publicó, correo enmascarado; el lookup anónimo encuentra al encontrable y da 404 al desconocido; y cuando **el operador retira su consentimiento en PIMA, la página y el lookup dan 404** aunque BIT siga teniendo su fila.

**Limitación declarada**: el enlace entre la ficha y el identificador se sembró por SQL para la corrida en vivo. Lo graba la evaluación de insignia, que está cubierta por prueba unitaria pero no se ejerció acá — habría requerido rehacer el onboarding turístico completo con token de participación.
