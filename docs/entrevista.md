# Cómo presentar JobPilot en una entrevista

## Pitch de 60 segundos
> "Construí **JobPilot**, una API en **.NET 10** para gestionar mi propia búsqueda de empleo. Usa **Clean Architecture**
> con CQRS y un **mediator propio**, porque MediatR pasó a ser de pago. Validación, logging y transacciones van en
> **decorators genéricos**. El ciclo de vida de una postulación es un **State pattern**, así que las transiciones
> inválidas son imposibles. Tiene IA con **Microsoft.Extensions.AI**: analiza ofertas con structured output, calcula
> el match de mi CV con varias **estrategias** y hace **búsqueda semántica con pgvector**. Los eventos de dominio se
> publican con el patrón **Outbox**. También expone un **servidor MCP** para que un agente como Claude consulte mis
> postulaciones. Lo orquesto con **Aspire**, tiene OpenTelemetry y tests unitarios, de arquitectura y de integración
> con **Testcontainers** contra Postgres real."

## Preguntas probables (con respuesta corta)

### Arquitectura
1. **¿Por qué Clean Architecture y no N-capas?** Porque el dominio no depende de la infraestructura (inversión de dependencias). Puedo probar las reglas sin base de datos, y las reglas de capas se verifican con NetArchTest en CI.
2. **¿Dónde registras la inyección de dependencias?** Cada capa expone `AddApplication()` / `AddInfrastructure()` y `Program.cs` es el *composition root*. No hace falta un proyecto IoC aparte.
3. **¿El repositorio no es redundante con EF Core?** EF ya implementa Repository + UoW. Mi `IRepository<TAggregate,TId>` aísla Application de EF, restringe la persistencia a aggregate roots por la restricción genérica y simplifica los mocks. Es un trade-off consciente.
4. **¿Qué es un aggregate y por qué `Interview` no tiene repositorio?** Es un límite de consistencia: `Interview` solo cambia a través de `JobApplication`, que valida las reglas (por ejemplo, "no hay entrevistas en Wishlist").
5. **¿Cómo manejas los errores?** Con el Result pattern para errores de negocio y excepciones solo para lo inesperado. `ApiResults` los traduce a ProblemDetails (RFC 9457) con `errorCode` y `traceId`.

### Patrones y genéricos
6. **¿Cómo funciona tu mediator?** `SendAsync<TResponse>` conoce TResponse, pero el tipo del comando solo en ejecución. Con `MakeGenericType` creo un wrapper genérico cerrado, lo cacheo en un `ConcurrentDictionary` y a partir de ahí todo es tipado.
7. **¿En qué orden corren los decorators?** Logging → Validation → UnitOfWork → Handler. Con Scrutor, el último `Decorate` queda más afuera.
8. **¿Por qué State y no un enum?** Porque los estados tienen comportamiento (transiciones, si permiten entrevistas). Con enum la lógica se dispersa en `switch`; aquí cada estado conoce sus reglas.
9. **¿Dónde usas Strategy?** En el match CV↔oferta: keyword, embedding o hybrid (este último es un Composite). El handler elige por nombre entre `IEnumerable<IMatchScoringStrategy>`.
10. **¿Qué es `static abstract` en una interfaz?** Permite que el código genérico llame a `TId.From(value)` sin reflexión. Una limitación: no se puede usar dentro de árboles de expresión (CS8927).
11. **¿Qué significa `in` en `IDomainEventHandler<in TEvent>`?** Contravarianza: un handler de un tipo base sirve para eventos derivados.

### Datos
12. **¿Por qué IDs Guid v7?** Son ordenables por tiempo (fragmentan menos el índice que v4) y los genera el dominio, que conoce su ID antes de guardar. Hay que marcarlos `ValueGeneratedNever()`.
13. **¿Qué problema encontraste con EF Core?** Comparar contra un campo estático de un smart enum no se traducía a SQL. Lo detecté con un test que inspecciona `ToQueryString()` y lo resolví con variables locales, que además se convierten en parámetros SQL.
14. **¿Cómo funciona la búsqueda semántica?** Calculo el embedding de la consulta, uso el operador `<=>` de pgvector (distancia coseno) con SQL interpolado parametrizado y convierto la distancia en similitud. Con más volumen agregaría un índice HNSW.
15. **¿Qué es el Outbox y qué problema resuelve?** Evita el "dual write": el evento se guarda en la misma transacción que el cambio y un BackgroundService lo publica. Con varias instancias usaría `FOR UPDATE SKIP LOCKED`, y los handlers deben ser idempotentes.

### IA
16. **¿Cómo garantizas que el LLM devuelva JSON válido?** Con `GetResponseAsync<T>`, que genera un JSON Schema del tipo y pide structured output. Si `TryGetResult` falla, devuelvo `AI.InvalidResponse` sin romper nada.
17. **¿Cómo evitas el vendor lock-in?** Con `IChatClient` de Microsoft.Extensions.AI. Cambiar de Claude a Ollama es configuración.
18. **¿Cómo controlas los costos?** Con rate limiting en los endpoints de IA, embeddings asíncronos solo cuando cambia la descripción y un modelo pequeño para la extracción.
19. **¿Qué es MCP?** Model Context Protocol, un estándar para exponer herramientas a agentes de IA. Mis tools reutilizan los mismos casos de uso (`ISender`) que la API REST.

### Seguridad y multiusuario
- **¿Cómo aíslas los datos entre usuarios?** Con un global query filter de EF Core 10 registrado genéricamente para todo aggregate `IOwnedByUser`. Si alguien agrega una entidad nueva queda protegida sola, y un test de arquitectura exige la interfaz.
- **¿Dónde NO aplican los query filters?** En SQL crudo, en `IgnoreQueryFilters()` y en procesos sin usuario. Por eso la búsqueda vectorial filtra por `OwnerId` explícitamente y el Outbox corre en modo sistema.
- **¿Por qué 404 y no 403 para recursos ajenos?** Un 403 confirma que el recurso existe (enumeración de IDs). Con el filtro, para el otro usuario simplemente no existe.
- **¿Qué tiene que ver la caché con la seguridad?** Una clave global (`pipeline-stats`) le mostraría a un usuario las métricas de otro. Las claves llevan el `userId`.
- **¿Identity bearer o JWT?** Identity API endpoints es lo más rápido y seguro para una sola API. Con varios servicios usaría JWT o un proveedor como Entra ID o Keycloak.

### Testing y operación
20. **¿Por qué Testcontainers y no InMemory?** El proveedor InMemory no es relacional: no prueba SQL, constraints ni pgvector. Testcontainers usa el mismo Postgres que producción.
21. **¿Qué son los tests de arquitectura?** Reglas como "Domain no depende de EF Core" convertidas en tests que fallan en CI.
22. **¿Cómo pruebas código que depende de la hora?** Con `TimeProvider` inyectado y `FakeTimeProvider` en los tests.
23. **¿Qué aporta Aspire?** Orquestación local (Postgres + API con un comando), service defaults (OpenTelemetry, health checks, resiliencia) y un dashboard con trazas de SQL y del LLM.

### Conductuales (método STAR)
24. **"Cuéntame de un bug difícil."** Usa la traducción de EF del smart enum: situación, cómo lo diagnosticaste con un test de `ToQueryString`, la solución y el aprendizaje.
25. **"¿Qué mejorarías?"** Row-Level Security en PostgreSQL como segunda capa de aislamiento, índice HNSW, Outbox con `SKIP LOCKED`, un frontend y el despliegue en Azure Container Apps.

> Practica con la skill `interview-prep` de Claude Code: te hará estas preguntas con repreguntas y te dará feedback.
