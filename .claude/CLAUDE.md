# CLAUDE.md — TourismCore (BIT API — vertical Turismo)

Este repo es la **BIT API** (vertical de negocio Turismo), uno de los tres destinos de la migración arquitectónica BIT → Platform / PIMA / BIT en curso.

- Reglas invariantes de la migración (rara vez cambian, definidas centralmente): @MIGRATION_RULES.md
- Estado local de este repo (detalle de implementación específico de Turismo): @MIGRATION_STATE.md
- **El tablero global de la migración** (inventario AS-IS→TO-BE completo, decisiones de ownership confirmadas, riesgos cross-repo) **vive en el repo legacy `BitCore`**, en `.claude/MIGRATION_STATE.md`. Consultarlo siempre antes de implementar un flujo nuevo — es la fuente de verdad de qué le corresponde a BIT y por qué.

## Rol de este repo

BIT deja de ser la plataforma general: este repo representa **exclusivamente** la vertical de negocio Turismo. Debe tender a ser propietario de: workflows turísticos, perfiles turísticos de Organizations, reglas y restricciones propias del negocio turístico, configuración turística, decisiones derivadas de evaluaciones, casos de uso propios de Turismo, orquestación de PIMA necesaria para procesos turísticos.

No debe absorber capacidades transversales (Users/Auth/Tenants → Platform) ni lógica de evaluación de identidad/riesgo (→ PIMA).

Ver el detalle completo de fronteras, restricciones sobre el legacy y checklist de calidad en `MIGRATION_RULES.md`.
