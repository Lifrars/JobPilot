# ADR 0003 — PostgreSQL + pgvector como base relacional y vectorial

- **Estado:** aceptada · **Fecha:** 2026-10-26

## Contexto
Necesitamos datos relacionales (postulaciones, estados) y búsqueda por similitud semántica (embeddings).

## Decisión
PostgreSQL 17 con la extensión **pgvector** (imagen `pgvector/pgvector:pg17`), mapeada con `Pgvector.EntityFrameworkCore`.
El dominio guarda `float[]` (no conoce pgvector); `FloatArrayToVectorConverter` hace la traducción.
La columna es `vector` sin dimensión fija para poder cambiar de modelo de embeddings.

## Alternativas
- Base vectorial dedicada (Qdrant, Pinecone, Azure AI Search): otro servicio más que operar y sincronizar.
- SQL Server 2025 (tipo `vector`): válida, pero Postgres es gratis y domina en startups.

## Consecuencias
- ✅ Un solo motor, transacciones ACID entre datos y vectores.
- ⚠️ Sin dimensión fija no se puede crear un índice HNSW. Con volumen real: fijar la dimensión (p. ej. `vector(768)`) y agregar `HasIndex(...).HasMethod("hnsw").HasOperators("vector_cosine_ops")`.
- ⚠️ Si se cambia de modelo de embeddings hay que regenerarlos todos (no se pueden mezclar dimensiones).
